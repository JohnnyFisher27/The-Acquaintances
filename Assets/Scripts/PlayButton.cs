using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class PlayButton : MonoBehaviour
{
    [SerializeField] private string gameSceneName = "SampleScene";

    private void Awake()
    {
        GetComponent<Button>().onClick.AddListener(Play);
    }

    private void Play()
    {
        SceneManager.LoadScene(gameSceneName);
    }
}
