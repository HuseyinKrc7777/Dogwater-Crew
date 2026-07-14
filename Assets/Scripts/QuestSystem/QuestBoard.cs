using System.Collections.Generic;
using UnityEngine;

// A notice board. Reading it generates the day's quests for the whole crew.
//
// Deliberately a plain MonoBehaviour, not a NetworkBehaviour: the RPC lives on the in-scene
// QuestManager, so a board can sit anywhere - on the ship, or inside an island scene that holds no
// NetworkObjects at all.
// The collider must be on this same GameObject - FirstPersonController raycasts and calls
// TryGetComponent on the hit collider itself, not on its parents.
public class QuestBoard : MonoBehaviour, IHandInput
{
    [Tooltip("Must be unique across EVERY board in the game, island scenes included. The server tracks " +
             "the once-per-day generation limit per index and does not care which scene a board lives in.")]
    [SerializeField] private int boardIndex;

    // Boards live in island scenes that are authored separately and loaded at runtime, so two of them
    // keeping the default index is an easy mistake - and a silent one: reading one board would use up
    // the other one's daily batch, on an island the crew has not even visited. The registry exists
    // only to turn that into a visible warning.
    private static readonly List<QuestBoard> All = new List<QuestBoard>();

    private void OnEnable()
    {
        if (All.Contains(this)) return;

        WarnOnDuplicateIndex();
        All.Add(this);
    }

    private void OnDisable()
    {
        All.Remove(this);
    }

    private void WarnOnDuplicateIndex()
    {
        for (int i = 0; i < All.Count; i++)
        {
            QuestBoard other = All[i];
            if (other == null) continue;
            if (other.boardIndex != boardIndex) continue;

            Debug.LogWarning($"QuestBoard '{name}' uses boardIndex {boardIndex}, which '{other.name}' already uses. " +
                             "Boards share one global index space: reading one of them consumes the other's quest batch " +
                             "for the day. Give every board its own index.", this);
            return;
        }
    }

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
