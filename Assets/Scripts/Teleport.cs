using UnityEngine;
using System.Collections;
using System.Collections.Generic;
public class Teleport : MonoBehaviour
{
    public Transform pointToTeleport;
    public Teleport teleportToUse;

    private ScreenFader screenFader;

    private void Start()
    {
        screenFader = FindAnyObjectByType<ScreenFader>();   
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player")) 
        {
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
        yield return new WaitForSeconds(0.5f);
        screenFader.FadeIn();
    }

}
