using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Networking;
[RequireComponent(typeof(Renderer))]
public class Map : NetworkBehaviour, IHandInput
{
    //TODO
    //her oyuncu çizdiği piksellerin kordinat dizisini tutacak ve bunlar
    //haritanın kayıt teknolojisi için kullanılabilir.
    //eğer sonradan katılan bir oyuncunun var olması durumunda da senkronizasyon için
    //kullanılabilir.
    Texture2D map;
    public void OnButtonInput()
    {
        //throw new System.NotImplementedException();
    }

    public void OnHandInput(float xValue, float yValue)
    {
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

                DrawRpc(x,y);
            }
        }
    }

    public void OnInteract(Player player)
    {
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
    Renderer rend;
    int mapSize = 2000;
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
        rend.material.mainTextureScale = new Vector2(0.2f, 0.2f);
    }
    [Rpc(SendTo.Everyone)]
    void DrawRpc(int x , int y)
    {
        map.SetPixel(x, y, Color.black);
        map.Apply();

    }
    // Update is called once per frame
    void Update()
    {

    }
}
