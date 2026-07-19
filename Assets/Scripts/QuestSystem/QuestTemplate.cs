using UnityEngine;

// One authored quest pattern. The server picks a template, rolls its parameters and stores the
// result as a QuestInstanceState; the text itself is rebuilt on every client from this asset.
// Placeholders understood by QuestTextBuilder: {npcName}, {itemName}, {location}, {gold}
[CreateAssetMenu(menuName = "Dogwater/Quests/Quest Template")]
public class QuestTemplate : ScriptableObject
{
    [SerializeField] private QuestType type;
    [SerializeField] private string titleTemplate;
    [TextArea]
    [SerializeField] private string descriptionTemplate;

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
    [Tooltip("Generated when this quest is completed. Must also be listed in the QuestDatabase templates array.")]
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
}
