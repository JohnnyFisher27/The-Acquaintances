using UnityEngine;

public class SoundEffectsManager : MonoBehaviour
{
    public static SoundEffectsManager instance;

    [SerializeField] private AudioSource soundFXObject;

    [SerializeField] private AudioSource currentSongSource;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        
    }

    public void PlaySound(AudioClip audioClip, Transform spawnTransform, float volume, bool isSong = false)
    {
        AudioSource audioSource = Instantiate(soundFXObject, spawnTransform.position, Quaternion.identity);
        audioSource.volume = volume/2;
        audioSource.clip = audioClip;
        audioSource.loop = true;

        if (isSong)
        {
            if (currentSongSource != null)
            {
                Destroy(currentSongSource.gameObject);
            }
            currentSongSource = audioSource;

        }

        audioSource.Play();

        float clipLength = audioSource.clip.length;

        if (!isSong)
        {
            Destroy(audioSource.gameObject, clipLength);
        }
    }
    
}