using System.Collections;
using UnityEngine;

[RequireComponent(typeof(CanvasGroup))]
public class ScreenFader : MonoBehaviour
{
    [SerializeField] private float fadeDuration = 1f;

    private CanvasGroup group;

    private void Awake()
    {
        group = GetComponent<CanvasGroup>();
    }

    public void SetBlack()
    {
        group.alpha = 1f;
    }

    public Coroutine FadeOut()
    {
        return StartCoroutine(Fade(1f));
    }

    public Coroutine FadeIn()
    {
        return StartCoroutine(Fade(0f));
    }

    private IEnumerator Fade(float target)
    {
        float start = group.alpha;
        for (float t = 0f; t < fadeDuration; t += Time.deltaTime)
        {
            group.alpha = Mathf.Lerp(start, target, t / fadeDuration);
            yield return null;
        }
        group.alpha = target;
    }
}
