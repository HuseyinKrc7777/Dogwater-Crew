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

    [Tooltip("Index into QuestDatabase.locationNames. -1 = this place has no display name.")]
    [SerializeField] private int locationNameIndex = -1;

    public SpawnKind Kind => kind;
    public int LocationNameIndex => locationNameIndex;

    // Self-maintaining registry: points register themselves while they are enabled. Island prefabs
    // are created and destroyed at runtime, so the registry has to follow their lifecycle rather
    // than being collected once at startup.
    public static readonly List<QuestSpawnPoint> All = new List<QuestSpawnPoint>();

    private void OnEnable()
    {
        if (!All.Contains(this)) All.Add(this);
    }

    private void OnDisable()
    {
        All.Remove(this);
    }
}
