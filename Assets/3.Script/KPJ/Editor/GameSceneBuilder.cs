using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Lexio/Build Game Scenes: 흩어진 씬을 합쳐 KPJ/GameClient(로그인~결과·설정 전부)와 KPJ/GameServer(헤드리스 서버)를 새로 만듦
// 원본 씬은 열어서 오브젝트를 옮겨 온 뒤 저장하지 않고 닫으므로 바뀌지 않음. 다시 실행하면 두 씬을 처음부터 다시 만듦
//  - 인게임 화면과 조명 설정: KPJ/Client.unity (KPJ 테스트용 네트워크 매니저와 접속 패널은 뺌)
//  - 로그인~메인 화면, 설정 창, 사운드: KPJ/UI.unity
//  - 네트워크 매니저(인증·매칭)와 NYM이 추가한 UI(프로필 수정 팝업 등): NYM_Client_Scene.unity
//  - 메인 화면 게임 가이드: LSY/UI_work.unity
//  - 서버: NYM_Server_Scene.unity
public class GameSceneBuilder
{
    public const string ClientScenePath = "Assets/1.Scene/KPJ/GameClient.unity";
    public const string ServerScenePath = "Assets/1.Scene/KPJ/GameServer.unity";
    private const string GameSourcePath = "Assets/1.Scene/KPJ/Client.unity";
    private const string FrontSourcePath = "Assets/1.Scene/KPJ/UI.unity";
    private const string NymClientSourcePath = "Assets/1.Scene/NYM_Client_Scene.unity";
    private const string NymServerSourcePath = "Assets/1.Scene/NYM_Server_Scene.unity";
    private const string GuideSourcePath = "Assets/1.Scene/LSY/UI_work.unity";
    // 메인 화면 배경의 회전 테이블 (LexioTableTurnover)
    private const string KtsClientSourcePath = "Assets/1.Scene/kts/kts_Client_Scene.unity";
    // 로고 애니메이션 범위를 재는 간격(초)
    private const float LogoSampleStep = 0.05f;
    private const string PopupSettingsPath = "Assets/2.Model/Prefabs/PopupSettings.asset";
    // 빌드한 클라이언트가 접속할 서버 (AWS EC2 공인 IP). NYM 씬 값과 상관없이 GameClient에 이 주소를 넣음
    private const string ServerAddress = "13.125.241.244";
    private const string NymUiRootPath = "FrontendUI/UICanvas";
    // 매칭 인원 카드 이미지 (card_3p_default.png 등)
    private const string MatchCardFolder = "Assets/4.Sprite/LexioUI/match/";
    // 전적 행 순위 배지 (rank_badge_1.png ~ rank_badge_5.png)와 행 배경
    private const string RankBadgeFolder = "Assets/4.Sprite/LexioUI/result/";
    private const string HistoryRowFolder = "Assets/4.Sprite/LexioUI/9slice/";
    // Mirror NetworkManager.HeadlessStartOptions.AutoStartServer
    private const int AutoStartServer = 1;

    // 로그인~메인 화면의 3D 배경(타일·카메라)을 인게임 테이블과 겹치지 않게 멀리 둠
    private readonly Vector3 frontendOffset = new Vector3(1000f, 0f, 0f);
    // NYM 씬에만 있는 UI (UICanvas 기준 경로). 같은 부모 아래로 옮겨 옴
    private readonly string[] nymOnlyPaths =
    {
        "ProfileEditPopup", "MainPage/ProfileCard/GuestNicknameInput",
        "PasswordChangePopup/Window/Feedback", "AccountDeletePopup/Window/Feedback", "HistoryPopup/Window/EmptyText"
    };
    // 열고 닫는 모션(PopupView + 창의 PopOutAnimator)을 붙일 팝업
    private readonly string[] animatedPopups =
    {
        "SignUpPopup", "PasswordChangePopup", "AccountDeletePopup", "MatchmakingPopup",
        "MatchingPopup", "MyInfoPopup", "HistoryPopup", "ProfileEditPopup"
    };
    // NYM 매칭 UI가 메뉴를 흐리게 하고 로그아웃 때 닫는 팝업
    private readonly string[] menuPopups =
    {
        "SignUpPopup", "PasswordChangePopup", "AccountDeletePopup", "MatchmakingPopup",
        "MatchingPopup", "MyInfoPopup", "HistoryPopup", "ProfileEditPopup", "GuidePopup"
    };
    // NYM 계정 UI가 실행 중에 이름으로 찾는 경로. 하나라도 없으면 NYM 코드가 멈추므로 조립 후 검사
    private readonly string[] nymRuntimePaths =
    {
        "AccountDeletePopup/Window/CancelButton", "AccountDeletePopup/Window/CloseButton", "AccountDeletePopup/Window/ConfirmToggle",
        "AccountDeletePopup/Window/DeleteButton", "AccountDeletePopup/Window/Feedback", "AccountDeletePopup/Window/IdInput",
        "AccountDeletePopup/Window/PasswordInput", "HistoryPopup/Window/EmptyText", "HistoryPopup/Window/List",
        "HistoryPopup/Window/List/Row0/RankText", "HistoryPopup/Window/List/Row0/DateText", "HistoryPopup/Window/List/Row0/Crown",
        "HistoryPopup/Window/List/Row0/Players", "HistoryPopup/Window/List/Row0/RoundBadge/RoundText", "HistoryPopup/Window/List/Row0/ChipDeltaText",
        "HistoryPopup/Window/Pager/NextButton", "HistoryPopup/Window/Pager/PageText", "HistoryPopup/Window/Pager/PrevButton",
        "HistoryPopup/Window/Tabs/AllTab", "HistoryPopup/Window/Tabs/Tab3P", "HistoryPopup/Window/Tabs/Tab4P", "HistoryPopup/Window/Tabs/Tab5P",
        "LoginPage/LoginPanel/AccountDeleteLink", "LoginPage/LoginPanel/IdInput", "LoginPage/LoginPanel/PasswordChangeLink",
        "LoginPage/LoginPanel/SaveIdToggle", "MainPage/Menu/ExitButton", "MainPage/Menu/HistoryButton",
        "MainPage/ProfileCard/IdText", "MainPage/ProfileCard/ChipText", "MyInfoPopup/Window/NicknameText",
        "MatchingPopup/Window/Slots/Slot0/Avatar", "MatchingPopup/Window/Slots/Slot0/NameText",
        "MyInfoPopup/Window/AccountDeleteButton", "MyInfoPopup/Window/AvatarEditButton", "MyInfoPopup/Window/BestChipStat/ValueText",
        "MyInfoPopup/Window/ChipBox/ChipText", "MyInfoPopup/Window/FirstPlaceStat/ValueText", "MyInfoPopup/Window/FirstRateStat/ValueText",
        "MyInfoPopup/Window/PasswordChangeButton", "MyInfoPopup/Window/TotalGamesStat/ValueText",
        "MyInfoPopup/Window/Row3P/Bar", "MyInfoPopup/Window/Row4P/Bar", "MyInfoPopup/Window/Row5P/Bar",
        "PasswordChangePopup/Window/CancelButton", "PasswordChangePopup/Window/ChangeButton", "PasswordChangePopup/Window/CloseButton",
        "PasswordChangePopup/Window/CurrentPasswordInput", "PasswordChangePopup/Window/Feedback", "PasswordChangePopup/Window/IdInput",
        "PasswordChangePopup/Window/NewPasswordConfirmInput", "PasswordChangePopup/Window/NewPasswordInput",
        "ProfileEditPopup/Window/CancelButton", "ProfileEditPopup/Window/CloseButton", "ProfileEditPopup/Window/Feedback",
        "ProfileEditPopup/Window/NicknameInput", "ProfileEditPopup/Window/SaveButton",
        "ProfileEditPopup/Window/ProfileSelect/Preview/PreviewAvatar", "ProfileEditPopup/Window/ProfileSelect/Preview/PreviewEditButton",
        "ProfileEditPopup/Window/ProfileSelect/Preview/PreviewNameText", "ProfileEditPopup/Window/ProfileSelect/AvatarGrid/Avatar5/SelectCheck",
        "SignUpPopup/Window/CancelButton", "SignUpPopup/Window/CloseButton", "SignUpPopup/Window/DuplicateCheckButton",
        "SignUpPopup/Window/IdInput", "SignUpPopup/Window/IdMessage", "SignUpPopup/Window/NicknameInput",
        "SignUpPopup/Window/PasswordConfirmInput", "SignUpPopup/Window/PasswordConfirmMessage", "SignUpPopup/Window/PasswordInput",
        "SignUpPopup/Window/SubmitButton", "SignUpPopup/Window/ProfileSelect/Preview/PreviewAvatar",
        "SignUpPopup/Window/ProfileSelect/Preview/PreviewEditButton", "SignUpPopup/Window/ProfileSelect/Preview/PreviewNameText",
        "SignUpPopup/Window/ProfileSelect/AvatarGrid/Avatar5/SelectRing"
    };

    private readonly List<string> problems = new List<string>();
    // 로그인 로고를 UI로 옮길 때 이미지마다 원본 스프라이트의 정렬 순서
    private readonly Dictionary<Transform, int> logoOrders = new Dictionary<Transform, int>();
    private Scene targetScene;
    private GameObject oldManager;
    private GameObject manager;
    private Transform uiCanvas;
    private Transform sharedCanvas;

    [MenuItem("Lexio/Build Game Scenes")]
    public static void BuildScenes()
    {
        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            new GameSceneBuilder().Run();
        }
    }

    // 배치모드 실행용 (에디터를 닫은 상태에서)
    // Unity.exe -batchmode -quit -projectPath <프로젝트> -executeMethod GameSceneBuilder.BuildScenesBatch -logFile <로그>
    public static void BuildScenesBatch()
    {
        new GameSceneBuilder().Run();
    }

    private void Run()
    {
        BuildClientScene();
        BuildServerScene();
        UpdateBuildSettings();

        for (int i = 0; i < problems.Count; i++)
        {
            Debug.LogWarning("[GameSceneBuilder] " + problems[i]);
        }
        Debug.Log("[GameSceneBuilder] 완료: " + ClientScenePath + ", " + ServerScenePath + " / 확인 필요 " + problems.Count + "건");
    }

    // ---------------- 클라이언트 ----------------

    private void BuildClientScene()
    {
        // 인게임 씬을 다른 이름으로 저장해서 시작 (조명·환경 설정 유지, 원본 파일은 그대로)
        targetScene = EditorSceneManager.OpenScene(GameSourcePath, OpenSceneMode.Single);
        EditorSceneManager.SaveScene(targetScene, ClientScenePath);
        Scene frontScene = EditorSceneManager.OpenScene(FrontSourcePath, OpenSceneMode.Additive);
        Scene nymScene = EditorSceneManager.OpenScene(NymClientSourcePath, OpenSceneMode.Additive);
        Scene guideScene = EditorSceneManager.OpenScene(GuideSourcePath, OpenSceneMode.Additive);
        Scene ktsScene = EditorSceneManager.OpenScene(KtsClientSourcePath, OpenSceneMode.Additive);

        oldManager = FindRoot(targetScene, "NetworkManager");
        oldManager.name = "OldNetworkManager";
        GameObject gameCamera = FindRoot(targetScene, "Main Camera");
        GameObject lexioCanvas = FindRoot(targetScene, "LexioCanvas");

        // 네트워크 매니저 (인증·매칭 포함)와 서버 주소 선택 (NYM 씬에 서버 주소 선택이 이미 붙어 있으면 그대로 씀)
        manager = FindRoot(nymScene, "NetworkManager");
        SceneManager.MoveGameObjectToScene(manager, targetScene);
        if (manager.GetComponent<ServerAddressSelector>() == null)
        {
            manager.AddComponent<ServerAddressSelector>();
        }
        SerializedObject addressObject = new SerializedObject(manager.GetComponent<ServerAddressSelector>());
        addressObject.FindProperty("serverAddress").stringValue = ServerAddress;
        addressObject.ApplyModifiedPropertiesWithoutUndo();
        manager.GetComponent<Mirror.NetworkManager>().networkAddress = ServerAddress;

        // NYM 계정·매칭 UI (NYM 계정 UI가 이미 붙어 있으면 그대로 씀)
        GameObject accountSystem = FindAccountSystem(nymScene);
        MoveToTarget(accountSystem, null);
        accountSystem.name = "AccountSystem";
        NYMAccountUI accountUi = accountSystem.GetComponent<NYMAccountUI>();
        if (accountUi == null)
        {
            accountUi = accountSystem.AddComponent<NYMAccountUI>();
        }
        ClearPopupBackdrop(accountSystem.GetComponent<NYMMatchmakingUI>());

        // 로그인~메인 화면 (3D 배경 포함)은 Frontend 아래로 모아 게임 중에 통째로 끔
        GameObject frontend = new GameObject("Frontend");
        SceneManager.MoveGameObjectToScene(frontend, targetScene);
        GameObject frontCamera = FindRoot(frontScene, "Main Camera");
        frontCamera.name = "FrontendCamera";
        string[] frontRoots = { "FrontendCamera", "BackgroundCanvas", "BackgroundTiles", "UICanvas" };
        for (int i = 0; i < frontRoots.Length; i++)
        {
            GameObject root = FindRoot(frontScene, frontRoots[i]);
            SceneManager.MoveGameObjectToScene(root, targetScene);
            root.transform.SetParent(frontend.transform, true);
        }
        frontend.transform.position = frontendOffset;
        uiCanvas = frontend.transform.Find("UICanvas");
        SetupFrontendCamera(frontCamera, gameCamera);
        SceneManager.MoveGameObjectToScene(FindRoot(frontScene, "SoundManager"), targetScene);
        SceneManager.MoveGameObjectToScene(FindRoot(frontScene, "ScreenSettings"), targetScene);

        // 설정 창·종료 확인은 게임 중에도 열리므로 Frontend 밖의 캔버스로 옮김
        sharedCanvas = CreateSharedCanvas(lexioCanvas);
        MoveToTarget(uiCanvas.Find("SettingsPopup2").gameObject, sharedCanvas);
        MoveToTarget(uiCanvas.Find("QuitConfirmPopup").gameObject, sharedCanvas);
        // 경기 중 인원 부족으로 끝났을 때 안내 (게임 화면에서 메인으로 돌아오는 순간 뜸)
        MoveToTarget(uiCanvas.Find("MatchEndedPopup").gameObject, sharedCanvas);
        // 매칭이 잡혀 경기에 들어갈 때의 "게임 시작!" 화면 (메인 화면과 게임 화면 사이에 보여야 해서 맨 위 캔버스로)
        MoveToTarget(uiCanvas.Find("GameStartIntro").gameObject, sharedCanvas);
        // 스크립트가 없는 예전 설정 창 배치 (SettingsPopup2로 대체됨)
        DestroyIfFound(uiCanvas, "SettingsPopup");

        // NYM에만 있는 UI
        Transform nymUiCanvas = FindRoot(nymScene, "FrontendUI").transform.Find("UICanvas");
        for (int i = 0; i < nymOnlyPaths.Length; i++)
        {
            CopyNymUi(nymUiCanvas, nymOnlyPaths[i]);
        }

        // LSY 게임 가이드
        GameObject guideManager = FindRoot(guideScene, "GuideManager");
        SceneManager.MoveGameObjectToScene(guideManager, targetScene);
        GameObject guidePopup = FindRoot(guideScene, "UICanvas").transform.Find("GuidePopup").gameObject;
        MoveToTarget(guidePopup, uiCanvas);
        // 메인 화면 설정 버튼은 맨 아래(가장 위에 그려짐). 위에서 옮겨 온 팝업들 뒤로 다시 보냄
        uiCanvas.Find("SettingsButton").SetAsLastSibling();

        // 로고: 로그인 화면은 LSY 애니메이션 로고(UI로 옮김), 메인 화면은 로고 없음
        SetupLoginLogo(guideScene);
        DestroyIfFound(uiCanvas, "MainPage/Logo");

        // kts 회전 테이블 (메인 화면 배경, kts 씬과 같은 자리). 메인 화면과 함께 켜지고 꺼짐 (FrontPageTransition)
        GameObject turnTable = FindRoot(ktsScene, "LexioTableTurnover");
        PlaceLikeParent(turnTable, FindRoot(ktsScene, "FrontendUI").transform, frontend.transform);

        // 다른 씬을 가리키던 참조를 이 씬의 같은 오브젝트로 바꿈 (원본 씬을 닫기 전에 해야 경로를 읽을 수 있음)
        RemapSceneReferences();

        SetupLexioUi(lexioCanvas, frontend);
        SetupAccountUi(accountUi, accountSystem.GetComponent<NYMMatchmakingUI>());
        SetupPopups();
        PopupView guideView = SetupGuidePopup(guidePopup);
        WireButtons(lexioCanvas, guideView);
        SetInitialStates(guidePopup);
        SetupPageTransition(frontend, accountSystem.GetComponent<NYMMatchmakingUI>(), turnTable);
        // 프로필 수정 창의 닫기·취소에 NYM 씬에서 따라온 즉시 끄기 연결이 있으면 NYM 코드(닫힘 모션)보다 먼저 꺼 버리므로 지움
        Transform profileEditPopup = uiCanvas.Find("ProfileEditPopup");
        if (profileEditPopup != null)
        {
            RemoveListenersTo(profileEditPopup, profileEditPopup.gameObject);
        }
        FitChipTexts(lexioCanvas);
        EnlargeProfileCardTexts();

        Object.DestroyImmediate(oldManager);
        RemoveMissingScripts();
        CheckRuntimePaths();

        EditorSceneManager.CloseScene(frontScene, true);
        EditorSceneManager.CloseScene(nymScene, true);
        EditorSceneManager.CloseScene(guideScene, true);
        EditorSceneManager.CloseScene(ktsScene, true);
        EditorSceneManager.MarkSceneDirty(targetScene);
        EditorSceneManager.SaveScene(targetScene);
    }

    // LSY UI_work의 애니메이션 로고(Title, 3D 공간의 스프라이트)를 같은 구성의 UI 이미지로 바꿔 로그인 화면의 로고 자리(LoginPage/Logo)에 넣음
    // 애니메이션은 로그인용 KPJ 복사본(LoginLogo, 조각 이름으로 연결). 애니메이션 전체에서 로고가 퍼지는 범위가 기존 로고 칸 안에 들어가게 크기를 맞춤
    // 로그인 화면(LoginPage) 안에 있으므로 로그인 화면과 함께 켜지고 꺼짐. LSY 원본 씬은 읽기만 함
    private void SetupLoginLogo(Scene guideScene)
    {
        GameObject title = FindRoot(guideScene, "Title");
        RectTransform oldLogo = uiCanvas.Find("LoginPage/Logo") as RectTransform;
        if (title != null && oldLogo != null)
        {
            GameObject logoObject = new GameObject("Logo", typeof(RectTransform));
            logoObject.layer = oldLogo.gameObject.layer;
            RectTransform logo = (RectTransform)logoObject.transform;
            logo.SetParent(oldLogo.parent, false);
            logo.anchorMin = oldLogo.anchorMin;
            logo.anchorMax = oldLogo.anchorMax;
            logo.pivot = oldLogo.pivot;
            logo.sizeDelta = oldLogo.sizeDelta;
            logo.anchoredPosition = oldLogo.anchoredPosition;
            logo.SetSiblingIndex(oldLogo.GetSiblingIndex());
            Object.DestroyImmediate(oldLogo.gameObject);

            RectTransform uiTitle = CopySpritesAsUi(title.transform, logo);
            SortLogoLayers(uiTitle);
            FitLogo(uiTitle, logo);
        }
        else
        {
            problems.Add("UI_work의 Title 또는 LoginPage/Logo 없음 (로그인 로고 교체 생략)");
        }
    }

    // 로그인 ↔ 메인 화면 전환 연출. NYM 매칭 UI는 화면을 직접 켜고 끄지 않고 FrontPageTransition에 맡김
    private void SetupPageTransition(GameObject frontend, NYMMatchmakingUI matchmakingUi, GameObject turnTable)
    {
        FrontPageTransition transition = frontend.AddComponent<FrontPageTransition>();
        SerializedObject transitionObject = new SerializedObject(transition);
        transitionObject.FindProperty("loginPage").objectReferenceValue = uiCanvas.Find("LoginPage").gameObject;
        transitionObject.FindProperty("mainPage").objectReferenceValue = uiCanvas.Find("MainPage").gameObject;
        transitionObject.FindProperty("loginPanel").objectReferenceValue = FindUi(uiCanvas, "LoginPage/LoginPanel").transform;
        transitionObject.FindProperty("logo").objectReferenceValue = FindUi(uiCanvas, "LoginPage/Logo").transform;
        transitionObject.FindProperty("profileCard").objectReferenceValue = FindUi(uiCanvas, "MainPage/ProfileCard").transform;
        transitionObject.FindProperty("menu").objectReferenceValue = FindUi(uiCanvas, "MainPage/Menu").transform;
        transitionObject.FindProperty("noticeBar").objectReferenceValue = FindUi(uiCanvas, "MainPage/NoticeBar").transform;
        // 배경 타일 묶음 (둥둥 모션 TileMouseMotion이 붙은 오브젝트). 로그인하면 양옆으로 치워지고 메인 화면에서는 꺼짐
        TileMouseMotion tileMotion = frontend.GetComponentInChildren<TileMouseMotion>(true);
        if (tileMotion != null)
        {
            transitionObject.FindProperty("backgroundTiles").objectReferenceValue = tileMotion.transform;
        }
        else
        {
            problems.Add("Frontend에 TileMouseMotion(배경 타일) 없음 (타일 치우기 생략)");
        }
        SerializedProperty propsProperty = transitionObject.FindProperty("mainProps");
        propsProperty.arraySize = 0;
        if (turnTable != null)
        {
            propsProperty.arraySize = 1;
            propsProperty.GetArrayElementAtIndex(0).objectReferenceValue = turnTable.transform;
            // 처음에는 로그인 화면이므로 꺼 둠
            turnTable.SetActive(false);
        }
        transitionObject.ApplyModifiedPropertiesWithoutUndo();

        SerializedObject matchmakingObject = new SerializedObject(matchmakingUi);
        matchmakingObject.FindProperty("externalPageSwitch").boolValue = true;
        matchmakingObject.ApplyModifiedPropertiesWithoutUndo();
    }

    // ---------------- 3D 스프라이트 로고를 UI로 ----------------

    // source 아래 구조를 같은 이름·위치·회전·크기의 UI 오브젝트로 복사. 스프라이트는 Image로, Animator는 UI에서도 돌게 복사
    // UI 칸 크기는 스프라이트 크기(유닛)라서, 부모 배율로 화면 크기를 정함 (애니메이션 값이 유닛 기준이라 그대로 맞음)
    // 이미지마다 원본의 정렬 순서(Order in Layer)를 logoOrders에 적어 둠 (SortLogoLayers가 그리는 순서로 씀)
    private RectTransform CopySpritesAsUi(Transform source, Transform parent)
    {
        RectTransform rect = CreateLogoRect(source.name, parent);
        rect.gameObject.SetActive(source.gameObject.activeSelf);
        rect.localPosition = source.localPosition;
        rect.localRotation = source.localRotation;
        rect.localScale = source.localScale;

        SpriteRenderer spriteRenderer = source.GetComponent<SpriteRenderer>();
        if (spriteRenderer != null && spriteRenderer.sprite != null)
        {
            // 자식이 있는 오브젝트의 스프라이트는 UI에서 항상 자식보다 먼저 그려지므로, 같은 자리의 자식 이미지로 따로 둬서 순서를 정할 수 있게 함
            RectTransform imageRect = rect;
            if (source.childCount > 0)
            {
                imageRect = CreateLogoRect(source.name + "Image", rect);
            }
            // 피벗을 바꿔도 위치(피벗 위치)는 그대로 둠
            Vector3 position = imageRect.localPosition;
            Sprite sprite = spriteRenderer.sprite;
            imageRect.sizeDelta = sprite.rect.size / sprite.pixelsPerUnit;
            imageRect.pivot = new Vector2(sprite.pivot.x / sprite.rect.width, sprite.pivot.y / sprite.rect.height);
            imageRect.localPosition = position;
            Image image = imageRect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = spriteRenderer.color;
            image.raycastTarget = false;
            image.enabled = spriteRenderer.enabled;
            logoOrders[imageRect] = spriteRenderer.sortingOrder;
        }

        Animator animator = source.GetComponent<Animator>();
        if (animator != null)
        {
            Animator uiAnimator = rect.gameObject.AddComponent<Animator>();
            // 로그인 화면용으로 더 크게 튀는 KPJ 복사본(LoginLogo)을 씀. 만들 수 없으면 LSY 원본
            RuntimeAnimatorController loginController = LoginLogoAnimationMaker.GetOrMake();
            if (loginController != null)
            {
                uiAnimator.runtimeAnimatorController = loginController;
            }
            else
            {
                uiAnimator.runtimeAnimatorController = animator.runtimeAnimatorController;
            }
            uiAnimator.applyRootMotion = false;
            uiAnimator.updateMode = animator.updateMode;
            // UI에는 렌더러가 없어 화면 밖으로 판단해 멈추지 않게 항상 재생
            uiAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }

        for (int i = 0; i < source.childCount; i++)
        {
            CopySpritesAsUi(source.GetChild(i), rect);
        }
        return rect;
    }

    private RectTransform CreateLogoRect(string name, Transform parent)
    {
        GameObject item = new GameObject(name, typeof(RectTransform));
        item.layer = parent.gameObject.layer;
        RectTransform rect = (RectTransform)item.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = Vector2.zero;
        return rect;
    }

    // UI는 계층 순서대로 그려지므로 원본 정렬 순서를 형제 순서로 옮김
    // Title 자체 스프라이트(완성 로고, 순서 1)는 애니메이션 조각(순서 0) 위, 코인 조각(순서 2) 아래여야 해서 애니메이션 묶음(Animator) 안으로 넣음
    // 애니메이션은 조각 이름으로 연결되므로 묶음 안에 이미지를 더 넣어도 그대로 동작. 묶음이 Title과 같은 자리일 때만 옮김 (위치가 그대로이도록)
    private void SortLogoLayers(RectTransform uiTitle)
    {
        Transform rootImage = uiTitle.Find(uiTitle.name + "Image");
        Animator family = uiTitle.GetComponentInChildren<Animator>(true);
        if (rootImage != null && family != null && family.transform.localPosition == Vector3.zero && family.transform.localRotation == Quaternion.identity && family.transform.localScale == Vector3.one)
        {
            rootImage.SetParent(family.transform, false);
        }
        SortByOrder(uiTitle);
    }

    // 정렬 순서가 낮은 것부터 앞(먼저 그림)에 오게 형제를 정렬하고, 이 묶음의 순서(자식 중 가장 낮은 값)를 돌려줌. 같은 순서는 원래 순서 유지
    private int SortByOrder(Transform node)
    {
        int result = 0;
        if (logoOrders.ContainsKey(node))
        {
            result = logoOrders[node];
        }
        if (node.childCount > 0)
        {
            List<Transform> children = new List<Transform>();
            List<int> orders = new List<int>();
            for (int i = 0; i < node.childCount; i++)
            {
                children.Add(node.GetChild(i));
            }
            for (int i = 0; i < children.Count; i++)
            {
                orders.Add(SortByOrder(children[i]));
            }
            for (int i = 1; i < children.Count; i++)
            {
                for (int j = i; j > 0 && orders[j - 1] > orders[j]; j--)
                {
                    int order = orders[j];
                    orders[j] = orders[j - 1];
                    orders[j - 1] = order;
                    Transform child = children[j];
                    children[j] = children[j - 1];
                    children[j - 1] = child;
                }
            }
            for (int i = 0; i < children.Count; i++)
            {
                children[i].SetSiblingIndex(i);
            }
            result = orders[0];
        }
        return result;
    }

    // 애니메이션 전체에서 로고 이미지들이 차지하는 가장 큰 범위가 logo 칸 안에 들어가게 uiTitle의 배율·위치를 맞춤 (가로세로 비율 유지)
    private void FitLogo(RectTransform uiTitle, RectTransform logo)
    {
        Rect goal = logo.rect;
        Rect current = GetAnimatedLogoRect(uiTitle, logo);
        if (current.width > 0f && current.height > 0f)
        {
            // 원근이 없으므로 한 번 키우고 한 번 옮기면 맞음
            float scale = Mathf.Min(goal.width / current.width, goal.height / current.height);
            // 스프라이트 유닛(약 10)을 화면 칸(수백 px)에 맞추므로 보통 수십 배. 터무니없는 값이면 로고가 사라지지 않게 바꾸지 않음
            if (scale > 0.1f && scale < 10000f)
            {
                uiTitle.localScale = new Vector3(uiTitle.localScale.x * scale, uiTitle.localScale.y * scale, 1f);
                current = GetAnimatedLogoRect(uiTitle, logo);
                Vector2 move = goal.center - current.center;
                uiTitle.localPosition = new Vector3(uiTitle.localPosition.x + move.x, uiTitle.localPosition.y + move.y, 0f);
            }
            else
            {
                problems.Add("로그인 로고 크기 계산 이상 (배율 " + scale + ", 범위 " + current + ") — 크기·위치를 바꾸지 않음");
            }
        }
        else
        {
            problems.Add("로그인 로고 이미지 범위를 못 구함 (크기 조정 생략)");
        }
    }

    // logo 칸 기준 좌표에서, uiTitle 아래 애니메이션을 처음부터 끝까지 돌려 보며 이미지들이 차지한 가장 큰 범위
    private Rect GetAnimatedLogoRect(RectTransform uiTitle, RectTransform logo)
    {
        Transform[] transforms = uiTitle.GetComponentsInChildren<Transform>(true);
        Vector3[] positions = new Vector3[transforms.Length];
        Quaternion[] rotations = new Quaternion[transforms.Length];
        Vector3[] scales = new Vector3[transforms.Length];
        for (int i = 0; i < transforms.Length; i++)
        {
            positions[i] = transforms[i].localPosition;
            rotations[i] = transforms[i].localRotation;
            scales[i] = transforms[i].localScale;
        }

        Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
        Vector2 max = new Vector2(float.MinValue, float.MinValue);
        AddImageRect(uiTitle, logo, ref min, ref max);
        Animator[] animators = uiTitle.GetComponentsInChildren<Animator>(true);
        for (int a = 0; a < animators.Length; a++)
        {
            if (animators[a].runtimeAnimatorController != null)
            {
                AnimationClip[] clips = animators[a].runtimeAnimatorController.animationClips;
                for (int c = 0; c < clips.Length; c++)
                {
                    for (float time = 0f; time <= clips[c].length; time += LogoSampleStep)
                    {
                        clips[c].SampleAnimation(animators[a].gameObject, time);
                        AddImageRect(uiTitle, logo, ref min, ref max);
                    }
                }
            }
        }

        // 애니메이션을 돌려 보느라 바뀐 자세를 원래대로
        for (int i = 0; i < transforms.Length; i++)
        {
            transforms[i].localPosition = positions[i];
            transforms[i].localRotation = rotations[i];
            transforms[i].localScale = scales[i];
        }

        Rect result = Rect.zero;
        if (max.x > min.x && max.y > min.y)
        {
            result = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
        return result;
    }

    // 켜진 이미지 네 모서리를 logo 칸 기준 좌표로 옮겨 범위에 더함
    // 에디터에서 Overlay 캔버스는 배율 0으로 저장돼 있어 월드 좌표를 거치면 값이 깨지므로, logo까지의 부모 변환만 곱해서 계산함
    // 크기가 0인 순간(애니메이션 시작 전)의 조각은 보이지 않으므로 뺌
    private void AddImageRect(RectTransform uiTitle, RectTransform logo, ref Vector2 min, ref Vector2 max)
    {
        Vector3[] corners = new Vector3[4];
        Image[] images = uiTitle.GetComponentsInChildren<Image>(true);
        for (int i = 0; i < images.Length; i++)
        {
            if (images[i].enabled && images[i].gameObject.activeInHierarchy)
            {
                Matrix4x4 toLogo = Matrix4x4.identity;
                Transform current = images[i].transform;
                while (current != null && current != logo)
                {
                    toLogo = Matrix4x4.TRS(current.localPosition, current.localRotation, current.localScale) * toLogo;
                    current = current.parent;
                }
                images[i].rectTransform.GetLocalCorners(corners);
                Vector3 right = toLogo.MultiplyPoint3x4(corners[2]) - toLogo.MultiplyPoint3x4(corners[1]);
                Vector3 up = toLogo.MultiplyPoint3x4(corners[1]) - toLogo.MultiplyPoint3x4(corners[0]);
                if (current == logo && right.sqrMagnitude > 0.000001f && up.sqrMagnitude > 0.000001f)
                {
                    for (int c = 0; c < 4; c++)
                    {
                        Vector3 local = toLogo.MultiplyPoint3x4(corners[c]);
                        min = Vector2.Min(min, local);
                        max = Vector2.Max(max, local);
                    }
                }
            }
        }
    }

    // 원본 씬에서 sourceParent 기준이던 위치·회전 그대로 newParent 아래에 둠 (크기는 원래 값 유지)
    private void PlaceLikeParent(GameObject target, Transform sourceParent, Transform newParent)
    {
        if (target != null && sourceParent != null)
        {
            Quaternion inverse = Quaternion.Inverse(sourceParent.rotation);
            Vector3 localPosition = inverse * (target.transform.position - sourceParent.position);
            Quaternion localRotation = inverse * target.transform.rotation;
            Vector3 localScale = target.transform.lossyScale;
            MoveToTarget(target, newParent);
            target.transform.localPosition = localPosition;
            target.transform.localRotation = localRotation;
            target.transform.localScale = localScale;
        }
    }

    // NYM 계정·매칭 UI 오브젝트. 지금 NYM 씬은 AccountSystem 루트(매칭 UI + 계정 UI)
    // 예전 NYM 씬은 LexioCanvas 아래 NYMMatchmakingUI(매칭 UI만)
    private GameObject FindAccountSystem(Scene nymScene)
    {
        GameObject result = null;
        GameObject[] roots = nymScene.GetRootGameObjects();
        for (int i = 0; i < roots.Length && result == null; i++)
        {
            if (roots[i].name == "AccountSystem")
            {
                result = roots[i];
            }
        }
        if (result == null)
        {
            result = FindRoot(nymScene, "LexioCanvas").transform.Find("NYMMatchmakingUI").gameObject;
        }
        return result;
    }

    private void SetupFrontendCamera(GameObject frontCamera, GameObject gameCamera)
    {
        // 소리는 게임 카메라 하나에서만 들음
        AudioListener listener = frontCamera.GetComponent<AudioListener>();
        if (listener != null)
        {
            Object.DestroyImmediate(listener);
        }
        // 게임 밖에서는 Frontend 카메라가 화면 전체를 덮음 (게임 중에는 Frontend와 함께 꺼짐)
        Camera camera = frontCamera.GetComponent<Camera>();
        camera.depth = gameCamera.GetComponent<Camera>().depth + 1f;
    }

    // 팝업이 열려 있어도 로그인·메인 화면은 그대로 보여 줌 (뒤는 팝업의 어두운 배경만 덮음)
    // NYM 매칭 UI는 이 칸들이 비어 있으면 화면을 투명하게 하거나 로고를 띄우지 않음
    // NYM 씬을 가리키는 참조라서, 다른 씬 참조를 이 씬으로 다시 잇기 전에 비움
    private void ClearPopupBackdrop(NYMMatchmakingUI matchmakingUi)
    {
        SerializedObject serializedObject = new SerializedObject(matchmakingUi);
        serializedObject.FindProperty("menuGroup").objectReferenceValue = null;
        serializedObject.FindProperty("loginGroup").objectReferenceValue = null;
        serializedObject.FindProperty("popupLogo").objectReferenceValue = null;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private Transform CreateSharedCanvas(GameObject lexioCanvas)
    {
        GameObject canvasObject = new GameObject("SharedCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        SceneManager.MoveGameObjectToScene(canvasObject, targetScene);
        canvasObject.layer = uiCanvas.gameObject.layer;
        CanvasScaler source = uiCanvas.GetComponent<CanvasScaler>();
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = source.uiScaleMode;
        scaler.referenceResolution = source.referenceResolution;
        scaler.screenMatchMode = source.screenMatchMode;
        scaler.matchWidthOrHeight = source.matchWidthOrHeight;

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        int order = Mathf.Max(uiCanvas.GetComponent<Canvas>().sortingOrder, lexioCanvas.GetComponent<Canvas>().sortingOrder);
        canvas.sortingOrder = order + 10;
        return canvasObject.transform;
    }

    private void CopyNymUi(Transform nymUiCanvas, string path)
    {
        Transform source = nymUiCanvas.Find(path);
        if (source == null)
        {
            problems.Add("NYM 씬에 " + path + " 없음");
        }
        else if (uiCanvas.Find(path) == null)
        {
            Transform parent = uiCanvas;
            int slash = path.LastIndexOf('/');
            if (slash >= 0)
            {
                parent = uiCanvas.Find(path.Substring(0, slash));
            }

            if (parent != null)
            {
                int index = source.GetSiblingIndex();
                MoveToTarget(source.gameObject, parent);
                source.SetSiblingIndex(Mathf.Min(index, parent.childCount - 1));
            }
            else
            {
                problems.Add(path + "의 부모가 UI.unity에 없음");
            }
        }
    }

    // 다른 씬(아직 열려 있는 원본)이나 지울 KPJ 매니저를 가리키는 참조를 이 씬의 같은 자리 오브젝트로 바꿈
    private void RemapSceneReferences()
    {
        GameObject[] roots = targetScene.GetRootGameObjects();
        for (int r = 0; r < roots.Length; r++)
        {
            // 지울 KPJ 매니저 자신은 건너뜀
            if (roots[r] != oldManager)
            {
                Component[] components = roots[r].GetComponentsInChildren<Component>(true);
                for (int c = 0; c < components.Length; c++)
                {
                    if (components[c] != null && (components[c] is Transform) == false)
                    {
                        RemapComponent(components[c]);
                    }
                }
            }
        }
    }

    private void RemapComponent(Component component)
    {
        SerializedObject serializedObject = new SerializedObject(component);
        SerializedProperty property = serializedObject.GetIterator();
        bool isChanged = false;
        while (property.Next(true))
        {
            if (property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceValue != null && property.name != "m_GameObject")
            {
                Object value = property.objectReferenceValue;
                GameObject owner = GetOwner(value);
                if (owner != null && (owner.scene != targetScene || IsUnder(owner.transform, oldManager.transform)))
                {
                    string path = GetPath(owner.transform);
                    Object mapped = MapReference(value, path);
                    property.objectReferenceValue = mapped;
                    isChanged = true;
                    if (mapped == null && path != NymUiRootPath + "/GuideDrawer")
                    {
                        problems.Add(GetPath(component.transform) + " (" + component.GetType().Name + "." + property.propertyPath + ") → " + path + " 연결 대상 없음");
                    }
                }
            }
        }
        if (isChanged)
        {
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private Object MapReference(Object value, string path)
    {
        GameObject mappedOwner = null;
        if (path == "NetworkManager" || path == "OldNetworkManager")
        {
            mappedOwner = manager;
        }
        else if (path == NymUiRootPath)
        {
            mappedOwner = uiCanvas.gameObject;
        }
        else if (path.StartsWith(NymUiRootPath + "/"))
        {
            Transform found = uiCanvas.Find(path.Substring(NymUiRootPath.Length + 1));
            if (found != null)
            {
                mappedOwner = found.gameObject;
            }
        }
        else if (path == "LexioCanvas/SettingsPopup")
        {
            mappedOwner = sharedCanvas.Find("SettingsPopup2").gameObject;
        }
        else if (path.StartsWith("SharedCanvas/"))
        {
            // NYM 씬에도 설정 창·종료 확인용 SharedCanvas가 있음
            Transform found = sharedCanvas.Find(path.Substring("SharedCanvas/".Length));
            if (found != null)
            {
                mappedOwner = found.gameObject;
            }
        }
        else if (path == "LexioTable" || path == "BGCanvas")
        {
            mappedOwner = FindRoot(targetScene, path);
        }

        Object result = null;
        if (mappedOwner != null)
        {
            if (value is GameObject)
            {
                result = mappedOwner;
            }
            else
            {
                result = mappedOwner.GetComponent(value.GetType());
            }
        }
        return result;
    }

    private void SetupLexioUi(GameObject lexioCanvas, GameObject frontend)
    {
        LexioUI lexioUi = lexioCanvas.GetComponent<LexioUI>();
        SerializedObject serializedObject = new SerializedObject(lexioUi);
        // 게임 밖에서는 로그인~메인 화면 전체를 보여 주고, 게임이 시작되면 끔
        serializedObject.FindProperty("connectPanel").objectReferenceValue = frontend;
        serializedObject.FindProperty("gameStartIntro").objectReferenceValue = sharedCanvas.Find("GameStartIntro").GetComponent<GameStartIntro>();
        // 패 돌리기 연출도 시작 화면이 끝난 뒤에 시작 (DealAnimator는 인게임 씬의 LexioTableView에 있음)
        DealAnimator dealAnimator = FindRoot(targetScene, "LexioTableView").GetComponent<DealAnimator>();
        SerializedObject dealObject = new SerializedObject(dealAnimator);
        dealObject.FindProperty("gameStartIntro").objectReferenceValue = sharedCanvas.Find("GameStartIntro").GetComponent<GameStartIntro>();
        dealObject.ApplyModifiedPropertiesWithoutUndo();
        // 인게임 공지 띠는 메인 화면(UICanvas)의 공지 수신기와 시작 화면에 연결 (둘 다 다른 원본 씬에서 옴)
        Transform noticeBanner = lexioCanvas.transform.Find("GamePanel/InGameNotice");
        if (noticeBanner != null && noticeBanner.GetComponent<InGameNoticeBanner>() != null)
        {
            SerializedObject bannerObject = new SerializedObject(noticeBanner.GetComponent<InGameNoticeBanner>());
            bannerObject.FindProperty("noticeReceiver").objectReferenceValue = uiCanvas.GetComponent<ServerNoticeReceiver>();
            bannerObject.FindProperty("gameStartIntro").objectReferenceValue = sharedCanvas.Find("GameStartIntro").GetComponent<GameStartIntro>();
            bannerObject.ApplyModifiedPropertiesWithoutUndo();
            if (uiCanvas.GetComponent<ServerNoticeReceiver>() == null)
            {
                problems.Add("UICanvas 에 ServerNoticeReceiver 없음 (인게임 공지 띠가 공지를 못 받음)");
            }
        }
        else
        {
            problems.Add("LexioCanvas/GamePanel/InGameNotice 의 InGameNoticeBanner 없음 (인게임 공지 생략)");
        }
        serializedObject.FindProperty("nicknameInput").objectReferenceValue = uiCanvas.Find("MainPage/ProfileCard/GuestNicknameInput").GetComponent<InputField>();
        serializedObject.FindProperty("connectButton").objectReferenceValue = null;
        serializedObject.FindProperty("connectStatusText").objectReferenceValue = null;
        serializedObject.FindProperty("exitToMenu").boolValue = true;
        // 정원이 차면 서버가 바로 시작하므로 시작 버튼 대기실은 쓰지 않음
        serializedObject.FindProperty("lobbyPanel").objectReferenceValue = null;
        serializedObject.FindProperty("lobbyPlayersText").objectReferenceValue = null;
        serializedObject.FindProperty("startButton").objectReferenceValue = null;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        // 닉네임만으로 접속하던 KPJ 테스트용 접속 패널과 대기실
        DestroyIfFound(lexioCanvas.transform, "ConnectPanel");
        DestroyIfFound(lexioCanvas.transform, "LobbyPanel");
    }

    private void SetupAccountUi(NYMAccountUI accountUi, NYMMatchmakingUI matchmakingUi)
    {
        SerializedObject account = new SerializedObject(accountUi);
        account.FindProperty("uiRoot").objectReferenceValue = uiCanvas;
        account.FindProperty("account").objectReferenceValue = manager.GetComponent<NYMAccountAuthenticator>();
        account.FindProperty("matchmaking").objectReferenceValue = matchmakingUi;
        // 설정 창은 KPJ SettingsPanel이 맡음
        account.FindProperty("settingsPopup").objectReferenceValue = null;
        // 메인 나가기 버튼은 KPJ 종료 확인 팝업을 띄움
        account.FindProperty("quitConfirmPopup").objectReferenceValue = FindUi(sharedCanvas, "QuitConfirmPopup");
        // 전적 행은 순위에 맞는 배지와 배경으로 바뀜 (1위만 강조 배경)
        SerializedProperty badges = account.FindProperty("rankBadgeSprites");
        badges.arraySize = 5;
        for (int i = 0; i < 5; i++)
        {
            badges.GetArrayElementAtIndex(i).objectReferenceValue = LoadSprite(RankBadgeFolder + "rank_badge_" + (i + 1) + ".png");
        }
        account.FindProperty("firstRowSprite").objectReferenceValue = LoadSprite(HistoryRowFolder + "row_first.png");
        account.FindProperty("rowSprite").objectReferenceValue = LoadSprite(HistoryRowFolder + "row_white.png");
        account.ApplyModifiedPropertiesWithoutUndo();

        // 메인 프로필 카드의 닉네임은 NYM 닉네임 칸(GuestNicknameInput)이 보여 주므로 배치용 글자는 지움
        DestroyIfFound(uiCanvas, "MainPage/ProfileCard/NicknameText");

        SerializedObject matchmaking = new SerializedObject(matchmakingUi);
        // 인원 카드는 선택만 하고 시작 버튼으로 매칭 신청
        matchmaking.FindProperty("startButton").objectReferenceValue = FindButton(uiCanvas, "MatchmakingPopup/Window/StartButton");
        SerializedProperty defaultSprites = matchmaking.FindProperty("cardDefaultSprites");
        SerializedProperty selectedSprites = matchmaking.FindProperty("cardSelectedSprites");
        defaultSprites.arraySize = 3;
        selectedSprites.arraySize = 3;
        for (int i = 0; i < 3; i++)
        {
            defaultSprites.GetArrayElementAtIndex(i).objectReferenceValue = LoadCardSprite(i + 3, "default");
            selectedSprites.GetArrayElementAtIndex(i).objectReferenceValue = LoadCardSprite(i + 3, "selected");
        }
        MatchCardSize();
        // 메인 화면 가이드는 LSY 가이드 팝업 (popupPanels로 처리)
        matchmaking.FindProperty("guideDrawer").objectReferenceValue = null;
        SerializedProperty panels = matchmaking.FindProperty("popupPanels");
        panels.arraySize = menuPopups.Length + 2;
        for (int i = 0; i < menuPopups.Length; i++)
        {
            panels.GetArrayElementAtIndex(i).objectReferenceValue = FindUi(uiCanvas, menuPopups[i]);
        }
        panels.GetArrayElementAtIndex(menuPopups.Length).objectReferenceValue = FindUi(sharedCanvas, "SettingsPopup2");
        panels.GetArrayElementAtIndex(menuPopups.Length + 1).objectReferenceValue = FindUi(sharedCanvas, "QuitConfirmPopup");
        matchmaking.ApplyModifiedPropertiesWithoutUndo();
    }

    private Sprite LoadSprite(string path)
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null)
        {
            problems.Add(path + " 없음");
        }
        return sprite;
    }

    private Sprite LoadCardSprite(int playerCount, string state)
    {
        string path = MatchCardFolder + "card_" + playerCount + "p_" + state + ".png";
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null)
        {
            problems.Add(path + " 없음");
        }
        return sprite;
    }

    // UI.unity는 4인 카드가 선택된 모습(큰 크기)으로 배치돼 있음. 선택은 NYM 매칭 UI가 배율로 키우므로 다른 카드와 같은 크기로 맞춤
    private void MatchCardSize()
    {
        RectTransform reference = uiCanvas.Find("MatchmakingPopup/Window/Card3P") as RectTransform;
        RectTransform card = uiCanvas.Find("MatchmakingPopup/Window/Card4P") as RectTransform;
        if (reference != null && card != null)
        {
            card.sizeDelta = reference.sizeDelta;
            card.anchoredPosition = new Vector2(card.anchoredPosition.x, reference.anchoredPosition.y);
            RectTransform referenceText = reference.Find("WaitText") as RectTransform;
            RectTransform text = card.Find("WaitText") as RectTransform;
            if (referenceText != null && text != null)
            {
                text.anchoredPosition = new Vector2(text.anchoredPosition.x, referenceText.anchoredPosition.y);
            }
        }
        else
        {
            problems.Add("MatchmakingPopup 인원 카드 없음");
        }
    }

    // 팝업을 열 때 창이 튀어나오고, 버튼으로 닫을 때 들어가게 함
    private void SetupPopups()
    {
        PopupSettings settings = AssetDatabase.LoadAssetAtPath<PopupSettings>(PopupSettingsPath);
        if (settings == null)
        {
            problems.Add(PopupSettingsPath + " 없음 (팝업 모션 기본값 사용)");
        }

        for (int i = 0; i < animatedPopups.Length; i++)
        {
            Transform popup = uiCanvas.Find(animatedPopups[i]);
            Transform window = null;
            if (popup != null)
            {
                window = popup.Find("Window");
            }

            if (window != null)
            {
                PopOutAnimator animator = window.GetComponent<PopOutAnimator>();
                if (animator == null)
                {
                    animator = window.gameObject.AddComponent<PopOutAnimator>();
                }
                SerializedObject animatorObject = new SerializedObject(animator);
                animatorObject.FindProperty("setting").objectReferenceValue = settings;
                animatorObject.ApplyModifiedPropertiesWithoutUndo();

                PopupView view = popup.GetComponent<PopupView>();
                if (view == null)
                {
                    view = popup.gameObject.AddComponent<PopupView>();
                }
                SerializedObject viewObject = new SerializedObject(view);
                viewObject.FindProperty("window").objectReferenceValue = animator;
                // 닫기 버튼을 NYM 코드가 맡지 않는 팝업만 PopupView가 닫음 (나머지는 NYM이 입력칸 정리 후 닫음)
                if (animatedPopups[i] == "MyInfoPopup" || animatedPopups[i] == "HistoryPopup")
                {
                    viewObject.FindProperty("closeButton").objectReferenceValue = window.Find("CloseButton").GetComponent<Button>();
                }
                viewObject.ApplyModifiedPropertiesWithoutUndo();
            }
            else
            {
                problems.Add(animatedPopups[i] + "/Window 없음 (팝업 모션 생략)");
            }
        }
    }

    // LSY 가이드 팝업은 루트에 PopOutAnimator가 있어 어두운 배경(Dim)까지 같이 줄었다 커짐
    // 다른 팝업처럼 Dim은 루트에 두고 나머지를 Window로 묶어 창만 튀어나오게 함 (LSY 원본 씬은 그대로)
    private PopupView SetupGuidePopup(GameObject guidePopup)
    {
        RectTransform root = (RectTransform)guidePopup.transform;
        Object setting = AssetDatabase.LoadAssetAtPath<PopupSettings>(PopupSettingsPath);
        PopOutAnimator oldAnimator = guidePopup.GetComponent<PopOutAnimator>();
        if (oldAnimator != null)
        {
            setting = new SerializedObject(oldAnimator).FindProperty("setting").objectReferenceValue;
            RemoveListenersTo(root, oldAnimator);
            Object.DestroyImmediate(oldAnimator);
        }
        // 예전 애니메이션이 쓰던 루트의 CanvasGroup은 항상 보이고 눌리게 둠
        CanvasGroup rootGroup = guidePopup.GetComponent<CanvasGroup>();
        if (rootGroup != null)
        {
            rootGroup.alpha = 1f;
            rootGroup.interactable = true;
            rootGroup.blocksRaycasts = true;
        }

        GameObject windowObject = new GameObject("Window", typeof(RectTransform), typeof(CanvasGroup));
        windowObject.layer = guidePopup.layer;
        RectTransform window = (RectTransform)windowObject.transform;
        window.SetParent(root, false);
        // 크기와 위치는 루트와 같게 해서 옮겨 올 자식의 배치 값이 그대로 유지되게 함
        // 피벗만 화면 가운데에 두어 창이 화면 가운데를 기준으로 커지고 줄게 함 (피벗은 0~1 밖이어도 됨)
        window.anchorMin = Vector2.zero;
        window.anchorMax = Vector2.one;
        window.pivot = GetCanvasCenterPivot(root);
        window.anchoredPosition = Vector2.zero;
        window.sizeDelta = Vector2.zero;

        // Dim을 뺀 나머지를 그리는 순서대로 창 아래로 옮기고, 창은 Dim 위에 그려지게 맨 뒤에 둠
        List<Transform> contents = new List<Transform>();
        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child != window && child.name != "Dim")
            {
                contents.Add(child);
            }
        }
        for (int i = 0; i < contents.Count; i++)
        {
            contents[i].SetParent(window, true);
        }
        window.SetAsLastSibling();

        PopOutAnimator animator = windowObject.AddComponent<PopOutAnimator>();
        SerializedObject animatorObject = new SerializedObject(animator);
        animatorObject.FindProperty("setting").objectReferenceValue = setting;
        animatorObject.ApplyModifiedPropertiesWithoutUndo();

        PopupView view = guidePopup.AddComponent<PopupView>();
        SerializedObject viewObject = new SerializedObject(view);
        viewObject.FindProperty("window").objectReferenceValue = animator;
        viewObject.FindProperty("closeButton").objectReferenceValue = FindButton(window, "CloseButton");
        viewObject.ApplyModifiedPropertiesWithoutUndo();
        return view;
    }

    // root 안에서 화면(UICanvas) 한가운데가 피벗 비율로 어디인지. root는 UICanvas의 바로 아래에 있어야 함
    private Vector2 GetCanvasCenterPivot(RectTransform root)
    {
        RectTransform canvasRect = (RectTransform)uiCanvas;
        Vector2 center = canvasRect.rect.center - (Vector2)root.localPosition;
        Rect rect = root.rect;
        Vector2 result = new Vector2(0.5f, 0.5f);
        if (rect.width > 0f && rect.height > 0f)
        {
            result = new Vector2((center.x - rect.xMin) / rect.width, (center.y - rect.yMin) / rect.height);
        }
        return result;
    }

    // root 아래 버튼들의 OnClick 중 target을 부르는 연결을 지움
    private void RemoveListenersTo(Transform root, Object target)
    {
        Button[] buttons = root.GetComponentsInChildren<Button>(true);
        for (int b = 0; b < buttons.Length; b++)
        {
            for (int i = buttons[b].onClick.GetPersistentEventCount() - 1; i >= 0; i--)
            {
                if (buttons[b].onClick.GetPersistentTarget(i) == target)
                {
                    UnityEventTools.RemovePersistentListener(buttons[b].onClick, i);
                }
            }
        }
    }

    // ---------------- 메인 화면 프로필 카드 글자 ----------------

    // 메인 화면 왼쪽 아래 프로필 카드의 닉네임·아이디·칩 글자를 키움 (칸보다 길면 최소 크기까지 줄어듦)
    // 닉네임 칸(GuestNicknameInput)은 NYM 씬에서 옮겨 온 것이라 여기서 맞춤
    private void EnlargeProfileCardTexts()
    {
        SetCardText("MainPage/ProfileCard/GuestNicknameInput/Text", 36, 24);
        SetCardText("MainPage/ProfileCard/GuestNicknameInput/Placeholder", 36, 24);
        SetCardText("MainPage/ProfileCard/IdText", 26, 16);
        SetCardText("MainPage/ProfileCard/ChipText", 40, 24);
    }

    private void SetCardText(string path, int size, int minSize)
    {
        Transform found = uiCanvas.Find(path);
        Text text = null;
        if (found != null)
        {
            text = found.GetComponent<Text>();
        }
        if (text != null)
        {
            text.fontSize = size;
            text.resizeTextForBestFit = true;
            text.resizeTextMaxSize = size;
            text.resizeTextMinSize = minSize;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
        }
        else
        {
            problems.Add("UICanvas/" + path + " 글자 없음 (프로필 카드 글자 크기 조정 생략)");
        }
    }

    // ---------------- 칩 글자 칸 ----------------

    // 칩 수(-123까지)가 칸 안에 들어가게 칸을 넓히고, 그보다 길면 글자가 줄어들게 함 (Best Fit)
    //  - 인게임 이름표·내 정보: 카드 수 칸(두 자리면 충분)을 줄인 만큼 칩 칸을 넓힘. 줄 전체 폭은 그대로
    //  - 결과창: 칩 칸을 왼쪽으로 넓히고 코인 아이콘을 그만큼 옮김
    private void FitChipTexts(GameObject lexioCanvas)
    {
        Transform dynamicCanvas = lexioCanvas.transform.Find("GamePanel/DynamicCanvas");
        if (dynamicCanvas != null)
        {
            for (int i = 0; i < dynamicCanvas.childCount; i++)
            {
                Transform label = dynamicCanvas.GetChild(i);
                if (label.name == "MyInfo" || label.name.StartsWith("SeatLabel"))
                {
                    FitStatRow(label);
                }
            }
        }
        else
        {
            problems.Add("LexioCanvas/GamePanel/DynamicCanvas 없음 (이름표 칩 칸 조정 생략)");
        }

        Transform resultPanel = lexioCanvas.transform.Find("ResultPanel");
        if (resultPanel != null)
        {
            for (int i = 0; i < resultPanel.childCount; i++)
            {
                Transform row = resultPanel.GetChild(i);
                if (row.name.StartsWith("Row"))
                {
                    FitResultRow(row);
                }
            }
        }
        else
        {
            problems.Add("LexioCanvas/ResultPanel 없음 (결과창 칩 칸 조정 생략)");
        }
    }

    private void FitStatRow(Transform label)
    {
        RectTransform coin = label.Find("Coin") as RectTransform;
        RectTransform card = label.Find("Card") as RectTransform;
        RectTransform coinText = null;
        RectTransform cardText = null;
        if (coin != null && card != null)
        {
            coinText = coin.Find("Coin_Text") as RectTransform;
            cardText = card.Find("Card_Text") as RectTransform;
        }

        if (coinText != null && cardText != null)
        {
            Text chip = coinText.GetComponent<Text>();
            Text tiles = cardText.GetComponent<Text>();
            float extra = GetTextWidth(chip, "-123") - coinText.rect.width;
            float spare = cardText.rect.width - GetTextWidth(tiles, "15");
            float delta = Mathf.Min(extra, spare);
            if (delta > 0f)
            {
                // 칩 칸은 왼쪽 끝을 두고 오른쪽으로, 카드 칸은 오른쪽 끝을 두고 왼쪽 끝을 줄임
                GrowKeepLeft(coin, delta);
                GrowKeepLeft(coinText, delta);
                GrowKeepRight(card, -delta);
                GrowKeepLeft(cardText, -delta);
            }
            if (extra > spare)
            {
                problems.Add(GetPath(label) + " 칩 칸이 " + Mathf.CeilToInt(extra - spare) + " 부족 (글자 자동 축소로 맞춤)");
            }
            EnableBestFit(chip);
        }
        else
        {
            problems.Add(GetPath(label) + " 의 Coin/Coin_Text·Card/Card_Text 없음 (칩 칸 조정 생략)");
        }
    }

    private void FitResultRow(Transform row)
    {
        RectTransform chipText = row.Find("ChipText") as RectTransform;
        RectTransform icon = row.Find("Coin_Icon") as RectTransform;
        RectTransform changeText = row.Find("ChangeText") as RectTransform;
        if (chipText != null && icon != null)
        {
            Text chip = chipText.GetComponent<Text>();
            float extra = GetTextWidth(chip, "-123") - chipText.rect.width;
            if (extra > 0f)
            {
                GrowKeepRight(chipText, extra);
                icon.anchoredPosition = new Vector2(icon.anchoredPosition.x - extra, icon.anchoredPosition.y);
            }
            EnableBestFit(chip);
            if (changeText != null)
            {
                EnableBestFit(changeText.GetComponent<Text>());
                if (GetRight(changeText) > GetLeft(icon))
                {
                    problems.Add(GetPath(row) + " 증감 칸과 코인 아이콘이 겹침");
                }
            }
        }
        else
        {
            problems.Add(GetPath(row) + " 의 ChipText·Coin_Icon 없음 (칩 칸 조정 생략)");
        }
    }

    // 이 글꼴·크기로 sample을 한 줄로 썼을 때의 너비 (캔버스 단위). 글꼴 정보를 못 얻으면 숫자 너비를 글자 크기의 0.6배로 어림
    private float GetTextWidth(Text text, string sample)
    {
        float width = 0f;
        if (text != null && text.font != null)
        {
            TextGenerationSettings settings = text.GetGenerationSettings(new Vector2(10000f, 10000f));
            settings.horizontalOverflow = HorizontalWrapMode.Overflow;
            settings.scaleFactor = 1f;
            settings.resizeTextForBestFit = false;
            width = new TextGenerator().GetPreferredWidth(sample, settings);
        }
        if (width <= 0f && text != null)
        {
            width = text.fontSize * 0.6f * sample.Length;
        }
        // 글자 테두리(Outline 등)와 여유
        return width + 6f;
    }

    // 칸보다 긴 숫자는 글자가 줄어들게 함 (원래 크기가 최대, 0.6배가 최소)
    private void EnableBestFit(Text text)
    {
        if (text != null)
        {
            text.resizeTextForBestFit = true;
            text.resizeTextMaxSize = text.fontSize;
            text.resizeTextMinSize = Mathf.Max(1, Mathf.RoundToInt(text.fontSize * 0.6f));
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
        }
    }

    // 왼쪽 끝을 그대로 두고 너비를 delta만큼 바꿈 (가로 앵커가 한 점인 칸)
    private void GrowKeepLeft(RectTransform target, float delta)
    {
        target.anchoredPosition = new Vector2(target.anchoredPosition.x + delta * target.pivot.x, target.anchoredPosition.y);
        target.sizeDelta = new Vector2(target.sizeDelta.x + delta, target.sizeDelta.y);
    }

    // 오른쪽 끝을 그대로 두고 너비를 delta만큼 바꿈 (가로 앵커가 한 점인 칸)
    private void GrowKeepRight(RectTransform target, float delta)
    {
        target.anchoredPosition = new Vector2(target.anchoredPosition.x - delta * (1f - target.pivot.x), target.anchoredPosition.y);
        target.sizeDelta = new Vector2(target.sizeDelta.x + delta, target.sizeDelta.y);
    }

    // 같은 부모 안 칸의 좌우 끝 (가로 앵커가 한 점인 칸, 앵커 기준 위치)
    private float GetLeft(RectTransform target)
    {
        return target.anchoredPosition.x - target.sizeDelta.x * target.pivot.x + target.anchorMin.x * ((RectTransform)target.parent).rect.width;
    }

    private float GetRight(RectTransform target)
    {
        return GetLeft(target) + target.sizeDelta.x;
    }

    // 팝업을 여는 버튼 (NYM 코드가 입력칸 준비 등을 따로 붙이는 버튼도 여기서 연다)
    private void WireButtons(GameObject lexioCanvas, PopupView guideView)
    {
        WireOpen(uiCanvas, "LoginPage/LoginPanel/SignUpLink", "SignUpPopup");
        WireOpen(uiCanvas, "LoginPage/LoginPanel/PasswordChangeLink", "PasswordChangePopup");
        WireOpen(uiCanvas, "LoginPage/LoginPanel/AccountDeleteLink", "AccountDeletePopup");
        WireOpen(uiCanvas, "MainPage/Menu/MyInfoButton", "MyInfoPopup");
        WireOpen(uiCanvas, "MainPage/Menu/HistoryButton", "HistoryPopup");
        WireOpen(uiCanvas, "MyInfoPopup/Window/PasswordChangeButton", "PasswordChangePopup");
        WireOpen(uiCanvas, "MyInfoPopup/Window/AccountDeleteButton", "AccountDeletePopup");

        Button guideButton = FindButton(uiCanvas, "MainPage/Menu/GuideButton");
        if (guideButton != null)
        {
            UnityEventTools.AddPersistentListener(guideButton.onClick, guideView.Open);
        }

        // 게임 화면의 설정 버튼
        Button gameSettingsButton = FindButton(lexioCanvas.transform, "GamePanel/SettingsButton");
        if (gameSettingsButton != null)
        {
            UnityEventTools.AddPersistentListener(gameSettingsButton.onClick, sharedCanvas.Find("SettingsPopup2").GetComponent<PopupView>().Open);
        }
    }

    private void WireOpen(Transform root, string buttonPath, string popupPath)
    {
        Button button = FindButton(root, buttonPath);
        Transform popup = uiCanvas.Find(popupPath);
        if (button != null && popup != null && popup.GetComponent<PopupView>() != null)
        {
            UnityEventTools.AddPersistentListener(button.onClick, popup.GetComponent<PopupView>().Open);
        }
        else
        {
            problems.Add(buttonPath + " → " + popupPath + " 연결 실패");
        }
    }

    private void SetInitialStates(GameObject guidePopup)
    {
        for (int i = 0; i < animatedPopups.Length; i++)
        {
            Transform popup = uiCanvas.Find(animatedPopups[i]);
            if (popup != null)
            {
                popup.gameObject.SetActive(false);
            }
        }
        guidePopup.SetActive(false);
        sharedCanvas.Find("SettingsPopup2").gameObject.SetActive(false);
        sharedCanvas.Find("QuitConfirmPopup").gameObject.SetActive(false);
        // 로그인 여부에 따라 NYM 매칭 UI가 매 프레임 다시 정함
        uiCanvas.Find("LoginPage").gameObject.SetActive(true);
        uiCanvas.Find("MainPage").gameObject.SetActive(false);
    }

    private void RemoveMissingScripts()
    {
        GameObject[] roots = targetScene.GetRootGameObjects();
        for (int r = 0; r < roots.Length; r++)
        {
            Transform[] transforms = roots[r].GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                int removed = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(transforms[i].gameObject);
                if (removed > 0)
                {
                    problems.Add(GetPath(transforms[i]) + " 의 없는 스크립트 " + removed + "개 제거");
                }
            }
        }
    }

    private void CheckRuntimePaths()
    {
        for (int i = 0; i < nymRuntimePaths.Length; i++)
        {
            if (uiCanvas.Find(nymRuntimePaths[i]) == null)
            {
                problems.Add("NYM 코드가 찾는 UICanvas/" + nymRuntimePaths[i] + " 없음");
            }
        }
    }

    // ---------------- 서버 ----------------

    private void BuildServerScene()
    {
        // NYM 서버 씬을 다른 이름으로 저장해서 시작 (원본 파일은 그대로)
        Scene scene = EditorSceneManager.OpenScene(NymServerSourcePath, OpenSceneMode.Single);
        EditorSceneManager.SaveScene(scene, ServerScenePath);

        LexioNetworkManager serverManager = null;
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length && serverManager == null; i++)
        {
            serverManager = roots[i].GetComponentInChildren<LexioNetworkManager>(true);
        }

        if (serverManager != null)
        {
            // 헤드리스 실행 파일은 켜자마자 서버 시작 (Mirror의 헤드리스 자동 시작)
            // 일반 에디터 Play는 헤드리스가 아니라서 위 자동 시작이 동작하지 않으므로 startAsServer로 시작함
            // (헤드리스에서는 Mirror가 먼저 서버를 켜고, KPJ 매니저는 이미 켜져 있으면 건너뜀)
            SerializedObject serializedObject = new SerializedObject(serverManager);
            serializedObject.FindProperty("headlessStartMode").enumValueIndex = AutoStartServer;
            serializedObject.FindProperty("editorAutoStart").boolValue = true;
            serializedObject.FindProperty("startAsServer").boolValue = true;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();

            // 서버 콘솔 명령 (notice 공지 등)
            if (serverManager.GetComponent<ServerConsole>() == null)
            {
                serverManager.gameObject.AddComponent<ServerConsole>();
            }
        }
        else
        {
            problems.Add("서버 씬에 LexioNetworkManager 없음");
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    // 빌드 목록에 GameClient·GameServer가 없을 때만 끝에 추가. 순서·켜짐은 팀이 정한 대로 둠
    // (Lexio > Build Client/Server 메뉴는 빌드 목록과 상관없이 이 두 씬을 빌드함)
    private void UpdateBuildSettings()
    {
        List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        bool hasClient = false;
        bool hasServer = false;
        for (int i = 0; i < scenes.Count; i++)
        {
            if (scenes[i].path == ClientScenePath)
            {
                hasClient = true;
            }
            if (scenes[i].path == ServerScenePath)
            {
                hasServer = true;
            }
        }
        if (hasClient == false)
        {
            scenes.Add(new EditorBuildSettingsScene(ClientScenePath, true));
        }
        if (hasServer == false)
        {
            scenes.Add(new EditorBuildSettingsScene(ServerScenePath, true));
        }
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    // ---------------- 공통 ----------------

    // 루트가 아닌 오브젝트는 먼저 루트로 꺼낸 뒤 씬을 옮기고 새 부모 아래에 둠 (UI 배치 값 유지)
    private void MoveToTarget(GameObject target, Transform parent)
    {
        if (target.transform.parent != null)
        {
            target.transform.SetParent(null, false);
        }
        SceneManager.MoveGameObjectToScene(target, targetScene);
        if (parent != null)
        {
            target.transform.SetParent(parent, false);
        }
    }

    private GameObject FindRoot(Scene scene, string name)
    {
        GameObject result = null;
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length && result == null; i++)
        {
            if (roots[i].name == name)
            {
                result = roots[i];
            }
        }
        if (result == null)
        {
            problems.Add(scene.name + " 씬에 " + name + " 없음");
        }
        return result;
    }

    private GameObject FindUi(Transform root, string path)
    {
        GameObject result = null;
        Transform found = root.Find(path);
        if (found != null)
        {
            result = found.gameObject;
        }
        else
        {
            problems.Add(root.name + "/" + path + " 없음");
        }
        return result;
    }

    private Button FindButton(Transform root, string path)
    {
        Button result = null;
        Transform found = root.Find(path);
        if (found != null)
        {
            result = found.GetComponent<Button>();
        }
        if (result == null)
        {
            problems.Add(root.name + "/" + path + " 버튼 없음");
        }
        return result;
    }

    private void DestroyIfFound(Transform root, string path)
    {
        Transform found = root.Find(path);
        if (found != null)
        {
            Object.DestroyImmediate(found.gameObject);
        }
    }

    private GameObject GetOwner(Object value)
    {
        GameObject owner = value as GameObject;
        Component component = value as Component;
        if (component != null)
        {
            owner = component.gameObject;
        }
        if (owner != null && EditorUtility.IsPersistent(owner))
        {
            // 프리팹·에셋은 씬 밖이라 그대로 둠
            owner = null;
        }
        return owner;
    }

    private bool IsUnder(Transform target, Transform root)
    {
        bool result = false;
        Transform current = target;
        while (current != null && result == false)
        {
            result = current == root;
            current = current.parent;
        }
        return result;
    }

    private string GetPath(Transform target)
    {
        string path = target.name;
        Transform current = target.parent;
        while (current != null)
        {
            path = current.name + "/" + path;
            current = current.parent;
        }
        return path;
    }
}
