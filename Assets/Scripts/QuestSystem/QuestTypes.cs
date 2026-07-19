using System;
using Unity.Netcode;

public enum QuestType
{
    TalkTo = 0,
    FetchDeliver = 1
}

// Completed quests are removed from the quest list, so there is no Completed status.
public enum QuestStatus
{
    Active = 0,
    ItemCollected = 1
}

public enum QuestEntityKind
{
    Giver = 0,
    Item = 1
}

// Must stay unmanaged (ints and enums only, no strings) to be usable inside a NetworkList.
// Quest text is never sent over the network: every client rebuilds it locally from these
// indices through QuestTextBuilder, using its own copy of the QuestDatabase asset.
public struct QuestInstanceState : INetworkSerializable, IEquatable<QuestInstanceState>
{
    public int InstanceId;
    public int TemplateIndex;
    public int NpcNameIndex;
    public int ItemNameIndex;      // -1 when the quest type has no item
    public int LocationNameIndex;  // -1 when the destination has no display name
    public int GoldReward;
    public int CreatedDay;
    public int ChainStepIndex;     // 0 = standalone quest or first chain link
    public QuestStatus Status;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref InstanceId);
        serializer.SerializeValue(ref TemplateIndex);
        serializer.SerializeValue(ref NpcNameIndex);
        serializer.SerializeValue(ref ItemNameIndex);
        serializer.SerializeValue(ref LocationNameIndex);
        serializer.SerializeValue(ref GoldReward);
        serializer.SerializeValue(ref CreatedDay);
        serializer.SerializeValue(ref ChainStepIndex);

        int status = (int)Status;
        serializer.SerializeValue(ref status);
        Status = (QuestStatus)status;
    }

    public bool Equals(QuestInstanceState other)
    {
        return InstanceId == other.InstanceId
            && TemplateIndex == other.TemplateIndex
            && NpcNameIndex == other.NpcNameIndex
            && ItemNameIndex == other.ItemNameIndex
            && LocationNameIndex == other.LocationNameIndex
            && GoldReward == other.GoldReward
            && CreatedDay == other.CreatedDay
            && ChainStepIndex == other.ChainStepIndex
            && Status == other.Status;
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
