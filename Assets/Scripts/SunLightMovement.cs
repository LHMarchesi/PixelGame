using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SunLightMovement : MonoBehaviour
{
    [SerializeField] bool canMove; // Flag to control movement
    [SerializeField] float speed = 1.0f; // Speed of the sun movement

    void Update()
    {
        if (canMove)
        {
           transform.position += Vector3.right * speed * Time.deltaTime; // Move the sun to the right
        }
    }
}
