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


public enum TypeItem { Plant, Objects }

[System.Serializable]

public class Plant
{
    
    public string namePlant;
    public int id;
    public PlantCategory category = PlantCategory.Healthy;


    // Icon shown in the inventory panel and recipe rows.
    public Sprite spr;

    public TypeItem type;

    public float multMorning = 1f;
    public float multAfternoon = 1f;
    public float multNight = 1f;

    //Water: fraction of the meter lost per second while growing
    public float waterDepletionRate = 0.05f;

    //Weather resistances (0 = none, 1 = immune)
    [Range(0f, 1f)] public float heatResist;
    [Range(0f, 1f)] public float rainResist;
    [Range(0f, 1f)] public float windResist;

    // Seconds of growth this species needs. 0 falls back to the plot's own
    // growTime. The alchemy table's gT upgrade scales this.
    public float groundTimer;

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
    public float pretectRadis;
    public float multiplierProtect;

    public Plant()
    {

    }

    // Used by RunTimePlants to make a mutable per-run copy, so alchemy upgrades
    // change the run without writing back into the asset. Every field has to be
    // copied here or upgrades silently drop it.
    public Plant(Plant other)
    {
        namePlant = other.namePlant;
        id = other.id;
        category = other.category;

        spr = other.spr;
        type = other.type;

        multMorning = other.multMorning;
        multAfternoon = other.multAfternoon;
        multNight = other.multNight;

        waterDepletionRate = other.waterDepletionRate;

        heatResist = other.heatResist;
        rainResist = other.rainResist;
        windResist = other.windResist;

        groundTimer = other.groundTimer;

        foodYield = other.foodYield;
        seedYield = other.seedYield;
        hungerRestored = other.hungerRestored;
        eatHealthPenalty = other.eatHealthPenalty;

        placeholderColor = other.placeholderColor;

        plantedSpr = other.plantedSpr;
        grownedSpr = other.grownedSpr;
        readySpr = other.readySpr;
        witheredSpr = other.witheredSpr;

        pretectRadis = other.pretectRadis;
        multiplierProtect = other.multiplierProtect;

        neccesaryItems = new List<Item>(other.neccesaryItems);
    }
}
