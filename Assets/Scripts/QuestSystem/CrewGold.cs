using System;
using Unity.Netcode;
using UnityEngine;

// Shared crew treasury. Deliberately minimal: one server-written NetworkVariable plus a safe
// spend API, so a real economy system can later replace this single file without touching callers.
public class CrewGold : NetworkBehaviour
{
    public static CrewGold Instance { get; private set; }

    [Min(0)][SerializeField] private int startingGold = 0;

    private NetworkVariable<int> gold = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public int Current => gold.Value;

    public event Action<int> OnGoldChanged;

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

        gold.OnValueChanged += HandleGoldChanged;

        if (IsServer) gold.Value = startingGold;
    }

    public override void OnNetworkDespawn()
    {
        gold.OnValueChanged -= HandleGoldChanged;

        base.OnNetworkDespawn();
    }

    public override void OnDestroy()
    {
        if (Instance == this) Instance = null;

        base.OnDestroy();
    }

    // Server-only. Negative or zero amounts are ignored rather than silently draining the treasury.
    public void AddGold(int amount)
    {
        if (!IsServer) return;
        if (amount <= 0) return;

        gold.Value += amount;
    }

    // Server-only. Returns false and changes nothing when the crew cannot afford it, so gold can
    // never go negative. Nothing spends gold yet; this exists for future shops and repairs.
    public bool TrySpend(int amount)
    {
        if (!IsServer) return false;
        if (amount <= 0) return false;
        if (amount > gold.Value) return false;

        gold.Value -= amount;
        return true;
    }

    private void HandleGoldChanged(int previous, int current)
    {
        OnGoldChanged?.Invoke(current);
    }
}
