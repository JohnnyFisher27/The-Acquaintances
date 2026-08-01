using UnityEngine;
using System.Linq;

public class FarmPlot : MonoBehaviour
{
    // Untilled is first so plots saved in the scene before this state
    // existed (serialized as 0) start out untilled.
    public enum CropState
    {
        Untilled,
        Empty,
        Planted,
        Growing,
        Ready,
        Withered
    }

    [Header("Crop Settings")]
    
    [SerializeField] private float growTime = 15f;
    [SerializeField] private int foodYield = 1;

    [Header("Water")]
    [SerializeField] private float waterPerUse = 0.5f;
    [SerializeField] private float witherGracePeriod = 10f;

    [Header("Crop Sprites")]
    [SerializeField] private Sprite untilledSprite;
    [SerializeField] private Sprite emptySoilSprite;

    [SerializeField] private Animator animator;
    [SerializeField] private TMPro.TextMeshProUGUI growthText;

    private SpriteRenderer spriteRenderer;
    public CropState state = CropState.Untilled;
    public float growthTimer;
    private float waterLevel;
    private float dryTimer;
    private float weatherStress;
    private Plant currentPlant;
    private DaySystem daySystem;

    public Plant CurrentPlant => currentPlant;
    public float WaterNormalized => waterLevel;
    public bool ShowWaterBar => state == CropState.Planted || state == CropState.Growing;
    public bool HasLivingCrop => state == CropState.Planted || state == CropState.Growing || state == CropState.Ready;

    public string InteractionText
    {
        get
        {
            return state switch
            {
                CropState.Untilled => "Till Soil (Hoe)",
                CropState.Empty => "Plant Seed",
                CropState.Planted => "Water Crop",
                CropState.Growing => "Crop is growing",
                CropState.Ready => "Harvest Crop",
                CropState.Withered => "Clear Crop (Scythe)",
                _ => ""
            };
        }
    }

    private void Awake()
    {
        daySystem = FindAnyObjectByType<DaySystem>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        UpdateSprite();
    }

    private void Update()
    {
        if (state != CropState.Growing)
        {
            return;
        }

        // Water drains while the crop grows. Weather can speed this up per plant.
        float depletion = currentPlant.waterDepletionRate;
        if (WeatherManager.Instance != null)
        {
            depletion *= WeatherManager.Instance.GetDepletionMultiplier(currentPlant);
        }
        waterLevel = Mathf.Max(0f, waterLevel - depletion * Time.deltaTime);

        // Growth stalls while dry. Stay dry too long and the crop dies.
        if (waterLevel <= 0f)
        {
            dryTimer += Time.deltaTime;
            float effectiveGrace = witherGracePeriod;

            if (WeatherManager.Instance != null && currentPlant != null) 
            {
                switch (WeatherManager.Instance.current) 
                {
                    case WeatherType.Heatwave:
                        effectiveGrace *= Mathf.Lerp(0.3f, 1f, currentPlant.heatResist);
                        break;
                    case WeatherType.Windstorm:
                        effectiveGrace *= Mathf.Lerp(0.3f, 1f, currentPlant.windResist);
                        break;
                    case WeatherType.Rainstorm:
                        effectiveGrace *= Mathf.Lerp(1f, 2f, currentPlant.rainResist);
                        break;
                }
            }

            if (dryTimer >= effectiveGrace)
            {
                Wither();
            }
            return;
        }
        dryTimer = 0f;

        // Time of day changes growth speed per plant (Ryan's day/night system).
        float periodMult = 1f;
        if (daySystem != null)
        {
            periodMult = daySystem.currentPeriod switch
            {
                DayPeriod.Morning => currentPlant.multMorning,
                DayPeriod.Afternoon => currentPlant.multAfternoon,
                DayPeriod.Night => currentPlant.multNight,
                _ => 1f
            };
        }
        growthTimer += Time.deltaTime * periodMult;
        growthText.text = $"{growthTimer / growTime:P0}";

        if (growthTimer >= growTime)
        {
            state = CropState.Ready;
            UpdateSprite();
        }
    }

    public void UseTool(ToolType tool, PlayerPlanting planting, Inventory inventory)
    {
        switch (tool)
        {
            case ToolType.Hoe:
                animator.SetBool("IsInteracting", true);
                animator.SetTrigger("Till");
                Till();
                break;

            case ToolType.Seeds:
                if (state == CropState.Ready) {
                    animator.SetBool("IsInteracting", true);
                    animator.SetTrigger("Harvest");
                    Harvest(planting);
                } else {
                    animator.SetBool("IsInteracting", true);
                    animator.SetTrigger("Plant");
                    PlantSeed(inventory.CurrentSeed, inventory);
                }
                break;

            case ToolType.WateringCan:
                if (state == CropState.Ready) {
                    animator.SetBool("IsInteracting", true);
                    animator.SetTrigger("Harvest");
                    Harvest(planting);
                } else { 
                    animator.SetBool("IsInteracting", true);
                    animator.SetTrigger("Water");
                    Water();
                }
                break;

            case ToolType.Scythe:
                Scythe();
                break;
        }
    }

    private void Till()
    {
        if (state != CropState.Untilled)
        {
            Debug.Log("This soil is already tilled.");
            return;
        }
        
        state = CropState.Empty;
        UpdateSprite();
        Debug.Log("Soil tilled. Ready for a seed.");
    }

    private void PlantSeed(int id, Inventory inventory )
    {
        if (state != CropState.Empty)
        {
            Debug.Log(InteractionText);
            return;
        }

        Plant plant = GameManager.Instance.runtimePlants.plants.FirstOrDefault(x => x.id == id);
        if (plant == null)
        {
            Debug.Log($"No plant data found for id {id}.");
            return;
        }

        bool isExisting = false;
        for (int i = 0; i < inventory.inventory.Count; i++) 
        {
            if (inventory.inventory[i].id == id) 
            {
                isExisting = true;
                break;
            }
        }

        if (!isExisting) return;

        inventory.RestItem(id, 1);
        currentPlant = plant;
        growTime = plant.groundTimer;
        state = CropState.Planted;
        waterLevel = 0f;
        dryTimer = 0f;
        growthTimer = 0f;

        UpdateSprite();
        Debug.Log("Seed planted. Water it to begin growing.");
    }

    private void Water()
    {
        if (state == CropState.Planted)
        {
            state = CropState.Growing;
            waterLevel = waterPerUse;
            dryTimer = 0f;
            UpdateSprite();
            Debug.Log("Crop watered. It is now growing.");
            return;
        }

        if (state == CropState.Growing)
        {
            waterLevel = Mathf.Min(1f, waterLevel + waterPerUse);
            dryTimer = 0f;
            Debug.Log("Crop watered.");
            return;
        }

        Debug.Log("There is nothing here to water.");
    }

    private void Scythe()
    {
        if (state == CropState.Untilled || state == CropState.Empty)
        {
            Debug.Log("There is nothing here to clear.");
            return;
        }

        ClearPlot();
        Debug.Log("Plot cleared.");
    }

    private void Harvest(PlayerPlanting inventory)
    {
        inventory.AddFood(foodYield);
        ClearPlot();
        Debug.Log($"Crop harvested. Received {foodYield} food.");
    }

    // Rain refills every planted crop.
    public void AddWater(float amount)
    {
        if (state == CropState.Planted || state == CropState.Growing)
        {
            waterLevel = Mathf.Min(1f, waterLevel + amount);
        }
    }

    // Storm damage builds up until the crop withers. Resistant plants take less.
    public void ApplyWeatherStress(float amount)
    {
        if (!HasLivingCrop)
        {
            return;
        }

        weatherStress += amount;
        if (weatherStress >= 1f)
        {
            Wither();
        }
    }

    // Snapshot of everything needed to rewind this plot to an earlier state
    // (used by the midnight checkpoint in DayManager).
    public struct Snapshot
    {
        public CropState state;
        public Plant plant;
        public float growthTimer;
        public float waterLevel;
        public float dryTimer;
        public float weatherStress;
    }

    public Snapshot Capture()
    {
        return new Snapshot
        {
            state = state,
            plant = currentPlant,
            growthTimer = growthTimer,
            waterLevel = waterLevel,
            dryTimer = dryTimer,
            weatherStress = weatherStress
        };
    }

    public void Restore(Snapshot snapshot)
    {
        state = snapshot.state;
        currentPlant = snapshot.plant;
        growthTimer = snapshot.growthTimer;
        waterLevel = snapshot.waterLevel;
        dryTimer = snapshot.dryTimer;
        weatherStress = snapshot.weatherStress;
        UpdateSprite();
    }

    private void Wither()
    {
        state = CropState.Withered;
        UpdateSprite();
        Debug.Log("A crop has withered.");
    }

    private void ClearPlot()
    {
        currentPlant = null;
        state = CropState.Empty;
        waterLevel = 0f;
        dryTimer = 0f;
        weatherStress = 0f;
        growthTimer = 0f;
        UpdateSprite();
    }

    private void UpdateSprite()
    {
        if (spriteRenderer == null)
        {
            return;
        }

        Sprite sprite = state switch
        {
            CropState.Untilled => untilledSprite,
            CropState.Empty => emptySoilSprite,
            CropState.Planted => currentPlant.plantedSpr,
            CropState.Growing => currentPlant.grownedSpr,
            CropState.Ready => currentPlant.readySpr,
            CropState.Withered => currentPlant.witheredSpr,
            _ => emptySoilSprite
        };

        if (sprite == null)
        {
            sprite = emptySoilSprite;
        }

        spriteRenderer.sprite = sprite;
    }
}
