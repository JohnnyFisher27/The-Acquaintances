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

[System.Serializable]

public class Plant
{
    public string namePlant;
    public int id;
    public Sprite spr;

    public TypeItem type;

    public float multMorning;
    public float multAfternoon;
    public float multNight;


    //Water: fraction of the meter lost per second while growing
    public float waterDepletionRate = 0.05f;

    //Weather resistances (0 = none, 1 = immune)
    [Range(0f, 1f)] public float heatResist;
    [Range(0f, 1f)] public float rainResist;
    [Range(0f, 1f)] public float windResist;
    public float groundTimer;


    //Sprites
    public Sprite plantedSpr;
    public Sprite grownedSpr;
    public Sprite readySpr;
    public Sprite witheredSpr;

    public List<Item> neccesaryItems = new List<Item>();

}
public enum TypeItem { Plant, Objects}