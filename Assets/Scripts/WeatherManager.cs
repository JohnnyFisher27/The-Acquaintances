using UnityEngine;
using TMPro;

public enum WeatherType
{
    Clear,
    Heatwave,
    Rainstorm,
    Windstorm
}

// Rolls a new weather condition each day period and applies its effects
// to every farm plot. Plant resistances reduce the impact:
// Heatwave drains water faster (heatResist), Rainstorm refills water but
// stresses unprotected plants (rainResist), Windstorm stresses plants (windResist).
public class WeatherManager : MonoBehaviour
{
    public static WeatherManager Instance { get; private set; }

    [Header("Settings")]
    [Range(0f, 1f)]
    [SerializeField] private float badWeatherChance = 0.5f;
    [SerializeField] private float heatwaveDepletionMultiplier = 2.5f;
    [SerializeField] private float rainRefillPerSecond = 0.05f;
    [SerializeField] private float stormStressPerSecond = 0.05f;

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI weatherText;

    public WeatherType current { get; private set; } = WeatherType.Clear;

    private DaySystem daySystem;
    private DayPeriod lastPeriod;
    private FarmPlot[] plots;

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Start()
    {
        daySystem = FindAnyObjectByType<DaySystem>();
        plots = FindObjectsByType<FarmPlot>();

        if (daySystem != null)
        {
            lastPeriod = daySystem.currentPeriod;
        }

        Roll();
    }

    private void Update()
    {
        // New weather whenever the day period changes.
        if (daySystem != null && daySystem.currentPeriod != lastPeriod)
        {
            lastPeriod = daySystem.currentPeriod;
            Roll();
        }

        ApplyEffects();
    }

    // Heat drains water faster. Heat-resistant plants are less affected.
    public float GetDepletionMultiplier(Plant plant)
    {
        if (current != WeatherType.Heatwave || plant == null)
        {
            return 1f;
        }

        return Mathf.Lerp(heatwaveDepletionMultiplier, 1f, plant.heatResist);
    }

    private void ApplyEffects()
    {
        if (current != WeatherType.Rainstorm && current != WeatherType.Windstorm)
        {
            return;
        }

        foreach (FarmPlot plot in plots)
        {
            Plant plant = plot.CurrentPlant;
            if (plant == null)
            {
                continue;
            }

            if (current == WeatherType.Rainstorm)
            {
                plot.AddWater(rainRefillPerSecond * Time.deltaTime);
                plot.ApplyWeatherStress(stormStressPerSecond * (1f - plant.rainResist) * Time.deltaTime);
            }
            else
            {
                plot.ApplyWeatherStress(stormStressPerSecond * (1f - plant.windResist) * Time.deltaTime);
            }
        }
    }

    private void Roll()
    {
        if (Random.value >= badWeatherChance)
        {
            current = WeatherType.Clear;
        }
        else
        {
            current = (WeatherType)Random.Range(1, 4);
        }

        if (weatherText != null)
        {
            weatherText.text = current.ToString();
        }

        Debug.Log($"Weather changed: {current}");
    }
}
