using UnityEngine;
using UnityEngine.UI;

// Finds a canvas safe to hang runtime HUD labels on.
//
// Picking any canvas is not safe: ScreenFader drives a CanvasGroup on
// FadeCanvas from 1 to 0, so anything parented there is invisible for the whole
// run once the opening fade finishes. Skip canvases under a CanvasGroup, and
// build a dedicated one if nothing suitable exists.
public static class HudCanvas
{
    private const string CanvasName = "HUD Canvas";

    public static Canvas Get()
    {
        foreach (Canvas canvas in Object.FindObjectsByType<Canvas>())
        {
            if (canvas.GetComponentInParent<CanvasGroup>() == null)
            {
                return canvas;
            }
        }

        var existing = GameObject.Find(CanvasName);
        if (existing != null)
        {
            Canvas found = existing.GetComponent<Canvas>();
            if (found != null)
            {
                return found;
            }
        }

        var canvasObject = new GameObject(CanvasName, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas created = canvasObject.GetComponent<Canvas>();
        created.renderMode = RenderMode.ScreenSpaceOverlay;
        return created;
    }
}
