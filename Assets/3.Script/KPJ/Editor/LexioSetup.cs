using System.Collections.Generic;
using kcp2k;
using Mirror;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Lexio/Setup Project: 타일 DB, 외곽선 머티리얼, 네트워크 프리팹, Server/Client 씬 구성을 한 번에 생성
// 다시 실행하면 씬의 NetworkManager / UI / 테이블은 새로 만들어지고, 에셋(프리팹/머티리얼/DB)은 기존 것을 유지
public class LexioSetup
{
    private const string ServerScenePath = "Assets/1.Scene/KPJ/Server.unity";
    private const string ClientScenePath = "Assets/1.Scene/KPJ/Client.unity";
    private const string TilePrefabFolder = "Assets/2.Model/Prefabs";
    private const string NetworkPrefabFolder = "Assets/2.Model/Prefabs/Network";
    private const string DataFolder = "Assets/11.Data";
    private const string TileDatabasePath = DataFolder + "/TileDatabase.asset";
    private const string OutlineMaterialPath = "Assets/6.Materials/Outline/LexioOutline.mat";
    private const string TableMaterialFolder = "Assets/4.Sprite/Map";
    private const string TableMaterialPath = TableMaterialFolder + "/TableFelt.mat";
    private const string HandSlotMaterialFolder = "Assets/6.Materials/Hand";
    private const string HandSlotMaterialPath = HandSlotMaterialFolder + "/HandSlotToon.mat";
    private const string FontPath = "Assets/9.Font/public/static/Pretendard-Regular.otf";
    private const string BoldFontPath = "Assets/9.Font/public/static/Pretendard-Bold.otf";
    private const string RuleBookSpritePath = "Assets/4.Sprite/jokbo_popup.png";
    private const string ChipModelPath = "Assets/2.Model/PokerChip.obj";
    // 카툰 테이블 모델. 없으면 초록 평면으로 대신함
    private const string TableModelPath = "Assets/4.Sprite/LexioTable_Toon/LexioTable.fbx";
    // 테이블 부품 머티리얼 폴더. fbx 임포트 설정의 M_Felt 연결이 깨져 있어서 Setup이 부품마다 직접 지정함
    private const string TableModelMaterialFolder = "Assets/6.Materials/Table";
    // 게임 화면 UI 이미지 폴더 / 글꼴 / 배경. 없는 이미지는 경고만 남기고 기본 모양으로 만듦
    private const string UiSpriteFolder = "Assets/4.Sprite/LexioUI";
    private const string GameFontPath = "Assets/9.Font/Jua-Regular.ttf";
    private const string BackgroundSpritePath = "Assets/4.Sprite/BG_Purple_1920x1080.png";
    private const string ServerAddress = "192.168.1.44";
    private const ushort ServerPort = 7777;

    private Font regularFont;
    private Font boldFont;
    private Font gameFont;

    [MenuItem("Lexio/Setup Project")]
    public static void SetupProject()
    {
        new LexioSetup().Run();
    }

    private void Run()
    {
        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            regularFont = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
            boldFont = AssetDatabase.LoadAssetAtPath<Font>(BoldFontPath);
            gameFont = AssetDatabase.LoadAssetAtPath<Font>(GameFontPath);

            EnsureFolder(NetworkPrefabFolder);
            EnsureFolder(DataFolder);

            CreateTileDatabase();
            CreateOutlineMaterial();
            CreateTableMaterial();
            CreateHandSlotMaterial();
            SetupRuleBookSprite();
            SetupResultSprites();
            CreateNetworkPrefab<LexioPlayer>("LexioPlayer");
            CreateNetworkPrefab<LexioTable>("LexioTable");

            // 씬을 열면 메모리상의 에셋 참조가 끊길 수 있으므로 먼저 디스크에 저장하고,
            // 씬마다 경로로 다시 불러와서 연결
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            SetupServerScene();
            SetupClientScene();

            AssetDatabase.SaveAssets();
            Debug.Log("[Lexio] Setup 완료. 서버 주소 " + ServerAddress + ":" + ServerPort);
        }
    }

    [MenuItem("Lexio/Build Server (Headless)")]
    public static void BuildServer()
    {
        BuildPlayerOptions options = new BuildPlayerOptions();
        options.scenes = new string[] { GameSceneBuilder.ServerScenePath };
        options.locationPathName = "Builds/Server/LexioServer.exe";
        options.target = BuildTarget.StandaloneWindows64;
        options.subtarget = (int)StandaloneBuildSubtarget.Server;
        BuildPipeline.BuildPlayer(options);
    }

    [MenuItem("Lexio/Build Client")]
    public static void BuildClient()
    {
        BuildPlayerOptions options = new BuildPlayerOptions();
        options.scenes = new string[] { GameSceneBuilder.ClientScenePath };
        options.locationPathName = "Builds/Client/LexioClient.exe";
        options.target = BuildTarget.StandaloneWindows64;
        options.subtarget = (int)StandaloneBuildSubtarget.Player;
        BuildPipeline.BuildPlayer(options);
    }

    // ---------------- 에셋 ----------------

    private TileDatabase CreateTileDatabase()
    {
        TileDatabase database = AssetDatabase.LoadAssetAtPath<TileDatabase>(TileDatabasePath);
        if (database == null)
        {
            database = ScriptableObject.CreateInstance<TileDatabase>();
            AssetDatabase.CreateAsset(database, TileDatabasePath);
        }

        string[] folders = { "Cloud", "Star", "Moon", "Sun" };
        // 구름 폴더는 예전 이름(Cloude)인 프로젝트도 있어서, 새 이름에 없으면 예전 이름에서도 찾음
        string[] oldFolders = { "Cloude", "Star", "Moon", "Sun" };
        string[] prefixes = { "Cloud", "Star", "Moon", "Sun" };
        for (int suit = 0; suit < GameRules.SuitCount; suit++)
        {
            for (int number = 1; number <= 15; number++)
            {
                string path = TilePrefabFolder + "/" + folders[suit] + "/" + prefixes[suit] + number + ".prefab";
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                {
                    string oldPath = TilePrefabFolder + "/" + oldFolders[suit] + "/" + prefixes[suit] + number + ".prefab";
                    prefab = AssetDatabase.LoadAssetAtPath<GameObject>(oldPath);
                }
                if (prefab == null)
                {
                    Debug.LogWarning("[Lexio] 타일 프리팹 없음: " + path);
                }
                database.SetPrefab((TileSuit)suit, number, prefab);
            }
        }
        database.SetBackPrefab(AssetDatabase.LoadAssetAtPath<GameObject>(TilePrefabFolder + "/Back.prefab"));
        EditorUtility.SetDirty(database);
        return database;
    }

    private Material CreateOutlineMaterial()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(OutlineMaterialPath);
        if (material == null)
        {
            material = new Material(Shader.Find("Lexio/Outline"));
            AssetDatabase.CreateAsset(material, OutlineMaterialPath);
        }
        return material;
    }

    private Material CreateTableMaterial()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(TableMaterialPath);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor", new Color(0.09f, 0.32f, 0.2f, 1f));
            material.SetFloat("_Smoothness", 0.15f);
            // 폴더가 없으면 에셋 생성이 실패하므로 먼저 만듦
            EnsureFolder(TableMaterialFolder);
            AssetDatabase.CreateAsset(material, TableMaterialPath);
        }
        return material;
    }

    // 드래그 중 손패 칸 표시용 머티리얼 (Lexio/SlotMarker: 둥근 사각형 + 진한 외곽선, 반투명, 양면). 채움 색은 HandView가 칸마다 지정
    private Material CreateHandSlotMaterial()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(HandSlotMaterialPath);
        if (material == null)
        {
            EnsureFolder(HandSlotMaterialFolder);
            material = new Material(Shader.Find("Lexio/SlotMarker"));
            AssetDatabase.CreateAsset(material, HandSlotMaterialPath);
        }
        return material;
    }

    // 족보표 이미지를 UI Image에서 쓸 수 있도록 Sprite로 임포트
    private void SetupRuleBookSprite()
    {
        TextureImporter importer = AssetImporter.GetAtPath(RuleBookSpritePath) as TextureImporter;
        if (importer == null)
        {
            Debug.LogWarning("[Lexio] 족보표 이미지 없음: " + RuleBookSpritePath);
        }
        else if (importer.textureType != TextureImporterType.Sprite)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }
    }

    // 결과 창 줄 배경은 9-slice로 늘려 쓰므로, 임포트 설정에 테두리(Border)가 없으면 넣어 줌
    // row_first는 row_default와 같은 모양이라 같은 값, row_me_highlight는 둥근 모서리 링이라 사방 80
    private void SetupResultSprites()
    {
        SetSpriteBorderIfEmpty(UiSpriteFolder + "/result/row_first.png", new Vector4(65f, 79f, 72f, 65f));
        SetSpriteBorderIfEmpty(UiSpriteFolder + "/result/row_me_highlight.png", new Vector4(80f, 80f, 80f, 80f));
    }

    // border: x = 왼쪽, y = 아래, z = 오른쪽, w = 위 (픽셀). 이미 테두리가 있으면 건드리지 않음
    private void SetSpriteBorderIfEmpty(string path, Vector4 border)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
        {
            Debug.LogWarning("[Lexio] 결과 창 이미지 없음: " + path);
        }
        else if (importer.spriteBorder == Vector4.zero)
        {
            importer.spriteBorder = border;
            importer.SaveAndReimport();
        }
    }

    private GameObject CreateNetworkPrefab<T>(string prefabName) where T : Component
    {
        string path = NetworkPrefabFolder + "/" + prefabName + ".prefab";
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
        {
            GameObject temp = new GameObject(prefabName);
            temp.AddComponent<NetworkIdentity>();
            temp.AddComponent<T>();
            prefab = PrefabUtility.SaveAsPrefabAsset(temp, path);
            Object.DestroyImmediate(temp);
        }
        return prefab;
    }

    // ---------------- 씬 ----------------

    private void SetupServerScene()
    {
        Scene scene = EditorSceneManager.OpenScene(ServerScenePath, OpenSceneMode.Single);
        GameObject playerPrefab = LoadRequired<GameObject>(NetworkPrefabFolder + "/LexioPlayer.prefab");
        GameObject tablePrefab = LoadRequired<GameObject>(NetworkPrefabFolder + "/LexioTable.prefab");

        DestroyRoot("NetworkManager");
        CreateNetworkManager(playerPrefab, tablePrefab, true);
        EditorSceneManager.SaveScene(scene);
    }

    private void SetupClientScene()
    {
        Scene scene = EditorSceneManager.OpenScene(ClientScenePath, OpenSceneMode.Single);
        GameObject playerPrefab = LoadRequired<GameObject>(NetworkPrefabFolder + "/LexioPlayer.prefab");
        GameObject tablePrefab = LoadRequired<GameObject>(NetworkPrefabFolder + "/LexioTable.prefab");
        TileDatabase database = LoadRequired<TileDatabase>(TileDatabasePath);
        Material outlineMaterial = LoadRequired<Material>(OutlineMaterialPath);
        Material tableMaterial = LoadRequired<Material>(TableMaterialPath);
        Sprite ruleBookSprite = LoadRequired<Sprite>(RuleBookSpritePath);
        Material handSlotMaterial = LoadRequired<Material>(HandSlotMaterialPath);
        GameObject chipModel = LoadRequired<GameObject>(ChipModelPath);

        DestroyRoot("NetworkManager");
        // 예전 구조(LexioClientView 하나)로 만든 오브젝트도 지움. 남아 있으면 타일이 두 벌 표시됨
        DestroyRoot("LexioClientView");
        DestroyRoot("LexioTableView");
        DestroyRoot("ChipFlightView");
        DestroyRoot("LexioCanvas");
        DestroyRoot("EventSystem");
        DestroyRoot("Table");
        DestroyRoot("LexioTable");
        DestroyRoot("BGCanvas");

        LexioTableNetworkManager manager = CreateNetworkManager(playerPrefab, tablePrefab, false);

        Camera viewCamera = Camera.main;
        if (viewCamera != null)
        {
            viewCamera.transform.position = new Vector3(0f, 9.5f, -10.5f);
            viewCamera.transform.LookAt(new Vector3(0f, 0f, -1.2f));
        }

        CreateTable(tableMaterial);
        CreateBackground(viewCamera);

        // 3D 테이블 표시: 연결 담당(LexioTableView) + 기능별 컴포넌트를 한 오브젝트에 붙임
        GameObject viewObject = new GameObject("LexioTableView");
        LexioTableView clientView = viewObject.AddComponent<LexioTableView>();
        HandView handView = viewObject.AddComponent<HandView>();
        OpponentView opponentView = viewObject.AddComponent<OpponentView>();
        CenterPlayView centerPlayView = viewObject.AddComponent<CenterPlayView>();
        DealAnimator dealAnimator = viewObject.AddComponent<DealAnimator>();

        SetReference(clientView, "networkManager", manager);
        SetReference(clientView, "tileDatabase", database);
        SetReference(clientView, "outlineMaterial", outlineMaterial);
        SetReference(clientView, "viewCamera", viewCamera);
        SetReference(clientView, "handView", handView);
        SetReference(clientView, "opponentView", opponentView);
        SetReference(clientView, "centerPlayView", centerPlayView);
        SetReference(clientView, "dealAnimator", dealAnimator);

        SetReference(handView, "tableView", clientView);
        SetReference(handView, "centerPlayView", centerPlayView);
        SetReference(handView, "slotMaterial", handSlotMaterial);

        SetReference(opponentView, "tableView", clientView);
        SetReference(opponentView, "handView", handView);

        SetReference(centerPlayView, "tableView", clientView);
        SetReference(centerPlayView, "handView", handView);
        SetReference(centerPlayView, "opponentView", opponentView);

        SetReference(dealAnimator, "tableView", clientView);
        SetReference(dealAnimator, "handView", handView);
        SetReference(dealAnimator, "opponentView", opponentView);

        GameObject chipObject = new GameObject("ChipFlightView");
        ChipFlightView chipView = chipObject.AddComponent<ChipFlightView>();
        SetReference(chipView, "networkManager", manager);
        SetReference(chipView, "clientView", clientView);
        SetReference(chipView, "chipPrefab", chipModel);

        GameObject eventSystem = new GameObject("EventSystem");
        eventSystem.AddComponent<EventSystem>();
        InputSystemUIInputModule inputModule = eventSystem.AddComponent<InputSystemUIInputModule>();
        // 키보드로 버튼이 눌리지 않게 Submit(Enter) / Move(방향키)를 끔
        // 켜 두면 마지막에 클릭한 버튼이 선택된 채로 남아 Enter에 다시 눌리고, 방향키로 옆 버튼에 선택이 옮겨감
        // 마우스 클릭과 입력칸 타이핑에는 영향 없음
        inputModule.submit = null;
        inputModule.move = null;

        CreateUi(manager, clientView, chipView, ruleBookSprite);
        EditorSceneManager.SaveScene(scene);
    }

    private LexioTableNetworkManager CreateNetworkManager(GameObject playerPrefab, GameObject tablePrefab, bool startAsServer)
    {
        GameObject managerObject = new GameObject("NetworkManager");
        KcpTransport transport = managerObject.AddComponent<KcpTransport>();
        transport.port = ServerPort;

        LexioTableNetworkManager manager = managerObject.AddComponent<LexioTableNetworkManager>();
        manager.transport = transport;
        manager.playerPrefab = playerPrefab;
        manager.spawnPrefabs = new List<GameObject> { tablePrefab };
        manager.maxConnections = GameRules.MaxPlayers;
        manager.networkAddress = ServerAddress;
        SetBool(manager, "startAsServer", startAsServer);
        SetReference(manager, "tablePrefab", tablePrefab.GetComponent<LexioTable>());
        return manager;
    }

    // 카툰 테이블 모델이 있으면 그것을, 없으면 초록 평면을 테이블로 씀
    private void CreateTable(Material tableMaterial)
    {
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(TableModelPath);
        if (model != null)
        {
            GameObject table = (GameObject)PrefabUtility.InstantiatePrefab(model);
            PrefabUtility.UnpackPrefabInstance(table, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            table.name = "LexioTable";
            // 모델 윗면이 패를 놓는 높이(y = 0)에 오도록 씬에서 맞춘 값
            // 화면에서 테이블이 위로 치우쳐 보여서 플레이어 쪽(-Z)으로 조금 내림. 패 배치에는 영향 없음
            table.transform.position = new Vector3(0f, -7.55f, -0.5f);
            table.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            table.transform.localScale = new Vector3(10f, 10f, 10f);
            AssignTableMaterials(table.transform);
        }
        else
        {
            Debug.LogWarning("[Lexio] 테이블 모델 없음: " + TableModelPath + " (초록 평면으로 대신함)");
            GameObject table = GameObject.CreatePrimitive(PrimitiveType.Plane);
            table.name = "Table";
            // 화면에서 테이블이 위로 치우쳐 보여서 플레이어 쪽(-Z)으로 조금 내림. 패 배치에는 영향 없음
            table.transform.position = new Vector3(0f, 0f, -1f);
            table.transform.localScale = new Vector3(1.8f, 1f, 1.6f);
            Object.DestroyImmediate(table.GetComponent<Collider>());
            table.GetComponent<MeshRenderer>().sharedMaterial = tableMaterial;
        }
    }

    // 테이블 부품(자식 오브젝트 이름)별 머티리얼. 씬에서 맞춘 값
    private void AssignTableMaterials(Transform table)
    {
        string[] partNames =
        {
            "Felt",
            "Base_Bottom", "Base_Side", "Pedestal_Collar", "Pedestal_Column", "Rim_BlueBand", "Rim_SideApron",
            "Base_BottomTrim", "Pedestal_WaistRing", "Rim_YellowLip",
            "Base_TopTrim", "Rim_CreamBand"
        };
        string[] materialNames =
        {
            "M_Felt",
            "M_BluePanel", "M_BluePanel", "M_BluePanel", "M_BluePanel", "M_BluePanel", "M_BluePanel",
            "M_Yellow", "M_Yellow", "M_Yellow",
            "M_Cream", "M_Cream"
        };

        for (int i = 0; i < partNames.Length; i++)
        {
            Transform part = table.Find(partNames[i]);
            Material material = LoadRequired<Material>(TableModelMaterialFolder + "/" + materialNames[i] + ".mat");
            if (part == null)
            {
                Debug.LogWarning("[Lexio] 테이블 부품 없음: " + partNames[i]);
            }
            else if (material != null)
            {
                MeshRenderer partRenderer = part.GetComponent<MeshRenderer>();
                if (partRenderer != null)
                {
                    partRenderer.sharedMaterial = material;
                }
            }
        }
    }

    // 화면 전체 배경. 카메라 기준 캔버스를 3D 테이블 뒤(카메라에서 50)에 그림
    // GraphicRaycaster를 붙이지 않음: 배경이 클릭을 받으면 HandView가 항상 UI 위라고 판단해 패를 고를 수 없음
    private void CreateBackground(Camera viewCamera)
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(BackgroundSpritePath);
        if (sprite == null || viewCamera == null)
        {
            Debug.LogWarning("[Lexio] 배경 이미지 또는 Main Camera 없음: 배경을 만들지 않습니다. " + BackgroundSpritePath);
        }
        else
        {
            GameObject canvasObject = new GameObject("BGCanvas");
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = viewCamera;
            canvas.planeDistance = 50f;
            Image background = CreateImage("Background", canvasObject.transform, sprite, Stretch());
            background.color = new Color(1f, 0f, 0.74f, 1f);
        }
    }

    // ---------------- UI ----------------

    private void CreateUi(LexioTableNetworkManager manager, LexioTableView clientView, ChipFlightView chipView, Sprite ruleBookSprite)
    {
        GameObject canvasObject = new GameObject("LexioCanvas");
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasObject.AddComponent<GraphicRaycaster>();
        Transform root = canvasObject.transform;

        LexioUI ui = canvasObject.AddComponent<LexioUI>();
        SetReference(ui, "networkManager", manager);
        SetReference(ui, "clientView", clientView);
        SetReference(ui, "chipView", chipView);

        // 접속 화면
        Image connectPanel = CreateStretchPanel("ConnectPanel", root, new Color(0.05f, 0.08f, 0.07f, 0.92f));
        CreateText("Title", connectPanel.transform, "LEXIO", 120, TextAnchor.MiddleCenter, Center(new Vector2(0f, 220f), new Vector2(800f, 160f)), true);
        InputField nicknameInput = CreateInputField("NicknameInput", connectPanel.transform, "닉네임", Center(new Vector2(0f, 40f), new Vector2(520f, 80f)));
        Button connectButton = CreateButton("ConnectButton", connectPanel.transform, "접속", Center(new Vector2(0f, -70f), new Vector2(520f, 80f)));
        Text connectStatus = CreateText("StatusText", connectPanel.transform, "", 30, TextAnchor.MiddleCenter, Center(new Vector2(0f, -170f), new Vector2(1200f, 60f)), false);
        SetReference(ui, "connectPanel", connectPanel.gameObject);
        SetReference(ui, "nicknameInput", nicknameInput);
        SetReference(ui, "connectButton", connectButton);
        SetReference(ui, "connectStatusText", connectStatus);

        // 대기실
        Image lobbyPanel = CreatePanel("LobbyPanel", root, new Color(0f, 0f, 0f, 0.75f), Center(Vector2.zero, new Vector2(640f, 620f)));
        Text lobbyPlayers = CreateText("PlayersText", lobbyPanel.transform, "", 36, TextAnchor.UpperCenter, Center(new Vector2(0f, 60f), new Vector2(560f, 440f)), false);
        Button startButton = CreateButton("StartButton", lobbyPanel.transform, "게임 시작", Center(new Vector2(0f, -240f), new Vector2(420f, 90f)));
        SetReference(ui, "lobbyPanel", lobbyPanel.gameObject);
        SetReference(ui, "lobbyPlayersText", lobbyPlayers);
        SetReference(ui, "startButton", startButton);

        // 게임 화면
        // 이미지는 UiSpriteFolder 기준. 9-slice 이미지의 마지막 숫자(Pixels Per Unit Multiplier)가 클수록 테두리가 얇아짐
        Sprite creamSprite = LoadSprite("9slice/panel_cream.png");
        Sprite pillSprite = LoadSprite("9slice/panel_pill_cream.png");
        Sprite pillHighlightSprite = LoadSprite("9slice/panel_pill_highlight.png");
        Sprite profileFrameSprite = LoadSprite("avatar/avatar_ring.png");
        Sprite chipIcon = LoadSprite("icon/icon_chip.png");
        Sprite tileIcon = LoadSprite("icon/icon_tile_stack.png");
        Sprite checkSprite = LoadSprite("icon/icon_check.png");
        Sprite bubbleSprite = LoadSprite("icon/bubble_body.png");
        Sprite bubbleTailSprite = LoadSprite("icon/bubble_tail.png");
        if (bubbleSprite == null)
        {
            bubbleSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        }

        RectTransform gamePanel = CreateRect("GamePanel", root, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

        // 왼쪽 위 라운드 배지
        Image roundBadge = CreateSlicedImage("Round", gamePanel, LoadSprite("9slice/badge_cream_small.png"), 1f, Anchored(new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), new Vector2(165.7f, -74.3f), new Vector2(280f, 100f)));
        Text roundText = CreateGameText("RoundText", roundBadge.transform, 36, TextAnchor.MiddleCenter, Spanned(new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, 1f), new Vector2(0f, 42.5f), new Vector2(0f, 72.5f)), true);

        // 가운데 위 차례 배너 (흰 글자 + 검은 외곽선)
        Image turnBanner = CreateSlicedImage("Turn", gamePanel, LoadSprite("9slice/banner_coral.png"), 1.5f, Anchored(new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -74.6f), new Vector2(399.5f, 109.2f)));
        Text turnText = CreateGameText("TurnText", turnBanner.transform, 44, TextAnchor.MiddleCenter, Anchored(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), new Vector2(0f, 49.7f), new Vector2(600f, 89.8f)), false);
        turnText.color = Color.white;
        AddOutline(turnText, new Vector2(3f, -3f));
        AddOutline(turnText, new Vector2(1.21f, -1f));
        AddOutline(turnText, new Vector2(-0.05f, 0.04f));

        // 차례 배너 아래 준비 인원 ("준비 2 / 4"). 패 정리 시간에만 LexioUI가 켬
        Image readyPanel = CreateSlicedImage("Ready", gamePanel, creamSprite, 2.5f, Anchored(new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -119f), new Vector2(147.9f, 59.2f)));
        Text readyCountText = CreateGameText("ReadyText", readyPanel.transform, 30, TextAnchor.MiddleCenter, Anchored(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), new Vector2(0f, 27.5f), new Vector2(147.9f, 51.6f)), false);
        readyPanel.gameObject.SetActive(false);

        // 가운데 낸 패 정보 (흰 글자 + 검은 외곽선, 선택 정보 글자와 같은 모양). 맞은편 상대 패와 겹치지 않게 가운데 패 바로 아래
        Text lastPlayText = CreateGameText("LastPlayText", gamePanel, 34, TextAnchor.MiddleCenter, Center(new Vector2(0f, -30f), new Vector2(900f, 60f)), false);
        lastPlayText.color = Color.white;
        AddOutline(lastPlayText, new Vector2(1f, -3f));
        AddOutline(lastPlayText, new Vector2(0.73f, -1f));
        AddOutline(lastPlayText, new Vector2(1.57f, -1f));

        // 하단 줄 (왼쪽부터): 선택 해제 / 문양 정렬 / 숫자 정렬 / 제출 / 패스. 글자가 그려진 이미지 버튼
        // 한 줄이 같이 움직이도록 모두 화면 아래 가운데 기준 (x는 버튼 가운데 위치). 화면 비율이 바뀌어도 간격 유지
        Vector2 buttonSize = new Vector2(236.7f, 96.8f);
        Vector2 bottomCenter = new Vector2(0.5f, 0f);
        Button clearSelectionButton = CreateSpriteButton("ClearSelectionButton", gamePanel, "text_icon/btn_select_clear", "선택 해제", Anchored(bottomCenter, bottomCenter, new Vector2(-401.65f, 30f), buttonSize));
        Button sortSuitButton = CreateSpriteButton("SortSuitButton", gamePanel, "text_icon/btn_sort_suit", "문양 정렬", Anchored(bottomCenter, bottomCenter, new Vector2(-143.05f, 30f), buttonSize));
        Button sortNumberButton = CreateSpriteButton("SortNumberButton", gamePanel, "text_icon/btn_sort_number", "숫자 정렬", Anchored(bottomCenter, bottomCenter, new Vector2(115.65f, 30f), buttonSize));
        Button submitButton = CreateSpriteButton("SubmitButton", gamePanel, "text_icon/btn_submit", "제출", Anchored(bottomCenter, bottomCenter, new Vector2(374.25f, 30f), buttonSize));
        Button passButton = CreateSpriteButton("PassButton", gamePanel, "text_icon/btn_pass", "패스", Anchored(bottomCenter, bottomCenter, new Vector2(632.85f, 30f), buttonSize));
        // 패 정리 시간에만 켜짐 (그동안 꺼지는 제출 버튼 자리에 같은 크기로 표시)
        Button readyButton = CreateSpriteButton("ReadyButton", gamePanel, "text_icon/btn_ready", "준비 완료", Anchored(bottomCenter, bottomCenter, new Vector2(374.25f, 30f), buttonSize));
        readyButton.gameObject.SetActive(false);

        // 패 위 중앙: 선택한 패 정보 (흰 글자 + 검은 외곽선)
        // 3D 손패 위치를 따라가도록 화면 가운데 기준 (3D 오브젝트는 화면 높이에 비례해 가운데 기준으로 움직임)
        Text selectionText = CreateGameText("SelectionText", gamePanel, 32, TextAnchor.MiddleCenter, Anchored(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0f), new Vector2(0f, -145f), new Vector2(700f, 50f)), false);
        selectionText.color = Color.white;
        AddOutline(selectionText, new Vector2(1f, -3f));
        AddOutline(selectionText, new Vector2(0.73f, -1f));
        AddOutline(selectionText, new Vector2(1.57f, -1f));

        // 오른쪽 위 설정 버튼 (기능 미구현, 버튼만 배치)
        CreateSpriteButton("SettingsButton", gamePanel, "icon_button/btn_settings", "설정", Corner(Vector2.one, new Vector2(-48f, -39f), new Vector2(121.8f, 131.5f)));

        // 매 프레임 바뀌는 UI(상대 이름표, 내 정보, 타이머)만 따로 묶는 자식 캔버스
        // UI가 바뀌면 그 UI가 속한 캔버스 전체를 다시 그리므로, 버튼/배지가 있는 LexioCanvas가 매 프레임 다시 그려지지 않게 나눔
        // 크기와 위치는 GamePanel과 같음(전체). 안의 UI는 모두 클릭을 받지 않으므로 GraphicRaycaster는 붙이지 않음
        // 족보표 서랍보다 먼저 만들어 서랍이 이 위에 그려짐
        RectTransform dynamicPanel = CreateRect("DynamicCanvas", gamePanel, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        dynamicPanel.gameObject.AddComponent<Canvas>();

        // 상대 이름표: 왼쪽 프로필, 오른쪽 윗줄 이름, 아랫줄 칩/남은 패, 오른쪽 위 준비 표시
        RectTransform[] seatLabels = new RectTransform[GameRules.MaxPlayers];
        Text[] seatNameTexts = new Text[GameRules.MaxPlayers];
        Text[] seatChipTexts = new Text[GameRules.MaxPlayers];
        Text[] seatTileTexts = new Text[GameRules.MaxPlayers];
        Image[] seatProfileImages = new Image[GameRules.MaxPlayers];
        GameObject[] seatReadyIcons = new GameObject[GameRules.MaxPlayers];
        GameObject[] seatBubbles = new GameObject[GameRules.MaxPlayers];
        Text[] seatBubbleTexts = new Text[GameRules.MaxPlayers];
        for (int i = 0; i < GameRules.MaxPlayers; i++)
        {
            Image label = CreateSlicedImage("SeatLabel" + i, dynamicPanel, pillSprite, 1.39f, Center(Vector2.zero, new Vector2(345.8f, 123.9f)));
            seatLabels[i] = label.rectTransform;
            seatNameTexts[i] = CreateGameText("Name", label.transform, 35, TextAnchor.MiddleLeft, Spanned(Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(29.4f, 24.5f), new Vector2(-188.2f, -77.8f)), false);
            // 상대 말풍선: 이름표 바로 위 (이름표 자식이라 같이 움직이고 같이 꺼짐)
            seatBubbleTexts[i] = CreateBubble("Bubble", label.transform, Anchored(new Vector2(0.5f, 1f), new Vector2(0.5f, 0f), new Vector2(0f, 11.4f), Vector2.zero), bubbleSprite, bubbleTailSprite);
            seatBubbles[i] = seatBubbleTexts[i].transform.parent.gameObject;
            seatProfileImages[i] = CreateImage("Profil", label.transform, profileFrameSprite, Center(new Vector2(-104.3f, 2.9f), new Vector2(89.4f, 89.4f)));
            seatChipTexts[i] = CreateStatRow(label.transform, new Vector2(-7.5f, -19.8f), true, chipIcon);
            seatTileTexts[i] = CreateStatRow(label.transform, new Vector2(99.5f, -19.8f), false, tileIcon);
            Image readyIcon = CreateImage("ReadyIcon", label.transform, checkSprite, Center(new Vector2(127.7f, 23.1f), new Vector2(39f, 39f)));
            readyIcon.gameObject.SetActive(false);
            seatReadyIcons[i] = readyIcon.gameObject;
        }

        // 내 정보: 화면 왼쪽 아래, 그 오른쪽은 3D 패 (HandView.handCenter로 위치 맞춤)
        Image myInfo = CreateSlicedImage("MyInfo", dynamicPanel, creamSprite, 1f, Anchored(Vector2.zero, new Vector2(0.5f, 0.5f), new Vector2(242f, 228.6f), new Vector2(344.6f, 167.8f)));
        Text myNameText = CreateGameText("Name", myInfo.transform, 40, TextAnchor.MiddleLeft, Corner(Vector2.zero, new Vector2(126f, 82.6f), new Vector2(144.7f, 56.9f)), false);
        Image myProfileImage = CreateImage("Profil_Image", myInfo.transform, profileFrameSprite, Center(new Vector2(-106f, 7.5f), new Vector2(100f, 100f)));
        Text myChipText = CreateStatRow(myInfo.transform, new Vector2(-1.9f, -21.25f), true, chipIcon);
        Text myTileText = CreateStatRow(myInfo.transform, new Vector2(105.1f, -21.25f), false, tileIcon);
        // 준비 완료 표시: 이름 줄 오른쪽 끝 (상대 이름표의 ReadyIcon과 같은 자리)
        Image myReadyIcon = CreateImage("ReadyIcon", myInfo.transform, checkSprite, Center(new Vector2(127.3f, 27f), new Vector2(39f, 39f)));
        myReadyIcon.gameObject.SetActive(false);
        // 내 정산 계산식 말풍선: 내 정보 바로 위
        Text myBubbleText = CreateBubble("Bubble", myInfo.transform, Anchored(new Vector2(0.5f, 1f), new Vector2(0.5f, 0f), new Vector2(0f, 11.4f), Vector2.zero), bubbleSprite, bubbleTailSprite);

        // 오른쪽 아래 타이머: 원형 게이지(남은 시간 비율) + 남은 초
        Image timer = CreateImage("Timer", dynamicPanel, LoadSprite("timer/timer_bg.png"), Anchored(new Vector2(1f, 0f), new Vector2(0.5f, 0.5f), new Vector2(-108.9f, 85f), new Vector2(138.2f, 145.6f)));
        Slider timerSlider = CreateTimerSlider(timer.transform, LoadSprite("timer/timer_fill.png"));
        Text timerText = CreateGameText("TimerText", timer.transform, 50, TextAnchor.MiddleCenter, Anchored(new Vector2(1f, 0f), Vector2.zero, new Vector2(-169.1f, 26.1f), new Vector2(200f, 80f)), false);

        ActionBubbleView bubbleView = canvasObject.AddComponent<ActionBubbleView>();
        SetReference(bubbleView, "networkManager", manager);
        SetReferenceArray(bubbleView, "seatBubbles", seatBubbles);
        SetReferenceArray(bubbleView, "seatBubbleTexts", seatBubbleTexts);
        SetReference(bubbleView, "myBubble", myBubbleText.transform.parent.gameObject);
        SetReference(bubbleView, "myBubbleText", myBubbleText);

        // 족보표 서랍: 화면 오른쪽 끝 바깥에 대기(높이 = 화면의 75% - 186), 왼쪽에 붙은 탭 버튼으로 열고 닫음
        // 게임 화면의 마지막 자식이라 다른 게임 UI보다 위에 그려짐
        RectTransform ruleBook = CreateRect("RuleBook", gamePanel, new Vector2(1f, 0.125f), new Vector2(1f, 0.875f), new Vector2(0f, 0.5f), new Vector2(0f, 149f), new Vector2(0f, -186.3f));
        Image ruleBookImage = ruleBook.gameObject.AddComponent<Image>();
        ruleBookImage.sprite = ruleBookSprite;
        ruleBookImage.preserveAspect = true;
        AspectRatioFitter ruleBookFitter = ruleBook.gameObject.AddComponent<AspectRatioFitter>();
        ruleBookFitter.aspectMode = AspectRatioFitter.AspectMode.HeightControlsWidth;
        if (ruleBookSprite != null)
        {
            ruleBookFitter.aspectRatio = ruleBookSprite.rect.width / ruleBookSprite.rect.height;
        }

        Layout ruleBookButtonLayout = Spanned(new Vector2(0f, 0.3f), new Vector2(0f, 0.3f), new Vector2(1f, 0.5f), new Vector2(0f, 177f), new Vector2(79.4f, 250.3f));
        Button ruleBookButton = CreateSpriteButton("ToggleButton", ruleBook, "text/tab_jokbo", "족\n보\n표", ruleBookButtonLayout);

        RuleBookPanel ruleBookPanel = ruleBook.gameObject.AddComponent<RuleBookPanel>();
        SetReference(ruleBookPanel, "toggleButton", ruleBookButton);

        Sprite[] profileSprites = new Sprite[5];
        for (int i = 0; i < profileSprites.Length; i++)
        {
            profileSprites[i] = LoadSprite("avatar/avatar_default_" + (i + 1) + ".png");
        }

        SetReference(ui, "gamePanel", gamePanel.gameObject);
        SetReference(ui, "roundText", roundText);
        SetReference(ui, "turnText", turnText);
        SetReference(ui, "readyPanel", readyPanel.gameObject);
        SetReference(ui, "readyCountText", readyCountText);
        SetReference(ui, "timerText", timerText);
        SetReference(ui, "timerSlider", timerSlider);
        SetReference(ui, "lastPlayText", lastPlayText);
        SetReference(ui, "selectionText", selectionText);
        SetReference(ui, "myNameText", myNameText);
        SetReference(ui, "myChipText", myChipText);
        SetReference(ui, "myTileText", myTileText);
        SetReference(ui, "myProfileImage", myProfileImage);
        SetReference(ui, "myReadyIcon", myReadyIcon.gameObject);
        SetReference(ui, "myInfoPanel", myInfo);
        SetReference(ui, "timerWarningSprite", LoadSprite("timer/timer_fill_warning.png"));
        SetReference(ui, "timerRoot", timer.rectTransform);
        SetReference(ui, "clearSelectionButton", clearSelectionButton);
        SetReference(ui, "sortSuitButton", sortSuitButton);
        SetReference(ui, "sortNumberButton", sortNumberButton);
        SetReference(ui, "passButton", passButton);
        SetReference(ui, "submitButton", submitButton);
        SetReference(ui, "readyButton", readyButton);
        SetReferenceArray(ui, "seatLabels", seatLabels);
        SetReferenceArray(ui, "seatNameTexts", seatNameTexts);
        SetReferenceArray(ui, "seatChipTexts", seatChipTexts);
        SetReferenceArray(ui, "seatTileTexts", seatTileTexts);
        SetReferenceArray(ui, "seatProfileImages", seatProfileImages);
        SetReferenceArray(ui, "seatReadyIcons", seatReadyIcons);
        SetReference(ui, "normalLabelSprite", pillSprite);
        SetReference(ui, "turnLabelSprite", pillHighlightSprite);
        SetReferenceArray(ui, "labelAvoidRects", new Object[] { turnBanner.rectTransform, readyPanel.rectTransform });
        SetReferenceArray(ui, "profileSprites", profileSprites);
        SetReference(ui, "bubbleView", bubbleView);

        // 최종 결과: 화면을 어둡게 덮고 가운데 결과 창 (제목 배너 + 순위 줄 5개 + 나가기)
        // 결과 창 이미지는 UiSpriteFolder/result. 줄 배경은 9-slice (테두리는 SetupResultSprites에서 넣음)
        Image resultPanel = CreatePanel("ResultPanel", root, new Color(0f, 0f, 0f, 0.6f), Stretch());
        Image lightRays = CreateImage("LightRays", resultPanel.transform, LoadSprite("result/fx_light_rays.png"), Center(new Vector2(0f, 444f), new Vector2(1400f, 1400f)));
        lightRays.color = new Color(1f, 1f, 1f, 0.25f);
        CreateSlicedImage("Panel", resultPanel.transform, LoadSprite("result/panel_result.png"), 1.5f, Center(new Vector2(0f, 8.5f), new Vector2(870f, 873f)));
        Image resultTitle = CreateImage("Title", resultPanel.transform, LoadSprite("result/banner_result_text.png"), Center(new Vector2(0f, 444f), new Vector2(416f, 111f)));
        resultTitle.preserveAspect = true;
        Text resultSubtitle = CreateGameText("SubtitleText", resultPanel.transform, 26, TextAnchor.MiddleCenter, Center(new Vector2(0f, 351f), new Vector2(800f, 40f)), false);
        resultSubtitle.color = new Color(0.45f, 0.38f, 0.55f, 1f);

        // 내 줄 테두리 (줄보다 먼저 만들어 줄 뒤에 그려짐. 위치/크기는 ResultPanelView가 내 줄에 맞춤)
        Image meHighlight = CreateSlicedImage("MeHighlight", resultPanel.transform, LoadSprite("result/row_me_highlight.png"), 2f, Center(Vector2.zero, new Vector2(784f, 124f)));
        meHighlight.gameObject.SetActive(false);

        Sprite firstRowSprite = LoadSprite("result/row_first.png");
        Sprite defaultRowSprite = LoadSprite("result/row_default.png");
        Sprite[] rankBadgeSprites = new Sprite[GameRules.MaxPlayers];
        for (int i = 0; i < rankBadgeSprites.Length; i++)
        {
            rankBadgeSprites[i] = LoadSprite("result/rank_badge_" + (i + 1) + ".png");
        }
        Sprite meTagSprite = LoadSprite("result/tag_me.png");

        // 줄 칸들 (ResultPanelView.ResultRow의 필드 순서대로 모아서 한 번에 연결)
        GameObject[] rowRoots = new GameObject[GameRules.MaxPlayers];
        Image[] rowBackgrounds = new Image[GameRules.MaxPlayers];
        Image[] rowBadges = new Image[GameRules.MaxPlayers];
        Image[] rowProfiles = new Image[GameRules.MaxPlayers];
        Text[] rowNames = new Text[GameRules.MaxPlayers];
        RectTransform[] rowMeTags = new RectTransform[GameRules.MaxPlayers];
        Text[] rowChanges = new Text[GameRules.MaxPlayers];
        Text[] rowChips = new Text[GameRules.MaxPlayers];
        for (int i = 0; i < GameRules.MaxPlayers; i++)
        {
            // 1위 줄만 조금 넓음. 줄 간격 116
            float rowWidth = 766f;
            Sprite rowSprite = defaultRowSprite;
            if (i == 0)
            {
                rowWidth = 794f;
                rowSprite = firstRowSprite;
            }
            Image row = CreateSlicedImage("Row" + (i + 1), resultPanel.transform, rowSprite, 2f, Center(new Vector2(0f, 265f - 116f * i), new Vector2(rowWidth, 108f)));
            rowRoots[i] = row.gameObject;
            rowBackgrounds[i] = row;

            // 왼쪽부터: 순위 배지 / 프로필 / 이름(+나 표시)
            rowBadges[i] = CreateImage("RankBadge", row.transform, rankBadgeSprites[Mathf.Min(i, rankBadgeSprites.Length - 1)], Anchored(new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(55f, 2f), new Vector2(66f, 66f)));
            rowProfiles[i] = CreateImage("Profil", row.transform, profileSprites[0], Anchored(new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(145f, 2f), new Vector2(72f, 72f)));
            rowNames[i] = CreateGameText("Name", row.transform, 40, TextAnchor.MiddleLeft, Anchored(new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(202f, 2f), new Vector2(330f, 60f)), false);
            rowNames[i].color = new Color(0.14f, 0.09f, 0.22f, 1f);
            Image meTag = CreateImage("MeTag", row.transform, meTagSprite, Anchored(new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(330f, 2f), new Vector2(46f, 31f)));
            meTag.preserveAspect = true;
            meTag.gameObject.SetActive(false);
            rowMeTags[i] = meTag.rectTransform;

            // 오른쪽부터: 최종 칩 / 칩 아이콘 / 칩 증감
            rowChanges[i] = CreateGameText("ChangeText", row.transform, 28, TextAnchor.MiddleCenter, Anchored(new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-226f, 2f), new Vector2(110f, 40f)), false);
            CreateImage("Coin_Icon", row.transform, chipIcon, Anchored(new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-110f, 2f), new Vector2(42f, 42f)));
            rowChips[i] = CreateGameText("ChipText", row.transform, 44, TextAnchor.MiddleLeft, Anchored(new Vector2(1f, 0.5f), new Vector2(0f, 0.5f), new Vector2(-84f, 2f), new Vector2(80f, 60f)), false);
            rowChips[i].color = new Color(0.14f, 0.09f, 0.22f, 1f);

            if (i == 0)
            {
                // 1위 줄 왼쪽 위 모서리에 걸친 왕관
                Image crown = CreateImage("Crown", row.transform, LoadSprite("result/icon_crown_big.png"), Anchored(new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), new Vector2(40f, 2f), new Vector2(64f, 51f)));
                crown.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 15f);
            }
        }

        Button exitButton = CreateSpriteButton("ExitButton", resultPanel.transform, "result/btn_exit", "나가기", Center(new Vector2(0f, -349f), new Vector2(296f, 102f)));

        ResultPanelView resultView = resultPanel.gameObject.AddComponent<ResultPanelView>();
        SetResultRows(resultView, rowRoots, rowBackgrounds, rowBadges, rowProfiles, rowNames, rowMeTags, rowChanges, rowChips);
        SetReference(resultView, "firstRowSprite", firstRowSprite);
        SetReference(resultView, "defaultRowSprite", defaultRowSprite);
        SetReferenceArray(resultView, "rankBadgeSprites", rankBadgeSprites);
        SetReference(resultView, "meHighlight", meHighlight.rectTransform);
        SetReference(resultView, "subtitleText", resultSubtitle);
        SetReference(resultView, "lightRays", lightRays.rectTransform);

        SetReference(ui, "resultPanel", resultPanel.gameObject);
        SetReference(ui, "resultView", resultView);
        SetReference(ui, "exitButton", exitButton);

        // 알림 (항상 맨 위). 대기실 알림("N명 이상 모여야...")도 보이도록 게임 화면 밖에 둠
        // 손패를 가리지 않도록 가운데 패와 맞은편 상대 패 사이에 표시 (3D 패 위치를 따라가도록 화면 가운데 기준)
        Text noticeText = CreateGameText("NoticeText", root, 36, TextAnchor.MiddleCenter, Anchored(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), new Vector2(0f, 142f), new Vector2(1200f, 60f)), false);
        SetReference(ui, "noticeText", noticeText);

        lobbyPanel.gameObject.SetActive(false);
        gamePanel.gameObject.SetActive(false);
        resultPanel.gameObject.SetActive(false);
    }

    // 위치 지정용: 앵커(=피벗) 기준 위치/크기
    private struct Layout
    {
        public Vector2 anchorMin;
        public Vector2 anchorMax;
        public Vector2 pivot;
        public Vector2 position;
        public Vector2 size;
    }

    private Layout Center(Vector2 position, Vector2 size)
    {
        return Corner(new Vector2(0.5f, 0.5f), position, size);
    }

    private Layout Corner(Vector2 anchor, Vector2 position, Vector2 size)
    {
        Layout layout = new Layout();
        layout.anchorMin = anchor;
        layout.anchorMax = anchor;
        layout.pivot = anchor;
        layout.position = position;
        layout.size = size;
        return layout;
    }

    // 앵커와 피벗이 다를 때 사용
    private Layout Anchored(Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
    {
        Layout layout = Corner(anchor, position, size);
        layout.pivot = pivot;
        return layout;
    }

    private Layout Stretch()
    {
        Layout layout = new Layout();
        layout.anchorMin = Vector2.zero;
        layout.anchorMax = Vector2.one;
        layout.pivot = new Vector2(0.5f, 0.5f);
        layout.position = Vector2.zero;
        layout.size = Vector2.zero;
        return layout;
    }

    // 앵커 최소/최대가 다를 때 사용 (한 방향으로 늘어나는 칸)
    private Layout Spanned(Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size)
    {
        Layout layout = new Layout();
        layout.anchorMin = anchorMin;
        layout.anchorMax = anchorMax;
        layout.pivot = pivot;
        layout.position = position;
        layout.size = size;
        return layout;
    }

    private RectTransform CreateRect(string objectName, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size)
    {
        GameObject gameObject = new GameObject(objectName, typeof(RectTransform));
        RectTransform rect = gameObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    private RectTransform CreateRect(string objectName, Transform parent, Layout layout)
    {
        return CreateRect(objectName, parent, layout.anchorMin, layout.anchorMax, layout.pivot, layout.position, layout.size);
    }

    private Image CreatePanel(string objectName, Transform parent, Color color, Layout layout)
    {
        RectTransform rect = CreateRect(objectName, parent, layout);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        return image;
    }

    private Image CreateStretchPanel(string objectName, Transform parent, Color color)
    {
        return CreatePanel(objectName, parent, color, Stretch());
    }

    private Text CreateText(string objectName, Transform parent, string content, int fontSize, TextAnchor alignment, Layout layout, bool isBold)
    {
        RectTransform rect = CreateRect(objectName, parent, layout);
        Text text = rect.gameObject.AddComponent<Text>();
        if (isBold && boldFont != null)
        {
            text.font = boldFont;
        }
        else
        {
            text.font = regularFont;
        }
        text.text = content;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = Color.white;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        return text;
    }

    private Button CreateButton(string objectName, Transform parent, string label, Layout layout)
    {
        Image image = CreatePanel(objectName, parent, Color.white, layout);
        Button button = image.gameObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.85f, 0.65f, 0.25f, 1f);
        colors.highlightedColor = new Color(0.95f, 0.75f, 0.35f, 1f);
        colors.pressedColor = new Color(0.7f, 0.5f, 0.2f, 1f);
        colors.selectedColor = colors.normalColor;
        colors.disabledColor = new Color(0.35f, 0.35f, 0.35f, 0.8f);
        button.colors = colors;

        Text text = CreateText("Label", image.transform, label, 34, TextAnchor.MiddleCenter, Stretch(), true);
        text.color = new Color(0.1f, 0.08f, 0.05f, 1f);
        return button;
    }

    // 말풍선: 9-slice 본체 + 아래 가운데 꼬리. 가로/세로 크기는 글자에 맞춰 자동 (layout의 size는 무시됨)
    // 반환값은 글자. 말풍선 본체는 글자의 부모
    private Text CreateBubble(string objectName, Transform parent, Layout layout, Sprite bubbleSprite, Sprite tailSprite)
    {
        Image body = CreateSlicedImage(objectName, parent, bubbleSprite, 2f, layout);

        HorizontalLayoutGroup group = body.gameObject.AddComponent<HorizontalLayoutGroup>();
        group.padding = new RectOffset(24, 24, 10, 10);
        group.childAlignment = TextAnchor.MiddleCenter;
        group.childControlWidth = true;
        group.childControlHeight = true;
        group.childForceExpandWidth = false;
        group.childForceExpandHeight = false;
        ContentSizeFitter fitter = body.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // 꼬리: 아래 가장자리 가운데에 걸침 (레이아웃 계산에서 제외)
        // 꼬리 이미지가 없으면 45도 돌린 작은 흰 사각형으로 대신함
        Image tail = CreateImage("Tail", body.transform, tailSprite, Anchored(new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0f, 3.1f), new Vector2(20f, 20f)));
        if (tailSprite != null)
        {
            tail.preserveAspect = true;
        }
        else
        {
            tail.rectTransform.anchoredPosition = Vector2.zero;
            tail.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
        }
        LayoutElement tailElement = tail.gameObject.AddComponent<LayoutElement>();
        tailElement.ignoreLayout = true;

        Text text = CreateGameText("Text", body.transform, 30, TextAnchor.MiddleCenter, Stretch(), false);
        text.color = new Color(0.1f, 0.1f, 0.1f, 1f);

        body.gameObject.SetActive(false);
        return text;
    }

    // 글자에 검은 외곽선 추가. 여러 번 붙이면 외곽선이 두꺼워짐
    private void AddOutline(Text text, Vector2 distance)
    {
        Outline outline = text.gameObject.AddComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = distance;
    }

    // 장식용 이미지. 클릭을 가로채지 않도록 raycastTarget을 끔
    private Image CreateImage(string objectName, Transform parent, Sprite sprite, Layout layout)
    {
        Image image = CreatePanel(objectName, parent, Color.white, layout);
        image.sprite = sprite;
        image.raycastTarget = false;
        return image;
    }

    // 9-slice 이미지 (스프라이트 테두리는 임포트 설정의 Border 값 사용)
    private Image CreateSlicedImage(string objectName, Transform parent, Sprite sprite, float pixelsPerUnitMultiplier, Layout layout)
    {
        Image image = CreateImage(objectName, parent, sprite, layout);
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = pixelsPerUnitMultiplier;
        return image;
    }

    // 게임 화면 글자 (Jua 글꼴, 검은색). 글꼴이 없으면 기본 글꼴 사용
    private Text CreateGameText(string objectName, Transform parent, int fontSize, TextAnchor alignment, Layout layout, bool isBold)
    {
        Text text = CreateText(objectName, parent, "", fontSize, alignment, layout, false);
        if (gameFont != null)
        {
            text.font = gameFont;
        }
        if (isBold)
        {
            text.fontStyle = FontStyle.Bold;
        }
        text.color = Color.black;
        return text;
    }

    // 글자가 그려진 이미지 버튼. spritePath + "_normal/_pressed/_disabled.png" 이미지를 씀 (없는 상태 이미지는 건너뜀)
    // 기본 이미지가 없으면 fallbackLabel 글자가 있는 기본 버튼으로 대신함
    private Button CreateSpriteButton(string objectName, Transform parent, string spritePath, string fallbackLabel, Layout layout)
    {
        Button button;
        Sprite normalSprite = AssetDatabase.LoadAssetAtPath<Sprite>(UiSpriteFolder + "/" + spritePath + "_normal.png");
        if (normalSprite == null)
        {
            // 상태 이미지가 따로 없는 탭 버튼 (예: tab_jokbo.png)
            normalSprite = AssetDatabase.LoadAssetAtPath<Sprite>(UiSpriteFolder + "/" + spritePath + ".png");
        }

        if (normalSprite != null)
        {
            Image image = CreatePanel(objectName, parent, Color.white, layout);
            image.sprite = normalSprite;
            button = image.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.SpriteSwap;
            SpriteState state = new SpriteState();
            state.pressedSprite = AssetDatabase.LoadAssetAtPath<Sprite>(UiSpriteFolder + "/" + spritePath + "_pressed.png");
            state.disabledSprite = AssetDatabase.LoadAssetAtPath<Sprite>(UiSpriteFolder + "/" + spritePath + "_disabled.png");
            button.spriteState = state;
        }
        else
        {
            Debug.LogWarning("[Lexio] 버튼 이미지 없음: " + UiSpriteFolder + "/" + spritePath + " (기본 버튼으로 대신함)");
            button = CreateButton(objectName, parent, fallbackLabel, layout);
        }
        return button;
    }

    // 아이콘 + 숫자 한 줄. 칩(Coin)과 남은 패(Card)는 아이콘 크기만 다름. 반환값은 숫자 글자
    private Text CreateStatRow(Transform parent, Vector2 position, bool isChip, Sprite icon)
    {
        string rowName;
        float width;
        Vector2 iconPosition;
        Vector2 iconSize;
        float textX;
        if (isChip)
        {
            rowName = "Coin";
            width = 101.7f;
            iconPosition = new Vector2(20f, 0f);
            iconSize = new Vector2(40f, 40f);
            textX = 44.6f;
        }
        else
        {
            rowName = "Card";
            width = 112.2f;
            iconPosition = new Vector2(17.6f, 0.1f);
            iconSize = new Vector2(35.2f, 42.3f);
            textX = 42.9f;
        }

        RectTransform row = CreateRect(rowName, parent, Center(position, new Vector2(width, 42.5f)));
        CreateImage(rowName + "_Icon", row, icon, Anchored(new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), iconPosition, iconSize));
        // 숫자는 아이콘 오른쪽부터 줄 끝까지
        Layout textLayout = Anchored(new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(textX, 0f), new Vector2(width - textX, 42.5f));
        return CreateGameText(rowName + "_Text", row, 30, TextAnchor.MiddleLeft, textLayout, true);
    }

    // 원형 타이머 게이지. 채움 이미지가 Filled(360도)라서 Slider가 값을 fillAmount로 표시함 (손잡이 없음, 조작은 LexioUI가 막음)
    private Slider CreateTimerSlider(Transform parent, Sprite fillSprite)
    {
        RectTransform sliderRect = CreateRect("Timer_Slider", parent, Center(Vector2.zero, new Vector2(129.3f, 136.2f)));
        Image background = CreateImage("Background", sliderRect, fillSprite, Stretch());
        background.color = Color.black;
        RectTransform fillArea = CreateRect("Fill Area", sliderRect, Stretch());
        Image fill = CreateImage("Fill", fillArea, fillSprite, Stretch());
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Radial360;
        fill.fillOrigin = (int)Image.Origin360.Bottom;
        fill.fillClockwise = true;

        Slider slider = sliderRect.gameObject.AddComponent<Slider>();
        slider.transition = Selectable.Transition.None;
        slider.interactable = false;
        slider.fillRect = fill.rectTransform;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 1f;
        return slider;
    }

    private InputField CreateInputField(string objectName, Transform parent, string placeholder, Layout layout)
    {
        Image image = CreatePanel(objectName, parent, new Color(1f, 1f, 1f, 0.95f), layout);
        InputField inputField = image.gameObject.AddComponent<InputField>();

        Layout textLayout = Stretch();
        textLayout.size = new Vector2(-40f, 0f);
        Text placeholderText = CreateText("Placeholder", image.transform, placeholder, 34, TextAnchor.MiddleLeft, textLayout, false);
        placeholderText.color = new Color(0.5f, 0.5f, 0.5f, 1f);
        placeholderText.horizontalOverflow = HorizontalWrapMode.Wrap;

        Text inputText = CreateText("Text", image.transform, "", 34, TextAnchor.MiddleLeft, textLayout, false);
        inputText.color = new Color(0.1f, 0.1f, 0.1f, 1f);
        inputText.supportRichText = false;
        inputText.horizontalOverflow = HorizontalWrapMode.Wrap;

        inputField.textComponent = inputText;
        inputField.placeholder = placeholderText;
        inputField.targetGraphic = image;
        return inputField;
    }

    // ---------------- 유틸 ----------------

    // UiSpriteFolder 기준 경로로 스프라이트를 불러옴. 없으면 경고만 남기고 null
    private Sprite LoadSprite(string path)
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(UiSpriteFolder + "/" + path);
        if (sprite == null)
        {
            Debug.LogWarning("[Lexio] UI 이미지 없음: " + UiSpriteFolder + "/" + path);
        }
        return sprite;
    }

    private T LoadRequired<T>(string path) where T : Object
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null)
        {
            Debug.LogError("[Lexio] 에셋을 불러오지 못했습니다: " + path);
        }
        return asset;
    }

    private void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path) == false)
        {
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            string folderName = System.IO.Path.GetFileName(path);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, folderName);
        }
    }

    private void DestroyRoot(string objectName)
    {
        GameObject[] roots = SceneManager.GetActiveScene().GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i].name == objectName)
            {
                Object.DestroyImmediate(roots[i]);
            }
        }
    }

    private void SetBool(Object target, string fieldName, bool value)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        serializedObject.FindProperty(fieldName).boolValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private void SetReference(Object target, string fieldName, Object value)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        serializedObject.FindProperty(fieldName).objectReferenceValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    // ResultPanelView.rows (ResultRow 배열) 연결. 인자 배열은 모두 줄 수만큼, 같은 순서
    private void SetResultRows(ResultPanelView view, GameObject[] roots, Image[] backgrounds, Image[] badges, Image[] profiles, Text[] names, RectTransform[] meTags, Text[] changes, Text[] chips)
    {
        SerializedObject serializedObject = new SerializedObject(view);
        SerializedProperty property = serializedObject.FindProperty("rows");
        property.arraySize = roots.Length;
        for (int i = 0; i < roots.Length; i++)
        {
            SerializedProperty row = property.GetArrayElementAtIndex(i);
            row.FindPropertyRelative("root").objectReferenceValue = roots[i];
            row.FindPropertyRelative("background").objectReferenceValue = backgrounds[i];
            row.FindPropertyRelative("rankBadge").objectReferenceValue = badges[i];
            row.FindPropertyRelative("profileImage").objectReferenceValue = profiles[i];
            row.FindPropertyRelative("nameText").objectReferenceValue = names[i];
            row.FindPropertyRelative("meTag").objectReferenceValue = meTags[i];
            row.FindPropertyRelative("changeText").objectReferenceValue = changes[i];
            row.FindPropertyRelative("chipText").objectReferenceValue = chips[i];
        }
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private void SetReferenceArray(Object target, string fieldName, Object[] values)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(fieldName);
        property.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
        {
            property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }
}
