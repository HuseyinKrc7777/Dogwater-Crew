// Turns synchronized integer parameters back into readable text on each client. Authored strings
// never cross the network; missing data renders visibly as "???" instead of throwing.
public static class QuestTextBuilder
{
    public static string BuildQuestTitle(QuestDatabase database, QuestInstanceState quest,
        QuestObjectiveState firstObjective)
    {
        QuestTemplate template = database != null ? database.GetTemplate(quest.TemplateIndex) : null;
        if (template == null) return QuestDatabase.MissingName;

        return Fill(template.TitleTemplate, database, firstObjective, quest.GoldReward);
    }

    public static string BuildQuestDescription(QuestDatabase database, QuestInstanceState quest,
        QuestObjectiveState firstObjective)
    {
        QuestTemplate template = database != null ? database.GetTemplate(quest.TemplateIndex) : null;
        if (template == null) return QuestDatabase.MissingName;

        return Fill(template.DescriptionTemplate, database, firstObjective, quest.GoldReward);
    }

    public static string BuildObjectiveTitle(QuestDatabase database, QuestInstanceState quest,
        QuestObjectiveState objective)
    {
        if (!TryGetObjectiveText(database, quest, objective.ObjectiveIndex, out string title, out _))
        {
            return QuestDatabase.MissingName;
        }

        return Fill(title, database, objective, quest.GoldReward);
    }

    public static string BuildObjectiveDescription(QuestDatabase database, QuestInstanceState quest,
        QuestObjectiveState objective)
    {
        if (!TryGetObjectiveText(database, quest, objective.ObjectiveIndex, out _, out string description))
        {
            return QuestDatabase.MissingName;
        }

        return Fill(description, database, objective, quest.GoldReward);
    }

    private static bool TryGetObjectiveText(QuestDatabase database, QuestInstanceState quest,
        int objectiveIndex, out string title, out string description)
    {
        title = string.Empty;
        description = string.Empty;

        QuestTemplate template = database != null ? database.GetTemplate(quest.TemplateIndex) : null;
        if (template == null) return false;

        return template.TryGetObjective(objectiveIndex, out _, out title, out description, out _);
    }

    private static string Fill(string pattern, QuestDatabase database, QuestObjectiveState objective,
        int goldReward)
    {
        if (string.IsNullOrEmpty(pattern)) return string.Empty;
        if (database == null) return QuestDatabase.MissingName;

        string result = pattern;
        result = result.Replace("{npcName}", database.GetNpcName(objective.NpcNameIndex));
        result = result.Replace("{itemName}", database.GetItemName(objective.ItemNameIndex));
        result = result.Replace("{location}", database.GetLocationName(objective.LocationNameIndex));
        result = result.Replace("{gold}", goldReward.ToString());

        return result;
    }
}
