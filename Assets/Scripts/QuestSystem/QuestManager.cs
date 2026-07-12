using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using Random = UnityEngine.Random;

// Core of the quest system. The server owns every decision; clients only send request RPCs and
// read the synchronized quest list.
//
// Not implemented yet (plan steps 5-7): island-bound deferred spawning, expiry, quest chains.
public class QuestManager : NetworkBehaviour
{
    public static QuestManager Instance { get; private set; }

    [Header("Data")]
    [SerializeField] private QuestDatabase database;

    [Header("Entity Prefabs")]
    [Tooltip("Must have NetworkObject + QuestEntity + a collider, and be registered in NetworkManager's network prefabs.")]
    [SerializeField] private GameObject questNpcPrefab;
    [SerializeField] private GameObject questItemPrefab;

    [Header("Board Generation")]
    [Min(1)][SerializeField] private int minQuestsPerBoard = 6;
    [Min(1)][SerializeField] private int maxQuestsPerBoard = 8;

    [Header("Reward Scaling")]
    [Tooltip("Gold grows by this fraction of the base roll for every in-game day that has passed.")]
    [Min(0f)][SerializeField] private float goldPerDayFactor = 0.1f;

    [Header("Lifetime")]
    [Tooltip("Days a quest stays in the list before it expires. Expiry itself lands in a later step.")]
    [Min(1)][SerializeField] private int expiryDays = 3;

    [Header("Debug")]
    [Tooltip("Server-side: prints the raw parameters of every generated quest. Development aid only.")]
    [SerializeField] private bool logGeneration = true;

    // Synchronized quest list. Server-write by default, readable by everyone, and late-join safe:
    // a client that connects later receives the full list automatically.
    // NGO disposes NetworkVariable/NetworkList fields itself in NetworkBehaviour.OnDestroy(),
    // so this must not be disposed by hand.
    private NetworkList<QuestInstanceState> questStates = new NetworkList<QuestInstanceState>();

    // A cheap stat for future progression work. Nothing consumes it yet.
    private NetworkVariable<int> totalQuestsCompleted = new NetworkVariable<int>(0);

    // Server-only bookkeeping. Deliberately not networked: it is meaningless on clients.
    private readonly Dictionary<int, int> lastGeneratedDayByBoard = new Dictionary<int, int>();
    private readonly Dictionary<int, List<NetworkObject>> spawnedEntitiesByQuest = new Dictionary<int, List<NetworkObject>>();
    private readonly Dictionary<int, List<QuestSpawnPoint>> reservedPointsByQuest = new Dictionary<int, List<QuestSpawnPoint>>();
    private readonly HashSet<QuestSpawnPoint> occupiedPoints = new HashSet<QuestSpawnPoint>();
    private readonly List<QuestTemplate> eligibleTemplates = new List<QuestTemplate>();
    private readonly List<int> usedNpcNamesInBatch = new List<int>();
    private readonly List<int> freeNpcNameIndices = new List<int>();
    private int nextInstanceId = 1;

    public QuestDatabase Database => database;
    public NetworkList<QuestInstanceState> QuestStates => questStates;
    public int ExpiryDays => expiryDays;
    public int TotalQuestsCompleted => totalQuestsCompleted.Value;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public override void OnDestroy()
    {
        if (Instance == this) Instance = null;

        base.OnDestroy();
    }

    // ---------------------------------------------------------------- board request

    // Any client may ask a board for quests, so the invoke permission is explicit even though
    // Everyone is the NGO default: this is the behaviour we depend on, not an accident.
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void RequestBoardQuestsRpc(int boardIndex)
    {
        GenerateBoardQuests(boardIndex);
    }

    private void GenerateBoardQuests(int boardIndex)
    {
        if (!IsServer) return;

        if (database == null)
        {
            Debug.LogError("QuestManager: no QuestDatabase assigned, cannot generate quests.");
            return;
        }

        int day = GameDayClock.Instance != null ? GameDayClock.Instance.CurrentDay : 1;

        // One batch per board per in-game day. Re-reading a board simply shows the same notices.
        if (lastGeneratedDayByBoard.TryGetValue(boardIndex, out int lastDay) && lastDay == day)
        {
            Debug.Log($"QuestManager: board {boardIndex} already generated quests on day {day}.");
            return;
        }

        CollectEligibleTemplates(day);

        if (eligibleTemplates.Count == 0)
        {
            Debug.LogWarning($"QuestManager: no eligible quest template for day {day}. " +
                             "Check boardSelectable, minDay, and whether free QuestSpawnPoints exist.");
            return;
        }

        lastGeneratedDayByBoard[boardIndex] = day;
        usedNpcNamesInBatch.Clear();

        int questCount = Random.Range(minQuestsPerBoard, Mathf.Max(minQuestsPerBoard, maxQuestsPerBoard) + 1);

        if (logGeneration)
        {
            Debug.Log($"QuestManager: day {day}, board {boardIndex}, generating up to {questCount} quests. " +
                      $"Pools -> eligible templates: {eligibleTemplates.Count}, npcNames: {database.NpcNameCount}, " +
                      $"itemNames: {database.ItemNameCount}, free spawn points: npc={CountFreePoints(QuestSpawnPoint.SpawnKind.Npc)}, " +
                      $"item={CountFreePoints(QuestSpawnPoint.SpawnKind.Item)}");
        }

        for (int i = 0; i < questCount; i++)
        {
            // Recomputed every slot: each spawned quest consumes spawn points, which can make some
            // templates impossible for the remaining slots.
            CollectEligibleTemplates(day);
            if (eligibleTemplates.Count == 0) break;

            QuestTemplate template = PickWeightedTemplate();
            if (template == null) continue;

            TryCreateQuest(template, day);
        }
    }

    // ---------------------------------------------------------------- quest creation

    private void TryCreateQuest(QuestTemplate template, int day)
    {
        if (!TryReserveDestinations(template, out QuestSpawnPoint giverPoint, out QuestSpawnPoint itemPoint))
        {
            return;
        }

        // The location shown to the player is the place they must travel to: the item's hiding place
        // for FetchDeliver, the giver's place for TalkTo.
        QuestSpawnPoint locationSource = itemPoint != null ? itemPoint : giverPoint;

        QuestInstanceState state = CreateQuestState(template, day, locationSource.LocationNameIndex);

        if (!SpawnQuestEntities(state, template, giverPoint, itemPoint))
        {
            ReleasePoints(giverPoint, itemPoint);
            return;
        }

        questStates.Add(state);

        if (logGeneration)
        {
            Debug.Log($"QuestManager: quest #{state.InstanceId} -> templateIndex={state.TemplateIndex} " +
                      $"({template.name}), npcNameIndex={state.NpcNameIndex}, itemNameIndex={state.ItemNameIndex}, " +
                      $"locationNameIndex={state.LocationNameIndex}, gold={state.GoldReward}");
        }
    }

    private QuestInstanceState CreateQuestState(QuestTemplate template, int day, int locationNameIndex)
    {
        int itemNameIndex = -1;

        // One switch per concern: adding a QuestType means visiting this switch, not hunting for
        // scattered type checks.
        switch (template.Type)
        {
            case QuestType.TalkTo:
                break;
            case QuestType.FetchDeliver:
                itemNameIndex = PickIndex(database.ItemNameCount);
                break;
            default:
                Debug.LogWarning($"QuestManager: unsupported quest type {template.Type} while filling parameters.");
                break;
        }

        return new QuestInstanceState
        {
            InstanceId = nextInstanceId++,
            TemplateIndex = database.GetTemplateIndex(template),
            NpcNameIndex = PickNpcNameIndex(),
            ItemNameIndex = itemNameIndex,
            LocationNameIndex = locationNameIndex,
            GoldReward = RollGold(template, day),
            CreatedDay = day,
            ChainStepIndex = 0,
            Status = QuestStatus.Active
        };
    }

    private void CollectEligibleTemplates(int day)
    {
        eligibleTemplates.Clear();

        for (int i = 0; i < database.TemplateCount; i++)
        {
            QuestTemplate template = database.GetTemplate(i);
            if (template == null) continue;
            if (!template.BoardSelectable) continue;   // chain follow-up links never appear on boards
            if (template.MinDay > day) continue;
            if (!CanSatisfyDestinations(template)) continue;

            eligibleTemplates.Add(template);
        }
    }

    private QuestTemplate PickWeightedTemplate()
    {
        float totalWeight = 0f;

        for (int i = 0; i < eligibleTemplates.Count; i++)
        {
            totalWeight += Mathf.Max(0f, eligibleTemplates[i].Weight);
        }

        // Every weight is zero (or negative): fall back to a plain uniform pick.
        if (totalWeight <= 0f)
        {
            return eligibleTemplates[Random.Range(0, eligibleTemplates.Count)];
        }

        float roll = Random.Range(0f, totalWeight);

        for (int i = 0; i < eligibleTemplates.Count; i++)
        {
            roll -= Mathf.Max(0f, eligibleTemplates[i].Weight);
            if (roll <= 0f) return eligibleTemplates[i];
        }

        return eligibleTemplates[eligibleTemplates.Count - 1];
    }

    // Avoids repeating an NPC name inside one board batch; once the pool runs out, repeats are allowed.
    private int PickNpcNameIndex()
    {
        int count = database.NpcNameCount;
        if (count == 0) return -1;

        if (usedNpcNamesInBatch.Count >= count) usedNpcNamesInBatch.Clear();

        freeNpcNameIndices.Clear();

        for (int i = 0; i < count; i++)
        {
            if (!usedNpcNamesInBatch.Contains(i)) freeNpcNameIndices.Add(i);
        }

        int picked = freeNpcNameIndices[Random.Range(0, freeNpcNameIndices.Count)];
        usedNpcNamesInBatch.Add(picked);

        return picked;
    }

    private int RollGold(QuestTemplate template, int day)
    {
        int baseGold = Random.Range(template.MinGold, template.MaxGold + 1);
        float dayScale = 1f + (day - 1) * goldPerDayFactor;

        return Mathf.RoundToInt(baseGold * dayScale);
    }

    private static int PickIndex(int count)
    {
        return count > 0 ? Random.Range(0, count) : -1;
    }

    // ---------------------------------------------------------------- destinations & spawning

    private bool CanSatisfyDestinations(QuestTemplate template)
    {
        switch (template.Type)
        {
            case QuestType.TalkTo:
                return CountFreePoints(QuestSpawnPoint.SpawnKind.Npc) > 0;
            case QuestType.FetchDeliver:
                return CountFreePoints(QuestSpawnPoint.SpawnKind.Npc) > 0
                    && CountFreePoints(QuestSpawnPoint.SpawnKind.Item) > 0;
            default:
                Debug.LogWarning($"QuestManager: unsupported quest type {template.Type} while checking destinations.");
                return false;
        }
    }

    private bool TryReserveDestinations(QuestTemplate template, out QuestSpawnPoint giverPoint, out QuestSpawnPoint itemPoint)
    {
        giverPoint = null;
        itemPoint = null;

        giverPoint = FindFreePoint(QuestSpawnPoint.SpawnKind.Npc);
        if (giverPoint == null) return false;

        if (template.Type == QuestType.FetchDeliver)
        {
            itemPoint = FindFreePoint(QuestSpawnPoint.SpawnKind.Item);
            if (itemPoint == null) return false;   // nothing reserved yet, so nothing to roll back
        }

        occupiedPoints.Add(giverPoint);
        if (itemPoint != null) occupiedPoints.Add(itemPoint);

        return true;
    }

    private bool SpawnQuestEntities(QuestInstanceState state, QuestTemplate template, QuestSpawnPoint giverPoint, QuestSpawnPoint itemPoint)
    {
        NetworkObject giver = SpawnQuestEntity(questNpcPrefab, giverPoint, state.InstanceId, QuestEntityKind.Giver);
        if (giver == null) return false;

        List<NetworkObject> entities = new List<NetworkObject> { giver };
        List<QuestSpawnPoint> points = new List<QuestSpawnPoint> { giverPoint };

        if (template.Type == QuestType.FetchDeliver)
        {
            NetworkObject item = SpawnQuestEntity(questItemPrefab, itemPoint, state.InstanceId, QuestEntityKind.Item);

            if (item == null)
            {
                // Roll back the giver: half a quest is worse than no quest.
                giver.Despawn(true);
                return false;
            }

            entities.Add(item);
            points.Add(itemPoint);
        }

        spawnedEntitiesByQuest[state.InstanceId] = entities;
        reservedPointsByQuest[state.InstanceId] = points;

        return true;
    }

    private NetworkObject SpawnQuestEntity(GameObject prefab, QuestSpawnPoint point, int questInstanceId, QuestEntityKind kind)
    {
        if (prefab == null)
        {
            Debug.LogError($"QuestManager: no prefab assigned for quest entity kind {kind}.");
            return null;
        }

        GameObject instance = Instantiate(prefab, point.transform.position, point.transform.rotation);

        if (!instance.TryGetComponent(out QuestEntity entity) || !instance.TryGetComponent(out NetworkObject networkObject))
        {
            Debug.LogError($"QuestManager: prefab '{prefab.name}' needs both QuestEntity and NetworkObject components.");
            Destroy(instance);
            return null;
        }

        // Hand the identity over before Spawn(); QuestEntity copies it into its NetworkVariables
        // inside OnNetworkSpawn, which still happens before the spawn payload is serialized.
        entity.ServerInitialize(questInstanceId, kind);
        networkObject.Spawn();

        return networkObject;
    }

    private QuestSpawnPoint FindFreePoint(QuestSpawnPoint.SpawnKind kind)
    {
        for (int i = 0; i < QuestSpawnPoint.All.Count; i++)
        {
            QuestSpawnPoint point = QuestSpawnPoint.All[i];
            if (point == null) continue;
            if (point.Kind != kind) continue;
            if (occupiedPoints.Contains(point)) continue;

            return point;
        }

        return null;
    }

    private int CountFreePoints(QuestSpawnPoint.SpawnKind kind)
    {
        int count = 0;

        for (int i = 0; i < QuestSpawnPoint.All.Count; i++)
        {
            QuestSpawnPoint point = QuestSpawnPoint.All[i];
            if (point == null) continue;
            if (point.Kind != kind) continue;
            if (occupiedPoints.Contains(point)) continue;

            count++;
        }

        return count;
    }

    private void ReleasePoints(QuestSpawnPoint giverPoint, QuestSpawnPoint itemPoint)
    {
        if (giverPoint != null) occupiedPoints.Remove(giverPoint);
        if (itemPoint != null) occupiedPoints.Remove(itemPoint);
    }

    // ---------------------------------------------------------------- interaction & completion

    // Called from QuestEntity's server RPC. Server-only.
    public void HandleEntityInteracted(QuestEntity entity)
    {
        if (!IsServer || entity == null) return;

        int index = FindQuestIndex(entity.QuestInstanceId);
        if (index < 0) return;   // quest already gone (finished or expired): ignore silently

        QuestInstanceState state = questStates[index];
        QuestTemplate template = database != null ? database.GetTemplate(state.TemplateIndex) : null;
        if (template == null) return;

        switch (entity.Kind)
        {
            case QuestEntityKind.Item:
                if (state.Status != QuestStatus.Active) return;

                state.Status = QuestStatus.ItemCollected;
                questStates[index] = state;   // structs are copies: write the modified value back

                DespawnEntity(state.InstanceId, entity.NetworkObject);
                break;

            case QuestEntityKind.Giver:
                if (!CanTurnIn(template, state)) return;

                CompleteQuest(index, state);
                break;
        }
    }

    // Talking to the giver too early does nothing: the item has not been picked up yet.
    private bool CanTurnIn(QuestTemplate template, QuestInstanceState state)
    {
        switch (template.Type)
        {
            case QuestType.TalkTo:
                return state.Status == QuestStatus.Active;
            case QuestType.FetchDeliver:
                return state.Status == QuestStatus.ItemCollected;
            default:
                Debug.LogWarning($"QuestManager: unsupported quest type {template.Type} on turn-in.");
                return false;
        }
    }

    private void CompleteQuest(int index, QuestInstanceState state)
    {
        if (CrewGold.Instance != null) CrewGold.Instance.AddGold(state.GoldReward);

        totalQuestsCompleted.Value++;

        DespawnQuestEntities(state.InstanceId);
        ReleaseQuestPoints(state.InstanceId);

        questStates.RemoveAt(index);

        Debug.Log($"QuestManager: quest #{state.InstanceId} completed, {state.GoldReward} gold paid to the crew.");
    }

    private int FindQuestIndex(int instanceId)
    {
        for (int i = 0; i < questStates.Count; i++)
        {
            if (questStates[i].InstanceId == instanceId) return i;
        }

        return -1;
    }

    private void DespawnEntity(int questInstanceId, NetworkObject networkObject)
    {
        if (networkObject == null) return;

        if (spawnedEntitiesByQuest.TryGetValue(questInstanceId, out List<NetworkObject> entities))
        {
            entities.Remove(networkObject);
        }

        if (networkObject.IsSpawned) networkObject.Despawn(true);
    }

    private void DespawnQuestEntities(int questInstanceId)
    {
        if (!spawnedEntitiesByQuest.TryGetValue(questInstanceId, out List<NetworkObject> entities)) return;

        for (int i = 0; i < entities.Count; i++)
        {
            NetworkObject networkObject = entities[i];
            if (networkObject == null) continue;
            if (networkObject.IsSpawned) networkObject.Despawn(true);
        }

        spawnedEntitiesByQuest.Remove(questInstanceId);
    }

    private void ReleaseQuestPoints(int questInstanceId)
    {
        if (!reservedPointsByQuest.TryGetValue(questInstanceId, out List<QuestSpawnPoint> points)) return;

        for (int i = 0; i < points.Count; i++)
        {
            if (points[i] != null) occupiedPoints.Remove(points[i]);
        }

        reservedPointsByQuest.Remove(questInstanceId);
    }
}
