using Unity.Netcode;
using UnityEngine;

// A quest NPC or quest item spawned into the world by the server. The prefab needs a collider on
// this same GameObject: FirstPersonController raycasts and calls TryGetComponent on the collider
// it hits, not on its parents.
[RequireComponent(typeof(NetworkObject))]
public class QuestEntity : NetworkBehaviour, IHandInput
{
    // Server-write. Written in OnNetworkSpawn, which NGO runs BEFORE it serializes the spawn message
    // to the clients - so the values still ride along with the spawn payload and every client,
    // including late joiners, immediately knows what this object belongs to.
    private NetworkVariable<int> questInstanceId = new NetworkVariable<int>(0);
    private NetworkVariable<int> objectiveIndex = new NetworkVariable<int>(0);
    private NetworkVariable<int> kind = new NetworkVariable<int>((int)QuestEntityKind.Giver);

    // Plain server-side fields, handed over between Instantiate and Spawn. Writing the
    // NetworkVariables directly at that point works, but NGO warns about it: the variable has not
    // been bound to its NetworkBehaviour yet, so it cannot mark itself dirty.
    private int pendingQuestInstanceId;
    private int pendingObjectiveIndex;
    private QuestEntityKind pendingKind;

    public int QuestInstanceId => questInstanceId.Value;
    public int ObjectiveIndex => objectiveIndex.Value;
    public QuestEntityKind Kind => (QuestEntityKind)kind.Value;

    // Server-only. Must be called after Instantiate and BEFORE NetworkObject.Spawn().
    public void ServerInitialize(int instanceId, int questObjectiveIndex, QuestEntityKind entityKind)
    {
        pendingQuestInstanceId = instanceId;
        pendingObjectiveIndex = questObjectiveIndex;
        pendingKind = entityKind;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (!IsServer) return;

        questInstanceId.Value = pendingQuestInstanceId;
        objectiveIndex.Value = pendingObjectiveIndex;
        kind.Value = (int)pendingKind;
    }

    public void OnInteract(Player player)
    {
        InteractRequestRpc();
    }

    // No id travels from the client: the server reads this object's own NetworkVariable, so a
    // client cannot claim to have interacted with a different quest.
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void InteractRequestRpc()
    {
        if (QuestManager.Instance == null) return;

        QuestManager.Instance.HandleEntityInteracted(this);
    }

    // Empty on purpose, and it matters more here than anywhere else: the server can despawn this
    // object while another player is still in hand mode with it (someone else turned the quest in,
    // the island unloaded, the quest expired). FirstPersonController keeps an *interface* reference,
    // and an interface reference does NOT compare as null once the Unity object is destroyed - so
    // these callbacks get invoked on a dead object. They must never touch a Unity or NGO member.
    public void OnUnInteract(Player player) { }
    public void OnHandInput(float xValue, float yValue) { }
    public void OnRightHandInput(float xValue, float yValue) { }
    public void OnButtonInput() { }
}
