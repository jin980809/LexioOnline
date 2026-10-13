using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerProfileClick : MonoBehaviour
{
    [SerializeField] private ProfilePopup profilePopup;

    // 서버에서 프로필 응답을 받은 뒤 호출할 함수
    public void OnProfileReceived(PlayerProfileData data)
    {
        profilePopup.ShowProfile(data);
    }
}
