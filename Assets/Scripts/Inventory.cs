using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System;
using UnityEngine.InputSystem;
public class Inventory : MonoBehaviour
{
    public List<Item> inventory;

    public DataPlants plants;

    public RectTransform container;
    public GameObject panelInventory;
    public GameObject prefabItem;

    public void UpdateSeed(int id) { currentSeed = id; }

    [SerializeField] private int currentSeed;
    public int CurrentSeed => currentSeed;

    private void Update() 
    {
        if (Keyboard.current.iKey.wasPressedThisFrame)
            TurnPanel();

        if (Keyboard.current.uKey.wasPressedThisFrame) 
        {
            AddItem(1, 1);
        }
        if (Keyboard.current.oKey.wasPressedThisFrame)
        {
            RestItem(1, 1);
        }

    }

    public void AddItem( int id, int cant) 
    {
        bool isExisting = false;
        for (int i = 0; i < inventory.Count; i++) 
        {
            if (inventory[i].id == id) 
            {
                inventory[i].cant += cant;
                isExisting = true;
                break;
            }
        }

        if (!isExisting)
        {
            
            inventory.Add(new Item(id, cant));
        }
        if (panelInventory.activeInHierarchy) showItems();
    }



    public void RestItem(int id, int cant) 
    {
        for (int i = 0; i < inventory.Count; i++) 
        {
            if (inventory[i].id == id) 
            {
                inventory[i].cant -= cant;
                if (inventory[i].cant == 0) 
                {
                    inventory.Remove(inventory[i]);
                    if (currentSeed == inventory[i].id) currentSeed = 0;
                } 
                return;
            }
        }
        if (panelInventory.activeInHierarchy) showItems();
    }

    private void TurnPanel()
    {
        panelInventory.SetActive(!panelInventory.activeInHierarchy);

        if (panelInventory.activeInHierarchy) showItems();
    }

    public void showItems() 
    {
        foreach (Transform item in container) 
        {
            Destroy(item.gameObject);
        }
        for (int i = 0; i < inventory.Count; i++) 
        {
            GameObject newItem = Instantiate(prefabItem, container);
            Plant plant = plants.plants.FirstOrDefault(x => x.id == inventory[i].id);
            newItem.GetComponent<ItemInventory>().SetUp(inventory[i].cant, plant);
        }

    }
}


[Serializable]
public class Item
{
    public int id;
    public int cant;

    public Item(int id, int cant)
    {
        this.id = id;
        this.cant = cant;
    }
}
