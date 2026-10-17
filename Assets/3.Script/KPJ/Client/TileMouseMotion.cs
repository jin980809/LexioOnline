using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// 로비 배경 3D 타일 모션
// 기본: 타일마다 다른 박자로 둥둥 떠다님
// 마우스를 움직이면: 마우스 방향으로 기울고 반대로 밀리는 패럴랙스가 섞이고, 멈춘 뒤 잠시 지나면 다시 둥둥 상태로 돌아감
// 자식 그룹(LoginTiles, MainTiles 등) 아래의 타일을 모두 대상으로 한다
public class TileMouseMotion : MonoBehaviour
{
    [Header("둥둥 떠다니기 (기본 모션)")]
    [Tooltip("위아래로 떠다니는 높이 (월드 단위)")]
    [SerializeField] private float floatHeight = 0.08f;
    [Tooltip("떠다니는 속도")]
    [SerializeField] private float floatSpeed = 1.1f;
    [Tooltip("떠다니며 흔들리는 회전 각도")]
    [SerializeField] private float floatSwayAngle = 4f;

    [Header("마우스 기울기")]
    [Tooltip("마우스가 화면 끝에 있을 때 타일이 기우는 최대 각도")]
    [SerializeField] private float tiltAngle = 12f;

    [Header("마우스 이동")]
    [Tooltip("마우스가 화면 끝에 있을 때 타일이 움직이는 최대 거리 (월드 단위)")]
    [SerializeField] private float moveDistance = 0.25f;
    [Tooltip("체크하면 마우스 반대 방향으로 움직여 깊이감을 준다")]
    [SerializeField] private bool moveOpposite = true;

    [Header("전환")]
    [Tooltip("값이 클수록 마우스를 빠르게 따라간다")]
    [SerializeField] private float followSpeed = 5f;
    [Tooltip("마우스가 이만큼(픽셀) 이상 움직여야 마우스 모션을 켠다")]
    [SerializeField] private float moveThreshold = 2f;
    [Tooltip("마우스가 멈춘 뒤 둥둥 모션으로 돌아가기까지 기다리는 시간(초)")]
    [SerializeField] private float returnDelay = 1.5f;
    [Tooltip("둥둥 모션과 마우스 모션 사이를 전환하는 속도")]
    [SerializeField] private float blendSpeed = 2f;

    private class TileState
    {
        public Transform tile;
        public Vector3 basePosition;
        public Quaternion baseRotation;
        public float depth;
        public float phase;
    }

    private readonly List<TileState> tiles = new List<TileState>();
    private Vector2 smoothedPointer;
    private Vector2 lastMousePosition;
    private bool hasLastMousePosition;
    private float lastMoveTime = -100f;
    private float mouseWeight;

    private void Awake()
    {
        float scaleSum = 0f;
        foreach (Transform group in transform)
        {
            foreach (Transform tile in group)
            {
                TileState state = new TileState();
                state.tile = tile;
                state.basePosition = tile.localPosition;
                state.baseRotation = tile.localRotation;
                state.depth = tile.localScale.x;
                state.phase = tiles.Count * 1.7f;
                tiles.Add(state);
                scaleSum += tile.localScale.x;
            }
        }

        // 큰 타일(가까워 보이는 타일)일수록 더 많이 움직이도록 평균 크기 기준 비율로 바꾼다
        if (tiles.Count > 0 && scaleSum > 0f)
        {
            float averageScale = scaleSum / tiles.Count;
            for (int i = 0; i < tiles.Count; i++)
            {
                tiles[i].depth = tiles[i].depth / averageScale;
            }
        }
    }

    private void Update()
    {
        UpdatePointer();

        float direction = 1f;
        if (moveOpposite)
        {
            direction = -1f;
        }

        Vector2 pointer = smoothedPointer * Mathf.SmoothStep(0f, 1f, mouseWeight);
        float time = Time.time;

        for (int i = 0; i < tiles.Count; i++)
        {
            TileState state = tiles[i];
            if (state.tile != null && state.tile.gameObject.activeInHierarchy)
            {
                // 둥둥 모션: 위아래 + 살짝 흔들림 (타일마다 박자 다르게)
                float wave = time * floatSpeed + state.phase;
                Vector3 floatOffset = new Vector3(0f, Mathf.Sin(wave) * floatHeight, 0f);
                Quaternion sway = Quaternion.Euler(Mathf.Sin(wave * 0.8f) * floatSwayAngle * 0.5f, 0f, Mathf.Sin(wave * 0.6f + 1f) * floatSwayAngle);

                // 마우스 모션: 앞면이 마우스 쪽을 보도록 기울이고 위치를 민다
                Vector3 mouseOffset = new Vector3(pointer.x, pointer.y, 0f) * (moveDistance * state.depth * direction);
                Quaternion tilt = Quaternion.Euler(pointer.y * tiltAngle, -pointer.x * tiltAngle, 0f);

                state.tile.localPosition = state.basePosition + floatOffset + mouseOffset;
                state.tile.localRotation = tilt * sway * state.baseRotation;
            }
        }
    }

    private void UpdatePointer()
    {
        Vector2 pointer = smoothedPointer;
        Mouse mouse = Mouse.current;
        if (mouse != null && Screen.width > 0 && Screen.height > 0)
        {
            Vector2 mousePosition = mouse.position.ReadValue();
            if (hasLastMousePosition)
            {
                if ((mousePosition - lastMousePosition).magnitude >= moveThreshold)
                {
                    lastMoveTime = Time.time;
                }
            }
            lastMousePosition = mousePosition;
            hasLastMousePosition = true;

            // 화면 중앙 0, 가장자리 -1~1
            pointer = new Vector2(mousePosition.x / Screen.width * 2f - 1f, mousePosition.y / Screen.height * 2f - 1f);
            pointer.x = Mathf.Clamp(pointer.x, -1f, 1f);
            pointer.y = Mathf.Clamp(pointer.y, -1f, 1f);
        }
        smoothedPointer = Vector2.Lerp(smoothedPointer, pointer, 1f - Mathf.Exp(-followSpeed * Time.deltaTime));

        // 최근에 움직였으면 마우스 모션 쪽으로, 오래 멈춰 있으면 둥둥 모션 쪽으로
        float targetWeight = 0f;
        if (Time.time - lastMoveTime < returnDelay)
        {
            targetWeight = 1f;
        }
        mouseWeight = Mathf.MoveTowards(mouseWeight, targetWeight, blendSpeed * Time.deltaTime);
    }
}
