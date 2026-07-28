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

    private void Update() 
    {
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
    }



    public void RestItem(int id, int cant) 
    {
        for (int i = 0; i < inventory.Count; i++) 
        {
            if (inventory[i].id == id) 
            {
                inventory[i].cant -= cant;
                if (inventory[i].cant == 0) inventory.Remove(inventory[i]);
                return;
            }
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
