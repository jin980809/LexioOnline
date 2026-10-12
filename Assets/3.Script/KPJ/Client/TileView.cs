using System.Collections.Generic;
using UnityEngine;

// 화면에 놓인 타일 1개 (클라이언트 전용, 네트워크 오브젝트 아님)
public class TileView : MonoBehaviour
{
    private readonly List<Renderer> renderers = new List<Renderer>();
    private readonly List<Material[]> originalMaterials = new List<Material[]>();
    private readonly List<Material[]> outlinedMaterials = new List<Material[]>();
    private MaterialPropertyBlock propertyBlock;
    private int baseColorId;
    private int colorId;
    private Vector3 basePosition;
    private float liftHeight;
    private Color selectedTint;

    public Tile Tile { get; private set; }
    public bool IsSelected { get; private set; }

    public void Initialize(Tile tile, Material outlineMaterial, Dictionary<Mesh, Material> outlineCache, float lift, Color tint, bool isClickable)
    {
        Tile = tile;
        baseColorId = Shader.PropertyToID("_BaseColor");
        colorId = Shader.PropertyToID("_Color");
        liftHeight = lift;
        selectedTint = tint;
        propertyBlock = new MaterialPropertyBlock();

        GetComponentsInChildren(renderers);
        for (int i = 0; i < renderers.Count; i++)
        {
            Material[] original = renderers[i].sharedMaterials;
            originalMaterials.Add(original);

            // 외곽선은 클릭 가능한 손패에만 필요
            if (isClickable && outlineMaterial != null)
            {
                Material[] outlined = new Material[original.Length + 1];
                for (int j = 0; j < original.Length; j++)
                {
                    outlined[j] = original[j];
                }
                outlined[original.Length] = GetSharedOutline(outlineMaterial, outlineCache, renderers[i]);
                outlinedMaterials.Add(outlined);
            }
            else
            {
                outlinedMaterials.Add(original);
            }
        }

        if (isClickable)
        {
            AddColliderFromRenderers();
        }
    }

    public void SetBasePosition(Vector3 position)
    {
        basePosition = position;
        ApplyPosition();
    }

    public void SetSelected(bool selected)
    {
        IsSelected = selected;
        ApplyPosition();

        for (int i = 0; i < renderers.Count; i++)
        {
            if (selected)
            {
                renderers[i].sharedMaterials = outlinedMaterials[i];
                propertyBlock.Clear();
                propertyBlock.SetColor(baseColorId, selectedTint);
                propertyBlock.SetColor(colorId, selectedTint);
                renderers[i].SetPropertyBlock(propertyBlock);
            }
            else
            {
                renderers[i].sharedMaterials = originalMaterials[i];
                renderers[i].SetPropertyBlock(null);
            }
        }
    }

    private void ApplyPosition()
    {
        if (IsSelected)
        {
            transform.position = basePosition + Vector3.up * liftHeight;
        }
        else
        {
            transform.position = basePosition;
        }
    }

    // 프리팹에 콜라이더가 없어서 렌더러 바운드로 클릭용 BoxCollider 생성
    private void AddColliderFromRenderers()
    {
        bool hasBounds = false;
        Bounds localBounds = new Bounds();
        for (int i = 0; i < renderers.Count; i++)
        {
            MeshFilter meshFilter = renderers[i].GetComponent<MeshFilter>();
            if (meshFilter != null && meshFilter.sharedMesh != null)
            {
                Bounds meshBounds = meshFilter.sharedMesh.bounds;
                Vector3 min = meshBounds.min;
                Vector3 max = meshBounds.max;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 cornerPoint = min;
                    if ((corner & 1) != 0)
                    {
                        cornerPoint.x = max.x;
                    }
                    if ((corner & 2) != 0)
                    {
                        cornerPoint.y = max.y;
                    }
                    if ((corner & 4) != 0)
                    {
                        cornerPoint.z = max.z;
                    }
                    Vector3 worldPoint = meshFilter.transform.TransformPoint(cornerPoint);
                    Vector3 localPoint = transform.InverseTransformPoint(worldPoint);
                    if (hasBounds)
                    {
                        localBounds.Encapsulate(localPoint);
                    }
                    else
                    {
                        localBounds = new Bounds(localPoint, Vector3.zero);
                        hasBounds = true;
                    }
                }
            }
        }

        if (hasBounds)
        {
            BoxCollider boxCollider = gameObject.AddComponent<BoxCollider>();
            boxCollider.center = localBounds.center;
            boxCollider.size = localBounds.size;
        }
    }

    // 같은 메시를 쓰는 타일끼리 외곽선 머티리얼 1개를 공유 (캐시는 LexioClientView가 소유)
    private Material GetSharedOutline(Material outlineMaterial, Dictionary<Mesh, Material> outlineCache, Renderer targetRenderer)
    {
        Mesh mesh = null;
        MeshFilter meshFilter = targetRenderer.GetComponent<MeshFilter>();
        if (meshFilter != null)
        {
            mesh = meshFilter.sharedMesh;
        }

        Material shared = outlineMaterial;
        if (mesh != null)
        {
            if (outlineCache.TryGetValue(mesh, out shared) == false)
            {
                shared = new Material(outlineMaterial);
                shared.SetVector(Shader.PropertyToID("_OutlineCenter"), mesh.bounds.center);
                outlineCache.Add(mesh, shared);
            }
        }
        return shared;
    }
}
