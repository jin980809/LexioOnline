# HANDOFF — Claude ↔ GPT 작업 현황과 인수인계

2026-10-12 사용자 요청으로 대화 기록의 협업 규칙·결정·요청을 복구했다. 삭제된 원본 전체 로그와 동일한 복구본은 아니다.

최신 갱신: 2026-10-14 12:30 KST. 사용자 요청 테스트 계정1개 생성 유지(kts_test / 잠자는김태수), DBManager.Login 검증 통과. KTS 일괄검수 수정 항목은 대기 유지.

## 역할 분담

| 담당 | 범위 |
|---|---|
| GPT | 전체 작업 틀·규약·작업 순서, Unity MCP 씬·프리팹·인스펙터 구성·참조 할당·검수 |
| Claude | C# 작성·수정, 코드 구조 구현, 로직·네트워크·UI 이벤트 연결 |
| 사용자 | 서버 작업 책임·팀원 산출물 취합·규칙 결정·배포 준비 |
| DB 팀원 | AccountService·Repository·MariaDB·계정 검증·저장·DB 운영·백업 |

## 협업 규칙

1. 작업 전 이 파일 확인 → 진행 중 등록 → 종료 후 잠금 해제·최신 로그 추가.
2. 상대 담당 영역은 직접 변경하지 않고 요청에 기록. 팀원 코드 수정은 작성자와 협의.
3. 같은 파일·씬·프리팹 동시 편집 금지. 새 컴포넌트·직렬화 필드·필드명 변경 후 Claude가 GPT 참조 할당 요청을 기록.
4. 시각은 KST YYYY-MM-DD HH:MM. 로그는 최신이 위. 파일은 Assets 밖 프로젝트 루트에 유지.
5. 사용자 사전 승인: HANDOFF 확인 → GPT가 작업·변경 범위를 먼저 보고 → 사용자 승인 후 수정·실행. 승인 범위가 바뀌면 다시 보고한다.

## 진행 중

| 담당 | 대상 | 작업 | 시작 |
|---|---|---|---|


## 요청

| ID | 요청자 → 담당 | 내용 | 대상 | 상태 |
|---|---|---|---|---|
| KPJ-20261020-01 | KPJ(Claude) → NYM | 최종 빌드 씬을 KPJ/GameClient.unity(index0)·GameServer.unity(index1)로 통합. `Lexio/Build Game Scenes`가 KPJ Client·UI, NYM_Client/Server, LSY/UI_work를 합쳐 다시 생성(원본 씬 수정 없음). NYMAccountUI.cs는 사용자 승인으로 settingsPopup이 비면 볼륨 연결을 건너뛰게만 변경(GameClient는 KPJ SettingsPanel 사용). 결과창 나가기는 접속 유지 후 메인 복귀(KPJ DismissFinishedTable, 다음 매칭 신청 때 서버 LeaveFinishedMatch로 정리) | NYM/NYMAccountUI.cs·KPJ/1.Scene | 씬 생성·서버/클라 빌드 성공. 서버 EXE 헤드리스 자동 기동 확인, server-keys.json 없어 ServerStart 중단 — NYM 키 파일 필요. 실제 로그인·매칭·경기 플레이 미검수. 추가(사용자 승인): NYMMatchmakingUI 카드 선택→startButton으로 신청(startButton 비우면 기존 즉시 신청), NYMAccountUI quitConfirmPopup(비우면 기존 즉시 종료), 시작 점수 64→50(NYM_LexioTable 변형·LexioProtocol.StartScore), ProtocolVersion 6→7 — 서버·클라 재빌드 필요 |
| DB-20261014-01 | GPT → Claude·DB 팀원 | KTS 실DB 검수 실패 보완: SignUpController가 (닉네임,ID,PW)를 넘겨 (ID,PW,닉네임)으로 저장. DBManager 닉네임 WHERE UserID 오류, 접속 실패 시 null reader 예외4종, 저장 실패 시 메모리 전적 선증가, 탈퇴 후 info 잔존. 정상 API CRUD/1~4위 저장은 롤백 검수 통과 | kts/SignUpController.cs·DBManager.cs | 검수 완료·수정 대기 — 팀원 원본 코드 수정 없음 |
| KTS-20261014-01 | GPT → KTS/UI 팀원·Claude | 로그인 전 ProfileController NullReference, imageNum=-1/3 범위 예외 재현. KTS ProfilePopup profileImages 배열0, OtherProfile 여는 버튼이 OffObject 호출. 모든 비밀번호 InputField Standard/길이 제한0. 기본 로그인·가입·변경·탈퇴 Canvas 비활성. 작성자와 협의 후 코드 가드·씬 참조·마스킹 수정 | Assets/1.Scene/kts/kts.unity·kts/ProfileController.cs·ProfilePopup.cs | 검수 완료·수정 대기 — GPT 씬 원본 변경 없음 |

| GPT-20261012-01 | GPT → Claude | LexioPlayer·LexioTable 원본 코드/meta 위치·소유자 확인 후 누락 참조 복구. 동명 신규 클래스 생성 전 원본 확인 | Assets/2.Model/Prefabs/Network/ | 완료 — main 머지로 `KPJ/Network/LexioPlayer.cs`·`LexioTable.cs` 수령, GUID `388d1e09…`·`ffdee4b5…` 프리팹과 일치(Missing Script 해소) |
| GPT-20261012-02 | GPT → Claude | NetworkContracts 이후 서버 관리·전용 서버 기동·인증 후 Player 생성·매칭·이탈 연결의 최소 틀 구현. 컴포넌트명/직렬화 필드/참조 대상을 기록하면 GPT가 Inspector 연결 | Assets/3.Script/NYM/Server/; NYM_Server_Scene/NetworkRoot | 뼈대·최신 Inspector 연결 완료 — 2026-10-14 NYM 변형 Table·클라 칩 연출 연결, ServerOnly 기동 통과. 13일 게스트 3/4/5인·2경기 검수는 이전 빌드 기록. 최신 경기·DB 인증·규칙 검수 대기 |
| GPT-20261012-03 | GPT → Claude | DB 팀원 서비스 호출 경계만 연결. DB 미준비/실패를 인증 성공으로 처리하지 않음 | AccountAuthenticator·AccountService | 대기 — DB 팀원 `kts/DBManager`는 클라에서 MySQL 직접 접속, 서버 Authenticator 경계 없음. DB-20261013-01 선행 |
| GPT-20261012-04 | GPT → Claude | PlayerProfile.userId는 현재 long, 첨부 v2.1 계정 키는 정규화 아이디 문자열. DB 팀원과 타입 일치 확인·수정 | NetworkContracts.cs:108 | 완료 — `string userId`(소문자 정규화 아이디)로 변경. DB 팀원 최종 확인만 남음 |
| CL-20261012-01 | Claude → GPT | 원 NYM 매니저 부착 요청은 사용자 2안 확정으로 대체. NYM_Server_Scene에 KPJ LexioTableNetworkManager·기존 암호화 Transport·Player/Table 참조 적용 | NYM_Server_Scene/NetworkRoot | 원 요청 취소·2안 Inspector 구성 완료 |
| GPT-20261012-05 | GPT → Claude | 팀원 머지 후 공통 타입/명명 충돌·직렬화 참조·단일 NetworkManager·계정 키 타입 검수. 코드/키/인증/프리팹 연결 완료 후 1클라 접속→3/4/5인→2매치 격리 결과 기록 | NYM/·NYM_Server_Scene·Network 프리팹 | 부분 완료 — 14일 새 서버EXE+모드별 새 클라+Editor로 3/4/5인 Playing·덱 범위9/13/15·손패12/13/12·64점·화면 확인, Console 오류0. 완주/재매칭·정산 연출·2경기 격리·DB·다른 PC 대기 |
| CL-20261013-01 | Claude → GPT | 빌드 첫 씬 경로 NYM_Server_Scene.unity, 기존 GUID f9e53b13… 유지 | ProjectSettings/EditorBuildSettings.asset | 갱신 완료 — 14일 NYM_Client(index0)·NYM_Server(index1) 모두 등록, 기존 GUID 유지. 각 빌드는 해당 씬만 명시. 서버 빌드 자동 기동 설정은 별도 준비 |
| CL-20261013-02 | Claude → 사용자·KPJ 팀원 | 2안: KPJ LexioTableNetworkManager 한 개에 NYM 매칭 기능 통합. 작업 서버 씬은 사용자 NYM_Server_Scene. KPJ Server 씬으로 변경하지 않음 | KPJ/Network/·NYM/Server/·NYM_Server_Scene | 사용자 결정 완료 — 2안·사용자 씬. 코드 통합은 Claude·KPJ 담당 |
| CL-20261013-03 | Claude → KPJ 팀원 | 규칙 결정과 다른 부분 확인·수정: `GameRules.StartingChips`=50(결정 64), 이탈 자리 AI가 가장 약한 싱글 제출(결정: 빈 바닥 리드+구름3 보유 시 구름3만, 그 외 패스), 사람 2명 이하 Aborted 없음(현재 사람 0명일 때만 리셋) | KPJ/Core/GameRules.cs·KPJ/Network/LexioTable.cs | 대기 — KPJ 기본값50·AI 대행·2명 이하 Aborted 미반영 유지. NYM 변형에서 startingChips64·roundCount5만 Inspector로 적용. 정산/순위 제외는 GPT-20261014-02도 확인 |
| DB-20261013-01 | Claude → DB 팀원 | 보안·규약: ① `DBManager` SQL을 `string.Format` 조립 → 파라미터 쿼리 ② 비밀번호 평문 저장 → 해시 ③ `Assets/Database/config.json`(root 접속 정보) Git·빌드 포함 → 비밀번호 변경 후 Git 제외 ④ DB 접속은 서버에서만, 클라는 Mirror Authenticator 메시지로 로그인 ⑤ SignUp 닉네임 중복 검사가 `WHERE UserID='{name}'` ⑥ 5인전 5위 기록 컬럼 없음 ⑦ userID 소문자 정규화 | kts/DBManager.cs·Assets/Database/ | 대기 — 14일 실DB 연결/InnoDB 확인. root 직접 접속·TLS 미사용·평문 PW·문자열 SQL·Git 설정 추적 유지. userinfo 인덱스/PK/UNIQUE 없음, 계정3컬럼 NULL 허용, 5위/승패/완료경기 원장 없음. API CRUD/1~4위 저장은 임시 계정 롤백 통과, UI/예외 실패는 DB-20261014-01. 서버 Authenticator·완료 저장 미연결 |
| GPT-20261013-06 | GPT → Claude·KPJ | 2안·사용자 씬 확정. KPJ 단일 매니저에 NYM 대기열·매치별 테이블·계정 인증 후 Player 생성·이탈/종료 연결. 현재 MatchInterestManagement가 있으므로 Player/Table에 NetworkMatch를 붙이고 non-empty 동일 matchId를 생성/스폰 시 배정하는 연결 필요(GPT prefab 구성 요청). 신규 SerializeField/호출 경로 전달 | KPJ/Network/·NYM/Server/·NYM_Server_Scene | 매칭 코드·최신 참조 연결 완료 — 14일 공유 Table의 NetworkMatch 누락을 원본 수정 없이 NYM 변형으로 보완. 실제 계정 인증·최신 경기 격리 재검수는 대기 |
| CL-20261013-04 | Claude → GPT | ① `Network/LexioPlayer.prefab`·`Network/LexioTable.prefab`에 Mirror `NetworkMatch` 부착(프리팹 원작성자 KPJ에 공유) ② NYM_Server_Scene/NetworkRoot의 `LexioTableNetworkManager`를 `LexioNetworkManager`(NYM/Server, GUID cfe49bc2…)로 교체. 기존 값 전부 유지(transport·주소·Player Prefab·tablePrefab·Spawnable Prefabs·startAsServer=false·autoCreatePlayer=false 등), 중복 부착 금지 ③ 새 필드: `렉시오 서버 설정 > maxMatches`=20, `matchTablePrefab`=Network/LexioTable ④ `렉시오 테스트 > autoJoinMatchSize`=0(검수 시 클라 3/4/5로 설정) | 프리팹 2개·NYM_Server_Scene/NetworkRoot | 갱신 완료 — Player 원본 NetworkMatch 유지, Table은 Network/NYM_LexioTable.prefab 변형 사용. 서버/클라 각 파생 매니저1개·Spawnable 참조 확인, 최신 ServerOnly 기동 통과. 다중 경기 재검수 대기 |
| GPT-20261013-07 | GPT → UI 팀원 | 임시 autoJoin 대신 테스트 클라 매칭 UI 연결: 모드 선택3/4/5 → ClientJoinQueue, 취소 → ClientCancelQueue, QueueResultReceived·QueueUpdatedReceived로 대기/실패 표시. KPJ LexioUI 작성자 협의. 실제 DB 로그인 준비 전 게스트 테스트임을 구분 | UI 팀원 담당·KPJ LexioUI 호출 경계 | 대기 — 사용자 지시로 UI 팀원 담당(Claude·NYM 담당 아님). 호출 경계는 `LexioNetworkManager` public API 그대로 사용 |

| GPT-20261013-08 | GPT → UI/KPJ 팀원 | KPJ Client RuleBook의 m_Script GUID 86710e43de46f6f4bac7c8e50813a599 meta가 Assets에서 미발견. NYM 복제 씬 Play 중 Unknown Behaviour 경고2개 관찰. 원본 스크립트/meta 및 경고 원인 확인 후 전달, GPT는 복제 씬 참조 검수. 원본 임의 수정 없음 | KPJ Client/RuleBook·UI 원본/meta | 현재 NYM에서 해소 — 최신 KPJ Client 구성을 NYM으로 갱신 후 Missing Script·깨진 프리팹0, 클라 Play Console 오류/경고0. 과거 누락 진단을 현재 미해결 항목으로 사용하지 않음 |

| GPT-20261014-01 | GPT → Claude | 공통 DTO를 실제 제출·패스에 연결: requestId·경기/라운드/턴·revision 검증, 같은 번호의 공개/개인 상태, 스냅샷 복원. 현재 레거시 Command/SyncVar 경로와 일치시키고 새 Inspector 필드 요청 기록 | NYM/Server/NetworkContracts·KPJ/Network·클라 수신 경계 | 대기(KPJ 협의) — 제출/패스는 KPJ `LexioPlayer.CmdSubmit/CmdPass`→`LexioTable` 경로이고 requestId·revision 없음. 팀원 코드 수정 없이는 연결 불가, 별도 NYM 경로는 중복이라 미구현. KPJ 작성자 합의 후 진행 |
| GPT-20261014-02 | GPT → KPJ·Claude | 이탈자를 정산/최종순위에서 제외, 점수 고정. 서버가 최종 결과 확정 후 경기 완료 저장 전달. 완료/Aborted 후 매치 슬롯·소속 해제와 접속 유지 재매칭 연결 | KPJ/Core/LexioGame·LexioTable·NYM/Server/LexioNetworkManager·UI 결과 | NYM 부분 코드 완료 — Finished 경기 참가자가 `ClientJoinQueue` 재신청 시 접속 유지로 경기 이탈·플레이어 파괴·대기열 등록, 전원 빠지면 테이블 파괴·슬롯 반환. 이탈자 정산/순위 제외·서버 최종순위·Aborted(KPJ)·완료 저장(DB) 대기. 실행 검수 미실행 |
| GPT-20261014-03 | GPT → UI·KPJ | NYM 클라에 최신 ChipFlightView·LexioUI·LexioClientView 연결 완료. 이후 UI 직렬화 필드/참조 변경은 HANDOFF 요청. 모드 선택/취소 UI는 GPT-20261013-07 담당 유지 | NYM_Client_Scene | 최신 씬·3/4/5 클라 재빌드·인원별 초기 게임 화면 검수 완료. 실제 제출·칩 정산 연출·모드 선택 UI는 별도 검수/작업 |

## 결정 사항

- 사용자 직접 로컬 실행·플레이 확인 완료 후 AWS 진행. AWS 시험 환경은 EC2 Windows Server 2019 Base 예정(Linux 제안 대체). 직접 확인용 클라 씬은 Assets/1.Scene/NYM_Client_Scene.unity(GUID d9b5001068354c94688633752ca075d0), KPJ Client 복제본. 팀원 C#·씬·프리팹 원본 수정 없이 연결/검수한다.
- 적용 완료: NYM_Server_Scene·NYM_Client_Scene에 LexioNetworkManager : KPJ LexioTableNetworkManager 각1개. EncryptionTransport→KcpTransport·UDP7777·주소127.0.0.1·Player 자동 생성off·maxMatches20. Player는 공유 원본, Table은 NYM_LexioTable 변형(NetworkMatch·64점·5라운드·30초). 서버 autoJoin0·startAsServer=false·headless DoNothing, 클라 autoJoin3·headless AutoStartClient 유지. Authenticator는 미연결 게스트 상태. 최신화 후 초기 화면/서버 기동만 검수했으며 13일 다중 클라 결과와 구분한다.

- 테스트 씬: Assets/1.Scene/NYM_Server_Scene.unity(구 NYM_Scene, GUID f9e53b13… 유지). 별도 LexioMain/LexioServer 씬을 새로 만들지 않는다.
- 스크립트: Assets/3.Script/NYM/ (구 NYM_Script, 폴더 GUID 4e3b7763… 유지). 서버 코드는 NYM/Server/.
- Mirror 전용 서버 → 로컬 검수 → AWS 배포. ParrelSync는 기존 선택 도구, 필수 신규 의존성 없음.
- NetworkManager Transport → EncryptionTransport, Inner → KcpTransport. 게임 UDP7777. 경기별 NetworkMatch·MatchInterestManagement.
- 서버 주소·포트는 Inspector 설정. 기존 필드 유지, 신규 코드 필드는 한글 Header/Tooltip. 변수 camelCase, 나머지 PascalCase, 불필요한 파일·폴더 분리 없음.
- 계정 검증·DB 구현/운영은 DB 팀원. GPT는 코드 수정하지 않고 Claude에게 연결 요구사항 전달.
- 시작64점·5라운드·음수 허용, 모드별 정원 충족 시 시작, 접속자2명 이하 Aborted, 타인 손패 비공개.
- 이탈자의 해당 경기 복귀·재접속 없음. 이탈자는 빈 바닥 리드+구름3(TileId 3) 보유 시 구름3만 제출, 그 외 자동 패스. 다른 타일 자동 제출 없음.
- 확인 대기: 정상 접속자의 패스 후 재참여까지 금지인지, 구름3 없는 이탈자의 빈 바닥 자동 패스를 패스 횟수에 포함하는지. 답변 전 게임 규칙 코드 변경 없음.
- C01 탈주 전적 A=집계 제외, C02 DB위치 A=동일 EC2 MariaDB는 임시안. R02 이후 선·R03 숫자2 네 개 배수 확인 대기.
- 프로젝트 목적: 포트폴리오용 시험 제작, 실서비스 배포 아님. 비용 최소(가능하면 0) 우선. 유료 서비스·유료 의존성 도입 금지, AWS는 프리티어 범위에서 테스트할 때만 켠다 (사용자 지시).
- 기능 구현 시 붙일 만한 API(Mirror·Unity 내장, AWS, 외부 웹 API 등)가 있으면 직접 구현 전에 검토하고, 쓸 경우 의존성·비용·키 관리와 함께 HANDOFF에 제안한다 (사용자 지시).
- Mirror 서버 코드(NetworkManager·Authenticator·NetworkBehaviour 등)는 `Assets/ScriptTemplates/`의 해당 템플릿으로 시작한다. 템플릿 콜백 구조·주석 유지, 필요한 부분만 채운다 (사용자 지시).
- 타일 ID는 KPJ `Tile.Id` 규칙 = `suit*16+number`(구름0·별1·달2·해3, 숫자1~15). 구름3=3, 해2=50. `LexioProtocol` 함수도 같은 규칙 (2026-10-13 사용자 결정).
- 서버 연결 경계(2안 구현): NYM `LexioNetworkManager`가 KPJ `LexioTableNetworkManager`를 상속(KPJ 코드 수정 없음, KPJ 쪽 `as LexioTableNetworkManager` 그대로 동작). KPJ의 단일 테이블 생성·입장 차단은 호출 안 함, 이탈 처리·클라 이벤트는 base 사용. 클라 `ClientJoinQueue(3/4/5)`·`ClientCancelQueue()` → NetworkMessage `QueueJoinRequest`/`QueueCancelRequest` → 서버 `RequestResult`·`QueueUpdated`(클라 이벤트 `QueueResultReceived`·`QueueUpdatedReceived`). 정원 충족 시 matchId 생성 → 테이블·플레이어 NetworkMatch에 스폰 전 배정 → KPJ `ServerAddPlayer`·`ServerRequestStart`로 시작. 참가자 전원 이탈 시 테이블 파괴·슬롯 반환. 계정 키=`conn.authenticationData`, Authenticator 없으면 임시 `guest:연결ID`(shortcut).

## 다음 단계 (순서)

1. KPJ: CL-20261013-03·GPT-20261014-02 규칙·이탈·접속자 정산·서버 최종순위 수정. NYM Inspector64점은 적용됐지만 KPJ 기본값50은 별도 수정 필요.
2. UI: GPT-20261013-07 모드 선택3/4/5·대기·취소 연결. 현재 NYM 클라 autoJoin3은 테스트용, UI 연결 후0으로 변경. 공통 호출 API 유지.
3. DB 팀원: DB-20261013-01 처리 → Claude: AccountAuthenticator·완료 결과 저장 호출 연결. 비밀번호/해시 반환 금지, DB 실패를 인증 성공으로 처리하지 않음.
4. Claude: GPT-20261014-01 공개/개인 revision·요청 검증·스냅샷 및 완료 후 매치 수명 연결. 새로운 직렬화 필드는 GPT 참조 할당 요청.
5. 사용자·GPT: 서버 키 경로·클라 신뢰 지문 준비. NYM 두 씬·NYM_LexioTable 변형+.meta·빌드 설정·AGENTS/HANDOFF 함께 검토 후 전달. 변형이 참조하는 KPJ 원본 prefab/meta도 필요. 원본에 NetworkMatch가 추가되면 변형의 중복 컴포넌트 검사.
6. 새 실행본 빌드: 클라는 NYM_Client_Scene만, 서버는 NYM_Server_Scene만 명시. 서버 빌드 자동 기동 방식을 적용한 후 원래 소스 설정 유지. Lexio-로컬테스트 패키지는 14일 최신 UI/변형·Claude 재매칭 코드로 재빌드 완료.
7. 새 빌드로 3/4/5인·같은 서버2매치·이탈·5라운드 완주·재매칭·DB 저장 검수. 사용자 로컬 확인 후 AWS Windows Server 2019 Base 준비. AWS 실행 검수는 미실시.

## 작업 로그

### 2026-10-14 12:26 — GPT KTS 씬 DB 일괄검수
- 사용자 요청: KTS씬 DB 일괄검증. 시작·종료 모두 원래 KTS 씬, Edit Mode. 코드/meta·씬/meta·DB config 총25파일 SHA256 동일. DB 설정·기존 계정/전적·스키마 변경 없음. 비밀값/기존 계정 상세 출력 없음.
- 실DB 접속 성공, userinfo InnoDB. 고유 임시 계정을 기존 ID와 충돌 검사 후 트랜잭션에서만 생성. API 가입→중복ID 거절→틀린/맞는 로그인→1/2/3/4위 저장(4판·평균2.5)→PW 변경→구PW 거절→신PW 로그인→잘못된 탈퇴 거절→정상 탈퇴→탈퇴 로그인 거절 통과. 회원가입 UI와 닉네임 오판도 실제 재현. 두 트랜잭션 모두 Rollback, 임시 행 잔존0. 검수 연결 명시 Close/Dispose 후 Play 종료.
- 기존 데이터 집계만 조회: 총2행, NULL 계정필드0·범위 밖 아바타0·등수합/판수 불일치0·평균 불일치0·중복ID 그룹0. 현재 정상 데이터와 스키마의 무결성 보장 유무는 별개.
- 씬 Missing Script0, DBManager1, 계정 컨트롤러의 입력·로그·버튼 참조 모두 할당. 4종 Start의 AddListener+빈 입력 버튼 분기 통과, 한글 메시지 정상(CP949 원본 유지). Console error/warning0. 수동 예외 검수는 catch하여 결과에 기록했으므로 오류0이 예외 처리 통과를 의미하지 않음.

| 우선순위 | 실패·미구현 | 근거·수정 경계 |
|---|---|---|
| P1 | 회원가입 UI 인자 순서 뒤바뀜 | SignUpController:32 → DBManager:274. 실제 저장 ID가 닉네임, PW가 ID, 닉네임이 PW로 기록됨. UI 팀원/Claude 수정 |
| P1 | 계정 보안 | 클라 root 직접 DB접속, TLS 미사용 실제 확인. 평문 PW·SQL 문자열 조립·config Git 추적 확인. SQL 공격 페이로드는 실DB에 실행하지 않음. DB 팀원 서버 서비스/파라미터 쿼리/해시/비밀 분리, Claude 인증 경계 |
| P1 | DB 고유성 없음 | userinfo PK/UNIQUE/인덱스0. userID/userPW/userName NULL 허용. 앱 중복 조회만으로 동시 가입 방지 불가. DB 팀원 스키마 |
| P1 | 접속 실패 예외4종 | con=null·reader=null에서 Login/SignUp/NewPassword/DeleteAccount 모두 NullReference 재현. catch의 reader.IsClosed 접근. Claude/DB 팀원 |
| P1 | 저장 실패에도 전적 증가 | First_UP을 미접속 연결로 호출 → InvalidOperationException, 메모리 playNum은0→1. 성공 후 반영/트랜잭션/중복저장 방지 필요 |
| P1 | 요구 전적/경기 연결 없음 | DB/UserInfo/PlayerProfileData 1~4위만, 5위·명시 승패·완료 경기 원장 없음. Assets/3.Script에서 경기 완료 저장 호출 미발견. 5라운드 완료=1판 구현 대기 |
| P2 | 닉네임 검사 다른 컬럼 | SignUp WHERE UserID=name. 임시 A의 ID를 B 닉네임으로 쓰면 중복ID가 아닌데 가입 거절 재현. 닉네임 중복 허용 정책과 검사 일치 필요 |
| P2 | 프로필 초기화·범위 오류 | 로그인 전 NullReference, imageNum=-1/3이면 IndexOutOfRange 재현. SelectImageON(-1)도 허용. 가드/허용범위0~2 필요 |
| P2 | 프로필 씬 구성 | ProfilePopup 이미지 배열0, OtherProfile 여는 버튼 OffObject 연결. 이름/1~4위/판수/평균 Text는 서로 다른7개 정상 할당 |
| P2 | 비밀번호 입력 표시·검증 | PW/NewPW 필드 모두 Standard, 마스킹 없음. 전체 입력 characterLimit0, 코드 빈 문자열만 검사, 공백/ID 정규화/길이 검증 미구현 |
| P2 | 탈퇴 후 세션·평균0판 | DeleteAccount 성공 후 info 잔존 재현. AverageRankNew 0판→NaN 재현 |
| P2 | 실행 환경·수명 | Editor Assets/Database와 Windows persistentDataPath/Database 경로 다름. 최초 실행은 코드의 샘플 설정 생성. JSON 읽기/파싱은 Start의 접속 try 밖, 연결 동기 처리, 종료 Dispose 구현 없음. Windows KTS 실행본 검수는 이번 범위에서 미실시 |

- 상세 검수 결과: C:/Users/m/Documents/ChatGPT/렉시오 모작 프로젝트 2/tmp/lexio-kts-db-audit-20261014/results.json. 위 실패는 요청에 기록만 했으며 코드/씬/스키마 자동 수정 없음. NYM 테스트 빌드 재생성 없음. 계정 인증/5라운드 종료 후 전적·승패 저장의 네트워크 통합 검수는 관련 구현 완료 후 별도 진행.

### 2026-10-14 12:09 — GPT 테스트 서버와 3 4 5인 클라 재빌드
- 사용자 요청: 테스트용 재빌드. 최신 NYM 씬·변형 Table·Claude의 Finished 재매칭 코드로 Server Windows Dedicated + Client3/4/5 Windows Player 빌드. strict_mode·detailed_report, 별도 클라/서버 씬 명시. 오류 모두0, 경고는 서버2·클라3인2·4인1·5인1. Unity BuildReport 용량 서버97.14MB·클라각711.37MB.
- 서버 빌드만 startAsServer=true, 클라 빌드만 autoJoin3/4/5 저장 후 빌드. 최종 서버 false·클라3·UDP7777·Editor Player·Edit Mode·깨끗한 NYM 클라 활성 상태로 원복. Scene/meta·모든 C#·프리팹·패키지·프로젝트 설정은 작업 시작 백업과 동일. 자동 줄바꿈만 바뀐 ProjectSettings.asset은 내용 동일 확인 후 백업 바이트로 복구.
- 기존 Server/server-keys.json 유지·SHA256 동일, 클라 List 신뢰 지문 일치 확인. 키 생성/변경·비밀 출력 없음. 기존 Play/Start/Stop 실행 도구 변경 없음, README 최신화.
- 실제 새 Server EXE UDP7777·AES256-GCM 기동, 같은 서버에 모드별 headless 클라N-1개+Editor 클라1개를 순차 접속. 3인 MaxNumber9·손패12, 4인13·13, 5인15·12. 모두 Playing·Round1/5·전원64점·로컬 Player 정상·보이는 스폰 객체N+1. Editor의 3/4/5인 게임 화면 캡처·표시 검수, Console 오류/경고0.
- 검수 범위는 초기 매칭·분배·화면. 실제 제출/패스·칩 정산 연출·5라운드 완주·Finished 재매칭·DB 인증/저장·이탈/Aborted·동시2경기 격리·다른 PC·AWS 전체 검수는 미실시. 해당 요청은 완료로 올리지 않음.
- 보조 클라 -nographics의 Null GPU shader 로그와 강제 종료 후 KCP10초 타임아웃은 검수 방식에 따른 로그로 분리. 테스트 PID와 정확한 EXE 경로로 이 작업의 프로세스만 종료. 종료 후 기록된 프로세스0·UDP7777 해제·Editor 네트워크 비활성.
- 증거: Codex 작업 폴더 tmp/lexio-rebuild-20261014/build-results.json·smoke-results.json·로그, 프로젝트 Temp/NYMTestQA/rebuild-3player/4player/5player.png. 배포물은 Lexio-로컬테스트의 기존 Server·Client-3/4/5 경로. C# 수정·commit/push·AWS 변경 없음. 작업 잠금 해제.

### 2026-10-14 — Claude (HANDOFF 일괄 점검)
- Claude 대기 항목 점검: GPT-20261014-02 NYM 부분만 팀원 코드 수정 없이 가능. GPT-20261014-01은 KPJ Command 경로 변경 필요로 KPJ 협의 대기, GPT-20261012-03은 DB-20261013-01 선행 대기 유지.
- `NYM/Server/LexioNetworkManager.cs`: `LeaveFinishedMatch` 추가. `JoinQueue`에서 소속 경기 테이블 Phase==Finished면 좌석 비움·소속 해제·`RemovePlayerForConnection(Destroy)` 후 대기열 등록, 아니면 기존대로 AlreadyInMatch. 남은 사람 결과 화면 유지를 위해 KPJ `ServerRemovePlayer`는 호출하지 않음. 전원 빠지면 `EndMatch`. 기존 shortcut 주석(재접속 후 재매칭) 제거.
- 직렬화 필드·씬 변경 없음 → GPT 재연결 불필요. 재신청 UI는 UI 팀원(GPT-20261013-07), API는 기존 `ClientJoinQueue` 그대로.
- Unity 컴파일 오류0(경고 kts CS0108만). 실행 검수 미실행 → GPT 검수 요청: 3인 완주 → 1명 재신청 시 남은 2명 결과 화면 유지·재신청자 대기열 진입 → 전원 재신청 시 테이블 제거·슬롯 반환.

### 2026-10-14 11:37 — GPT NYM 테스트 씬 최신화와 인수인계 갱신
- 사용자 요청: 내 테스트 씬·인수인계 최신화. KPJ 원본 씬·공유 Player/Table 프리팹·게임 C# 수정 없음. 작업 전 52파일 백업, 기존 NYM 씬/meta GUID 보존.
- NYM_Client_Scene을 최신 KPJ Client 구성으로 갱신. 최신 UI·RuleBook·ChipFlightView 포함, UI/View/Chip의 networkManager를 NYM 파생 매니저로 재연결. 부모 컴포넌트 교체 후 실행 매니저1개. 암호화·기존 List 신뢰 지문·KCP7777·주소127.0.0.1·autoJoin3 유지. URP 카메라 추가 데이터 확인.
- 공유 LexioTable의 NetworkMatch 누락은 원본 변경 대신 Network/NYM_LexioTable.prefab 변형으로 보완. GUID257544e3a5df23146b19ffe2baad495b, 고유 assetId3218560493. NetworkMatch 추가·startingChips64·roundCount5·turnSeconds30·정산 연출 대기5초. 서버/클라의 tablePrefab·matchTablePrefab·Spawnable Prefabs를 동일 변형으로 연결. 원본 Player의 NetworkMatch는 유지.
- 빌드 목록: NYM_Client(index0)·NYM_Server(index1) 모두 enabled. MCP 인메모리 목록과 ProjectSettings/EditorBuildSettings.asset 디스크를 일치시킴. 개별 클라/서버 빌드는 해당 씬을 명시해야 함. 이번 EXE 재빌드 없음.
- 검수: 서버/클라 Scene validate totalIssues0·Missing Script0·brokenPrefabs0. UI/View/Chip 필수 객체 참조 누락0. 클라 Play에서 초기 접속 화면 렌더 확인, 오류·경고0. 서버 Play에서 메모리 임시 키·테스트UDP27778·AES256-GCM·List 검증 설정으로 ServerOnly 기동 성공, 오류·경고0. 접속 클라0명이라 실제 인증/매칭/칩 정산 검수로 기록하지 않음.
- 검수 종료: StopServer·Play 중지. 소스 설정은 UDP7777·키 파일 로드true·서버 자동 기동off로 유지. 임시 키 파일 생성/비밀 출력 없음. 기존 테스트 패키지·운영 키·DB·AWS 미변경.
- 기존 13일의 게스트3/4/5인·2매치/전원 이탈 기록은 구 빌드 결과. 최신 변형/클라의 다중 인원·완주·이탈·DB·다른 PC 검수는 대기. KPJ 기본값50·AI 대행·2명 이하 Aborted·정산 제외 미반영은 코드 담당 요청 유지.
- 자동 승인 검토: KPJ 원본 씬의 저장 확인 없는 종료는 미저장 변경 유실 가능성으로 거절. 원본 종료 없이 NYM 저장 후, 저장 상태를 확인해 씬 전환. 서버 빌드 씬 비활성화는 범위 변경으로 거절되어 클라·서버 모두 enabled 유지.
- C# 변경·commit/push·팀원 메시지 발송 없음. 최종 소스/원본 해시 검수와 활성 클라 Edit Mode 확인 후 작업 잠금 해제.

### 2026-10-13 14:42 — GPT 사용자 직접 로컬 확인용 준비 완료
- 사용자 범위: 차후 팀원 산출물에 네트워크를 연결하기 쉽게 준비, 팀원 원본 수정 없음. 사용량 제한으로 막힌4인 클라 설정은 사용자 재개 요청 후 계속 진행. 승인 범위 변경 없이 준비 완료.
- KPJ/Client.unity를 NYM_Client_Scene.unity로 복제(새 GUID d9b5001068354c94688633752ca075d0). 복제본에 LexioNetworkManager 파생 컴포넌트1개·EncryptionTransport 추가, Inner→기존KCP·주소127.0.0.1·UDP7777·autoCreatePlayer=false. 기존 LexioUI·LexioClientView의 부모 매니저 필드를 파생 매니저로 재할당. 게임 UI/View·타일/테이블 프리팹·DB 코드 재작성 없음. 복제 카메라에 URP 추가 데이터 부착.
- 클라 씬 기본: autoJoinMatchSize3(직접 검수용), editorAutoStart=false·startAsServer=false·headlessStartMode=AutoStartClient. 일반 게임 창은 닉네임/접속 버튼으로 연결, -nographics 실행만 자동 연결. 모드 선택·대기·취소 UI는 UI 팀원 담당 유지, 현재 실행본3/4/5에 각 정원 고정. UI 변경 수령 후 autoJoin0으로 복구하고 public 매칭 API 연결.
- Windows64 클라3/4/5 Player 빌드 및 Windows Dedicated Server 빌드 오류0. 클라 약134.25MB, 서버 약100.51MB. 경고는 기존 DBManager.name CS0108, 일부 빌드 Unity Services native symbols 업로드 실패. 새 의존성 설치 없음.
- 서버 빌드만 NYM_Server_Scene.startAsServer=true 사용 후 소스false 원복. 최종 서버 소스는 이전과 동일, 자동 기동off·autoJoin0·maxMatches20·UDP7777·키경로./server-keys.json. 빌드 첫 씬 목록도 이전과 동일. Editor 대상은 서버 빌드 후 Server에 남아 Player 클라 빌드로 복구·확인.
- 별도 서버 EXE 기동: UDP7777 수신·AES256-GCM 암호화 연결 ready 확인. Editor 클라+별도 클라2개의3인 경기에서 LexioTable 생성·Phase Playing·MaxNumber9·Round1, Table/LocalPlayer 등록·손패 타일 생성 관찰. Editor 클라는 이후10초 타임아웃이 재발하여 버튼 조작·연속 플레이 완료로 판정하지 않음. 사용자 실제 실행본 플레이가 최종 완료 조건.
- 실행 패키지 C:/Users/m/Documents/ChatGPT/렉시오 모작 프로젝트 2/Lexio-로컬테스트: Play-Local.cmd →3/4/5 입력→숨김 서버+게임 창 정원수 실행, 각 창에서 닉네임·접속. Stop-Local.cmd는 PID와 정확한exe 경로 확인 후 해당 패키지 프로세스만 종료. PowerShell5 JSON 배열 처리 차이를 수정, 실행/종료 확인. 검수 프로세스0·UDP7777 해제, Editor Edit Mode. 실행 도구 C# 아님.
- 로컬 패키지 전용 키는 Server/server-keys.json에 유지(사용자 직접 테스트 필요), 현재 사용자만 접근하도록 ACL 보호. 프로젝트/Git 밖 저장, 비밀값 출력 없음. 클라에는 공개키 지문 List 검증만 포함. 테스트 키를 AWS/소스에 재사용하지 않고 별도 준비. README에 실행/종료·검수 범위·원본 연결 경계·머지 절차 기록.
- 원본 보존 검수: KPJ C#/씬/meta·기존 Network prefab2개·NYM 매니저 C#·NYM_Server_Scene·EditorBuildSettings 총45파일 SHA256이 준비 전과 동일. 새 클라 씬만 저장·Scene validate 이슈0. C# 수정·commit/push·AWS 작업 없음.
- 복제 씬 Play의 Unknown Behaviour 경고2개, 원본 RuleBook의 GUID86710e43… meta 미발견은 GPT-20261013-08로 원작성자 확인 요청. 임의 삭제·대체 구현 없음. 시작50점·이탈/Aborted·DB 인증·매칭 UI·로비 복귀·실제 수동 플레이는 대기 유지.
- 중간 MCP가 ZeroSivert/연결없음으로 응답한 구간은 수정 중단 후 LexioOnline 재지정·프로젝트 경로 확인하고 재개. 다른 프로젝트 수정 없음. 테스트 잠금 해제.

### 2026-10-13 12:10 — GPT 승인된 전원 이탈·슬롯 반환 검수 통과
- 사용자 승인: 전원 이탈 → 테이블 삭제·경기 슬롯 반환·재매칭 검수. AWS 밑작업은 테스트 성공 후 예정이며 이번에 AWS 리소스 생성/설정 변경 없음.
- 테스트: 기존 NYM_Server_Scene, Editor ServerOnly, Windows headless 클라, 임시 guest 계정·ECDH 키·AES256-GCM·List 공개키 지문 검증, UDP27777. maxMatches를 검수 중1로 낮춰 슬롯 반환을 실제 증명(KCP Timeout10000ms 유지).
- A 3인 경기 시작(테이블1·Player3). B 3인 추가 접속 후 슬롯 포화 동안 테이블1·Player3 유지. A 전원 프로세스 강제 종료 → 약12.8초 후 A 테이블 제거·다른 matchId의 B 테이블/Player 생성·Playing·observer3 확인. 기존 대기 클라가 재접속 없이 자동 시작, maxMatches1 상태이므로 슬롯 반환 통과.
- B 전원 강제 종료 → 약13초 후 테이블0·Player0·numPlayers0. 서버 isNetworkActive=true·ServerOnly 유지. 서버 재시작 없이 신규 C 3인 접속 → 세 번째 고유 matchId·Playing·observer3 확인. C 종료 후에도 테이블0·Player0 확인. 총3그룹9개 프로세스 사용, 서버 시작/중지는 각1회.
- 실행 중 서버 Console 오류0. 클라 로그에서 주요 예외·접속/암호화 실패 검색 결과0. 클라는 -nographics이므로 기존 Null GPU shader 로그 존재, UI/렌더링 검수 아님.
- 클라 strict build: 최초 MirrorIcon HideFlags.DontSave/resources.assets 오류4로 실패, 아이콘 재임포트 후 재빌드 오류0·기존 DBManager.name 경고1로 성공. 근본 빌드 아이콘 문제는 수정하지 않음.
- 종료: 테스트 클라0, 임시 비밀키 삭제, Editor Edit Mode. maxMatches20·autoJoin0·자동 기동off·UDP7777·키 경로./server-keys.json·신뢰 지문[] 복구. NYM 서버 씬·프리팹2개·NYM 매니저 C#·URP 설정2개·GraphicsSettings·UnityConnectSettings 총8파일 SHA256이 테스트 전과 동일. Scene validate 이슈0.
- 증거: C:/Users/m/Documents/ChatGPT/렉시오 모작 프로젝트 2/tmp/lexio-cleanup-tests/results.json 및 group-a/b/c 클라 로그. 시작점수50·이탈 패스/Aborted·DB 인증·UI·정상 경기 종료/로비 복귀는 별도 대기. 해당 테스트 범위만 전부 통과.
- 다음 제안(승인 전): AWS 밑작업으로 Linux Dedicated Server 빌드 환경 확인·서버 기동/키/UDP7777 배포 설정 초안. 실제 Linux 빌드·EC2 접속/비용 작업은 범위 확정 후 진행. C# 수정·commit/push 없음. 테스트 잠금 해제.

### 2026-10-13 — Claude (UI 담당 정정)
- HANDOFF 점검: Claude 담당 대기 작업 없음. CL-20261013-03(KPJ)·DB-20261013-01(DB 팀원)·GPT-20261012-03(DB 선행) 대기.
- 사용자 지시: UI는 다른 팀원 담당. 잠시 작성한 미커밋 `NYM/Server/LexioMatchUI.cs`+.meta 삭제(참조 없음 확인), GPT 요청 CL-20261013-05 철회. GPT-20261013-07 담당을 UI 팀원으로 변경.

### 2026-10-13 10:43 — GPT CL-20261013-04 구성·매칭 실행 검수
- 최신 Claude 요청 적용. NYM_Server_Scene의 KPJ 부모 컴포넌트를 NYM LexioNetworkManager(스크립트 GUID cfe49bc22dd05d844b9fc46b33b75633) 한 개로 교체. KPJ 캐스팅은 상속으로 유지. KPJ C#·KPJ Server/Client 씬 수정 없음.
- KPJ 프리팹 원작성자 공유: Network/LexioPlayer·LexioTable 루트에 Mirror.NetworkMatch 추가, 기존 prefab meta GUID·NetworkIdentity·게임 컴포넌트 유지. Table 저장 시 기존 코드 기본 필드 dealSeconds2·prepareSeconds60도 직렬화됨. MatchID는 에셋에서 비어 있고 서버 스폰 전에 실제 경기 GUID를 배정.
- 기존 transport→EncryptionTransport·Inner→KCP, Player Prefab·tablePrefab·Spawnable Prefabs 참조 유지. 새 maxMatches20·matchTablePrefab=LexioTable 설정. 최종 autoJoin0·startAsServer=false·headlessStartMode=DoNothing·editorAutoStart=false·autoCreatePlayer=false·Authenticator=null.
- 실제 실행: Editor ServerOnly + 별도 Windows Player 클라(-batchmode -nographics). 임시 ECDH 키·클라 신뢰 지문 List 검증 유지·테스트 UDP27777, 계정은 승인된 임시 guest:connectionId. 실제 계정/DB 인증 성공으로 처리하지 않음.
- 3인: Playing·숫자1~9·좌석3·초기 타일12개씩. 4인: Playing·숫자1~13·좌석4·타일13개씩. 5인: Playing·숫자1~15·좌석5·타일12개씩. 각 테이블 observer 수는 해당 참가자 수와 일치, 전원 chips50(목표64 불일치).
- 동시 3인 경기2개(클라6개): 서로 다른 non-empty matchId, observer 연결 겹침0, 각 연결의 observing은 자기 Table+Player3개뿐. 상대 경기 Table 가시성0. 서버 Console 오류0. 손패 비공개/전체 경기 규칙 검수 완료로 기록하지 않음.
- Windows 클라 3/4/5 모드 strict build 성공(오류0·경고1: 기존 DBManager.name CS0108). 최초 빌드는 MirrorIcon HideFlags.DontSave 오류 → 해당 아이콘만 재임포트 후 성공, png/meta SHA256·GUID·파일 내용 변화 없음. 빌드 후 객체 ID 갱신을 놓친 첫 접속 시도는 실패로 제외, 서버 활성 재확인 후 위 통과 결과 획득.
- -nographics의 Null GPU shader 경고/오류는 클라 로그에 존재. 렌더링·UI 검수 미실행. 전원 프로세스 종료 직후 서버를 멈췄으므로 KCP Timeout10초 후 테이블 제거/슬롯 반환은 이번 검수에서 확정하지 않음.
- 종료: 작업에서 띄운 클라만 PID+exe 경로 확인 후 전부 종료, Editor Edit Mode. 임시 비밀키 삭제, 씬 UDP7777·ServerKeypairPath=./server-keys.json·ClientTrustedPubKeySignatures=[]·List 검증·ServerLoadKeyPairFromFile=true 복구·저장. 테스트 키 절대 경로/지문은 최종 씬에 없음.
- 최종 diff --check는 새 NetworkMatch 블록의 Unity 기본 빈 문자열(m_Name·MatchID) 후행 공백4줄을 보고. Unity 직렬화는 유지, 씬 검증/참조 오류 없음.
- 최종 Scene validate totalIssues0·missingScripts0·brokenPrefabs0, Console 오류0. 빌드 첫 씬 NYM_Server_Scene(index0)·기존 GUID 확인. 테스트 증거는 Codex 작업 폴더 tmp/lexio-network-tests/results.json과 run3b/run3c/run4/run5 클라 로그에 보관(프로젝트 에셋/비밀키 아님).
- 빌드 과정에서 URP 설정2개·GraphicsSettings 자동 직렬화와 UnityConnectSettings 저장이 발생. 기존 Sprite meta 변경은 2026-10-12 수정 시각으로 이번 작업보다 이전. 출처 불명 기존 변경을 덮어쓰지 않고 유지, 팀원 머지 전 해당 diff 분리 검토 필요.
- 남은 작업: CL-20261013-03 규칙, GPT-20261013-07 UI, DB-20261013-01 및 Authenticator. AGENTS·결정 사항을 파생 단일 컴포넌트 기준으로 갱신. C# 직접 수정·새 의존성·commit/push 없음. 작업 잠금 해제.

### 2026-10-13 — Claude (GPT-20261013-06 매칭 통합)
- 사용자 지시: 팀원 코드 수정 없이 진행, 로그인은 DB 작업 전까지 임시 테스트 허용.
- `NYM/Server/LexioNetworkManager.cs`(GUID cfe49bc2… 유지): 부모를 `LexioTableNetworkManager`로 변경. 대기열(3/4/5·maxMatches)·매치별 테이블 생성·NetworkMatch matchId 배정·전원 이탈 시 테이블 정리. 템플릿 콜백 구조 유지, KPJ가 오버라이드한 7개 콜백(Start·OnStartServer·OnStopServer·OnServerConnect·OnServerAddPlayer·OnServerDisconnect·OnClientDisconnect)은 의도대로 base 호출/생략. 이전 이벤트 `MatchStarted`·`MatchPlayerLeft`·`QueueChanged`·public `EndMatch` 제거(KPJ 테이블이 경기 진행 담당).
- `NetworkContracts.cs`: `QueueJoinRequest`·`QueueCancelRequest`·`QueueUpdated`·`RequestResult`에 `: NetworkMessage` 추가.
- Unity 컴파일 오류0(경고는 kts CS0108만), 리플렉션으로 부모 타입·메시지 타입 확인. 프리팹 NetworkMatch·씬 교체 전이라 실제 서버 기동·3/4/5인·2매치 격리 실행 검수는 미실행.
- shortcut 2건(코드 주석): Authenticator 없을 때 `guest:연결ID`, 종료된 경기는 참가자 접속 해제 후 정리(로비 복귀 없음).
- KPJ `LexioUI.Update`는 `Table`·`LocalPlayer`가 생길 때까지 접속 화면 유지(코드 확인) → 매칭 대기 중에도 동작상 문제 없음. 대기 인원 표시·모드 선택 UI는 없음.

### 2026-10-13 10:08 — GPT 2안 사용자 씬 적용 완료
- 사용자 선택대로 서버 작업 씬은 NYM_Server_Scene(GUID f9e53b13af686ff4e9ffc1e2647c345d), 단일 매니저는 KPJ LexioTableNetworkManager(스크립트 GUID ea9d1d63…)로 통일. KPJ Server/Client 씬·팀원 C#·프리팹은 이 작업에서 수정하지 않음.
- NYM NetworkRoot의 기본 Mirror.NetworkManager 제거 후 LexioTableNetworkManager 부착. 기존 KcpTransport·EncryptionTransport·MatchInterestManagement 유지. Manager.transport→EncryptionTransport, Inner→KCP, 주소127.0.0.1·UDP7777.
- Player Prefab=Network/LexioPlayer, tablePrefab=Network/LexioTable의 LexioTable 컴포넌트, Spawnable Prefabs=[LexioTable] 할당. 참조 실제값·저장된 GUID/fileID 확인.
- startAsServer=false, headlessStartMode=DoNothing, editorAutoStart=false, autoCreatePlayer=false 유지. Authenticator는 비어 있음. 실제 키·클라 신뢰 지문·인증/매칭 통합 전 자동 서버/Player 기동 없음.
- 빌드 목록은 NYM_Server_Scene(index0)·SampleScene(index1), 디스크 파일과 Unity 목록 모두 확인. 씬/프리팹 원본 GUID 유지.
- 씬 검증: totalIssues0·missingScripts0·brokenPrefabs0, 최신 Console 오류0. 실제 서버 기동·계정 접속·매칭·경기·2매치 격리 검수는 미실행.
- MatchInterestManagement가 사용되지만 현재 Player/Table prefab에는 NetworkMatch 없음. 경기마다 non-empty 동일 matchId를 배정하는 코드까지 완료 후 격리 검수. 현재 단일 테이블 생성 코드만으로 경기 격리 완료로 표시하지 않음.
- CL-20261013-02 사용자 결정 완료(2안+사용자 씬), CL-20261012-01 원 요청 취소·새 Inspector 구성 완료, CL-20261013-01 경로 확인 완료. Claude·KPJ 코드 연결 요청 GPT-20261013-06 추가.
- C# 코드 작성/삭제·키 생성·Git commit/push 없음. Inspector 작업 잠금 해제.


### 2026-10-13 10:06 — GPT 사용자 방향 확정
- 직접 사용자 지시: 2안 진행, 씬은 사용자 씬 사용. KPJ LexioTableNetworkManager + NYM_Server_Scene 조합으로 기준 수정.
- 기존 KPJ Server/Client 씬 기준 AGENTS 문구와 중단된 GPT 진행 등록을 새 범위로 정정. 원본 KPJ 씬·팀원 C# 코드 수정 없음.
- CL-20261013-02 결정 완료. 기존 NYM 매니저 교체 요청 CL-20261012-01은 새 2안 구성으로 대체. Claude·KPJ 코드 통합 작업 유지.


### 2026-10-13 09:55 — GPT 인수인계 점검
- 최신 기준 확인: NYM_Server_Scene, NYM/Server, 타일ID=suit*16+number·구름3=3, 계정 키 string. Player/Table 원본 meta GUID와 프리팹 참조 일치 확인.
- 현재 빌드 파일은 삭제된 Assets/1.Scene/NYM_Scene.unity 경로를 가리킴. CL-20261013-01은 실제 미처리. 새 씬 meta GUID f9e53b13af686ff4e9ffc1e2647c345d 유지 확인.
- NYM 서버 씬은 아직 기본 Mirror.NetworkManager, Authenticator/Player Prefab/Spawnable Prefabs 비어 있음. 컴포넌트 교체는 통합 결정 전 보류 유지.
- 핵심 차단점 확인: KPJ Player/Table은 NetworkManager.singleton을 LexioTableNetworkManager로 캐스팅. NYM 매니저의 대기열 호출·MatchStarted/MatchPlayerLeft/QueueChanged 이벤트 구독은 현재 게임 스크립트에서 발견되지 않음. 매니저 교체만으로 접속·매칭 연결이 완성되지 않음.
- 제안: 동일 서버 여러 경기 요구에 맞춰 CL-20261013-02 안1(NYM 단일 매니저·경기별 테이블/NetworkMatch)을 사용자·KPJ 팀원과 먼저 결정. KPJ 참조 변경은 작성자 협의 후 Claude가 연결. 결정 전 C#·씬 매니저 변경 없음.
- 규칙 차이 실제 확인: StartingChips=50, 이탈자 빈 바닥 리드는 가장 약한 싱글 자동 제출, 사람0명일 때 리셋. 결정값64·구름3 예외 외 자동 패스·접속자2명 이하 Aborted와 불일치. CL-20261013-03 필요.
- DB-20261013-01의 서버 전용 접속·파라미터 SQL·비밀번호 해시·설정 제외/자격 정보 교체는 DB 팀원 선행 작업. Assets/Database/config.json Git 추적 여부만 확인, 비밀값 읽기/출력 없음.
- Git 확인: 구 씬/스크립트 삭제(D)와 신규 NYM_Server_Scene·NYM 폴더(??), AGENTS/HANDOFF 미추적. 이동 파일·원본 meta·복구 파일을 함께 커밋 필요. commit/push 미실행.
- Unity 현재 활성 씬은 KPJ/Server. 최신 Console 조회 오류 없음·경고2건(DBManager.name CS0108·카메라 추가 데이터). 재컴파일·서버/클라 실행 검수 미실행.
- 다음 작업 제시: 매니저 방식 결정 + GPT 빌드 경로 갱신 → 이동/복구 파일 커밋 → Claude 매칭/테이블/NetworkMatch/이탈 연결. DB 경계 보안 수정과 KPJ 규칙 수정은 병행. 이후 Inspector 참조 할당·1클라·3/4/5인·2매치 격리 검수.
- 이번 작업은 읽기 점검·계획 제안·이 로그 갱신만. 게임 C#·씬·빌드 설정 변경 없음.


### 2026-10-13 — Claude (타일 ID·인수인계)
- 사용자 결정: 타일 ID를 KPJ 규칙으로 통일. `NYM/Server/NetworkContracts.cs` `LexioProtocol`: `Cloud3TileId` 8→3, `ToTileId`=`suit*16+number`, `GetNumber`=`id%16`, `GetSuit`=`id/16`, `IsValidTileId`=0~63·숫자1~MaxNumber. 다른 사용처 없음(grep). Unity 컴파일 오류0, 에디터 검증: 4문양×1~15 전 타일 KPJ `Tile.Id`와 일치·3인 유효범위·경계값(0·16·64) 불일치0.
- 요청 01 완료 처리, 03 상태 갱신. 신규 요청 CL-20261013-01~03·DB-20261013-01, 다음 단계 섹션 추가.

### 2026-10-13 — Claude (a안)
- main(9c5b743) 머지 확인: 팀원 매니저 `LexioTableNetworkManager`, `Suit`→`TileSuit`로 이름 변경되어 전역 충돌 해소. 바로 아래 로그의 NYM 매니저·`Suit` 삭제는 불필요했음.
- 사용자 이동 확인: `NYM_Script/`→`NYM/Server/`(폴더 GUID 4e3b7763…·NetworkContracts 680c58d4… 유지), `NYM_Scene`→`NYM_Server_Scene`(GUID f9e53b13… 유지). Git 기준 미커밋.
- 복구: `NYM/Server/LexioNetworkManager.cs`+.meta(b19e4bd 원본, GUID cfe49bc2…), `NetworkContracts.cs`의 `Suit` enum. NetworkContracts는 원본과 동일.
- Unity 재컴파일 Console 오류0. 경고: kts `DBManager.name` CS0108(팀원 코드), Main Camera 추가 데이터 없음. 실행 검수 미실행.
- 남은 불일치: 타일 ID 규칙(KPJ `suit*16+number`, 구름3=3 / NetworkContracts `(n-1)*4+suit`, 구름3=8) 팀원 협의 필요. EditorBuildSettings에 구 경로 `NYM_Scene.unity` 남음(GPT 확인).

### 2026-10-13 — Claude
- 팀원 머지(f5b0b91) 후 전역 타입 충돌로 컴파일 오류 9개: `LexioNetworkManager`(NYM·KPJ/Network), `Suit`(NetworkContracts·KPJ/Core/Tile.cs).
- 사용자 결정: 팀원 것 유지. `NYM_Script/LexioNetworkManager.cs`+.meta(GUID cfe49bc2…) 삭제 — 씬·프리팹 참조 없음 확인. `NetworkContracts.cs`의 `Suit` enum 삭제(KPJ `Suit`와 값 동일).
- Unity 재컴파일 후 Console 오류 0. 실행 검수는 미실행.

### 2026-10-12 14:45 — Claude
- 사용자 결정: LexioPlayer·LexioTable은 로직 팀원 담당. 원본 수령 후 작업, 그때까지 현 상태 유지. 요청 01 상태를 보류로 변경. 코드·씬·프리팹 변경 없음.

### 2026-10-12 14:40 — Claude
- 요청 04: `NetworkContracts.cs` `PlayerProfile.userId` long → string(소문자 정규화 로그인 아이디).
- 요청 02: `Assets/3.Script/NYM_Script/LexioNetworkManager.cs` 추가(ScriptTemplates 50번 기반). Queue3/4/5, 연결당 대기표 1개, 정원+경기 슬롯(maxMatches) 확보 시 등록순 좌석으로 시작, 대기 중 이탈 시 대기표 제거, 경기 중 이탈 시 좌석 비우고 `MatchPlayerLeft` 전달(재접속 없음), `EndMatch`로 소속 해제.
- Unity 컴파일·Mirror weaver 통과, Console 오류·경고 0. 실제 서버 기동·접속·매칭 검수는 미실행(플레이어 오브젝트·인증이 없어 호출 경로 없음).
- GPT 요청 CL-20261012-01 등록(NetworkManager 컴포넌트 교체). 요청 01은 원본 미발견으로 사용자 결정 대기, 03은 DB 규약 대기.

### 2026-10-12 14:38 — GPT
- MCP manage_build scenes는 에디터 목록을 바꿨지만 EditorBuildSettings.asset 디스크 내용은 그대로였음. 팀원 머지 재현을 위해 m_Scenes 블록만 NYM_Scene(index0)·SampleScene(index1)과 기존 GUID로 저장. m_configObjects 등 기존 설정 유지.
- 디스크 내용·Git diff/추적·빌드 씬 GUID 확인. EditorBuildSettings.asset·.gitignore diff --check 통과. NYM_Scene은 Unity 기본 직렬화의 빈 값 줄 후행 공백 알림이 있었으며, 씬 검증/참조에는 영향 없음. 임의 재직렬화·공백 수정 없음.
- 머지에 포함할 파일: NYM_Scene.unity(기존 meta 유지), ProjectSettings/EditorBuildSettings.asset, .gitignore, AGENTS.md, HANDOFF.md. 실제 merge/commit/push·다른 PC 검수 미실행.


### 2026-10-12 14:35 — GPT
- 팀원 머지 대비 기준을 AGENTS.md에 추가: 동일 Unity/패키지 버전, 원본 meta/GUID·경로 대소문자 유지, 공통 타입 중복 방지, 직렬화 참조 변경 인수인계, 단일 NetworkManager, 머지 후 접속·인원별/경기 격리 검수.
- Build scenes를 NYM_Scene(index0)·SampleScene(index1)으로 등록. 테스트 빌드가 지정한 NYM_Scene에서 시작하도록 수정. 기존 SampleScene 보존.
- .gitignore에 /server-keys.json 추가. 실제 비밀키 파일·자격 증명 추가 없음.
- 읽기 검수: Assets/Packages의 meta GUID1667개 중 중복0, NYM Transport 내부 fileID 참조2개 정상, NetworkManager1개, Force Text·Visible Meta Files 활성. 씬/meta·NetworkContracts/meta·패키지 manifest/lock은 기존 Git 추적 확인.
- 현재 씬 뼈대 검수와 향후 팀원 코드 통합 검수는 별도. 기존 Player/Table 게임 스크립트 참조 복구·계정 키 타입·인증·키·프리팹 연결은 요청 대기. 실제 팀원 머지·서버/클라 접속 검수 미실행.
- 변경된 NYM_Scene·EditorBuildSettings.asset·.gitignore와 새 AGENTS/HANDOFF는 머지 전 함께 커밋 필요. 이 작업에서 commit/push 미실행. 기존 다른 변경 ProjectSettings.asset·SceneTemplateSettings.json은 수정하지 않음.


### 2026-10-12 14:30 — GPT
- AGENTS.md·HANDOFF.md 복구 완료. 테스트 씬 NYM_Scene, 코드 폴더 Assets/3.Script/NYM_Script 확정 기록.
- NYM_Scene에 NetworkRoot 생성: KcpTransport·EncryptionTransport·NetworkManager·MatchInterestManagement. Manager.transport→EncryptionTransport, EncryptionTransport.Inner→KcpTransport 실제 참조·저장된 fileID 확인. UDP7777·networkAddress=127.0.0.1.
- Main Camera에 UniversalAdditionalCameraData 부착. 씬 저장·활성 씬 확인. 원본 씬/프리팹·C# 수정 없음(변경 씬은 NYM_Scene만).
- 보안 준비: ClientValidateServerPubKey=List, ClientTrustedPubKeySignatures=[]; ServerLoadKeyPairFromFile=true, ServerKeypairPath=./server-keys.json. 실제 키 파일·신뢰 지문은 준비 필요. 비밀키 생성/저장 미실행.
- autoCreatePlayer=false·headlessStartMode=DoNothing·editorAutoStart=false. Authenticator·Player Prefab·Spawnable Prefabs는 코드/원본 참조 복구 후 연결 대기. maxConnections=100은 Mirror 기본값, 배포 수용량 결정 아님.
- 검증: Scene validate 이슈0·missingScripts0·brokenPrefabs0, 참조·포트·저장 내용 확인. Play Mode 진입/중지 호출 후 Edit Mode·isDirty=false 확인. 최신 Console 오류0. 실제 서버 기동·네트워크 접속·인증·경기·Linux 빌드 검수 미실행.
- 구성 중 MCP 참조 역직렬화 경고·Inner 미연결 상태 Port 조회 로그가 있었음. 최종 참조/Port 정상 확인. Console 임의 삭제 없음.
- Claude 후속 요청 유지: NYM_Script에 서버 코드·인증 경계·매칭/이탈 처리, 누락 Player/Table 원본·meta 복구, 계정 키 타입 일치. 규칙 해석 질문2건은 답변 대기.


### 2026-10-12 14:25 — GPT
- 사용자 요청으로 누락 AGENTS.md·HANDOFF.md 복구. 테스트 씬 NYM_Scene, 스크립트 NYM_Script 지정 반영.
- MCP 재조회 성공: LexioOnline@57f63501, Unity6000.2.8f1, 컴파일/리로드 없음, 작업 가능.
- 활성 씬 미저장·변경 없음 확인. NYM_Scene 저장 파일에는 카메라·조명만 존재.
- NetworkContracts.cs 추가 확인. 게임 컴포넌트·인증·경기 구현은 Claude 담당으로 요청 유지.

### 2026-10-12 14:20 — GPT
- 서버 뼈대 선제작 구성·Claude 요청 정리. 당시 MCP 에디터 조회 실패·협업 MD 누락으로 씬 변경 미실행.
- 임시 계획 문서: C:/Users/m/Documents/ChatGPT/렉시오 모작 프로젝트 2/렉시오-서버뼈대-인수인계.md.

### 2026-10-12 11:16 — GPT
- 첨부 통합 명세·확인 항목 v2.1 검토, 기초 틀·사용자 준비 목록·Claude 요청 정리.
- 타일 앞면60개·Back1개·Network 프리팹2개 확인. LexioPlayer·LexioTable 게임 스크립트 참조 meta 없음.
- 기존 계획 문서: C:/Users/m/Documents/ChatGPT/렉시오 모작 프로젝트 2/렉시오-오늘작업-준비목록.md.

### 2026-10-12 — 기존 기록 요약
- Claude가 협업 문서 생성·팀원 코드 수정 금지·Inspector 구성·명명·최소 파일 규칙 기록.
- GPT가 MCP 연결·역할 분담 확인. 당시 씬/코드 변경 없음.
