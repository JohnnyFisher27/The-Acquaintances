using UnityEngine;

public class PlayerPlanting: MonoBehaviour
{
    [Header("Starting Inventory")]
    [SerializeField] private int seeds = 5;
    [SerializeField] private int food = 0;

    [Header("Food")]
    [SerializeField] private float hungerRestoredPerFood = 250f;

    public int Seeds => seeds;
    public int Food => food;

    public bool UseSeed()
    {
        if (seeds <= 0)
        {
            return false;
        }

        seeds--;
        return true;
    }

    public void AddSeeds(int amount)
    {
        seeds += Mathf.Max(0, amount);
    }

    public void AddFood(int amount)
    {
        food += Mathf.Max(0, amount);
    }

    public bool EatFood(PlayerHunger hunger)
    {
        if (food <= 0 || hunger == null)
        {
            return false;
        }

        food--;
        hunger.Eat(hungerRestoredPerFood);
        return true;
    }
}