using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;
using UnityEngine.InputSystem;

// Produce must stay first. Recipe ingredients in BaseDataPlants were authored
// before kinds existed, so they deserialize with kind = 0 and have to read as produce.
public enum ItemKind
{
    Produce,
    Seed
}

// Single source of truth for everything the player carries, and the panel that
// shows it. Seeds are what you plant, produce is what you harvest, and the
// alchemy table turns produce back into seeds.
public class Inventory : MonoBehaviour
{
    public List<Item> inventory = new List<Item>();

    // Fallback plant data. Lookups prefer GameManager's runtime copies so
    // alchemy upgrades are visible; this is what's used before one exists.
    public DataPlants plants;

    [Header("UI")]
    public RectTransform container;
    public GameObject panelInventory;
    public GameObject prefabItem;

    [Header("Starting Items")]
    [SerializeField]
    private List<Item> startingItems = new List<Item>
    {
        new Item(ItemKind.Seed, 1, 2),
        new Item(ItemKind.Seed, 2, 2),
        new Item(ItemKind.Seed, 3, 2),
        new Item(ItemKind.Seed, 8, 2),
        new Item(ItemKind.Seed, 9, 2)
    };

    // Species selected for planting. Set by clicking a seed slot in the panel
    // or by cycling with R in PlayerTools.
    [SerializeField] private int currentSeed = 1;
    public int CurrentSeed => currentSeed;

    // The HUD, the panel and the crafting rows redraw off this instead of polling.
    public event Action OnChanged;

    public void UpdateSeed(int id)
    {
        currentSeed = id;
        OnChanged?.Invoke();
    }

    private void Awake()
    {
        if (inventory == null)
        {
            inventory = new List<Item>();
        }

        for (int i = 0; i < startingItems.Count; i++)
        {
            AddItem(startingItems[i].kind, startingItems[i].id, startingItems[i].cant);
        }
    }

    private void Start()
    {
        if (panelInventory != null)
        {
            panelInventory.SetActive(false);
        }

        OnChanged += RefreshPanel;
        EnsureValidSeed();
    }

    private void OnDestroy()
    {
        OnChanged -= RefreshPanel;
    }

    private void Update()
    {
        Keyboard kb = Keyboard.current;
        if (kb != null && kb.iKey.wasPressedThisFrame)
        {
            TurnPanel();
        }
    }

    public int Count(ItemKind kind, int id)
    {
        Item item = Find(kind, id);
        return item != null ? item.cant : 0;
    }

    public int TotalOf(ItemKind kind)
    {
        int total = 0;
        for (int i = 0; i < inventory.Count; i++)
        {
            if (inventory[i].kind == kind)
            {
                total += inventory[i].cant;
            }
        }
        return total;
    }

    // Species the player holds at least one of, ascending. Drives seed cycling.
    public List<int> IdsOf(ItemKind kind)
    {
        return inventory.Where(x => x.kind == kind && x.cant > 0)
                        .Select(x => x.id)
                        .Distinct()
                        .OrderBy(x => x)
                        .ToList();
    }

    public void AddItem(ItemKind kind, int id, int cant)
    {
        if (cant <= 0)
        {
            return;
        }

        Item existing = Find(kind, id);
        if (existing != null)
        {
            existing.cant += cant;
        }
        else
        {
            inventory.Add(new Item(kind, id, cant));
        }

        OnChanged?.Invoke();
    }

    // All-or-nothing: takes nothing and returns false if the player is short, so
    // a failed craft or planting can never half-spend the ingredients.
    public bool RestItem(ItemKind kind, int id, int cant)
    {
        if (cant <= 0)
        {
            return true;
        }

        Item existing = Find(kind, id);
        if (existing == null || existing.cant < cant)
        {
            return false;
        }

        existing.cant -= cant;
        if (existing.cant <= 0)
        {
            inventory.Remove(existing);
            if (kind == ItemKind.Seed && currentSeed == id)
            {
                EnsureValidSeed();
            }
        }

        OnChanged?.Invoke();
        return true;
    }

    // Prefer the runtime copies so alchemy upgrades show up; fall back to the
    // asset when no GameManager is in the scene.
    public Plant PlantData(int id)
    {
        if (GameManager.Instance != null && GameManager.Instance.runtimePlants != null)
        {
            Plant runtime = GameManager.Instance.runtimePlants.plants.FirstOrDefault(x => x.id == id);
            if (runtime != null)
            {
                return runtime;
            }
        }

        return plants != null ? plants.plants.FirstOrDefault(x => x.id == id) : null;
    }

    // Category decides this, not food yield. Special plants are alchemy
    // reagents; Healthy and NonFatal are both edible, NonFatal just hurts.
    public bool IsEdible(int id)
    {
        Plant plant = PlantData(id);
        return plant != null && plant.category != PlantCategory.Special;
    }

    private void EnsureValidSeed()
    {
        List<int> owned = IdsOf(ItemKind.Seed);
        if (owned.Count > 0 && !owned.Contains(currentSeed))
        {
            currentSeed = owned[0];
        }
    }

    private Item Find(ItemKind kind, int id)
    {
        return inventory.FirstOrDefault(x => x.kind == kind && x.id == id);
    }

    private void TurnPanel()
    {
        if (panelInventory == null)
        {
            return;
        }

        panelInventory.SetActive(!panelInventory.activeInHierarchy);
        RefreshPanel();
    }

    private void RefreshPanel()
    {
        if (panelInventory != null && panelInventory.activeInHierarchy)
        {
            showItems();
        }
    }

    public void showItems()
    {
        if (container == null || prefabItem == null)
        {
            return;
        }

        foreach (Transform item in container)
        {
            Destroy(item.gameObject);
        }

        for (int i = 0; i < inventory.Count; i++)
        {
            Plant plant = PlantData(inventory[i].id);
            if (plant == null)
            {
                continue;
            }

            GameObject newItem = Instantiate(prefabItem, container);
            newItem.GetComponent<ItemInventory>().SetUp(inventory[i], plant, inventory[i].id == currentSeed);
        }
    }

    // Deep copies, for the midnight checkpoint in DayManager.
    public List<Item> Capture()
    {
        return inventory.Select(x => new Item(x.kind, x.id, x.cant)).ToList();
    }

    public void Restore(List<Item> snapshot)
    {
        inventory = snapshot != null
            ? snapshot.Select(x => new Item(x.kind, x.id, x.cant)).ToList()
            : new List<Item>();

        EnsureValidSeed();
        OnChanged?.Invoke();
    }
}


[Serializable]
public class Item
{
    public ItemKind kind;
    public int id;
    public int cant;

    public Item() { }

    public Item(int id, int cant) : this(ItemKind.Produce, id, cant) { }

    public Item(ItemKind kind, int id, int cant)
    {
        this.kind = kind;
        this.id = id;
        this.cant = cant;
    }
}
