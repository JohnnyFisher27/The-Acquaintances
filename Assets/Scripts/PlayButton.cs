using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class PlayButton : MonoBehaviour
{
    [SerializeField] private string gameSceneName = "WorldScene";
    [SerializeField] private string tutorialSceneName = "Tutorial";
    [SerializeField] private string menuSceneName = "MainMenu";
    [SerializeField] private GameObject currentButton;
    [SerializeField] private AudioClip menuMusic;


    private void Awake()
    {
        GetComponent<Button>().onClick.AddListener(Play);

        SoundEffectsManager.instance.PlaySound(menuMusic, transform, 1f, true);
    }

    private void Play()
    {
        if (currentButton.name == "PlayButton")
        {
            SceneManager.LoadScene(gameSceneName);
        }
        else if (currentButton.name == "TutorialButton")
        {
            SceneManager.LoadScene(tutorialSceneName);
        }
        else if (currentButton.name == "MenuButton")
        {
            SceneManager.LoadScene(menuSceneName);
        }
    }
}
