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

    private Color targetTint;
    private float targetIntensity;

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

        heatOverlay = BuildOverlay(WeatherType.Heat, heatOverride);
        rainOverlay = BuildOverlay(WeatherType.Rain, rainOverride);
        windOverlay = BuildOverlay(WeatherType.Wind, windOverride);

        weather.OnWeatherChanged += Apply;
        Apply(weather.current);
    }

    private void OnDestroy()
    {
        if (weather != null)
        {
            weather.OnWeatherChanged -= Apply;
        }
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

        SetEmitting(heatOverlay, condition == WeatherType.Heat);
        SetEmitting(rainOverlay, condition == WeatherType.Rain);
        SetEmitting(windOverlay, condition == WeatherType.Wind);
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
        if (globalLight == null)
        {
            return;
        }

        float step = transitionDuration > 0f ? Time.deltaTime / transitionDuration : 1f;

        globalLight.color = Color.Lerp(globalLight.color, targetTint, step);
        globalLight.intensity = Mathf.Lerp(globalLight.intensity, targetIntensity, step);
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

    private void Configure(ParticleSystem system, WeatherType condition)
    {
        // Cover whatever the camera can actually see, so the effect never
        // reveals its edges on a wide monitor.
        float height = view.orthographic ? view.orthographicSize * 2f : 10f;
        float width = height * view.aspect;

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
