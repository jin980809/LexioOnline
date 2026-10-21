# AGENTS.md

LexioOnline — Unity 6000.2.8f1 (URP) + Mirror 렉시오 멀티플레이.

- 작업 전 HANDOFF.md 확인. 대상 파일·씬·프리팹을 진행 중 표에 등록하고, 종료 후 로그·요청 갱신.
- GPT: 전체 작업 틀·규약·순서, Unity MCP로 씬·프리팹·인스펙터 수정, 컴포넌트 부착·참조 할당·검수.
- Claude: C# 작성·수정·코드 구조 구현·로직/네트워크/UI 이벤트 연결. GPT는 필요한 코드 변경을 HANDOFF 요청에 기록.
- 사용자 2안 확정: 서버 작업 씬 Assets/1.Scene/NYM_Server_Scene.unity(GUID 유지). 단일 컴포넌트는 NYM/Server/LexioNetworkManager.cs(KPJ LexioTableNetworkManager 상속). KPJ 부모 코드는 그대로 사용한다. KPJ Server/Client 씬은 원본 유지·참고/검수용. 기존 KPJ 파일 유지, 신규 공통 규약·통합 보조 코드는 NYM/Server/ 사용. 대소문자만 다른 중복 폴더 생성 금지.
- 팀원 코드 임의 수정 금지. 변경 필요 시 작성자와 협의. 서드파티 Assets/Resources/Mirror/, Assets/Resources/ParrelSync/, Assets/Plugins/ 수정 금지.
- 에셋 이동·삭제 시 .meta도 함께 처리.
- 최소 코드·최소 파일/폴더. 표준 기능으로 해결 가능하면 의존성 추가 금지. 입력 검증 유지.
- 변수 camelCase, 클래스·메서드·프로퍼티·enum·상수 PascalCase. _·m_ 접두사 없음. 기존 서드파티 이름 유지.
- Inspector 설정 필드는 SerializeField·한글 Header/Tooltip 사용. 서버 주소·포트는 Inspector에서 조정.
- 응답: 짧은 한국어·핵심만.
## 팀원 머지와 테스트

- Unity 6000.2.8f1 및 Packages/manifest.json·packages-lock.json의 버전 사용. 패키지/규약 버전 변경은 담당자와 공유.
- 씬·프리팹·스크립트와 원본 .meta를 함께 전달/커밋. 기존 GUID 유지. 파일·폴더 경로의 대소문자도 Git 기준 그대로 유지.
- Force Text·Visible Meta Files 유지. 씬 담당자를 지정하고 같은 씬/프리팹 동시 변경은 HANDOFF에서 조율.
- NetworkContracts의 공통 DTO·enum·클래스 정의는 한 곳에서 관리. 팀원 타입과 충돌하면 새 연결 코드의 네임스페이스/어댑터로 조정하고 작성자와 협의.
- 직렬화 필드·클래스·파일·네임스페이스 변경 시 기존 참조 보존 방법과 GPT 재연결 대상을 HANDOFF에 기록. 기존 .meta를 이름만으로 새로 생성하지 않는다.
- 실행 중 NetworkManager는 한 개. NYM LexioNetworkManager : KPJ LexioTableNetworkManager 사용. NYM 서버 씬에서 부모 컴포넌트를 파생 컴포넌트로 교체하며 두 매니저 동시 부착 금지. 기존 Player/Table/UI 참조·원본 GUID 유지, 매칭 기능은 Claude·KPJ가 단일 매니저에 통합.
- 머지 후 컴파일 오류0 → NYM_Server_Scene 및 테스트 클라 Missing Script/깨진 프리팹0·Inspector 참조 확인 → 키·인증·Player·Spawnable Prefab 연결 → 전용 서버/클라 접속 → 3/4/5인·동일 서버 2매치 격리 검수. 미실행 항목은 완료로 기록하지 않는다.
- server-keys.json 비밀키 파일은 Git 제외. 팀원은 서버 키를 별도로 준비하고 클라 신뢰 공개키/지문을 맞춘다.
## NYM 테스트 씬 최신화 기준

- 2026-10-21 사용자 승인: 기존 NYM_Client_Scene을 KPJ/GameClient 최신 UI 구성으로 갱신하되 KPJ 원본·NYM 씬 GUID·인증/매칭 계약 보존. FrontendUI/UICanvas + 별도 AccountSystem·SharedCanvas·SoundManager·ScreenSettings·GuideManager 구성을 사용한다. 전적 참가자 색상은 DB-20261021-08 스냅샷/API 구현 후 연결하며 임의 색상 표시 금지.

- NYM 서버/클라 Table 참조는 `Assets/2.Model/Prefabs/Network/NYM_LexioTable.prefab` 변형 사용. 공유 원본 prefab/GUID 유지. 변형에 NetworkMatch·64점·5라운드 설정, Player는 기존 원본 재사용. 원본에 NetworkMatch 추가 시 변형 중복 여부 확인.
- NYM_Client_Scene은 UI·테스트 클라를 통합한 단일 작업 씬. FrontendUI(BackgroundCanvas·BackgroundTiles·UICanvas)에 메뉴/계정/매칭 화면, LexioCanvas에 게임/결과 UI. 전체 씬을 KPJ Client 복제로 덮어쓰지 말고 필요한 영역만 갱신. UI/LexioTableView/ChipFlightView/ActionBubbleView의 NYM 매니저·View·ResultPanelView 참조 유지. LexioUI.connectPanel=FrontendUI, NYMMatchmakingUI는 통합 UI의 버튼/패널 참조 사용. 계정/DB 미연결 기능을 인증 완료처럼 표시하지 않는다.
- 빌드 목록은 NYM_Client(index0)·NYM_Server(index1) 모두 등록. 개별 빌드는 해당 씬만 명시. 구 실행 패키지의 검수 결과를 최신 씬 검수로 기록하지 않는다.
- NYM 클라 Play는 테스트 씬 단독 로드. 다른 씬의 미저장 변경은 복구본 Save As Copy·재열기 검증 후 보존하고 닫는다. 검수 종료 전에도 활성 EventSystem·AudioListener 각1개와 Console 경고 재발 여부 확인. 런타임에서만 중복 객체를 끈 상태로 작업을 완료하지 않는다.
