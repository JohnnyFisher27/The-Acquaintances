using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine.UI;

// The alchemy table. Recipes consume harvested produce and hand back a seed of
// the crafted species, which closes the loop: grow reagents -> harvest produce
// -> craft an exotic seed -> plant it.
public class CraftingSystem : MonoBehaviour
{
    public GameObject prefab;
    public List<int> recipes;
    public RectTransform container;
    public DataPlants plants;

    [Header("UI")]
    public GameObject panel;

    private Inventory inventory;
    private readonly List<GameObject> rows = new List<GameObject>();
    private bool refreshQueued;

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
        if (plants == null && Inv != null)
        {
            plants = Inv.plants;
        }

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
            panel.SetActive(true);
            ShowRecipies();
        }
    }

    // Walking away closes the table again; nothing used to dismiss it.
    private void OnCollisionExit2D(Collision2D collision)
    {
        if (collision.collider.CompareTag("Player") && panel != null)
        {
            panel.SetActive(false);
        }
    }

    public void ShowRecipies()
    {
        if (container == null || prefab == null || plants == null)
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

        for (int i = 0; i < recipes.Count; i++)
        {
            Plant p = plants.plants.FirstOrDefault(x => x.id == recipes[i]);
            if (p == null)
            {
                Debug.LogWarning($"[Craft] recipe id {recipes[i]} has no plant data.");
                continue;
            }

            GameObject newRecipe = Instantiate(prefab, container);
            rows.Add(newRecipe);
            newRecipe.transform.GetChild(0).GetComponent<TextMeshProUGUI>().text = p.namePlant + " (seed)";

            // Ingredients read as held/needed so the player can see what is short.
            string items = "";
            bool canCraft = true;
            for (int j = 0; j < p.neccesaryItems.Count; j++)
            {
                Item need = p.neccesaryItems[j];
                Plant ingredient = plants.plants.FirstOrDefault(x => x.id == need.id);
                string name = ingredient != null ? ingredient.namePlant : need.id.ToString();

                int held = Inv != null ? Inv.Count(ItemKind.Produce, need.id) : 0;
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

            int id = p.id;
            button.onClick.AddListener(() => tryMakeItem(id));
        }
    }

    public void tryMakeItem(int id)
    {
        Plant p = plants != null ? plants.plants.FirstOrDefault(x => x.id == id) : null;
        if (p == null || Inv == null)
        {
            return;
        }

        // Check everything before spending anything, so a partial craft can
        // never eat the player's reagents.
        for (int i = 0; i < p.neccesaryItems.Count; i++)
        {
            Item need = p.neccesaryItems[i];
            if (Inv.Count(ItemKind.Produce, need.id) < need.cant)
            {
                Debug.Log($"[Craft] not enough of item {need.id} for {p.namePlant}.");
                return;
            }
        }

        for (int i = 0; i < p.neccesaryItems.Count; i++)
        {
            Item need = p.neccesaryItems[i];
            Inv.RestItem(ItemKind.Produce, need.id, need.cant);
        }

        Inv.AddItem(ItemKind.Seed, id, 1);
        Debug.Log($"[Craft] created a {p.namePlant} seed.");
    }
}
