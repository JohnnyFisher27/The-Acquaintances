using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class DayManager : MonoBehaviour
{
    private PlayerHunger hunger;
    private ScreenFader fader;

    // Hook up death handling and fade in from black at the start of the day.
    private void Start()
    {
        hunger = FindFirstObjectByType<PlayerHunger>();
        fader = FindFirstObjectByType<ScreenFader>();

        hunger.OnDepleted += HandlePlayerDeath;

        fader.SetBlack();
        fader.FadeIn();
    }

    // Unsubscribe so a dead reference never gets called.
    private void OnDestroy()
    {
        if (hunger != null)
        {
            hunger.OnDepleted -= HandlePlayerDeath;
        }
    }

    // Called when hunger hits 0.
    private void HandlePlayerDeath()
    {
        StartCoroutine(RestartDay());
    }

    // Freeze the player, fade to black, then reload the scene for a fresh day.
    private IEnumerator RestartDay()
    {
        var movement = FindFirstObjectByType<PlayerMovement>();
        if (movement != null)
        {
            movement.enabled = false;
            movement.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
        }

        yield return fader.FadeOut();
        SceneManager.LoadScene(gameObject.scene.name);
    }
}
