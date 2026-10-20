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

    public UserInfo userInfo { get; private set; }

    public void UserInfoUpDate(UserInfo info)
    {
        if (info == null)
        {
            Debug.LogError("[PlayerDBManager] null UserInfo는 저장할 수 없습니다.");
            return;
        }

        userInfo = info;

        Debug.Log($"[PlayerDBManager] 저장 완료: {userInfo.userID}");
    }
}
