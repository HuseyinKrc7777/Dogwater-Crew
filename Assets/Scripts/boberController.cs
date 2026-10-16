using Unity.Netcode;
using Unity.VisualScripting;
using Unity.VisualScripting.FullSerializer;
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
    public void MoveTowardsPosition(Vector3 pos)
    {
        float dist = Vector3.Distance(pos,transform.position);
        Vector3 dir = pos - transform.position;
        if(dist > 3.0f)
            transform.position = pos;
        else
        {
            _rigidbody.AddForce(dir*dist/3);
        }
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

        onWater = false;
 
        Vector3 dir = (rod.player.getRodTipPosition() - transform.position).normalized;
        //float dist = rod.GetLenght();
        Vector2 pos1 = new Vector2(rod.player.getRodTipPosition().x,rod.player.getRodTipPosition().z);
        Vector2 pos2 = new Vector2(transform.position.x,transform.position.z);
        float horizontalDist = Vector2.Distance(pos1,pos2);
        float power = Mathf.Max(2,10/horizontalDist);

        
        _rigidbody.AddForce(dir * power, ForceMode.Force);
        rod.player.FishingRodReelRpc(dir*power);

        if(Vector3.Distance(rod.player.getRodTipPosition(),transform.position) < 0.3f)
        {
            rod.retractBoberOnRod();
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
