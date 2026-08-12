using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class DayManager : MonoBehaviour
{
    [SerializeField] private float farmCheckInterval = 1f;
    [SerializeField] private float hungerAfterDeath = 0.5f;

    private PlayerHunger hunger;
    private ScreenFader fader;
    private PlayerPlanting planting;
    private DaySystem daySystem;
    private FarmPlot[] plots;
    private float farmCheckTimer;
    private bool busy;

    // Midnight checkpoint: the whole game state is rewound to this on death.
    private FarmPlot.Snapshot[] plotCheckpoint;
    private PlayerPlanting.Snapshot inventoryCheckpoint;
    private Vector3 playerCheckpoint;
    private int checkpointDay;

    [SerializeField] private AudioClip dayMusic;

    // Hook up death handling and fade in from black at the start of a run.
    private void Start()
    {
        hunger = FindAnyObjectByType<PlayerHunger>();
        fader = FindAnyObjectByType<ScreenFader>();
        planting = FindAnyObjectByType<PlayerPlanting>();
        daySystem = FindAnyObjectByType<DaySystem>();
        plots = FindObjectsByType<FarmPlot>();

        if (hunger != null)
        {
            hunger.OnDepleted += HandlePlayerDeath;
        }

        TakeCheckpoint();

        if (fader != null)
        {
            fader.SetBlack();
            fader.FadeIn();
        }

        SoundEffectsManager.instance.PlaySound(dayMusic, transform, 1f);
    }

    private void Update()
    {
        if (busy || planting == null)
        {
            return;
        }

        // A new day started: this is the new 12 AM state to rewind to.
        if (daySystem != null && daySystem.day != checkpointDay)
        {
            TakeCheckpoint();
        }

        CheckFarmLoseCondition();
    }

    // Lose condition: nothing alive on the farm and no seeds left to plant.
    private void CheckFarmLoseCondition()
    {
        if (plots.Length == 0)
        {
            return;
        }

        farmCheckTimer += Time.deltaTime;
        if (farmCheckTimer < farmCheckInterval)
        {
            return;
        }
        farmCheckTimer = 0f;

        if (planting.Seeds > 0)
        {
            return;
        }

        foreach (FarmPlot plot in plots)
        {
            if (plot.HasLivingCrop)
            {
                return;
            }
        }

        Debug.Log("The farm is dead and there are no seeds left. Game over.");
        busy = true;
        StartCoroutine(RestartRun());
    }

    private void TakeCheckpoint()
    {
        checkpointDay = daySystem != null ? daySystem.day : 1;
        inventoryCheckpoint = planting.Capture();
        playerCheckpoint = planting.transform.position;

        plotCheckpoint = new FarmPlot.Snapshot[plots.Length];
        for (int i = 0; i < plots.Length; i++)
        {
            plotCheckpoint[i] = plots[i].Capture();
        }
    }

    private void RestoreCheckpoint()
    {
        planting.Restore(inventoryCheckpoint);
        planting.transform.position = playerCheckpoint;

        for (int i = 0; i < plots.Length; i++)
        {
            plots[i].Restore(plotCheckpoint[i]);
        }
    }

    // Unsubscribe so a dead reference never gets called.
    private void OnDestroy()
    {
        if (hunger != null)
        {
            hunger.OnDepleted -= HandlePlayerDeath;
        }
    }

    // Called when hunger hits 0. The game keeps going: rewind everything
    // to the 12 AM state of the current day, with half hunger as the penalty.
    private void HandlePlayerDeath()
    {
        if (busy)
        {
            return;
        }

        busy = true;
        StartCoroutine(ResetToMidnight());
    }

    private IEnumerator ResetToMidnight()
    {
        var movement = FindAnyObjectByType<PlayerMovement>();
        FreezePlayer(movement);
        hunger.DepletionPaused = true;

        yield return fader.FadeOut();

        if (daySystem != null)
        {
            daySystem.ResetToMidnight();
        }
        RestoreCheckpoint();
        hunger.ResetHunger(hungerAfterDeath);

        yield return fader.FadeIn();

        if (movement != null)
        {
            movement.enabled = true;
        }
        hunger.DepletionPaused = false;
        busy = false;
    }

    // Full restart: the farm is unrecoverable, so reload the scene.
    private IEnumerator RestartRun()
    {
        FreezePlayer(FindAnyObjectByType<PlayerMovement>());

        yield return fader.FadeOut();
        SceneManager.LoadScene(gameObject.scene.name);
    }

    private void FreezePlayer(PlayerMovement movement)
    {
        if (movement != null)
        {
            movement.enabled = false;
            movement.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
        }
    }
}
