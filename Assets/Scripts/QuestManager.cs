using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public class QuestManager : NetworkBehaviour
{
    public static QuestManager Instance { get; private set; }

    [SerializeField] private bool persistAcrossScenes = true;
    [SerializeField] private bool debugLogs = true;
    [SerializeField] private NetworkGameClock gameClock;
    [SerializeField] private QuestDefinition[] questDefinitions = Array.Empty<QuestDefinition>();

    private NetworkList<QuestRuntimeState> questStates;
    private readonly Dictionary<string, QuestDefinition> definitionsById = new Dictionary<string, QuestDefinition>(StringComparer.Ordinal);

    public NetworkList<QuestRuntimeState> QuestStates => questStates;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Multiple QuestManager instances found. Keeping the first instance.");
            enabled = false;
            return;
        }

        Instance = this;
        questStates = new NetworkList<QuestRuntimeState>();
        RebuildDefinitionLookup();
    }

    public override void OnNetworkSpawn()
    {
        RebuildDefinitionLookup();

        if (persistAcrossScenes)
        {
            DontDestroyOnLoad(gameObject);
        }
    }

    private void Update()
    {
        if (!IsServer || questStates == null)
        {
            return;
        }

        ExpireTimedOutQuests();
    }

    public void ActivateBoardQuests(QuestDefinition[] boardQuests, string boardId)
    {
        if (!IsServer)
        {
            Debug.LogWarning("ActivateBoardQuests can only run on the server.", this);
            return;
        }

        if (boardQuests == null || string.IsNullOrWhiteSpace(boardId))
        {
            LogQuestDebug($"Board activation ignored. BoardId='{boardId}', BoardQuests null={boardQuests == null}.");
            return;
        }

        NetworkGameClock clock = GetClock();
        if (clock == null)
        {
            Debug.LogWarning("Cannot activate board quests because no NetworkGameClock is available.");
            return;
        }

        int currentDay = clock.CurrentDay;
        int activatedCount = 0;
        LogQuestDebug($"Board '{boardId}' interaction received on day {currentDay}. Candidate quests: {boardQuests.Length}.");

        foreach (QuestDefinition definition in boardQuests)
        {
            if (definition == null)
            {
                LogQuestDebug($"Board '{boardId}' skipped a null quest definition.");
                continue;
            }

            if (!IsVisibleOnDay(definition, currentDay))
            {
                LogQuestDebug($"Board '{boardId}' skipped quest '{definition.QuestId}' because it is not visible on day {currentDay}. Visible range: {definition.VisibleFromDay}-{definition.VisibleUntilDay}.");
                continue;
            }

            if (HasQuestState(definition.QuestId))
            {
                LogQuestDebug($"Board '{boardId}' skipped quest '{definition.QuestId}' because it already has runtime state.");
                continue;
            }

            AddActiveQuest(definition, QuestSourceType.Board, boardId, clock.ElapsedGameSeconds);
            activatedCount++;
        }

        LogQuestDebug($"Board '{boardId}' activated {activatedCount} quest(s).");
    }

    public void AcceptNpcQuest(QuestDefinition definition, string npcId)
    {
        if (!IsServer)
        {
            Debug.LogWarning("AcceptNpcQuest can only run on the server.", this);
            return;
        }

        if (definition == null || string.IsNullOrWhiteSpace(npcId))
        {
            LogQuestDebug($"NPC quest accept ignored. NpcId='{npcId}', Quest null={definition == null}.");
            return;
        }

        RegisterDefinition(definition);

        if (HasQuestState(definition.QuestId) || HasActiveQuestFromNpc(npcId))
        {
            LogQuestDebug($"NPC '{npcId}' could not accept quest '{definition.QuestId}'. Quest already exists={HasQuestState(definition.QuestId)}, NPC already has active quest={HasActiveQuestFromNpc(npcId)}.");
            return;
        }

        NetworkGameClock clock = GetClock();
        if (clock == null)
        {
            Debug.LogWarning("Cannot accept NPC quest because no NetworkGameClock is available.");
            return;
        }

        AddActiveQuest(definition, QuestSourceType.Npc, npcId, clock.ElapsedGameSeconds);
        LogQuestDebug($"NPC '{npcId}' accepted quest '{definition.QuestId}'.");
    }

    public void CompleteInteractionObjective(string targetId)
    {
        if (!IsServer)
        {
            Debug.LogWarning("CompleteInteractionObjective can only run on the server.", this);
            return;
        }

        if (string.IsNullOrWhiteSpace(targetId))
        {
            LogQuestDebug("Objective completion ignored because TargetId is empty.");
            return;
        }

        int completedCount = 0;
        LogQuestDebug($"Objective interaction received for target '{targetId}'. Active quest states: {questStates.Count}.");

        for (int i = 0; i < questStates.Count; i++)
        {
            QuestRuntimeState state = questStates[i];
            if (state.Status != QuestStatus.Active)
            {
                LogQuestDebug($"Quest '{state.QuestId}' ignored for objective target '{targetId}' because status is {state.Status}.");
                continue;
            }

            QuestDefinition definition = GetDefinition(state.QuestId);
            if (definition == null || TryExpireQuest(i, state))
            {
                LogQuestDebug($"Quest '{state.QuestId}' ignored for objective target '{targetId}'. Definition found={definition != null}.");
                continue;
            }

            if (TryCompleteObjective(definition, ref state, targetId))
            {
                if (AreAllObjectivesComplete(definition, state))
                {
                    state.Status = QuestStatus.ReadyToTurnIn;
                }

                questStates[i] = state;
                completedCount++;
                LogQuestDebug($"Quest '{definition.QuestId}' completed objective target '{targetId}'. New status: {state.Status}, current objective index: {state.CurrentObjectiveIndex}.");
            }
        }

        if (completedCount == 0)
        {
            LogQuestDebug($"No active quest objective matched target '{targetId}'. Check objective target ids and quest status.");
        }
    }

    public void TurnInQuests(string targetId)
    {
        if (!IsServer)
        {
            Debug.LogWarning("TurnInQuests can only run on the server.", this);
            return;
        }

        if (string.IsNullOrWhiteSpace(targetId))
        {
            LogQuestDebug("Quest turn-in ignored because TargetId is empty.");
            return;
        }

        int turnedInCount = 0;
        LogQuestDebug($"Turn-in interaction received for target '{targetId}'. Quest states: {questStates.Count}.");

        for (int i = 0; i < questStates.Count; i++)
        {
            QuestRuntimeState state = questStates[i];
            if (state.Status != QuestStatus.ReadyToTurnIn || state.RewardClaimed)
            {
                LogQuestDebug($"Quest '{state.QuestId}' ignored for turn-in target '{targetId}' because status is {state.Status}, reward claimed={state.RewardClaimed}.");
                continue;
            }

            QuestDefinition definition = GetDefinition(state.QuestId);
            if (definition == null || !StringEquals(definition.TurnInTargetId, targetId))
            {
                LogQuestDebug($"Quest '{state.QuestId}' ignored for turn-in target '{targetId}'. Definition found={definition != null}, expected turn-in='{definition?.TurnInTargetId}'.");
                continue;
            }

            if (TryExpireQuest(i, state))
            {
                continue;
            }

            GrantRewardToTeam(definition);
            state.RewardClaimed = true;
            state.Status = QuestStatus.Completed;
            questStates[i] = state;
            turnedInCount++;
            LogQuestDebug($"Quest '{definition.QuestId}' turned in at '{targetId}'.");
        }

        if (turnedInCount == 0)
        {
            LogQuestDebug($"No ready quest was turned in at '{targetId}'.");
        }
    }

    private void AddActiveQuest(QuestDefinition definition, QuestSourceType sourceType, string sourceId, double startedAtGameSeconds)
    {
        RegisterDefinition(definition);

        NetworkGameClock clock = GetClock();
        double expiresAtGameSeconds = definition.DurationDays > 0 && clock != null
            ? startedAtGameSeconds + definition.DurationDays * clock.DayLengthSeconds
            : -1d;

        QuestRuntimeState state = new QuestRuntimeState(definition.QuestId, sourceType, sourceId, QuestStatus.Active, startedAtGameSeconds, expiresAtGameSeconds);
        if (definition.Objectives == null || definition.Objectives.Length == 0)
        {
            state.Status = QuestStatus.ReadyToTurnIn;
        }

        questStates.Add(state);
        LogQuestDebug($"Quest '{definition.QuestId}' added. Source={sourceType}:{sourceId}, Status={state.Status}, ExpiresAt={expiresAtGameSeconds}.");
    }

    private bool TryCompleteObjective(QuestDefinition definition, ref QuestRuntimeState state, string targetId)
    {
        QuestObjectiveDefinition[] objectives = definition.Objectives;
        if (objectives == null || objectives.Length == 0)
        {
            return false;
        }

        if (definition.ObjectiveMode == QuestObjectiveMode.Sequential)
        {
            int objectiveIndex = state.CurrentObjectiveIndex;
            if (objectiveIndex >= objectives.Length || !DoesObjectiveMatchTarget(objectives[objectiveIndex], targetId))
            {
                return false;
            }

            state.MarkObjectiveCompleted(objectiveIndex);
            state.CurrentObjectiveIndex = Mathf.Min(objectiveIndex + 1, objectives.Length);
            return true;
        }

        bool completedAny = false;
        for (int i = 0; i < objectives.Length; i++)
        {
            if (state.IsObjectiveCompleted(i) || !DoesObjectiveMatchTarget(objectives[i], targetId))
            {
                continue;
            }

            state.MarkObjectiveCompleted(i);
            completedAny = true;
        }

        return completedAny;
    }

    private bool DoesObjectiveMatchTarget(QuestObjectiveDefinition objective, string targetId)
    {
        return objective != null
            && objective.ObjectiveType == QuestObjectiveType.InteractWithTarget
            && StringEquals(objective.TargetId, targetId);
    }

    private bool AreAllObjectivesComplete(QuestDefinition definition, QuestRuntimeState state)
    {
        QuestObjectiveDefinition[] objectives = definition.Objectives;
        if (objectives == null || objectives.Length == 0)
        {
            return true;
        }

        for (int i = 0; i < objectives.Length; i++)
        {
            if (!state.IsObjectiveCompleted(i))
            {
                return false;
            }
        }

        return true;
    }

    private void ExpireTimedOutQuests()
    {
        for (int i = 0; i < questStates.Count; i++)
        {
            TryExpireQuest(i, questStates[i]);
        }
    }

    private bool TryExpireQuest(int stateIndex, QuestRuntimeState state)
    {
        if (state.Status != QuestStatus.Active && state.Status != QuestStatus.ReadyToTurnIn)
        {
            return false;
        }

        NetworkGameClock clock = GetClock();
        if (clock == null || state.ExpiresAtGameSeconds < 0d || clock.ElapsedGameSeconds < state.ExpiresAtGameSeconds)
        {
            return false;
        }

        state.Status = QuestStatus.Expired;
        questStates[stateIndex] = state;
        LogQuestDebug($"Quest '{state.QuestId}' expired at game time {clock.ElapsedGameSeconds}.");
        return true;
    }

    private bool HasQuestState(string questId)
    {
        for (int i = 0; i < questStates.Count; i++)
        {
            if (StringEquals(questStates[i].QuestId.ToString(), questId))
            {
                return true;
            }
        }

        return false;
    }

    private bool HasActiveQuestFromNpc(string npcId)
    {
        for (int i = 0; i < questStates.Count; i++)
        {
            QuestRuntimeState state = questStates[i];
            if (state.Status != QuestStatus.Active && state.Status != QuestStatus.ReadyToTurnIn)
            {
                continue;
            }

            if (state.SourceType == QuestSourceType.Npc && StringEquals(state.SourceId.ToString(), npcId))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsVisibleOnDay(QuestDefinition definition, int day)
    {
        return day >= definition.VisibleFromDay && day <= definition.VisibleUntilDay;
    }

    private QuestDefinition GetDefinition(FixedString64Bytes questId)
    {
        return GetDefinition(questId.ToString());
    }

    private QuestDefinition GetDefinition(string questId)
    {
        if (string.IsNullOrWhiteSpace(questId))
        {
            return null;
        }

        definitionsById.TryGetValue(questId, out QuestDefinition definition);
        return definition;
    }

    private NetworkGameClock GetClock()
    {
        return gameClock != null ? gameClock : NetworkGameClock.Instance;
    }

    private void RebuildDefinitionLookup()
    {
        definitionsById.Clear();

        foreach (QuestDefinition definition in questDefinitions)
        {
            RegisterDefinition(definition);
        }
    }

    private void RegisterDefinition(QuestDefinition definition)
    {
        if (definition == null || string.IsNullOrWhiteSpace(definition.QuestId))
        {
            return;
        }

        if (definitionsById.ContainsKey(definition.QuestId))
        {
            if (definitionsById[definition.QuestId] != definition)
            {
                Debug.LogWarning($"Duplicate quest id found: {definition.QuestId}", definition);
            }

            return;
        }

        definitionsById.Add(definition.QuestId, definition);
    }

    private void GrantRewardToTeam(QuestDefinition definition)
    {
        QuestRewardDefinition reward = definition.Reward;
        if (reward == null)
        {
            Debug.Log($"Quest reward granted to team: {definition.QuestId}");
            return;
        }

        Debug.Log($"Quest reward granted to team: {definition.QuestId} | {reward.RewardId} x{reward.Amount}");
    }

    private void LogQuestDebug(string message)
    {
        if (!debugLogs)
        {
            return;
        }

        Debug.Log($"[QuestManager] {message}", this);
    }

    private static bool StringEquals(string left, string right)
    {
        return string.Equals(left, right, StringComparison.Ordinal);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        questStates?.Dispose();
    }
}
