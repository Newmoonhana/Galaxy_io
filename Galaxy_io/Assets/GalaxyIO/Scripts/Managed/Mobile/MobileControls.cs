using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.UI;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace GalaxyIO.Mobile
{
    /// <summary>
    /// 웹 전용 터치 조작(휴대폰, 태블릿 브라우저). 떠다니는 조이스틱 두 개.
    /// <para/>
    /// 화면 왼쪽 절반: 누른 자리에 이동 조이스틱(호버링). 끝까지 밀면 그 방향으로 가속.
    /// 화면 오른쪽 절반: 누른 자리에 조준 조이스틱. 미는 방향으로 기수를 돌리고, 미는 동안 사격.
    /// 조준하지 않으면 이동 방향으로 기수를 돌림. 손가락이 멀리 가면 조이스틱 중심이 따라옴.
    /// 버튼, 스크롤 같은 UI 위에서 시작한 터치는 UI 가 받음.
    /// <para/>
    /// 터치하면 켜지고, 마우스를 움직이면 꺼짐(터치 노트북). 결과는 정적 값으로 두고
    /// <c>PlayerInputEncodeSystem</c>이 마우스, 키보드 입력 대신 읽음.
    /// </summary>
    public sealed class MobileControls : MonoBehaviour
    {
        // 조이스틱 반지름(화면 짧은 변 대비)
        private const float StickRadiusRatio = 0.1F;

        // 이 비율 넘게 밀면 가속(이동), 사격(조준)
        private const float BoostThreshold = 0.92F;
        private const float FireThreshold = 0.25F;

        // 이보다 작게 밀면 무시함.
        private const float DeadZone = 0.12F;

        public static bool Active { get; private set; }

        /// <summary>
        /// 호버링 방향(-1 ~ 1)
        /// </summary>
        public static float2 Move { get; private set; }

        /// <summary>
        /// 바라볼 방향(단위 벡터). 조준 중이면 조준 방향, 아니면 이동 방향. 둘 다 없으면 0(기수 유지).
        /// </summary>
        public static float2 Aim { get; private set; }

        public static bool Fire { get; private set; }
        public static bool Boost { get; private set; }

        /// <summary>
        /// 입력 시스템이 이번 프레임에 값을 읽었는지(내 우주선이 있음). 조이스틱 그림을 이때만 보임.
        /// </summary>
        public static void MarkInGame()
        {
            lastInGameFrame = Time.frameCount;
        }

        private static int lastInGameFrame = -1000;

        private static bool zoomStep;

        /// <summary>
        /// 시야 버튼을 눌렀는지. 읽으면 지움. 카메라(FollowPlayerEntity)가 배율을 다음 단계로 바꿈.
        /// </summary>
        public static bool TakeZoomStep()
        {
            bool step = zoomStep;
            zoomStep = false;
            return step;
        }

        /// <summary>
        /// 웹 페이지의 시야 버튼이 부름(unityInstance.SendMessage("MobileControls", "CycleZoom")).
        /// </summary>
        public void CycleZoom()
        {
            zoomStep = true;
        }

        /// <summary>
        /// 탭으로 찍는 명령. 페이지의 핑, 명령 버튼을 누르면 켜지고, 다음 탭 자리를 그 명령에 씀.
        /// </summary>
        public enum TapMode
        {
            None,
            Ping,
            Order,
        }

        public static TapMode PendingMode { get; private set; }

        private static TapMode tappedMode;
        private static Vector2 tappedPoint;
        private static int tappedFrame = -1000;

        /// <summary>
        /// 페이지 버튼이 부름. "Ping" | "Order". 같은 것을 다시 누르면 끔.
        /// </summary>
        public void SetTapMode(string mode)
        {
            TapMode next = System.Enum.TryParse(mode, true, out TapMode parsed) ? parsed : TapMode.None;
            PendingMode = PendingMode == next ? TapMode.None : next;
        }

        /// <summary>
        /// 이 명령으로 찍은 탭이 있으면 화면 좌표를 돌려주고 지움. 핑(PingView), 함재기 명령(CarrierOrderView)이 읽음.
        /// </summary>
        public static bool TryTakeTap(TapMode mode, out Vector2 point)
        {
            point = tappedPoint;

            if (tappedMode != mode || Time.frameCount - tappedFrame > 10)
            {
                return false;
            }

            tappedMode = TapMode.None;
            return true;
        }

        // 페이지 버튼으로 누른 키. 몇 프레임 뒤에 뗌.
        private static int releaseKeysAt = -1;

        /// <summary>
        /// 페이지 버튼이 부름. 키보드 키(E: 인벤토리, Enter: 채팅, Escape: 메뉴, R: 레이더 조준 등)를 한 번 누른 것처럼 넣음.
        /// </summary>
        public void PressKey(string keyName)
        {
            if (Keyboard.current == null || !System.Enum.TryParse(keyName, true, out Key key) || key == Key.None)
            {
                return;
            }

            InputSystem.QueueStateEvent(Keyboard.current, new UnityEngine.InputSystem.LowLevel.KeyboardState(key));
            releaseKeysAt = Time.frameCount + 2;
        }

        private sealed class Stick
        {
            public int Finger = -1;
            public Vector2 Center;
            public Vector2 Point;
            public RectTransform Base;
            public RectTransform Knob;

            public Vector2 Value(float radius)
            {
                return Finger < 0 ? Vector2.zero : Vector2.ClampMagnitude((Point - Center) / radius, 1.0F);
            }
        }

        private readonly Stick moveStick = new Stick();
        private readonly Stick aimStick = new Stick();

        private Canvas canvas;
        private Sprite circle;
        private Sprite ring;

        private readonly List<RaycastResult> hits = new List<RaycastResult>();

#if UNITY_WEBGL && !UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            var go = new GameObject("MobileControls");
            DontDestroyOnLoad(go);
            go.AddComponent<MobileControls>();
            go.AddComponent<MobileCanvasFitter>();
        }
#endif

        private void Awake()
        {
            EnhancedTouchSupport.Enable();

            circle = MakeSprite(false);
            ring = MakeSprite(true);

            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;

            moveStick.Base = MakeImage("MoveBase", ring, new Color(1F, 1F, 1F, 0.3F));
            moveStick.Knob = MakeImage("MoveKnob", circle, new Color(0.24F, 0.91F, 1F, 0.5F));
            aimStick.Base = MakeImage("AimBase", ring, new Color(1F, 1F, 1F, 0.3F));
            aimStick.Knob = MakeImage("AimKnob", circle, new Color(1F, 0.42F, 0.36F, 0.5F));

            Active = Application.isMobilePlatform;
            canvas.enabled = false;

#if UNITY_WEBGL && !UNITY_EDITOR
            // 채팅 입력창을 누르면 휴대폰 키보드를 띄움.
            WebGLInput.mobileKeyboardSupport = true;
#endif
        }

        private void OnDestroy()
        {
            Active = false;
            Move = float2.zero;
            Aim = float2.zero;
            Fire = false;
            Boost = false;
        }

        private void Update()
        {
            if (releaseKeysAt >= 0 && Time.frameCount >= releaseKeysAt && Keyboard.current != null)
            {
                releaseKeysAt = -1;
                InputSystem.QueueStateEvent(Keyboard.current, new UnityEngine.InputSystem.LowLevel.KeyboardState());
            }

            // 터치하면 켜고, 손가락이 없는데 마우스가 움직이면 끔.
            if (Touch.activeTouches.Count > 0)
            {
                Active = true;
            }
            else if (Active && !Application.isMobilePlatform && Mouse.current != null
                     && Mouse.current.delta.ReadValue().sqrMagnitude > 9.0F)
            {
                Active = false;
            }

            float radius = Mathf.Min(Screen.width, Screen.height) * StickRadiusRatio;
            float margin = radius * 1.6F;

            // 손을 떼면 그림이 돌아갈 기본 자리
            Vector2 moveHome = new Vector2(margin, margin);
            Vector2 aimHome = new Vector2(Screen.width - margin, margin);

            bool moveSeen = false;
            bool aimSeen = false;

            if (Active)
            {
                foreach (Touch touch in Touch.activeTouches)
                {
                    int id = touch.touchId;
                    Vector2 point = touch.screenPosition;
                    bool ended = touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled;

                    // 핑, 명령 버튼을 누른 뒤의 탭은 조이스틱 대신 그 명령의 자리로 씀.
                    if (touch.phase == TouchPhase.Began && PendingMode != TapMode.None && !OverInteractiveUI(point))
                    {
                        tappedMode = PendingMode;
                        tappedPoint = point;
                        tappedFrame = Time.frameCount;
                        PendingMode = TapMode.None;
                        continue;
                    }

                    if (touch.phase == TouchPhase.Began && !OverInteractiveUI(point))
                    {
                        // 누른 쪽 절반의 조이스틱. 이미 쓰는 중이면 반대쪽.
                        Stick stick = point.x < Screen.width * 0.5F ? moveStick : aimStick;

                        if (stick.Finger >= 0)
                        {
                            stick = stick == moveStick ? aimStick : moveStick;
                        }

                        if (stick.Finger < 0)
                        {
                            stick.Finger = id;
                            stick.Center = point;
                            stick.Point = point;
                        }
                    }

                    foreach (Stick stick in new[] { moveStick, aimStick })
                    {
                        if (stick.Finger != id)
                        {
                            continue;
                        }

                        if (ended)
                        {
                            stick.Finger = -1;
                            continue;
                        }

                        stick.Point = point;

                        // 멀리 끌면 중심이 손가락을 따라옴.
                        Vector2 offset = point - stick.Center;
                        float limit = radius * 1.25F;

                        if (offset.magnitude > limit)
                        {
                            stick.Center = point - offset.normalized * limit;
                        }

                        if (stick == moveStick) moveSeen = true;
                        else aimSeen = true;
                    }
                }
            }

            // Ended 를 못 받고 사라진 손가락
            if (!moveSeen) moveStick.Finger = -1;
            if (!aimSeen) aimStick.Finger = -1;

            Vector2 move = moveStick.Value(radius);
            Vector2 aim = aimStick.Value(radius);

            if (move.magnitude < DeadZone) move = Vector2.zero;

            Move = new float2(move.x, move.y);
            Boost = Active && move.magnitude >= BoostThreshold;
            Fire = Active && aim.magnitude >= FireThreshold;

            if (aim.magnitude >= FireThreshold)
            {
                Aim = new float2(aim.normalized.x, aim.normalized.y);
            }
            else if (move.sqrMagnitude > 0.0F)
            {
                Aim = new float2(move.normalized.x, move.normalized.y);
            }
            else
            {
                Aim = float2.zero;
            }

            // 그림
            bool visible = Active && Time.frameCount - lastInGameFrame < 30;

            if (canvas.enabled != visible)
            {
                canvas.enabled = visible;
            }

            if (!visible)
            {
                return;
            }

            Vector2 moveCenter = moveStick.Finger >= 0 ? moveStick.Center : moveHome;
            Vector2 aimCenter = aimStick.Finger >= 0 ? aimStick.Center : aimHome;

            Place(moveStick.Base, moveCenter, radius);
            Place(moveStick.Knob, moveCenter + move * radius, radius * 0.45F);
            Place(aimStick.Base, aimCenter, radius);
            Place(aimStick.Knob, aimCenter + aim * radius, radius * 0.45F);

            moveStick.Knob.GetComponent<Image>().color = Boost
                ? new Color(1F, 0.85F, 0.3F, 0.7F)
                : new Color(0.24F, 0.91F, 1F, 0.5F);
        }

        /// <summary>
        /// 버튼, 토글, 입력창, 스크롤 같은 UI 위인지. 그런 곳에서 시작한 터치는 조작으로 쓰지 않음.
        /// </summary>
        private bool OverInteractiveUI(Vector2 point)
        {
            EventSystem events = EventSystem.current;

            if (events == null)
            {
                return false;
            }

            hits.Clear();
            events.RaycastAll(new PointerEventData(events) { position = point }, hits);

            foreach (RaycastResult hit in hits)
            {
                if (hit.gameObject != null
                    && (hit.gameObject.GetComponentInParent<Selectable>() != null
                        || hit.gameObject.GetComponentInParent<ScrollRect>() != null))
                {
                    return true;
                }
            }

            return false;
        }

        private static void Place(RectTransform rect, Vector2 center, float radius)
        {
            rect.position = center;
            rect.sizeDelta = new Vector2(radius * 2.0F, radius * 2.0F);
        }

        private RectTransform MakeImage(string name, Sprite sprite, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(transform, false);
            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(0.5F, 0.5F);
            return rect;
        }

        // 원(채움) 또는 고리 그림. 가장자리를 부드럽게.
        private static Sprite MakeSprite(bool hollow)
        {
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            float r = size * 0.5F;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5F, y + 0.5F), new Vector2(r, r));
                    float outer = Mathf.Clamp01(r - d);
                    float alpha = hollow ? outer * Mathf.Clamp01(d - (r - 6.0F)) + 0.15F * outer : outer;
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(alpha) * 255));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5F, 0.5F));
        }
    }

    /// <summary>
    /// 웹 전용. 화면 비율에 맞춰 UI 배율 기준을 바꿈.
    /// 기준 해상도(1920x1080)보다 가로로 긴 화면(휴대폰 가로)은 높이에, 그보다 좁은 화면(태블릿, 세로)은 너비에 맞춤.
    /// 터치 조작 중에는 UI 를 조금 크게 보임.
    /// </summary>
    public sealed class MobileCanvasFitter : MonoBehaviour
    {
        private readonly Dictionary<CanvasScaler, Vector2> original = new Dictionary<CanvasScaler, Vector2>();
        private float nextScan;

        private struct Placement
        {
            public Vector2 AnchorMin;
            public Vector2 AnchorMax;
            public Vector2 Position;
            public bool Moved;
        }

        private readonly Dictionary<RectTransform, Placement> placements = new Dictionary<RectTransform, Placement>();

        private readonly HashSet<Canvas> hiddenCanvases = new HashSet<Canvas>();

        // 옮기는 거리는 1920x1080 기준 값. 캔버스 기준 해상도(예: 960x540)에 맞춰 비례시킴.
        private float CanvasUnit(RectTransform rect)
        {
            CanvasScaler scaler = rect.GetComponentInParent<CanvasScaler>();

            if (scaler == null || !original.TryGetValue(scaler, out Vector2 reference))
            {
                return 1.0F;
            }

            return reference.y / 1080.0F;
        }

        // 터치 중이면 move 로 옮기고, 아니면 처음 자리로 되돌림. 한 번만 옮김.
        private void Relocate(RectTransform rect, bool touch, System.Action<RectTransform> move)
        {
            if (rect == null)
            {
                return;
            }

            if (!placements.TryGetValue(rect, out Placement placement))
            {
                placement = new Placement { AnchorMin = rect.anchorMin, AnchorMax = rect.anchorMax, Position = rect.anchoredPosition };
            }

            if (touch && !placement.Moved)
            {
                move(rect);
                placement.Moved = true;
            }
            else if (!touch && placement.Moved)
            {
                rect.anchorMin = placement.AnchorMin;
                rect.anchorMax = placement.AnchorMax;
                rect.anchoredPosition = placement.Position;
                placement.Moved = false;
            }

            placements[rect] = placement;
        }

        private void Update()
        {
            if (Time.unscaledTime < nextScan)
            {
                return;
            }

            nextScan = Time.unscaledTime + 0.5F;

            float aspect = Screen.height > 0 ? (float)Screen.width / Screen.height : 16.0F / 9.0F;
            float match = aspect > 16.0F / 9.0F ? 1.0F : 0.0F;
            float enlarge = MobileControls.Active ? 0.85F : 1.0F;

            // 세로 화면(휴대폰 세로): 너비에 맞추되 기준 너비를 1100 으로 줄여 글자가 너무 작아지지 않게 함(상점 창 1000 은 들어감).
            bool portrait = aspect < 1.0F;

            foreach (CanvasScaler scaler in FindObjectsByType<CanvasScaler>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (scaler == null || scaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize)
                {
                    continue;
                }

                if (!original.TryGetValue(scaler, out Vector2 reference))
                {
                    reference = scaler.referenceResolution;
                    original[scaler] = reference;
                }

                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = match;
                // 세로: 기준 너비를 1920 기준 1100 비율(0.573)로 줄임. 캔버스마다 기준 해상도가 달라도 같은 비율.
                scaler.referenceResolution = portrait ? reference * (1100.0F / 1920.0F) : reference * enlarge;
            }

            // 터치 조작 중에는 조이스틱과 겹치는 HUD 를 위로 옮김: 레이더(오른쪽 아래 -> 오른쪽 위), 킬 로그(레이더 아래로),
            // 채팅(왼쪽 아래 -> 왼쪽 위, 점수판 아래). 참고 웹 게임과 같은 배치. 터치를 끄면 원래 자리.
            bool touch = MobileControls.Active;

            foreach (Scripts.Managed.UI.RadarView radar in FindObjectsByType<Scripts.Managed.UI.RadarView>(FindObjectsSortMode.None))
            {
                Relocate((RectTransform)radar.transform, touch, rect =>
                {
                    rect.anchorMin = rect.anchorMax = new Vector2(1.0F, 1.0F);
                    rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, -Mathf.Abs(rect.anchoredPosition.y) - 10.0F * CanvasUnit(rect));
                });
            }

            foreach (Scripts.Managed.UI.KillLogView killLog in FindObjectsByType<Scripts.Managed.UI.KillLogView>(FindObjectsSortMode.None))
            {
                foreach (RectTransform child in killLog.GetComponentsInChildren<RectTransform>(true))
                {
                    if (child.name == "KillLogRows")
                    {
                        Relocate(child, touch, rect => rect.anchoredPosition += new Vector2(0.0F, -170.0F * CanvasUnit(rect)));
                    }
                }
            }

            // 조작법 창(키보드, 마우스 안내)은 터치 조작 중에는 숨김.
            foreach (GalaxyIO.Tools.PopupUI.Popup.ControlUI help in FindObjectsByType<GalaxyIO.Tools.PopupUI.Popup.ControlUI>(FindObjectsSortMode.None))
            {
                foreach (Canvas helpCanvas in help.GetComponentsInChildren<Canvas>(true))
                {
                    if (touch && helpCanvas.enabled)
                    {
                        helpCanvas.enabled = false;
                        hiddenCanvases.Add(helpCanvas);
                    }
                    else if (!touch && hiddenCanvases.Remove(helpCanvas))
                    {
                        helpCanvas.enabled = true;
                    }
                }
            }

            foreach (Scripts.Managed.UI.ChatView chat in FindObjectsByType<Scripts.Managed.UI.ChatView>(FindObjectsSortMode.None))
            {
                Relocate((RectTransform)chat.transform, touch, rect =>
                {
                    rect.anchorMin = rect.anchorMax = new Vector2(0.0F, 1.0F);
                    rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, -380.0F * CanvasUnit(rect));
                });
            }

            if (original.Count > 64)
            {
                var dead = new List<CanvasScaler>();

                foreach (CanvasScaler key in original.Keys)
                {
                    if (key == null) dead.Add(key);
                }

                foreach (CanvasScaler key in dead) original.Remove(key);
            }
        }
    }
}
