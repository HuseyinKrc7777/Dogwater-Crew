using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using Random = UnityEngine.Random;

// Core of the quest system. The server owns every decision; clients only send request RPCs and
// read the synchronized quest list.
//
// Not implemented yet (plan steps 6-7): expiry, quest chains.
public class QuestManager : NetworkBehaviour
{
    public static QuestManager Instance { get; private set; }

    [Header("Data")]
    [SerializeField] private QuestDatabase database;

    [Header("Entity Prefabs")]
    [Tooltip("Must have NetworkObject + QuestEntity + a collider, and be registered in NetworkManager's network prefabs.")]
    [SerializeField] private GameObject questNpcPrefab;
    [SerializeField] private GameObject questItemPrefab;

    [Header("Destinations")]
    [Tooltip("Islands that quests may be sent to. Drag in the same IslandDefinition assets the islands' " +
             "triggers and spawn points use. An island is never 'full': several quests can target the same " +
             "one, and their entities queue up until its spawn points are in the world.")]
    [SerializeField] private IslandDefinition[] islandDestinations;

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

    // Where one quest entity is supposed to end up. Either a persistent spawn point (exclusive: it is
    // taken the moment the entity stands on it) or an island (soft capacity: never taken).
    private struct QuestDestination
    {
        public QuestSpawnPoint Point;   // null for an island destination
        public string IslandName;       // empty for a persistent destination
        public int LocationNameIndex;
    }

    // Server-only. One record per entity a quest needs. The entity is either standing in the world
    // (Instance != null) or waiting for its island to load (Instance == null: pending). Both states
    // live in the same list on purpose - an island unloading just moves a record from one to the other.
    private class QuestEntityRecord
    {
        public int QuestInstanceId;
        public QuestEntityKind Kind;
        public string IslandName;        // empty => persistent destination, so it is never pending
        public QuestSpawnPoint Point;    // null while pending
        public NetworkObject Instance;   // null while pending
    }

    // Synchronized quest list. Server-write by default, readable by everyone, and late-join safe:
    // a client that connects later receives the full list automatically.
    // NGO disposes NetworkVariable/NetworkList fields itself in NetworkBehaviour.OnDestroy(),
    // so this must not be disposed by hand.
    private NetworkList<QuestInstanceState> questStates = new NetworkList<QuestInstanceState>();

    // A cheap stat for future progression work. Nothing consumes it yet.
    private NetworkVariable<int> totalQuestsCompleted = new NetworkVariable<int>(0);

    // Server-only bookkeeping. Deliberately not networked: it is meaningless on clients. Clients only
    // ever see spawned NetworkObjects, which is why late-join keeps working without extra code.
    private readonly Dictionary<int, int> lastGeneratedDayByBoard = new Dictionary<int, int>();
    private readonly List<QuestEntityRecord> entityRecords = new List<QuestEntityRecord>();
    private readonly List<QuestTemplate> eligibleTemplates = new List<QuestTemplate>();
    private readonly List<QuestDestination> destinationCandidates = new List<QuestDestination>();
    private readonly List<int> usedNpcNamesInBatch = new List<int>();
    private readonly List<int> freeNpcNameIndices = new List<int>();
    private readonly HashSet<string> warnedIslands = new HashSet<string>();
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

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // Island scenes are loaded on every peer, so spawn points register on clients too.
        // Only the server spawns anything, so only the server listens.
        if (!IsServer) return;

        ValidateIslandDestinations();

        QuestSpawnPoint.Registered += HandleSpawnPointRegistered;
        QuestSpawnPoint.Deregistered += HandleSpawnPointDeregistered;
    }

    public override void OnNetworkDespawn()
    {
        UnsubscribeFromSpawnPoints();

        base.OnNetworkDespawn();
    }

    public override void OnDestroy()
    {
        // A static event outlives this object: a listener that is never removed keeps a destroyed
        // manager reachable. Unsubscribing twice is harmless, so do it here as well.
        UnsubscribeFromSpawnPoints();

        if (Instance == this) Instance = null;

        base.OnDestroy();
    }

    private void UnsubscribeFromSpawnPoints()
    {
        QuestSpawnPoint.Registered -= HandleSpawnPointRegistered;
        QuestSpawnPoint.Deregistered -= HandleSpawnPointDeregistered;
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
                             "Check boardSelectable, minDay, and whether any destination exists " +
                             "(a free persistent QuestSpawnPoint or an island destination).");
            return;
        }

        lastGeneratedDayByBoard[boardIndex] = day;
        usedNpcNamesInBatch.Clear();

        int questCount = Random.Range(minQuestsPerBoard, Mathf.Max(minQuestsPerBoard, maxQuestsPerBoard) + 1);

        if (logGeneration)
        {
            Debug.Log($"QuestManager: day {day}, board {boardIndex}, generating up to {questCount} quests. " +
                      $"Pools -> eligible templates: {eligibleTemplates.Count}, npcNames: {database.NpcNameCount}, " +
                      $"itemNames: {database.ItemNameCount}, free persistent points: " +
                      $"npc={CountFreePoints(QuestSpawnPoint.SpawnKind.Npc, string.Empty)}, " +
                      $"item={CountFreePoints(QuestSpawnPoint.SpawnKind.Item, string.Empty)}, " +
                      $"island destinations: {CountIslandDestinations()}");
        }

        for (int i = 0; i < questCount; i++)
        {
            // Recomputed every slot: each spawned quest consumes persistent spawn points, which can
            // make some templates impossible for the remaining slots.
            CollectEligibleTemplates(day);
            if (eligibleTemplates.Count == 0) break;

            QuestTemplate template = PickWeightedTemplate();
            if (template == null) continue;

            TryCreateQuest(template, day);
        }

        // Island-bound entities were only queued above. Drain the queue now: the target island may
        // already be loaded, and then its NPC has to be standing there immediately, not on the next load.
        ResolvePendingSpawns();
    }

    // ---------------------------------------------------------------- quest creation

    private void TryCreateQuest(QuestTemplate template, int day)
    {
        if (!TryPickDestinations(template, out QuestDestination giverDestination, out QuestDestination itemDestination))
        {
            return;
        }

        // The location shown to the player is the place they must travel to: the item's hiding place
        // for FetchDeliver, the giver's place for TalkTo.
        QuestDestination shownDestination = RequiresItemEntity(template.Type) ? itemDestination : giverDestination;

        QuestInstanceState state = CreateQuestState(template, day, shownDestination.LocationNameIndex);

        // Nothing is reserved until an entity is actually placed, so a failure here needs no rollback
        // beyond what PlaceQuestEntities does itself.
        if (!PlaceQuestEntities(state.InstanceId, template, giverDestination, itemDestination))
        {
            return;
        }

        questStates.Add(state);

        if (logGeneration)
        {
            Debug.Log($"QuestManager: quest #{state.InstanceId} -> templateIndex={state.TemplateIndex} " +
                      $"({template.name}), npcNameIndex={state.NpcNameIndex}, itemNameIndex={state.ItemNameIndex}, " +
                      $"locationNameIndex={state.LocationNameIndex}, gold={state.GoldReward}, " +
                      $"giver -> {Describe(giverDestination)}" +
                      (RequiresItemEntity(template.Type) ? $", item -> {Describe(itemDestination)}" : string.Empty));
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

    // ---------------------------------------------------------------- destinations

    // The single place that maps a quest type to the entities it needs. Destination picking, spawning
    // and the shown location all derive from it, so a new QuestType is taught here once instead of in
    // three places. Returns false for a type this manager has no entity layout for - such a template
    // is then never eligible, so it can never produce an uncompletable quest.
    private static bool TryGetRequiredEntities(QuestType type, out bool needsItemEntity)
    {
        needsItemEntity = false;

        switch (type)
        {
            case QuestType.TalkTo:
                return true;
            case QuestType.FetchDeliver:
                needsItemEntity = true;
                return true;
            default:
                Debug.LogWarning($"QuestManager: unsupported quest type {type}; it has no entity layout.");
                return false;
        }
    }

    private static bool RequiresItemEntity(QuestType type)
    {
        TryGetRequiredEntities(type, out bool needsItemEntity);

        return needsItemEntity;
    }

    private bool CanSatisfyDestinations(QuestTemplate template)
    {
        if (!TryGetRequiredEntities(template.Type, out bool needsItem)) return false;

        if (!HasDestination(QuestEntityKind.Giver)) return false;
        if (needsItem && !HasDestination(QuestEntityKind.Item)) return false;

        return true;
    }

    private bool HasDestination(QuestEntityKind kind)
    {
        // An island is never full, so as soon as one is catalogued there is always somewhere to go.
        if (CountIslandDestinations() > 0) return true;

        return FindFreePoint(ToSpawnKind(kind), string.Empty) != null;
    }

    // Persistent points and islands compete as equals. No tuning knob: persistent points are consumed
    // as quests take them while islands never are, so a batch drifts towards the islands by itself.
    private bool TryPickDestinations(QuestTemplate template, out QuestDestination giverDestination, out QuestDestination itemDestination)
    {
        giverDestination = default;
        itemDestination = default;

        if (!TryGetRequiredEntities(template.Type, out bool needsItem)) return false;

        if (!TryPickDestination(QuestEntityKind.Giver, out giverDestination)) return false;
        if (needsItem && !TryPickDestination(QuestEntityKind.Item, out itemDestination)) return false;

        // Nothing is occupied here: a destination is only taken once an entity is placed on it. The
        // giver and the item never collide anyway - they need points of different kinds.
        return true;
    }

    private bool TryPickDestination(QuestEntityKind kind, out QuestDestination destination)
    {
        destination = default;
        destinationCandidates.Clear();

        QuestSpawnPoint.SpawnKind spawnKind = ToSpawnKind(kind);

        for (int i = 0; i < QuestSpawnPoint.All.Count; i++)
        {
            QuestSpawnPoint point = QuestSpawnPoint.All[i];
            if (!IsFreePoint(point, spawnKind, string.Empty)) continue;

            destinationCandidates.Add(new QuestDestination
            {
                Point = point,
                IslandName = string.Empty,
                LocationNameIndex = point.LocationNameIndex
            });
        }

        if (islandDestinations != null)
        {
            for (int i = 0; i < islandDestinations.Length; i++)
            {
                IslandDefinition island = islandDestinations[i];
                if (island == null || string.IsNullOrEmpty(island.SceneName)) continue;

                destinationCandidates.Add(new QuestDestination
                {
                    Point = null,
                    IslandName = island.SceneName,
                    LocationNameIndex = island.LocationNameIndex
                });
            }
        }

        if (destinationCandidates.Count == 0) return false;

        destination = destinationCandidates[Random.Range(0, destinationCandidates.Count)];

        return true;
    }

    // Generation may only ever pick points whose islandName is empty; island points are reachable
    // exclusively through the catalog and the pending queue. That invariant is what guarantees no
    // entity is ever left standing where an island used to be: everything on an island got there
    // through a record that knows which island it belongs to.
    private bool IsFreePoint(QuestSpawnPoint point, QuestSpawnPoint.SpawnKind spawnKind, string islandName)
    {
        if (point == null) return false;
        if (point.Kind != spawnKind) return false;
        if (!string.Equals(point.IslandName, islandName, StringComparison.Ordinal)) return false;

        return !IsOccupied(point);
    }

    // Occupancy is derived, not stored: a point is taken when an entity record sits on it. One list is
    // the single source of truth, so no second structure can fall out of sync with it. The lists hold
    // a couple of dozen entries at most, and this only runs when a board is read or an island loads.
    private bool IsOccupied(QuestSpawnPoint point)
    {
        for (int i = 0; i < entityRecords.Count; i++)
        {
            if (entityRecords[i].Point == point) return true;
        }

        return false;
    }

    private QuestSpawnPoint FindFreePoint(QuestSpawnPoint.SpawnKind spawnKind, string islandName)
    {
        for (int i = 0; i < QuestSpawnPoint.All.Count; i++)
        {
            QuestSpawnPoint point = QuestSpawnPoint.All[i];
            if (IsFreePoint(point, spawnKind, islandName)) return point;
        }

        return null;
    }

    private int CountFreePoints(QuestSpawnPoint.SpawnKind spawnKind, string islandName)
    {
        int count = 0;

        for (int i = 0; i < QuestSpawnPoint.All.Count; i++)
        {
            if (IsFreePoint(QuestSpawnPoint.All[i], spawnKind, islandName)) count++;
        }

        return count;
    }

    private int CountIslandDestinations()
    {
        if (islandDestinations == null) return 0;

        int count = 0;

        for (int i = 0; i < islandDestinations.Length; i++)
        {
            IslandDefinition island = islandDestinations[i];
            if (island == null || string.IsNullOrEmpty(island.SceneName)) continue;

            count++;
        }

        return count;
    }

    // The island name can no longer be mistyped (it comes from the asset), but its display name still
    // points into the database by index, and that can be left at -1 or aimed past the end of the pool.
    // Both would only show up as a "???" in a quest text hours later, so they are reported at startup.
    private void ValidateIslandDestinations()
    {
        if (islandDestinations == null || database == null) return;

        for (int i = 0; i < islandDestinations.Length; i++)
        {
            IslandDefinition island = islandDestinations[i];

            if (island == null)
            {
                Debug.LogWarning($"QuestManager: island destination {i} is empty.", this);
                continue;
            }

            if (string.IsNullOrEmpty(island.SceneName))
            {
                Debug.LogError($"QuestManager: island definition '{island.name}' has no sceneName.", island);
                continue;
            }

            if (island.LocationNameIndex < 0 || island.LocationNameIndex >= database.LocationNameCount)
            {
                Debug.LogWarning($"QuestManager: island '{island.name}' has locationNameIndex " +
                                 $"{island.LocationNameIndex}, which is not a name in the database " +
                                 $"({database.LocationNameCount} location names). Quests sent there will " +
                                 "show \"???\" as their location.", island);
            }
        }
    }

    private static bool IsIslandLoaded(string islandName)
    {
        for (int i = 0; i < QuestSpawnPoint.All.Count; i++)
        {
            QuestSpawnPoint point = QuestSpawnPoint.All[i];
            if (point == null) continue;

            if (string.Equals(point.IslandName, islandName, StringComparison.Ordinal)) return true;
        }

        return false;
    }

    // ---------------------------------------------------------------- placing entities

    private bool PlaceQuestEntities(int questInstanceId, QuestTemplate template, QuestDestination giverDestination, QuestDestination itemDestination)
    {
        if (!TryGetRequiredEntities(template.Type, out bool needsItem)) return false;

        if (!TryPlaceEntity(questInstanceId, QuestEntityKind.Giver, giverDestination)) return false;

        if (needsItem && !TryPlaceEntity(questInstanceId, QuestEntityKind.Item, itemDestination))
        {
            // Roll the giver back, spawned or merely queued: half a quest is worse than no quest.
            RemoveQuestEntities(questInstanceId);
            return false;
        }

        return true;
    }

    private bool TryPlaceEntity(int questInstanceId, QuestEntityKind kind, QuestDestination destination)
    {
        QuestEntityRecord record = new QuestEntityRecord
        {
            QuestInstanceId = questInstanceId,
            Kind = kind,
            IslandName = destination.IslandName
        };

        // An island destination instantiates nothing right now - the island may not even be in the
        // world. The record waits in the queue and ResolvePendingSpawns() turns it into a real object
        // as soon as a matching spawn point registers (immediately, if the island is already loaded).
        if (!string.IsNullOrEmpty(destination.IslandName))
        {
            entityRecords.Add(record);
            return true;
        }

        NetworkObject instance = SpawnQuestEntity(GetPrefab(kind), destination.Point, questInstanceId, kind);
        if (instance == null) return false;

        record.Point = destination.Point;
        record.Instance = instance;
        entityRecords.Add(record);

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

        // Pin the entity to this manager's own scene (the ship's world) instead of trusting whichever
        // scene happens to be active: Instantiate drops a parentless object into the ACTIVE scene, and an
        // island scene can end up active. An entity living in the island's scene would be destroyed by
        // Unity when that scene unloads - behind this manager's back, before it can despawn it and put the
        // record back in the pending queue. Its position is unaffected; it still stands on the island.
        if (instance.scene != gameObject.scene)
        {
            SceneManager.MoveGameObjectToScene(instance, gameObject.scene);
        }

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

    private GameObject GetPrefab(QuestEntityKind kind)
    {
        switch (kind)
        {
            case QuestEntityKind.Giver: return questNpcPrefab;
            case QuestEntityKind.Item: return questItemPrefab;
            default:
                Debug.LogWarning($"QuestManager: no prefab mapped for quest entity kind {kind}.");
                return null;
        }
    }

    private static QuestSpawnPoint.SpawnKind ToSpawnKind(QuestEntityKind kind)
    {
        switch (kind)
        {
            case QuestEntityKind.Giver: return QuestSpawnPoint.SpawnKind.Npc;
            case QuestEntityKind.Item: return QuestSpawnPoint.SpawnKind.Item;
            default:
                Debug.LogWarning($"QuestManager: no spawn point kind mapped for quest entity kind {kind}.");
                return QuestSpawnPoint.SpawnKind.Npc;
        }
    }

    private static string Describe(QuestDestination destination)
    {
        return string.IsNullOrEmpty(destination.IslandName)
            ? (destination.Point != null ? destination.Point.name : "none")
            : $"island '{destination.IslandName}' (queued)";
    }

    // ---------------------------------------------------------------- deferred island spawning

    // The only place a queued entity becomes a real object. Called whenever something could have made
    // one spawnable: a fresh batch of quests, an island's spawn points registering, or a point freeing
    // up because a quest moved on. There is deliberately no per-second poll - all three are events, so
    // a tick would only re-check state that nothing has touched.
    private void ResolvePendingSpawns()
    {
        if (!IsServer) return;

        warnedIslands.Clear();

        for (int i = 0; i < entityRecords.Count; i++)
        {
            QuestEntityRecord record = entityRecords[i];
            if (record.Instance != null) continue;   // already standing in the world

            QuestSpawnPoint point = FindFreePoint(ToSpawnKind(record.Kind), record.IslandName);

            if (point == null)
            {
                WarnIfIslandIsLoadedButFull(record);
                continue;   // island simply is not loaded: keep waiting, that is the whole point
            }

            NetworkObject instance = SpawnQuestEntity(GetPrefab(record.Kind), point, record.QuestInstanceId, record.Kind);
            if (instance == null) continue;   // setup error, already logged by SpawnQuestEntity

            record.Point = point;
            record.Instance = instance;
        }
    }

    // A record whose island is not loaded is just waiting - normal, and not worth a log line. A record
    // whose island IS loaded but has no free point of the right kind means that island's scene holds
    // fewer QuestSpawnPoints of that kind than the quests sent to it.
    private void WarnIfIslandIsLoadedButFull(QuestEntityRecord record)
    {
        if (!IsIslandLoaded(record.IslandName)) return;
        if (!warnedIslands.Add(record.IslandName)) return;

        Debug.LogWarning($"QuestManager: island '{record.IslandName}' is loaded but has no free spawn point of the " +
                         $"required kind ({ToSpawnKind(record.Kind)}). Quest entities stay queued until one frees up - " +
                         "add more QuestSpawnPoints to that island's scene.");
    }

    // An island finished loading: its spawn points just registered. Nothing else can turn a queued
    // record into an object, so this is where the queue drains.
    private void HandleSpawnPointRegistered(QuestSpawnPoint point)
    {
        if (!IsSpawned) return;   // see HandleSpawnPointDeregistered: nothing to do outside a live session
        if (point == null) return;
        if (string.IsNullOrEmpty(point.IslandName)) return;   // a persistent point serves no queued record

        ResolvePendingSpawns();
    }

    // An island is unloading: the ground under its quest entities is about to disappear, so they go
    // back into the queue. The quest itself is untouched - it stays in the NetworkList and its
    // entities reappear the next time the island loads (at whatever points are free then).
    private void HandleSpawnPointDeregistered(QuestSpawnPoint point)
    {
        // Tearing the session down destroys every GameObject, which deregisters every spawn point in
        // an undefined order - this manager may still be alive while they go. Despawning into a
        // NetworkManager that is already shutting down only produces errors, and NGO cleans the
        // entities up anyway, so once this manager is no longer spawned there is nothing to do here.
        if (!IsSpawned) return;
        if (point == null) return;

        for (int i = 0; i < entityRecords.Count; i++)
        {
            QuestEntityRecord record = entityRecords[i];
            if (record.Point != point) continue;

            // A persistent point that was merely disabled is a different story: its entity is an
            // independent world object standing on ground that is still there, so it stays. Only the
            // dead point reference is dropped, which also frees the point in the occupancy check.
            if (string.IsNullOrEmpty(record.IslandName))
            {
                record.Point = null;
                continue;
            }

            if (record.Instance != null && record.Instance.IsSpawned) record.Instance.Despawn(true);

            record.Instance = null;
            record.Point = null;
        }
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

                RemoveEntity(entity.NetworkObject);

                // Picking the item up gave its spawn point back, which may be exactly what a queued
                // entity was waiting for.
                ResolvePendingSpawns();
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

        RemoveQuestEntities(state.InstanceId);

        questStates.RemoveAt(index);

        Debug.Log($"QuestManager: quest #{state.InstanceId} completed, {state.GoldReward} gold paid to the crew.");

        // The finished quest just released its spawn points: a queued entity may fit in one of them now.
        ResolvePendingSpawns();
    }

    private int FindQuestIndex(int instanceId)
    {
        for (int i = 0; i < questStates.Count; i++)
        {
            if (questStates[i].InstanceId == instanceId) return i;
        }

        return -1;
    }

    // Removes one entity: despawns the object and drops its record, which frees its spawn point.
    private void RemoveEntity(NetworkObject instance)
    {
        if (instance == null) return;

        for (int i = entityRecords.Count - 1; i >= 0; i--)
        {
            if (entityRecords[i].Instance != instance) continue;

            entityRecords.RemoveAt(i);
        }

        if (instance.IsSpawned) instance.Despawn(true);
    }

    // Drops every entity of a quest: spawned ones are despawned, queued ones just leave the queue.
    private void RemoveQuestEntities(int questInstanceId)
    {
        for (int i = entityRecords.Count - 1; i >= 0; i--)
        {
            QuestEntityRecord record = entityRecords[i];
            if (record.QuestInstanceId != questInstanceId) continue;

            if (record.Instance != null && record.Instance.IsSpawned) record.Instance.Despawn(true);

            entityRecords.RemoveAt(i);
        }
    }
}
