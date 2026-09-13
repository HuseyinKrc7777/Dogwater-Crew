using System;
using NUnit.Framework;
using UnityEngine;

public class PauseMenuController : MonoBehaviour
{
    [SerializeField] private GameObject menu;
    public static PauseMenuController Instance { get; private set; }
    public bool isMenuOpen = false;
    internal void OpenPauseMenu()
    {
        isMenuOpen = true;
        menu.SetActive(true);
    }
    internal void ClosePauseMenu()
    {
        isMenuOpen = false;
        menu.SetActive(false);
    }
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        menu.SetActive(false);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
