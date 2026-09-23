using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using DogWater;
using NUnit.Framework;
using TMPro;
using UnityEditor;
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

    private class Page
    {
        public string Text;
        public Page(){}
        public Page(string text)
        {
            Text = text;
        }
        //DateTime date;
    }
    public Diary()
    {
        //ilk 2 sayfayı ekliyoruz
        pages.Add(new Page(1.ToString()+'\n'));
        pages.Add(new Page(2.ToString()+'\n'));

    }
    public void Equip(FirstPersonController controller)
    {

        player = controller.GetComponent<Player>();
        textArea = player.diaryUi;

        UpdateDisplay();
        //elde göster
    }

    public bool open = false;
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
        Interacting = false;
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
            Page page = new((currentPage+1).ToString()+'\n');
            pages.Add(page);
        }
        textArea.text = pages[currentPage].Text;
        
    }
    public void savePage()
    {
        pages[currentPage].Text = textArea.text;
    }
    public void NextPage()
    {
        savePage();
        currentPage++;
        UpdateDisplay();
    }

    public void PreviousPage()
    {
        if (currentPage == 0)
            return;
        savePage();
        currentPage--;
        UpdateDisplay();
    }
    float counter1 = 0;
    float counter2 = 0;

    public void Use1()
    {
        counter1+=Time.deltaTime;
        counter2=0;
        Debug.LogError(counter1);
        if(counter1>=0.3)
        {
            counter1=0;
            PreviousPage();
        }
    }

    public void Use2()
    {
        counter1=0;
        counter2+=Time.deltaTime;
        Debug.LogError(counter2);
        if(counter2>=0.3)
        {
            counter2=0;
            NextPage();
        }
    }
}