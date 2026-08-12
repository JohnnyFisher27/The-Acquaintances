using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum WeatherType
{
    Clear,
    Heat,
    Rain,
    Wind
}

// Alternates a long clear spell with a randomly chosen harsh condition: 1:00 of
// clear weather to plant and water in, then 0:30 of heat, rain or wind. Half the
// original cycle, so a jam-length session sees several storms.
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
    [SerializeField] private float clearDuration = 60f;
    [SerializeField] private float harshDuration = 30f;

    [Header("Soil Damage")]
    // Storms are half as long now, so the damage has to land harder to still be
    // a threat: an unresisted crop is destroyed four ticks (12s) into a storm,
    // a half-resistant one lasts about one full storm, and only an enhanced
    // plant walks away with soil to spare.
    [SerializeField] private float damageTickInterval = 3f;
    [Range(0f, 1f)]
    [SerializeField] private float damageStep = 0.25f;
    [SerializeField] private float heatDepletionMultiplier = 2.5f;
    [SerializeField] private float rainRefillPerSecond = 0.05f;

    // Soil integrity never recovers on its own, so damage carries across storms
    // and a plot that survives two of them still dies. Left at 0 - the behaviour
    // the prototype has had all along - so turning it on is a deliberate call.
    [Range(0f, 1f)]
    [SerializeField] private float clearSoilRecoveryPerSecond = 0f;

    [Header("Wind")]
    // Added to the player's target velocity while wind blows. moveSpeed is 6,
    // so walking into a gust roughly halves your ground speed without ever
    // making the farm unwalkable.
    [SerializeField] private float windPushSpeed = 2.2f;
    [Range(0f, 1f)]
    [SerializeField] private float gustVariation = 0.4f;
    [SerializeField] private float gustFrequency = 0.35f;

    // Fired whenever the condition changes, including the initial clear spell.
    public event Action<WeatherType> OnWeatherChanged;

    public WeatherType current { get; private set; } = WeatherType.Clear;

    // During clear weather this is the storm that has already been rolled, so
    // the HUD can warn the player in advance and they can plant accordingly.
    public WeatherType Next => current == WeatherType.Clear ? pendingHarsh : WeatherType.Clear;
    public float TimeRemaining => Mathf.Max(0f, currentDuration - elapsed);

    // Which way this storm's wind blows, rolled once when wind starts. Kept
    // purely horizontal: the player, the plant lean and the streak overlay all
    // read off it, and a diagonal gust makes all three harder to parse.
    public Vector2 WindDirection { get; private set; } = Vector2.right;

    // Pulses so wind arrives in gusts rather than as a constant shove.
    public float GustStrength => 1f + Mathf.Sin(Time.time * gustFrequency * Mathf.PI * 2f) * gustVariation;

    // Weather is an outdoor system. Inside the barn the storm keeps running on
    // the plots outside, but the player neither sees nor feels any of it, so
    // everything player-facing reads this first.
    public bool IsSheltered
    {
        get
        {
            if (player == null)
            {
                player = FindAnyObjectByType<PlayerMovement>();
            }

            return player != null && player.isInBarn;
        }
    }

    // Velocity wind adds to anything standing in it. Zero outside a wind storm
    // and zero under a roof, so callers can add it unconditionally.
    public Vector2 WindVelocity => current == WeatherType.Wind && !IsSheltered
        ? WindDirection * (windPushSpeed * GustStrength)
        : Vector2.zero;

    private WeatherType pendingHarsh = WeatherType.Rain;
    private WeatherType lastHarsh = WeatherType.Clear;
    private float currentDuration;
    private float elapsed;
    private float damageTimer;
    private FarmPlot[] plots = Array.Empty<FarmPlot>();
    private PlayerMovement player;

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

    // The tutorial does have a practice plot, so it would otherwise pick up the
    // whole climate system off the check below. It is teaching the controls,
    // and a storm rolling over that is a distraction - no readout, no overlays,
    // no gusts.
    private const string TutorialSceneName = "Tutorial";

    private static void EnsureExists()
    {
        if (FindAnyObjectByType<WeatherManager>() != null)
        {
            return;
        }

        if (SceneManager.GetActiveScene().name == TutorialSceneName)
        {
            return;
        }

        // Menu scenes have no farm, and no business having weather.
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
        EnsureHud();
        EnsureVisuals();

        // Open on a clear spell so the player can plant before the first storm.
        current = WeatherType.Clear;
        currentDuration = clearDuration;
        elapsed = 0f;
        damageTimer = 0f;
        pendingHarsh = RollHarsh();

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

    // No scene wires up a readout, which left the climate system invisible: the
    // player had no way to see the current weather or what was coming. Build the
    // badge HUD when one is missing, same trick PlayerTools uses.
    private void EnsureHud()
    {
        if (GetComponent<WeatherHud>() == null && FindAnyObjectByType<WeatherHud>() == null)
        {
            gameObject.AddComponent<WeatherHud>();
        }
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

            // Fresh gust direction per storm, so wind isn't always a shove in
            // the same direction and the player has to re-read it each time.
            if (current == WeatherType.Wind)
            {
                WindDirection = UnityEngine.Random.value < 0.5f ? Vector2.left : Vector2.right;
            }
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
}
