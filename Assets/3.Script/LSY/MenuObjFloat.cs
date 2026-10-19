using UnityEngine;
using UnityEngine.InputSystem;

public class MenuObjFloat : MonoBehaviour
{
    [Header("마우스에 반응하는 오브제의 이동/회전값")]
    [SerializeField] private float objMoveX = 0.2f;
    [SerializeField] private float objMoveY = 0.1f;

    [SerializeField] private float objRotX = 15.0f;
    [SerializeField] private float objRotY = 15.0f;

    [Header("오브제 모션블러")]
    [SerializeField] private float smoothTime = 4.0f;

    [Header("오브제 개별 둥실둥실 효과")]
    private bool isFloat = true;
    [SerializeField] private Vector3 floatSpeed = new Vector3(1.2f, 1.0f, 0.8f);
    [SerializeField] private Vector3 floatAmount = new Vector3(0.05f, 0.1f, 0.05f);

    private Vector3 iniPos;
    private Quaternion iniRot;
    private float rndOffset;

    private void Start()
    {
        iniPos = transform.localPosition;
        iniRot = transform.localRotation;
        rndOffset = Random.Range(0f, 100f);
    }

    private void Update()
    {
        if (Mouse.current == null)
        {
            return;
        }

        Vector2 mousePos = Mouse.current.position.ReadValue();
        float mouseX = (mousePos.x / Screen.width) * 2f - 1f;
        float mouseY = (mousePos.y / Screen.height) * 2f - 1f;

        mouseX = Mathf.Clamp(mouseX, -1f, 1f);
        mouseY = Mathf.Clamp(mouseY, -1f, 1f);
        Vector3 targetPos = iniPos + new Vector3(mouseX * objMoveX, mouseY * objMoveY, 0f);

        float pitch = -mouseY * objRotY;
        float yaw = mouseX * objRotX;
        Quaternion targetRot = iniRot * Quaternion.Euler(pitch, yaw, 0f);

        if (isFloat)
        {
            float t = Time.time + rndOffset;
            float idleX = Mathf.Sin(t * floatSpeed.x) * floatAmount.x;
            float idleY = Mathf.Sin(t * floatSpeed.y) * floatAmount.y;
            float idleZ = Mathf.Cos(t * floatSpeed.z) * floatAmount.z;
            targetPos += new Vector3(idleX, idleY, idleZ);
        }

        transform.localPosition = Vector3.Lerp(transform.localPosition, targetPos, Time.deltaTime * smoothTime);
        transform.localRotation = Quaternion.Slerp(transform.localRotation, targetRot, Time.deltaTime * smoothTime);
    }
}
