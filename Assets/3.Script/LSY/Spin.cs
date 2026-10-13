using UnityEngine;

public class Spin : MonoBehaviour
{
    [Header("회전 설정")]
    [Tooltip("초당 회전할 각도")]
    public float rotationSpeed = 90f;

    [Tooltip("회전할 축 (기본: Y축)")]
    public Vector3 rotationAxis = Vector3.up;

    void Update()
    {
        // Time.deltaTime을 곱해 기기 프레임에 독립적인 일정한 속도 보장
        transform.Rotate(rotationAxis.normalized * rotationSpeed * Time.deltaTime);
    }
}