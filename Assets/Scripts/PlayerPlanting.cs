using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

// The player's view onto the shared Inventory. Seeds and food used to be two
// loose ints here; they are now stacks in Inventory so that what you harvest is
// the same thing the alchemy table crafts with.
public class PlayerPlanting: MonoBehaviour
{
    [SerializeField] private InputActionAsset inputActions;

    [Header("Food")]
    [SerializeField] private float hungerRestoredPerFood = 250f;

    [SerializeField] private TextMeshProUGUI foodCountText;
    private int currentFood = 0;

    private PlayerHunger playerHunger;
    private Inventory inv;

    // Selection lives on Inventory so the panel and R-cycling agree. Kept under
    // the old name because FarmPlot and PlayerTools read it.
    public int id
    {
        get => Inv.CurrentSeed;
        set => Inv.UpdateSeed(value);
    }

    // Read-only views kept so DayManager's lose condition and the HUD still work.
    public int Seeds => Inv.TotalOf(ItemKind.Seed);
    public int Food => EdibleProduce().Sum(x => x.cant);

    public Inventory Inv
    {
        get
        {
            if (inv == null)
            {
                inv = FindAnyObjectByType<Inventory>();
                if (inv == null)
                {
                    // Nothing in the scene: keep the game playable rather than
                    // null-reffing on the first planting.
                    inv = gameObject.AddComponent<Inventory>();
                }
            }
            return inv;
        }
    }

    private void Awake()
    {
        playerHunger = GetComponent<PlayerHunger>();
    }

    // Tool use (left click) lives in PlayerTools now. Eating stays here.
    public void Update()
    {
        Keyboard kb = Keyboard.current;
        if (kb != null && kb.qKey.wasPressedThisFrame)
        {
            EatFood(playerHunger);
        }
    }

    public FarmPlot farmplot;

    // Inventory snapshot for the midnight checkpoint in DayManager.
    public struct Snapshot
    {
        public int id;
        public List<Item> items;
    }

    public Snapshot Capture()
    {
        return new Snapshot { id = id, items = Inv.Capture() };
    }

    public void Restore(Snapshot snapshot)
    {
        Inv.Restore(snapshot.items);
        Inv.UpdateSeed(snapshot.id);
    }

    // Spends one seed of the given species. False means the player had none.
    public bool UseSeed(int plantId)
    {
        return Inv.RestItem(ItemKind.Seed, plantId, 1);
    }

    public void AddSeeds(int plantId, int amount)
    {
        Inv.AddItem(ItemKind.Seed, plantId, amount);
    }

    public void AddProduce(int plantId, int amount)
    {
        currentFood++;
        foodCountText.text = currentFood.ToString();
        Inv.AddItem(ItemKind.Produce, plantId, amount);
    }

    // Eats one produce, preferring the species currently selected so the player
    // can pick what to burn through. Alchemy reagents are skipped.
    public bool EatFood(PlayerHunger hunger)
    {
        if (hunger == null)
        {
            return false;
        }

        List<Item> edible = EdibleProduce();
        
        if (edible.Count == 0)
        {
            Debug.Log("You have nothing edible to eat.");
            return false;
        }

        Item chosen = edible.FirstOrDefault(x => x.id == id) ?? edible[0];
        if (!Inv.RestItem(ItemKind.Produce, chosen.id, 1))
        {
            return false;
        }

        Plant plant = Inv.PlantData(chosen.id);
        string name = plant != null ? plant.namePlant : chosen.id.ToString();

        // Species can carry their own nutrition; 0 means "use the default".
        float restored = plant != null && plant.hungerRestored > 0f
            ? plant.hungerRestored
            : hungerRestoredPerFood;

        hunger.Eat(restored);

        // NonFatal plants are edible but toxic. The cost lands here, on eating,
        // rather than on harvesting - picking a plant was never what hurt you.
        if (plant != null && plant.eatHealthPenalty > 0f)
        {
            hunger.Drain(hunger.Max * plant.eatHealthPenalty);
            Debug.Log($"Ate {name}. It restored {restored:F0} but made you ill.");
        }
        else
        {
            Debug.Log($"Ate {name}. Restored {restored:F0} hunger.");
        }

        currentFood--;
        return true;
    }

    private List<Item> EdibleProduce()
    {
        return Inv.inventory
            .Where(x => x.kind == ItemKind.Produce && x.cant > 0 && Inv.IsEdible(x.id))
            .ToList();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("FarmPlot")&& farmplot == null)
        {
            FarmPlot currentFarmplot = other.GetComponent<FarmPlot>();

            if (farmplot == null && currentFarmplot != farmplot)
            {
                Debug.Log($"current collision is:{other.name}");
                farmplot = currentFarmplot;
            }
        }
    }
    private void OnTriggerStay2D(Collider2D collision)
    {
        if (collision.CompareTag("FarmPlot"))
        {
            FarmPlot currentFarmPlot = collision.GetComponent<FarmPlot>();
            if (currentFarmPlot != null && currentFarmPlot != farmplot)
            {
                Debug.Log($"Current collision is: {collision.name}");
                farmplot = currentFarmPlot;
            }
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("FarmPlot"))
        {
            farmplot = null;
        }
    }
}
