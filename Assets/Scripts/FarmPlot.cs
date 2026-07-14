using UnityEngine;
using UnityEngine.InputSystem;
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
    [SerializeField] private float growTime = 15f;
    [SerializeField] private int foodYield = 1;

    [Header("Crop Sprites")]
    [SerializeField] private Sprite emptySoilSprite;
    [SerializeField] private Sprite plantedSprite;
    [SerializeField] private Sprite growingSprite;
    [SerializeField] private Sprite readySprite;

    private SpriteRenderer spriteRenderer;
    public CropState state = CropState.Empty;
    public float growthTimer;
    private bool watered;

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
        spriteRenderer = GetComponent<SpriteRenderer>();
        UpdateSprite();
    }

    private void Update()
    {
        if (state != CropState.Growing)
        {
            return;
        }

        growthTimer += Time.deltaTime;

        if (growthTimer >= growTime)
        {
            state = CropState.Ready;
            UpdateSprite();
        }
    }

    public void Interact(PlayerPlanting inventory)
    {
        if (inventory == null)
        {
            return;
        }

        switch (state)
        {
            case CropState.Empty:
                Plant(inventory);
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

    private void Plant(PlayerPlanting inventory)
    {
        if (!inventory.UseSeed())
        {
            Debug.Log("You do not have any seeds.");
            return;
        }

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
            CropState.Planted => plantedSprite,
            CropState.Growing => growingSprite,
            CropState.Ready => readySprite,
            _ => emptySoilSprite
        };
    }
}