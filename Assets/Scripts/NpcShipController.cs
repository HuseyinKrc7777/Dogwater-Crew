using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;

[Serializable]
public class NpcShip
{
    public LatitudeCoordinate latitude;
    public LongitudeCoordinate longitude;
    public List<position> targetPositions;
    public ShipKind shipKind;
    public NetworkObject prefab;
    public NetworkObject instance;
    public int loadDistance = 900;
    //TODO Shipai da kullanılacak olan konfigürsayon şeysi de burada yer alacaktır.
}
public class NpcShipController : NetworkBehaviour
{

    [SerializeField] private List<NpcShip> shipList = new();
    float counter = 0.0f;
    float timeout = 10.0f;
    public static NpcShipController Instance { get; private set; }

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
    public void CheckShipsToSpawnOrDespawnThem()
    {
        if (shipCoordinate == null)
            return;
        Debug.Log("checking");
        foreach (NpcShip ship in shipList)
        {
            if (shipCoordinate.CalculateDistanceBetweenTwoPoints(shipCoordinate.latitude.Value, shipCoordinate.longitude.Value, ship.latitude, ship.longitude) < ship.loadDistance)
            {
                _ = LoadShip(ship);
            }
            else
                UnloadShip(ship);
        }
    }
    private async Task LoadShip(NpcShip ship)
    {
        if (ship.instance != null)
            return;
        Debug.Log("loading");
        
        float dist = shipCoordinate.CalculateDistanceBetweenTwoPoints(
            shipCoordinate.latitude.Value,
            shipCoordinate.longitude.Value,
            ship.latitude,
            ship.longitude);

        Vector3 dir = shipCoordinate.CalculateDirectionBetweenTwoPoints(
            shipCoordinate.latitude.Value,
            shipCoordinate.longitude.Value,
            ship.latitude,
            ship.longitude);

        Vector3 position = dir * dist + shipCoordinate.transform.position;
        Quaternion rotation = Quaternion.identity;

        SpawnShip(ship,position,rotation);
    }

    async void SpawnShip(NpcShip ship, Vector3 position, Quaternion rotation)
    {
        Debug.Log("spawning");
        
        var op = InstantiateAsync<NetworkObject>(
        ship.prefab,
        null,
        position,
        rotation);
        ship.instance = (await op)[0];
        NetworkObject rootNetworkObject = ship.instance.GetComponent<NetworkObject>();
        rootNetworkObject.Spawn(true);
        foreach(NetworkObject childNetworkObject in ship.instance.GetComponentsInChildren<NetworkObject>())
        {
            if(childNetworkObject != rootNetworkObject)
            {
                childNetworkObject.Spawn(true);
                childNetworkObject.transform.SetParent(rootNetworkObject.transform);
            }
        }
     
        
        configureShip(ref ship);
        //TODO başka ayarlar yapılacaksa burada clientlara rpc gönderilmesi lazım olabilir
    }
    public void configureShip(ref NpcShip ship)
    {
        Debug.Log("configuring");
        //TODO konfigurasyon structına geçildiğinde buarada birsürü şey yerine bir adet strcut verilecektir.
        GlobalCoordinate shipCoordinate = ship.instance.GetComponent<GlobalCoordinate>();
        ShipAi shipai = ship.instance.GetComponent<ShipAi>();

        shipCoordinate.latitude.Value = ship.latitude;
        shipCoordinate.longitude.Value = ship.longitude;

        shipai.whatKindOfShipIsThis = ship.shipKind;

        shipai.TravelPositions = ship.targetPositions;

    }


    void UnloadShip(NpcShip ship)
    {
        if(ship.instance == null)
            return;
        NetworkObject shipNetworkObject = ship.instance.GetComponent<NetworkObject>();
        //TODO kaliteli bir gemi modeline geçildiğinde burası düzenlenmesi gerek
        /*
        GameObject Boat = ship.instance.transform.Find("Boat").gameObject;
        foreach (var childNetworkObject in Boat.GetComponentsInChildren<NetworkObject>())
        {
            if(childNetworkObject != shipNetworkObject)
                childNetworkObject.Despawn();
        }
        */
        foreach (var childNetworkObject in ship.instance.GetComponentsInChildren<NetworkObject>())
        {
            if(childNetworkObject != shipNetworkObject)
                childNetworkObject.Despawn();
        }

        shipNetworkObject.Despawn();
        ship.instance = null;
    }

    void Start()
    {

    }

    void FixedUpdate()
    {
        if(!IsServer)
            return;
        
        if(counter<timeout)
        {
            counter+=Time.deltaTime;
        }
        else
        {
            counter=0;
            CheckShipsToSpawnOrDespawnThem();
        }

        //TODO spawnolmamış gemilerin yapay zekalarına göre potansiyel konumları burada güncellenebilir. 
        /*
        foreach(NpcShip ship in shipList)
        {
            if(ship.instance == null)
            {
                eser miktarda hedefine konumunu hedefine ilerlet ,
                eğer hedefindeyse sonraki hedefe geç
                mevcut hedefini tut ve spawnlandığında o hedefi ata
            }
        }
        */
    }
}
