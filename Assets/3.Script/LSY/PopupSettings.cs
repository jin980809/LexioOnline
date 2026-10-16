using UnityEngine;

[CreateAssetMenu(fileName = "PopupSettings", menuName = "UI/Popup Settings")]
public class PopupSettings : ScriptableObject
{
    public float openTime = 0.3f;
    public float closeTime = 0.18f;
    [Range(0f, 1f)] public float startScale = 0.6f;
}
