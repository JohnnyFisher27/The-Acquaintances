using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public enum WeatherType
{
    Clear,
    Heat,
    Rain,
    Wind
}

// Alternates a long clear spell with a randomly chosen harsh condition, per the
// prototype spec: 1:30 of clear weather to plant and water in, then 1:00 of
// heat, rain or wind.
//
// While a harsh condition is active it chips every farm plot's soil integrity
// down in fixed steps until the plot is destroyed. A plant's weather resistance
// (Plant.heatResist/rainResist/windResist) reduces or, at 1, fully negates each
// step - that is what the alchemy table's enhancement upgrades buy.
//
// WeatherVisuals turns the state changes broadcast here into the on-screen
// shift; this class deliberately owns no rendering of its own beyond the HUD
// readout.
public class WeatherManager : MonoBehaviour
{
    public static WeatherManager Instance { get; private set; }

    private static readonly WeatherType[] HarshConditions =
    {
        WeatherType.Heat,
        WeatherType.Rain,
        WeatherType.Wind
    };

    [Header("Cycle")]
    [SerializeField] private float clearDuration = 90f;
    [SerializeField] private float harshDuration = 60f;

    [Header("Soil Damage")]
    [SerializeField] private float damageTickInterval = 5f;
    [Range(0f, 1f)]
    [SerializeField] private float damageStep = 0.15f;
    [SerializeField] private float heatDepletionMultiplier = 2.5f;
    [SerializeField] private float rainRefillPerSecond = 0.05f;

    // Soil integrity never recovers on its own, so damage carries across storms
    // and a plot that survives two of them still dies. Left at 0 - the behaviour
    // the prototype has had all along - so turning it on is a deliberate call.
    [Range(0f, 1f)]
    [SerializeField] private float clearSoilRecoveryPerSecond = 0f;

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI weatherText;
    [SerializeField] private TextMeshProUGUI forecastText;
    [SerializeField] private TextMeshProUGUI timerText;

    // Fired whenever the condition changes, including the initial clear spell.
    public event Action<WeatherType> OnWeatherChanged;

    public WeatherType current { get; private set; } = WeatherType.Clear;

    // During clear weather this is the storm that has already been rolled, so
    // the HUD can warn the player in advance and they can plant accordingly.
    public WeatherType Next => current == WeatherType.Clear ? pendingHarsh : WeatherType.Clear;
    public float TimeRemaining => Mathf.Max(0f, currentDuration - elapsed);

    private WeatherType pendingHarsh = WeatherType.Rain;
    private WeatherType lastHarsh = WeatherType.Clear;
    private float currentDuration;
    private float elapsed;
    private float damageTimer;
    private FarmPlot[] plots = Array.Empty<FarmPlot>();

    // No scene but SampleScene has ever contained a WeatherManager, so the whole
    // climate system was dead in WorldScene. Rather than hand-edit that scene's
    // YAML, install one on load wherever there are plots to rain on. A manager
    // placed in a scene by hand still wins, so Inspector tuning keeps working.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InstallBootstrap()
    {
        SceneManager.sceneLoaded += (scene, mode) => EnsureExists();
        EnsureExists();
    }

    private static void EnsureExists()
    {
        if (FindAnyObjectByType<WeatherManager>() != null)
        {
            return;
        }

        // Menu and tutorial scenes have no farm, and no business having weather.
        if (FindAnyObjectByType<FarmPlot>() == null)
        {
            return;
        }

        new GameObject("Weather Manager", typeof(WeatherManager));
    }

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
        RefreshPlots();
        EnsureWeatherText();
        EnsureVisuals();

        // Open on a clear spell so the player can plant before the first storm.
        current = WeatherType.Clear;
        currentDuration = clearDuration;
        elapsed = 0f;
        damageTimer = 0f;
        pendingHarsh = RollHarsh();

        UpdateForecastDisplay();
        OnWeatherChanged?.Invoke(current);
    }

    // Plots were captured once and never again, so anything spawned or enabled
    // after startup was immune to weather for the rest of the run.
    private void RefreshPlots()
    {
        plots = FindObjectsByType<FarmPlot>();

        foreach (FarmPlot plot in plots)
        {
            if (plot.GetComponent<PlotStormBar>() == null)
            {
                plot.gameObject.AddComponent<PlotStormBar>();
            }

            if (plot.GetComponent<PlotWeatherEffects>() == null)
            {
                plot.gameObject.AddComponent<PlotWeatherEffects>();
            }
        }
    }

    private void EnsureVisuals()
    {
        if (FindAnyObjectByType<WeatherVisuals>() == null)
        {
            gameObject.AddComponent<WeatherVisuals>();
        }
    }

    // No scene wires these up, which left the climate system invisible: the
    // player had no way to see the current weather or what was coming. Build a
    // readout when one is missing, same trick PlayerTools uses.
    private void EnsureWeatherText()
    {
        if (weatherText != null)
        {
            return;
        }

        Canvas canvas = HudCanvas.Get();

        var textObject = new GameObject("Weather Text", typeof(TextMeshProUGUI));
        textObject.transform.SetParent(canvas.transform, false);

        var rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 0f);
        rect.pivot = new Vector2(0f, 0f);
        rect.anchoredPosition = new Vector2(16f, 16f);
        rect.sizeDelta = new Vector2(360f, 80f);

        weatherText = textObject.GetComponent<TextMeshProUGUI>();
        weatherText.fontSize = 22f;
        weatherText.alignment = TextAlignmentOptions.BottomLeft;
        weatherText.color = Color.white;
        weatherText.raycastTarget = false;
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        if (elapsed >= currentDuration)
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
        if (current != WeatherType.Heat || plant == null)
        {
            return 1f;
        }

        return Mathf.Lerp(heatDepletionMultiplier, 1f, plant.heatResist);
    }

    // Rain tops up water levels while it's damaging soil integrity; clear
    // weather optionally lets the soil knit back together.
    private void ApplyContinuousEffects()
    {
        if (current == WeatherType.Rain)
        {
            foreach (FarmPlot plot in plots)
            {
                if (plot != null && plot.CurrentPlant != null)
                {
                    plot.AddWater(rainRefillPerSecond * Time.deltaTime);
                }
            }
            return;
        }

        if (current == WeatherType.Clear && clearSoilRecoveryPerSecond > 0f)
        {
            foreach (FarmPlot plot in plots)
            {
                if (plot != null)
                {
                    plot.RecoverSoil(clearSoilRecoveryPerSecond * Time.deltaTime);
                }
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
            if (plot == null)
            {
                continue;
            }

            Plant plant = plot.CurrentPlant;
            if (plant == null)
            {
                continue;
            }

            float resist = current switch
            {
                WeatherType.Rain => plant.rainResist,
                WeatherType.Wind => plant.windResist,
                WeatherType.Heat => plant.heatResist,
                _ => 0f
            };

            plot.ApplySoilDamage(damageStep * (1f - resist));
        }
    }

    private void Advance()
    {
        if (current == WeatherType.Clear)
        {
            current = pendingHarsh;
            currentDuration = harshDuration;
        }
        else
        {
            lastHarsh = current;
            current = WeatherType.Clear;
            currentDuration = clearDuration;
            pendingHarsh = RollHarsh();
        }

        elapsed = 0f;
        damageTimer = 0f;

        // Picks up plots that were tilled, destroyed or spawned since last time.
        RefreshPlots();

        UpdateForecastDisplay();
        OnWeatherChanged?.Invoke(current);
        Debug.Log($"[Weather] {current} for {currentDuration}s (next: {Next})");
    }

    // Random per the spec, but never the same storm twice running - back-to-back
    // repeats read as a bug to players and waste the resistance variety.
    private WeatherType RollHarsh()
    {
        WeatherType pick = HarshConditions[UnityEngine.Random.Range(0, HarshConditions.Length)];
        if (pick != lastHarsh)
        {
            return pick;
        }

        // Step to a different one rather than re-rolling in a loop.
        int index = Array.IndexOf(HarshConditions, pick);
        int offset = UnityEngine.Random.Range(1, HarshConditions.Length);
        return HarshConditions[(index + offset) % HarshConditions.Length];
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

        // Single label carries all three when the separate ones are unwired.
        if (weatherText != null && forecastText == null && timerText == null)
        {
            weatherText.text = $"Weather: {current}\nNext: {Next} in {Mathf.CeilToInt(TimeRemaining)}s";
        }
    }
}
