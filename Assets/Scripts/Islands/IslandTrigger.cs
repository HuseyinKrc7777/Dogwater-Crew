using UnityEngine;

// The doorway in and out of an island. Enter goes on the silhouette island out at sea, Leave goes
// inside the island scene (a doorway, a jetty, or the boundary volume that catches anyone falling off).
//
// Deliberately a plain MonoBehaviour, not a NetworkBehaviour: the silhouette islands are plain
// GameObjects (WorldIslandController instantiates them, they carry no NetworkObject) and the island
// scenes hold no NetworkObjects either. The RPC lives on IslandManager in ShipTest, exactly like
// QuestBoard defers to QuestManager.
public class IslandTrigger : MonoBehaviour
{
    public enum Mode
    {
        Enter = 0,
        Leave = 1
    }

    [SerializeField] private Mode mode = Mode.Enter;

    [Tooltip("Enter mode only: the island this doorway leads to. Leave mode needs nothing - the server " +
             "already knows which island the player is standing on.")]
    [SerializeField] private IslandDefinition island;

    [Tooltip("Ignores repeat requests from this trigger for a moment, so brushing against it does not " +
             "fire a burst of RPCs.")]
    [Min(0f)][SerializeField] private float requestCooldownSeconds = 2f;

    // A timestamp, not a latch: an "already transitioning" bool that some path forgets to clear leaves the
    // trigger permanently dead on that client. A cooldown cannot get stuck.
    private float nextRequestTime;

    // Setup mistakes are reported the moment the trigger enters the world - when the silhouette island
    // spawns - rather than when a player has finally swum out to it and nothing happens.
    private void OnEnable()
    {
        if (mode != Mode.Enter) return;

        if (island == null)
        {
            Debug.LogError($"IslandTrigger '{name}' is an Enter trigger with no IslandDefinition assigned.", this);
            return;
        }

        if (!island.IsInBuildSettings())
        {
            Debug.LogError($"IslandTrigger '{name}': island scene '{island.SceneName}' is not in the Build " +
                           "Settings scene list, so Netcode cannot load it. Add it to the build list, or fix " +
                           $"the sceneName on the '{island.name}' asset.", this);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (Time.time < nextRequestTime) return;
        if (mode == Mode.Enter && island == null) return;   // already reported in OnEnable

        // This trigger exists on every peer (silhouettes and island scenes are loaded by everyone), so it
        // must only speak for the player it actually belongs to: without the ownership check, every client
        // would send a request for whoever walked in.
        ClientPlayerMove player = other.GetComponentInParent<ClientPlayerMove>();
        if (player == null || !player.IsOwner) return;

        // Invoking an RPC on a NetworkBehaviour that is not spawned throws, so an island trigger in a
        // scene opened without a session must stay quiet rather than break Play Mode.
        if (IslandManager.Instance == null || !IslandManager.Instance.IsSpawned)
        {
            Debug.LogWarning("IslandTrigger: no spawned IslandManager, island entry/exit does nothing.", this);
            return;
        }

        nextRequestTime = Time.time + requestCooldownSeconds;

        // The player's identity is never sent: the server reads the sender of the RPC. Duplicate or
        // nonsensical requests (already on an island, not on one at all) are dropped there.
        if (mode == Mode.Enter)
        {
            IslandManager.Instance.RequestEnterIslandRpc(island.SceneName);
        }
        else
        {
            IslandManager.Instance.RequestLeaveIslandRpc();
        }
    }
}
