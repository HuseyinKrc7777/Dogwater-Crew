using System.Collections.Generic;
using UnityEngine;

// Where a player lands when entering an island. One per island scene: place it on solid ground, facing
// the way the player should look on arrival.
//
// A self-maintaining registry rather than an inspector reference, for the same reason QuestSpawnPoint
// uses one: IslandManager lives in ShipTest and an island scene is loaded at runtime, so no serialized
// reference can reach across from one to the other.
public class IslandArrivalPoint : MonoBehaviour
{
    [Tooltip("The island this point belongs to. Drag in the same IslandDefinition asset the island's " +
             "entry trigger and quest spawn points use.")]
    [SerializeField] private IslandDefinition island;

    public string IslandName => island != null ? island.SceneName : string.Empty;

    public static readonly List<IslandArrivalPoint> All = new List<IslandArrivalPoint>();

    private void OnEnable()
    {
        if (All.Contains(this)) return;

        All.Add(this);
    }

    private void OnDisable()
    {
        All.Remove(this);
    }

    public static IslandArrivalPoint Find(string islandName)
    {
        if (string.IsNullOrEmpty(islandName)) return null;

        for (int i = 0; i < All.Count; i++)
        {
            IslandArrivalPoint point = All[i];
            if (point == null) continue;

            if (point.IslandName == islandName) return point;
        }

        return null;
    }
}
