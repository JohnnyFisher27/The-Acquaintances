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

    // Optional: only used to look up the selected seed's name for toolText.
    [SerializeField] private DataPlants baseData;

    [SerializeField] private TMPro.TextMeshProUGUI toolText;

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

        if (baseData == null)
        {
            baseData = inventory.plants;
        }

        inventory.OnChanged += UpdateToolText;

        EnsureToolText();
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
            if (kb.digit1Key.wasPressedThisFrame) SelectTool(ToolType.Hoe);
            if (kb.digit2Key.wasPressedThisFrame) SelectTool(ToolType.Seeds);
            if (kb.digit3Key.wasPressedThisFrame) SelectTool(ToolType.WateringCan);
            if (kb.digit4Key.wasPressedThisFrame) SelectTool(ToolType.Scythe);

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
            planting.farmplot.UseTool(currentTool, planting);
        }
    }

    private void SelectTool(ToolType tool)
    {
        currentTool = tool;
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
        if (toolText == null)
        {
            return;
        }

        string line;
        if (currentTool == ToolType.Seeds)
        {
            Plant plant = baseData != null ? baseData.plants.Find(p => p.id == planting.id) : null;
            string name = plant != null ? plant.namePlant : planting.id.ToString();
            int held = inventory.Count(ItemKind.Seed, planting.id);
            line = $"Tool: Seeds - {name} x{held} (LMB, R to cycle)";
        }
        else
        {
            line = $"Tool: {currentTool} (LMB)";
        }

        toolText.text = $"{line}\nSeeds: {planting.Seeds}   Food: {planting.Food} (Q to eat)";
    }

    // The scene ships with no tool label wired up, which left the player with no
    // read on their tool or their stock. Build a minimal one when it is missing.
    private void EnsureToolText()
    {
        if (toolText != null)
        {
            return;
        }

        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null)
        {
            var canvasObject = new GameObject("HUD Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        }

        var textObject = new GameObject("Tool Text", typeof(TextMeshProUGUI));
        textObject.transform.SetParent(canvas.transform, false);

        var rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(16f, -16f);
        rect.sizeDelta = new Vector2(560f, 80f);

        toolText = textObject.GetComponent<TextMeshProUGUI>();
        toolText.fontSize = 22f;
        toolText.color = Color.white;
        toolText.raycastTarget = false;
    }
}
