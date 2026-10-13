using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OnOffObject : MonoBehaviour
{
    [SerializeField] private GameObject Obj;
    public void OnObject()
    {
        Obj.SetActive(true);
    }
    public void OffObject()
    {
        Obj.SetActive(false);
    }
}
