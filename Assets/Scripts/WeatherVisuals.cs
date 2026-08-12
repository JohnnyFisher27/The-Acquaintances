using UnityEngine;
using UnityEngine.Rendering.Universal;

// Turns WeatherManager's state changes into the on-screen shift the prototype
// spec asks for: a bright warm wash in heat, raindrops in rain, curved lines
// streaking across in wind.
//
// Everything here is generated at runtime - textures, materials and particle
// systems - so the system is playable with no art in the project, exactly the
// way PlaceholderCropSprite and PlotStormBar stand in for missing sprites. Drop
// a prefab into one of the override slots and it replaces the placeholder with
// no code change.
[RequireComponent(typeof(WeatherManager))]
public class WeatherVisuals : MonoBehaviour
{
    [Header("Light Tint")]
    [SerializeField] private Color clearTint = Color.white;
    [SerializeField] private Color heatTint = new Color(1f, 0.85f, 0.6f);
    [SerializeField] private Color rainTint = new Color(0.65f, 0.72f, 0.9f);
    [SerializeField] private Color windTint = new Color(0.85f, 0.83f, 0.75f);

    [SerializeField] private float clearIntensity = 1f;
    [SerializeField] private float heatIntensity = 1.35f;
    [SerializeField] private float rainIntensity = 0.7f;
    [SerializeField] private float windIntensity = 0.9f;

    // Weather rolling in over a beat and a half reads as weather. Snapping
    // instantly reads as a bug.
    [SerializeField] private float transitionDuration = 1.5f;

    [Header("Heat Glare")]
    [SerializeField] private Color glareColor = new Color(1f, 0.94f, 0.72f, 0.5f);
    // Fraction of the view the glare reaches down from the top edge.
    [SerializeField] private float glareCoverage = 0.7f;

    [Header("Overlay Overrides (optional)")]
    [SerializeField] private ParticleSystem heatOverride;
    [SerializeField] private ParticleSystem rainOverride;
    [SerializeField] private ParticleSystem windOverride;

    private WeatherManager weather;
    private Light2D globalLight;
    private Camera view;

    private ParticleSystem heatOverlay;
    private ParticleSystem rainOverlay;
    private ParticleSystem windOverlay;

    // The doc's "bright light covers the screen from the top" during heat.
    private SpriteRenderer heatGlare;
    private float glareAlpha;

    private Color targetTint;
    private float targetIntensity;
    private WeatherType applied = WeatherType.Clear;

    // Cached view extents, needed again when wind is re-aimed on each storm.
    private float viewWidth;
    private float viewHeight;

    private void Awake()
    {
        weather = GetComponent<WeatherManager>();
        targetTint = clearTint;
        targetIntensity = clearIntensity;
    }

    private void Start()
    {
        globalLight = FindGlobalLight();
        view = Camera.main != null ? Camera.main : FindAnyObjectByType<Camera>();

        // Cover whatever the camera can actually see, so no effect reveals its
        // edges on a wide monitor. Measured here rather than inside Configure
        // so the glare still sizes correctly when every overlay is overridden.
        if (view != null)
        {
            viewHeight = view.orthographic ? view.orthographicSize * 2f : 10f;
            viewWidth = viewHeight * view.aspect;
        }

        heatOverlay = BuildOverlay(WeatherType.Heat, heatOverride);
        rainOverlay = BuildOverlay(WeatherType.Rain, rainOverride);
        windOverlay = BuildOverlay(WeatherType.Wind, windOverride);
        heatGlare = BuildHeatGlare();

        applied = Shown;
        Apply(applied);
    }

    // What the player should currently be seeing. Under the barn roof that is
    // always clear skies, however hard it is blowing on the farm outside.
    private WeatherType Shown => weather.IsSheltered ? WeatherType.Clear : weather.current;

    // Polled rather than driven off OnWeatherChanged, because stepping in or
    // out of the barn turns the overlays over without the weather changing.
    private void Refresh()
    {
        WeatherType shown = Shown;
        if (shown == applied)
        {
            return;
        }

        applied = shown;
        Apply(shown);
    }

    // A scene can hold several Light2Ds; only the global one washes the whole
    // farm, which is what the spec's screen-wide overlays describe.
    private static Light2D FindGlobalLight()
    {
        foreach (Light2D light in FindObjectsByType<Light2D>())
        {
            if (light.lightType == Light2D.LightType.Global)
            {
                return light;
            }
        }

        return null;
    }

    private void Apply(WeatherType condition)
    {
        switch (condition)
        {
            case WeatherType.Heat:
                targetTint = heatTint;
                targetIntensity = heatIntensity;
                break;
            case WeatherType.Rain:
                targetTint = rainTint;
                targetIntensity = rainIntensity;
                break;
            case WeatherType.Wind:
                targetTint = windTint;
                targetIntensity = windIntensity;
                break;
            default:
                targetTint = clearTint;
                targetIntensity = clearIntensity;
                break;
        }

        if (condition == WeatherType.Wind)
        {
            AimWind();
        }

        SetEmitting(heatOverlay, condition == WeatherType.Heat);
        SetEmitting(rainOverlay, condition == WeatherType.Rain);
        SetEmitting(windOverlay, condition == WeatherType.Wind);
    }

    // Move the emitter to the upwind edge and turn it around, so the streaks
    // agree with the direction the player is being shoved. Stretch render mode
    // aligns the streak to velocity, so the sprites flip on their own.
    private void AimWind()
    {
        if (windOverlay == null || windOverride != null)
        {
            return;
        }

        float sign = weather.WindDirection.x < 0f ? -1f : 1f;

        ParticleSystem.ShapeModule shape = windOverlay.shape;
        shape.position = new Vector3(-sign * viewWidth * 0.7f, 0f, 0f);
        shape.rotation = new Vector3(0f, 90f * sign, 0f);
    }

    // Stop emitting rather than clearing, so particles already on screen finish
    // their fall instead of vanishing the instant the weather turns.
    private static void SetEmitting(ParticleSystem system, bool on)
    {
        if (system == null)
        {
            return;
        }

        if (on)
        {
            system.Play();
        }
        else
        {
            system.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    private void Update()
    {
        Refresh();

        if (globalLight == null)
        {
            return;
        }

        float step = transitionDuration > 0f ? Time.deltaTime / transitionDuration : 1f;

        globalLight.color = Color.Lerp(globalLight.color, targetTint, step);
        globalLight.intensity = Mathf.Lerp(globalLight.intensity, targetIntensity, step);
    }

    private void LateUpdate()
    {
        if (heatGlare == null)
        {
            return;
        }

        float target = applied == WeatherType.Heat ? 1f : 0f;
        float step = transitionDuration > 0f ? Time.deltaTime / transitionDuration : 1f;
        glareAlpha = Mathf.MoveTowards(glareAlpha, target, step);

        bool visible = glareAlpha > 0f;
        if (heatGlare.gameObject.activeSelf != visible)
        {
            heatGlare.gameObject.SetActive(visible);
        }

        Color color = glareColor;
        color.a *= glareAlpha;
        heatGlare.color = color;
    }

    // ------------------------------------------------------------------
    // Runtime-built overlays
    // ------------------------------------------------------------------

    private ParticleSystem BuildOverlay(WeatherType condition, ParticleSystem overridePrefab)
    {
        if (view == null)
        {
            return null;
        }

        ParticleSystem system;
        if (overridePrefab != null)
        {
            system = Instantiate(overridePrefab, view.transform);
        }
        else
        {
            var host = new GameObject($"{condition} Overlay");
            host.transform.SetParent(view.transform, false);
            system = host.AddComponent<ParticleSystem>();
            Configure(system, condition);
        }

        // Sit in front of the camera; sorting order keeps it over the tilemap.
        system.transform.localPosition = new Vector3(0f, 0f, 1f);
        system.transform.localRotation = Quaternion.identity;

        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        return system;
    }

    // A warm wash hanging off the top edge of the view, brightest at the top
    // and falling away downward. The global light already warms the whole farm
    // during heat; this is the directional glare sitting on top of it.
    private SpriteRenderer BuildHeatGlare()
    {
        if (view == null || viewHeight <= 0f)
        {
            return null;
        }

        float coverage = Mathf.Clamp01(glareCoverage);
        float band = viewHeight * coverage;

        var go = new GameObject("Heat Glare");
        go.transform.SetParent(view.transform, false);

        // Top edge of the band pinned to the top edge of the view.
        go.transform.localPosition = new Vector3(0f, viewHeight * 0.5f - band * 0.5f, 1f);
        go.transform.localScale = new Vector3(viewWidth * 1.05f, band, 1f);

        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = BuildDownwardFade(64);

        // Under the particle overlays, over everything in the world.
        renderer.sortingOrder = 31999;
        renderer.color = new Color(glareColor.r, glareColor.g, glareColor.b, 0f);

        go.SetActive(false);
        return renderer;
    }

    // One world unit square so the caller's localScale sets the size directly.
    private static Sprite BuildDownwardFade(int res)
    {
        var tex = new Texture2D(res, res, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        var pixels = new Color[res * res];
        for (int y = 0; y < res; y++)
        {
            // Texture row 0 is the bottom, so the top of the band is y = res-1.
            float t = (y + 0.5f) / res;

            // Squared so the falloff hangs near the top instead of washing the
            // whole band evenly.
            float alpha = t * t;

            for (int x = 0; x < res; x++)
            {
                pixels[y * res + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();

        return Sprite.Create(tex, new Rect(0f, 0f, res, res), new Vector2(0.5f, 0.5f), res);
    }

    private void Configure(ParticleSystem system, WeatherType condition)
    {
        float height = viewHeight;
        float width = viewWidth;

        ParticleSystem.MainModule main = system.main;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.playOnAwake = false;
        main.loop = true;
        main.gravityModifier = 0f;

        ParticleSystem.ShapeModule shape = system.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;

        ParticleSystem.EmissionModule emission = system.emission;
        ParticleSystem.VelocityOverLifetimeModule velocity = system.velocityOverLifetime;

        Texture2D texture;

        switch (condition)
        {
            case WeatherType.Rain:
                // Dense fast streaks falling from above the top edge.
                main.startLifetime = 1.1f;
                main.startSpeed = height * 1.6f;
                main.startSize = height * 0.022f;
                main.startColor = new Color(0.7f, 0.82f, 1f, 0.6f);
                main.startRotation = 0f;
                main.maxParticles = 1200;
                emission.rateOverTime = 320f;
                shape.scale = new Vector3(width * 1.3f, 0.1f, 1f);
                shape.position = new Vector3(0f, height * 0.6f, 0f);
                shape.rotation = new Vector3(90f, 0f, 0f);
                texture = BuildStreak(4, 32, new Color(0.85f, 0.92f, 1f));
                break;

            case WeatherType.Wind:
                // Sparse horizontal streaks; the arc comes from a downward drift
                // applied over lifetime, which is the spec's "curved lines".
                main.startLifetime = 2.2f;
                main.startSpeed = width * 0.75f;
                main.startSize = height * 0.03f;
                main.startColor = new Color(0.95f, 0.93f, 0.85f, 0.4f);
                main.maxParticles = 300;
                emission.rateOverTime = 45f;
                shape.scale = new Vector3(0.1f, height * 1.2f, 1f);
                shape.position = new Vector3(-width * 0.7f, 0f, 0f);
                shape.rotation = new Vector3(0f, 90f, 0f);

                // Unity requires all three velocity axes to share a curve mode;
                // setting only y throws "Particle Velocity curves must all be in
                // the same mode". x and z stay flat curves rather than constants.
                velocity.enabled = true;
                velocity.space = ParticleSystemSimulationSpace.Local;
                velocity.x = new ParticleSystem.MinMaxCurve(0f, AnimationCurve.Constant(0f, 1f, 0f));
                velocity.z = new ParticleSystem.MinMaxCurve(0f, AnimationCurve.Constant(0f, 1f, 0f));
                velocity.y = new ParticleSystem.MinMaxCurve(
                    -height * 0.05f,
                    AnimationCurve.Linear(0f, 0f, 1f, 1f));
                texture = BuildStreak(32, 3, new Color(1f, 0.98f, 0.9f));
                break;

            default:
                // Heat: slow rising shimmer sitting on top of the warm light wash.
                main.startLifetime = 3f;
                main.startSpeed = height * 0.12f;
                main.startSize = height * 0.14f;
                main.startColor = new Color(1f, 0.85f, 0.55f, 0.18f);
                main.maxParticles = 200;
                emission.rateOverTime = 25f;
                shape.scale = new Vector3(width * 1.2f, 0.1f, 1f);
                shape.position = new Vector3(0f, -height * 0.55f, 0f);
                shape.rotation = new Vector3(-90f, 0f, 0f);
                texture = BuildBlob(32, new Color(1f, 0.9f, 0.65f));
                break;
        }

        var renderer = system.GetComponent<ParticleSystemRenderer>();
        renderer.material = new Material(Shader.Find("Sprites/Default")) { mainTexture = texture };
        renderer.sortingOrder = 32000;
        renderer.renderMode = condition == WeatherType.Heat
            ? ParticleSystemRenderMode.Billboard
            : ParticleSystemRenderMode.Stretch;

        if (renderer.renderMode == ParticleSystemRenderMode.Stretch)
        {
            // Stretch scales length off startSize, so thinning the streaks also
            // shortens them. Raise lengthScale to keep them reading as streaks
            // rather than dashes.
            renderer.lengthScale = condition == WeatherType.Rain ? 9f : 14f;
            renderer.velocityScale = 0f;
        }
    }

    // Soft-edged bar, used for both raindrops and wind lines by swapping which
    // dimension is the long one.
    private static Texture2D BuildStreak(int width, int height, Color color)
    {
        var tex = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        var pixels = new Color[width * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                // Fade out towards both ends of the long axis and the outer edges.
                float u = width > height ? (x + 0.5f) / width : (y + 0.5f) / height;
                float across = width > height ? (y + 0.5f) / height : (x + 0.5f) / width;

                float alongFade = Mathf.Sin(u * Mathf.PI);
                float acrossFade = 1f - Mathf.Abs(across * 2f - 1f);

                Color pixel = color;
                pixel.a = alongFade * acrossFade;
                pixels[y * width + x] = pixel;
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    private static Texture2D BuildBlob(int res, Color color)
    {
        var tex = new Texture2D(res, res, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        float center = res * 0.5f;
        var pixels = new Color[res * res];
        for (int y = 0; y < res; y++)
        {
            for (int x = 0; x < res; x++)
            {
                float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(center, center));
                Color pixel = color;
                pixel.a = Mathf.Clamp01(1f - dist / center);
                pixel.a *= pixel.a;
                pixels[y * res + x] = pixel;
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }
}
