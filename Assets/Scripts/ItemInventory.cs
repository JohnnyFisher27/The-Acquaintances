using UnityEngine;
using TMPro;
using System.Collections.Generic;
using System.Collections;
using UnityEngine.UI;

// One slot in the inventory panel. Seed slots are clickable to select what gets
// planted; produce slots are inert, they are food and crafting stock.
public class ItemInventory : MonoBehaviour
{
    public Image icon;
    public TextMeshProUGUI txAmount;
    public Plant myPlant;

    [SerializeField] private Image selectionHighlight;

    private ItemKind kind;

    public void SetUp(Item item, Plant p, bool isSelected)
    {
        myPlant = p;
        kind = item.kind;

        if (icon != null)
        {
            // Only four species have authored icon art, so the rest fall back to
            // the same colored placeholder the plots use.
            icon.sprite = myPlant.spr != null
                ? myPlant.spr
                : PlaceholderCropSprite.Get(myPlant.placeholderColor, FarmPlot.CropState.Ready);
            // Seeds read as a dimmer version of the plant they grow into, so a
            // seed stack and a produce stack are not the same picture.
            icon.color = kind == ItemKind.Seed ? new Color(0.75f, 0.7f, 0.5f) : Color.white;
        }

        if (txAmount != null)
        {
            txAmount.text = item.cant.ToString();
            txAmount.gameObject.SetActive(item.cant > 1);
        }

        if (selectionHighlight != null)
        {
            selectionHighlight.enabled = isSelected && kind == ItemKind.Seed;
        }
    }

    public void OnClick()
    {
        if (kind != ItemKind.Seed || myPlant == null || myPlant.type != TypeItem.Plant)
        {
            return;
        }

        Inventory inventory = FindAnyObjectByType<Inventory>();
        if (inventory != null)
        {
            inventory.UpdateSeed(myPlant.id);
        }
    }
}
