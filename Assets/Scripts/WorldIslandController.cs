using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering.Universal.Internal;
using System.Threading.Tasks;
[Serializable]
public class WorldIsland
{
    public LatitudeCoordinate latitude;
    public LongitudeCoordinate longitude;
    public GameObject prefab;
    public GameObject instance;
    public int loadDistance = 900;
}
[Serializable]
public class WorldIslandController : NetworkBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public List<WorldIsland> worldIslands = new();
    List<WorldIsland> loadedIslands = new();
    List<WorldIsland> loadQueue = new();
    List<WorldIsland> unLoadQueue = new();

    float counter = 0.0f;
    float timeout = 1.0f;

    public static WorldIslandController Instance { get; private set; }

    public GlobalCoordinate shipCoordinate;
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
        CheckIslandsToSpawnOrDespawnThem();
    }

    void FixedUpdate()
    {
        if (!IsServer)
            return;
        counter += Time.deltaTime;
        if (counter >= timeout)
        {
            CheckIslandsToSpawnOrDespawnThem();

            counter = 0;
        }
    }
    public void CheckIslandsToSpawnOrDespawnThem()
    {
        if (shipCoordinate == null)
            return;
        List<WorldIsland> temp = loadedIslands.ToList<WorldIsland>();
        // gelecekte buraya her adanın her zaman konum tespiti değil de 
        // en yakın adaların cart curtu tutulup onun güncellenip ona göre bi performans
        // şeysi yapılabilir.
        foreach (WorldIsland island in temp)
        {
            if (CheckIslandToUnLoad(island))
                unLoadQueue.Add(island);
        }
        foreach (WorldIsland island in worldIslands)
        {
            if (CheckIslandToLoad(island))
            {
                if (!loadedIslands.Contains(island))
                    loadQueue.Add(island);
            }
        }
        _ = LoadIslandList(loadQueue);
        UnloadIslandList(unLoadQueue);
        loadQueue.Clear();
        unLoadQueue.Clear();


    }

    bool CheckIslandToLoad(WorldIsland island)
    {
        if (shipCoordinate == null)
            return false;
        float dist = shipCoordinate.CalculateDistanceBetweenTwoPoints(shipCoordinate.latitude.Value, shipCoordinate.longitude.Value, island.latitude, island.longitude);
        Debug.Log(dist);
        if (dist <= island.loadDistance)
            return true;
        else
            return false;
    }

    bool CheckIslandToUnLoad(WorldIsland island)
    {
        if (shipCoordinate == null)
            return false;
        float dist = shipCoordinate.CalculateDistanceBetweenTwoPoints(shipCoordinate.latitude.Value, shipCoordinate.longitude.Value, island.latitude, island.longitude);
        Debug.Log(dist);
        if (dist >= island.loadDistance || Vector3.Distance(island.instance.transform.position, shipCoordinate.transform.position) >= island.loadDistance)
            return true;
        else
            return false;
    }

    private async Task LoadIslandList(List<WorldIsland> islands)
    {
        var tasks = new List<Task>();

        foreach (WorldIsland island in islands)
        {
            tasks.Add(LoadIsland(island));
        }

        await Task.WhenAll(tasks);
    }
    private async Task LoadIsland(WorldIsland island)
    {
        float dist = shipCoordinate.CalculateDistanceBetweenTwoPoints(
            shipCoordinate.latitude.Value,
            shipCoordinate.longitude.Value,
            island.latitude,
            island.longitude);

        Vector3 dir = shipCoordinate.CalculateDirectionBetweenTwoPoints(
            shipCoordinate.latitude.Value,
            shipCoordinate.longitude.Value,
            island.latitude,
            island.longitude);

        Vector3 pos = dir * dist + shipCoordinate.transform.position;
        Quaternion rot = Quaternion.identity;
        SpawnIslandRpc(worldIslands.IndexOf(island), pos, rot);
    }
    [Rpc(SendTo.Everyone)]
    void SpawnIslandRpc(int index, Vector3 position, Quaternion rotation)
    {
        SpawnStuff(index, position, rotation);
    }
    async void SpawnStuff(int index, Vector3 position, Quaternion rotation)
    {
        WorldIsland island = worldIslands[index];
        if (island.instance != null)
            return;

        var op = InstantiateAsync<GameObject>(
            island.prefab,
            null,
            position,
            rotation);

        loadedIslands.Add(island);
        island.instance = (await op)[0];
    }
    [Rpc(SendTo.Everyone)]
    void DestroyIslandRpc(int index)
    {
        Destroy(worldIslands[index].instance);
        worldIslands[index].instance = null;
        loadedIslands.Remove(worldIslands.Find(x => x == worldIslands[index]));

    }

    void UnloadIslandList(List<WorldIsland> islands)
    {
        foreach (WorldIsland island in islands)
        {
            int index = worldIslands.IndexOf(island);
            DestroyIslandRpc(index);
            /*
            Destroy(worldIslands.Find(x => x == island).instance);
            worldIslands.Find(x => x == island).instance = null;
            loadedIslands.Remove(worldIslands.Find(x => x == island));
            */
        }

    }
}
