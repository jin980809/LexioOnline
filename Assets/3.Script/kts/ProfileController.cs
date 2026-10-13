using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ProfileController : MonoBehaviour
{
    [SerializeField] private Sprite[] profileImages;
    [SerializeField] private Image mainImage;

    private void OnEnable()
    {
        mainImage.sprite = profileImages[DBManager.instance.info.imageNum];
    }
}
