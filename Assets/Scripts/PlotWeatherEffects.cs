using UnityEngine;

// Per-plot reactions to the weather, the plant-level half of the design doc's
// hazard visuals:
//
//   Rain  - puddles pool on plants being flooded out. Mutated plants get none.
//   Wind  - every grown plant shakes and leans downwind. Mutated ones shake
//           too, per the doc, and simply survive it.
//   Heat  - eroded soil creeps over the plot and smoke rises off the crop.
//           Mutated plants show neither.
//
// These overlays are a read-out of damage that WeatherManager is already
// dealing, not a second way to kill a crop. Soil integrity is the single thing
// that destroys a plot (FarmPlot.ApplySoilDamage -> DestroySoil -> Untilled),
// so the effects deepen as integrity drains and clear the instant the plot
// resets. Two independent kill paths would let a plot die with no warning on
// screen.
//
// Reads FarmPlot's public state by polling in LateUpdate, the same way
// PlotWaterBar and PlotStormBar do, so FarmPlot itself needs no hooks.
[RequireComponent(typeof(FarmPlot))]
public class PlotWeatherEffects : MonoBehaviour
{
    // Erosion tiles live in Resources because WeatherManager adds this
    // component at runtime, so nothing in a scene is around to wire them up.
    private const string ErosionResourcePath = "Weather/Erosion";

    [Header("Art Overrides (optional)")]
    // Tlandolphi05's puddle art goes here once it lands.
    [SerializeField] private Sprite puddleOverride;
    [SerializeField] private Sprite[] erosionOverride;

    // The artist offered either reading. Drawn over by default, which is what
    // they leaned toward ("so it can cover parts of the plants"); the plot's
    // soil and crop share one SpriteRenderer, so behind means largely hidden
    // until crops get their own transparent art.
    [SerializeField] private bool erosionBehindPlant;

    [Header("Tint")]
    [SerializeField] private Color puddleColor = new Color(0.45f, 0.6f, 0.85f, 0.55f);
    [SerializeField] private Color erosionTint = Color.white;
    [SerializeField] private Color smokeColor = new Color(0.55f, 0.52f, 0.5f, 0.8f);

    [Header("Wind")]
    // Small enough not to shift the plot's collider meaningfully, large enough
    // to read as the plant being battered.
    [SerializeField] private float shakeAmplitude = 0.045f;
    [SerializeField] private float shakeSpeed = 28f;
    // Steady bend downwind, on top of the shake, so the gust has a direction.
    [SerializeField] private float leanAmount = 0.07f;
    [SerializeField] private float leanSpeed = 4f;

    [Header("Heat")]
    [SerializeField] private float smokeRateMin = 1.5f;
    [SerializeField] private float smokeRateMax = 7f;

    [Header("Response")]
    // At or above this resistance a plant reads as "mutated for this weather"
    // and stops showing the hazard at all, per the doc. Base special plants sit
    // at 0.5 and alchemy pushes them toward 1, so the midpoint separates a
    // species that merely holds out longer from one that has been enhanced.
    [Range(0f, 1f)]
    [SerializeField] private float mutatedResistThreshold = 0.75f;

    // Effects start visible the moment harsh weather lands rather than waiting
    // for damage, so the player can see which crops are in trouble in time to
    // do something about it. Damage then drives them the rest of the way up.
    [SerializeField] private float minIntensity = 0.4f;
    [SerializeField] private float fadeSpeed = 1.5f;

    private static Sprite puddleSprite;
    private static Sprite smokeSprite;
    private static Sprite[] erosionTiles;
    private static bool erosionLoadAttempted;

    private FarmPlot plot;
    private SpriteRenderer plotRenderer;
    private SpriteRenderer puddle;
    private SpriteRenderer erosion;
    private ParticleSystem smoke;
    private ParticleSystem.EmissionModule smokeEmission;

    private Vector3 basePosition;
    private bool hadLivingCrop;
    private float puddleAlpha;
    private float erosionAlpha;
    private float lean;

    private void Awake()
    {
        plot = GetComponent<FarmPlot>();
        plotRenderer = GetComponent<SpriteRenderer>();
        basePosition = transform.localPosition;

        int order = plotRenderer != null ? plotRenderer.sortingOrder : 0;

        // Water pools under the crop; eroded soil creeps over it.
        puddle = BuildOverlay("Puddle", PuddleSprite, order - 1,
            new Vector3(0f, -0.12f, 0f), new Vector3(1.15f, 0.55f, 1f));
        erosion = BuildOverlay("Erosion", PickErosionTile(), order + (erosionBehindPlant ? -2 : 1),
            Vector3.zero, Vector3.one);

        BuildSmoke(order + 11);
    }

    private void LateUpdate()
    {
        WeatherType condition = WeatherManager.Instance != null
            ? WeatherManager.Instance.current
            : WeatherType.Clear;

        bool living = plot.HasLivingCrop;

        // Untilled is reached only by DestroySoil: harvesting and scything land
        // on Empty, withering lands on Withered. So this is weather damage.
        if (hadLivingCrop && plot.state == FarmPlot.CropState.Untilled && smoke != null)
        {
            smoke.Emit(14);
        }
        hadLivingCrop = living;

        Plant plant = plot.CurrentPlant;

        float heat = living ? Intensity(condition, WeatherType.Heat, Resist(plant, WeatherType.Heat)) : 0f;
        float rain = living ? Intensity(condition, WeatherType.Rain, Resist(plant, WeatherType.Rain)) : 0f;

        erosionAlpha = Mathf.MoveTowards(erosionAlpha, heat, fadeSpeed * Time.deltaTime);
        puddleAlpha = Mathf.MoveTowards(puddleAlpha, rain, fadeSpeed * Time.deltaTime);

        Paint(erosion, erosionTint, erosionAlpha);
        Paint(puddle, puddleColor, puddleAlpha);
        Smoke(heat);
        Wind(condition == WeatherType.Wind && living);
    }

    private static float Resist(Plant plant, WeatherType condition)
    {
        if (plant == null)
        {
            return 0f;
        }

        return Mathf.Clamp01(condition switch
        {
            WeatherType.Heat => plant.heatResist,
            WeatherType.Rain => plant.rainResist,
            WeatherType.Wind => plant.windResist,
            _ => 0f
        });
    }

    // How hard this plot should be showing the current condition. A plant
    // mutated for this weather shows nothing at all; otherwise the effect is
    // scaled by how vulnerable the species is and how close the soil is to
    // giving out.
    private float Intensity(WeatherType current, WeatherType condition, float resist)
    {
        if (current != condition || resist >= mutatedResistThreshold)
        {
            return 0f;
        }

        float damage = 1f - Mathf.Clamp01(plot.SoilIntegrityNormalized);
        return (1f - resist) * Mathf.Lerp(minIntensity, 1f, damage);
    }

    private static void Paint(SpriteRenderer renderer, Color tint, float alpha)
    {
        if (renderer == null)
        {
            return;
        }

        bool visible = alpha > 0f;
        if (renderer.gameObject.activeSelf != visible)
        {
            renderer.gameObject.SetActive(visible);
        }

        tint.a *= alpha;
        renderer.color = tint;
    }

    // Smoke rises off crops the heat is killing, and puffs once on destruction.
    private void Smoke(float heat)
    {
        if (smoke == null)
        {
            return;
        }

        smokeEmission.rateOverTime = heat > 0f ? Mathf.Lerp(smokeRateMin, smokeRateMax, heat) : 0f;
    }

    // Everything grown shakes in wind, mutated or not - the doc is explicit
    // that resistant plants still shake and simply are not destroyed.
    private void Wind(bool blowing)
    {
        float windX = WeatherManager.Instance != null ? WeatherManager.Instance.WindDirection.x : 0f;

        // Ease the bend in and out rather than snapping upright when the storm
        // ends or the crop is harvested mid-gust.
        float targetLean = blowing ? windX * leanAmount : 0f;
        lean = Mathf.MoveTowards(lean, targetLean, leanSpeed * leanAmount * Time.deltaTime);

        float offset = lean;

        if (blowing)
        {
            // Offset by position so neighbouring plots don't shake in lockstep.
            float phase = basePosition.x * 7.3f + basePosition.y * 3.1f;
            float gust = WeatherManager.Instance != null ? WeatherManager.Instance.GustStrength : 1f;
            offset += Mathf.Sin(Time.time * shakeSpeed + phase) * shakeAmplitude * gust;
        }

        transform.localPosition = Mathf.Approximately(offset, 0f)
            ? basePosition
            : basePosition + new Vector3(offset, 0f, 0f);
    }

    private SpriteRenderer BuildOverlay(string label, Sprite sprite, int order, Vector3 localPosition, Vector3 scale)
    {
        var go = new GameObject(label);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = localPosition;
        go.transform.localScale = scale;

        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = order;
        if (plotRenderer != null)
        {
            renderer.sortingLayerID = plotRenderer.sortingLayerID;
        }

        go.SetActive(false);
        return renderer;
    }

    private void BuildSmoke(int order)
    {
        var go = new GameObject("Smoke");
        go.transform.SetParent(transform, false);

        smoke = go.AddComponent<ParticleSystem>();

        ParticleSystem.MainModule main = smoke.main;
        main.playOnAwake = false;
        main.loop = true;
        main.startLifetime = 1.4f;
        main.startSpeed = 0.7f;
        main.startSize = 0.4f;
        main.startColor = smokeColor;
        main.gravityModifier = -0.05f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 60;

        // Left playing at rate 0 so heat can dial the rate up and the
        // destruction puff can Emit() into a live system.
        smokeEmission = smoke.emission;
        smokeEmission.enabled = true;
        smokeEmission.rateOverTime = 0f;

        ParticleSystem.ShapeModule shape = smoke.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.35f;

        ParticleSystem.SizeOverLifetimeModule size = smoke.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.6f));

        ParticleSystem.ColorOverLifetimeModule fade = smoke.colorOverLifetime;
        fade.enabled = true;
        fade.color = new ParticleSystem.MinMaxGradient(BuildFadeGradient());

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.material = new Material(Shader.Find("Sprites/Default")) { mainTexture = SmokeSprite.texture };
        renderer.sortingOrder = order;
        if (plotRenderer != null)
        {
            renderer.sortingLayerID = plotRenderer.sortingLayerID;
        }

        smoke.Play();
    }

    private static Gradient BuildFadeGradient()
    {
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        return gradient;
    }

    // The erosion sheet is four variants rather than an animation, so each plot
    // keeps one for its lifetime and fades it in as the soil gives out. Picking
    // by position stops a field of plots from eroding in identical lockstep.
    private Sprite PickErosionTile()
    {
        Sprite[] tiles = ErosionTiles;
        if (tiles == null || tiles.Length == 0)
        {
            return null;
        }

        Vector3 world = transform.position;
        int hash = Mathf.Abs(Mathf.RoundToInt(world.x * 31.7f + world.y * 17.3f));
        return tiles[hash % tiles.Length];
    }

    private Sprite[] ErosionTiles
    {
        get
        {
            if (erosionOverride != null && erosionOverride.Length > 0 && erosionOverride[0] != null)
            {
                return erosionOverride;
            }

            if (!erosionLoadAttempted)
            {
                erosionLoadAttempted = true;
                erosionTiles = Resources.LoadAll<Sprite>(ErosionResourcePath);

                if (erosionTiles == null || erosionTiles.Length == 0)
                {
                    Debug.LogWarning(
                        $"[Weather] No erosion tiles at Resources/{ErosionResourcePath}. " +
                        "Heat will still damage plots, but the erosion overlay will not show.");
                }
            }

            return erosionTiles;
        }
    }

    private Sprite PuddleSprite => puddleOverride != null ? puddleOverride : SharedPuddleSprite;

    private static Sprite SharedPuddleSprite
    {
        get
        {
            if (puddleSprite == null)
            {
                puddleSprite = BuildSoftCircle(64, 0.9f);
            }

            return puddleSprite;
        }
    }

    private static Sprite SmokeSprite
    {
        get
        {
            if (smokeSprite == null)
            {
                smokeSprite = BuildSoftCircle(32, 1f);
            }

            return smokeSprite;
        }
    }

    private static Sprite BuildSoftCircle(int res, float radiusFrac)
    {
        var tex = new Texture2D(res, res, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        float center = res * 0.5f;
        float radius = center * radiusFrac;

        var pixels = new Color[res * res];
        for (int y = 0; y < res; y++)
        {
            for (int x = 0; x < res; x++)
            {
                float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(center, center));
                float alpha = Mathf.Clamp01(1f - dist / radius);

                pixels[y * res + x] = new Color(1f, 1f, 1f, alpha * alpha);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();

        return Sprite.Create(tex, new Rect(0f, 0f, res, res), new Vector2(0.5f, 0.5f), res);
    }
}
