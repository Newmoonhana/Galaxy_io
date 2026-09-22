using System;
using System.IO;
using JWServer.Runtime.DTO;
using JWServer.Runtime.Utils;
using Newmoonhana.Runtime.Utils;
using Newmoonhana.Runtime.Utils.Config;
using Newmoonhana.Runtime.Utils.DEBUG;
using UnityEngine;
using UnityEngine.Networking;

namespace JWServer.Runtime.Login
{
    public static class LoginManager
    {
        /// <summary>
        /// [PLACEHOLDER] 통합 게임 백엔드서버에서 사용되는 게임의 식별자
        /// </summary>
        public const string GAME_CODE = "91D3FE9281715AE3C9F96BC327789961";

#if UNITY_EDITOR
        private const string AUTH_SERVER = "http://localhost:5050";
#else
        /// <summary>
        /// [PLACEHOLDER] 개발 서버 주소 / 운영 서버 주소
        /// </summary>
        /// private const string AUTH_SERVER => SO_NmhnConfigData.Instance.ModeType != MODETYPE.BUILD ? "https://192.168.0.1" : "https://jwgameserver.com";
        /// TODO: 빌드 에러 나서 임시로 주석처리함.
        private const string AUTH_SERVER = "http://localhost:5050";
#endif

        /// <summary>
        /// [PLACEHOLDER] 인증 정보를 저장할 파일명
        /// </summary>
        private const string AUTH_FILE = "thunder_veil_auth.json";

        private const string CONTENT_TYPE_JSON = "application/json"; // HTTP통신 데이터 타입(JSON)

        /// <summary>
        /// 엑세스 토큰 & 리프레시토큰 — 메모리에서만 존재
        /// </summary>
        private static GameUserTokenDTO sUserTokenData;

        /// <summary>
        /// `sAccessToken`이 유효한지 보장함. ID/PW를 입력하여 로그인 하였거나<p/>
        /// 자동 로그인 기능으로 로그인에 성공하였을 때 true.<p/>
        ///<p/>
        /// Warning: 이 값이 true일때 토큰이 서버에서도 유효한지 보장하지 않음. 엑세스 토큰은<p/>
        ///         서버에서 1시간 단위로 초기화 되기 때문에 모든 트랜잭션에 "Access 토큰으로<p/>
        ///         통신 시도 -> Refresh 토큰으로 통신 재시도"하는 프로세스가 존재해야 함.
        /// </summary>
        private static bool sIsOnline;

        /// <summary>
        /// 로그인 여부. 로비 UI 표기용.
        /// </summary>
        public static bool IsOnline => sIsOnline;

        /// <summary>
        /// 로그인/회원가입으로 받은 유저 id. 자동 로그인 시 서버가 주지 않아 빈 값.
        /// </summary>
        private static string sUserId;
        public static string UserId => sUserId;

        /// <summary>
        /// 게임 계정 닉네임. 로그인 성공 시 accountinfo로 받아옴. 비로그인/조회 실패 시 빈 값.
        /// </summary>
        private static string sNickName;
        public static string NickName => sNickName;

        /// <summary>
        /// 인증이 필요한 API 호출을 감싸서, Access 토큰 만료 시 자동으로
        /// Refresh 토큰으로 재발급받고 원래 호출을 재시도
        /// </summary>
        /// <param name="apiCall">Awaitable&lt;뭐시기DTO&gt;로 받아오면 매개변수 받자마자 실행되서 &amp; 2번째 await로 재사용이 불가능해서 함수 델리게이트로 받음)</param>
        private static async Awaitable<GameUserResponseDTO> WithTokenRefresh(
            Func<Awaitable<GameUserResponseDTO>> apiCall)
        {
            GameUserResponseDTO response = await apiCall();

            if (ResponseMsgUtil.IsOk(response.responseMessage))
                return response;

            // 토큰 만료가 아닌 다른 실패 — 그대로 반환
            if (!ResponseMsgUtil.IsAccessTokenExpired(response.responseMessage))
                return response;

            // Access 토큰 만료 — Refresh 토큰으로 재발급 시도
            GameUserResponseDTO refreshResponse = await RefreshAccessToken();

            // Refresh도 실패 시 — 이대로 반환 (재로그인 필요)
            if (!ResponseMsgUtil.IsOk(refreshResponse.responseMessage))
                return refreshResponse;

            sUserTokenData = new GameUserTokenDTO(refreshResponse);

            // 재발급된 토큰으로 재시도
            return await apiCall();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticData()
        {
            sUserTokenData = default;
            sIsOnline = false;
            sUserId = string.Empty;
            sNickName = string.Empty;
        }

        /// <summary>
        /// 디스크에 영구히 보존되는 데이터 파일 위치<p/>
        /// Application.persistentDataPath: OS에서 보장하는 영구 데이터들의 위치를 담고 있음
        /// </summary>
        private static string sPersistentDataPath => Path.Combine(Application.persistentDataPath, AUTH_FILE);

        /// <summary>
        /// 인증 정보 json으로 저장
        /// </summary>
        private static void SaveAuth()
        {
            string json = JsonUtility.ToJson(
                new GameUserTokenDTO(_accessToken: sUserTokenData.accessToken, _refreshToken: sUserTokenData.refreshToken),
                true
            );
            File.WriteAllText(sPersistentDataPath, json);
            DebugLog.Log($"{sPersistentDataPath} 경로로 토큰 저장!\njson = {json}");
        }
        /// <summary>
        /// 인증 정보 json 불러오기
        /// </summary>
        private static void LoadAuth()
        {
            if (!File.Exists(sPersistentDataPath))
            {
                return;
            }

            string json = File.ReadAllText(sPersistentDataPath);
            sUserTokenData = JsonUtility.FromJson<GameUserTokenDTO>(json);
            DebugLog.Log($"{sPersistentDataPath} 경로의 저장된 토큰 참조\njson = {json}");
        }

        /// <summary>
        /// JW 게임 서버에서 받은 응답을 DTO로 변환(역직렬화)<p/>
        /// </summary>
        /// <param name="req"></param>
        /// <returns></returns>
        private static GameUserResponseDTO DeserializeJsonResponse(UnityWebRequest req)
        {
            //오류 메세지 출력
            if (req.result == UnityWebRequest.Result.ConnectionError)
                return new GameUserResponseDTO(_responseMessage: CommonMsg.CONNECTION_ERROR);

            string responseJson = req.downloadHandler.text;
            return JsonUtility.FromJson<GameUserResponseDTO>(responseJson);
        }

        /// <summary>
        /// 사용자 회원가입<p/>
        /// 필수 파라미터: actionType, gameCode, memberType, memberId, memberPw (6자리이상), memberName<p/>
        /// 조건부 필수 파라미터: snsProviderId, snsProvider (memberType == 1인 경우)
        /// </summary>
        public static async Awaitable<GameUserResponseDTO> SignUp(GameUserRequestDTO request)
        {
            const string gameUserRequestEndpoint = "/api/account/gameuser";
            string postData = JsonUtility.ToJson(request);

            // 웹 요청 전송 — using-IDisposable 실패시 자동으로 Close
            using (UnityWebRequest req = UnityWebRequest.Post(AUTH_SERVER + gameUserRequestEndpoint, postData, CONTENT_TYPE_JSON))
            {
                await req.SendWebRequest(); // 응답 대기 — 비동기 처리
                GameUserResponseDTO response = DeserializeJsonResponse(req);
                // 성공 — Access/Refresh 토큰 저장. 앞으로의 서버 통신은 Access토큰을 통해 이루어짐.
                if (ResponseMsgUtil.IsOk(response.responseMessage))
                {
                    sUserTokenData = new GameUserTokenDTO(response);
                    sIsOnline = true;
                    sUserId = response.userId;
                    SaveAuth();
                    await FetchAccountInfo();
                }
                return response;
            }
        }

        /// <summary>
        /// 사용자 로그인
        /// <p/>
        /// 필수 파라미터: memberId, memberPw
        /// </summary>
        public static async Awaitable<GameUserResponseDTO> LogIn(GameUserRequestDTO request)
        {
            // 이미 로그인한 경우
            if (sIsOnline)
                return new GameUserResponseDTO(_responseMessage: CommonMsg.ALREADY_ONLINE);

            const string gameUserRequestEndpoint = "/api/account/gameuser";

            string postData = JsonUtility.ToJson(request);

            using (UnityWebRequest req = UnityWebRequest.Post(AUTH_SERVER + gameUserRequestEndpoint, postData, CONTENT_TYPE_JSON))
            {
                await req.SendWebRequest();
                GameUserResponseDTO response = DeserializeJsonResponse(req);

                if (ResponseMsgUtil.IsOk(response.responseMessage))
                {
                    sUserTokenData = new GameUserTokenDTO(response);
                    sIsOnline = true;
                    sUserId = response.userId;
                    SaveAuth();
                    await FetchAccountInfo();
                }

                return response;
            }
        }

        /// <summary>
        /// 저장되어 있는 Access토큰과 Refresh토큰으로 로그인 시도
        /// <p/>
        /// 로그인 성공 시 별도로 ID/PW입력받을 필요 없음
        /// </summary>
        public static async Awaitable<GameUserResponseDTO> AutoLogIn()
        {
            if (sIsOnline)
            {
                DebugLog.Log($"자동 로그인 정보:\n{sUserTokenData}");
                return new GameUserResponseDTO(_responseMessage: CommonMsg.ALREADY_ONLINE);
            }

            LoadAuth();

            // 저장된 토큰 없으면 로그인 실패
            if (!sUserTokenData.IsVaild())
                return new GameUserResponseDTO(_responseMessage: CommonMsg.NO_AUTH_INFO_IN_LOCAL);

            GameUserResponseDTO dto = await WithTokenRefresh(GameStart);

            // 성공 — Access토큰과 Refresh토큰 모두 유효함
            if (ResponseMsgUtil.IsOk(dto.responseMessage))
            {
                sIsOnline = true;
                DebugLog.Log($"자동 로그인 정보:\n{sUserTokenData}");
                await FetchAccountInfo();
            }

            return dto;
        }

        /// <summary>
        /// 로그아웃. 서버 로그아웃 API 없음. 로컬 토큰 삭제가 곧 로그아웃.
        /// </summary>
        public static void LogOut()
        {
            sUserTokenData = default;
            sIsOnline = false;
            sUserId = string.Empty;
            sNickName = string.Empty;
            if (File.Exists(sPersistentDataPath))
                File.Delete(sPersistentDataPath);
        }

        /// <summary>
        /// 회원 ID 중복 확인. 게임별 계정이 아닌 공통 회원 DB 기준.
        /// </summary>
        public static async Awaitable<MemberIdDupCheckResponseDTO> CheckMemberIdDup(string memberId)
        {
            const string memberIdDupCheckEndpoint = "/api/account/memberiddupcheck";
            string postData = JsonUtility.ToJson(new MemberIdDupCheckRequestDTO(GAME_CODE, memberId));

            using (UnityWebRequest req = UnityWebRequest.Post(AUTH_SERVER + memberIdDupCheckEndpoint, postData, CONTENT_TYPE_JSON))
            {
                await req.SendWebRequest();
                if (req.result == UnityWebRequest.Result.ConnectionError)
                    return new MemberIdDupCheckResponseDTO { responseMessage = CommonMsg.CONNECTION_ERROR };

                return JsonUtility.FromJson<MemberIdDupCheckResponseDTO>(req.downloadHandler.text);
            }
        }

        // Hidden API — `GameStart` 호출은 `AutoLogIn`에서 자동으로 처리해줌
        private static async Awaitable<GameUserResponseDTO> GameStart()
        {
            // 이미 AutoLogin에서 sIsOnline 조건식을 거치기에 주석 처리함. 혹시 사용하는 게 맞는거면 주석 해제 필요.
            /*// 이미 로그인한 경우
            if (sIsOnline)
                return new GameUserResponseDTO { responseMessage = CommonMsg.ALREADY_ONLINE };*/

            const string gameUserRequestEndpoint = "/api/game/gamestart";

            using (UnityWebRequest req = UnityWebRequest.Post(AUTH_SERVER + gameUserRequestEndpoint, "", CONTENT_TYPE_JSON))
            {
                req.SetRequestHeader("Authorization", "Bearer " + sUserTokenData.accessToken);
                await req.SendWebRequest();
                GameUserResponseDTO response = DeserializeJsonResponse(req);
                return response;
            }
        }

        /// <summary>
        /// 게임 계정 정보를 조회해 닉네임 저장. 실패 시 빈 값.
        /// </summary>
        private static async Awaitable FetchAccountInfo()
        {
            const string accountInfoEndpoint = "/api/game/accountinfo";

            using (UnityWebRequest req = UnityWebRequest.Post(AUTH_SERVER + accountInfoEndpoint, "", CONTENT_TYPE_JSON))
            {
                req.SetRequestHeader("Authorization", "Bearer " + sUserTokenData.accessToken);
                await req.SendWebRequest();

                // 오류 응답(4xx/5xx)엔 닉네임이 없음.
                if (req.result != UnityWebRequest.Result.Success)
                {
                    sNickName = string.Empty;
                    return;
                }

                AccountInfoResponseDTO response = JsonUtility.FromJson<AccountInfoResponseDTO>(req.downloadHandler.text);
                sNickName = ResponseMsgUtil.IsOk(response.responseMessage) ? response.accountNickName : string.Empty;
            }
        }

        /// <summary>
        /// Access토큰 만료 후 다시 받기 (세션에 저장되어 있는 Refresh토큰으로 Access토큰 재발급)
        /// </summary>
        public static async Awaitable<GameUserResponseDTO> RefreshAccessToken()
        {
            const string accessTokenRefreshEndpoint = "/api/account/gameusertokenrefresh";

            if (string.IsNullOrEmpty(sUserTokenData.refreshToken))
            {
                // Refresh토큰이 없는 경우, `LogIn`으로 ID, PW 입력하여 로그인하여야 함
                return new GameUserResponseDTO(_responseMessage: "NO_REFRESH_TOKEN");
            }

            string postData = JsonUtility.ToJson(new GameUserTokenDTO(_accessToken: string.Empty, _refreshToken:  sUserTokenData.refreshToken));

            using (UnityWebRequest req = UnityWebRequest.Post(AUTH_SERVER + accessTokenRefreshEndpoint, postData, CONTENT_TYPE_JSON))
            {
                await req.SendWebRequest();
                GameUserResponseDTO response = DeserializeJsonResponse(req);
                return response;
            }
        }

    }
}