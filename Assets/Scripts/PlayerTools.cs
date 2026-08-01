using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

public enum ToolType
{
    Hoe,
    Seeds,
    WateringCan,
    Scythe
}

// Switch tools with 1-4 and press E to use the selected tool
// on the farm plot the player is standing on.
[RequireComponent(typeof(PlayerPlanting))]
public class PlayerTools : MonoBehaviour
{
    public ToolType currentTool = ToolType.Hoe;

    // Plant ids in BaseDataPlants run 1..maxSeedId. Bump this when plants are added.
    [SerializeField] private int maxSeedId = 10;

    // Optional: only used to look up the selected seed's name for toolText.
    [SerializeField] private DataPlants baseData;

    private PlayerPlanting planting;

    private Inventory inventory;

    [SerializeField] private TMPro.TextMeshProUGUI toolText;

    private void Awake()
    {
        planting = GetComponent<PlayerPlanting>();
        inventory = GetComponent<Inventory>();
    }

    private void Start()
    {
        UpdateToolText();
    }

    private void Update()
    {
        var kb = Keyboard.current;
        if (kb == null)
        {
            return;
        }

        if (kb.digit1Key.wasPressedThisFrame) SelectTool(ToolType.Hoe);
        if (kb.digit2Key.wasPressedThisFrame) SelectTool(ToolType.Seeds);
        if (kb.digit3Key.wasPressedThisFrame) SelectTool(ToolType.WateringCan);
        if (kb.digit4Key.wasPressedThisFrame) SelectTool(ToolType.Scythe);

        // R cycles which seed type gets planted.
        if (kb.rKey.wasPressedThisFrame)
        {
            planting.id = planting.id % maxSeedId + 1;
            UpdateToolText();
            Debug.Log($"Selected seed type: {planting.id}");
        }

        if (kb.eKey.wasPressedThisFrame && planting.farmplot != null)
        {
            planting.farmplot.UseTool(currentTool, planting, inventory);
        }
    }

    private void SelectTool(ToolType tool)
    {
        currentTool = tool;
        UpdateToolText();
        Debug.Log($"Selected tool: {tool}");
    }

    private void UpdateToolText()
    {
        if (toolText == null)
        {
            return;
        }

        if (currentTool == ToolType.Seeds && baseData != null)
        {
            Plant plant = baseData.plants.FirstOrDefault(p => p.id == planting.id);
            string name = plant != null ? plant.namePlant : planting.id.ToString();
            toolText.text = $"Tool: Seeds - {name} (E, R to cycle)";
        }
        else
        {
            toolText.text = $"Tool: {currentTool} (E)";
        }
    }
}
