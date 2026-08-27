using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Menu : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

    }
    //TODO host dan disconnect yapınca client patlıyor
    public void LeaveGame()
    {
        NetworkManager.Singleton.Shutdown();
        SceneManager.LoadScene("Bootstrap");
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
