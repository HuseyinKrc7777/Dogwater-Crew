using Unity.Netcode;
using UnityEngine;
using UnityEngine.UIElements;

public class boberController : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public FishingRod rod;
    public bool hooked = false;
    public bool onWater = false;
    public Rigidbody _rigidbody ;
    void Start()
    {
        _rigidbody = GetComponent<Rigidbody>();
    }

    // Update is called once per frame
    void Update()
    {
    
        floatObject();
    }
    public void floatObject()
    {
        if (WaterController.Instance == null)
            return;

        Vector3? temp = WaterController.Instance.GetWave(transform.position);
        Vector3 wave = new();
        if (temp != null)
            wave = (Vector3)temp;
        else
            wave = transform.position;
        if (transform.position.y < wave.y)
        {
            onWater = true;
            //_rigidbody.useGravity = false;
            _rigidbody.AddForce(Vector3.up * 15, ForceMode.Force);
        }
        else
        {
            onWater = false;
            _rigidbody.useGravity = true;
        }


    }
    public void ReelIn()
    {
        Debug.LogError("REELING IN");

        onWater = false;
        if(rod.GetLenght() > 2f)
        {
            Vector3 dir = (rod.rodTipPosition - transform.position).normalized;
            _rigidbody.AddForce(dir * 2, ForceMode.Force);
            rod.player.FishingRodReelRpc(dir*2);
            
        }
        else if(Mathf.Abs((rod.rodTipPosition - transform.position).x) < 0.5f && Mathf.Abs((rod.rodTipPosition - transform.position).z) < 0.5f)
        {
            //TODO: bu düzeltilecek , bober oltanın altında iken yukarı doğru hareket edecek.
            //hatta direk kinematic yapıp yukarı doğru bile hareket ettirilebilr.
            Vector3 dir = Vector3.up;
            _rigidbody.AddForce(dir * 30, ForceMode.Impulse);
            rod.player.FishingRodFlickRpc(dir*30);

        }


    }

    public void Flick(Vector3 Force)
    {
        _rigidbody.AddForce(Force , ForceMode.Impulse);   
        rod.player.FishingRodFlickRpc(Force);
    }

    private void OnCollisionEnter(Collision other)
    {
        rod.onBoberHit(other);

    }
}
