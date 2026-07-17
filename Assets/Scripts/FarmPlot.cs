using UnityEngine;
using UnityEngine.InputSystem;
using System.Linq;
public class FarmPlot : MonoBehaviour
{
    public enum CropState
    {
        Empty,
        Planted,
        Growing,
        Ready
    }

    [Header("Crop Settings")]
    [SerializeField] private DataPlants baseData;
    [SerializeField] private float growTime = 15f;
    [SerializeField] private int foodYield = 1;

    [Header("Crop Sprites")]
    [SerializeField] private Sprite emptySoilSprite;
    

    private SpriteRenderer spriteRenderer;
    public CropState state = CropState.Empty;
    public float growthTimer;
    private bool watered;
    private Plant currentPlant;
    private DaySystem daySystem;
    public string InteractionText
    {
        get
        {
            return state switch
            {
                CropState.Empty => "Plant Seed",
                CropState.Planted when !watered => "Water Crop",
                CropState.Planted => "Crop is watered",
                CropState.Growing => "Crop is growing",
                CropState.Ready => "Harvest Crop",
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

        growthTimer += Time.deltaTime * daySystem.currentPeriod switch
        {
            DayPeriod.Morning => currentPlant.multMorning,
            DayPeriod.Afternoon => currentPlant.multAfternoon,
            DayPeriod.Night => currentPlant.multNight,
            _ => 1


        };

        if (growthTimer >= growTime)
        {
            state = CropState.Ready;
            UpdateSprite();
        }
    }

    public void Interact(PlayerPlanting inventory)
    {
        /*if (inventory == null)
        {
            return;
        }*/

        switch (state)
        {
            case CropState.Empty:
                Plant(inventory.id);
                break;

            case CropState.Planted:
                Water();
                break;

            case CropState.Growing:
                Debug.Log("This crop is still growing.");
                break;

            case CropState.Ready:
                Harvest(inventory);
                break;
        }
    }

    private void Plant(int id)
    {
        /* if (!inventory.UseSeed())
         {
             Debug.Log("You do not have any seeds.");
             return;
         }*/

        currentPlant = baseData.plants.FirstOrDefault(x => x.id == id);

        state = CropState.Planted;
        watered = false;
        growthTimer = 0f;

        UpdateSprite();
        Debug.Log("Seed planted. Water it to begin growing.");
    }

    private void Water()
    {
        if (watered)
        {
            Debug.Log("This crop has already been watered.");
            return;
        }

        watered = true;
        state = CropState.Growing;
        growthTimer = 0f;

        UpdateSprite();
        Debug.Log("Crop watered. It is now growing.");
    }

    private void Harvest(PlayerPlanting inventory)
    {
        inventory.AddFood(foodYield);

        state = CropState.Empty;
        watered = false;
        growthTimer = 0f;

        UpdateSprite();
        Debug.Log($"Crop harvested. Received {foodYield} food.");
    }

    private void UpdateSprite()
    {
        
        if (spriteRenderer == null)
        {
            return;
        }

        spriteRenderer.sprite = state switch
        {
            CropState.Empty => emptySoilSprite,
            CropState.Planted => currentPlant.plantedSpr,
            CropState.Growing => currentPlant.grownedSpr,
            CropState.Ready => currentPlant.readySpr,
            _ => emptySoilSprite
        };
    }
}