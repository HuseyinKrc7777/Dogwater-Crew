
using DogWater;
using SunCalcSharp;
using Unity.Cinemachine;
using UnityEngine;

public class Sextant : IItem
{
    private bool _equipped;
    private bool _interacting;
    private Player player;
    CinemachineCamera cameraCinemachine;
    Camera camera;

    private Camera leftCamera;
    private Camera rightCamera;

    private CinemachineCamera leftCM;
    private CinemachineCamera rightCM;

    GameObject leftAimObject;
    GameObject rightAimObject;
    public bool Equipped { get => _equipped; set => _equipped = value; }
    public bool Interacting { get => _interacting; set => _interacting = value; }

    public void Equip(FirstPersonController controller)
    {
        player = controller.GetComponent<Player>();
        if (camera == null)
        {
            cameraCinemachine = (CinemachineCamera)CinemachineCore.GetVirtualCamera(0);
            camera = Camera.main;
        }



        // Duplicate Unity camera
        leftCamera = UnityEngine.Object.Instantiate(camera);
        rightCamera = UnityEngine.Object.Instantiate(camera);
        leftCamera.GetComponent<AudioListener>().enabled = false;

        leftCamera.name = "Camera_Left";
        rightCamera.name = "Camera_Right";

        // Duplicate Cinemachine cameras
        leftCM = UnityEngine.Object.Instantiate(cameraCinemachine);
        rightCM = UnityEngine.Object.Instantiate(cameraCinemachine);

        leftCM.name = "CM_Left";
        rightCM.name = "CM_Right";



        leftAimObject = new GameObject("LeftAimTarget");
        rightAimObject = new GameObject("RightAimTarget");
        leftAimObject.tag = "CinemachineTarget";
        rightAimObject.tag = "CinemachineTarget";


        Transform leftAimTarget = leftAimObject.transform;
        Transform rightAimTarget = rightAimObject.transform;

        Transform firstPersonTarget = controller.CinemachineCameraTarget.transform;
        leftAimTarget.SetParent(firstPersonTarget, false);
        rightAimTarget.SetParent(firstPersonTarget, false);

        leftAimTarget.localRotation =
            Quaternion.Euler(0f, -18, 0f);

        rightAimTarget.localRotation =
            Quaternion.Euler(0f, 18, 0f);


        leftCM.Follow = firstPersonTarget;
        rightCM.Follow = firstPersonTarget;

        rightCM.Target.TrackingTarget = rightAimTarget;
        leftCM.Target.TrackingTarget = leftAimTarget;

        leftCM.Target.LookAtTarget = leftAimTarget;
        rightCM.Target.LookAtTarget = rightAimTarget;
        if (leftCM.GetComponent<CinemachineRotationComposer>() == null)
        {
            leftCM.gameObject.AddComponent<CinemachineRotationComposer>();
        }

        if (rightCM.GetComponent<CinemachineRotationComposer>() == null)
        {
            rightCM.gameObject.AddComponent<CinemachineRotationComposer>();
        }
        
       

        CinemachineBrain leftBrain =
            leftCamera.GetComponent<CinemachineBrain>();

        CinemachineBrain rightBrain =
            rightCamera.GetComponent<CinemachineBrain>();


        if (leftBrain == null)
        {
            leftBrain = leftCamera.gameObject.AddComponent<CinemachineBrain>();
        }

        if (rightBrain == null)
        {
            rightBrain = rightCamera.gameObject.AddComponent<CinemachineBrain>();
        }


        leftCM.OutputChannel = OutputChannels.Channel01;
        rightCM.OutputChannel = OutputChannels.Channel02;


        leftBrain.ChannelMask = OutputChannels.Channel01;
        rightBrain.ChannelMask = OutputChannels.Channel02;


        leftCamera.rect = new Rect(0f, 0f, 0.5f, 1f);
        rightCamera.rect = new Rect(0.5f, 0f, 0.5f, 1f);


        leftCM.enabled = true;
        rightCM.enabled = true;

        leftCM.Priority = 100;
        rightCM.Priority = 100;

        rightCamera.gameObject.SetActive(false);
        rightCM.gameObject.SetActive(false);

        leftCamera.gameObject.SetActive(false);
        leftCM.gameObject.SetActive(false);


    }
    float count = 0;
    public void Interact()
    {
        //player ' dan görüntüsel fonksiyonlar ve rpc ler çalıştırılacak
        if(!Interacting)
            changeCam();
        Interacting = true;
        count+=Time.deltaTime;
        if(count>=1)
        {
            count = 0;
            //eğer kutup yıldızı gibi başka birşeye göre hesap yapılmak istenilirse diye açının gerçek değerini de
            // gösterilmemesini düşünmedim değil
            Debug.LogError(GetSunLatitudeCalculation());
        }
    }
    public double GetSunLatitudeCalculation()
    {
        float angle = Mathf.Abs(Mathf.DeltaAngle(
            leftCamera.transform.eulerAngles.x, 
            rightCamera.transform.eulerAngles.x
        ));
        double calculatedLatitude = 90 - angle - Mathf.Rad2Deg*SunCalc.GetDeclination(GameDayClock.Instance.GetCurrentDate());
        return calculatedLatitude;
    }

    public void UnEquip()
    {
        UnityEngine.Object.Destroy(leftCamera.gameObject);
        UnityEngine.Object.Destroy(leftCM.gameObject);
        UnityEngine.Object.Destroy(rightCamera.gameObject);
        UnityEngine.Object.Destroy(rightCM.gameObject);
        UnityEngine.Object.Destroy(leftAimObject);
        UnityEngine.Object.Destroy(rightAimObject);

        camera.gameObject.SetActive(true);
        cameraCinemachine.gameObject.SetActive(true);

    }

    public void UnInteract()
    {
        if(Interacting)
            changeCam();
        Interacting = false;

    }
    private void changeCam()
    {
        camera.gameObject.SetActive(!camera.gameObject.activeSelf);
        cameraCinemachine.gameObject.SetActive(!cameraCinemachine.gameObject.activeSelf);

        leftCamera.gameObject.SetActive(!leftCamera.gameObject.activeSelf);
        leftCM.gameObject.SetActive(!leftCM.gameObject.activeSelf);

        rightCamera.gameObject.SetActive(!rightCamera.gameObject.activeSelf);
        rightCM.gameObject.SetActive(!rightCM.gameObject.activeSelf);
    }
    public void Use1()
    {

        rightAimObject.transform.Rotate(Vector3.right,-0.2f);
    }

    public void Use2()
    {
        rightAimObject.transform.Rotate(Vector3.right,0.2f);


        
    }
}