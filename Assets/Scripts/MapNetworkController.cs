using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class MapNetworkController : NetworkBehaviour
{
    public List<Map> maps = new();
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        foreach(Map map in maps)
        {
            map.index  = maps.IndexOf(map);
            map.controller = this;

            map.map = new Texture2D(Map.mapSize, Map.mapSize, TextureFormat.RGBA32, false);
            for (int x = 0; x < Map.mapSize; x++)
                for (int y = 0; y < Map.mapSize; y++)
                    map.map.SetPixel(x, y, Color.white);
            map.map.Apply();
            map.rend = map.GetComponent<Renderer>();
            map.rend.material.mainTexture = map.map;
            map.rend.material.mainTextureScale = new Vector2(0.35f, 0.35f);
            OnJoinRpc(map.index);
        }


    }
    [Rpc(SendTo.Everyone)]
    public void DrawRpc(int index,int x, int y)
    {
        Texture2D map = maps[index].map;
        map.SetPixel(x, y, Color.black);
        map.Apply();

    }
    [Rpc(SendTo.Everyone)]
    public void EraseRpc(int index,int x, int y)
    {
        Texture2D map = maps[index].map;

        map.SetPixel(x, y, Color.white);
        map.Apply();
    }

    [Rpc(SendTo.Everyone)]
    public void EraseByArrayRpc(int index,Vector2Int[] array)
    {
        Texture2D map = maps[index].map;
        foreach(Vector2Int pixel in array)
        {
            map.SetPixel(pixel.x,pixel.y,Color.white);
        }
        map.Apply();
    }
    [Rpc(SendTo.Everyone)]
    public void OnJoinRpc(int index)
    {
        DrawByArrayRpc(index,maps[index].playerPixels.ToArray());
    }
    [Rpc(SendTo.Everyone)]
    public void DrawByArrayRpc(int index,Vector2Int[] array)
    {
        Texture2D map = maps[index].map;
        foreach(Vector2Int pixel in array)
        {
            map.SetPixel(pixel.x,pixel.y,Color.black);
        }
        map.Apply();
    }

}
