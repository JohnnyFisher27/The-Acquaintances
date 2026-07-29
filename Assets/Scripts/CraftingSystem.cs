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
    public List<Upgrade> upgrades;

    public enum TypeSystem { Crafting, Alchemy }
    public TypeSystem typeSystem;

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

        switch (typeSystem) 
        {
            case TypeSystem.Crafting:
                for (int i = 0; i < recipes.Count; i++)
                {
                    Plant p = GameManager.Instance.runtimePlants.plants.FirstOrDefault(x => x.id == recipes[i]);

                    GameObject newRecipe = Instantiate(prefab, container);
                    newRecipe.transform.GetChild(0).GetComponent<TextMeshProUGUI>().text = p.namePlant;

                    string items = "";
                    for (int j = 0; j < p.neccesaryItems.Count; j++)
                    {
                        bool islast = j == p.neccesaryItems.Count - 1;
                        Plant adI = GameManager.Instance.runtimePlants.plants.FirstOrDefault(x => x.id == p.neccesaryItems[j].id);
                        items += adI.namePlant + " : " + p.neccesaryItems[j].cant + (islast ? "." : " + ");

                    }
                    newRecipe.transform.GetChild(1).GetComponent<TextMeshProUGUI>().text = items;
                    newRecipe.transform.GetChild(2).GetComponent<Button>().onClick.AddListener(() => tryMakeItem(p.id));
                }
                break;
            case TypeSystem.Alchemy:
                for (int i = 0; i < upgrades.Count; i++)
                {
                    int index = i;

                    Plant p = GameManager.Instance.runtimePlants.plants.FirstOrDefault(x => x.id == upgrades[i].id);

                    GameObject newRecipe = Instantiate(prefab, container);
                    newRecipe.transform.GetChild(0).GetComponent<TextMeshProUGUI>().text = p.namePlant;

                    string items = "";
                    for (int j = 0; j < p.neccesaryItems.Count; j++)
                    {
                        bool islast = j == p.neccesaryItems.Count - 1;
                        Plant adI = GameManager.Instance.runtimePlants.plants.FirstOrDefault(x => x.id == p.neccesaryItems[j].id);
                        items += adI.namePlant + " : " + p.neccesaryItems[j].cant + (islast ? "." : " + ");

                    }
                    newRecipe.transform.GetChild(1).GetComponent<TextMeshProUGUI>().text = items;
                    newRecipe.transform.GetChild(2).GetComponent<Button>().onClick.AddListener(() => TryMakeUpgrade(upgrades[index]));
                }
                break;
        }
        
    }
    public void tryMakeItem(int id) 
    {
        Plant p = GameManager.Instance.runtimePlants.plants.FirstOrDefault(x => x.id == id);
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
    public void TryMakeUpgrade(Upgrade currentUpgrade)
    {
        Plant p = GameManager.Instance.runtimePlants.plants.FirstOrDefault(x => x.id == currentUpgrade.id);
        Inventory inventory = FindAnyObjectByType<Inventory>();

        bool canCraft = true;
        for (int i = 0; i < p.neccesaryItems.Count; i++)
        {
            int neededId = p.neccesaryItems[i].id;
            int neededCant = p.neccesaryItems[i].cant;

            var inventoryItem = inventory.inventory.FirstOrDefault(x => x.id == neededId);

            if (inventoryItem == null || inventoryItem.cant < neededCant)
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
        for (int i = 0; i < GameManager.Instance.runtimePlants.plants.Count; i++) 
        {
            if (GameManager.Instance.runtimePlants.plants[i].id == currentUpgrade.id) 
            {
                RunTimePlants runTime = GameManager.Instance.runtimePlants;

                if (currentUpgrade.mM) runTime.plants[i].multMorning *= currentUpgrade.multiplier;
                if (currentUpgrade.mA) runTime.plants[i].multAfternoon *= currentUpgrade.multiplier;
                if (currentUpgrade.mN) runTime.plants[i].multNight *= currentUpgrade.multiplier;
                if (currentUpgrade.wDR) runTime.plants[i].waterDepletionRate *= currentUpgrade.multiplier;
                if (currentUpgrade.hR) runTime.plants[i].heatResist *= currentUpgrade.multiplier;
                if (currentUpgrade.rR) runTime.plants[i].rainResist *= currentUpgrade.multiplier;
                if (currentUpgrade.wR) runTime.plants[i].windResist *= currentUpgrade.multiplier;
                if (currentUpgrade.gT) runTime.plants[i].groundTimer *= currentUpgrade.multiplier;

                Debug.Log(
                    $"Datos actualizados de: {runTime.plants[i].namePlant}\n" +
                    $"\nMult Morning: {runTime.plants[i].multMorning}" +
                    $"\nMult Afternoon: {runTime.plants[i].multAfternoon}" +
                    $"\nMult Night: {runTime.plants[i].multNight}" +
                    $"\nWater Depletion Rate: {runTime.plants[i].waterDepletionRate}" +
                    $"\nHeat Resist: {runTime.plants[i].heatResist}" +
                    $"\nRain Resist: {runTime.plants[i].rainResist}" +
                    $"\nWind Resist: {runTime.plants[i].windResist}" +
                    $"\nGrow Timer: {runTime.plants[i].groundTimer}"
                );
                return;
            }
        }
    }

}
[Serializable]
public class Upgrade 
{
    public int id;
    public float multiplier;

    public bool mM;
    public bool mA;
    public bool mN;
    public bool wDR;
    public bool hR;
    public bool rR;
    public bool wR;
    public bool gT;



    public Upgrade(int id, float multiplier, bool mM, bool mA, bool mN, bool wDR, bool hR, bool rR, bool wR, bool gT) 
    {
        this.id = id;
        this.multiplier = multiplier;
        this.mM = mM;
        this.mA = mA;
        this.mN = mN;
        this.wDR = wDR;
        this.hR = hR;
        this.rR = rR;
        this.wR = wR;
        this.gT = gT;

    }
}

