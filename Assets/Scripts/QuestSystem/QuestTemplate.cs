using System;
using UnityEngine;

[Serializable]
public class QuestObjectiveDefinition
{
    [Tooltip("The tested interaction flow used by this objective.")]
    [SerializeField] private QuestObjectiveType type;
    [SerializeField] private string titleTemplate;
    [TextArea]
    [SerializeField] private string descriptionTemplate;
    [Tooltip("Keep the previous objective's NPC name. Ignored for the first objective.")]
    [SerializeField] private bool keepNpcFromPrevious = true;

    public QuestType Type => (QuestType)type;
    public string TitleTemplate => titleTemplate;
    public string DescriptionTemplate => descriptionTemplate;
    public bool KeepNpcFromPrevious => keepNpcFromPrevious;
}

// One authored quest pattern. TalkTo and FetchDeliver are single-objective quests. MultiStep is a
// logical quest containing an ordered objective list and pays only after its final objective.
// Placeholders understood by QuestTextBuilder: {npcName}, {itemName}, {location}, {gold}
[CreateAssetMenu(menuName = "Dogwater/Quests/Quest Template")]
public class QuestTemplate : ScriptableObject
{
    [SerializeField] private QuestType type;
    [SerializeField] private string titleTemplate;
    [TextArea]
    [SerializeField] private string descriptionTemplate;

    [Header("Multi-Step Objectives")]
    [Tooltip("Used only when Type is MultiStep. Objectives run in this exact order and are all visible from the start.")]
    [SerializeField] private QuestObjectiveDefinition[] objectives;

    [Header("Reward")]
    [Min(0)][SerializeField] private int minGold = 10;
    [Min(0)][SerializeField] private int maxGold = 30;

    [Header("Availability")]
    [Tooltip("Boards do not offer this quest before this in-game day.")]
    [Min(1)][SerializeField] private int minDay = 1;
    [Tooltip("Relative chance of being picked among the eligible templates.")]
    [Min(0f)][SerializeField] private float weight = 1f;
    [Tooltip("Chain follow-up templates must turn this off so boards never roll them directly.")]
    [SerializeField] private bool boardSelectable = true;

    [Header("Chain")]
    [Tooltip("Generated as a new, separately rewarded quest after this whole quest is completed.")]
    [SerializeField] private QuestTemplate nextTemplate;

    public QuestType Type => type;
    public string TitleTemplate => titleTemplate;
    public string DescriptionTemplate => descriptionTemplate;
    public int MinGold => minGold;
    public int MaxGold => Mathf.Max(minGold, maxGold);
    public int MinDay => minDay;
    public float Weight => weight;
    public bool BoardSelectable => boardSelectable;
    public QuestTemplate NextTemplate => nextTemplate;
    public int ObjectiveCount => type == QuestType.MultiStep
        ? (objectives != null ? objectives.Length : 0)
        : 1;

    public bool TryGetObjective(int index, out QuestType objectiveType, out string objectiveTitle,
        out string objectiveDescription, out bool keepNpcFromPrevious)
    {
        if (type != QuestType.MultiStep)
        {
            objectiveType = type;
            objectiveTitle = titleTemplate;
            objectiveDescription = descriptionTemplate;
            keepNpcFromPrevious = false;
            return index == 0;
        }

        if (objectives == null || index < 0 || index >= objectives.Length || objectives[index] == null)
        {
            objectiveType = default;
            objectiveTitle = string.Empty;
            objectiveDescription = string.Empty;
            keepNpcFromPrevious = false;
            return false;
        }

        QuestObjectiveDefinition objective = objectives[index];
        objectiveType = objective.Type;
        objectiveTitle = objective.TitleTemplate;
        objectiveDescription = objective.DescriptionTemplate;
        keepNpcFromPrevious = objective.KeepNpcFromPrevious;
        return true;
    }
}
