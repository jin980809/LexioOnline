using UnityEngine;
using UnityEngine.InputSystem;

public class MouseFollow : MonoBehaviour
{
    [Header("이동 설정")]
    [SerializeField] public Vector2 moveAmount = new Vector2(0.5f, 0.3f);
    [SerializeField] public Vector2 rotateAmount = new Vector2(3.0f, 2.0f);

    [Header("모션 블러")]
    [SerializeField] public float smoothTime = 3.0f;

    [Header("둥실둥실 효과")]
    private bool isFloat = true;
    [SerializeField] private Vector3 floatSpeed = new Vector3(1.0f, 0.8f, 0.5f);
    [SerializeField] private Vector3 floatAmount = new Vector3(0.03f, 0.05f, 0.02f);


    private Vector3 iniPos;
    private Quaternion iniRot;

    private Vector3 targetPos;
    private Quaternion targetRot;

    private void Start()
    {
        iniPos = transform.localPosition;
        iniRot = transform.localRotation;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
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

        Vector3 posOffset = new Vector3(mouseX * moveAmount.x, mouseY * moveAmount.y, 0f);
        targetPos = iniPos + posOffset;

        float a = -mouseY * rotateAmount.y;
        float b = mouseX * rotateAmount.x;
        Quaternion targetRot = iniRot * Quaternion.Euler(a, b, 0f);

        if (isFloat)
        {
            float idleX = Mathf.Sin(Time.time * floatSpeed.x) * floatAmount.x;
            float idleY = Mathf.Sin(Time.time * floatSpeed.y) * floatAmount.y;
            float idleZ = Mathf.Sin(Time.time * floatSpeed.z) * floatAmount.z;
            targetPos += new Vector3(idleX, idleY, idleZ);
        }

        transform.localPosition = Vector3.Lerp(transform.localPosition, targetPos, Time.deltaTime * smoothTime);
        transform.localRotation = Quaternion.Slerp(transform.localRotation, targetRot, Time.deltaTime * smoothTime);
    }
}
