using UnityEngine;
using DogWater;

public class FishingRod : IItem
{
    private bool _equipped;
    private bool _interacting;
    internal Player player;
    public bool Equipped { get => _equipped; set => _equipped = value; }
    public bool Interacting { get => _interacting; set => _interacting = value; }

    IHoldableItem ItemInHook;
    float _charge = 0.0f;
    float _maxCharge = 2.0f;
    public float GetLenght()
    {
        if (bober != null)
            return Vector3.Distance(player.getRodTipPosition(), bober.transform.position);
        else
            return 0;
    }
    public float _maxLenght = 20.0f;
    public boberController bober;

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
        if (_charge < _maxCharge && bober == null)
            _charge += Time.deltaTime;
        else if (bober != null)
        {
            //ItemInHook?.Drop(bober.transform.position);
            //ItemInHook = null;
        }
    }

    public void UnEquip()
    {
        _interacting = false;
        player.FishingRodEquipRpc(false);
        _charge = 0;
        if(bober!=null)
            ItemInHook?.Drop(bober.transform.position);
        else
        {
            //bu pozisyon eşya düşürme fonksiyonundan referans
            Vector3 pos = player.transform.position + new Vector3(player.transform.forward.x,1,player.transform.forward.z)*2;
            ItemInHook?.Drop(pos);
        }
        ItemInHook = null; 
        GameObject.Destroy(bober?.gameObject);
        player.DestroyBobberRpc();
        bober = null;

        _interacting = false;
    }

    public void UnInteract()
    {
        if (_charge > 0.0f && bober == null)
        {
            player.FishingRodCastRpc(_charge, player.LookDirection);
            CastRod(_charge, player.LookDirection);
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

        if (lastDirection != null)
        {
            //TODO: bu düzeltilecek , bakılan pozisyon ve büyüklüğüne göre
            Vector3 flick = player.LookDirection - lastDirection;
            
            bober?.Flick(flick * 10);
        }

        lastDirection = player.LookDirection;

        //player.lookdirection ' dan oyuncunun hareketindeki değişikliğe göre güç uygulanacak.        
        //CastRod(_charge,player.transform.position,player.transform.forward);
    }

    public void CastRod(float charge, Vector3 dir)
    {
        bober = UnityEngine.Object.Instantiate(player.bobber).GetComponent<boberController>();
        bober.GetComponent<boberController>().rod = this;
        bober.transform.position = player.getRodTipPosition();
        if(ItemInHook!=null)
        {
            ItemInHook.spawnedMesh.transform.parent = bober.transform;
            ItemInHook.spawnedMesh.transform.position = bober.transform.position;
            player.HookRpc(ItemInHook.NetworkObjectId);

        }
        bober.gameObject.SetActive(true);
        bober._rigidbody = bober.GetComponent<Rigidbody>();
        bober._rigidbody.AddForce(dir.normalized * charge * 15, ForceMode.Impulse);
        //oltanun ucu ile olta arası her client kendi ip çizme ile yükümlüdür
        //olta ucunun konumu atış ile her client kendi yapacak


        //sıra ile açılı bir şekilde yere doğru inecek , gücü charge ile belli olan
        //bunu client tarafından bir objeye rigidbody ve collider ekleyip fırlatarak yapabilir,
        //çarpan obje eğer holdableitem ise oltanın ItemInHook a atanıp çekilebilir hale gelir.
        //çekilme işlemi tutulma işlemi gibi fakat mesh oltanın ucuna bağlı olarak (sticktosurface ile)
        //çekme sırasında e ile basılınca obje oradan yere bırakılır (Drop ile)
    }

    public void retractBoberOnRod()
    {
        if(ItemInHook!=null)
        {
            ItemInHook.spawnedMesh.transform.parent = player.transform;
            ItemInHook.spawnedMesh.transform.position = player.getRodTipPosition();
            player.OnRodRpc(ItemInHook.NetworkObjectId);
        }
        GameObject.Destroy(bober?.gameObject);
        player.DestroyBobberRpc();
        bober = null;

    }


    public void onBoberHit(Collision other)
    {
        Debug.LogError("hit" + other.gameObject);
        RaycastHit[] hits = { };
        int layer = 1 << LayerMask.NameToLayer("Default");
        hits = Physics.SphereCastAll(bober.transform.position, 0.5f, bober.transform.forward, 1.0f, layer);
        if (hits.Length > 0)
        {
            foreach (RaycastHit hit in hits)
            {
                Debug.LogError("spherecast" + hit.collider.gameObject);

                if (hit.collider.gameObject.TryGetComponent(out IHoldableItem item))
                {
                    Debug.LogError("hooked");
                    ItemInHook = item;
                    bober.transform.position = item.spawnedMesh.transform.position;
                    item.spawnedMesh.transform.parent = bober.transform;
                    player.HookRpc(item.NetworkObjectId);
                }
            }

        }

    }
}

