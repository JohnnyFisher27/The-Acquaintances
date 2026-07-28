using UnityEngine;
using System.Collections.Generic;
using System.Collections;
[CreateAssetMenu(fileName = "BaseDataPlants", menuName = "DataPlant")]
public class DataPlants : ScriptableObject
{
    public List<Plant> plants = new List<Plant>();

    private void OnEnable()
    {
        
    }
}

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
    public int foodYield = 1;
    // NonFatal plants hurt the player when harvested (0.1 = 10% of max hunger)
    [Range(0f, 1f)] public float harvestHealthPenalty;

    // Used to generate a colored placeholder sprite when no stage sprite is set below
    public Color placeholderColor = Color.white;

    //Sprites
    public Sprite plantedSpr;
    public Sprite grownedSpr;
    public Sprite readySpr;
    public Sprite witheredSpr;

}