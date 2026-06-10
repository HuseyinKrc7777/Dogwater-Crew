using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Networking;
using UnityEngine.Rendering;
using UnityEngine.UI;
[RequireComponent(typeof(Renderer))]
public class Map : NetworkBehaviour, IHandInput
{
    //TODO
    // haritada etkileşime geçerken imleç kalem /silgiye dönüşüsün ,
    // bunun kaliteli bir şekilde olması için ayrı olarak 
    // oyuncu imleç kontrolcüsü yapılmalıdır.
    Texture2D map;
    Renderer rend;
    const int mapSize = 2000;
    bool eraseMode = false;
    bool drawing = false;
    Vector2 lastDrawnPixel = new(-1,-1);
    List<Vector2Int> playerPixels = new();
    float buttonPressTimeCounter = 0.0f;
    float buttonPressTimeout = 1.0f;
    int buttonPressCounter = 0;
    int targetButtonPress = 10;
    bool countingButton = false;
    public void OnButtonInput()
    {
        if(!countingButton)
        {
            countingButton = true;
            buttonPressCounter=0;
            buttonPressTimeCounter=0;
        }
        else
            buttonPressCounter++;
        eraseMode = !eraseMode;
        //oyuncunun yazdığı herşeyi silme
        if(buttonPressCounter >= targetButtonPress)
        {
            countingButton = false;
            EraseByArrayRpc(playerPixels.ToArray());
            playerPixels.Clear();
        }
    }

    public void OnHandInput(float xValue, float yValue)
    {
        drawing = true;
        Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            if (hit.collider.gameObject == gameObject) // make sure it's THIS plane
            {
                Vector2 uv = hit.textureCoord;

                Vector2 scale = rend.material.mainTextureScale;
                Vector2 offset = rend.material.mainTextureOffset;

                uv = uv * scale + offset;

                uv.x = uv.x % 1f;
                uv.y = uv.y % 1f;

                int x = (int)(uv.x * map.width);
                int y = (int)(uv.y * map.height);

                if (eraseMode)
                {
                    for (int i = -1; i < 2; i++)
                    {
                        for (int j = -1; j < 2; j++)
                        {

                            if (x > mapSize)
                                x -= mapSize;
                            if (y > mapSize)
                                y -= mapSize;
                            if (x < 0)
                                x += mapSize;
                            if (y < 0)
                                y += mapSize;
                                
                            EraseRpc(x + i, y + j);
                        }
                    }
                }
                else
                {
                    Vector2 current = new(x,y);
                    if(lastDrawnPixel.x != -1)
                    {
                        Vector2 filler = Vector2.MoveTowards(lastDrawnPixel,current,1.0f);
                        while(Vector2.Distance(current,filler) > 1)
                        {
                            int newx = (int)filler.x;
                            int newy = (int)filler.y;

                            DrawRpc(newx,newy);
                            playerPixels.Add(new(newx,newy));
                            filler = Vector2.MoveTowards(filler,current,1.0f);

                        }
                    }
                    DrawRpc(x, y);
                    playerPixels.Add(new(x,y));
                    lastDrawnPixel.x = x;
                    lastDrawnPixel.y = y;

                }
            }
        }
    }

    public void OnInteract(Player player)
    {
        eraseMode = false;
        //throw new System.NotImplementedException();
    }

    public void OnRightHandInput(float xValue, float yValue)
    {
        Vector2 scale = rend.material.mainTextureScale;

        rend.material.mainTextureOffset += new Vector2(yValue * scale.x, xValue * -1 * scale.y);
        //throw new System.NotImplementedException();
    }

    public void OnUnInteract(Player player)
    {
        //throw new System.NotImplementedException();
    }
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        map = new Texture2D(mapSize, mapSize, TextureFormat.RGBA32, false);
        for (int x = 0; x < mapSize; x++)
            for (int y = 0; y < mapSize; y++)
                map.SetPixel(x, y, Color.white);
        map.Apply();
        rend = GetComponent<Renderer>();
        rend.material.mainTexture = map;
        rend.material.mainTextureScale = new Vector2(0.35f, 0.35f);

    }
    [Rpc(SendTo.Everyone)]
    void DrawRpc(int x, int y)
    {

        map.SetPixel(x, y, Color.black);
        map.Apply();

    }
    [Rpc(SendTo.Server)]
    void EraseRpc(int x, int y)
    {

        map.SetPixel(x, y, Color.white);
        map.Apply();
    }

    [Rpc(SendTo.Server)]
    void EraseByArrayRpc(Vector2Int[] array)
    {
        foreach(Vector2Int pixel in array)
        {
            map.SetPixel(pixel.x,pixel.y,Color.white);
        }
        map.Apply();
    }
    // Update is called once per frame
    void Update()
    {
        if(countingButton)
        {
            buttonPressTimeCounter += Time.deltaTime;
            if(buttonPressTimeCounter >= buttonPressTimeout)
                countingButton = false;
        }
    }

    void LateUpdate()
    {
        if(drawing)
            drawing = false;
        else
        {
            lastDrawnPixel.x = -1;
            lastDrawnPixel.y = -1;
        }
    }
}
