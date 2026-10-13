using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SelectProfileImage : MonoBehaviour
{
    [SerializeField] private int imagesNum = 0;

    public void SelectImage()
    {
        DBManager.instance.SelectImageON(imagesNum);
    }
}
