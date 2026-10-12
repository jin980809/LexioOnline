using UnityEngine;

// (문양, 숫자) → 타일 프리팹 매핑. Lexio/Setup Project 메뉴가 자동으로 채움
[CreateAssetMenu(fileName = "TileDatabase", menuName = "Lexio/Tile Database")]
public class TileDatabase : ScriptableObject
{
    [SerializeField] private GameObject[] cloudPrefabs = new GameObject[15];
    [SerializeField] private GameObject[] starPrefabs = new GameObject[15];
    [SerializeField] private GameObject[] moonPrefabs = new GameObject[15];
    [SerializeField] private GameObject[] sunPrefabs = new GameObject[15];
    [SerializeField] private GameObject backPrefab;

    public GameObject BackPrefab
    {
        get { return backPrefab; }
    }

    public GameObject GetPrefab(Tile tile)
    {
        GameObject[] prefabs = GetSuitPrefabs(tile.Suit);
        GameObject prefab = null;
        int index = tile.Number - 1;
        if (index >= 0 && index < prefabs.Length)
        {
            prefab = prefabs[index];
        }
        return prefab;
    }

    public void SetPrefab(TileSuit suit, int number, GameObject prefab)
    {
        GetSuitPrefabs(suit)[number - 1] = prefab;
    }

    public void SetBackPrefab(GameObject prefab)
    {
        backPrefab = prefab;
    }

    private GameObject[] GetSuitPrefabs(TileSuit suit)
    {
        GameObject[] prefabs;
        switch (suit)
        {
            case TileSuit.Cloud:
                prefabs = cloudPrefabs;
                break;
            case TileSuit.Star:
                prefabs = starPrefabs;
                break;
            case TileSuit.Moon:
                prefabs = moonPrefabs;
                break;
            default:
                prefabs = sunPrefabs;
                break;
        }
        return prefabs;
    }
}
