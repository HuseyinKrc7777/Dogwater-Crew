using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ChunkController : MonoBehaviour
{
    //TODO bunun aynısını düşük kalite su için de yapılması lazım , fakat acelesi yok gibi ?mi acaba.
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public List<GameObject> WaterTiles;
    public GameObject ship;
    [SerializeField] private readonly int edgeBlockCount = 4;
    [SerializeField] private readonly int blockSize = 160;
    
    [SerializeField] private string targetTag;
    [SerializeField] private float initalOffset;
    void Start()
    {
        GameObject.FindGameObjectsWithTag(targetTag, WaterTiles);
        ship = GameObject.FindGameObjectWithTag("Ship");
    }
    [SerializeField]float movementOffsetMultiplier = 1.3f;
    private void CheckForChunkMovement(GameObject _ship, List<GameObject> _WaterTiles)
    {
        Vector3 middle = findMiddle(_WaterTiles);
        if (Vector3.Distance(middle, _ship.transform.position) < blockSize / movementOffsetMultiplier )
        {
            return;
        }
        Vector3 dir = (_ship.transform.position - middle).normalized;
        if (Mathf.Abs(dir.z) > Mathf.Abs(dir.x))
        {
            //kuzey / güney hareketi
            if (dir.z > 0)
            {
                //kuzeye gidiş gerçekleşiyor
                //güney kenarını alıp kuzeye ekliyoruz
                List<GameObject> edge = sortForEdge(directions.south, _WaterTiles).Take(edgeBlockCount).ToList();
                MoveChunks(directions.north, edge);
            }
            else
            {
                //güneye gidiş gerçekleşiyor
                // diğer işlemde yaptığımızın tersini yapıyoruz
                List<GameObject> edge = sortForEdge(directions.north, _WaterTiles).Take(edgeBlockCount).ToList();
                MoveChunks(directions.south, edge);
            }

        }
        else
        {
            //doğu / batı hareketi

            if (dir.x > 0)
            {
                //doğuya gidiş gerçekleşiyor
                //batı kenarını alıp kuzeye ekliyoruz
                List<GameObject> edge = sortForEdge(directions.west, _WaterTiles).Take(edgeBlockCount).ToList();
                MoveChunks(directions.east, edge);
            }
            else
            {
                //batıya gidiş gerçekleşiyor
                // diğer işlemde yaptığımızın tersini yapıyoruz
                List<GameObject> edge = sortForEdge(directions.east, _WaterTiles).Take(edgeBlockCount).ToList();
                MoveChunks(directions.west, edge);
            }

        }
    }
    private void MoveChunks(directions dir, List<GameObject> chunks)
    {
        Vector3 offset = Vector3.zero; //yöne doğru ışınlacak
        float offsetAmount = (edgeBlockCount * blockSize) + initalOffset;

        switch (dir)
        {
            case directions.west:
                offset = Vector3.left * offsetAmount;
                break;
            case directions.east:
                offset = Vector3.right * offsetAmount;
                break;
            case directions.south:
                offset = Vector3.back * offsetAmount;
                break;
            case directions.north:
                offset = Vector3.forward * offsetAmount;
                break;
            default:
                break;
        }
        foreach (GameObject chunk in chunks)
        {
            chunk.transform.position += offset;
        }

    }
    private Vector3 findMiddle(List<GameObject> _WaterTiles)
    {
        Vector3 v3 = Vector3.zero;
        foreach (GameObject tile in _WaterTiles)
        {
            v3 += tile.transform.position;
        }
        v3 /= _WaterTiles.Count;
        return v3;
    }

    enum directions
    {
        north,
        south,
        east,
        west
    }
    private List<GameObject> sortForEdge(directions dir, List<GameObject> _WaterTiles)
    {
        //east -> x+, west -> x- , north -> z+ , south -> z-
        List<GameObject> list = new();
        switch (dir)
        {
            case directions.west:
                list = _WaterTiles.OrderBy(i => i.transform.position.x).ToList();
                break;
            case directions.east:
                list = _WaterTiles.OrderBy(i => i.transform.position.x).ToList();
                list.Reverse();

                break;
            case directions.south:
                list = _WaterTiles.OrderBy(i => i.transform.position.z).ToList();
                break;
            case directions.north:
                list = _WaterTiles.OrderBy(i => i.transform.position.z).ToList();
                list.Reverse();

                break;
            default:
                break;
        }
        return list;

    }

    // Update is called once per frame
    void Update()
    {
        CheckForChunkMovement(ship,WaterTiles);
    }
}
