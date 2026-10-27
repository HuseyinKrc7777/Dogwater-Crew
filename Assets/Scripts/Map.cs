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
    public Vector2 lastDrawnPixel = new(-1, -1);
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
        if (!countingButton)
        {
            countingButton = true;
            buttonPressCounter = 0;
            buttonPressTimeCounter = 0;
        }
        else
            buttonPressCounter++;
        eraseMode = !eraseMode;
        //oyuncunun yazdığı herşeyi silme
        if (buttonPressCounter >= targetButtonPress)
        {
            countingButton = false;
            controller.EraseByArrayRpc(index, playerPixels.ToArray());
            playerPixels.Clear();
        }
    }
    private bool isHolding;
    private bool justStartedHolding;
    private bool justLetGo;

    private bool receivedInputThisFrame;
    private bool receivedInputLastFrame;
    private Vector2 mappingStartPixel;
    private Vector2 mappingStartMappedPixel;
    //çizme işlemi gpt yardımı ile düzenlenmiştir.
    public void OnHandInput(float xValue, float yValue)
    {
        receivedInputThisFrame = true;

        if (justStartedHolding)
        {
            mappingValid = false;
            lastDrawnPixel = new Vector2(-1f, -1f);

            if (Mouse.current == null || Camera.main == null)
                return;

            Vector2 mousePosition = Mouse.current.position.ReadValue();

            Ray ray = Camera.main.ScreenPointToRay(mousePosition);

            if (!Physics.Raycast(ray, out RaycastHit hit))
                return;

            if (hit.collider.gameObject != gameObject)
                return;

            if (!InitializeMapping(hit, mousePosition))
                return;

            Vector2 uv = hit.textureCoord;

            Vector2 scale = rend.material.mainTextureScale;
            Vector2 offset = rend.material.mainTextureOffset;

            uv = Vector2.Scale(uv, scale) + offset;

            uv.x = Mathf.Repeat(uv.x, 1f);
            uv.y = Mathf.Repeat(uv.y, 1f);

            Vector2 startPixel = new Vector2(
                uv.x * map.width,
                uv.y * map.height
            );

            int x = Mathf.FloorToInt(startPixel.x);
            int y = Mathf.FloorToInt(startPixel.y);

            mappingStartPixel = startPixel;

            if (!TryGetPixel(mousePosition, out mappingStartMappedPixel))
            {
                mappingValid = false;
                return;
            }

            lastDrawnPixel = startPixel;

            if (eraseMode)
            {
                AddErasePixels(x, y);
            }
            else
            {
                playerPixels.Add(new(x, y));
                LastDrawList.Add(new(x, y));
            }

            return;
        }

        if (!mappingValid || lastDrawnPixel.x == -1f)
            return;

        accumulatedMousePosition -= new Vector2(xValue, yValue) * 1000f;

        if (!TryGetPixel(
        accumulatedMousePosition,
        out Vector2 mappedCurrent))
        {
            return;
        }

        // Reverse both mapped axes while preserving the initial hit pixel.
        Vector2 current =
            mappingStartPixel -
            (mappedCurrent - mappingStartMappedPixel);

        if (eraseMode)
        {
            AddErasePixels(
                Mathf.FloorToInt(current.x),
                Mathf.FloorToInt(current.y)
            );
        }
        else
        {
            // Interpolate to avoid gaps when the mouse moves quickly.
            Vector2 previous = lastDrawnPixel;

            float distance = Vector2.Distance(previous, current);
            int steps = Mathf.Max(1, Mathf.CeilToInt(distance));

            for (int i = 1; i <= steps; i++)
            {
                Vector2 point = Vector2.Lerp(
                    previous,
                    current,
                    (float)i / steps
                );

                int px = Mathf.FloorToInt(point.x);
                int py = Mathf.FloorToInt(point.y);

                playerPixels.Add(new(px, py));
                LastDrawList.Add(new(px, py));
            }
        }

        // Preserve fractional pixel movement.
        lastDrawnPixel = current;
    }
    private bool InitializeMapping(RaycastHit hit, Vector2 mousePosition)
    {
        mappingValid = false;

        drawingCamera = Camera.main;
        drawingPlane = transform;

        if (drawingCamera == null || rend == null || map == null)
            return false;

        MeshFilter meshFilter = drawingPlane.GetComponent<MeshFilter>();

        if (meshFilter == null || meshFilter.sharedMesh == null)
            return false;

        Bounds bounds = meshFilter.sharedMesh.bounds;

        if (bounds.size.x == 0f || bounds.size.z == 0f)
            return false;

        // The plane's local XZ surface is projected into screen space.
        // The camera transform already includes the player's position
        // and orientation when the camera follows the player.
        Matrix4x4 localToClip =
            drawingCamera.projectionMatrix *
            drawingCamera.worldToCameraMatrix *
            drawingPlane.localToWorldMatrix;

        Rect rect = drawingCamera.pixelRect;

        if (rect.width <= 0f || rect.height <= 0f)
            return false;

        float halfWidth = rect.width * 0.5f;
        float halfHeight = rect.height * 0.5f;
        float centerX = rect.x + halfWidth;
        float centerY = rect.y + halfHeight;

        // Homography mapping:
        // plane local (X, Z, 1) -> screen (X, Y, homogeneous W).
        Matrix4x4 planeToScreen = Matrix4x4.identity;

        planeToScreen.SetRow(0, new Vector4(
            halfWidth * localToClip[0, 0] +
                centerX * localToClip[3, 0],

            halfWidth * localToClip[0, 2] +
                centerX * localToClip[3, 2],

            halfWidth * localToClip[0, 3] +
                centerX * localToClip[3, 3],

            0f
        ));

        planeToScreen.SetRow(1, new Vector4(
            halfHeight * localToClip[1, 0] +
                centerY * localToClip[3, 0],

            halfHeight * localToClip[1, 2] +
                centerY * localToClip[3, 2],

            halfHeight * localToClip[1, 3] +
                centerY * localToClip[3, 3],

            0f
        ));

        planeToScreen.SetRow(2, new Vector4(
            localToClip[3, 0],
            localToClip[3, 2],
            localToClip[3, 3],
            0f
        ));

        if (Mathf.Abs(planeToScreen.determinant) < 1e-8f)
            return false;

        // Invert exactly once when drawing starts.
        screenToPlane = planeToScreen.inverse;
        accumulatedMousePosition = mousePosition;

        mappingValid = true;
        return true;
    }

    private bool TryGetPixel(Vector2 screenPosition, out Vector2 pixel)
    {
        pixel = default;

        if (!mappingValid)
            return false;

        // Transform screen coordinates into homogeneous plane-local XZ.
        Vector3 p = screenToPlane.MultiplyPoint3x4(
            new Vector3(screenPosition.x, screenPosition.y, 1f)
        );

        if (Mathf.Abs(p.z) < 1e-7f)
            return false;

        float localX = p.x / p.z;
        float localZ = p.y / p.z;

        Mesh mesh = drawingPlane.GetComponent<MeshFilter>().sharedMesh;
        Bounds bounds = mesh.bounds;

        // Convert local XZ into the mesh's UV coordinates.
        Vector2 uv = new Vector2(
            (localX - bounds.min.x) / bounds.size.x,
            (localZ - bounds.min.z) / bounds.size.z
        );

        // Match the texture tiling and offset used by the material.
        Vector2 scale = rend.material.mainTextureScale;
        Vector2 offset = rend.material.mainTextureOffset;

        uv = Vector2.Scale(uv, scale) + offset;

        uv.x = Mathf.Repeat(uv.x, 1f);
        uv.y = Mathf.Repeat(uv.y, 1f);

        pixel = new Vector2(
            uv.x * map.width,
            uv.y * map.height
        );

        return true;
    }

    private void AddErasePixels(int x, int y)
    {
        for (int i = -1; i <= 1; i++)
        {
            for (int j = -1; j <= 1; j++)
            {
                int px = (x + i) % mapSize;
                int py = (y + j) % mapSize;

                if (px < 0) px += mapSize;
                if (py < 0) py += mapSize;

                LastEraseList.Add(new(px, py));
            }
        }
    }
    private Camera drawingCamera;
    private Transform drawingPlane;


    private Matrix4x4 screenToPlane;
    private Vector2 accumulatedMousePosition;
    private bool mappingValid;




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
        counter += Time.deltaTime;
        if (counter >= syncTimeout)
        {
            SyncMap();
            counter = 0;
        }
        if (countingButton)
        {
            buttonPressTimeCounter += Time.deltaTime;
            if (buttonPressTimeCounter >= buttonPressTimeout)
                countingButton = false;
        }

    }
    float counter = 0;
    float syncTimeout = 0.05f;
    void LateUpdate()
    {
        justStartedHolding = receivedInputThisFrame && !receivedInputLastFrame;
        justLetGo = !receivedInputThisFrame && receivedInputLastFrame;

        isHolding = receivedInputThisFrame;

        if (justLetGo)
        {
            lastDrawnPixel = new Vector2(-1, -1);
        }

        receivedInputLastFrame = receivedInputThisFrame;
        receivedInputThisFrame = false;


    }
    public void SyncMap()
    {
        if (LastDrawList.Count > 0)
        {
            controller.DrawByArrayRpc(index, LastDrawList.ToArray());
            LastDrawList.Clear();
        }
        if (LastEraseList.Count > 0)
        {
            controller.EraseByArrayRpc(index, LastEraseList.ToArray());
            LastEraseList.Clear();
        }
    }

}
