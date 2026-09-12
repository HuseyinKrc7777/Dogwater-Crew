using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using DogWater;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

public class Diary : IItem
{
    //TODO yazma ui'ındaki düğmeler ile sonraki sayfalara geçme ve kapama işlemi yapıacaktır
    private bool _equipped;
    private bool _interacting;

    public bool Equipped { get => _equipped; set => _equipped = value; }
    public bool Interacting { get => _interacting; set => _interacting = value; }
    List<Page> pages = new();
    Player player;
    public TMP_InputField textArea;

    int currentPage = 0;

    private struct Page
    {
        public String Text;
        //DateTime date;
    }
    public Diary()
    {
        //ilk 2 sayfayı ekliyoruz
        pages.Add(new Page());
        pages.Add(new Page());

    }
    public void Equip(FirstPersonController controller)
    {

        player = controller.GetComponent<Player>();
        textArea = player.diaryUi;

        UpdateDisplay();
        //elde göster
    }
    bool open = false;
    bool ready = false;
    
    public void Interact()
    {
        if(ready)
        {
            if(!Interacting)
            {
                OpenDiary();
                Interacting = true;
            }
            
            ready = false;
        }
        

    }
    public void OpenDiary()
    {
        textArea.gameObject.SetActive(true);
        open = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        textArea.Select();

    }
    public void CloseDiary()
    {
        textArea.gameObject.SetActive(false);
        open = false;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public void UnEquip()
    {
        CloseDiary();
        ready= true;
        //elde göstermeyi bırak
    }

    public void UnInteract()
    {
        ready = true;
        //Interacting=false;
    }
    public void UpdateDisplay()
    {
        if (pages.Count <= currentPage)
        {
            Page page = new();
            page.Text = "";
            pages.Add(page);
        }
        textArea.text = pages[currentPage].Text;
        
    }
    public void NextPage()
    {
        currentPage++;
        UpdateDisplay();
    }

    public void PreviousPage()
    {
        if (currentPage == 0)
            return;
        currentPage--;
        UpdateDisplay();
    }
    public void Use1()
    {
        
    }

    public void Use2()
    {
        
    }
}