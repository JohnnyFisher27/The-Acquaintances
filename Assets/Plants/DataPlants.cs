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

    public float multMorning;
    public float multAfternoon;
    public float multNight;

    //Sprites
    public Sprite plantedSpr;
    public Sprite grownedSpr;
    public Sprite readySpr;

}