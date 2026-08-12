using UnityEngine;
using System.Linq;
using System.Collections.Generic;
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
    [SerializeField] private LayerMask layerFarmPlots;

    // Resolved per planting from Plant.groundTimer, falling back to growTime.
    private float activeGrowTime = 15f;

    [Header("Water")]
    [SerializeField] private float waterPerUse = 0.5f;
    [SerializeField] private float witherGracePeriod = 10f;

    [Header("Crop Sprites")]
    [SerializeField] private Sprite untilledSprite;
    [SerializeField] private Sprite emptySoilSprite;

    [SerializeField] private AudioClip tillingSound;
    [SerializeField] private AudioClip plantingSound;
    [SerializeField] private AudioClip wateringSound;
    [SerializeField] private AudioClip scythingSound;
    [SerializeField] private AudioClip plantGrownSound;

    [SerializeField] private Animator animator;
    //[SerializeField] private TMPro.TextMeshProUGUI growthText;

    private SpriteRenderer spriteRenderer;
    public CropState state = CropState.Untilled;
    public float growthTimer;
    private float waterLevel;
    private float dryTimer;
    private float soilIntegrity = 1f;
    private Plant currentPlant;
    private DaySystem daySystem;

    public float protectionRadius;
    private bool protective;
    private List<FarmPlot> protectionSources = new List<FarmPlot>();
    private float currentMutiplierProtection;

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
        protectionRadius = 0;
        UpdateSprite();
    }

    private void Update()
    {
        if (state != CropState.Growing || currentPlant == null)
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
                    case WeatherType.Heat:
                        effectiveGrace *= Mathf.Lerp(0.3f, 1f, Mathf.Clamp01( currentPlant.heatResist * currentMutiplierProtection));
                        break;
                    case WeatherType.Wind:
                        effectiveGrace *= Mathf.Lerp(0.3f, 1f, Mathf.Clamp01(currentPlant.windResist * currentMutiplierProtection));
                        break;
                    case WeatherType.Rain:
                        effectiveGrace *= Mathf.Lerp(1f, 2f, Mathf.Clamp01(currentPlant.rainResist * currentMutiplierProtection));
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
        //growthText.text = $"{growthTimer / growTime:P0}";

        if (growthTimer >= activeGrowTime)
        {
            SoundEffectsManager.instance.PlaySound(plantGrownSound, transform, 1f);
            state = CropState.Ready;
            if (currentPlant.pretectRadis > 0)
            {
                protective = true;
                protectionRadius = currentPlant.pretectRadis;
                ActiveProtection();
            }
            UpdateSprite();
        }
    }

    public void UseTool(ToolType tool, PlayerPlanting planting, Inventory inventory)
    {
        // A ready crop is always harvested, whatever is in hand. Scything one
        // used to throw the whole yield away.
        if (state == CropState.Ready)
        {
            Interact("Harvest", scythingSound);
            Harvest(planting);
            return;
        }

        switch (tool)
        {
            case ToolType.Hoe:
                animator.SetBool("IsInteracting", true);
                animator.SetTrigger("Till");
                SoundEffectsManager.instance.PlaySound(tillingSound, transform, 1f);
                Till();
                break;

            case ToolType.Seeds:
                if (state == CropState.Ready) {
                    animator.SetBool("IsInteracting", true);
                    animator.SetTrigger("Harvest");
                    SoundEffectsManager.instance.PlaySound(scythingSound, transform, 1f);
                    Harvest(planting);
                } else {
                    animator.SetBool("IsInteracting", true);
                    animator.SetTrigger("Plant");
                    SoundEffectsManager.instance.PlaySound(plantingSound, transform, 1f);
                    PlantSeed(planting, inventory);
                }
                break;

            case ToolType.WateringCan:
                if (state == CropState.Ready) {
                    animator.SetBool("IsInteracting", true);
                    animator.SetTrigger("Harvest");
                    SoundEffectsManager.instance.PlaySound(scythingSound, transform, 1f);
                    Harvest(planting);
                } else { 
                    animator.SetBool("IsInteracting", true);
                    animator.SetTrigger("Water");
                    SoundEffectsManager.instance.PlaySound(wateringSound, transform, 1f);
                    Water();
                }
                break;

            case ToolType.Scythe:
                Interact("Harvest", scythingSound);
                Scythe();
                break;
        }
    }

    // Animator and the sound manager are both optional in a scene, so neither
    // is allowed to take the interaction down with it.
    private void Interact(string trigger, AudioClip clip)
    {
        if (animator != null)
        {
            animator.SetBool("IsInteracting", true);
            animator.SetTrigger(trigger);
        }

        PlaySfx(clip);
    }

    private void PlaySfx(AudioClip clip)
    {
        if (clip != null && SoundEffectsManager.instance != null)
        {
            SoundEffectsManager.instance.PlaySound(clip, transform, 1f);
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

    private void PlantSeed(PlayerPlanting planting, Inventory inventory)
    {
        if (state != CropState.Empty)
        {
            Debug.Log(InteractionText);
            return;
        }

        int id = inventory.CurrentSeed;

        // Through the inventory so the runtime copy is used and alchemy
        // upgrades to grow time and resistances actually take effect.
        Plant plant = inventory.PlantData(id);
        if (plant == null)
        {
            Debug.Log($"No plant data found for id {id}.");
            return;
        }

        // Seeds are per species now, so planting spends a seed of exactly the
        // type the player has selected.
        if (!planting.UseSeed(plant.id))
        {
            Debug.Log($"You have no {plant.namePlant} seeds.");
            return;
        }

        // Species can define their own grow time; 0 keeps the plot default.
        activeGrowTime = plant.groundTimer > 0f ? plant.groundTimer : growTime;

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
        if (plant == null)
        {
            ClearPlot();
            return;
        }

        // Produce goes into the shared inventory as a stack of this species, so
        // it can be eaten or spent at the alchemy table. Reagents like Sunspot
        // yield no food but still hand over one item.
        int produce = Mathf.Max(1, plant.foodYield);
        inventory.AddProduce(plant.id, produce);

        // Seed recovered off the crop itself, the way you'd save seed from a
        // real harvest. Sterile species (seedYield 0) have to be re-crafted.
        inventory.AddSeeds(plant.id, plant.seedYield);
        DescativeProtect();

        ClearPlot();
        Debug.Log($"Harvested {produce} {plant.namePlant}, recovered {plant.seedYield} seed(s).");
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

    // Optional counterpart to ApplySoilDamage, driven by WeatherManager during
    // clear spells. Off by default, so soil damage still accumulates unless the
    // team turns recovery on.
    public void RecoverSoil(float amount)
    {
        if (!HasLivingCrop || amount <= 0f)
        {
            return;
        }

        soilIntegrity = Mathf.Min(1f, soilIntegrity + amount);
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
        public float activeGrowTime;
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
            soilIntegrity = soilIntegrity,
            activeGrowTime = activeGrowTime
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
        activeGrowTime = snapshot.activeGrowTime > 0f ? snapshot.activeGrowTime : growTime;
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
        protective = false;
        protectionRadius = 0f;
        UpdateSprite();

    }

    private void UpdateSprite()
    {
        if (spriteRenderer == null)
        {
            return;
        }

        // A crop state with no plant behind it (a bad restore, say) falls
        // through to bare soil instead of null-reffing.
        Sprite sprite = currentPlant == null
            ? (state == CropState.Untilled ? untilledSprite : emptySoilSprite)
            : state switch
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

        bool untilledWithoutArt = state == CropState.Untilled && untilledSprite == null;
        spriteRenderer.color = untilledWithoutArt ? new Color(0.62f, 0.55f, 0.45f) : Color.white;

    }
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, protectionRadius);
    }

    public void ActiveProtection() 
    {
        if (!protective) return;
        DescativeProtect();

        Collider2D[] colliders = Physics2D.OverlapCircleAll(transform.position, protectionRadius, layerFarmPlots);
        foreach (Collider2D collider in colliders) 
        {
            FarmPlot plot = collider.GetComponent<FarmPlot>();
            if (plot == null || plot == this) continue;

            SetMultiplierProtection(currentPlant.multiplierProtect);
            protectionSources.Add(plot);
        }
    }

    private void DescativeProtect() 
    {   foreach (FarmPlot item in protectionSources)
        {
            item.SetMultiplierProtection(1);
        }
        protectionSources.Clear();
    }

    private void SetMultiplierProtection(float newMultiplier) 
    {
        currentMutiplierProtection = newMultiplier;
    }

}
