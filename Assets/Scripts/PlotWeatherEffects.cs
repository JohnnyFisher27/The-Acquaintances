using UnityEngine;

// Per-plot reactions to the weather, the plant-level half of the spec's
// visuals: puddles pooling around crops in rain, plants shaking in wind, and a
// puff of smoke where a plot has just been destroyed.
//
// Reads FarmPlot's public state by polling in LateUpdate, the same way
// PlotWaterBar and PlotStormBar do, so FarmPlot itself needs no hooks. Sprites
// are generated at runtime like PlaceholderCropSprite.
[RequireComponent(typeof(FarmPlot))]
public class PlotWeatherEffects : MonoBehaviour
{
    [SerializeField] private Color puddleColor = new Color(0.45f, 0.6f, 0.85f, 0.55f);
    [SerializeField] private Color smokeColor = new Color(0.55f, 0.52f, 0.5f, 0.8f);

    // Small enough not to shift the plot's collider meaningfully, large enough
    // to read as the plant being battered.
    [SerializeField] private float shakeAmplitude = 0.045f;
    [SerializeField] private float shakeSpeed = 28f;
    [SerializeField] private float puddleFadeSpeed = 1.5f;

    private static Sprite puddleSprite;
    private static Sprite smokeSprite;

    private FarmPlot plot;
    private SpriteRenderer plotRenderer;
    private SpriteRenderer puddle;
    private ParticleSystem smoke;

    private Vector3 basePosition;
    private bool hadLivingCrop;
    private float puddleAlpha;

    private void Awake()
    {
        plot = GetComponent<FarmPlot>();
        plotRenderer = GetComponent<SpriteRenderer>();
        basePosition = transform.localPosition;

        int order = plotRenderer != null ? plotRenderer.sortingOrder : 0;

        BuildPuddle(order - 1);
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
            smoke.Play();
        }
        hadLivingCrop = living;

        UpdatePuddle(condition == WeatherType.Rain && living);
        UpdateShake(condition == WeatherType.Wind && living);
    }

    private void UpdatePuddle(bool wanted)
    {
        if (puddle == null)
        {
            return;
        }

        puddleAlpha = Mathf.MoveTowards(puddleAlpha, wanted ? 1f : 0f, puddleFadeSpeed * Time.deltaTime);

        bool visible = puddleAlpha > 0f;
        if (puddle.gameObject.activeSelf != visible)
        {
            puddle.gameObject.SetActive(visible);
        }

        Color color = puddleColor;
        color.a *= puddleAlpha;
        puddle.color = color;
    }

    // Resistant plants stand visibly firmer, so the alchemy enhancement reads
    // on screen and not just in the soil bar.
    private void UpdateShake(bool wanted)
    {
        if (!wanted)
        {
            transform.localPosition = basePosition;
            return;
        }

        Plant plant = plot.CurrentPlant;
        float resist = plant != null ? plant.windResist : 0f;
        float amplitude = shakeAmplitude * (1f - resist);

        // Offset by position so neighbouring plots don't shake in lockstep.
        float phase = basePosition.x * 7.3f + basePosition.y * 3.1f;
        float offset = Mathf.Sin(Time.time * shakeSpeed + phase) * amplitude;

        transform.localPosition = basePosition + new Vector3(offset, 0f, 0f);
    }

    private void BuildPuddle(int order)
    {
        var go = new GameObject("Puddle");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, -0.12f, 0f);
        go.transform.localScale = new Vector3(1.15f, 0.55f, 1f);

        puddle = go.AddComponent<SpriteRenderer>();
        puddle.sprite = PuddleSprite;
        puddle.sortingOrder = order;
        if (plotRenderer != null)
        {
            puddle.sortingLayerID = plotRenderer.sortingLayerID;
        }

        go.SetActive(false);
    }

    private void BuildSmoke(int order)
    {
        var go = new GameObject("Smoke");
        go.transform.SetParent(transform, false);

        smoke = go.AddComponent<ParticleSystem>();

        ParticleSystem.MainModule main = smoke.main;
        main.playOnAwake = false;
        main.loop = false;
        main.startLifetime = 1.4f;
        main.startSpeed = 0.7f;
        main.startSize = 0.4f;
        main.startColor = smokeColor;
        main.gravityModifier = -0.05f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 40;

        // One burst per Play(), which is how the destruction puff fires.
        ParticleSystem.EmissionModule emission = smoke.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 14) });

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

        smoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private static Gradient BuildFadeGradient()
    {
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        return gradient;
    }

    private static Sprite PuddleSprite
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
