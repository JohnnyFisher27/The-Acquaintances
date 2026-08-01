using UnityEngine;
using TMPro;
using System.Collections.Generic;
using System.Collections;
using UnityEngine.UI;
public class ItemInventory : MonoBehaviour
{
    public Image icon;
    public TextMeshProUGUI txAmount;
    public Plant myPlant;

    public void SetUp(int cant, Plant p) 
    {
        myPlant = p;
        icon.sprite = myPlant.spr;
        txAmount.text = cant.ToString();

        if (cant <= 1) txAmount.gameObject.SetActive(false);
    }

    public void OnClick() 
    {
        if (myPlant.type != TypeItem.Plant) return;

        FindAnyObjectByType<Inventory>().UpdateSeed(myPlant.id);
    }
}
