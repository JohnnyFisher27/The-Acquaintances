using UnityEngine;
using UnityEngine.Serialization;
using System.Collections.Generic;
using System.Collections;
[CreateAssetMenu(fileName = "BaseDataPlants", menuName = "DataPlant")]
public class DataPlants : ScriptableObject
{
    public List<Plant> plants = new List<Plant>();
}

// Healthy: ordinary food. Special: alchemy reagents, inedible. NonFatal: edible
// but it costs you health, which is the point of the name.
public enum PlantCategory
{
    Healthy,
    Special,
    NonFatal
}

[System.Serializable]

public class Plant
{
    public string namePlant;
    public int id;
    public PlantCategory category = PlantCategory.Healthy;

    // Reserved for an inventory/recipe icon. Nothing reads it yet; the plot
    // sprites below are what actually render.
    public Sprite spr;

    public float multMorning = 1f;
    public float multAfternoon = 1f;
    public float multNight = 1f;

    //Water: fraction of the meter lost per second while growing
    public float waterDepletionRate = 0.05f;

    //Weather resistances (0 = none, 1 = immune)
    [Range(0f, 1f)] public float heatResist;
    [Range(0f, 1f)] public float rainResist;
    [Range(0f, 1f)] public float windResist;

    //Harvest
    // How many produce items one harvest drops.
    public int foodYield = 1;

    // Seeds recovered from a harvest, like collecting seed from a real crop.
    // 0 makes a species sterile, so the only way to get more is to craft it.
    public int seedYield = 1;

    // Hunger restored per item eaten. 0 falls back to PlayerPlanting's default,
    // so species with no authored value still work.
    public float hungerRestored;

    // NonFatal plants cost health when eaten (0.1 = 10% of max hunger).
    [FormerlySerializedAs("harvestHealthPenalty")]
    [Range(0f, 1f)] public float eatHealthPenalty;

    // Used to generate a colored placeholder sprite when no stage sprite is set below
    public Color placeholderColor = Color.white;

    //Sprites
    public Sprite plantedSpr;
    public Sprite grownedSpr;
    public Sprite readySpr;
    public Sprite witheredSpr;

    public List<Item> neccesaryItems = new List<Item>();

}
