using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// The weather readout, built out of the WeatherMeter badge art instead of the
// condition's name in text.
//
// Two icons: the big one is what is happening now, the small one behind a
// "Next:" label is what WeatherManager has already rolled for the next spell.
// The next icon sits calm for most of the spell and starts flashing as the
// changeover approaches, faster and harder the closer it gets, so the player
// gets a read on "storm soon" from peripheral vision without parsing a timer.
//
// Sprites come from Resources/Weather/WeatherMeter, sliced 4x4: a wind badge, a
// sun badge and a 14 frame rain loop. Heat has no badge of its own on the sheet,
// so it reuses the sun tinted hot - drop a sprite into heatOverride to replace
// it without touching this code.
[RequireComponent(typeof(WeatherManager))]
public class WeatherHud : MonoBehaviour
{
    private const string SheetPath = "Weather/WeatherMeter";

    [Header("Icon Overrides (optional)")]
    [SerializeField] private Sprite clearOverride;
    [SerializeField] private Sprite heatOverride;
    [SerializeField] private Sprite windOverride;
    [SerializeField] private Sprite[] rainOverride;

    [Header("Tints")]
    [SerializeField] private Color clearTint = Color.white;
    // The sun badge is already gold, so heat only needs pushing towards red to
    // read as a different condition rather than a brighter clear spell.
    [SerializeField] private Color heatTint = new Color(1f, 0.55f, 0.32f);
    [SerializeField] private Color rainTint = Color.white;
    [SerializeField] private Color windTint = Color.white;

    [Header("Layout")]
    [SerializeField] private Vector2 margin = new Vector2(16f, 16f);
    // Everything else is sized off this, so scaling the whole readout up or
    // down for a different screen is a one field change.
    [SerializeField] private float currentIconSize = 112f;
    [SerializeField] private float nextIconSize = 64f;
    [SerializeField] private float timerFontSize = 36f;
    [SerializeField] private float nextLabelFontSize = 26f;

    [Header("Background")]
    // The badges are dark-framed and were washing out against the farm at
    // dusk and against the heat glare. A panel behind them keeps the readout
    // legible whatever the weather is doing to the screen.
    [SerializeField] private Sprite backgroundOverride;
    [SerializeField] private Color backgroundColor = new Color(0.07f, 0.09f, 0.12f, 0.72f);
    [SerializeField] private float backgroundPadding = 14f;
    [SerializeField] private float backgroundCornerRadius = 16f;

    [Header("Animation")]
    [SerializeField] private float rainFps = 12f;
    // Heat has no frame animation of its own, so it breathes instead - enough
    // motion to separate it from a static clear sun at a glance.
    [SerializeField] private float heatPulseScale = 0.08f;

    [Header("Forecast Warning")]
    // How long before the changeover the next icon starts flashing.
    [SerializeField] private float warnLeadTime = 20f;
    [SerializeField] private float slowFlashHz = 1.2f;
    [SerializeField] private float fastFlashHz = 7f;
    // How far the flash dips at each end of the warning window.
    [SerializeField] private float slowFlashDepth = 0.35f;
    [SerializeField] private float fastFlashDepth = 0.9f;
    [SerializeField] private float warnPunchScale = 0.25f;

    private WeatherManager weather;

    private Image currentIcon;
    private Image nextIcon;
    private TextMeshProUGUI nextLabel;
    private TextMeshProUGUI timerText;

    private Sprite sunSprite;
    private Sprite windSprite;
    private Sprite[] rainSprites = System.Array.Empty<Sprite>();

    private bool spritesMissing;
    private float rainTimer;
    // Accumulated rather than read off Time.time: the flash speeds up as the
    // changeover nears, and multiplying a rising frequency by a rising clock
    // jumps the phase every frame.
    private float flashPhase;

    private void Awake()
    {
        weather = GetComponent<WeatherManager>();
        LoadSprites();
    }

    private void Start()
    {
        Build();
        weather.OnWeatherChanged += OnWeatherChanged;
        OnWeatherChanged(weather.current);
    }

    private void OnDestroy()
    {
        if (weather != null)
        {
            weather.OnWeatherChanged -= OnWeatherChanged;
        }
    }

    private void LoadSprites()
    {
        var frames = new List<Sprite>();

        foreach (Sprite sprite in Resources.LoadAll<Sprite>(SheetPath))
        {
            if (sprite.name.Contains("Sun"))
            {
                sunSprite = sprite;
            }
            else if (sprite.name.Contains("Wind"))
            {
                windSprite = sprite;
            }
            else if (sprite.name.Contains("Rain"))
            {
                frames.Add(sprite);
            }
        }

        // LoadAll returns the slices in sheet order, which is already the frame
        // order, but sort by name so a re-slice can't scramble the loop.
        frames.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        rainSprites = frames.ToArray();

        spritesMissing = sunSprite == null && windSprite == null && rainSprites.Length == 0;
        if (spritesMissing)
        {
            Debug.LogWarning($"[WeatherHud] No sprites at Resources/{SheetPath}; falling back to text.");
        }
    }

    // ------------------------------------------------------------------
    // Sprite selection
    // ------------------------------------------------------------------

    private Sprite SpriteFor(WeatherType condition)
    {
        switch (condition)
        {
            case WeatherType.Heat:
                return heatOverride != null ? heatOverride : sunSprite;
            case WeatherType.Wind:
                return windOverride != null ? windOverride : windSprite;
            case WeatherType.Rain:
                return RainFrame(0);
            default:
                return clearOverride != null ? clearOverride : sunSprite;
        }
    }

    private Sprite RainFrame(int index)
    {
        Sprite[] frames = rainOverride != null && rainOverride.Length > 0 ? rainOverride : rainSprites;
        if (frames.Length == 0)
        {
            return null;
        }

        return frames[((index % frames.Length) + frames.Length) % frames.Length];
    }

    private int RainFrameCount =>
        rainOverride != null && rainOverride.Length > 0 ? rainOverride.Length : rainSprites.Length;

    private Color TintFor(WeatherType condition)
    {
        switch (condition)
        {
            case WeatherType.Heat:
                return heatTint;
            case WeatherType.Rain:
                return rainTint;
            case WeatherType.Wind:
                return windTint;
            default:
                return clearTint;
        }
    }

    // ------------------------------------------------------------------
    // Per-frame drive
    // ------------------------------------------------------------------

    private void OnWeatherChanged(WeatherType condition)
    {
        rainTimer = 0f;
        flashPhase = 0f;

        if (currentIcon != null)
        {
            currentIcon.sprite = SpriteFor(condition);
            currentIcon.color = TintFor(condition);
            currentIcon.enabled = currentIcon.sprite != null;
        }

        if (nextIcon != null)
        {
            nextIcon.sprite = SpriteFor(weather.Next);
            nextIcon.enabled = nextIcon.sprite != null;
        }
    }

    private void Update()
    {
        if (currentIcon == null)
        {
            return;
        }

        DriveCurrent();
        DriveForecast();
    }

    private void DriveCurrent()
    {
        if (weather.current == WeatherType.Rain && RainFrameCount > 1)
        {
            rainTimer += Time.deltaTime * Mathf.Max(0.01f, rainFps);
            currentIcon.sprite = RainFrame(Mathf.FloorToInt(rainTimer));
            currentIcon.rectTransform.localScale = Vector3.one;
            return;
        }

        if (weather.current == WeatherType.Heat && heatPulseScale > 0f)
        {
            float pulse = 1f + Mathf.Sin(Time.time * Mathf.PI) * heatPulseScale;
            currentIcon.rectTransform.localScale = new Vector3(pulse, pulse, 1f);
            return;
        }

        currentIcon.rectTransform.localScale = Vector3.one;
    }

    private void DriveForecast()
    {
        if (timerText != null)
        {
            int seconds = Mathf.CeilToInt(weather.TimeRemaining);
            string clock = $"{seconds / 60}:{seconds % 60:00}";

            // Nothing to draw badges with, so the labels carry the readout on
            // their own rather than leaving an empty corner.
            timerText.text = spritesMissing ? $"{weather.current}  {clock}" : clock;
        }

        if (spritesMissing && nextLabel != null)
        {
            nextLabel.text = $"Next: {weather.Next}";
        }

        if (nextIcon == null)
        {
            return;
        }

        // Keeps up with a mid-spell re-roll rather than only refreshing on the
        // changeover the icon is warning about.
        Sprite expected = SpriteFor(weather.Next);
        if (nextIcon.sprite != expected)
        {
            nextIcon.sprite = expected;
            nextIcon.enabled = expected != null;
        }

        // 0 for most of the spell, ramping to 1 at the changeover.
        float urgency = warnLeadTime > 0f
            ? 1f - Mathf.Clamp01(weather.TimeRemaining / warnLeadTime)
            : 0f;

        Color tint = TintFor(weather.Next);

        if (urgency <= 0f)
        {
            flashPhase = 0f;
            nextIcon.color = new Color(tint.r, tint.g, tint.b, tint.a * 0.75f);
            nextIcon.rectTransform.localScale = Vector3.one;

            if (nextLabel != null)
            {
                nextLabel.alpha = 0.75f;
            }
            return;
        }

        flashPhase += Time.deltaTime * Mathf.Lerp(slowFlashHz, fastFlashHz, urgency);

        // 1 at the top of the flash, down to (1 - depth) at the bottom.
        float wave = (Mathf.Sin(flashPhase * Mathf.PI * 2f) + 1f) * 0.5f;
        float depth = Mathf.Lerp(slowFlashDepth, fastFlashDepth, urgency);
        float level = 1f - depth * (1f - wave);

        nextIcon.color = new Color(tint.r, tint.g, tint.b, tint.a * level);

        float punch = 1f + wave * warnPunchScale * urgency;
        nextIcon.rectTransform.localScale = new Vector3(punch, punch, 1f);

        if (nextLabel != null)
        {
            nextLabel.alpha = level;
        }
    }

    // ------------------------------------------------------------------
    // Runtime-built widgets
    // ------------------------------------------------------------------

    private void Build()
    {
        Canvas canvas = HudCanvas.Get();

        // Two rows sharing the height of the big badge: countdown on top,
        // "Next:" and its badge underneath.
        float gap = currentIconSize * 0.13f;
        float column = currentIconSize + gap;
        float row = currentIconSize * 0.5f;

        // Rough advance widths for "0:00" and "Next:" at the chosen sizes.
        // Only used to place what comes after them, so being a few pixels
        // generous costs nothing but a slightly wider panel.
        float timerWidth = timerFontSize * 3.4f;
        float labelWidth = nextLabelFontSize * 2.9f;

        float nextIconX = column + labelWidth + gap * 0.4f;
        float contentWidth = Mathf.Max(column + timerWidth, nextIconX + nextIconSize);
        var content = new Vector2(contentWidth, currentIconSize);

        var root = new GameObject("Weather HUD", typeof(RectTransform), typeof(Image));
        root.transform.SetParent(canvas.transform, false);

        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.zero;
        rootRect.pivot = Vector2.zero;
        rootRect.anchoredPosition = margin;
        rootRect.sizeDelta = content + Vector2.one * (backgroundPadding * 2f);

        var background = root.GetComponent<Image>();
        background.sprite = backgroundOverride != null ? backgroundOverride : BuildPanelSprite();
        // Sliced so the corner radius holds its shape whatever the panel grows to.
        background.type = Image.Type.Sliced;
        background.color = backgroundColor;
        background.raycastTarget = false;

        // Inset from the panel so every widget below can keep positioning itself
        // from a plain (0,0) origin.
        var contentGo = new GameObject("Content", typeof(RectTransform));
        contentGo.transform.SetParent(rootRect, false);

        var contentRect = contentGo.GetComponent<RectTransform>();
        contentRect.anchorMin = Vector2.zero;
        contentRect.anchorMax = Vector2.zero;
        contentRect.pivot = Vector2.zero;
        contentRect.anchoredPosition = Vector2.one * backgroundPadding;
        contentRect.sizeDelta = content;

        currentIcon = BuildIcon(contentRect, "Current", Vector2.zero, currentIconSize);

        timerText = BuildLabel(
            contentRect,
            "Timer",
            new Vector2(column, row),
            new Vector2(timerWidth, row),
            timerFontSize);

        nextLabel = BuildLabel(
            contentRect,
            "Next Label",
            new Vector2(column, 0f),
            new Vector2(labelWidth, row),
            nextLabelFontSize);
        nextLabel.text = "Next:";

        nextIcon = BuildIcon(
            contentRect,
            "Next",
            new Vector2(nextIconX, (row - nextIconSize) * 0.5f),
            nextIconSize);
    }

    // Rounded panel, generated the same way WeatherVisuals builds its overlay
    // textures so the HUD needs no art in the project to look finished.
    private Sprite BuildPanelSprite()
    {
        float radius = Mathf.Max(1f, backgroundCornerRadius);

        // Only the corners carry detail, so the texture only has to be big
        // enough to hold them - the sliced middle stretches to any panel size.
        int res = Mathf.CeilToInt(radius * 2f) + 2;
        float half = res * 0.5f;
        float inner = half - radius;

        var tex = new Texture2D(res, res, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        var pixels = new Color[res * res];
        for (int y = 0; y < res; y++)
        {
            for (int x = 0; x < res; x++)
            {
                // Distance outside a rounded rect of half-extent `half` and
                // corner radius `radius`; negative inside.
                float dx = Mathf.Max(Mathf.Abs(x + 0.5f - half) - inner, 0f);
                float dy = Mathf.Max(Mathf.Abs(y + 0.5f - half) - inner, 0f);
                float distance = Mathf.Sqrt(dx * dx + dy * dy) - radius;

                // Half a pixel of falloff either side of the edge, so the
                // corners read as curves rather than staircases.
                pixels[y * res + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(0.5f - distance));
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();

        // Border keeps the corners intact under Image.Type.Sliced.
        float border = radius + 1f;
        return Sprite.Create(
            tex,
            new Rect(0f, 0f, res, res),
            new Vector2(0.5f, 0.5f),
            100f,
            0,
            SpriteMeshType.FullRect,
            new Vector4(border, border, border, border));
    }

    private static Image BuildIcon(RectTransform parent, string name, Vector2 position, float size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.zero;
        // Centre pivot so the warning punch scales about the badge's middle.
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(size, size);
        rect.anchoredPosition = position + new Vector2(size * 0.5f, size * 0.5f);

        var image = go.GetComponent<Image>();
        image.raycastTarget = false;
        image.preserveAspect = true;
        return image;
    }

    private static TextMeshProUGUI BuildLabel(
        RectTransform parent, string name, Vector2 position, Vector2 size, float fontSize)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.zero;
        rect.pivot = Vector2.zero;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        var text = go.GetComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.alignment = TextAlignmentOptions.Left;
        text.color = Color.white;
        text.raycastTarget = false;
        return text;
    }
}
