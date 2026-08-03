using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;

// Produce must stay first. Recipe ingredients in BaseDataPlants were authored
// before kinds existed, so they deserialize with kind = 0 and have to read as produce.
public enum ItemKind
{
    Produce,
    Seed
}

// Single source of truth for everything the player carries. Seeds are what you
// plant, produce is what you harvest, and the alchemy table turns produce back
// into seeds. PlayerPlanting reads its seed/food counts straight off this.
public class Inventory : MonoBehaviour
{
    public List<Item> inventory = new List<Item>();

    public DataPlants plants;

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

    // The HUD and the crafting panel redraw off this instead of polling.
    public event Action OnChanged;

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
        }

        OnChanged?.Invoke();
        return true;
    }

    public Plant PlantData(int id)
    {
        return plants != null ? plants.plants.FirstOrDefault(x => x.id == id) : null;
    }

    // Category decides this, not food yield. Special plants are alchemy
    // reagents; Healthy and NonFatal are both edible, NonFatal just hurts.
    public bool IsEdible(int id)
    {
        Plant plant = PlantData(id);
        return plant != null && plant.category != PlantCategory.Special;
    }

    private Item Find(ItemKind kind, int id)
    {
        return inventory.FirstOrDefault(x => x.kind == kind && x.id == id);
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
