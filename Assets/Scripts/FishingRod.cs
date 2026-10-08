using UnityEngine;
using System;
using DogWater;
using UnityPipeline.Microsoft.CodeAnalysis.CSharp.Syntax;
using Unity.VisualScripting;
using Unity.Services.Authentication.PlayerAccounts;
using Unity.Netcode;

public class FishingRod : IItem
{
    private bool _equipped;
    private bool _interacting;
    internal Player player;
    public Vector3 rodTipPosition{get => player.FishingRodObject.transform.position + player.FishingRodObject.transform.up *2;}

    public bool Equipped { get => _equipped; set => _equipped = value; }
    public bool Interacting { get => _interacting; set => _interacting = value;}
    
    IHoldableItem ItemInHook;
    float _charge = 0.0f;
    float _maxCharge = 2.0f ;
    public float GetLenght()
    {
        if(bober!=null)
            return Vector3.Distance(rodTipPosition,bober.transform.position);
        else
            return 0;
    }
    public float _maxLenght = 20.0f;
    boberController bober;
    
    public void Equip(FirstPersonController controller)
    {
        player = controller.GetComponent<Player>();
        player.FishingRodEquipRpc(true);
        _interacting = true;
    }

    public void Interact()
    {
        //oltayı kitliyor , uzaklaşmasını engelliyor
        //olta kitli iken mouse hareketleri oltayı etkiler
        if(_charge<_maxCharge && bober==null)
            _charge+=Time.deltaTime;
        else if(bober!=null)
        {
            ItemInHook?.Drop(bober.transform.position);
            ItemInHook = null;            
        }
    }

    public void UnEquip()
    {
        _interacting = false;
        player.FishingRodEquipRpc(false);
        _charge = 0;
        if(bober!=null)
            ItemInHook?.Drop(bober.transform.position);
        ItemInHook = null;
        GameObject.Destroy(bober?.gameObject);
        player.DestroyBobberRpc();

        _interacting = false;
    }

    public void UnInteract()
    {
        if(_charge>0.0f && bober==null)
        {
            player.FishingRodCastRpc(_charge,player.LookDirection);
            CastRod(_charge,player.LookDirection);
            _charge = 0.0f;
        }
    }
    //balıklar için balık zorlarken interact bıraklıp balığın serbest hareket etmesini sağlamak , zıplarken flick ike kaçmasını önlemek , sakinken de sol tık ile çekmek gereklidir
    //bunlar yapılmassa balık zorluğundan farkederek deneme sayısı sonunda balık kopar


    public void Use1()
    {
    //player.FishingRodAnimationRpc() tarzında şeyler çağırılabilir 
        
        bober?.ReelIn();
        
    }
    Vector3 lastDirection;
    public void Use2()
    {
        //bu ile flick yapılbilir. bu eylem oltay attıktan sonra yön vermek için kullanılabilir
        //sağ tıka basılı tutarken mouse hareketi olratnın ucunu hareket ettirir
        
        if(lastDirection!=null)
        {
            //TODO: bu düzeltilecek , bakılan pozisyon ve büyüklüğüne göre
            // yukarı ise yukarı , sağa sağa ,sola sola,aşağı da oyuncuya doğru attıracak şekilde
            //Rpc fonksiyonu da düzeltilecek
            Vector3 flick = player.LookDirection - lastDirection; 
            bober?.Flick(flick*10);
        }
            
        lastDirection = player.LookDirection;

        //player.lookdirection ' dan oyuncunun hareketindeki değişikliğe göre güç uygulanacak.        
        //CastRod(_charge,player.transform.position,player.transform.forward);
    }

    public void CastRod(float charge,Vector3 dir)
    {
        bober = UnityEngine.Object.Instantiate(player.bobber).GetComponent<boberController>();
        bober.GetComponent<boberController>().rod = this;
        bober.transform.position = player.bobber.transform.position;
        bober.gameObject.SetActive(true);
        bober._rigidbody = bober.GetComponent<Rigidbody>();
        bober._rigidbody.AddForce(dir.normalized * charge*15,ForceMode.Impulse);
        //oltanun ucu ile olta arası her client kendi ip çizme ile yükümlüdür
        //olta ucunun konumu atış ile her client kendi yapacak


        //sıra ile açılı bir şekilde yere doğru inecek , gücü charge ile belli olan
        //bunu client tarafından bir objeye rigidbody ve collider ekleyip fırlatarak yapabilir,
        //çarpan obje eğer holdableitem ise oltanın ItemInHook a atanıp çekilebilir hale gelir.
        //çekilme işlemi tutulma işlemi gibi fakat mesh oltanın ucuna bağlı olarak (sticktosurface ile)
        //çekme sırasında e ile basılınca obje oradan yere bırakılır (Drop ile)
    }


    public void onBoberHit(Collision other)
    {
        Debug.LogError("hit"+other.gameObject);
        RaycastHit []hits = {};
        int layer = 1 << LayerMask.NameToLayer("Default");
        hits = Physics.SphereCastAll(bober.transform.position, 0.5f, bober.transform.forward, 1.0f, layer);
        if (hits.Length>0)
        {
            foreach(RaycastHit hit in hits)
            {
                Debug.LogError("spherecast"+hit.collider.gameObject);

                if(hit.collider.gameObject.TryGetComponent(out IHoldableItem item))
                {
                    Debug.LogError("hooked");
                    ItemInHook = item;
                    item.spawnedMesh.transform.parent = bober.transform;
                    player.HookRpc(item.NetworkObjectId);
                }
            }
            
        }
        
    }
}

