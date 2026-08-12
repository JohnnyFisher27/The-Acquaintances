using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine.UI;

// Two stations share this component. Crafting turns harvested produce into a
// seed of a new species; Alchemy spends produce to permanently upgrade a
// species' runtime stats for the rest of the run.
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

    private Inventory inventory;
    private readonly List<GameObject> rows = new List<GameObject>();
    private bool refreshQueued;

    [SerializeField] private AudioClip useMachineSound;
    [SerializeField] private AudioClip machineRunningSound;

    private Inventory Inv
    {
        get
        {
            if (inventory == null)
            {
                inventory = FindAnyObjectByType<Inventory>();
            }
            return inventory;
        }
    }

    private void Start()
    {
        if (Inv != null)
        {
            Inv.OnChanged += QueueRefresh;
        }

        if (panel != null)
        {
            panel.SetActive(false);
        }

        ShowRecipies();
    }

    private void OnDestroy()
    {
        if (inventory != null)
        {
            inventory.OnChanged -= QueueRefresh;
        }
    }

    // Crafting mutates the inventory several times in a row, and each change
    // would otherwise rebuild the rows mid-click. Coalesce to one rebuild.
    private void QueueRefresh()
    {
        refreshQueued = true;
    }

    private void LateUpdate()
    {
        if (refreshQueued)
        {
            refreshQueued = false;
            ShowRecipies();
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.collider.CompareTag("Player") && panel != null && !panel.activeInHierarchy)
        {
            SoundEffectsManager.instance.PlaySound(useMachineSound, transform, 1f);
            panel.SetActive(true);
            ShowRecipies();
        }
    }

    // Walking away closes the station again; nothing used to dismiss it.
    private void OnCollisionExit2D(Collision2D collision)
    {
        if (collision.collider.CompareTag("Player") && panel != null)
        {
            panel.SetActive(false);
        }
    }

    public void ShowRecipies()
    {
        if (container == null || prefab == null || Inv == null)
        {
            return;
        }

        // Tracked explicitly rather than via childCount, which still reports
        // objects that are only queued for destruction this frame.
        for (int i = 0; i < rows.Count; i++)
        {
            if (rows[i] != null)
            {
                Destroy(rows[i]);
            }
        }
        rows.Clear();

        switch (typeSystem)
        {
            case TypeSystem.Crafting:
                for (int i = 0; i < recipes.Count; i++)
                {
                    Plant p = Inv.PlantData(recipes[i]);
                    if (p == null)
                    {
                        Debug.LogWarning($"[Craft] recipe id {recipes[i]} has no plant data.");
                        continue;
                    }

                    int id = p.id;
                    BuildRow(p, p.namePlant + " (seed)", () => tryMakeItem(id));
                }
                break;

            case TypeSystem.Alchemy:
                for (int i = 0; i < upgrades.Count; i++)
                {
                    Upgrade upgrade = upgrades[i];
                    Plant p = Inv.PlantData(upgrade.id);
                    if (p == null)
                    {
                        Debug.LogWarning($"[Alchemy] upgrade id {upgrade.id} has no plant data.");
                        continue;
                    }

                    BuildRow(p, p.namePlant + " (upgrade)", () => TryMakeUpgrade(upgrade));
                }
                break;
        }
    }

    // One row: name, ingredients as held/needed, and a button disabled when the
    // player is short so the cost is readable before clicking.
    private void BuildRow(Plant p, string title, Action onClick)
    {
        GameObject newRecipe = Instantiate(prefab, container);
        rows.Add(newRecipe);
        newRecipe.transform.GetChild(0).GetComponent<TextMeshProUGUI>().text = title;

        string items = "";
        bool canCraft = true;
        for (int j = 0; j < p.neccesaryItems.Count; j++)
        {
            Item need = p.neccesaryItems[j];
            Plant ingredient = Inv.PlantData(need.id);
            string name = ingredient != null ? ingredient.namePlant : need.id.ToString();

            int held = Inv.Count(ItemKind.Produce, need.id);
            if (held < need.cant)
            {
                canCraft = false;
            }

            bool islast = j == p.neccesaryItems.Count - 1;
            items += $"{name} : {held}/{need.cant}" + (islast ? "." : " + ");
        }
        newRecipe.transform.GetChild(1).GetComponent<TextMeshProUGUI>().text = items;

        Button button = newRecipe.transform.GetChild(2).GetComponent<Button>();
        button.interactable = canCraft;
        button.onClick.AddListener(() => onClick());

    }

    // Check everything before spending anything, so a partial craft can never
    // eat the player's reagents.
    private bool SpendIngredients(Plant p)
    {
        for (int i = 0; i < p.neccesaryItems.Count; i++)
        {
            Item need = p.neccesaryItems[i];
            if (Inv.Count(ItemKind.Produce, need.id) < need.cant)
            {
                Debug.Log($"[Craft] not enough of item {need.id} for {p.namePlant}.");
                return false;
            }
        }

        for (int i = 0; i < p.neccesaryItems.Count; i++)
        {
            Item need = p.neccesaryItems[i];
            Inv.RestItem(ItemKind.Produce, need.id, need.cant);
        }
        SoundEffectsManager.instance.PlaySound(machineRunningSound, transform, 1f);
        return true;
    }

    public void tryMakeItem(int id)
    {
        Plant p = Inv != null ? Inv.PlantData(id) : null;
        if (p == null || !SpendIngredients(p))
        {
            return;
        }

        Inv.AddItem(ItemKind.Seed, id, 1);
        Debug.Log($"[Craft] created a {p.namePlant} seed.");
    }

    // Upgrades write into the runtime plant copies, so they last the run
    // without ever touching the BaseDataPlants asset on disk.
    public void TryMakeUpgrade(Upgrade currentUpgrade)
    {
        if (Inv == null || GameManager.Instance == null || GameManager.Instance.runtimePlants == null)
        {
            Debug.LogWarning("[Alchemy] no runtime plant data to upgrade.");
            return;
        }

        Plant p = Inv.PlantData(currentUpgrade.id);
        if (p == null || !SpendIngredients(p))
        {
            return;
        }

        List<Plant> runtime = GameManager.Instance.runtimePlants.plants;
        for (int i = 0; i < runtime.Count; i++)
        {
            if (runtime[i].id != currentUpgrade.id)
            {
                continue;
            }

            if (currentUpgrade.mM) runtime[i].multMorning *= currentUpgrade.multiplier;
            if (currentUpgrade.mA) runtime[i].multAfternoon *= currentUpgrade.multiplier;
            if (currentUpgrade.mN) runtime[i].multNight *= currentUpgrade.multiplier;
            if (currentUpgrade.wDR) runtime[i].waterDepletionRate *= currentUpgrade.multiplier;
            if (currentUpgrade.gT) runtime[i].groundTimer *= currentUpgrade.multiplier;

            // Resistances are 0-1 where higher is better, so multiplying is
            // wrong twice over: a plant sitting at 0 can never improve, and the
            // authored multipliers are below 1, which would make it worse.
            // Treat the multiplier as the fraction of the remaining gap to
            // immunity that this upgrade closes.
            if (currentUpgrade.hR) runtime[i].heatResist = ImproveResist(runtime[i].heatResist, currentUpgrade.multiplier);
            if (currentUpgrade.rR) runtime[i].rainResist = ImproveResist(runtime[i].rainResist, currentUpgrade.multiplier);
            if (currentUpgrade.wR) runtime[i].windResist = ImproveResist(runtime[i].windResist, currentUpgrade.multiplier);

            Debug.Log(
                $"[Alchemy] upgraded {runtime[i].namePlant}: " +
                $"morning {runtime[i].multMorning:F2}, afternoon {runtime[i].multAfternoon:F2}, " +
                $"night {runtime[i].multNight:F2}, water {runtime[i].waterDepletionRate:F3}, " +
                $"heat {runtime[i].heatResist:F2}, rain {runtime[i].rainResist:F2}, " +
                $"wind {runtime[i].windResist:F2}, grow {runtime[i].groundTimer:F1}");
            return;
        }
    }

    private static float ImproveResist(float current, float multiplier)
    {
        return Mathf.Clamp01(current + (1f - current) * Mathf.Clamp01(multiplier));
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
