using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.VisualScripting.FullSerializer;
using UnityEngine;

[Serializable]
public class NpcShip
{
    public GlobalCoordinate coordinate;
    public List<GlobalCoordinate> targetPositions;
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
        GlobalCoordinate shipCoordinate = Ship.PlayerShip.coordinate;
        Debug.Log("checking");
        foreach (NpcShip ship in shipList)
        {
            if (GlobalCoordinate.CalculateDistanceBetweenTwoPoints(shipCoordinate, ship.coordinate) < ship.loadDistance)
            {
                if(ship.instance!=null)
                    continue;
                _ = LoadShip(ship);
            }
            else
                UnloadShip(ship);
        }
    }
    private async Task LoadShip(NpcShip ship)
    {
        GlobalCoordinate shipCoordinate = Ship.PlayerShip.coordinate;
        Vector3 shipPos = Ship.PlayerShip.transform.position;
        Debug.Log("loading");
        
        float dist = GlobalCoordinate.CalculateDistanceBetweenTwoPoints(shipCoordinate,ship.coordinate);

        Vector3 dir = GlobalCoordinate.CalculateDirectionBetweenTwoPoints(shipCoordinate,ship.coordinate);

        Vector3 position = dir * dist + shipPos;
        Quaternion rotation = Quaternion.identity;

        SpawnShip(ship,position,rotation);
    }

    async void SpawnShip(NpcShip ship, Vector3 position, Quaternion rotation)
    {
        Debug.Log("spawning in "+position +" coordinates : ");
        
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
        ShipAi shipai = ship.instance.GetComponent<ShipAi>();

        ship.instance.GetComponent<Ship>().coordinate = ship.coordinate;
        shipai.whatKindOfShipIsThis = ship.shipKind;
        shipai.TravelPositions = ship.targetPositions;
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
        ship.coordinate = ship.instance.GetComponent<Ship>().coordinate;
        ship.TravelPositionCounter = ship.instance.GetComponent<ShipAi>().TravelPositionCounter;

        NetworkObject shipNetworkObject = ship.instance.GetComponent<NetworkObject>();
        //TODO kaliteli bir gemi modeline geçildiğinde burası düzenlenmesi gerek
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
