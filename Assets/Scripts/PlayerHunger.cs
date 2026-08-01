using System;
using UnityEngine;

public class PlayerHunger : MonoBehaviour
{
    [SerializeField] private float maxHunger = 1000f;
    [SerializeField] private float depletionRate = 5f;

    public event Action OnDepleted;

    public float Max => maxHunger;
    public float Current { get; private set; }
    public float Normalized => Current / maxHunger;

    public bool DepletionPaused { get; set; }

    private bool depleted;

    // Start the day with a full hunger meter.
    private void Awake()
    {
        Current = maxHunger;
    }

    // Drain hunger over time and fire OnDepleted once it hits 0.
    private void Update()
    {
        if (depleted || DepletionPaused)
        {
            return;
        }

        Current = Mathf.Max(0f, Current - depletionRate * Time.deltaTime);
        if (Current <= 0f)
        {
            depleted = true;
            OnDepleted?.Invoke();
        }
    }

    // Death penalty respawn: set hunger to a fraction of max and allow depletion again.
    public void ResetHunger(float fraction)
    {
        depleted = false;
        Current = maxHunger * Mathf.Clamp01(fraction);
    }

    // Restore hunger from food, capped at the max.
    public void Eat(float amount)
    {
        if (!depleted)
        {
            Current = Mathf.Min(maxHunger, Current + amount);
        }
    }
}
