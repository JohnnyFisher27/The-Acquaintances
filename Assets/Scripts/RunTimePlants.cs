using UnityEngine;
using System.Collections.Generic;
public class RunTimePlants : MonoBehaviour
{
    public List<Plant> plants = new();

    public RunTimePlants(DataPlants baseData) 
    {
        foreach (Plant plant in baseData.plants) 
        {
            plants.Add(new Plant(plant));
        }
    }
}
