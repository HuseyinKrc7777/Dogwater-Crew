using System;
using System.Collections.Generic;
using UnityEngine;

// A place where the server may put a quest NPC or a quest item. Just a marker: no network code.
//
// Never place one on the ship: spawned quest entities are world-positioned NetworkObjects and do
// not follow a moving parent, so the NPC would be left standing on open water.
public class QuestSpawnPoint : MonoBehaviour
{
    public enum SpawnKind
    {
        Npc = 0,
        Item = 1
    }

    [SerializeField] private SpawnKind kind = SpawnKind.Npc;

    [Tooltip("Index into QuestDatabase.locationNames. -1 = this place has no display name. " +
             "Ignored on island points: an island is named by its IslandDefinition asset, because the quest " +
             "text is written while the island is still unloaded and its points do not exist yet.")]
    [SerializeField] private int locationNameIndex = -1;

    [Tooltip("Empty = a persistent place that is always loaded (on the ship's home scene). Otherwise: the " +
             "island this point sits on - drag in the same IslandDefinition asset that island's entry " +
             "trigger uses.")]
    [SerializeField] private IslandDefinition island;

    public SpawnKind Kind => kind;
    public int LocationNameIndex => locationNameIndex;

    // The identity an island is known by everywhere: its scene name, taken from the asset rather than
    // retyped here, so it cannot drift out of sync with the trigger or the quest catalog.
    public string IslandName => island != null ? island.SceneName : string.Empty;

    // Self-maintaining registry: points register themselves while they are enabled. Island scenes are
    // loaded and unloaded at runtime, so the registry has to follow their lifecycle rather than being
    // collected once at startup.
    public static readonly List<QuestSpawnPoint> All = new List<QuestSpawnPoint>();

    // The server drives deferred island spawning off these: an island loading IS its spawn points
    // registering, and an island unloading IS them deregistering. The events know nothing about
    // islands as such, so any other way of bringing points into the world works the same way.
    //
    // They fire on every peer (island scenes are loaded by everyone), so only the server may act on
    // them - see QuestManager.OnNetworkSpawn.
    public static event Action<QuestSpawnPoint> Registered;
    public static event Action<QuestSpawnPoint> Deregistered;

    // The registry is updated before the event fires, so a listener always sees a consistent world.
    private void OnEnable()
    {
        if (All.Contains(this)) return;

        All.Add(this);
        Registered?.Invoke(this);
    }

    private void OnDisable()
    {
        if (!All.Remove(this)) return;

        Deregistered?.Invoke(this);
    }
}
