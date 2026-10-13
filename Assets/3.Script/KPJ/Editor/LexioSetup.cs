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
    private const string HandSlotMaterialPath = HandSlotMaterialFolder + "/HandSlot.mat";
    private const string FontPath = "Assets/9.Font/public/static/Pretendard-Regular.otf";
    private const string BoldFontPath = "Assets/9.Font/public/static/Pretendard-Bold.otf";
    private const string RuleBookSpritePath = "Assets/4.Sprite/RuleBook.png";
    private const string ChipModelPath = "Assets/2.Model/PokerChip.obj";
    private const string ServerAddress = "192.168.1.44";
    private const ushort ServerPort = 7777;

    private Font regularFont;
    private Font boldFont;

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

            EnsureFolder(NetworkPrefabFolder);
            EnsureFolder(DataFolder);

            CreateTileDatabase();
            CreateOutlineMaterial();
            CreateTableMaterial();
            CreateHandSlotMaterial();
            SetupRuleBookSprite();
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
        options.scenes = new string[] { ServerScenePath };
        options.locationPathName = "Builds/Server/LexioServer.exe";
        options.target = BuildTarget.StandaloneWindows64;
        options.subtarget = (int)StandaloneBuildSubtarget.Server;
        BuildPipeline.BuildPlayer(options);
    }

    [MenuItem("Lexio/Build Client")]
    public static void BuildClient()
    {
        BuildPlayerOptions options = new BuildPlayerOptions();
        options.scenes = new string[] { ClientScenePath };
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

        string[] folders = { "Cloude", "Star", "Moon", "Sun" };
        string[] prefixes = { "Cloud", "Star", "Moon", "Sun" };
        for (int suit = 0; suit < GameRules.SuitCount; suit++)
        {
            for (int number = 1; number <= 15; number++)
            {
                string path = TilePrefabFolder + "/" + folders[suit] + "/" + prefixes[suit] + number + ".prefab";
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
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

    // 드래그 중 손패 칸 표시용 반투명 머티리얼 (URP Unlit, 양면). 색은 LexioClientView가 칸마다 지정
    private Material CreateHandSlotMaterial()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(HandSlotMaterialPath);
        if (material == null)
        {
            EnsureFolder(HandSlotMaterialFolder);
            material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            material.SetColor("_BaseColor", new Color(1f, 1f, 1f, 0.12f));
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
        DestroyRoot("LexioClientView");
        DestroyRoot("ChipFlightView");
        DestroyRoot("LexioCanvas");
        DestroyRoot("EventSystem");
        DestroyRoot("Table");

        LexioTableNetworkManager manager = CreateNetworkManager(playerPrefab, tablePrefab, false);

        Camera viewCamera = Camera.main;
        if (viewCamera != null)
        {
            viewCamera.transform.position = new Vector3(0f, 9.5f, -10.5f);
            viewCamera.transform.LookAt(new Vector3(0f, 0f, -1.2f));
        }

        GameObject table = GameObject.CreatePrimitive(PrimitiveType.Plane);
        table.name = "Table";
        table.transform.localScale = new Vector3(1.6f, 1f, 1.2f);
        Object.DestroyImmediate(table.GetComponent<Collider>());
        table.GetComponent<MeshRenderer>().sharedMaterial = tableMaterial;

        GameObject viewObject = new GameObject("LexioClientView");
        LexioClientView clientView = viewObject.AddComponent<LexioClientView>();
        SetReference(clientView, "networkManager", manager);
        SetReference(clientView, "tileDatabase", database);
        SetReference(clientView, "outlineMaterial", outlineMaterial);
        SetReference(clientView, "viewCamera", viewCamera);
        SetReference(clientView, "slotMaterial", handSlotMaterial);

        GameObject chipObject = new GameObject("ChipFlightView");
        ChipFlightView chipView = chipObject.AddComponent<ChipFlightView>();
        SetReference(chipView, "networkManager", manager);
        SetReference(chipView, "clientView", clientView);
        SetReference(chipView, "chipPrefab", chipModel);

        GameObject eventSystem = new GameObject("EventSystem");
        eventSystem.AddComponent<EventSystem>();
        eventSystem.AddComponent<InputSystemUIInputModule>();

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

    // ---------------- UI ----------------

    private void CreateUi(LexioTableNetworkManager manager, LexioClientView clientView, ChipFlightView chipView, Sprite ruleBookSprite)
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
        RectTransform gamePanel = CreateRect("GamePanel", root, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        Text roundText = CreateText("RoundText", gamePanel, "", 36, TextAnchor.UpperLeft, Corner(new Vector2(0f, 1f), new Vector2(40f, -30f), new Vector2(500f, 60f)), true);
        Text turnText = CreateText("TurnText", gamePanel, "", 44, TextAnchor.UpperCenter, Corner(new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(900f, 70f)), true);
        Text lastPlayText = CreateText("LastPlayText", gamePanel, "", 34, TextAnchor.MiddleCenter, Center(new Vector2(0f, 150f), new Vector2(900f, 60f)), false);

        // 하단 줄 (왼쪽부터): 시간 / 문양 정렬 / 숫자 정렬 / 제출 / 패스
        Text timerText = CreateText("TimerText", gamePanel, "", 56, TextAnchor.MiddleCenter, Corner(new Vector2(0f, 0f), new Vector2(40f, 30f), new Vector2(200f, 80f)), true);
        Button sortSuitButton = CreateButton("SortSuitButton", gamePanel, "문양 정렬", Corner(new Vector2(0f, 0f), new Vector2(260f, 30f), new Vector2(220f, 80f)));
        Button sortNumberButton = CreateButton("SortNumberButton", gamePanel, "숫자 정렬", Corner(new Vector2(0f, 0f), new Vector2(500f, 30f), new Vector2(220f, 80f)));
        Button submitButton = CreateButton("SubmitButton", gamePanel, "제출", Corner(new Vector2(0f, 0f), new Vector2(740f, 30f), new Vector2(220f, 80f)));
        Button passButton = CreateButton("PassButton", gamePanel, "패스", Corner(new Vector2(0f, 0f), new Vector2(980f, 30f), new Vector2(220f, 80f)));
        // 패 정리 시간에만 켜짐 (그동안 제출 버튼 대신 같은 자리에 표시)
        Button readyButton = CreateButton("ReadyButton", gamePanel, "준비 완료", Corner(new Vector2(0f, 0f), new Vector2(740f, 30f), new Vector2(220f, 80f)));
        readyButton.gameObject.SetActive(false);

        // 하단 줄 위: 왼쪽에 내 정보, 그 오른쪽은 3D 패 (LexioClientView.handCenter로 위치 맞춤)
        Text myInfoText = CreateText("MyInfoText", gamePanel, "", 34, TextAnchor.LowerLeft, Corner(new Vector2(0f, 0f), new Vector2(40f, 290f), new Vector2(420f, 160f)), true);
        // 패 위 중앙: 선택한 패 정보
        Text selectionText = CreateText("SelectionText", gamePanel, "", 32, TextAnchor.MiddleCenter, Corner(new Vector2(0.5f, 0f), new Vector2(0f, 480f), new Vector2(700f, 50f)), false);

        // 오른쪽 위 설정 버튼 (기능 미구현, 버튼만 배치)
        CreateButton("SettingsButton", gamePanel, "설정", Corner(new Vector2(1f, 1f), new Vector2(-40f, -30f), new Vector2(140f, 70f)));

        RectTransform[] seatLabels = new RectTransform[GameRules.MaxPlayers];
        Text[] seatLabelTexts = new Text[GameRules.MaxPlayers];
        for (int i = 0; i < GameRules.MaxPlayers; i++)
        {
            Image label = CreatePanel("SeatLabel" + i, gamePanel, new Color(0f, 0f, 0f, 0.6f), Center(Vector2.zero, new Vector2(300f, 96f)));
            label.raycastTarget = false;
            seatLabels[i] = label.rectTransform;
            seatLabelTexts[i] = CreateText("Text", label.transform, "", 28, TextAnchor.MiddleCenter, Stretch(), false);
        }

        // 족보표 서랍: 화면 오른쪽 끝 바깥에 대기(높이 75%), 왼쪽에 붙은 막대 버튼으로 열고 닫음
        // 게임 화면의 마지막 자식이라 다른 게임 UI보다 위에 그려짐
        RectTransform ruleBook = CreateRect("RuleBook", gamePanel, new Vector2(1f, 0.125f), new Vector2(1f, 0.875f), new Vector2(0f, 0.5f), new Vector2(0f, 115f), Vector2.zero);
        Image ruleBookImage = ruleBook.gameObject.AddComponent<Image>();
        ruleBookImage.sprite = ruleBookSprite;
        ruleBookImage.preserveAspect = true;
        AspectRatioFitter ruleBookFitter = ruleBook.gameObject.AddComponent<AspectRatioFitter>();
        ruleBookFitter.aspectMode = AspectRatioFitter.AspectMode.HeightControlsWidth;
        if (ruleBookSprite != null)
        {
            ruleBookFitter.aspectRatio = ruleBookSprite.rect.width / ruleBookSprite.rect.height;
        }

        Layout ruleBookButtonLayout = new Layout();
        ruleBookButtonLayout.anchorMin = new Vector2(0f, 0.3f);
        ruleBookButtonLayout.anchorMax = new Vector2(0f, 0.7f);
        ruleBookButtonLayout.pivot = new Vector2(1f, 0.5f);
        ruleBookButtonLayout.position = Vector2.zero;
        ruleBookButtonLayout.size = new Vector2(64f, 0f);
        Button ruleBookButton = CreateButton("ToggleButton", ruleBook, "족\n보\n표", ruleBookButtonLayout);

        RuleBookPanel ruleBookPanel = ruleBook.gameObject.AddComponent<RuleBookPanel>();
        SetReference(ruleBookPanel, "toggleButton", ruleBookButton);

        SetReference(ui, "gamePanel", gamePanel.gameObject);
        SetReference(ui, "roundText", roundText);
        SetReference(ui, "turnText", turnText);
        SetReference(ui, "timerText", timerText);
        SetReference(ui, "lastPlayText", lastPlayText);
        SetReference(ui, "selectionText", selectionText);
        SetReference(ui, "myInfoText", myInfoText);
        SetReference(ui, "sortSuitButton", sortSuitButton);
        SetReference(ui, "sortNumberButton", sortNumberButton);
        SetReference(ui, "passButton", passButton);
        SetReference(ui, "submitButton", submitButton);
        SetReference(ui, "readyButton", readyButton);
        SetReferenceArray(ui, "seatLabels", seatLabels);
        SetReferenceArray(ui, "seatLabelTexts", seatLabelTexts);

        // 최종 결과
        Image resultPanel = CreatePanel("ResultPanel", root, new Color(0f, 0f, 0f, 0.85f), Center(Vector2.zero, new Vector2(760f, 640f)));
        Text resultText = CreateText("ResultText", resultPanel.transform, "", 38, TextAnchor.UpperCenter, Center(new Vector2(0f, 50f), new Vector2(680f, 480f)), false);
        Button exitButton = CreateButton("ExitButton", resultPanel.transform, "나가기", Center(new Vector2(0f, -250f), new Vector2(360f, 90f)));
        SetReference(ui, "resultPanel", resultPanel.gameObject);
        SetReference(ui, "resultText", resultText);
        SetReference(ui, "exitButton", exitButton);

        // 알림 (항상 맨 위)
        Text noticeText = CreateText("NoticeText", root, "", 36, TextAnchor.MiddleCenter, Corner(new Vector2(0.5f, 1f), new Vector2(0f, -120f), new Vector2(1200f, 60f)), true);
        noticeText.color = new Color(1f, 0.6f, 0.4f, 1f);
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
