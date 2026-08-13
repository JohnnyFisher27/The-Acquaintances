using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public enum ToolType
{
    Hoe,
    Seeds,
    WateringCan,
    Scythe
}

// Switch tools with 1-4 and left click to use the selected tool on the farm
// plot the player is standing on.
[RequireComponent(typeof(PlayerPlanting))]
public class PlayerTools : MonoBehaviour
{
    public ToolType currentTool = ToolType.Hoe;

    [SerializeField] private TMPro.TextMeshProUGUI foodNumText;
    [SerializeField] private TMPro.TextMeshProUGUI seedsText;

    [SerializeField] private GameObject item1Image;
    [SerializeField] private GameObject item2Image;
    [SerializeField] private GameObject item3Image;

    [SerializeField] private Color transparentColor = new Color(1f, 1f, 1f, 0.5f);
    [SerializeField] private Color fullColor = new Color(1f, 1f, 1f, 1f);

    private PlayerPlanting planting;
    private Inventory inventory;

    private void Awake()
    {
        planting = GetComponent<PlayerPlanting>();
    }

    private void Start()
    {
        // Cached rather than re-resolved: PlayerPlanting.Inv creates a component
        // when none exists, which must not happen during teardown.
        inventory = planting.Inv;
        SelectTool(ToolType.Hoe, "Hoe");

        inventory.OnChanged += UpdateToolText;

        MakeTransparent("Hoe");
        SelectFirstOwnedSeed();
        UpdateToolText();
    }

    private void OnDestroy()
    {
        if (inventory != null)
        {
            inventory.OnChanged -= UpdateToolText;
        }
    }

    private void Update()
    {
        Keyboard kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.digit1Key.wasPressedThisFrame) SelectTool(ToolType.Hoe, "Hoe");
            if (kb.digit2Key.wasPressedThisFrame) SelectTool(ToolType.Seeds, "Seeds");
            if (kb.digit3Key.wasPressedThisFrame) SelectTool(ToolType.WateringCan, "Watering Can");
            if (kb.digit4Key.wasPressedThisFrame) SelectTool(ToolType.Scythe, "Scythe");

            // R cycles which seed type gets planted.
            if (kb.rKey.wasPressedThisFrame)
            {
                CycleSeed();
            }
        }

        Mouse mouse = Mouse.current;
        if (mouse == null || !mouse.leftButton.wasPressedThisFrame)
        {
            return;
        }

        // Clicking a crafting button must not also swing the tool underneath it.
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }

        if (planting.farmplot != null)
        {
            planting.farmplot.UseTool(currentTool, planting, inventory);
        }
    }

    private void SelectTool(ToolType tool, string toolName)
    {   
        currentTool = tool;
        MakeTransparent(toolName);
        UpdateToolText();
        Debug.Log($"Selected tool: {tool}");
    }

    // Cycles through the species the player actually holds seeds for, rather
    // than every id in the data asset.
    private void CycleSeed()
    {
        List<int> owned = inventory.IdsOf(ItemKind.Seed);
        if (owned.Count == 0)
        {
            Debug.Log("You have no seeds to select.");
            return;
        }

        int next = owned.IndexOf(planting.id) + 1;
        planting.id = owned[next % owned.Count];

        UpdateToolText();
        Debug.Log($"Selected seed type: {planting.id}");
    }

    private void SelectFirstOwnedSeed()
    {
        List<int> owned = inventory.IdsOf(ItemKind.Seed);
        if (owned.Count > 0 && !owned.Contains(planting.id))
        {
            planting.id = owned[0];
        }
    }

    private void UpdateToolText()
    {

        if (currentTool == ToolType.Seeds)
        {
            // Via the inventory so upgraded runtime stats and names are used.
            Plant plant = inventory.PlantData(planting.id);
            string name = plant != null ? plant.namePlant : planting.id.ToString();
            int held = inventory.Count(ItemKind.Seed, planting.id);

            seedsText.text = $"{name} x{held}";
        }
        else
        {
            seedsText.text = "";
        }

        foodNumText.text = $"{planting.Food}";
        
    }

    private void MakeTransparent(string toolName)
    {
        item1Image.GetComponent<Image>().color = transparentColor;
        item2Image.GetComponent<Image>().color = transparentColor;
        item3Image.GetComponent<Image>().color = transparentColor;
        
        switch (toolName)
        {
            case "Hoe":
                item1Image.GetComponent<Image>().color = fullColor;
                break;
            case "Seeds":
                item2Image.GetComponent<Image>().color = fullColor;
                break;
            case "Watering Can":
                item3Image.GetComponent<Image>().color = fullColor;
                break;
        }
        
    }
}
