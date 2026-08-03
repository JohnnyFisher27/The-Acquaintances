using UnityEngine;
using TMPro;

public enum WeatherType
{
    Clear,
    Heatwave,
    Rainstorm,
    Windstorm
}

[System.Serializable]
public struct ForecastEntry
{
    public WeatherType type;
    public float duration;
}

// Cycles through a fixed forecast of weather conditions, each lasting its
// own time limit (Ex. Heavy Rain -> Strong Winds -> Extreme Heat -> repeat).
// While a harsh weather is active it chips every farm plot's soil integrity
// down in fixed steps until the plot is destroyed. A plant's weather
// resistance (Plant.heatResist/rainResist/windResist) reduces or, at 1,
// fully negates each step - this stands in for "alchemy enhancement" until
// the alchemy machine exists.
public class WeatherManager : MonoBehaviour
{
    public static WeatherManager Instance { get; private set; }

    // Clear spells between the storms. Without them harsh weather never let up,
    // and since soil damage accumulates at 0.15 per 5s tick, every unresisted
    // plot was guaranteed to be destroyed roughly every 35 seconds forever.
    [Header("Forecast")]
    [SerializeField]
    private ForecastEntry[] forecast =
    {
        new ForecastEntry { type = WeatherType.Clear, duration = 30f },
        new ForecastEntry { type = WeatherType.Rainstorm, duration = 25f },
        new ForecastEntry { type = WeatherType.Clear, duration = 30f },
        new ForecastEntry { type = WeatherType.Windstorm, duration = 25f },
        new ForecastEntry { type = WeatherType.Clear, duration = 30f },
        new ForecastEntry { type = WeatherType.Heatwave, duration = 25f },
    };

    [Header("Soil Damage")]
    [SerializeField] private float damageTickInterval = 5f;
    [Range(0f, 1f)]
    [SerializeField] private float damageStep = 0.15f;
    [SerializeField] private float heatwaveDepletionMultiplier = 2.5f;
    [SerializeField] private float rainRefillPerSecond = 0.05f;

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI weatherText;
    [SerializeField] private TextMeshProUGUI forecastText;
    [SerializeField] private TextMeshProUGUI timerText;

    public WeatherType current { get; private set; } = WeatherType.Clear;
    public WeatherType Next => forecast.Length == 0 ? WeatherType.Clear : forecast[(forecastIndex + 1) % forecast.Length].type;
    public float TimeRemaining => forecast.Length == 0 ? 0f : Mathf.Max(0f, forecast[forecastIndex].duration - elapsed);

    private int forecastIndex = -1;
    private float elapsed;
    private float damageTimer;
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
        plots = FindObjectsByType<FarmPlot>();
        Advance();
    }

    private void Update()
    {
        if (forecast.Length == 0)
        {
            return;
        }

        elapsed += Time.deltaTime;
        if (elapsed >= forecast[forecastIndex].duration)
        {
            Advance();
        }

        ApplyContinuousEffects();
        ApplyDamageTick();
        UpdateForecastDisplay();
    }

    // Heat drains water faster on top of the soil damage ticks. Heat-resistant plants are less affected.
    public float GetDepletionMultiplier(Plant plant)
    {
        if (current != WeatherType.Heatwave || plant == null)
        {
            return 1f;
        }

        return Mathf.Lerp(heatwaveDepletionMultiplier, 1f, plant.heatResist);
    }

    // Rain also tops up water levels while it's damaging soil integrity.
    private void ApplyContinuousEffects()
    {
        if (current != WeatherType.Rainstorm)
        {
            return;
        }

        foreach (FarmPlot plot in plots)
        {
            if (plot.CurrentPlant != null)
            {
                plot.AddWater(rainRefillPerSecond * Time.deltaTime);
            }
        }
    }

    // Every damageTickInterval seconds, harsh weather knocks a fixed 15%-style
    // step off each planted plot's soil integrity, scaled down by resistance.
    private void ApplyDamageTick()
    {
        if (current == WeatherType.Clear)
        {
            return;
        }

        damageTimer += Time.deltaTime;
        if (damageTimer < damageTickInterval)
        {
            return;
        }
        damageTimer = 0f;

        foreach (FarmPlot plot in plots)
        {
            Plant plant = plot.CurrentPlant;
            if (plant == null)
            {
                continue;
            }

            float resist = current switch
            {
                WeatherType.Rainstorm => plant.rainResist,
                WeatherType.Windstorm => plant.windResist,
                WeatherType.Heatwave => plant.heatResist,
                _ => 0f
            };

            plot.ApplySoilDamage(damageStep * (1f - resist));
        }
    }

    private void Advance()
    {
        if (forecast.Length == 0)
        {
            current = WeatherType.Clear;
            return;
        }

        forecastIndex = (forecastIndex + 1) % forecast.Length;
        elapsed = 0f;
        damageTimer = 0f;
        current = forecast[forecastIndex].type;

        if (weatherText != null)
        {
            weatherText.text = current.ToString();
        }

        UpdateForecastDisplay();
        Debug.Log($"Weather changed: {current} for {forecast[forecastIndex].duration}s");
    }

    private void UpdateForecastDisplay()
    {
        if (forecastText != null)
        {
            forecastText.text = $"Next: {Next}";
        }

        if (timerText != null)
        {
            timerText.text = Mathf.CeilToInt(TimeRemaining).ToString();
        }
    }
}
