using DogWater;
using UnityEngine;

public class RatLines : MonoBehaviour , IHandInput , IInteractable
{
    FirstPersonController playerController;
    private bool holding = false;
    public void OnHandInput(float xValue, float yValue)
    {
        playerController._verticalVelocity+=yValue * 10;
    }

    public void OnInteract(Player player)
    {
        playerController = player.controller;
        holding = true;
    }

    public void OnUnInteract(Player player)
    {
        holding = false;
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if(holding)
        {
            if(playerController._verticalVelocity < 0 )
                playerController._verticalVelocity = 0;
        }
    }
}
