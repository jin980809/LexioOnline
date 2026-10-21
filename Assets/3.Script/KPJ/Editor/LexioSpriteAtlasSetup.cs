using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;

// Lexio/Setup UI Sprite Atlas: LexioUI 이미지 폴더마다 Sprite Atlas(V2)를 만들어 드로우콜과 메모리를 줄임
// 같은 아틀라스에 든 이미지끼리는 한 번에 그려지고, 크기가 4의 배수가 아닌 이미지도 아틀라스 단위로 압축됨
// 아틀라스에 들어가는 원본 이미지는 압축하지 않음 (압축은 아틀라스에서 한 번만 하도록. 빌드에는 아틀라스만 들어감)
// 다시 실행하면 이미 있는 아틀라스는 새로 만들지 않고 설정만 다시 맞춤
// 한 장짜리 큰 이미지 폴더(background, logo)는 아틀라스에 넣지 않음
public class LexioSpriteAtlasSetup
{
    private const string UiSpriteFolder = "Assets/4.Sprite/LexioUI";
    private const string AtlasFolderName = "Atlas";
    private const string AtlasFolder = UiSpriteFolder + "/" + AtlasFolderName;
    private const int MaxAtlasSize = 2048;
    // 이미지 사이 간격 (픽셀). 늘리거나 줄였을 때 옆 이미지가 번져 보이지 않게 함
    private const int AtlasPadding = 4;

    // 아틀라스로 묶을 폴더 (폴더 하나 = 아틀라스 하나)
    private readonly string[] packFolders =
    {
        "9slice", "avatar", "checkbox", "icon", "icon_button", "match",
        "result", "settings", "text", "text_icon", "timer"
    };

    [MenuItem("Lexio/Setup UI Sprite Atlas")]
    public static void SetupSpriteAtlas()
    {
        new LexioSpriteAtlasSetup().Run();
    }

    private void Run()
    {
        // 기본값(Disabled)이면 아틀라스를 만들어도 쓰이지 않음. 에디터 플레이와 빌드 모두 아틀라스를 쓰도록 켬
        EditorSettings.spritePackerMode = SpritePackerMode.SpriteAtlasV2;
        EnsureAtlasFolder();

        int atlasCount = 0;
        int uncompressedCount = 0;
        for (int i = 0; i < packFolders.Length; i++)
        {
            uncompressedCount += UncompressSources(UiSpriteFolder + "/" + packFolders[i]);
            if (SetupAtlas(packFolders[i]))
            {
                atlasCount++;
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[LexioSpriteAtlasSetup] 아틀라스 " + atlasCount + "개 설정 완료 (" + AtlasFolder + "), 원본 이미지 무압축으로 바꿈 " + uncompressedCount + "개");
    }

    private void EnsureAtlasFolder()
    {
        if (AssetDatabase.IsValidFolder(AtlasFolder) == false)
        {
            AssetDatabase.CreateFolder(UiSpriteFolder, AtlasFolderName);
        }
    }

    // 아틀라스에 들어갈 원본 이미지의 압축을 끔. 압축된 원본을 모아 아틀라스에서 다시 압축하면 화질이 두 번 떨어지기 때문
    // (Unity 경고: "Source Texture ... is using compressed format")
    // 기본 플랫폼 설정만 바꾸고, 9-slice 테두리 등 다른 임포트 설정은 그대로 둠. 바꾼 이미지 수를 돌려줌
    private int UncompressSources(string folderPath)
    {
        int changed = 0;
        if (AssetDatabase.IsValidFolder(folderPath))
        {
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new string[] { folderPath });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                TextureImporter textureImporter = AssetImporter.GetAtPath(path) as TextureImporter;
                if (textureImporter != null && textureImporter.textureCompression != TextureImporterCompression.Uncompressed)
                {
                    textureImporter.textureCompression = TextureImporterCompression.Uncompressed;
                    textureImporter.SaveAndReimport();
                    changed++;
                }
            }
        }
        return changed;
    }

    // 폴더 하나로 아틀라스를 만들거나(없을 때) 설정을 다시 맞춤. 성공하면 true
    private bool SetupAtlas(string folderName)
    {
        bool isDone = false;
        string folderPath = UiSpriteFolder + "/" + folderName;
        string atlasPath = AtlasFolder + "/UI_" + folderName + ".spriteatlasv2";
        Object folder = AssetDatabase.LoadAssetAtPath<Object>(folderPath);
        if (folder == null)
        {
            Debug.LogWarning("[LexioSpriteAtlasSetup] 폴더가 없어 건너뜀: " + folderPath);
        }
        else
        {
            if (AssetDatabase.LoadAssetAtPath<Object>(atlasPath) == null)
            {
                SpriteAtlasAsset atlasAsset = new SpriteAtlasAsset();
                atlasAsset.Add(new Object[] { folder });
                SpriteAtlasAsset.Save(atlasAsset, atlasPath);
                AssetDatabase.ImportAsset(atlasPath);
            }

            SpriteAtlasImporter importer = AssetImporter.GetAtPath(atlasPath) as SpriteAtlasImporter;
            if (importer == null)
            {
                Debug.LogWarning("[LexioSpriteAtlasSetup] 아틀라스 임포터를 찾지 못함: " + atlasPath);
            }
            else
            {
                ApplySettings(importer);
                importer.SaveAndReimport();
                isDone = true;
            }
        }
        return isDone;
    }

    // UI용 설정: 회전/모양 맞춤 패킹 끔 (UI Image는 사각형 그대로 그리므로 켜면 이미지가 깨짐), 밉맵 끔
    private void ApplySettings(SpriteAtlasImporter importer)
    {
        SpriteAtlasPackingSettings packing = importer.packingSettings;
        packing.enableRotation = false;
        packing.enableTightPacking = false;
        packing.padding = AtlasPadding;
        importer.packingSettings = packing;

        SpriteAtlasTextureSettings texture = importer.textureSettings;
        texture.generateMipMaps = false;
        texture.readable = false;
        texture.sRGB = true;
        texture.filterMode = FilterMode.Bilinear;
        importer.textureSettings = texture;

        TextureImporterPlatformSettings platform = importer.GetPlatformSettings("DefaultTexturePlatform");
        platform.maxTextureSize = MaxAtlasSize;
        platform.textureCompression = TextureImporterCompression.Compressed;
        importer.SetPlatformSettings(platform);

        importer.includeInBuild = true;
    }
}
