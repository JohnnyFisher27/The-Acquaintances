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
    [SerializeField] private int maxSeedId = 3;

    private PlayerPlanting planting;

    private void Awake()
    {
        planting = GetComponent<PlayerPlanting>();
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

        // R cycles which seed type gets planted (3 plant types so far).
        if (kb.rKey.wasPressedThisFrame)
        {
            planting.id = planting.id % maxSeedId + 1;
            Debug.Log($"Selected seed type: {planting.id}");
        }

        if (kb.eKey.wasPressedThisFrame && planting.farmplot != null)
        {
            planting.farmplot.UseTool(currentTool, planting);
        }
    }

    private void SelectTool(ToolType tool)
    {
        currentTool = tool;
        Debug.Log($"Selected tool: {tool}");
    }
}
