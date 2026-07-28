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
    [SerializeField] private DataPlants baseData;
    [SerializeField] private float growTime = 15f;

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
    private float soilIntegrity = 1f;
    private Plant currentPlant;
    private DaySystem daySystem;

    public Plant CurrentPlant => currentPlant;
    public float WaterNormalized => waterLevel;
    public bool ShowWaterBar => state == CropState.Planted || state == CropState.Growing;
    public bool HasLivingCrop => state == CropState.Planted || state == CropState.Growing || state == CropState.Ready;

    // Soil integrity: chipped away in fixed steps by harsh weather
    // (see WeatherManager). Hits 0 and the plot is destroyed outright,
    // not just withered - it has to be re-tilled from scratch.
    public float SoilIntegrityNormalized => soilIntegrity;
    public bool ShowSoilBar => HasLivingCrop && soilIntegrity < 1f;

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

    public void UseTool(ToolType tool, PlayerPlanting inventory)
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
                    Harvest(inventory);
                } else {
                    animator.SetBool("IsInteracting", true);
                    animator.SetTrigger("Plant");
                    PlantSeed(inventory);
                }
                break;

            case ToolType.WateringCan:
                if (state == CropState.Ready) {
                    animator.SetBool("IsInteracting", true);
                    animator.SetTrigger("Harvest");
                    Harvest(inventory);
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

    private void PlantSeed(PlayerPlanting inventory)
    {
        if (state != CropState.Empty)
        {
            Debug.Log(InteractionText);
            return;
        }

        Plant plant = baseData.plants.FirstOrDefault(x => x.id == inventory.id);
        if (plant == null)
        {
            Debug.Log($"No plant data found for id {inventory.id}.");
            return;
        }

        if (!inventory.UseSeed())
        {
            Debug.Log("You do not have any seeds.");
            return;
        }

        currentPlant = plant;
        state = CropState.Planted;
        waterLevel = 0f;
        dryTimer = 0f;
        growthTimer = 0f;
        soilIntegrity = 1f;

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
        Plant plant = currentPlant;
        inventory.AddFood(plant.foodYield);

        if (plant.category == PlantCategory.NonFatal && plant.harvestHealthPenalty > 0f)
        {
            inventory.ApplyHarvestGas(plant.harvestHealthPenalty);
        }

        ClearPlot();
        Debug.Log($"Crop harvested. Received {plant.foodYield} food.");
    }

    // Rain refills every planted crop.
    public void AddWater(float amount)
    {
        if (state == CropState.Planted || state == CropState.Growing)
        {
            waterLevel = Mathf.Min(1f, waterLevel + amount);
        }
    }

    // Harsh weather chips soil integrity down in fixed steps (see
    // WeatherManager). Resistant/enhanced plants take a reduced step, and a
    // fully resistant plant takes none. Hits 0 and the plot is destroyed.
    public void ApplySoilDamage(float amount)
    {
        if (!HasLivingCrop || amount <= 0f)
        {
            return;
        }

        soilIntegrity = Mathf.Max(0f, soilIntegrity - amount);
        if (soilIntegrity <= 0f)
        {
            DestroySoil();
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
        public float soilIntegrity;
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
            soilIntegrity = soilIntegrity
        };
    }

    public void Restore(Snapshot snapshot)
    {
        state = snapshot.state;
        currentPlant = snapshot.plant;
        growthTimer = snapshot.growthTimer;
        waterLevel = snapshot.waterLevel;
        dryTimer = snapshot.dryTimer;
        soilIntegrity = snapshot.soilIntegrity;
        UpdateSprite();
    }

    private void Wither()
    {
        state = CropState.Withered;
        UpdateSprite();
        Debug.Log("A crop has withered.");
    }

    // Soil integrity ran out: unlike withering, the plot itself is ruined
    // and needs to be tilled again from scratch.
    private void DestroySoil()
    {
        currentPlant = null;
        state = CropState.Untilled;
        waterLevel = 0f;
        dryTimer = 0f;
        soilIntegrity = 1f;
        growthTimer = 0f;
        UpdateSprite();
        Debug.Log("The weather destroyed this plot. It needs to be tilled again.");
    }

    private void ClearPlot()
    {
        currentPlant = null;
        state = CropState.Empty;
        waterLevel = 0f;
        dryTimer = 0f;
        soilIntegrity = 1f;
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

        // Species without authored art yet fall back to a colored placeholder.
        if (sprite == null && currentPlant != null)
        {
            sprite = PlaceholderCropSprite.Get(currentPlant.placeholderColor, state);
        }

        if (sprite == null)
        {
            sprite = emptySoilSprite;
        }

        spriteRenderer.sprite = sprite;
    }
}
