using System;
using Unity.Netcode;

public enum QuestType
{
    TalkTo = 0,
    FetchDeliver = 1,
    MultiStep = 2
}

// MultiStep is a quest container, not an objective. Keeping a separate Inspector enum prevents
// authors from nesting MultiStep objectives accidentally.
public enum QuestObjectiveType
{
    TalkTo = 0,
    FetchDeliver = 1
}

// A quest disappears when its final objective is completed. Completed is kept here because
// earlier objectives of a multi-step quest remain visible in the log until the whole quest ends.
public enum QuestStatus
{
    Active = 0,
    ItemCollected = 1,
    Locked = 2,
    Completed = 3
}

public enum QuestEntityKind
{
    Giver = 0,
    Item = 1
}

// Persistent state for one logical quest. Objective-specific parameters live in
// QuestObjectiveState so every objective can be shown to clients from the moment the quest is
// generated. Both structs must stay unmanaged for NetworkList.
public struct QuestInstanceState : INetworkSerializable, IEquatable<QuestInstanceState>
{
    public int InstanceId;
    public int TemplateIndex;
    public int GoldReward;
    public int CreatedDay;
    public int ChainStepIndex;
    public int CurrentObjectiveIndex;
    public int ObjectiveCount;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref InstanceId);
        serializer.SerializeValue(ref TemplateIndex);
        serializer.SerializeValue(ref GoldReward);
        serializer.SerializeValue(ref CreatedDay);
        serializer.SerializeValue(ref ChainStepIndex);
        serializer.SerializeValue(ref CurrentObjectiveIndex);
        serializer.SerializeValue(ref ObjectiveCount);
    }

    public bool Equals(QuestInstanceState other)
    {
        return InstanceId == other.InstanceId
            && TemplateIndex == other.TemplateIndex
            && GoldReward == other.GoldReward
            && CreatedDay == other.CreatedDay
            && ChainStepIndex == other.ChainStepIndex
            && CurrentObjectiveIndex == other.CurrentObjectiveIndex
            && ObjectiveCount == other.ObjectiveCount;
    }

    public override bool Equals(object obj)
    {
        return obj is QuestInstanceState other && Equals(other);
    }

    public override int GetHashCode()
    {
        return InstanceId;
    }
}

// One authored requirement inside a logical quest. Strings still never cross the network: the
// quest template and ObjectiveIndex identify the authored text, while only rolled indices and
// progress are synchronized.
public struct QuestObjectiveState : INetworkSerializable, IEquatable<QuestObjectiveState>
{
    public int QuestInstanceId;
    public int ObjectiveIndex;
    public int NpcNameIndex;
    public int ItemNameIndex;
    public int LocationNameIndex;
    public QuestStatus Status;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref QuestInstanceId);
        serializer.SerializeValue(ref ObjectiveIndex);
        serializer.SerializeValue(ref NpcNameIndex);
        serializer.SerializeValue(ref ItemNameIndex);
        serializer.SerializeValue(ref LocationNameIndex);

        int status = (int)Status;
        serializer.SerializeValue(ref status);
        Status = (QuestStatus)status;
    }

    public bool Equals(QuestObjectiveState other)
    {
        return QuestInstanceId == other.QuestInstanceId
            && ObjectiveIndex == other.ObjectiveIndex
            && NpcNameIndex == other.NpcNameIndex
            && ItemNameIndex == other.ItemNameIndex
            && LocationNameIndex == other.LocationNameIndex
            && Status == other.Status;
    }

    public override bool Equals(object obj)
    {
        return obj is QuestObjectiveState other && Equals(other);
    }

    public override int GetHashCode()
    {
        return (QuestInstanceId * 397) ^ ObjectiveIndex;
    }
}
