using UnityEngine;

// World-space water bar above a farm plot. Builds its own sprites at
// runtime, so it only needs to be added as a component on the plot.
[RequireComponent(typeof(FarmPlot))]
public class PlotWaterBar : MonoBehaviour
{
    [SerializeField] private Vector2 offset = new Vector2(0f, 0.7f);
    [SerializeField] private float width = 0.8f;
    [SerializeField] private float height = 0.12f;
    [SerializeField] private Color backColor = new Color(0.1f, 0.1f, 0.1f, 0.8f);
    [SerializeField] private Color fillColor = new Color(0.3f, 0.6f, 1f, 1f);

    private static Sprite whiteSprite;

    private FarmPlot plot;
    private SpriteRenderer plotRenderer;
    private GameObject barRoot;
    private Transform fill;

    private void Awake()
    {
        plot = GetComponent<FarmPlot>();
        plotRenderer = GetComponent<SpriteRenderer>();

        int order = plotRenderer != null ? plotRenderer.sortingOrder + 10 : 10;

        barRoot = new GameObject("WaterBar");
        barRoot.transform.SetParent(transform, false);
        barRoot.transform.localPosition = offset;

        Transform back = CreatePart("Back", backColor, order);
        back.localScale = new Vector3(width, height, 1f);

        fill = CreatePart("Fill", fillColor, order + 1);

        barRoot.SetActive(false);
    }

    private void LateUpdate()
    {
        bool show = plot.ShowWaterBar;
        if (barRoot.activeSelf != show)
        {
            barRoot.SetActive(show);
        }

        if (!show)
        {
            return;
        }

        // Shrink the fill toward the left edge as water depletes.
        float t = plot.WaterNormalized;
        fill.localScale = new Vector3(width * t, height * 0.7f, 1f);
        fill.localPosition = new Vector3(-width * (1f - t) * 0.5f, 0f, 0f);
    }

    private Transform CreatePart(string name, Color color, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(barRoot.transform, false);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = WhiteSprite;
        sr.color = color;
        sr.sortingOrder = order;
        if (plotRenderer != null)
        {
            sr.sortingLayerID = plotRenderer.sortingLayerID;
        }

        return go.transform;
    }

    private static Sprite WhiteSprite
    {
        get
        {
            if (whiteSprite == null)
            {
                var tex = new Texture2D(1, 1);
                tex.SetPixel(0, 0, Color.white);
                tex.Apply();
                whiteSprite = Sprite.Create(tex, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
            }

            return whiteSprite;
        }
    }
}
