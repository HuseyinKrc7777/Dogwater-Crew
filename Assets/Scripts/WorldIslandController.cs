using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering.Universal.Internal;
using System.Threading.Tasks;
using Unity.Collections;
[Serializable]
public class WorldIsland
{
    public GlobalCoordinate coordinate;
    public GameObject prefab;
    public GameObject instance;
    public int loadDistance = 900;
}
[Serializable]
public struct IslandSpawnData : INetworkSerializable ,IEquatable<IslandSpawnData>
{
    public int index;
    public Vector3 position;
    public Quaternion rotation;
    public int loadDistance;

    public bool Equals(IslandSpawnData other)
    {
        throw new NotImplementedException();
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer)
        where T : IReaderWriter
    {
        serializer.SerializeValue(ref index);
        serializer.SerializeValue(ref position);
        serializer.SerializeValue(ref rotation);
    }

}
[Serializable]
public class WorldIslandController : NetworkBehaviour
{
    //TODO burasının tamamı baştan yazılacak
    public List<WorldIsland> worldIslands = new();
    //rpc lerden liste dinyecisine geçilmesi denendi fakat başarısız olundu , daha sonra tekrar denenebilir.    
    NetworkList<IslandSpawnData> loadedIslands = new();

    float counter = 0.0f;
    float timeout = 1.0f;

    public static WorldIslandController Instance { get; private set; } 
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
        foreach(IslandSpawnData data in loadedIslands)
        {
            _ = SpawnIsland(data);
        }
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
        GlobalCoordinate shipCoordinate = Ship.PlayerShip.coordinate;
        // TODO gelecekte buraya her adanın her zaman konum tespiti değil de 
        // en yakın adaların cart curtu tutulup onun güncellenip ona göre bi performans
        // içinde index , pozisyon ve rotasyon bulunan bir struct
        // NetworkList<> içinde bu struct olacak şekilde
        // yapılabilir.
        foreach (WorldIsland island in worldIslands)
        {
            if (GlobalCoordinate.CalculateDistanceBetweenTwoPoints(shipCoordinate,island.coordinate) < island.loadDistance)
            {
                if(island.instance!=null)
                    continue;
                _ = LoadIsland(island);
            }   
            else if(island.instance!=null)
            {
                UnloadIsland(island);
            }
        }
    }

    private async Task LoadIsland(WorldIsland island)
    {
        //TODO editörde test ederken bazen adalar farklı alanlarda spawn oluyor , çözmek lazım olabilir ilerde tekrar kendini gösterirse
        GlobalCoordinate shipCoordinate = Ship.PlayerShip.coordinate;
        Vector3 shipPos = Ship.PlayerShip.transform.position;

        float dist = GlobalCoordinate.CalculateDistanceBetweenTwoPoints(shipCoordinate,island.coordinate);

        Vector3 dir = GlobalCoordinate.CalculateDirectionBetweenTwoPoints(shipCoordinate,island.coordinate);

        Vector3 pos = dir * dist + shipPos;
        Quaternion rot = Quaternion.identity;
        
        IslandSpawnData data;
        data.index = worldIslands.IndexOf(island);
        data.position = pos;
        data.rotation = rot;
        data.loadDistance = island.loadDistance;
        SpawnIslandRpc(data);
    }
    [Rpc(SendTo.Everyone)]
    void SpawnIslandRpc(IslandSpawnData data)
    {
        _ = SpawnIsland(data);
    }
    async Task SpawnIsland(IslandSpawnData data)
    {
        WorldIsland island = worldIslands[data.index];
        if (island.instance != null)
            return;

        var op = InstantiateAsync<GameObject>(
            island.prefab,
            null,
            data.position,
            data.rotation);

        loadedIslands.Add(data);
        island.instance = (await op)[0];
    }
    [Rpc(SendTo.Everyone)]
    void DestroyIslandRpc(int index)
    {
        Destroy(worldIslands[index].instance);
     
    }

    void UnloadIsland(WorldIsland island)
    {
        int index = worldIslands.IndexOf(island);
        DestroyIslandRpc(index);
        foreach(IslandSpawnData data in loadedIslands)
        {
            if(index == data.index)
            {
                loadedIslands.Remove(data);
                break;
            }
        }
    }
}
