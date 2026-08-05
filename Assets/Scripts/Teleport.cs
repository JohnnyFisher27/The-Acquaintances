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
        confiner2D.BoundingShape2D = null;
        yield return null;
        confiner2D.BoundingShape2D = teleportToUse.confiner;
        yield return new WaitForSeconds(0.5f);
        screenFader.FadeIn();
    }

}
