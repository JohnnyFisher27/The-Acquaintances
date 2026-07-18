using UnityEngine;
using UnityEngine.UI;

public class HungerBar : MonoBehaviour
{
    private RectTransform fill;
    private Text value;
    private PlayerHunger hunger;

    private void Awake()
    {
        fill = (RectTransform)transform.Find("Fill");
        value = transform.Find("Value").GetComponent<Text>();
    }

    private void Start()
    {
        hunger = FindAnyObjectByType<PlayerHunger>();
    }

    // Shrink the green fill so red shows on the right, and display the current number.
    private void Update()
    {
        if (hunger != null)
        {
            fill.anchorMax = new Vector2(hunger.Normalized, 1f);
            value.text = Mathf.CeilToInt(hunger.Current).ToString();
        }
    }
}
