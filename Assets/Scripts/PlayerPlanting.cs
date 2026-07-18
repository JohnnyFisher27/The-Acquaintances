using UnityEngine;
using UnityEngine.InputSystem;
public class PlayerPlanting: MonoBehaviour
{
    [SerializeField] private InputActionAsset inputActions;

    public int id;

    [Header("Starting Inventory")]
    [SerializeField] private int seeds = 5;
    [SerializeField] private int food = 0;

    [Header("Food")]
    [SerializeField] private float hungerRestoredPerFood = 250f;

    private InputAction interAction;
    public int Seeds => seeds;
    public int Food => food;
    private PlayerHunger playerHunger;
    private void Awake()
    {
        playerHunger = GetComponent<PlayerHunger>();
    }

    // Tool use (E) lives in PlayerTools now. Eating stays here.
    public void Update()
    {
        if (Keyboard.current.qKey.wasPressedThisFrame)
        {
            EatFood(playerHunger);
        }
    }

    public FarmPlot farmplot;

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

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("FarmPlot"))
        {
            farmplot = other.GetComponent<FarmPlot>();
        }
    }
    private void OnTriggerStay2D(Collider2D other)
    {
        
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("FarmPlot"))
        {
            farmplot = null;
        }
    }
}