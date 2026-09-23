Read `.claude\skills\write-plans\SKILL.md` & `.claude\skills\write-plans\TL_DR.md` before start.

## TL;DR

1. Goal: PlayerNickName 표기(고정/조건부 편집), 캔버스 분리.
2. Scope: WindowGameStart, WindowSignUp, LoginManager, NetcodeBootstrap, Lobby UI.prefab.
3. Note: Open Questions (4차) #1~#3 결정 대기.

## 작업 요청
- 이번 플랜 파일 내 한정으로, PlayerName InputField를 PlayerDisplayName이라 부르겠음. 혼동 방지.
- WIN_GameStart의 기존 PlayerName InputField를 InputField 대신 이미지와 TMP 텍스트로 변경
  - 즉, 개선 전과 후 둘다 모습은 차이가 없지만, 텍스트 입력 기능이 완전히 삭제되고 고정값 대입 방식이 됨.
- WIN_GameStart가 지금 하나의 Canvas에 자식 오브젝트들이 배치되어 있는데,<br>이걸 다른 WIN처럼 용도에 따라 여러 캔버스로 분류할 것.
  - Static Group: Label Text와 같이 계속 Static인 디자인용
  - Dynamic Group: Button과 같이 유저 입력에 따른 UI 갱신이 있는 경우
  - Interaction Group: Dynamic Group과 동일한 역할. 다만 InputField처럼 UI 갱신이 매우 자주 호출됨.
- 이미 로그인 된 상태에서 nickname 가져와서 PlayerDisplayName에서 표기<br>
  - 자동 로그인 시 accountinfo로 이름 가져오는 로직 구현 대상 포함
- 로그인이 안된 Guest유저 - `Guest#nnnn` 형식으로 PlayerDisplayName에서 닉네임 표기
  - n = 랜덤 숫자

> 참고사항:
> `C:\Users\User\Documents\Server_yijoongwon\allInOneGameServer`는 JW서버 소스 코드
> /api/game/accountinfo는 JW서버 소스 코드 경로의 코드 및 `ApiGuide.razor` 참고

---

# 수정 내용 (1차)

## 범위

| 종류 | 대상 | 내용 |
|---|---|---|
| 스크립트 (신규) | `Assets/JWServer/Runtime/DTOs/AccountInfoResponseDTO.cs` | `/api/game/accountinfo` 응답 DTO |
| 스크립트 | `Assets/JWServer/Runtime/Utils/LoginManager.cs` | 닉네임 조회/보관 |
| 스크립트 | `Assets/GalaxyIO/Scripts/Managed/Popup/Window/WindowGameStart.cs` | 입력창 → 고정 텍스트, 이름 경고 제거 |
| 스크립트 | `Assets/GalaxyIO/Scripts/DOTS/Components/Actor/PlayerNameComponent.cs` | 주석만 수정 |
| 프리팹 | `Assets/GalaxyIO/Prefabs/Lobby/Lobby UI.prefab` | `WIN_GameStart` 캔버스 분리, PlayerDisplayName 변환 |

유지:
- `GameStartBackend.cs` (`nameField` 사용 중이나 이번 작업 대상 아님).
- 서버 accountinfo 응답 스펙. `responseMessage`가 `"OK"`라 기존 `ResponseMsgUtil.IsOk`로 판별 가능.

## Step 1. `Assets/JWServer/Runtime/DTOs/AccountInfoResponseDTO.cs` (신규)

사용하는 필드만 선언. 나머지 키는 JsonUtility가 무시.

```csharp
+using System;
+
+namespace JWServer.Runtime.DTO
+{
+    /// <summary>
+    /// JW 게임 서버 `/api/game/accountinfo` 응답 DTO<p/>
+    /// JsonUtility로 역직렬화되므로 필드명이 곧 JSON 키가 됨 -> 반드시 서버 스펙과 동일하게 유지할 것
+    /// </summary>
+    [Serializable]
+    public struct AccountInfoResponseDTO
+    {
+        /// <summary>
+        /// 응답 메세지
+        /// </summary>
+        public string responseMessage;
+
+        /// <summary>
+        /// 게임 계정 닉네임
+        /// </summary>
+        public string accountNickName;
+    }
+}
```

## Step 2. `Assets/JWServer/Runtime/Utils/LoginManager.cs`

로그인/회원가입/자동 로그인 성공 직후 accountinfo 조회. 토큰이 막 검증된 시점이라 `WithTokenRefresh` 생략.

```csharp
        /// <summary>
        /// 로그인/회원가입으로 받은 유저 id. 자동 로그인 시 서버가 주지 않아 빈 값.
        /// </summary>
        private static string sUserId;
        public static string UserId => sUserId;
+
+        /// <summary>
+        /// 게임 계정 닉네임. 로그인 성공 시 accountinfo로 받아옴. 비로그인/조회 실패 시 빈 값.
+        /// </summary>
+        private static string sNickName;
+        public static string NickName => sNickName;
```

```csharp
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticData()
        {
            sUserTokenData = default;
            sIsOnline = false;
            sUserId = string.Empty;
+            sNickName = string.Empty;
        }
```

`SignUp`, `LogIn` 둘 다 동일하게 수정.

```csharp
                if (ResponseMsgUtil.IsOk(response.responseMessage))
                {
                    sUserTokenData = new GameUserTokenDTO(response);
                    sIsOnline = true;
                    sUserId = response.userId;
                    SaveAuth();
+                    await FetchAccountInfo();
                }
```

```csharp
            // 성공 — Access토큰과 Refresh토큰 모두 유효함
            if (ResponseMsgUtil.IsOk(dto.responseMessage))
            {
                sIsOnline = true;
                DebugLog.Log($"자동 로그인 정보:\n{sUserTokenData}");
+                await FetchAccountInfo();
            }
```

```csharp
        public static void LogOut()
        {
            sUserTokenData = default;
            sIsOnline = false;
            sUserId = string.Empty;
+            sNickName = string.Empty;
            if (File.Exists(sPersistentDataPath))
                File.Delete(sPersistentDataPath);
        }
```

`GameStart()` 아래에 추가.

```csharp
+        /// <summary>
+        /// 게임 계정 정보를 조회해 닉네임 저장. 실패 시 빈 값.
+        /// </summary>
+        private static async Awaitable FetchAccountInfo()
+        {
+            const string accountInfoEndpoint = "/api/game/accountinfo";
+
+            using (UnityWebRequest req = UnityWebRequest.Post(AUTH_SERVER + accountInfoEndpoint, "", CONTENT_TYPE_JSON))
+            {
+                req.SetRequestHeader("Authorization", "Bearer " + sUserTokenData.accessToken);
+                await req.SendWebRequest();
+
+                // 오류 응답(4xx/5xx)엔 닉네임이 없음.
+                if (req.result != UnityWebRequest.Result.Success)
+                {
+                    sNickName = string.Empty;
+                    return;
+                }
+
+                AccountInfoResponseDTO response = JsonUtility.FromJson<AccountInfoResponseDTO>(req.downloadHandler.text);
+                sNickName = ResponseMsgUtil.IsOk(response.responseMessage) ? response.accountNickName : string.Empty;
+            }
+        }
```

## Step 3. `Assets/GalaxyIO/Scripts/Managed/Popup/Window/WindowGameStart.cs`

이름이 항상 채워지므로 빈 이름 검사와 `nameWarning` 제거.

```csharp
    public class WindowGameStart : WindowBase
    {
        [SerializeField] private IpAddressField addressField;
-        [SerializeField] private TMP_InputField playerName_if;
-        [SerializeField] private TMP_Text nameWarning;
+        [SerializeField] private TMP_Text playerName_txt;
        [SerializeField] private TMP_Text warning;
        [SerializeField] private TMP_Text unreachable;
        [SerializeField] private Button startButton;
        [SerializeField] private Button loopbackButton;
+
+        // 이번에 창을 열 때 확정한 이름.
+        private string playerName;
```

```csharp
        protected override void Awake()
        {
            base.Awake();
            _connected = new UnityEvent();
            _changeWindow = new UnityEvent<string>();
-            playerName_if.characterLimit = PlayerNameComponent.MaxLength;
        }

        protected override void Start()
        {
            base.Start();

            warning.gameObject.SetActive(false);
            unreachable.gameObject.SetActive(false);
-            nameWarning.gameObject.SetActive(false);
```

```csharp
        public override void Open()
        {
            base.Open();
-            // 로그인 유저는 로그인 id를 제안값으로 채움. 수정 가능, 서버 계정과 무관.
-            playerName_if.text = LoginManager.UserId;
+            playerName = ResolvePlayerName();
+            playerName_txt.SetText(playerName);
        }
```

```csharp
        private void OnPressLoopback()
        {
            if (!IsActive()) return;

            addressField.Fill(NetcodeBootstrap.LoopbackAddress);
            warning.gameObject.SetActive(false);
            unreachable.gameObject.SetActive(false);
-            nameWarning.gameObject.SetActive(false);
        }
```

```csharp
        private void OnPressGameStart()
        {
            if (!IsActive()) return;

            unreachable.gameObject.SetActive(false);

-            // 공백만 입력한 경우도 빈 이름으로 취급함.
-            string playerName = playerName_if.text.Trim();
-
-            if (playerName.Length == 0)
-            {
-                warning.gameObject.SetActive(false);
-                nameWarning.gameObject.SetActive(true);
-                return;
-            }
-
-            nameWarning.gameObject.SetActive(false);
-
            // 모양이 맞아도 값이 범위를 넘으면(e.g. 999.0.0.1) 여기서 걸림.
```

```csharp
            startButton.interactable = !value;
            loopbackButton.interactable = !value;
-            playerName_if.interactable = !value;
        }
+
+        /// <summary>
+        /// 로그인 유저는 계정 닉네임, 비로그인 유저는 게스트 이름.
+        /// </summary>
+        private string ResolvePlayerName()
+        {
+            string nickName = LoginManager.NickName;
+
+            // 닉네임 조회 실패 시에도 게스트 이름 사용. 창 열 때마다 새 번호.
+            if (!LoginManager.IsOnline || string.IsNullOrEmpty(nickName))
+                return $"Guest#{Random.Range(0, 10000):D4}";
+
+            return nickName.Length > PlayerNameComponent.MaxLength
+                ? nickName.Substring(0, PlayerNameComponent.MaxLength)
+                : nickName;
+        }
    }
```

> `Random`은 `UnityEngine.Random`. 현재 `using System;`이 없어 모호성 없음.

## Step 4. `Assets/GalaxyIO/Scripts/DOTS/Components/Actor/PlayerNameComponent.cs`

```csharp
-        // 입력창 글자 수 제한. 한글 기준 3바이트씩이라 FixedString64Bytes 에 들어감.
+        // 이름 글자 수 제한. 한글 기준 3바이트씩이라 FixedString64Bytes 에 들어감.
        public const int MaxLength = 12;
```

## Step 5. `Lobby UI.prefab` — `WIN_GameStart` 수정 (에디터 작업)

### 5-1. 캔버스 분리

`WIN_SignIn`의 그룹 설정과 동일하게 맞춤.

| 그룹 | 컴포넌트 | RectTransform |
|---|---|---|
| Dynamic Group | Canvas(RenderMode 기본값), GraphicRaycaster | Stretch 전체, Offset 0 |
| Interaction Group | Canvas, GraphicRaycaster | 동일 |

변경 전 → 후:

```diff
 WIN_GameStart
-  Canvas                      (Canvas RenderMode=2, GraphicRaycaster)
-    Button
-    IpField
-    PlayerName InputField
-    Loopback
-    Warning
-    Unreachable
-    NameWarning
+  Dynamic Group               (Canvas, GraphicRaycaster)
+    Button
+    Loopback
+    PlayerNickName            (PlayerDisplayName)
+    Warning
+    Unreachable
+  Interaction Group           (Canvas, GraphicRaycaster)
+    IpField
```

- 기존 `Canvas` 오브젝트는 이름을 `Dynamic Group`으로 바꾸고 `m_RenderMode`를 SignIn 그룹과 동일하게 `0`으로 맞춤.
  - 중첩 Canvas는 `renderMode` 대입이 무시됨. 프리팹 YAML에서 직접 수정.
- `Interaction Group`은 새로 만들고 `Dynamic Group`의 Canvas/GraphicRaycaster 설정 복사. `IpField`만 이동.
- 자식은 월드 위치 유지하며 이동(부모 RectTransform이 동일하게 stretch라 위치 변화 없음).
- `NameWarning` 오브젝트 삭제.

### 5-2. PlayerDisplayName 변환

```diff
-PlayerName InputField   (Image, TMP_InputField, InputField_CheckVaildateChar)
-  Text Area             (RectMask2D)
-    Placeholder         (TMP_Text, LayoutElement)
-    Text                (TMP_Text)
+PlayerNickName          (Image)
+  Text Area             (RectMask2D)
+    Text                (TMP_Text)
```

- `TMP_InputField`, `InputField_CheckVaildateChar` 컴포넌트 제거. Image는 유지해 외형 동일.
- `Placeholder` 삭제.
- `Text`의 TMP 설정(폰트/크기/정렬/색) 그대로 유지. Overflow는 `Ellipsis`, 기본 텍스트(`​`)는 빈 값.

### 5-3. `WindowGameStart` 인스펙터 재연결

| 필드 | 연결 대상 |
|---|---|
| `playerName_txt` | `Dynamic Group/PlayerNickName/Text Area/Text` |
| 나머지 (`addressField`, `warning`, `unreachable`, `startButton`, `loopbackButton`) | 기존 오브젝트 그대로 (이동 후 참조 유지 확인) |

## 검증

- 비로그인 → Connect: `Guest#0000`~`Guest#9999` 표기, 창 재진입 시 새 번호.
- 로그인(ID/PW) → Connect: accountinfo 닉네임 표기.
- 자동 로그인 → Connect: accountinfo 닉네임 표기.
- 로그아웃 → Connect: 게스트 이름 표기.
- 게임 시작 → 인게임 이름 표시가 PlayerDisplayName 값과 동일.

## Open Questions (1차)

1. ~~Static Group: `WIN_GameStart`에는 현재 Label 같은 정적 요소가 없음. 빈 `Static Group`도 만들지, 생략할지?~~ → 생략. 계획 그대로.
2. ~~오브젝트 이름: `PlayerName InputField` → `PlayerName`으로 변경 예정. 다른 이름 원하면 알려주세요.~~ → `PlayerNickName` 반영 (Step 5).
3. ~~닉네임이 12자 초과 시 잘라서 표기/전송함. 서버 닉네임 최대 길이 기준을 따로 둘지?~~ → 현재 유지.
4. ~~Guest 번호: 세션 동안 고정(창 재진입 시 같은 번호)으로 계획함. 창 열 때마다 새로 뽑을지?~~ → 창 열 때마다 새 번호. `guestName` 필드 제거, `ResolvePlayerName`에서 생성 (Step 3).

### Answer
1. 생략
2. PlayerNickName
3. 현재 유지
4. 테스트 시 1로컬 n세션 환경이 가능하므로 후자로 진행

## 구현 결과

Step 1~5 반영 완료. 컴파일 에러 없음.

| 대상 | 결과 |
|---|---|
| `AccountInfoResponseDTO.cs` | 신규. `responseMessage`, `accountNickName` |
| `LoginManager.cs` | `NickName` 추가. `LogIn`/`SignUp`/`AutoLogIn` 성공 시 `FetchAccountInfo()` 호출 |
| `WindowGameStart.cs` | `playerName_txt` 고정 표기, `ResolvePlayerName()` 추가, `nameWarning` 제거 |
| `PlayerNameComponent.cs` | 주석 수정 |
| `Lobby UI.prefab` | `Dynamic Group` / `Interaction Group` 분리, `PlayerNickName` 변환, `NameWarning` 삭제, `playerName_txt` 연결 |

```
WIN_GameStart
  Dynamic Group      Button, PlayerNickName, Loopback, Warning, Unreachable
  Interaction Group  IpField
```

참고:
- 프리팹 수정은 임시 에디터 메뉴 스크립트로 실행 후 삭제함. MCP `execute_code`가 컴파일 실패(`Line 1: ﻿`)해서 우회.
- 수정 전 프리팹과 비교 시 위 항목 외 변경 없음.
- `GameStartBackend.cs`의 `nameWarning`은 범위 밖이라 유지.

검증:
- 플레이 검증 결과는 `## 구현 검증 결과` 참고.

## 구현 검증 결과
- `SO_NmhnConfigData.Instance.ModeType == MODETYPE.LOCAL` 환경에서 테스트 결과, 로그인 상태임에도 Guest displayName을 표기함
- 해당 부분 개선해야 재검증이 가능. 또한, 현재 claude는 MCP에 연결되어있고 JW서버가 켜진 환경이라 검증이 스스로 가능함. 

---

# 수정 내용 (2차) — 재검증 대응

## 원인 분석

클라이언트 로직 문제 아님. 서버 계정의 닉네임이 빈 값이라 게스트 이름으로 폴백된 것.

저장된 토큰(`test01`)으로 직접 호출한 결과:

```
POST /api/game/accountinfo  → 200
{"responseCode":"200","responseMessage":"OK", ..., "accountNickName":"", ...}
```

- `WindowSignUp`이 `GameUserRequestDTO`에 `_nickName`을 넘기지 않음. 서버는 빈 닉네임을 null로 저장.
- `WindowSignIn`도 닉네임 없음. 로그인 시 계정이 새로 생기는 경우도 동일.
- 서버에 가입 후 닉네임을 바꾸는 클라이언트 API 없음(Admin 페이지만 가능).
- 서버 닉네임 규칙: 최대 100자, 게임 내 중복 불가(409 `NICKNAME_ALREADY_EXISTS`).
- `MODETYPE.LOCAL`은 로그인 흐름과 무관. `DebugObj`, 로그 출력 등에만 쓰임.

## 수정 방향

회원가입 시 닉네임(필수, 12자)을 받아 서버에 전달. 기존 빈 닉네임 계정은 대응하지 않음(서비스 전, 실사용 계정은 모두 닉네임 보유).

| 종류 | 대상 | 내용 |
|---|---|---|
| 스크립트 | `Assets/JWServer/Runtime/Utils/Definitions/ResponseMessageDefinitions.cs` | `NICKNAME_ALREADY_EXISTS` 추가 |
| 스크립트 | `Assets/GalaxyIO/Scripts/Managed/Popup/Window/WindowSignUp.cs` | 닉네임 입력/검사/전송 |
| 프리팹 | `Assets/GalaxyIO/Prefabs/Lobby/Lobby UI.prefab` | `WIN_SignUp`에 `Enter NickName` 추가 |

## Step 6. `Assets/JWServer/Runtime/Utils/Definitions/ResponseMessageDefinitions.cs`

```csharp
        public const string ACCOUNT_ALREADY_EXISTS = "ACCOUNT_ALREADY_EXISTS";
+        public const string NICKNAME_ALREADY_EXISTS = "NICKNAME_ALREADY_EXISTS";
        public const string EXCEPTION_OCCURRED = "EXCEPTION_OCCURRED";
```

## Step 7. `Assets/GalaxyIO/Scripts/Managed/Popup/Window/WindowSignUp.cs`

인게임 이름과 같은 길이 제한 사용(서버 100자보다 짧음).

```csharp
+using GalaxyIO.DOTS.Components.Actor;
 using GalaxyIO.Tools.PopupUI.Popup;
```

```csharp
        [SerializeField] private TMP_InputField memberName_if;
+        [SerializeField] private TMP_InputField nickName_if;
        [SerializeField] private TMP_Text errorMsg_txt;
```

```csharp
            changeWindow = new UnityEvent<string>();
            memberName_if.characterLimit = MemberNameMaxLength;
+            nickName_if.characterLimit = PlayerNameComponent.MaxLength;
        }
```

```csharp
            memberName_if.text = string.Empty;
+            nickName_if.text = string.Empty;
            errorMsg_txt.text = string.Empty;
```

```csharp
            if (string.IsNullOrEmpty(memberName))
            {
                errorMsg_txt.text = "Invalid Name!";
                return;
            }
+            string nickName = nickName_if.text.Trim();
+            if (string.IsNullOrEmpty(nickName))
+            {
+                errorMsg_txt.text = "Invalid NickName!";
+                return;
+            }
            #endregion
```

```csharp
                _memberPw: pw,
-                _memberName: memberName
+                _memberName: memberName,
+                _nickName: nickName
            );
```

```csharp
            if (result.responseMessage is RefreshTokenMsg.ACCOUNT_ALREADY_EXISTS)
            {
                WriteErrorMsgText("이미 존재하는 회원입니다.");
                return;
            }
+            if (result.responseMessage is RefreshTokenMsg.NICKNAME_ALREADY_EXISTS)
+            {
+                WriteErrorMsgText("이미 사용 중인 닉네임입니다.");
+                return;
+            }
```

## Step 8. `Lobby UI.prefab` — `WIN_SignUp` (에디터 작업)

```diff
 WIN_SignUp
   Interaction Group
     Enter ID
     Enter PW
     Enter PW_Validate
     Enter Name
+    Enter NickName        (Enter Name 복제. Label 텍스트 "NickName")
+      Label
+      InputField
```

- `Enter NickName`은 `Enter Name` 아래에 추가만 함. 다른 오브젝트 배치는 건드리지 않음(개발자가 추후 조정).
- `WindowSignUp.nickName_if` ← `Interaction Group/Enter NickName/InputField`.

## 재검증 (Claude 직접 수행)

Unity MCP 플레이 모드 + JW 서버(localhost:5050) 환경. 닉네임 없는 기존 계정(`test01`)은 검증 대상에서 제외, 새로 가입한 계정으로 확인.

- 신규 가입(닉네임 입력) → Connect: 입력한 닉네임 표기.
- 같은 닉네임으로 재가입 → "이미 사용 중인 닉네임입니다." 표기.
- 자동 로그인(재실행) → Connect: 닉네임 표기.
- 로그아웃 → Connect: `Guest#nnnn` 표기, 창 재진입 시 새 번호.
- 게임 시작 → 인게임 이름이 표시 이름과 동일.
- 각 단계에서 `/api/game/accountinfo` 응답도 curl로 대조.

## Open Questions (2차)

1. ~~기존 빈 닉네임 계정(`test01` 등): 로그인 상태인데 닉네임이 없을 때 표기 방식?~~ → 대응 안 함. 코드 변경 없음(현재 `Guest#nnnn` 폴백 그대로).
2. ~~회원가입 닉네임 필수 여부?~~ → 필수. 빈 값이면 "Invalid NickName!" (Step 7).
3. ~~회원가입 닉네임 글자 수 12자 유지?~~ → 12자 유지 (Step 7).
4. ~~`WIN_SignUp` 레이아웃?~~ → `Enter NickName` 추가만. 배치는 개발자가 추후 확인 (Step 8).

### Answer
1. 아직 서비스 시작 안한 프로젝트니까 해당 부분 대응 안해도 됨. 실사용에선 모두 닉네임 입력함
2. 필수. Invaild 표기 필요
3. 12자 유지
4. 추가. 배치는 나중에 개발자가 확인.

### 반영 결과

- 1: 코드 변경 없음. 재검증은 `test01` 대신 새로 가입한 계정으로 수행 (`## 재검증`).
- 2, 3: Step 7 그대로.
- 4: Step 8은 `Enter NickName` 추가만. 다른 오브젝트 배치 유지.
- 질문 번호는 답변 기준 1~4로 맞춤. 대화 기록의 #5~#8은 기록이라 유지.
- 코드/프리팹은 아직 미변경. 다음 단계: Step 6~8 구현 → Unity MCP + JW 서버로 재검증 직접 수행.

## 구현 결과 (2차)

Step 6~8 반영 완료. 컴파일 에러 없음.

| 대상 | 결과 |
|---|---|
| `ResponseMessageDefinitions.cs` | `NICKNAME_ALREADY_EXISTS` 추가 |
| `WindowSignUp.cs` | `nickName_if` 추가(12자 제한), 빈 값 검사, `_nickName` 전송, 중복 닉네임 메시지 |
| `Lobby UI.prefab` | `WIN_SignUp/Interaction Group/Enter NickName` 추가, `nickName_if` 연결 |

`Enter NickName`:
- `Enter Name` 복제. 위치 `(0, -200)` (다른 필드와 같은 100 간격).
- Label `Enter NickName`, Placeholder `Enter your nickname...`.
- 다른 오브젝트 배치는 그대로. `Error Msg`(y -150, 높이 50) 바로 아래라 겹치지는 않음. 최종 배치는 개발자 확인.
- 복제본 InputField 내부 참조(Text Area/Text/Placeholder)가 복제본 자식을 가리키는 것 확인.

참고:
- 프리팹 수정·재검증 모두 임시 에디터 메뉴 스크립트로 수행 후 삭제(`execute_code` 컴파일 실패 우회).
- 수정 전 프리팹 백업(scratchpad `Lobby UI.prefab.bak2`)은 검증 후 삭제함.

## 재검증 결과

Unity MCP 플레이 모드 + JW 서버(localhost:5050). 전 항목 통과.

| 항목 | 결과 |
|---|---|
| 가입 시 닉네임 빈 값 | "Invalid NickName!" 표기 |
| 신규 가입(`vtest309485` / `Nick3364`) → Connect | `Nick3364` 표기 |
| 같은 닉네임으로 재가입(`vtest131388`) | 중복 닉네임 메시지 표기. 서버에 계정 미생성 확인(`memberiddupcheck`) |
| 로그아웃 → Connect → 재진입 | `Guest#6403` → `Guest#8815` (새 번호) |
| ID/PW 로그인 → Connect | `Nick3364` 표기 |
| 자동 로그인(플레이 재시작) → Connect | `Nick3364` 표기 (`UserId`는 설계대로 빈 값) |
| 게임 시작 → 인게임 이름표 | `NameLabel(Clone)` 텍스트 `Nick3364` |

부수 효과:
- JW 서버에 테스트 계정 `vtest309485`(닉네임 `Nick3364`) 생성됨.
- 로컬 자동 로그인 토큰(`thunder_veil_auth.json`)이 `test01` → `vtest309485`로 바뀜. `test01`로 돌아가려면 재로그인 필요.

---

# 수정 내용 (3차) — `WIN_SignUp` 오류 메시지 분리

## 요청

`errorMsg_txt` 1개를 3개로 나눠 오류를 입력 그룹별로 표기.

| 그룹 | 대상 입력칸 | 오류 |
|---|---|---|
| 계정 | ID, PW, PW 확인 | 빈 ID/PW/PW 확인, PW 6자 미만, PW 불일치, ID 중복, 기존 회원 |
| 실명 | Name | 빈 실명 |
| 닉네임 | NickName | 빈 닉네임, 닉네임 중복 |

## 현재 상태

- `Error Msg` 1개가 `Dynamic Group`에 있음. y -150 (`Enter Name`과 `Enter NickName` 사이).
- 버튼은 개발자가 y 61(하단 기준)로 옮겨 둠.
- 입력칸은 100 간격(ID 200 ~ NickName -200), 높이 50. 각 입력칸 아래 50 공간이 비어 있음.

## Step 9. `Assets/GalaxyIO/Scripts/Managed/Popup/Window/WindowSignUp.cs`

첫 오류에서 멈추는 기존 흐름 유지. 표기 위치만 그룹별로 나눔 (Open Questions (3차) #1).

```csharp
        [SerializeField] private TMP_InputField nickName_if;
-        [SerializeField] private TMP_Text errorMsg_txt;
+        [SerializeField] private TMP_Text accountErrorMsg_txt;
+        [SerializeField] private TMP_Text memberNameErrorMsg_txt;
+        [SerializeField] private TMP_Text nickNameErrorMsg_txt;
```

```csharp
            nickName_if.text = string.Empty;
-            errorMsg_txt.text = string.Empty;
+            ClearErrorMsg();
        }
```

```csharp
        private async Awaitable RunSignUp()
        {
            #region 서버 체크 전 조건 확인
            // [TEMP] errorMsg_txt 부분 전체적으로 번역 적용 시 텍스트 넘겨주기 대신 키 값 넘겨주기 필요
+            ClearErrorMsg();
            string id = id_if.text;
            if (string.IsNullOrEmpty(id))
            {
-                errorMsg_txt.text = "Invalid ID!";
+                accountErrorMsg_txt.text = "Invalid ID!";
                return;
            }
```

같은 방식으로 교체:

| 줄 (현재) | 메시지 | 변경 후 대상 |
|---|---|---|
| 98 | `Invalid Password!` | `accountErrorMsg_txt` |
| 103 | `패스워드는 6자리를 넘어야합니다` | `accountErrorMsg_txt` |
| 110 | `Invalid Validate Password!` | `accountErrorMsg_txt` |
| 116 | `Validate Password doesn't match!` | `accountErrorMsg_txt` |
| 122 (주석 블록) | `아이디와 비밀번호가 동일할 수 없습니다` | `accountErrorMsg_txt` |
| 129 | `Invalid Name!` | `memberNameErrorMsg_txt` |
| 135 | `Invalid NickName!` | `nickNameErrorMsg_txt` |

```csharp
            //서버 체크
-            errorMsg_txt.text = string.Empty;
            Deactivate();
```

```csharp
            if (isDuplicateId)
            {
-                WriteErrorMsgText("이미 존재하는 회원 아이디입니다.");
+                WriteErrorMsgText(accountErrorMsg_txt, "이미 존재하는 회원 아이디입니다.");
                return;
            }
```

```csharp
            if (ResponseMsgUtil.IsInvalidCredential(result.responseMessage))
            {
-                WriteErrorMsgText("이미 존재하는 회원 아이디입니다.");
+                WriteErrorMsgText(accountErrorMsg_txt, "이미 존재하는 회원 아이디입니다.");
                return;
            }
            // 이미 존재하는 회원 정보(아이디 비번 전부 동일)로 회원가입 시도 시
            if (result.responseMessage is RefreshTokenMsg.ACCOUNT_ALREADY_EXISTS)
            {
-                WriteErrorMsgText("이미 존재하는 회원입니다.");
+                WriteErrorMsgText(accountErrorMsg_txt, "이미 존재하는 회원입니다.");
                return;
            }
            if (result.responseMessage is RefreshTokenMsg.NICKNAME_ALREADY_EXISTS)
            {
-                WriteErrorMsgText("이미 사용 중인 닉네임입니다.");
+                WriteErrorMsgText(nickNameErrorMsg_txt, "이미 사용 중인 닉네임입니다.");
                return;
            }
```

```csharp
            if (msgBox == null)
            {
-                errorMsg_txt.text = "something error...";
+                accountErrorMsg_txt.text = "something error...";
                DebugLog.LogError($"프리팹 {key} 등록 안됨!");
            }
```

```csharp
            // [TEMP] 번역 적용 시 I2방식으로 변경 필요
            // 실패 메세지 출력(비밀번호 틀릴 시 같은 굳이 창이 뜰 필요 없는 경우)
-            void WriteErrorMsgText(string text)
+            void WriteErrorMsgText(TMP_Text target, string text)
            {
-                errorMsg_txt.text = text;
+                target.text = text;
            }
            #endregion
        }
+
+        private void ClearErrorMsg()
+        {
+            accountErrorMsg_txt.text = string.Empty;
+            memberNameErrorMsg_txt.text = string.Empty;
+            nickNameErrorMsg_txt.text = string.Empty;
+        }
```

## Step 10. `Lobby UI.prefab` — `WIN_SignUp` (에디터 작업)

```diff
 WIN_SignUp
   Dynamic Group
     Sign Up Enter Btn
     Sign Up Cancel Btn
-    Error Msg                 (y -150)
+    Error Msg_Account         (y  -50)  PW 확인 아래
+    Error Msg_MemberName      (y -150)  실명 아래. 기존 Error Msg 이름만 변경
+    Error Msg_NickName        (y -250)  닉네임 아래
```

- `Error Msg_Account`, `Error Msg_NickName`은 `Error Msg_MemberName` 복제. 앵커/크기/폰트 동일, y만 변경.
- 다른 오브젝트 배치는 그대로. y 값은 초기 배치이며 최종 위치는 개발자가 조정.

`WindowSignUp` 인스펙터 연결:

| 필드 | 연결 대상 |
|---|---|
| `accountErrorMsg_txt` | `Dynamic Group/Error Msg_Account` |
| `memberNameErrorMsg_txt` | `Dynamic Group/Error Msg_MemberName` |
| `nickNameErrorMsg_txt` | `Dynamic Group/Error Msg_NickName` |

## 검증 (Claude 직접 수행)

- 빈 ID / PW 6자 미만 / PW 불일치 → 계정 칸에만 표기.
- 빈 실명 → 실명 칸에만 표기.
- 빈 닉네임 / 중복 닉네임 → 닉네임 칸에만 표기.
- 오류 후 다시 가입 시도 → 이전 오류가 지워짐.
- 중복 ID → 계정 칸에 표기.

## Open Questions (3차)

1. ~~표기 방식: 첫 오류에서 멈춤 vs 3칸 동시 표기?~~ → 기존 유지(첫 오류에서 멈춤).
2. ~~위치/이름?~~ → `Error Msg_Name`을 `Error Msg_MemberName`으로 변경. 위치는 개발자가 직접 조정.
3. ~~기타 오류(`something error...`)를 계정 칸에 표기?~~ → 확정.

### Answer
1. 기존 유지
2. `Error Msg_Name` -> `Error Msg_MemberName`으로 변경. 위치는 만들면 내가 직접 수정하겠음
3. 오케이

### 반영 결과

- 1: Step 9 흐름 그대로(첫 오류에서 `return`).
- 2: 오브젝트 이름 `Error Msg_MemberName`으로 변경. 코드 필드도 기존 `memberName_if`에 맞춰 `nameErrorMsg_txt` → `memberNameErrorMsg_txt`로 변경. 프리팹 y 값은 초기 배치로만 두고 최종 위치는 개발자 조정.
- 3: `something error...`는 `accountErrorMsg_txt`에 표기 확정.
- 코드/프리팹은 아직 미변경. 다음 단계: Step 9~10 구현 → 직접 검증.

## 구현 결과 (3차)

Step 9~10 반영 완료. 컴파일 에러 없음.

| 대상 | 결과 |
|---|---|
| `WindowSignUp.cs` | `errorMsg_txt` → `accountErrorMsg_txt` / `memberNameErrorMsg_txt` / `nickNameErrorMsg_txt`. `ClearErrorMsg()` 추가, `WriteErrorMsgText(target, text)` |
| `Lobby UI.prefab` | `Error Msg` → `Error Msg_MemberName`(y -150). 복제로 `Error Msg_Account`(y -50), `Error Msg_NickName`(y -250) 추가. 3필드 연결 |

- 수정 전 프리팹과 비교 시 위 항목 외 변경 없음. 백업(scratchpad `Lobby UI.prefab.bak3`)은 검증 후 삭제함.
- 프리팹 수정·검증은 임시 에디터 메뉴 스크립트로 수행 후 삭제.

## 검증 결과 (3차)

Unity MCP 플레이 모드 + JW 서버. 전 항목 통과. 각 시도마다 이전 오류가 지워짐.

| 입력 | 계정 칸 | 실명 칸 | 닉네임 칸 |
|---|---|---|---|
| 빈 ID | `Invalid ID!` | - | - |
| PW 3자 | `패스워드는 6자리를 넘어야합니다` | - | - |
| PW 불일치 | `Validate Password doesn't match!` | - | - |
| 빈 실명 | - | `Invalid Name!` | - |
| 빈 닉네임 | - | - | `Invalid NickName!` |
| 중복 닉네임(`Nick3364`) | - | - | `이미 사용 중인 닉네임입니다.` |
| 중복 ID(`vtest309485`) | `이미 존재하는 회원 아이디입니다.` | - | - |

- 중복 닉네임 시도 계정(`etest633396`)은 서버에 생성되지 않음 확인.

---

# 수정 내용 (4차) — `PlayerNickName` 입력 가능 조건

## 요청

1. `WIN_GameStart`의 `PlayerNickName`을 Image + TMP Text → InputField로 변경.
2. 이름 편집 가능 여부를 서버/로그인 상태로 나눔.

| 서버 | 로그인 | 이름 |
|---|---|---|
| 공용 | 로그인 | 고정 (현재 로직) |
| 공용 | 비로그인 | 고정 (현재 로직) |
| 사설 | 비로그인 | 편집 가능 (신규) |
| 사설 | 로그인 | 고정 (현재 로직) |

## 조사 결과

### 사설 서버 + 로그인 조합은 성립함

- 로그인은 JW 인증 서버(`LoginManager.AUTH_SERVER`) 대상, 게임 접속은 `WIN_GameStart`에 입력한 IP의 Netcode 서버(`NetcodeBootstrap.Port` 7979) 대상. 두 경로가 서로 독립임.
- 3차 검증에서 로그인(`vtest309485`) 상태로 루프백(`127.0.0.1`)에 접속·스폰까지 성공함. 이미 실재하는 조합.
- 따라서 "사설 + 로그인 = 고정" 규칙을 그대로 둠.

### 공용/사설 구분 기준이 코드에 없음

- `NetcodeBootstrap`은 `Port`, `LoopbackAddress`만 가짐. 공용 서버 주소 개념 없음.
- `SO_NmhnConfigData`에도 서버 주소 필드 없음(`ModeType`, `OsType`뿐).
- `WIN_GameStart`는 항상 사용자가 IP를 입력해 접속하므로, 현재 상태에선 모든 접속이 사설에 해당함.
- 판별 기준을 먼저 정해야 함 → Open Questions (4차) #1.

## Step 11. `Assets/GalaxyIO/Scripts/DOTS/Networking/NetcodeBootstrap.cs`

Open Questions (4차) #1에서 (a)를 고른 경우의 안.

```csharp
         // 루프백 버튼이 입력창에 채워 넣을 주소.
         public const string LoopbackAddress = "127.0.0.1";
+
+        // [PLACEHOLDER] 공용(운영) 서버 주소. 이 주소로 접속할 때만 이름이 계정 기준으로 고정됨.
+        public const string PublicServerAddress = "127.0.0.1";
+
+        /// <summary>
+        /// 공용 서버 주소인지 여부. 그 외 주소는 사설 서버로 취급함.
+        /// </summary>
+        public static bool IsPublicServer(string address) => address == PublicServerAddress;
```

## Step 12. `Assets/GalaxyIO/Scripts/Managed/UI/IpAddressField.cs`

주소가 바뀌면 편집 가능 여부도 바뀌므로 알림을 제공.

```csharp
  using System.Text;
  using TMPro;
  using UnityEngine;
+ using UnityEngine.Events;
```

```csharp
         private void Awake()
         {
             field = GetComponent<TMP_InputField>();
             field.characterLimit = MaxLength;
             field.onValueChanged.AddListener(OnValueChanged);
         }
+
+        /// <summary>
+        /// 주소가 바뀔 때 알림 받을 리스너 등록.
+        /// </summary>
+        public void AddListener(UnityAction<string> _action)
+        {
+            field.onValueChanged.AddListener(_action);
+        }
```

## Step 13. `Assets/GalaxyIO/Scripts/Managed/Popup/Window/WindowGameStart.cs`

```csharp
         [SerializeField] private IpAddressField addressField;
-        [SerializeField] private TMP_Text playerName_txt;
+        [SerializeField] private TMP_InputField playerName_if;
         [SerializeField] private TMP_Text warning;
         [SerializeField] private TMP_Text unreachable;
+        [SerializeField] private TMP_Text nameWarning;
         [SerializeField] private Button startButton;
         [SerializeField] private Button loopbackButton;

-        // 이번에 창을 열 때 확정한 이름.
-        private string playerName;
+        // 사설 서버에 비로그인으로 접속할 때 제안하는 이름. 창 열 때마다 새로 뽑음.
+        private string guestName;
```

```csharp
         protected override void Awake()
         {
             base.Awake();
             _connected = new UnityEvent();
             _changeWindow = new UnityEvent<string>();
+            playerName_if.characterLimit = PlayerNameComponent.MaxLength;
         }
```

```csharp
         protected override void Start()
         {
             base.Start();

             warning.gameObject.SetActive(false);
             unreachable.gameObject.SetActive(false);
+            nameWarning.gameObject.SetActive(false);
+            addressField.AddListener(_ => RefreshNameEditable());
```

```csharp
         public override void Open()
         {
             base.Open();
-            playerName = ResolvePlayerName();
-            playerName_txt.SetText(playerName);
+            guestName = $"Pilot#{Random.Range(0, 10000):D4}";
+            nameWarning.gameObject.SetActive(false);
+            RefreshNameEditable();
         }
```

```csharp
+        /// <summary>
+        /// 사설 서버에 비로그인으로 접속할 때만 이름을 직접 입력할 수 있음.
+        /// 그 외에는 계정 닉네임/게스트 이름으로 고정함.
+        /// </summary>
+        private void RefreshNameEditable()
+        {
+            bool editable = !NetcodeBootstrap.IsPublicServer(addressField.Address) && !LoginManager.IsOnline;
+
+            playerName_if.interactable = editable;
+            playerName_if.SetTextWithoutNotify(editable ? guestName : ResolvePlayerName());
+        }
```

```csharp
         private void OnPressGameStart()
         {
             if (!IsActive()) return;

             unreachable.gameObject.SetActive(false);
+
+            // 공백만 입력한 경우도 빈 이름으로 취급함.
+            string playerName = playerName_if.text.Trim();
+
+            if (playerName.Length == 0)
+            {
+                nameWarning.gameObject.SetActive(true);
+                return;
+            }
+
+            nameWarning.gameObject.SetActive(false);
```

```csharp
         private void SetConnecting(bool value)
         {
             connecting = value;
             startButton.interactable = !value;
             loopbackButton.interactable = !value;
+            playerName_if.interactable = !value && playerName_if.interactable;
         }
```

`OnPressLoopback`의 `addressField.Fill`은 `AddListener`를 통해 `RefreshNameEditable`을 부르므로 추가 변경 없음.

## Step 14. `Lobby UI.prefab` — `WIN_GameStart` (에디터 작업)

```diff
 WIN_GameStart
   Dynamic Group
     Button
     Loopback
-    PlayerNickName          (Image + Text)
     Warning
     Unreachable
+    NameWarning             (Warning 복제. Open Questions (4차) #2)
   Interaction Group
     IpField
+    PlayerNickName          (Image + TMP_InputField)
+      Text Area
+        Placeholder
+        Text
```

- `PlayerNickName`에 `TMP_InputField` 추가. `Text Area/Text`를 Text Component로, 새 `Placeholder`를 Placeholder로 연결.
- InputField가 되었으므로 `Interaction Group`으로 이동(2차 분류 기준).
- `WindowGameStart` 인스펙터: `playerName_if` ← `PlayerNickName`, `nameWarning` ← `NameWarning`.

## 검증 (Claude 직접 수행)

- 사설 주소 + 비로그인 → 입력 가능, 기본값 `Pilot#nnnn`. 수정한 이름으로 접속 → 인게임 이름표 일치.
- 사설 주소 + 로그인 → 입력 불가, 계정 닉네임 고정.
- 공용 주소 + 비로그인 → 입력 불가, `Pilot#nnnn` 고정.
- 공용 주소 + 로그인 → 입력 불가, 계정 닉네임 고정.
- 주소를 공용↔사설로 바꾸면 즉시 편집 가능 여부가 바뀜.
- 편집 가능 상태에서 이름을 비우고 시작 → 경고 표기, 접속 안 함.

## Open Questions (4차)

1. 공용 서버 판별 기준 (현재 코드에 없음):
   - (a) `NetcodeBootstrap.PublicServerAddress` 상수 추가 후 입력 주소와 비교. (추천. 변경 최소)
   - (b) `SO_NmhnConfigData`에 서버 주소 필드 추가. 모드별 주소 분리 가능.
   - (c) 로비 진입 경로로 구분(예: `Connect Btn`=공용, `Host Btn`=사설). 주소 입력창의 의미도 바뀜.
   - 어느 쪽이든 공용 서버 주소 값이 필요함. 계획은 `[PLACEHOLDER]`로 루프백을 넣어 둠.
2. 편집 가능 상태에서 이름이 비었을 때: (a) `NameWarning`을 되살려 경고(1차에서 삭제한 오브젝트 복구. 계획은 이쪽) / (b) 경고 없이 `Pilot#nnnn`으로 대체.
3. 편집한 이름의 유지 범위: 계획은 창을 열 때마다 초기화(`Pilot#nnnn` 새로 뽑음). 세션 동안 유지로 바꿀지?

### Answer
1. 
2. 
3. 

---

## 대화 기록

```
User: Try ".claude/plans/26-09/260922_fix_WIN_gameStart"
AI: Accepted. 계획 작성함. 프리팹 작업은 에디터 단계로 기술.
User: Open Questions 답했어. 답변해 (1. 생략 / 2. PlayerNickName / 3. 현재 유지 / 4. 창 열 때마다 새 번호)
AI: Accepted. 4건 모두 계획에 반영.
User: 구현 진행해
AI: Accepted. Step 1~5 구현 완료. 중첩 Canvas는 renderMode 대입이 무시돼 프리팹 YAML에서 m_RenderMode 0으로 직접 수정. 플레이 테스트(검증 항목)는 미실행.
User: 플랜에 답변 갱신해줘
AI: Accepted. `## 구현 결과` 추가, Step 5 설명을 실제 작업 방식으로 수정.
User: 구현 검증 결과 추가했어. 플랜 갱신해 (로그인 상태인데 Guest 표기됨)
AI: Accepted. 원인은 서버 계정 닉네임이 빈 값(회원가입 시 nickName 미전송). 가입 닉네임 입력(Step 6~8) 계획 추가, Open Questions #5, #6 추가.
User: 답변을 플랜에 갱신해
AI: 보류. 플랜에 #5, #6 답변이 없어 확인 요청.
User: 플랜 글 구조 개선. 5,6,7,8은 ## Open Question (2차)로 분류.
AI: Accepted. `# 수정 내용 (1차)` / `# 수정 내용 (2차)`로 분리. Step 7/8 본문에 있던 결정 사항을 #7, #8로 분리해 2차 질문 4건과 빈 Answer 칸 마련.
User: Open Questions (2차) 답했어. 플랜 갱신해 (1. 대응 안 함 / 2. 필수 / 3. 12자 유지 / 4. 추가만, 배치는 개발자 확인)
AI: Accepted. 4건 모두 반영. 수정 방향/Step 8/재검증 문구 갱신. 2차 질문 번호는 답변 기준 1~4로 맞춤. 다음 단계로 Step 6~8 구현 + 직접 재검증 제안.
User: 지금 답변도 플랜에 갱신해
AI: Accepted. `### 반영 결과` 추가.
User: 구현 진행해
AI: Accepted. Step 6~8 구현, Unity MCP + JW 서버로 재검증 전 항목 통과. `## 구현 결과 (2차)`, `## 재검증 결과` 추가.
User: WIN_SignUp 배치 확인. errorMsg_txt를 3개(계정 / 실명 / 닉네임)로 나눠 표기하도록 개선 필요.
AI: Accepted. `# 수정 내용 (3차)` 추가(Step 9~10). Open Questions (3차) 3건.
User: Open Questions (3차) 답했어. 플랜 갱신해 (1. 기존 유지 / 2. Error Msg_MemberName, 위치는 개발자 조정 / 3. OK)
AI: Accepted. 3건 반영. 필드명도 `memberNameErrorMsg_txt`로 맞춤.
User: memberName 짝 맞추기도 오케이. 구현 진행해
AI: Accepted. Step 9~10 구현, 직접 검증 전 항목 통과. `## 구현 결과 (3차)`, `## 검증 결과 (3차)` 추가.
User: scratchpad 백업 파일 삭제해
AI: Accepted. `Lobby UI.prefab.bak`/`.bak2`/`.bak3` 삭제. 플랜의 백업 경로 언급도 삭제됨으로 갱신.
User: 나머지도 삭제해
AI: Accepted. scratchpad 작업용 파일(`hier.py`, `restructure.py`, `plan3.md`) 삭제. scratchpad 비움.
User: WIN_GameStart PlayerNickName 개선 (InputField로 변경 / 공용·사설·로그인별 편집 가능 여부)
AI: Accepted. 사설+로그인 조합은 성립함(인증 서버와 게임 서버가 분리). `# 수정 내용 (4차)` 추가(Step 11~14). 공용/사설 판별 기준이 코드에 없어 Open Questions (4차) #1로 남김.
```
