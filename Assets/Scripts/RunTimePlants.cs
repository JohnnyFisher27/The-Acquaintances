using UnityEngine;
using System.Collections.Generic;

// Per-run copies of the plant data. Alchemy upgrades write in here so they last
// the run without ever modifying the BaseDataPlants asset on disk.
//
// Deliberately NOT a MonoBehaviour: GameManager builds this with `new`, and
// Unity reports a MonoBehaviour created that way as null through its overloaded
// null operator. Every `runtimePlants != null` check therefore failed, so
// upgrades never applied and plant lookups always fell back to the asset.
[System.Serializable]
public class RunTimePlants
{
    public List<Plant> plants = new();

    public RunTimePlants(DataPlants baseData)
    {
        if (baseData == null)
        {
            Debug.LogWarning("[RunTimePlants] no base data assigned.");
            return;
        }

        foreach (Plant plant in baseData.plants)
        {
            plants.Add(new Plant(plant));
        }
    }
}
