using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

// Owns the island scenes. A player walks into an island's entry trigger, the server loads that island's
// scene ADDITIVELY (ShipTest stays alive underneath, with the ship, the water and the quest system) and
// teleports the player onto it. When the last player leaves, the scene is unloaded again.
//
// The whole island system funnels through this one server-side class: the triggers are plain
// MonoBehaviours (silhouette islands and island scenes hold no NetworkObjects), so this NetworkBehaviour
// is the only thing that may touch network state - the QuestBoard/QuestManager split, repeated.
//
// The quest system needs no code here: a loading island registers its QuestSpawnPoints, which is exactly
// what QuestManager's deferred spawning listens for.
public class IslandManager : NetworkBehaviour
{
    public static IslandManager Instance { get; private set; }

    [Header("Anchor Gate")]
    [Tooltip("The ship anchor whose authoritative rope amount controls island entry and recalls the crew " +
             "before the ship starts moving again.")]
    [SerializeField] private Anchor anchor;

    [Header("Return")]
    [Tooltip("Where a player lands when leaving an island. Put it on the ship's deck: it is read at the " +
             "moment of the teleport, so a crew that has sailed on is still found.")]
    [SerializeField] private Transform shipReturnPoint;

    [Header("Timing")]
    [Tooltip("How long the server keeps an island scene open waiting for a leaving player to confirm the " +
             "teleport landed, before unloading anyway.")]
    [Min(0.5f)][SerializeField] private float teleportConfirmTimeoutSeconds = 3f;

    [Tooltip("Retry delay when Netcode is busy with another scene event (a late joiner synchronizing, " +
             "for example) and refuses ours.")]
    [Min(0.05f)][SerializeField] private float sceneEventRetrySeconds = 0.25f;

    private enum SceneOpKind
    {
        Load = 0,
        Unload = 1
    }

    private struct SceneOp
    {
        public SceneOpKind Kind;
        public string IslandName;
    }

    // A player who has been told to go back to the ship but has not reported landing yet. The island
    // scene must stay loaded until then - see SendPlayerToShip.
    private class PendingExit
    {
        public ulong ClientId;
        public string IslandName;
    }

    // Server-only bookkeeping. None of it is networked: clients learn everything they need from the
    // scene synchronization and their own teleport RPC.
    private readonly Dictionary<string, Scene> loadedIslandScenes = new Dictionary<string, Scene>();
    private readonly Dictionary<string, List<ulong>> playersByIsland = new Dictionary<string, List<ulong>>();
    private readonly Dictionary<string, List<ulong>> entrantsByIsland = new Dictionary<string, List<ulong>>();
    private readonly Dictionary<int, PendingExit> pendingExits = new Dictionary<int, PendingExit>();

    // Netcode runs one scene event at a time ("a scene event cannot already be in progress"), so every
    // load and unload queues up here instead of being fired straight at NetworkSceneManager.
    private readonly List<SceneOp> sceneOpQueue = new List<SceneOp>();
    private bool sceneEventInFlight;
    private bool pumping;
    private SceneOp inFlightOp;
    private Coroutine retryRoutine;

    private int nextTeleportRequestId = 1;
    private bool subscribed;

    // The scene this manager lives in - the ship's world, which must never stop being the active scene.
    private Scene HomeScene => gameObject.scene;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // Every peer guards the active scene, not just the server: each machine refuses to unload its own
        // active scene, so an island that stole the slot on a single client would fail to close there and
        // that client alone would keep standing on a world nobody else can see.
        SceneManager.sceneLoaded += HandleUnitySceneLoaded;

        if (!IsServer) return;

        if (shipReturnPoint == null)
        {
            Debug.LogError("IslandManager: shipReturnPoint is not assigned. Players would have nowhere to " +
                           "return to, so leaving an island will be refused.", this);
        }

        if (anchor == null)
        {
            Debug.LogError("IslandManager: anchor is not assigned. Island entry will be refused because " +
                           "the server cannot verify that the ship is safely anchored.", this);
        }
        else
        {
            anchor.controller.releasedRopeAmount.OnValueChanged += HandleAnchorRopeAmountChanged;
        }

        NetworkManager.SceneManager.OnLoadEventCompleted += HandleLoadEventCompleted;
        NetworkManager.SceneManager.OnUnloadEventCompleted += HandleUnloadEventCompleted;
        NetworkManager.OnClientDisconnectCallback += HandleClientDisconnect;
        subscribed = true;
    }

    private void HandleUnitySceneLoaded(Scene scene, LoadSceneMode loadSceneMode)
    {
        if (loadSceneMode != LoadSceneMode.Additive) return;

        EnsureHomeSceneIsActive();
    }

    public override void OnNetworkDespawn()
    {
        Unsubscribe();

        base.OnNetworkDespawn();
    }

    public override void OnDestroy()
    {
        Unsubscribe();

        if (Instance == this) Instance = null;

        base.OnDestroy();
    }

    private void Unsubscribe()
    {
        SceneManager.sceneLoaded -= HandleUnitySceneLoaded;

        if (!subscribed) return;

        // NetworkManager can already be gone during shutdown; its SceneManager can be gone before it.
        if (NetworkManager != null)
        {
            if (NetworkManager.SceneManager != null)
            {
                NetworkManager.SceneManager.OnLoadEventCompleted -= HandleLoadEventCompleted;
                NetworkManager.SceneManager.OnUnloadEventCompleted -= HandleUnloadEventCompleted;
            }

            NetworkManager.OnClientDisconnectCallback -= HandleClientDisconnect;
        }

        if (anchor != null)
        {
            anchor.controller.releasedRopeAmount.OnValueChanged -= HandleAnchorRopeAmountChanged;
        }

        subscribed = false;
    }

    // ---------------------------------------------------------------- requests from players

    // Sent by an Enter trigger on a silhouette island. Any client may ask, so the invoke permission is
    // written out even though Everyone is the NGO 2.x default: this is behaviour we depend on.
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void RequestEnterIslandRpc(string islandName, RpcParams rpcParams = default)
    {
        EnterIsland(rpcParams.Receive.SenderClientId, islandName);
    }

    // Sent by a Leave trigger inside an island scene. The island is not a parameter: the server knows
    // where the sender is, and a client must not be able to claim otherwise.
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void RequestLeaveIslandRpc(RpcParams rpcParams = default)
    {
        LeaveIsland(rpcParams.Receive.SenderClientId);
    }

    // Sent by PlayerTeleporter once a departing player has actually landed back on the ship.
    public void HandleTeleportConfirmed(ulong clientId, int requestId)
    {
        if (!IsServer) return;

        if (!pendingExits.TryGetValue(requestId, out PendingExit exit)) return;
        if (exit.ClientId != clientId) return;   // a confirmation only counts for its own sender

        pendingExits.Remove(requestId);

        UnloadIslandIfEmpty(exit.IslandName);
    }

    // ---------------------------------------------------------------- entering

    private void EnterIsland(ulong clientId, string islandName)
    {
        if (!IsServer || !IsSpawned) return;

        // The trigger only requests entry. The authoritative decision is made here from the server-owned
        // anchor state, so a client cannot enter early by bypassing a local check.
        if (anchor == null || shipReturnPoint == null || !anchor.IsFullyDeployed) return;

        if (string.IsNullOrEmpty(islandName))
        {
            Debug.LogWarning($"IslandManager: client {clientId} asked to enter an island with no name.");
            return;
        }

        // Already there, already on the way, or still walking back to the ship from somewhere else.
        if (FindIslandOfPlayer(clientId) != null) return;
        if (IsEntrant(clientId)) return;
        if (HasPendingExit(clientId)) return;

        GetOrCreate(entrantsByIsland, islandName).Add(clientId);

        // The scene is already in the world: nothing to load, just walk in. A queued unload is cancelled
        // on the way - the crew is not leaving after all. An unload that is already IN FLIGHT cannot be
        // taken back, so that case falls through to the queue and simply loads the scene again.
        if (loadedIslandScenes.ContainsKey(islandName) && !IsUnloadInFlight(islandName))
        {
            CancelQueuedOp(SceneOpKind.Unload, islandName);
            SendEntrantsToIsland(islandName, null);
            return;
        }

        EnqueueOp(SceneOpKind.Load, islandName);
        PumpSceneQueue();
    }

    // Moves everyone waiting for this island onto it. clientsThatLoaded is the list Netcode reports after
    // a load; a client that timed out does not have the scene and must not be teleported into a hole.
    // It is null when the island was already loaded, in which case everyone has it.
    private void SendEntrantsToIsland(string islandName, List<ulong> clientsThatLoaded)
    {
        entrantsByIsland.TryGetValue(islandName, out List<ulong> entrants);

        // Everyone who asked for this island is gone again (they disconnected, or turned back, while it
        // was loading). Do not leave an island scene standing empty.
        if (entrants == null || entrants.Count == 0)
        {
            UnloadIslandIfEmpty(islandName);
            return;
        }

        IslandArrivalPoint arrival = IslandArrivalPoint.Find(islandName);

        if (arrival == null)
        {
            // Teleporting into a scene with no arrival point drops the player into empty space, so the
            // entry is refused instead. The scene is then pointless and gets unloaded again below.
            Debug.LogError($"IslandManager: island scene '{islandName}' has no IslandArrivalPoint (or its " +
                           "islandName does not match). Nobody can be sent there.");
            entrants.Clear();
            UnloadIslandIfEmpty(islandName);
            return;
        }

        Vector3 position = arrival.transform.position;
        float yaw = arrival.transform.eulerAngles.y;
        List<ulong> arrived = GetOrCreate(playersByIsland, islandName);

        for (int i = 0; i < entrants.Count; i++)
        {
            ulong clientId = entrants[i];

            if (clientsThatLoaded != null && !clientsThatLoaded.Contains(clientId))
            {
                Debug.LogWarning($"IslandManager: client {clientId} did not finish loading '{islandName}' " +
                                 "and stays on the ship.");
                continue;
            }

            // requestId 0: an arrival needs no confirmation, nothing is waiting on it.
            if (!SendTeleport(clientId, position, yaw, 0)) continue;

            arrived.Add(clientId);
        }

        entrants.Clear();

        // Everyone who asked failed to get there (all timed out or disconnected): do not leave an empty
        // island scene loaded.
        UnloadIslandIfEmpty(islandName);
    }

    // ---------------------------------------------------------------- leaving

    private void LeaveIsland(ulong clientId)
    {
        if (!IsServer || !IsSpawned) return;

        // Changed their mind while the island was still loading: just take them out of the queue.
        string entrantIsland = FindIslandOfEntrant(clientId);
        if (entrantIsland != null)
        {
            entrantsByIsland[entrantIsland].Remove(clientId);
            UnloadIslandIfEmpty(entrantIsland);
            return;
        }

        string islandName = FindIslandOfPlayer(clientId);
        if (islandName == null) return;   // not on an island: nothing to do

        if (shipReturnPoint == null)
        {
            // Without a return point the player would be teleported into nothing. Leaving them on the
            // island is the safe failure: the scene stays loaded and they keep their footing.
            Debug.LogError("IslandManager: shipReturnPoint is not assigned, cannot bring a player back " +
                           "from the island.", this);
            return;
        }

        playersByIsland[islandName].Remove(clientId);

        SendPlayerToShip(clientId, islandName);
    }

    // The ship starts moving again as soon as the rope leaves the fully-deployed range. Cancel anyone
    // still waiting for an island load and send every player already on an island through the existing
    // confirmed teleport path. Pending exits are left alone: they are already returning to the ship.
    private void HandleAnchorRopeAmountChanged(float previousValue, float currentValue)
    {
        if (!IsServer || !IsSpawned) return;
        if (previousValue <= Anchor.FullyDeployedRopeThreshold) return;
        if (currentValue > Anchor.FullyDeployedRopeThreshold) return;
        if (!HasCrewOnOrEnteringIsland()) return;

        ShowAnchorRecallWarningRpc();
        RecallCrewToShip();
    }

    [Rpc(SendTo.Everyone)]
    private void ShowAnchorRecallWarningRpc()
    {
        Debug.LogWarning("Çapa kaldırılıyor! Adadaki mürettebat gemiye geri çağrılıyor.", this);
    }

    private bool HasCrewOnOrEnteringIsland()
    {
        foreach (List<ulong> entrants in entrantsByIsland.Values)
        {
            if (entrants.Count > 0) return true;
        }

        foreach (List<ulong> players in playersByIsland.Values)
        {
            if (players.Count > 0) return true;
        }

        return false;
    }

    private void RecallCrewToShip()
    {
        if (shipReturnPoint == null)
        {
            Debug.LogError("IslandManager: the anchor was raised while shipReturnPoint is not assigned. " +
                           "Players on islands cannot be recalled safely.", this);
            return;
        }

        foreach (KeyValuePair<string, List<ulong>> pair in entrantsByIsland)
        {
            if (pair.Value.Count == 0) continue;

            pair.Value.Clear();
            UnloadIslandIfEmpty(pair.Key);
        }

        foreach (KeyValuePair<string, List<ulong>> pair in playersByIsland)
        {
            List<ulong> players = pair.Value;

            // Remove before sending so the pending-exit handshake becomes the only thing keeping the
            // scene alive. Iterating backwards lets us mutate the list without allocating a copy.
            for (int i = players.Count - 1; i >= 0; i--)
            {
                ulong clientId = players[i];
                players.RemoveAt(i);
                SendPlayerToShip(clientId, pair.Key);
            }

            UnloadIslandIfEmpty(pair.Key);
        }
    }

    // The island scene may only be unloaded once the player is off it. A teleport RPC and a scene event
    // are different Netcode messages and are not guaranteed to be applied in the order they were sent, so
    // the server does not assume: it waits for the owner to report that it landed. If that report never
    // comes (a client that dies mid-teleport), the timeout unloads anyway.
    private void SendPlayerToShip(ulong clientId, string islandName)
    {
        int requestId = nextTeleportRequestId++;
        pendingExits[requestId] = new PendingExit { ClientId = clientId, IslandName = islandName };

        if (!SendTeleport(clientId, shipReturnPoint.position, shipReturnPoint.eulerAngles.y, requestId))
        {
            // The player is gone (disconnected mid-request): no confirmation will ever arrive.
            pendingExits.Remove(requestId);
            UnloadIslandIfEmpty(islandName);
            return;
        }

        // A host-owner can apply the teleport and confirm it immediately while SendTeleport is still on
        // the stack. In that case HandleTeleportConfirmed has already removed the record and no timeout
        // coroutine is needed. Remote clients leave the record in place until their confirmation arrives.
        if (pendingExits.ContainsKey(requestId))
        {
            StartCoroutine(ExitConfirmationTimeout(requestId));
        }
    }

    private IEnumerator ExitConfirmationTimeout(int requestId)
    {
        yield return new WaitForSeconds(teleportConfirmTimeoutSeconds);

        if (!IsServer || !IsSpawned) yield break;
        if (!pendingExits.TryGetValue(requestId, out PendingExit exit)) yield break;   // confirmed in time

        pendingExits.Remove(requestId);

        Debug.LogWarning($"IslandManager: client {exit.ClientId} never confirmed its teleport off " +
                         $"'{exit.IslandName}'. Unloading the island anyway.");

        UnloadIslandIfEmpty(exit.IslandName);
    }

    private void UnloadIslandIfEmpty(string islandName)
    {
        if (!IsServer || !IsSpawned) return;
        if (!IsIslandEmpty(islandName)) return;

        // Not in the world yet: if its load is still sitting in the queue, nobody is left who wants it,
        // so drop the operation instead of loading a scene only to unload it again. A load that has
        // already STARTED cannot be taken back - that one resolves itself when it completes and finds no
        // entrants waiting.
        if (!loadedIslandScenes.ContainsKey(islandName))
        {
            CancelQueuedOp(SceneOpKind.Load, islandName);
            return;
        }

        EnqueueOp(SceneOpKind.Unload, islandName);
        PumpSceneQueue();
    }

    // Nobody standing on it, nobody on the way to it, and nobody still stepping off it.
    private bool IsIslandEmpty(string islandName)
    {
        if (playersByIsland.TryGetValue(islandName, out List<ulong> players) && players.Count > 0) return false;
        if (entrantsByIsland.TryGetValue(islandName, out List<ulong> entrants) && entrants.Count > 0) return false;

        foreach (PendingExit exit in pendingExits.Values)
        {
            if (exit.IslandName == islandName) return false;
        }

        return true;
    }

    // ---------------------------------------------------------------- scene queue

    private void EnqueueOp(SceneOpKind kind, string islandName)
    {
        for (int i = 0; i < sceneOpQueue.Count; i++)
        {
            if (sceneOpQueue[i].Kind == kind && sceneOpQueue[i].IslandName == islandName) return;
        }

        sceneOpQueue.Add(new SceneOp { Kind = kind, IslandName = islandName });
    }

    private void CancelQueuedOp(SceneOpKind kind, string islandName)
    {
        for (int i = sceneOpQueue.Count - 1; i >= 0; i--)
        {
            if (sceneOpQueue[i].Kind != kind) continue;
            if (sceneOpQueue[i].IslandName != islandName) continue;

            sceneOpQueue.RemoveAt(i);
        }
    }

    private bool IsUnloadInFlight(string islandName)
    {
        return sceneEventInFlight && inFlightOp.Kind == SceneOpKind.Unload && inFlightOp.IslandName == islandName;
    }

    // Runs one scene operation at a time, because Netcode allows exactly one. The important part is that
    // every operation is RE-VALIDATED the moment it is taken off the queue: the world may have moved on
    // while it waited (the crew came back to an island that was about to be unloaded, a second player
    // asked for a scene that has since loaded). That single check is what makes the enter/leave races
    // harmless instead of a source of ghost scenes.
    private void PumpSceneQueue()
    {
        // Draining the queue can enqueue more work (sending entrants in can leave an island empty, which
        // queues its unload) and that path calls back into this method. Without the guard the nested call
        // would start a scene event while the outer loop is still running and the loop would carry on
        // pulling operations behind its back. The nested call simply returns; the outer loop picks the
        // new operation up on its next turn.
        if (!IsServer || !IsSpawned || sceneEventInFlight || pumping) return;

        pumping = true;

        try
        {
            while (sceneOpQueue.Count > 0)
            {
                SceneOp op = sceneOpQueue[0];
                sceneOpQueue.RemoveAt(0);

                if (op.Kind == SceneOpKind.Load)
                {
                    if (loadedIslandScenes.ContainsKey(op.IslandName))
                    {
                        SendEntrantsToIsland(op.IslandName, null);   // it arrived by another route meanwhile
                        continue;
                    }
                }
                else
                {
                    if (!loadedIslandScenes.ContainsKey(op.IslandName)) continue;
                    if (!IsIslandEmpty(op.IslandName)) continue;     // someone came back: cancel the unload
                }

                SceneEventProgressStatus status = op.Kind == SceneOpKind.Load
                    ? NetworkManager.SceneManager.LoadScene(op.IslandName, LoadSceneMode.Additive)
                    : NetworkManager.SceneManager.UnloadScene(loadedIslandScenes[op.IslandName]);

                if (status == SceneEventProgressStatus.Started)
                {
                    sceneEventInFlight = true;
                    inFlightOp = op;
                    return;
                }

                if (status == SceneEventProgressStatus.SceneEventInProgress)
                {
                    // Not our event: a client synchronizing on join is a scene event too. Put the
                    // operation back and try again shortly - nothing is lost, it just waits its turn.
                    sceneOpQueue.Insert(0, op);
                    ScheduleRetry();
                    return;
                }

                // Anything else (InvalidSceneName, SceneNotLoaded, SceneFailedVerification...) is a setup
                // error that retrying cannot fix, so the operation is dropped rather than looped on.
                Debug.LogError($"IslandManager: {op.Kind} of island scene '{op.IslandName}' failed with " +
                               $"{status}. For a load, check that the scene is in the Build Settings scene " +
                               "list and that its name matches the trigger exactly.");

                if (op.Kind == SceneOpKind.Load) DropEntrants(op.IslandName);
                else loadedIslandScenes.Remove(op.IslandName);
            }
        }
        finally
        {
            pumping = false;
        }
    }

    private void ScheduleRetry()
    {
        if (retryRoutine != null) return;

        retryRoutine = StartCoroutine(RetryAfterDelay());
    }

    private IEnumerator RetryAfterDelay()
    {
        yield return new WaitForSeconds(sceneEventRetrySeconds);

        retryRoutine = null;

        if (!IsServer || !IsSpawned) yield break;

        PumpSceneQueue();
    }

    private void HandleLoadEventCompleted(string sceneName, LoadSceneMode loadSceneMode, List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
    {
        if (!IsServer || !IsSpawned) return;
        if (!sceneEventInFlight) return;
        if (inFlightOp.Kind != SceneOpKind.Load || inFlightOp.IslandName != sceneName) return;

        sceneEventInFlight = false;

        EnsureHomeSceneIsActive();

        Scene scene = SceneManager.GetSceneByName(sceneName);

        if (!scene.IsValid() || !scene.isLoaded)
        {
            Debug.LogError($"IslandManager: island scene '{sceneName}' reported a completed load but is not " +
                           "actually loaded on the server.");
            DropEntrants(sceneName);
            PumpSceneQueue();
            return;
        }

        loadedIslandScenes[sceneName] = scene;

        if (clientsTimedOut != null && clientsTimedOut.Count > 0)
        {
            Debug.LogWarning($"IslandManager: {clientsTimedOut.Count} client(s) timed out loading '{sceneName}'.");
        }

        // The island's QuestSpawnPoints registered while this scene loaded, so QuestManager has already
        // put the quest NPCs and items in place by the time anyone walks in.
        SendEntrantsToIsland(sceneName, clientsCompleted);

        PumpSceneQueue();
    }

    // Two things depend on the ship's scene staying the active one, and both fail quietly if it does not:
    // Unity refuses to unload the active scene (the island would never close again), and anything
    // instantiated at runtime without a parent - quest entities, a late joiner's player object - lands in
    // the active scene and would be destroyed with the island when it unloads.
    //
    // Loading a scene should not steal the active slot, but this is too cheap not to enforce, and the
    // warning names whoever took it.
    private void EnsureHomeSceneIsActive()
    {
        Scene active = SceneManager.GetActiveScene();
        if (active == HomeScene) return;

        Debug.LogWarning($"IslandManager: '{active.name}' had become the active scene; putting " +
                         $"'{HomeScene.name}' back. An active scene cannot be unloaded, and runtime-spawned " +
                         "objects land in it.");

        SceneManager.SetActiveScene(HomeScene);
    }

    private void HandleUnloadEventCompleted(string sceneName, LoadSceneMode loadSceneMode, List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
    {
        if (!IsServer || !IsSpawned) return;
        if (!sceneEventInFlight) return;
        if (inFlightOp.Kind != SceneOpKind.Unload || inFlightOp.IslandName != sceneName) return;

        sceneEventInFlight = false;

        loadedIslandScenes.Remove(sceneName);

        // The scene's QuestSpawnPoints deregistered as it unloaded, so QuestManager has already despawned
        // the quest entities that stood on them and put them back in its pending queue. The quests
        // themselves are untouched and stay in the log.
        PumpSceneQueue();
    }

    // ---------------------------------------------------------------- disconnects

    private void HandleClientDisconnect(ulong clientId)
    {
        if (!IsServer || !IsSpawned) return;

        string entrantIsland = FindIslandOfEntrant(clientId);
        if (entrantIsland != null) entrantsByIsland[entrantIsland].Remove(clientId);

        // A player who disconnects mid-teleport will never confirm; drop the record so the island is not
        // held open by a client that no longer exists.
        int pendingExitId = FindPendingExitId(clientId);
        string exitIsland = pendingExitId != 0 ? pendingExits[pendingExitId].IslandName : null;
        if (pendingExitId != 0) pendingExits.Remove(pendingExitId);

        string playerIsland = FindIslandOfPlayer(clientId);
        if (playerIsland != null) playersByIsland[playerIsland].Remove(clientId);

        // Rare enough to be worth a line: it is the only way to tell "Netcode never reported the
        // disconnect" apart from "the island failed to close", and the two look identical from outside.
        if (playerIsland != null || entrantIsland != null || exitIsland != null)
        {
            Debug.Log($"IslandManager: client {clientId} disconnected while on/near island " +
                      $"'{playerIsland ?? entrantIsland ?? exitIsland}'; releasing it.");
        }

        if (entrantIsland != null) UnloadIslandIfEmpty(entrantIsland);
        if (exitIsland != null) UnloadIslandIfEmpty(exitIsland);
        if (playerIsland != null) UnloadIslandIfEmpty(playerIsland);
    }

    // ---------------------------------------------------------------- helpers

    private bool SendTeleport(ulong clientId, Vector3 position, float yaw, int requestId)
    {
        if (!NetworkManager.ConnectedClients.TryGetValue(clientId, out NetworkClient client)) return false;

        NetworkObject playerObject = client.PlayerObject;
        if (playerObject == null) return false;

        if (!playerObject.TryGetComponent(out PlayerTeleporter teleporter))
        {
            Debug.LogError("IslandManager: the player prefab has no PlayerTeleporter component, so players " +
                           "cannot be moved to or from an island.");
            return false;
        }

        teleporter.TeleportRpc(position, yaw, requestId);

        return true;
    }

    private void DropEntrants(string islandName)
    {
        if (entrantsByIsland.TryGetValue(islandName, out List<ulong> entrants)) entrants.Clear();
    }

    private string FindIslandOfPlayer(ulong clientId)
    {
        foreach (KeyValuePair<string, List<ulong>> pair in playersByIsland)
        {
            if (pair.Value.Contains(clientId)) return pair.Key;
        }

        return null;
    }

    private string FindIslandOfEntrant(ulong clientId)
    {
        foreach (KeyValuePair<string, List<ulong>> pair in entrantsByIsland)
        {
            if (pair.Value.Contains(clientId)) return pair.Key;
        }

        return null;
    }

    private bool IsEntrant(ulong clientId)
    {
        return FindIslandOfEntrant(clientId) != null;
    }

    private bool HasPendingExit(ulong clientId)
    {
        return FindPendingExitId(clientId) != 0;
    }

    private int FindPendingExitId(ulong clientId)
    {
        foreach (KeyValuePair<int, PendingExit> pair in pendingExits)
        {
            if (pair.Value.ClientId == clientId) return pair.Key;
        }

        return 0;   // ids start at 1, so 0 means "none"
    }

    private static List<ulong> GetOrCreate(Dictionary<string, List<ulong>> map, string key)
    {
        if (map.TryGetValue(key, out List<ulong> list)) return list;

        list = new List<ulong>();
        map[key] = list;

        return list;
    }
}
