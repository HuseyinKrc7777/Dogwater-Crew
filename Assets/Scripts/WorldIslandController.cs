using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering.Universal.Internal;

[Serializable]
public class WorldIsland
{
    public Coordinate latitude;
    public Coordinate longitude;
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
    float counter = 0.0f;
    float timeout = 1.0f;
    GlobalCoordinate shipCoordinate;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        if (shipCoordinate == null)
            shipCoordinate = GameObject.FindWithTag("Ship").GetComponent<GlobalCoordinate>();
        doStuff();
    }

    void FixedUpdate()
    {
        counter += Time.deltaTime;
        if (counter >= timeout)
        {
            doStuff();

            counter = 0;
        }
    }
    void doStuff()
    {
        if (shipCoordinate == null)
            shipCoordinate = GameObject.FindWithTag("Ship").GetComponent<GlobalCoordinate>();

        List<WorldIsland> temp = loadedIslands.ToList<WorldIsland>();
        foreach (WorldIsland island in temp)
        {
            if (CheckIslandToUnLoad(island))
                UnloadIsland(island);
        }
        foreach (WorldIsland island in worldIslands)
        {
            if (CheckIslandToLoad(island))
            {
                if(!loadedIslands.Contains(island))
                    LoadIsland(island);
                
            }
        }
        
    }

    bool CheckIslandToLoad(WorldIsland island)
    {
        if (shipCoordinate == null)
            shipCoordinate = FindAnyObjectByType<GlobalCoordinate>();
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
            shipCoordinate = FindAnyObjectByType<GlobalCoordinate>();
        float dist = shipCoordinate.CalculateDistanceBetweenTwoPoints(shipCoordinate.latitude.Value, shipCoordinate.longitude.Value, island.latitude, island.longitude);
        Debug.Log(dist);
        if (dist >= island.loadDistance || Vector3.Distance(island.instance.transform.position,shipCoordinate.transform.position) >= island.loadDistance)
            return true;
        else
            return false;
    }

    void LoadIsland(WorldIsland island)
    {
        float dist = shipCoordinate.CalculateDistanceBetweenTwoPoints(shipCoordinate.latitude.Value, shipCoordinate.longitude.Value, island.latitude, island.longitude);
        Vector3 dir = shipCoordinate.CalculateDirectionBetweenTwoPoints(shipCoordinate.latitude.Value, shipCoordinate.longitude.Value, island.latitude, island.longitude);
        //TODO buna geçilecek => InstantiateAsync<GameObject>(island.prefab,null, dir * dist + shipCoordinate.transform.position, Quaternion.identity);
        GameObject isl = Instantiate(island.prefab, dir * dist + shipCoordinate.transform.position, Quaternion.identity, null);
        loadedIslands.Add(island);
        island.instance = isl;
    }

    void UnloadIsland(WorldIsland island)
    {
        Destroy(worldIslands.Find(x => x==island).instance);
        worldIslands.Find(x => x==island).instance = null;
        loadedIslands.Remove(worldIslands.Find(x => x==island));
    }
}
