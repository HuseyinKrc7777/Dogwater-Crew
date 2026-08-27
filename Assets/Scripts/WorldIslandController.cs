using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering.Universal.Internal;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.VisualScripting;
[Serializable]
public class WorldIsland
{
    public GlobalCoordinate coordinate;
    public GameObject prefab;
    public GameObject instance;
    public int loadDistance = 9000;
}
[Serializable]
public struct IslandSpawnData : INetworkSerializable, IEquatable<IslandSpawnData>
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
        foreach (IslandSpawnData data in loadedIslands)
        {
            _ = SpawnIsland(data);
        }
        if (IsServer)
        {
            GenerateIslands(3 * GlobalCoordinate.EarthRadius / 100);
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
            if (GlobalCoordinate.CalculateDistanceBetweenTwoPoints(shipCoordinate, island.coordinate) < island.loadDistance)
            {
                if (island.instance != null)
                    continue;
                _ = LoadIsland(island);
            }
            else if (island.instance != null)
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

        float dist = GlobalCoordinate.CalculateDistanceBetweenTwoPoints(shipCoordinate, island.coordinate);

        Vector3 dir = GlobalCoordinate.CalculateDirectionBetweenTwoPoints(shipCoordinate, island.coordinate);

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
        foreach (IslandSpawnData data in loadedIslands)
        {
            if (index == data.index)
            {
                loadedIslands.Remove(data);
                break;
            }
        }
    }
    void GenerateIslands(int count)
    {
        List<GlobalCoordinate> coordinates = new();
        for(int i = 0;i<5*count/6;i++)
        {
            WorldIsland newisland = new()
            {
                prefab = worldIslands[0].prefab,
                coordinate = new GlobalCoordinate(new LatitudeCoordinate(),new LongitudeCoordinate()),
                loadDistance = 9000
            };
            
            int sign1 = 0;
            if (UnityEngine.Random.Range(-1, 1) < 0)
                sign1 = -1;
            else
                sign1 = 1;

            int sign2 = 0;
            if (UnityEngine.Random.Range(-1, 1) < 0)
                sign2 = -1;
            else
                sign2 = 1;
            int max = 0;
            restart:
            GlobalCoordinate coordinate = new();
            coordinate.latitude.AddSecond((int)(UnityEngine.Random.value  * 89 * 60 * 60 * sign1));
            coordinate.longitude.AddSecond((int)(UnityEngine.Random.value  * 180 * 60 * 60 * sign2));
            if(max>10)
            {
                break;                
            }
            foreach(GlobalCoordinate co in coordinates)
            {
                if(GlobalCoordinate.CalculateDistanceBetweenTwoPoints(co,coordinate) < 1000/*ada büyüklüğü + pay*/)
                {
                    max++;
                    goto restart;
                }
            }
            coordinates.Add(coordinate);
            newisland.coordinate = coordinate;
            worldIslands.Add(newisland);
        }

        for(int i = 0;i<count/6;i++)
        {
            
            WorldIsland newisland = new()
            {
                prefab = worldIslands[0].prefab,
                coordinate = new GlobalCoordinate(new LatitudeCoordinate(),new LongitudeCoordinate()),
                loadDistance = 9000
            };
            
            int sign1 = 0;
            if (UnityEngine.Random.Range(-1, 1) < 0)
                sign1 = -1;
            else
                sign1 = 1;

            int sign2 = 0;
            if (UnityEngine.Random.Range(-1, 1) < 0)
                sign2 = -1;
            else
                sign2 = 1;
            int max = 0;
            restart:

            GlobalCoordinate randcoordinate = coordinates[UnityEngine.Random.Range(0,coordinates.Count)];
            GlobalCoordinate coordinate = new(randcoordinate.latitude,randcoordinate.longitude);
            coordinate.latitude.AddSecond((int)(UnityEngine.Random.value  * 7 * 60 * 60 * sign1));
            coordinate.longitude.AddSecond((int)(UnityEngine.Random.value  * 7 * 60 * 60 * sign2));
            if(max>10)
            {
                break;                
            }
            foreach(GlobalCoordinate co in coordinates)
            {
                if(GlobalCoordinate.CalculateDistanceBetweenTwoPoints(co,coordinate) < 200/*ada büyüklüğü + pay*/)
                {
                    max++;
                    goto restart;
                }
            }
            coordinates.Add(coordinate);
            newisland.coordinate = coordinate;
            worldIslands.Add(newisland);
        }
        
    }
}
