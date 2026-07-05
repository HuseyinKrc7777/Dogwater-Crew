using System;
using Unity.Collections;
using Unity.Netcode;

public struct QuestRuntimeState : INetworkSerializable, IEquatable<QuestRuntimeState>
{
    public FixedString64Bytes QuestId;
    public QuestSourceType SourceType;
    public FixedString64Bytes SourceId;
    public QuestStatus Status;
    public double StartedAtGameSeconds;
    public double ExpiresAtGameSeconds;
    public int CurrentObjectiveIndex;
    public int CompletedObjectiveMask;
    public bool RewardClaimed;

    public QuestRuntimeState(
        FixedString64Bytes questId,
        QuestSourceType sourceType,
        FixedString64Bytes sourceId,
        QuestStatus status,
        double startedAtGameSeconds,
        double expiresAtGameSeconds)
    {
        QuestId = questId;
        SourceType = sourceType;
        SourceId = sourceId;
        Status = status;
        StartedAtGameSeconds = startedAtGameSeconds;
        ExpiresAtGameSeconds = expiresAtGameSeconds;
        CurrentObjectiveIndex = 0;
        CompletedObjectiveMask = 0;
        RewardClaimed = false;
    }

    public bool IsObjectiveCompleted(int objectiveIndex)
    {
        if (objectiveIndex < 0 || objectiveIndex >= 31)
        {
            return false;
        }

        return (CompletedObjectiveMask & (1 << objectiveIndex)) != 0;
    }

    public void MarkObjectiveCompleted(int objectiveIndex)
    {
        if (objectiveIndex < 0 || objectiveIndex >= 31)
        {
            return;
        }

        CompletedObjectiveMask |= 1 << objectiveIndex;
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref QuestId);

        int sourceTypeValue = (int)SourceType;
        serializer.SerializeValue(ref sourceTypeValue);
        if (serializer.IsReader)
        {
            SourceType = (QuestSourceType)sourceTypeValue;
        }

        serializer.SerializeValue(ref SourceId);

        int statusValue = (int)Status;
        serializer.SerializeValue(ref statusValue);
        if (serializer.IsReader)
        {
            Status = (QuestStatus)statusValue;
        }

        serializer.SerializeValue(ref StartedAtGameSeconds);
        serializer.SerializeValue(ref ExpiresAtGameSeconds);
        serializer.SerializeValue(ref CurrentObjectiveIndex);
        serializer.SerializeValue(ref CompletedObjectiveMask);
        serializer.SerializeValue(ref RewardClaimed);
    }

    public bool Equals(QuestRuntimeState other)
    {
        return QuestId.Equals(other.QuestId)
            && SourceType == other.SourceType
            && SourceId.Equals(other.SourceId)
            && Status == other.Status
            && StartedAtGameSeconds.Equals(other.StartedAtGameSeconds)
            && ExpiresAtGameSeconds.Equals(other.ExpiresAtGameSeconds)
            && CurrentObjectiveIndex == other.CurrentObjectiveIndex
            && CompletedObjectiveMask == other.CompletedObjectiveMask
            && RewardClaimed == other.RewardClaimed;
    }
}
