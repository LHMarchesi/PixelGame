using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Lever : MonoBehaviour, IPickUp
{
    [SerializeField] private Animator door;
    [SerializeField] private GameObject up;
    [SerializeField] private GameObject down;

    public void PickUp()
    {
        door.SetBool("CanOpen", true);
        up.SetActive(false);
        down.SetActive(true);
    }
}
