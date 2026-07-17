using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public class QuestObjectiveInteractionPoint : NetworkBehaviour, IHandInput
{
    [SerializeField] private bool debugLogs = true;
    [SerializeField] private string targetId;

    public void OnInteract(Player player)
    {
        LogQuestDebug($"Objective interacted. TargetId='{targetId}', IsSpawned={IsSpawned}, IsServer={IsServer}.");

        QuestManager questManager = QuestManager.Instance;
        if (questManager == null)
        {
            Debug.LogWarning("Objective interaction ignored because no QuestManager is available.", this);
            return;
        }

        if (string.IsNullOrWhiteSpace(targetId))
        {
            Debug.LogWarning("Objective interaction ignored because Target Id is empty.", this);
            return;
        }

        CompleteObjectiveRpc();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void CompleteObjectiveRpc()
    {
        if (QuestManager.Instance == null)
        {
            return;
        }

        QuestManager.Instance.CompleteInteractionObjective(targetId);
    }

    private void LogQuestDebug(string message)
    {
        if (!debugLogs)
        {
            return;
        }

        Debug.Log($"[QuestObjectiveInteractionPoint] {message}", this);
    }

    public void OnUnInteract(Player player)
    {
    }

    public void OnHandInput(float xValue, float yValue)
    {
    }

    public void OnRightHandInput(float xValue, float yValue)
    {
    }

    public void OnButtonInput()
    {
    }
}
