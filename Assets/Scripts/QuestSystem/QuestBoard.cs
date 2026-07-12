using UnityEngine;

// A notice board. Reading it generates the day's quests for the whole crew.
//
// Deliberately a plain MonoBehaviour, not a NetworkBehaviour: the RPC lives on the in-scene
// QuestManager, so a board can sit on an island prefab that is not a NetworkObject.
// The collider must be on this same GameObject - FirstPersonController raycasts and calls
// TryGetComponent on the hit collider itself, not on its parents.
public class QuestBoard : MonoBehaviour, IHandInput
{
    [Tooltip("Must be unique per board: the server tracks the daily generation limit per index.")]
    [SerializeField] private int boardIndex;

    public void OnInteract(Player player)
    {
        if (QuestManager.Instance == null)
        {
            Debug.LogWarning("QuestBoard: no QuestManager found in the scene.");
            return;
        }

        QuestManager.Instance.RequestBoardQuestsRpc(boardIndex);
    }

    // Callbacks this board does not use. Empty bodies on purpose: FirstPersonController always
    // calls OnUnInteract when the player lets go, so a NotImplementedException here would throw
    // every single time.
    public void OnUnInteract(Player player) { }
    public void OnHandInput(float xValue, float yValue) { }
    public void OnRightHandInput(float xValue, float yValue) { }
    public void OnButtonInput() { }
}
