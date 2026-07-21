using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using TMPro;
using System.Linq;
public class CraftingSystem : MonoBehaviour
{
    public GameObject prefab;
    public List<int> recipes;
    public RectTransform container;
    public DataPlants plants;
    private void Start() 
    {
        ShowRecipies();
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
                items += adI.namePlant + (islast ? "." : "+");

            }
            newRecipe.transform.GetChild(1).GetComponent<TextMeshProUGUI>().text = items;
        }
    }
}
