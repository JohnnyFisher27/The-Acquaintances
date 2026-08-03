using System.Collections.Generic;
using UnityEngine;

// Colored silhouette sprites for plant species that don't have real art yet.
// Built at runtime and cached per (color, stage), same trick PlotWaterBar/
// PlotStormBar use for their bars. A Plant only falls back to this when its
// own plantedSpr/grownedSpr/readySpr/witheredSpr is left unassigned, so
// dropping in real art later just overrides it automatically.
public static class PlaceholderCropSprite
{
    private static readonly Dictionary<(Color, FarmPlot.CropState), Sprite> cache = new();

    public static Sprite Get(Color color, FarmPlot.CropState stage)
    {
        var key = (color, stage);
        if (cache.TryGetValue(key, out Sprite existing))
        {
            return existing;
        }

        float sizeFrac = stage switch
        {
            FarmPlot.CropState.Planted => 0.35f,
            FarmPlot.CropState.Growing => 0.65f,
            FarmPlot.CropState.Ready => 1f,
            FarmPlot.CropState.Withered => 0.5f,
            _ => 1f
        };

        Color tint = stage == FarmPlot.CropState.Withered
            ? Color.Lerp(color, new Color(0.4f, 0.35f, 0.25f), 0.7f)
            : color;

        Sprite sprite = BuildCircleSprite(tint, sizeFrac);
        cache[key] = sprite;
        return sprite;
    }

    private static Sprite BuildCircleSprite(Color color, float sizeFrac)
    {
        const int res = 64;
        var tex = new Texture2D(res, res, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        float center = res * 0.5f;
        float radius = center * sizeFrac;
        float edge = Mathf.Max(1f, radius * 0.08f);

        var pixels = new Color[res * res];
        for (int y = 0; y < res; y++)
        {
            for (int x = 0; x < res; x++)
            {
                float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(center, center));
                float alpha = 1f - Mathf.Clamp01((dist - radius) / edge);
                Color pixel = color;
                pixel.a *= alpha;
                pixels[y * res + x] = pixel;
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();

        return Sprite.Create(tex, new Rect(0f, 0f, res, res), new Vector2(0.5f, 0.5f), res);
    }
}
