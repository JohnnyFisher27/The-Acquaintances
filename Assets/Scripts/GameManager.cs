using UnityEngine;
using System.Collections.Generic;
using System.Collections;
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [SerializeField] private DataPlants baseData;

    // Rebuilt from the asset every run in Awake, so it must not be serialized.
    [System.NonSerialized] public RunTimePlants runtimePlants;

    private void Awake()
    {
        if (Instance != null && Instance != this) 
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        DontDestroyOnLoad(gameObject);
        runtimePlants = new RunTimePlants(baseData);
    }

    
}
