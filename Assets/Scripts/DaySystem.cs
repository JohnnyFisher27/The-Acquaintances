using UnityEngine;
using TMPro;

public enum DayPeriod { Morning, Afternoon, Night }
public class DaySystem : MonoBehaviour
{
    [Header("Settings")]
    public float startHour = 9;
    public float secondsPerMinute = 1f;

    [Header("Day Periods")]
    public float morningStart = 6f;
    public float afternoonStart = 12f;
    public float nightStart = 19f;


    [Header("UI")]
    public TextMeshProUGUI clockText;


    public DayPeriod currentPeriod { get; private set;}
    public int day { get; private set; } = 1;

    private float currentMintues;
    private DayPeriod lastPeriod;

    private void Start()
    {
        currentMintues = startHour * 60f ;
        currentPeriod = GetPeriod();
        lastPeriod = currentPeriod;
    }

    private void Update()
    {
        currentMintues += (Time.deltaTime / secondsPerMinute);

        // The game is continuous: the clock rolls into the next day at midnight.
        if (currentMintues >= 24f * 60f)
        {
            currentMintues -= 24f * 60f;
            day++;
            Debug.Log($"Day {day} begins");
        }

        CheckPeriod();
        UpdateDisplay();
    }

    // Death penalty: rewind the clock to 12 AM of the current day.
    public void ResetToMidnight()
    {
        currentMintues = 0f;
        CheckPeriod();
        UpdateDisplay();
    }

    private void UpdateDisplay()
    {
        if (clockText == null) return;

        int tM = Mathf.FloorToInt(currentMintues);
        int h = tM / 60;
        int m = tM % 60;

        string p = h >= 12 ? "PM" : "AM";
        int dH = h % 12;
        if (dH == 0) dH = 12;

        clockText.text = $"{dH}:{m:D2} {p}";
    }

    private DayPeriod GetPeriod()
    {
        float hour = currentMintues / 60f;

        // Midnight to morningStart counts as night now that the clock wraps.
        if (hour < morningStart || hour >= nightStart) return DayPeriod.Night;
        if (hour >= afternoonStart) return DayPeriod.Afternoon;
        return DayPeriod.Morning;
    }

    private void CheckPeriod()
    {
        currentPeriod = GetPeriod();

        if (currentPeriod != lastPeriod)
        {
            lastPeriod = currentPeriod;
        }
    }

}
