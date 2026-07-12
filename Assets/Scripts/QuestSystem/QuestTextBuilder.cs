// Turns a networked QuestInstanceState back into readable text on the local client.
// Never throws and never returns null: missing templates or out-of-range indices render as "???",
// so a badly authored database shows up as visible placeholder text instead of a crash.
public static class QuestTextBuilder
{
    public static string BuildTitle(QuestDatabase database, QuestInstanceState state)
    {
        QuestTemplate template = database != null ? database.GetTemplate(state.TemplateIndex) : null;
        if (template == null) return QuestDatabase.MissingName;

        return Fill(template.TitleTemplate, database, state);
    }

    public static string BuildDescription(QuestDatabase database, QuestInstanceState state)
    {
        QuestTemplate template = database != null ? database.GetTemplate(state.TemplateIndex) : null;
        if (template == null) return QuestDatabase.MissingName;

        return Fill(template.DescriptionTemplate, database, state);
    }

    private static string Fill(string pattern, QuestDatabase database, QuestInstanceState state)
    {
        if (string.IsNullOrEmpty(pattern)) return string.Empty;

        string result = pattern;
        result = result.Replace("{npcName}", database.GetNpcName(state.NpcNameIndex));
        result = result.Replace("{itemName}", database.GetItemName(state.ItemNameIndex));
        result = result.Replace("{location}", database.GetLocationName(state.LocationNameIndex));
        result = result.Replace("{gold}", state.GoldReward.ToString());

        return result;
    }
}
