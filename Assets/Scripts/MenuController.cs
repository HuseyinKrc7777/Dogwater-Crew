using System;
using NUnit.Framework;
using UnityEngine;

public class MenuController : MonoBehaviour
{
    [SerializeField] private GameObject menu;
    public static MenuController Instance { get; private set; }
    public bool isMenuOpen = false;
    internal void OpenMenu()
    {
        isMenuOpen = true;
        menu.SetActive(true);
    }
    internal void CloseMenu()
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
