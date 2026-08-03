using UnityEngine;

public class SoundEffectsManager : MonoBehaviour
{
    public static SoundEffectsManager instance;

    [SerializeField] private AudioSource soundFXObject;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        
    }

    public void PlaySound(AudioClip audioClip, Transform spawnTransform, float volume)
    {
        // A missing clip or prefab used to null-ref and abort whatever
        // gameplay action was trying to play the sound.
        if (audioClip == null || soundFXObject == null || spawnTransform == null)
        {
            return;
        }

        AudioSource audioSource = Instantiate(soundFXObject, spawnTransform.position, Quaternion.identity);
        audioSource.volume = volume;
        audioSource.clip = audioClip;
        audioSource.Play();

        Destroy(audioSource.gameObject, audioClip.length);
    }
    
}