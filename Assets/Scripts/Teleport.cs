using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Unity.Cinemachine;
public class Teleport : MonoBehaviour
{
    public Transform pointToTeleport;
    public Teleport teleportToUse;
    public Collider2D confiner;
    public CinemachineConfiner2D confiner2D;

    private ScreenFader screenFader;
    [SerializeField] private AudioClip openDoorSound;

    private void Start()
    {
        screenFader = FindAnyObjectByType<ScreenFader>();   
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player")) 
        {
            SoundEffectsManager.instance.PlaySound(openDoorSound, transform, 1f);
            CallTP(collision.gameObject);
        }
    }

    public void CallTP(GameObject player) 
    {
        StartCoroutine(CoroutineTeleport(player));
    }

    IEnumerator CoroutineTeleport(GameObject player) 
    {
        screenFader.FadeOut();
        yield return new WaitForSeconds(1);
        player.transform.position = teleportToUse.pointToTeleport.position;
        // Both doors run this same script, so the flag has to flip rather than
        // latch: it was stuck on after the first trip inside, which left the
        // player walking on floorboards - and now sheltered from the weather -
        // out in the middle of the farm.
        PlayerMovement movement = player.GetComponent<PlayerMovement>();
        movement.isInBarn = !movement.isInBarn;
        confiner2D.BoundingShape2D = null;
        yield return null;
        confiner2D.BoundingShape2D = teleportToUse.confiner;
        yield return new WaitForSeconds(0.5f);
        screenFader.FadeIn();
    }

}
