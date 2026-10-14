using UnityEngine;

public class BGspin : MonoBehaviour
{
    [SerializeField] private float speed = 2f;

    private void Update()
    {
        transform.Rotate(0f,0f,speed * Time.deltaTime);
    }
}
