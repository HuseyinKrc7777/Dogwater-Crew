using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public class QuestBoardInteractionPoint : NetworkBehaviour, IHandInput
{
    [SerializeField] private bool debugLogs = true;
    [SerializeField] private string boardId = "MainBoard";
    [SerializeField] private QuestDefinition[] boardQuests;

    public void OnInteract(Player player)
    {
        LogQuestDebug($"Board interacted. BoardId='{boardId}', IsSpawned={IsSpawned}, IsServer={IsServer}, quest count={(boardQuests == null ? 0 : boardQuests.Length)}.");

        QuestManager questManager = QuestManager.Instance;
        if (questManager == null)
        {
            Debug.LogWarning("Board interaction ignored because no QuestManager is available.", this);
            return;
        }

        if (string.IsNullOrWhiteSpace(boardId))
        {
            Debug.LogWarning("Board interaction ignored because Board Id is empty.", this);
            return;
        }

        ActivateBoardQuestsRpc();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void ActivateBoardQuestsRpc()
    {
        if (QuestManager.Instance == null)
        {
            return;
        }

        QuestManager.Instance.ActivateBoardQuests(boardQuests, boardId);
    }

    private void LogQuestDebug(string message)
    {
        if (!debugLogs)
        {
            return;
        }

        Debug.Log($"[QuestBoardInteractionPoint] {message}", this);
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
