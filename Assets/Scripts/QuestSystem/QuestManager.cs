using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using Random = UnityEngine.Random;

// Server-authoritative quest generation, objective progression, entity lifecycle and rewards.
// Clients only request interactions and rebuild text from synchronized integer state.
public class QuestManager : NetworkBehaviour
{
    private const float MaintenanceIntervalSeconds = 1f;

    public static QuestManager Instance { get; private set; }

    [Header("Data")]
    [SerializeField] private QuestDatabase database;

    [Header("Entity Prefabs")]
    [Tooltip("Must have NetworkObject + QuestEntity + a collider, and be registered in NetworkManager's network prefabs.")]
    [SerializeField] private GameObject questNpcPrefab;
    [SerializeField] private GameObject questItemPrefab;

    [Header("Destinations")]
    [Tooltip("Islands that quests may target. Island capacity stays soft: entities queue until a matching point is free.")]
    [SerializeField] private IslandDefinition[] islandDestinations;

    [Header("Board Generation")]
    [Min(1)][SerializeField] private int minQuestsPerBoard = 6;
    [Min(1)][SerializeField] private int maxQuestsPerBoard = 8;

    [Header("Reward Scaling")]
    [Tooltip("Gold grows by this fraction of the base roll for every elapsed in-game day.")]
    [Min(0f)][SerializeField] private float goldPerDayFactor = 0.1f;

    [Header("Lifetime")]
    [Tooltip("A multi-step quest shares this one lifetime across all objectives.")]
    [Min(1)][SerializeField] private int expiryDays = 3;

    [Header("Debug")]
    [SerializeField] private bool logGeneration = false;

    private struct QuestDestination
    {
        public QuestSpawnPoint Point;
        public string IslandName;
        public int LocationNameIndex;
    }

    // One assignment per objective. Future persistent points are reserved here before their entity
    // is spawned, so every destination advertised in the quest log remains completable.
    private class QuestObjectiveRuntime
    {
        public int QuestInstanceId;
        public int ObjectiveIndex;
        public QuestType Type;
        public QuestDestination GiverDestination;
        public QuestDestination ItemDestination;
    }

    // One record per entity required by the active objective. Pending island entity == Instance null.
    private class QuestEntityRecord
    {
        public int QuestInstanceId;
        public int ObjectiveIndex;
        public QuestEntityKind Kind;
        public string IslandName;
        public QuestSpawnPoint Point;
        public NetworkObject Instance;
    }

    private NetworkList<QuestInstanceState> questStates = new NetworkList<QuestInstanceState>();
    private NetworkList<QuestObjectiveState> questObjectives = new NetworkList<QuestObjectiveState>();
    private NetworkVariable<int> totalQuestsCompleted = new NetworkVariable<int>(0);

    private readonly Dictionary<int, int> lastGeneratedDayByBoard = new Dictionary<int, int>();
    private readonly List<QuestObjectiveRuntime> objectiveRuntimes = new List<QuestObjectiveRuntime>();
    private readonly List<QuestEntityRecord> entityRecords = new List<QuestEntityRecord>();
    private readonly List<QuestTemplate> eligibleTemplates = new List<QuestTemplate>();
    private readonly List<QuestDestination> destinationCandidates = new List<QuestDestination>();
    private readonly List<int> usedNpcNamesInBatch = new List<int>();
    private readonly List<int> freeNpcNameIndices = new List<int>();
    private readonly HashSet<int> invalidTemplateIndices = new HashSet<int>();
    private readonly HashSet<string> warnedIslands = new HashSet<string>();
    private bool npcPrefabValid;
    private bool itemPrefabValid;
    private int nextInstanceId = 1;
    private float maintenanceTimer;

    public QuestDatabase Database => database;
    public NetworkList<QuestInstanceState> QuestStates => questStates;
    public NetworkList<QuestObjectiveState> QuestObjectives => questObjectives;
    public int ExpiryDays => Mathf.Max(1, expiryDays);
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
        maintenanceTimer = 0f;

        if (!IsServer) return;

        if (GameDayClock.Instance == null)
        {
            Debug.LogError("QuestManager: no GameDayClock found. Quests cannot expire without the current day.", this);
        }

        ValidateIslandDestinations();
        ValidateEntityPrefabs();
        ValidateTemplates();

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
        UnsubscribeFromSpawnPoints();
        if (Instance == this) Instance = null;
        base.OnDestroy();
    }

    private void UnsubscribeFromSpawnPoints()
    {
        QuestSpawnPoint.Registered -= HandleSpawnPointRegistered;
        QuestSpawnPoint.Deregistered -= HandleSpawnPointDeregistered;
    }

    private void Update()
    {
        if (!IsServer || !IsSpawned) return;

        maintenanceTimer += Time.deltaTime;
        if (maintenanceTimer < MaintenanceIntervalSeconds) return;

        maintenanceTimer %= MaintenanceIntervalSeconds;
        RunServerMaintenance();
    }

    private void RunServerMaintenance()
    {
        if (GameDayClock.Instance != null) ExpireQuests(GameDayClock.Instance.CurrentDay);
        ResolvePendingSpawns(false);
    }

    private void ExpireQuests(int currentDay)
    {
        int expiredCount = 0;

        for (int i = questStates.Count - 1; i >= 0; i--)
        {
            QuestInstanceState state = questStates[i];
            if (currentDay - state.CreatedDay < ExpiryDays) continue;

            RemoveQuestRuntime(state.InstanceId);
            RemoveObjectiveStates(state.InstanceId);
            questStates.RemoveAt(i);
            expiredCount++;
        }

        if (expiredCount > 0 && logGeneration)
        {
            Debug.Log($"QuestManager: expired {expiredCount} quest(s) on day {currentDay}.", this);
        }
    }

    // ---------------------------------------------------------------- board generation

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
            Debug.LogError("QuestManager: no QuestDatabase assigned, cannot generate quests.", this);
            return;
        }

        int day = GameDayClock.Instance != null ? GameDayClock.Instance.CurrentDay : 1;

        if (lastGeneratedDayByBoard.TryGetValue(boardIndex, out int lastDay) && lastDay == day)
        {
            Debug.Log($"QuestManager: board {boardIndex} already generated quests on day {day}.", this);
            return;
        }

        CollectEligibleTemplates(day);
        if (eligibleTemplates.Count == 0)
        {
            Debug.LogWarning($"QuestManager: no eligible quest template for day {day}. Check template setup and destinations.", this);
            return;
        }

        lastGeneratedDayByBoard[boardIndex] = day;
        usedNpcNamesInBatch.Clear();

        int questCount = Random.Range(minQuestsPerBoard,
            Mathf.Max(minQuestsPerBoard, maxQuestsPerBoard) + 1);

        if (logGeneration)
        {
            Debug.Log($"QuestManager: day {day}, board {boardIndex}, generating up to {questCount} quests.", this);
        }

        for (int i = 0; i < questCount; i++)
        {
            CollectEligibleTemplates(day);
            if (eligibleTemplates.Count == 0) break;

            QuestTemplate template = PickWeightedTemplate();
            if (template == null) continue;

            TryCreateQuest(template, day, 0, -1, true);
        }

        ResolvePendingSpawns();
    }

    private bool TryCreateQuest(QuestTemplate template, int day, int chainStepIndex,
        int carriedNpcNameIndex, bool avoidBatchNpcRepeats)
    {
        if (template == null || database == null) return false;

        int templateIndex = database.GetTemplateIndex(template);
        if (templateIndex < 0 || invalidTemplateIndices.Contains(templateIndex)) return false;
        if (!CanSatisfyTemplateDestinations(template)) return false;

        int objectiveCount = template.ObjectiveCount;
        int instanceId = nextInstanceId++;
        List<QuestObjectiveState> newObjectives = new List<QuestObjectiveState>(objectiveCount);
        List<QuestObjectiveRuntime> newRuntimes = new List<QuestObjectiveRuntime>(objectiveCount);
        int previousNpcNameIndex = carriedNpcNameIndex;

        for (int objectiveIndex = 0; objectiveIndex < objectiveCount; objectiveIndex++)
        {
            if (!template.TryGetObjective(objectiveIndex, out QuestType objectiveType, out _, out _,
                    out bool keepNpcFromPrevious))
            {
                return false;
            }

            int npcNameIndex;
            if (objectiveIndex == 0 && carriedNpcNameIndex >= 0)
            {
                npcNameIndex = carriedNpcNameIndex;
            }
            else if (objectiveIndex > 0 && keepNpcFromPrevious)
            {
                npcNameIndex = previousNpcNameIndex;
            }
            else
            {
                npcNameIndex = PickNpcNameIndex(avoidBatchNpcRepeats);
            }

            if (!TryPickObjectiveDestinations(objectiveType, instanceId,
                    out QuestDestination giverDestination, out QuestDestination itemDestination))
            {
                return false;
            }

            QuestDestination shownDestination = RequiresItemEntity(objectiveType)
                ? itemDestination
                : giverDestination;

            newObjectives.Add(new QuestObjectiveState
            {
                QuestInstanceId = instanceId,
                ObjectiveIndex = objectiveIndex,
                NpcNameIndex = npcNameIndex,
                ItemNameIndex = RequiresItemEntity(objectiveType) ? PickIndex(database.ItemNameCount) : -1,
                LocationNameIndex = shownDestination.LocationNameIndex,
                Status = objectiveIndex == 0 ? QuestStatus.Active : QuestStatus.Locked
            });

            newRuntimes.Add(new QuestObjectiveRuntime
            {
                QuestInstanceId = instanceId,
                ObjectiveIndex = objectiveIndex,
                Type = objectiveType,
                GiverDestination = giverDestination,
                ItemDestination = itemDestination
            });

            previousNpcNameIndex = npcNameIndex;
        }

        objectiveRuntimes.AddRange(newRuntimes);

        if (!TryPlaceObjective(instanceId, 0))
        {
            RemoveQuestRuntime(instanceId);
            return false;
        }

        for (int i = 0; i < newObjectives.Count; i++) questObjectives.Add(newObjectives[i]);

        QuestInstanceState state = new QuestInstanceState
        {
            InstanceId = instanceId,
            TemplateIndex = templateIndex,
            GoldReward = RollGold(template, day),
            CreatedDay = day,
            ChainStepIndex = chainStepIndex,
            CurrentObjectiveIndex = 0,
            ObjectiveCount = objectiveCount
        };

        questStates.Add(state);

        if (logGeneration)
        {
            Debug.Log($"QuestManager: quest #{instanceId} -> template={template.name}, " +
                      $"objectives={objectiveCount}, chainStep={chainStepIndex}, gold={state.GoldReward}.", this);
        }

        return true;
    }

    private void CollectEligibleTemplates(int day)
    {
        eligibleTemplates.Clear();
        if (database == null) return;

        for (int i = 0; i < database.TemplateCount; i++)
        {
            QuestTemplate template = database.GetTemplate(i);
            if (template == null || invalidTemplateIndices.Contains(i)) continue;
            if (!template.BoardSelectable || template.MinDay > day) continue;
            if (!CanSatisfyTemplateDestinations(template)) continue;

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

    private int PickNpcNameIndex(bool avoidBatchRepeats)
    {
        int count = database.NpcNameCount;
        if (count == 0) return -1;
        if (!avoidBatchRepeats) return PickIndex(count);

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

    // ---------------------------------------------------------------- template validation

    private void ValidateTemplates()
    {
        invalidTemplateIndices.Clear();
        if (database == null) return;

        for (int i = 0; i < database.TemplateCount; i++)
        {
            QuestTemplate template = database.GetTemplate(i);
            if (template == null)
            {
                invalidTemplateIndices.Add(i);
                Debug.LogWarning($"QuestManager: template entry {i} is empty.", this);
                continue;
            }

            if (template.Type == QuestType.MultiStep && template.ObjectiveCount < 2)
            {
                invalidTemplateIndices.Add(i);
                Debug.LogWarning($"QuestManager: multi-step template '{template.name}' needs at least two objectives.", template);
                continue;
            }

            bool invalid = false;
            for (int objectiveIndex = 0; objectiveIndex < template.ObjectiveCount; objectiveIndex++)
            {
                if (!template.TryGetObjective(objectiveIndex, out QuestType objectiveType, out _, out _, out _)
                    || !TryGetRequiredEntities(objectiveType, out _))
                {
                    invalid = true;
                    break;
                }
            }

            if (invalid)
            {
                invalidTemplateIndices.Add(i);
                Debug.LogWarning($"QuestManager: template '{template.name}' contains an unsupported or empty objective.", template);
            }

            if (template.NextTemplate != null && database.GetTemplateIndex(template.NextTemplate) < 0)
            {
                Debug.LogWarning($"QuestManager: nextTemplate on '{template.name}' is not present in QuestDatabase.", template);
            }
        }
    }

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
                return false;
        }
    }

    private static bool RequiresItemEntity(QuestType type)
    {
        return TryGetRequiredEntities(type, out bool needsItemEntity) && needsItemEntity;
    }

    private bool CanSatisfyTemplateDestinations(QuestTemplate template)
    {
        for (int i = 0; i < template.ObjectiveCount; i++)
        {
            if (!template.TryGetObjective(i, out QuestType objectiveType, out _, out _, out _)) return false;
            if (!TryGetRequiredEntities(objectiveType, out bool needsItem)) return false;
            if (!npcPrefabValid || (needsItem && !itemPrefabValid)) return false;
            if (!HasDestination(QuestEntityKind.Giver)) return false;
            if (needsItem && !HasDestination(QuestEntityKind.Item)) return false;
        }

        return true;
    }

    private void ValidateEntityPrefabs()
    {
        npcPrefabValid = ValidateEntityPrefab(questNpcPrefab, "NPC");
        itemPrefabValid = ValidateEntityPrefab(questItemPrefab, "item");
    }

    private bool ValidateEntityPrefab(GameObject prefab, string label)
    {
        if (prefab == null)
        {
            Debug.LogError($"QuestManager: no quest {label} prefab assigned.", this);
            return false;
        }

        if (!prefab.TryGetComponent(out QuestEntity _)
            || !prefab.TryGetComponent(out NetworkObject _))
        {
            Debug.LogError($"QuestManager: quest {label} prefab '{prefab.name}' needs QuestEntity " +
                           "and NetworkObject components.", prefab);
            return false;
        }

        return true;
    }

    // ---------------------------------------------------------------- destinations and reservations

    private bool TryPickObjectiveDestinations(QuestType type, int questInstanceId,
        out QuestDestination giverDestination, out QuestDestination itemDestination)
    {
        giverDestination = default;
        itemDestination = default;

        if (!TryGetRequiredEntities(type, out bool needsItem)) return false;
        if (!TryPickDestination(QuestEntityKind.Giver, questInstanceId, out giverDestination)) return false;
        if (needsItem && !TryPickDestination(QuestEntityKind.Item, questInstanceId, out itemDestination)) return false;

        return true;
    }

    private bool HasDestination(QuestEntityKind kind)
    {
        if (CountIslandDestinations() > 0) return true;
        return FindFreePersistentPoint(ToSpawnKind(kind), -1) != null;
    }

    private bool TryPickDestination(QuestEntityKind kind, int questInstanceId,
        out QuestDestination destination)
    {
        destination = default;
        destinationCandidates.Clear();
        QuestSpawnPoint.SpawnKind spawnKind = ToSpawnKind(kind);

        for (int i = 0; i < QuestSpawnPoint.All.Count; i++)
        {
            QuestSpawnPoint point = QuestSpawnPoint.All[i];
            if (!IsPersistentPointAvailable(point, spawnKind, questInstanceId)) continue;

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

    private bool IsPersistentPointAvailable(QuestSpawnPoint point, QuestSpawnPoint.SpawnKind kind,
        int requestingQuestInstanceId)
    {
        if (point == null || point.Kind != kind || !string.IsNullOrEmpty(point.IslandName)) return false;

        for (int i = 0; i < objectiveRuntimes.Count; i++)
        {
            QuestObjectiveRuntime runtime = objectiveRuntimes[i];
            if (runtime.QuestInstanceId == requestingQuestInstanceId) continue;
            if (runtime.GiverDestination.Point == point || runtime.ItemDestination.Point == point) return false;
        }

        for (int i = 0; i < entityRecords.Count; i++)
        {
            QuestEntityRecord record = entityRecords[i];
            if (record.QuestInstanceId == requestingQuestInstanceId) continue;
            if (record.Point == point) return false;
        }

        return true;
    }

    private QuestSpawnPoint FindFreePersistentPoint(QuestSpawnPoint.SpawnKind kind, int questInstanceId)
    {
        for (int i = 0; i < QuestSpawnPoint.All.Count; i++)
        {
            QuestSpawnPoint point = QuestSpawnPoint.All[i];
            if (IsPersistentPointAvailable(point, kind, questInstanceId)) return point;
        }

        return null;
    }

    private QuestSpawnPoint FindFreeIslandPoint(QuestSpawnPoint.SpawnKind kind, string islandName)
    {
        for (int i = 0; i < QuestSpawnPoint.All.Count; i++)
        {
            QuestSpawnPoint point = QuestSpawnPoint.All[i];
            if (point == null || point.Kind != kind) continue;
            if (!string.Equals(point.IslandName, islandName, StringComparison.Ordinal)) continue;
            if (IsEntityPointOccupied(point)) continue;

            return point;
        }

        return null;
    }

    private bool IsEntityPointOccupied(QuestSpawnPoint point)
    {
        for (int i = 0; i < entityRecords.Count; i++)
        {
            if (entityRecords[i].Point == point) return true;
        }

        return false;
    }

    private int CountIslandDestinations()
    {
        if (islandDestinations == null) return 0;

        int count = 0;
        for (int i = 0; i < islandDestinations.Length; i++)
        {
            IslandDefinition island = islandDestinations[i];
            if (island != null && !string.IsNullOrEmpty(island.SceneName)) count++;
        }

        return count;
    }

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
                Debug.LogWarning($"QuestManager: island '{island.name}' has invalid locationNameIndex " +
                                 $"{island.LocationNameIndex}; quests sent there show \"???\".", island);
            }
        }
    }

    private static bool IsIslandLoaded(string islandName)
    {
        for (int i = 0; i < QuestSpawnPoint.All.Count; i++)
        {
            QuestSpawnPoint point = QuestSpawnPoint.All[i];
            if (point != null && string.Equals(point.IslandName, islandName, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    // ---------------------------------------------------------------- objective entity placement

    private bool TryPlaceObjective(int questInstanceId, int objectiveIndex)
    {
        QuestObjectiveRuntime runtime = FindObjectiveRuntime(questInstanceId, objectiveIndex);
        if (runtime == null) return false;

        if (!TryPlaceEntity(runtime, QuestEntityKind.Giver, runtime.GiverDestination)) return false;

        if (RequiresItemEntity(runtime.Type)
            && !TryPlaceEntity(runtime, QuestEntityKind.Item, runtime.ItemDestination))
        {
            RemoveObjectiveEntities(questInstanceId, objectiveIndex);
            return false;
        }

        return true;
    }

    private bool TryPlaceEntity(QuestObjectiveRuntime runtime, QuestEntityKind kind,
        QuestDestination destination)
    {
        QuestEntityRecord record = new QuestEntityRecord
        {
            QuestInstanceId = runtime.QuestInstanceId,
            ObjectiveIndex = runtime.ObjectiveIndex,
            Kind = kind,
            IslandName = destination.IslandName
        };

        if (!string.IsNullOrEmpty(destination.IslandName))
        {
            entityRecords.Add(record);
            return true;
        }

        if (destination.Point == null || !destination.Point.isActiveAndEnabled)
        {
            Debug.LogError($"QuestManager: reserved persistent point is unavailable for quest " +
                           $"#{runtime.QuestInstanceId}, objective {runtime.ObjectiveIndex}.", this);
            return false;
        }

        NetworkObject instance = SpawnQuestEntity(GetPrefab(kind), destination.Point,
            runtime.QuestInstanceId, runtime.ObjectiveIndex, kind);
        if (instance == null) return false;

        record.Point = destination.Point;
        record.Instance = instance;
        entityRecords.Add(record);
        return true;
    }

    private NetworkObject SpawnQuestEntity(GameObject prefab, QuestSpawnPoint point, int questInstanceId,
        int objectiveIndex, QuestEntityKind kind)
    {
        if (prefab == null || point == null)
        {
            Debug.LogError($"QuestManager: missing prefab or spawn point for quest entity kind {kind}.", this);
            return null;
        }

        GameObject instance = Instantiate(prefab, point.transform.position, point.transform.rotation);

        // Instantiate during additive scene loading can put the object into the loading island scene.
        // Pin it to the persistent manager scene so island unload cannot destroy it behind our back.
        if (instance.scene != gameObject.scene)
        {
            SceneManager.MoveGameObjectToScene(instance, gameObject.scene);
        }

        if (!instance.TryGetComponent(out QuestEntity entity)
            || !instance.TryGetComponent(out NetworkObject networkObject))
        {
            Debug.LogError($"QuestManager: prefab '{prefab.name}' needs QuestEntity and NetworkObject.", this);
            Destroy(instance);
            return null;
        }

        entity.ServerInitialize(questInstanceId, objectiveIndex, kind);
        networkObject.Spawn();
        return networkObject;
    }

    private GameObject GetPrefab(QuestEntityKind kind)
    {
        switch (kind)
        {
            case QuestEntityKind.Giver: return questNpcPrefab;
            case QuestEntityKind.Item: return questItemPrefab;
            default: return null;
        }
    }

    private static QuestSpawnPoint.SpawnKind ToSpawnKind(QuestEntityKind kind)
    {
        return kind == QuestEntityKind.Item
            ? QuestSpawnPoint.SpawnKind.Item
            : QuestSpawnPoint.SpawnKind.Npc;
    }

    // ---------------------------------------------------------------- deferred island spawning

    private void ResolvePendingSpawns(bool logCapacityWarnings = true)
    {
        if (!IsServer) return;
        if (logCapacityWarnings) warnedIslands.Clear();

        for (int i = 0; i < entityRecords.Count; i++)
        {
            QuestEntityRecord record = entityRecords[i];
            if (record.Instance != null) continue;

            QuestSpawnPoint point = FindFreeIslandPoint(ToSpawnKind(record.Kind), record.IslandName);
            if (point == null)
            {
                if (logCapacityWarnings) WarnIfIslandIsLoadedButFull(record);
                continue;
            }

            NetworkObject instance = SpawnQuestEntity(GetPrefab(record.Kind), point,
                record.QuestInstanceId, record.ObjectiveIndex, record.Kind);
            if (instance == null) continue;

            record.Point = point;
            record.Instance = instance;
        }
    }

    private void WarnIfIslandIsLoadedButFull(QuestEntityRecord record)
    {
        if (!IsIslandLoaded(record.IslandName)) return;
        string warningKey = $"{record.IslandName}:{record.Kind}";
        if (!warnedIslands.Add(warningKey)) return;

        Debug.LogWarning($"QuestManager: island '{record.IslandName}' has no free " +
                         $"{ToSpawnKind(record.Kind)} quest point. The entity stays queued.", this);
    }

    private void HandleSpawnPointRegistered(QuestSpawnPoint point)
    {
        if (!IsSpawned || point == null || string.IsNullOrEmpty(point.IslandName)) return;
        ResolvePendingSpawns();
    }

    private void HandleSpawnPointDeregistered(QuestSpawnPoint point)
    {
        if (!IsSpawned || point == null) return;

        for (int i = 0; i < entityRecords.Count; i++)
        {
            QuestEntityRecord record = entityRecords[i];
            if (record.Point != point) continue;

            if (string.IsNullOrEmpty(record.IslandName))
            {
                record.Point = null;
                continue;
            }

            if (record.Instance != null && record.Instance.IsSpawned) record.Instance.Despawn(true);
            record.Instance = null;
            record.Point = null;
        }

        if (string.IsNullOrEmpty(point.IslandName)) RepairFutureReservations(point);
    }

    private void RepairFutureReservations(QuestSpawnPoint disabledPoint)
    {
        List<int> questsToCancel = new List<int>();

        for (int i = 0; i < objectiveRuntimes.Count; i++)
        {
            QuestObjectiveRuntime runtime = objectiveRuntimes[i];
            int questIndex = FindQuestIndex(runtime.QuestInstanceId);
            if (questIndex < 0 || runtime.ObjectiveIndex <= questStates[questIndex].CurrentObjectiveIndex) continue;

            bool changed = false;

            if (runtime.GiverDestination.Point == disabledPoint)
            {
                if (!TryPickDestination(QuestEntityKind.Giver, runtime.QuestInstanceId,
                        out QuestDestination replacement))
                {
                    questsToCancel.Add(runtime.QuestInstanceId);
                    continue;
                }

                runtime.GiverDestination = replacement;
                changed = true;
            }

            if (RequiresItemEntity(runtime.Type) && runtime.ItemDestination.Point == disabledPoint)
            {
                if (!TryPickDestination(QuestEntityKind.Item, runtime.QuestInstanceId,
                        out QuestDestination replacement))
                {
                    questsToCancel.Add(runtime.QuestInstanceId);
                    continue;
                }

                runtime.ItemDestination = replacement;
                changed = true;
            }

            if (changed) UpdateObjectiveLocation(runtime);
        }

        for (int i = 0; i < questsToCancel.Count; i++)
        {
            int questInstanceId = questsToCancel[i];
            if (FindQuestIndex(questInstanceId) < 0) continue;

            Debug.LogError($"QuestManager: cancelling quest #{questInstanceId}; a reserved persistent " +
                           "point disappeared and no replacement destination exists.", this);
            CancelQuest(questInstanceId);
        }
    }

    private void UpdateObjectiveLocation(QuestObjectiveRuntime runtime)
    {
        int index = FindObjectiveStateIndex(runtime.QuestInstanceId, runtime.ObjectiveIndex);
        if (index < 0) return;

        QuestObjectiveState objective = questObjectives[index];
        objective.LocationNameIndex = RequiresItemEntity(runtime.Type)
            ? runtime.ItemDestination.LocationNameIndex
            : runtime.GiverDestination.LocationNameIndex;
        questObjectives[index] = objective;
    }

    // ---------------------------------------------------------------- interaction, objective progression and chains

    public void HandleEntityInteracted(QuestEntity entity)
    {
        if (!IsServer || entity == null) return;

        int questIndex = FindQuestIndex(entity.QuestInstanceId);
        if (questIndex < 0) return;

        QuestInstanceState quest = questStates[questIndex];
        if (entity.ObjectiveIndex != quest.CurrentObjectiveIndex) return;

        int objectiveStateIndex = FindObjectiveStateIndex(quest.InstanceId, quest.CurrentObjectiveIndex);
        if (objectiveStateIndex < 0) return;

        QuestObjectiveState objective = questObjectives[objectiveStateIndex];
        QuestObjectiveRuntime runtime = FindObjectiveRuntime(quest.InstanceId, quest.CurrentObjectiveIndex);
        if (runtime == null) return;

        switch (entity.Kind)
        {
            case QuestEntityKind.Item:
                if (runtime.Type != QuestType.FetchDeliver || objective.Status != QuestStatus.Active) return;

                objective.Status = QuestStatus.ItemCollected;
                questObjectives[objectiveStateIndex] = objective;
                RemoveEntity(entity.NetworkObject);
                ResolvePendingSpawns();
                break;

            case QuestEntityKind.Giver:
                if (!CanTurnIn(runtime.Type, objective.Status)) return;
                CompleteObjective(questIndex, quest, objectiveStateIndex, objective);
                break;
        }
    }

    private static bool CanTurnIn(QuestType type, QuestStatus status)
    {
        switch (type)
        {
            case QuestType.TalkTo:
                return status == QuestStatus.Active;
            case QuestType.FetchDeliver:
                return status == QuestStatus.ItemCollected;
            default:
                return false;
        }
    }

    private void CompleteObjective(int questIndex, QuestInstanceState quest, int objectiveStateIndex,
        QuestObjectiveState objective)
    {
        int nextObjectiveIndex = quest.CurrentObjectiveIndex + 1;
        if (nextObjectiveIndex >= quest.ObjectiveCount)
        {
            CompleteQuest(questIndex, quest, objective);
            return;
        }

        int nextStateIndex = FindObjectiveStateIndex(quest.InstanceId, nextObjectiveIndex);
        if (nextStateIndex < 0)
        {
            Debug.LogError($"QuestManager: missing synchronized state for objective {nextObjectiveIndex}.", this);
            return;
        }

        // Place first, commit second. If a prefab/setup failure occurs, the current objective and its
        // giver stay intact so the player can retry after the setup is corrected.
        if (!TryPlaceObjective(quest.InstanceId, nextObjectiveIndex))
        {
            Debug.LogError($"QuestManager: could not activate objective {nextObjectiveIndex + 1} " +
                           $"for quest #{quest.InstanceId}; current objective remains active.", this);
            return;
        }

        RemoveObjectiveEntities(quest.InstanceId, quest.CurrentObjectiveIndex);

        objective.Status = QuestStatus.Completed;
        questObjectives[objectiveStateIndex] = objective;

        QuestObjectiveState nextObjective = questObjectives[nextStateIndex];
        nextObjective.Status = QuestStatus.Active;
        questObjectives[nextStateIndex] = nextObjective;

        quest.CurrentObjectiveIndex = nextObjectiveIndex;
        questStates[questIndex] = quest;

        ResolvePendingSpawns();
    }

    private void CompleteQuest(int questIndex, QuestInstanceState quest, QuestObjectiveState finalObjective)
    {
        QuestTemplate completedTemplate = database != null ? database.GetTemplate(quest.TemplateIndex) : null;
        int carriedNpcNameIndex = finalObjective.NpcNameIndex;

        if (CrewGold.Instance != null) CrewGold.Instance.AddGold(quest.GoldReward);
        totalQuestsCompleted.Value++;

        RemoveQuestRuntime(quest.InstanceId);
        RemoveObjectiveStates(quest.InstanceId);
        questStates.RemoveAt(questIndex);

        Debug.Log($"QuestManager: quest #{quest.InstanceId} completed, {quest.GoldReward} gold paid to the crew.", this);

        if (completedTemplate != null && completedTemplate.NextTemplate != null)
        {
            QuestTemplate nextTemplate = completedTemplate.NextTemplate;
            int nextTemplateIndex = database.GetTemplateIndex(nextTemplate);

            if (nextTemplateIndex < 0 || invalidTemplateIndices.Contains(nextTemplateIndex))
            {
                Debug.LogWarning($"QuestManager: chain after '{completedTemplate.name}' ended because " +
                                 "nextTemplate is missing from the database or invalid.", completedTemplate);
            }
            else
            {
                int day = GameDayClock.Instance != null ? GameDayClock.Instance.CurrentDay : 1;
                if (!TryCreateQuest(nextTemplate, day, quest.ChainStepIndex + 1,
                        carriedNpcNameIndex, false))
                {
                    Debug.LogWarning($"QuestManager: chain after '{completedTemplate.name}' ended because " +
                                     "the next quest could not reserve all required destinations.", completedTemplate);
                }
            }
        }

        ResolvePendingSpawns();
    }

    // ---------------------------------------------------------------- lookup and cleanup

    private int FindQuestIndex(int instanceId)
    {
        for (int i = 0; i < questStates.Count; i++)
        {
            if (questStates[i].InstanceId == instanceId) return i;
        }

        return -1;
    }

    private int FindObjectiveStateIndex(int questInstanceId, int objectiveIndex)
    {
        for (int i = 0; i < questObjectives.Count; i++)
        {
            QuestObjectiveState state = questObjectives[i];
            if (state.QuestInstanceId == questInstanceId && state.ObjectiveIndex == objectiveIndex) return i;
        }

        return -1;
    }

    private QuestObjectiveRuntime FindObjectiveRuntime(int questInstanceId, int objectiveIndex)
    {
        for (int i = 0; i < objectiveRuntimes.Count; i++)
        {
            QuestObjectiveRuntime runtime = objectiveRuntimes[i];
            if (runtime.QuestInstanceId == questInstanceId && runtime.ObjectiveIndex == objectiveIndex)
            {
                return runtime;
            }
        }

        return null;
    }

    private void RemoveEntity(NetworkObject instance)
    {
        if (instance == null) return;

        for (int i = entityRecords.Count - 1; i >= 0; i--)
        {
            if (entityRecords[i].Instance == instance) entityRecords.RemoveAt(i);
        }

        if (instance.IsSpawned) instance.Despawn(true);
    }

    private void RemoveObjectiveEntities(int questInstanceId, int objectiveIndex)
    {
        for (int i = entityRecords.Count - 1; i >= 0; i--)
        {
            QuestEntityRecord record = entityRecords[i];
            if (record.QuestInstanceId != questInstanceId || record.ObjectiveIndex != objectiveIndex) continue;

            if (record.Instance != null && record.Instance.IsSpawned) record.Instance.Despawn(true);
            entityRecords.RemoveAt(i);
        }
    }

    private void RemoveQuestRuntime(int questInstanceId)
    {
        for (int i = entityRecords.Count - 1; i >= 0; i--)
        {
            QuestEntityRecord record = entityRecords[i];
            if (record.QuestInstanceId != questInstanceId) continue;

            if (record.Instance != null && record.Instance.IsSpawned) record.Instance.Despawn(true);
            entityRecords.RemoveAt(i);
        }

        for (int i = objectiveRuntimes.Count - 1; i >= 0; i--)
        {
            if (objectiveRuntimes[i].QuestInstanceId == questInstanceId) objectiveRuntimes.RemoveAt(i);
        }
    }

    private void RemoveObjectiveStates(int questInstanceId)
    {
        for (int i = questObjectives.Count - 1; i >= 0; i--)
        {
            if (questObjectives[i].QuestInstanceId == questInstanceId) questObjectives.RemoveAt(i);
        }
    }

    private void CancelQuest(int questInstanceId)
    {
        int questIndex = FindQuestIndex(questInstanceId);
        if (questIndex < 0) return;

        RemoveQuestRuntime(questInstanceId);
        RemoveObjectiveStates(questInstanceId);
        questStates.RemoveAt(questIndex);
        ResolvePendingSpawns();
    }
}
