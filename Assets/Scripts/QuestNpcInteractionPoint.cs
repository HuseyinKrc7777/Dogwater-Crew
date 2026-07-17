using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public class QuestNpcInteractionPoint : NetworkBehaviour, IHandInput
{
    [SerializeField] private bool debugLogs = true;
    [SerializeField] private string npcId;
    [SerializeField] private QuestDefinition npcQuest;
    [SerializeField] private bool canTurnInQuestsHere = true;

    public void OnInteract(Player player)
    {
        LogQuestDebug($"NPC interacted. NpcId='{npcId}', IsSpawned={IsSpawned}, IsServer={IsServer}, can turn in={canTurnInQuestsHere}, quest='{(npcQuest == null ? "null" : npcQuest.QuestId)}'.");

        QuestManager questManager = QuestManager.Instance;
        if (questManager == null)
        {
            Debug.LogWarning("NPC quest interaction ignored because no QuestManager is available.", this);
            return;
        }

        if (string.IsNullOrWhiteSpace(npcId))
        {
            Debug.LogWarning("NPC quest interaction ignored because NPC Id is empty.", this);
            return;
        }

        if (canTurnInQuestsHere)
        {
            TurnInQuestsRpc();
        }

        if (npcQuest != null && !string.IsNullOrWhiteSpace(npcQuest.QuestId))
        {
            AcceptNpcQuestRpc();
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void TurnInQuestsRpc()
    {
        if (QuestManager.Instance == null)
        {
            return;
        }

        QuestManager.Instance.TurnInQuests(npcId);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void AcceptNpcQuestRpc()
    {
        if (QuestManager.Instance == null)
        {
            return;
        }

        QuestManager.Instance.AcceptNpcQuest(npcQuest, npcId);
    }

    private void LogQuestDebug(string message)
    {
        if (!debugLogs)
        {
            return;
        }

        Debug.Log($"[QuestNpcInteractionPoint] {message}", this);
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
