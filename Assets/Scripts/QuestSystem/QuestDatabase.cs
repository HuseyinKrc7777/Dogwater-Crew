using UnityEngine;

// The shared vocabulary of the quest system. Every client owns an identical copy of this asset
// (same build), which is what allows quests to travel over the network as plain indices.
// Reordering these arrays while a session is running shifts the meaning of the indices the
// running clients already hold, so only edit them between sessions.
[CreateAssetMenu(menuName = "Dogwater/Quests/Quest Database")]
public class QuestDatabase : ScriptableObject
{
    public const string MissingName = "???";

    [SerializeField] private QuestTemplate[] templates;
    [SerializeField] private string[] npcNames;
    [SerializeField] private string[] itemNames;
    [SerializeField] private string[] locationNames;

    public int TemplateCount => templates != null ? templates.Length : 0;
    public int NpcNameCount => npcNames != null ? npcNames.Length : 0;
    public int ItemNameCount => itemNames != null ? itemNames.Length : 0;
    public int LocationNameCount => locationNames != null ? locationNames.Length : 0;

    public QuestTemplate GetTemplate(int index)
    {
        return IsInRange(TemplateCount, index) ? templates[index] : null;
    }

    // Resolves a template reference (e.g. QuestTemplate.NextTemplate) back to its network-safe index.
    // Returns -1 when the template is not part of this database.
    public int GetTemplateIndex(QuestTemplate template)
    {
        if (template == null || templates == null) return -1;

        for (int i = 0; i < templates.Length; i++)
        {
            if (templates[i] == template) return i;
        }

        return -1;
    }

    public string GetNpcName(int index) => GetPoolEntry(npcNames, index);
    public string GetItemName(int index) => GetPoolEntry(itemNames, index);
    public string GetLocationName(int index) => GetPoolEntry(locationNames, index);

    private static string GetPoolEntry(string[] pool, int index)
    {
        if (pool == null || !IsInRange(pool.Length, index)) return MissingName;

        return string.IsNullOrEmpty(pool[index]) ? MissingName : pool[index];
    }

    private static bool IsInRange(int count, int index)
    {
        return index >= 0 && index < count;
    }
}
