using System.Text;
using TMPro;
using Unity.Netcode;
using UnityEngine;

// Text-only quest log. Both synchronized lists can change several times in one server operation,
// so callbacks only mark the view dirty and Update rebuilds a consistent snapshot once per frame.
public class QuestLogUI : MonoBehaviour
{
    [Tooltip("Optional. Leave empty to run log-only while the UI is not built yet.")]
    [SerializeField] private TMP_Text questListText;
    [Tooltip("Optional. Leave empty to run log-only while the UI is not built yet.")]
    [SerializeField] private TMP_Text goldText;

    [Tooltip("Prints the same text to the Console. Turn off once the real UI is in place.")]
    [SerializeField] private bool logToConsole = true;

    private readonly StringBuilder builder = new StringBuilder();

    private QuestManager boundManager;
    private CrewGold boundGold;
    private GameDayClock boundClock;
    private bool questListDirty;

    private void Update()
    {
        if (boundManager == null)
        {
            TryBind();
            return;
        }

        if (!questListDirty) return;

        questListDirty = false;
        RebuildQuestList();
    }

    private void OnDestroy()
    {
        if (boundManager != null)
        {
            boundManager.QuestStates.OnListChanged -= HandleQuestListChanged;
            boundManager.QuestObjectives.OnListChanged -= HandleObjectiveListChanged;
        }

        if (boundGold != null) boundGold.OnGoldChanged -= HandleGoldChanged;
        if (boundClock != null) boundClock.OnDayChanged -= HandleDayChanged;
    }

    private void TryBind()
    {
        QuestManager manager = QuestManager.Instance;
        if (manager == null || !manager.IsSpawned) return;

        boundManager = manager;
        boundManager.QuestStates.OnListChanged += HandleQuestListChanged;
        boundManager.QuestObjectives.OnListChanged += HandleObjectiveListChanged;

        boundGold = CrewGold.Instance;
        if (boundGold != null) boundGold.OnGoldChanged += HandleGoldChanged;

        boundClock = GameDayClock.Instance;
        if (boundClock != null) boundClock.OnDayChanged += HandleDayChanged;

        // Late joiners receive both lists before this component can subscribe.
        RebuildQuestList();
        RefreshGold();
    }

    private void HandleQuestListChanged(NetworkListEvent<QuestInstanceState> changeEvent)
    {
        questListDirty = true;
    }

    private void HandleObjectiveListChanged(NetworkListEvent<QuestObjectiveState> changeEvent)
    {
        questListDirty = true;
    }

    private void HandleGoldChanged(int current)
    {
        RefreshGold();
    }

    private void HandleDayChanged(int current)
    {
        questListDirty = true;
    }

    private void RebuildQuestList()
    {
        if (boundManager == null) return;

        QuestDatabase database = boundManager.Database;
        NetworkList<QuestInstanceState> quests = boundManager.QuestStates;
        int currentDay = boundClock != null ? boundClock.CurrentDay : 1;

        builder.Clear();
        builder.Append("--- Görevler (Gün ").Append(currentDay).AppendLine(") ---");

        if (quests.Count == 0) builder.AppendLine("Aktif görev yok.");

        for (int i = 0; i < quests.Count; i++)
        {
            QuestInstanceState quest = quests[i];

            if (!TryGetObjective(quest.InstanceId, 0, out QuestObjectiveState firstObjective))
            {
                builder.AppendLine("[?] Görev verisi eksik.");
                builder.AppendLine();
                continue;
            }

            if (quest.ChainStepIndex > 0)
            {
                builder.Append("Zincir adımı: ").Append(quest.ChainStepIndex + 1).AppendLine();
            }

            if (quest.ObjectiveCount <= 1)
            {
                builder.Append('[').Append(GetStatusLabel(firstObjective.Status)).Append("] ");
                builder.AppendLine(QuestTextBuilder.BuildObjectiveTitle(database, quest, firstObjective));
                builder.AppendLine(QuestTextBuilder.BuildObjectiveDescription(database, quest, firstObjective));
            }
            else
            {
                builder.Append("[İster ").Append(quest.CurrentObjectiveIndex + 1).Append('/')
                    .Append(quest.ObjectiveCount).Append("] ");
                builder.AppendLine(QuestTextBuilder.BuildQuestTitle(database, quest, firstObjective));
                builder.AppendLine(QuestTextBuilder.BuildQuestDescription(database, quest, firstObjective));

                for (int objectiveIndex = 0; objectiveIndex < quest.ObjectiveCount; objectiveIndex++)
                {
                    if (!TryGetObjective(quest.InstanceId, objectiveIndex,
                            out QuestObjectiveState objective))
                    {
                        builder.Append("  [?] İster ").Append(objectiveIndex + 1)
                            .AppendLine(" verisi eksik.");
                        continue;
                    }

                    builder.Append("  [").Append(GetStatusLabel(objective.Status)).Append("] İster ")
                        .Append(objectiveIndex + 1).Append('/').Append(quest.ObjectiveCount).Append(": ");
                    builder.AppendLine(QuestTextBuilder.BuildObjectiveTitle(database, quest, objective));
                    builder.Append("  ").AppendLine(
                        QuestTextBuilder.BuildObjectiveDescription(database, quest, objective));
                }
            }

            int remainingDays = Mathf.Max(0,
                boundManager.ExpiryDays - (currentDay - quest.CreatedDay));
            builder.Append("Kalan süre: ").Append(remainingDays).AppendLine(" gün");
            builder.AppendLine();
        }

        Publish(questListText, builder.ToString());
    }

    private bool TryGetObjective(int questInstanceId, int objectiveIndex,
        out QuestObjectiveState objective)
    {
        NetworkList<QuestObjectiveState> objectives = boundManager.QuestObjectives;

        for (int i = 0; i < objectives.Count; i++)
        {
            QuestObjectiveState candidate = objectives[i];
            if (candidate.QuestInstanceId != questInstanceId
                || candidate.ObjectiveIndex != objectiveIndex) continue;

            objective = candidate;
            return true;
        }

        objective = default;
        return false;
    }

    private void RefreshGold()
    {
        int current = boundGold != null ? boundGold.Current : 0;
        Publish(goldText, $"Altın: {current}");
    }

    private void Publish(TMP_Text target, string text)
    {
        if (target != null) target.text = text;
        if (logToConsole) Debug.Log(text);
    }

    private static string GetStatusLabel(QuestStatus status)
    {
        switch (status)
        {
            case QuestStatus.Active: return "Aktif";
            case QuestStatus.ItemCollected: return "Eşya alındı";
            case QuestStatus.Locked: return "Bekliyor";
            case QuestStatus.Completed: return "Tamamlandı";
            default: return "?";
        }
    }
}
