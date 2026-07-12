using System.Text;
using TMPro;
using Unity.Netcode;
using UnityEngine;

// Text-only quest log. Deliberately minimal: no panels, no styling, no per-quest widgets - the
// visual side of the UI is authored by hand in Unity.
//
// Rebuilds only on change events (list changed, gold changed, day rolled over), never per frame.
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

    private void Update()
    {
        // The quest system network-spawns after this UI's Start runs, so bind as soon as it shows up.
        if (boundManager == null) TryBind();
    }

    private void OnDestroy()
    {
        if (boundManager != null) boundManager.QuestStates.OnListChanged -= HandleQuestListChanged;
        if (boundGold != null) boundGold.OnGoldChanged -= HandleGoldChanged;
        if (boundClock != null) boundClock.OnDayChanged -= HandleDayChanged;
    }

    private void TryBind()
    {
        QuestManager manager = QuestManager.Instance;
        if (manager == null || !manager.IsSpawned) return;

        boundManager = manager;
        boundManager.QuestStates.OnListChanged += HandleQuestListChanged;

        boundGold = CrewGold.Instance;
        if (boundGold != null) boundGold.OnGoldChanged += HandleGoldChanged;

        boundClock = GameDayClock.Instance;
        if (boundClock != null) boundClock.OnDayChanged += HandleDayChanged;

        // A late joiner receives the list before it can subscribe, so draw once on bind.
        RebuildQuestList();
        RefreshGold();
    }

    private void HandleQuestListChanged(NetworkListEvent<QuestInstanceState> changeEvent)
    {
        RebuildQuestList();
    }

    private void HandleGoldChanged(int current)
    {
        RefreshGold();
    }

    // Remaining-time labels are relative to the current day, so they need a redraw when it rolls over.
    private void HandleDayChanged(int current)
    {
        RebuildQuestList();
    }

    private void RebuildQuestList()
    {
        if (boundManager == null) return;

        QuestDatabase database = boundManager.Database;
        NetworkList<QuestInstanceState> states = boundManager.QuestStates;
        int currentDay = boundClock != null ? boundClock.CurrentDay : 1;

        builder.Clear();
        builder.Append("--- Görevler (Gün ").Append(currentDay).AppendLine(") ---");

        if (states.Count == 0)
        {
            builder.AppendLine("Aktif görev yok.");
        }

        for (int i = 0; i < states.Count; i++)
        {
            QuestInstanceState state = states[i];

            builder.Append('[').Append(GetStatusLabel(state.Status)).Append("] ");
            builder.AppendLine(QuestTextBuilder.BuildTitle(database, state));
            builder.AppendLine(QuestTextBuilder.BuildDescription(database, state));

            if (state.ChainStepIndex > 0)
            {
                builder.Append("Adım ").Append(state.ChainStepIndex + 1).AppendLine();
            }

            int remainingDays = Mathf.Max(0, boundManager.ExpiryDays - (currentDay - state.CreatedDay));
            builder.Append("Kalan süre: ").Append(remainingDays).AppendLine(" gün");

            builder.AppendLine();
        }

        Publish(questListText, builder.ToString());
    }

    private void RefreshGold()
    {
        int current = boundGold != null ? boundGold.Current : 0;

        Publish(goldText, $"Altın: {current}");
    }

    // The UI does not exist yet, so the same text goes to the Console while logToConsole is on.
    // Both outputs are event-driven (list changed, gold changed, day rolled over), never per frame.
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
            default: return "?";
        }
    }
}
