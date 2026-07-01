using System;
using UnityEngine;

public enum QuestSourceType
{
    Board,
    Npc
}

public enum QuestStatus
{
    Hidden,
    Available,
    Active,
    ReadyToTurnIn,
    Completed,
    Expired,
    Failed
}

public enum QuestObjectiveMode
{
    Sequential,
    Parallel
}

public enum QuestObjectiveType
{
    InteractWithTarget
}

[Serializable]
public class QuestObjectiveDefinition
{
    [SerializeField] private QuestObjectiveType objectiveType = QuestObjectiveType.InteractWithTarget;
    [SerializeField] private string targetId;
    [SerializeField] private string description;

    public QuestObjectiveType ObjectiveType => objectiveType;
    public string TargetId => targetId;
    public string Description => description;
}

[Serializable]
public class QuestRewardDefinition
{
    [SerializeField] private string rewardId = "Gold";
    [SerializeField] private int amount;
    [SerializeField] private string description;

    public string RewardId => rewardId;
    public int Amount => amount;
    public string Description => description;
}

[CreateAssetMenu(menuName = "Dogwater/Quests/Quest Definition")]
public class QuestDefinition : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private string questId;
    [SerializeField] private string title;
    [TextArea]
    [SerializeField] private string description;

    [Header("Turn In")]
    [SerializeField] private string turnInTargetId;

    [Header("Board Visibility")]
    [Min(1)]
    [SerializeField] private int visibleFromDay = 1;
    [Min(1)]
    [SerializeField] private int visibleUntilDay = 1;

    [Header("Timing")]
    [Min(0)]
    [SerializeField] private int durationDays = 1;

    [Header("Objectives")]
    [SerializeField] private QuestObjectiveMode objectiveMode = QuestObjectiveMode.Sequential;
    [SerializeField] private QuestObjectiveDefinition[] objectives = Array.Empty<QuestObjectiveDefinition>();

    [Header("Reward")]
    [SerializeField] private QuestRewardDefinition reward = new QuestRewardDefinition();

    public string QuestId => questId;
    public string Title => title;
    public string Description => description;
    public string TurnInTargetId => turnInTargetId;
    public int VisibleFromDay => visibleFromDay;
    public int VisibleUntilDay => visibleUntilDay;
    public int DurationDays => durationDays;
    public QuestObjectiveMode ObjectiveMode => objectiveMode;
    public QuestObjectiveDefinition[] Objectives => objectives;
    public QuestRewardDefinition Reward => reward;
}
