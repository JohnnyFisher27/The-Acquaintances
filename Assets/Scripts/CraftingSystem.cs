using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using TMPro;
using System.Linq;
using UnityEngine.UI;
public class CraftingSystem : MonoBehaviour
{
    public GameObject prefab;
    public List<int> recipes;
    public RectTransform container;
    public DataPlants plants;

    [Header("UI")]
    public GameObject panel;
    private void Start() 
    {
        ShowRecipies();
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.collider.CompareTag("Player")) 
        {
            if (!panel.activeInHierarchy) 
            {
                panel.SetActive(true);
                ShowRecipies();
            }
        
        }
    }
    public void ShowRecipies() 
    {
        if (container.childCount > 0) return;


        for (int i = 0; i < recipes.Count; i++) 
        {
            Plant p = plants.plants.FirstOrDefault(x => x.id == recipes[i]);

            GameObject newRecipe = Instantiate(prefab, container);
            newRecipe.transform.GetChild(0).GetComponent<TextMeshProUGUI>().text = p.namePlant;

            string items = "";
            for (int j = 0; j < p.neccesaryItems.Count; j++) 
            {
                bool islast =  j == p.neccesaryItems.Count - 1;
                Plant adI = plants.plants.FirstOrDefault(x => x.id == p.neccesaryItems[j].id);
                items += adI.namePlant + " : " + p.neccesaryItems[j].cant  + (islast ? "." : " + ");

            }
            newRecipe.transform.GetChild(1).GetComponent<TextMeshProUGUI>().text = items;
            newRecipe.transform.GetChild(2).GetComponent<Button>().onClick.AddListener(()=> tryMakeItem(p.id));
        }
    }
    public void tryMakeItem(int id) 
    {
        Plant p = plants.plants.FirstOrDefault(x => x.id == id);
        Inventory inventory = FindAnyObjectByType<Inventory>();

        bool canCraft = true;
        for (int i = 0; i < p.neccesaryItems.Count; i ++) 
        {
            int neededId = p.neccesaryItems[i].id;
            int neededCant = p.neccesaryItems[i].cant;

            var inventoryItem = inventory.inventory.FirstOrDefault(x => x.id == neededId);

            if ( inventoryItem == null || inventoryItem.cant < neededCant) 
            {
                canCraft = false;
                Debug.Log($"[Craft] player doesn't have {neededId} : {neededId}");
                break;
            }
        }
        if (!canCraft) 
        {
            Debug.Log("[Craft] player doesn't have necessary items");
            return;
        }
        for (int i = 0; i < p.neccesaryItems.Count; i++) 
        {
            int neededId = p.neccesaryItems[i].id;
            int neededCant = p.neccesaryItems[i].cant;


            inventory.RestItem(neededId, neededCant);

        }
        inventory.AddItem(id, 1);
        Debug.Log($"[Craft] {p.namePlant} create!");

    }
}
