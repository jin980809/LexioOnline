using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TableTurnover : MonoBehaviour
{
    [SerializeField] private float rotateSpeed = 20f;

    void Update()
    {
        if (gameObject.activeSelf)
        {
            transform.Rotate(
                Vector3.up * rotateSpeed * Time.deltaTime,
                Space.Self
            );
        }
    }
}
