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
public class Map : MonoBehaviour, IHandInput
{
    //TODO
    // haritada etkileşime geçerken imleç kalem /silgiye dönüşüsün ,
    // bunun kaliteli bir şekilde olması için ayrı olarak 
    // oyuncu imleç kontrolcüsü yapılmalıdır.
    public int index = 0;
    public MapNetworkController controller;
    public Texture2D map;
    public Renderer rend;
    public const int mapSize = 2000;
    public bool eraseMode = false;
    public bool drawing = false;
    public Vector2 lastDrawnPixel = new(-1,-1);
    public List<Vector2Int> playerPixels = new();
    public List<Vector2Int> LastDrawList = new();
    public List<Vector2Int> LastEraseList = new();


    public float buttonPressTimeCounter = 0.0f;
    public float buttonPressTimeout = 1.0f;
    public int buttonPressCounter = 0;
    public int targetButtonPress = 10;
    public bool countingButton = false;
    bool interacting = false;
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
            controller.EraseByArrayRpc(index,playerPixels.ToArray());
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
                                
                            //controller.EraseRpc(index,x + i, y + j);
                            LastEraseList.Add(new(x + i, y + j));
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

                            //controller.DrawRpc(index,newx,newy);
                            playerPixels.Add(new(newx,newy));
                            LastDrawList.Add(new(newx,newy));
                            filler = Vector2.MoveTowards(filler,current,1.0f);

                        }
                    }
                    //controller.DrawRpc(index,x, y);
                    playerPixels.Add(new(x,y));
                    LastDrawList.Add(new(x,y));

                    lastDrawnPixel.x = x;
                    lastDrawnPixel.y = y;

                }
            }
        }
    }

    public void OnInteract(Player player)
    {
        eraseMode = false;
        interacting = true;
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
        interacting = false;
        //throw new System.NotImplementedException();
    }

    // Update is called once per frame
    void Update()
    {
        counter+=Time.deltaTime;
        if(counter>=syncTimeout)
        {
            SyncMap();
            counter=0;
        }
        if(countingButton)
        {
            buttonPressTimeCounter += Time.deltaTime;
            if(buttonPressTimeCounter >= buttonPressTimeout)
                countingButton = false;
        }
       
    }
    float counter = 0;
    float syncTimeout = 0.05f;
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
    public void SyncMap()
    {
        if(LastDrawList.Count>0)
        {
            controller.DrawByArrayRpc(index,LastDrawList.ToArray());
            LastDrawList.Clear();
        }
        if(LastEraseList.Count>0)
        {
            controller.EraseByArrayRpc(index,LastEraseList.ToArray());
            LastEraseList.Clear();
        }
    }

}
