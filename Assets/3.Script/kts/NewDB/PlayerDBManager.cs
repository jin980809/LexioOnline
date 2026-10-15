using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerDBManager : MonoBehaviour
{
    public static PlayerDBManager instance = null;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
        DontDestroyOnLoad(gameObject);
    }

    public UserInfo userInfo;
}
