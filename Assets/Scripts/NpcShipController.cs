using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.VisualScripting.FullSerializer;
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
    public int TravelPositionCounter = 0;
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
        Debug.Log("spawning in "+position +" coordinates : " +ship.latitude + " , "+ship.longitude);
        
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
        //TODO client tarafında etkili başka ayarlar yapılacaksa burada clientlara rpc gönderilmesi lazım olabilir
    }
    public void configureShip(ref NpcShip ship)
    {
        Debug.Log("configuring");
        //TODO konfigurasyon structına geçildiğinde buarada birsürü şey yerine bir adet strcut verilecektir.
        //TODO önceden silinen geminin , konum ve hedef bilgileri burada tekrar verilmeli
        GlobalCoordinate shipCoordinate = ship.instance.GetComponent<GlobalCoordinate>();
        ShipAi shipai = ship.instance.GetComponent<ShipAi>();

        shipCoordinate.latitude.Value = ship.latitude;
        shipCoordinate.longitude.Value = ship.longitude;

        shipai.whatKindOfShipIsThis = ship.shipKind;

        shipai.TravelPositions = ship.targetPositions;

        ship.instance.GetComponent<GlobalCoordinate>().latitude.Value = ship.latitude;
        ship.instance.GetComponent<GlobalCoordinate>().longitude.Value = ship.longitude;
        ship.instance.GetComponent<ShipAi>().TravelPositionCounter = ship.TravelPositionCounter;


    }
    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        foreach(NpcShip ship in shipList)
        {
            if(ship.instance!=null)
                UnloadShip(ship);
        }
    }


    void UnloadShip(NpcShip ship)
    {
        if(ship.instance == null)
            return;
        ship.latitude = ship.instance.GetComponent<GlobalCoordinate>().latitude.Value;
        ship.longitude = ship.instance.GetComponent<GlobalCoordinate>().longitude.Value;
        ship.TravelPositionCounter = ship.instance.GetComponent<ShipAi>().TravelPositionCounter;
        NetworkObject shipNetworkObject = ship.instance.GetComponent<NetworkObject>();
        //TODO kaliteli bir gemi modeline geçildiğinde burası düzenlenmesi gerek
        //TODO silinen geminin konum ve hedef bilgilerinin , NpcShip verisinde tutulması gerek
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

            /* //TODO pasif gemi hareketi
            foreach(NpcShip ship in shipList)
            {
                if(ship.instance == null)
                {
                    position targetPos = ship.targetPositions[ship.TravelPositionCounter];
                    //TODO hareket belki geminin yelkenlerinin hızına göre ayarlanabilir
                    // hesaplanan birime göre konumdaki oynaması gereken kordinatlar hesaplanıp ona göre eklenebilir.
                   
                }
            }
            */
        }

        
        
        
    }
}
