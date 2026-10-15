using GalaxyIO.DOTS.Client.Components.Singleton;
using GalaxyIO.DOTS.Components.Message;
using GalaxyIO.Scripts.Managed.Chat;
using GalaxyIO.Tools;
using Newmoonhana.Runtime.Core;
using System.Collections.Generic;
using TMPro;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

namespace GalaxyIO.Scripts.Managed.UI
{
    /// <summary>
    /// 탭별 대화 기록을 보여주고, 입력한 채팅과 명령어를 서버로 보내는 클라이언트 전용 뷰.
    /// 닫혀 있는 동안엔 본문 글자와 안 읽은 귓속말 탭만 보이고, 새 글이 없으면 본문 글자도 서서히 사라짐.
    /// </summary>
    public sealed class ChatView : MonoBehaviour
    {
        // 파티 초대 안내문의 [수락] 링크 ID. 뒤에 초대한 플레이어의 NetworkId 가 붙음.
        private const string AcceptLink = "accept:";

        [Header("Layout")]

        [SerializeField]
        private ChatTab tabPrefab;

        [SerializeField]
        [Tooltip("탭이 쌓이는 부모. 왼쪽에서 오른쪽으로 정렬됨. 닫혀 있는 동안엔 입력 줄 자리로 내려감.")]
        private RectTransform tabContainer;

        [SerializeField]
        [Tooltip("열려 있을 때만 보이는 본문 배경")]
        private Graphic bodyBackground;

        [SerializeField]
        [Tooltip("열려 있을 때만 보이는 본문 테두리")]
        private GameObject bodyOutline;

        [SerializeField]
        [Tooltip("열려 있을 때만 보이는 입력 줄")]
        private GameObject inputRow;

        [SerializeField]
        [Tooltip("보고 있는 탭의 대화 기록. 아래쪽 정렬이라 새 줄이 아래에 붙음.")]
        private TMP_Text body;

        [SerializeField]
        [Tooltip("입력창 앞에 붙는 채널 이름. 리치 텍스트를 꺼 둬야 함.")]
        private TMP_Text channelLabel;

        [SerializeField]
        private TMP_InputField input;

        [SerializeField]
        [Tooltip("커서를 올린 닉네임을 덮는 강조 상자. 본문 글자의 자식이어야 함.")]
        private RectTransform nameHighlight;

        [SerializeField]
        [Tooltip("닉네임을 누르면 커서 위치에 뜨는 메뉴")]
        private RectTransform nameMenu;

        [SerializeField]
        [Tooltip("메뉴의 귓속말 항목. 고른 플레이어의 귓속말 탭을 엶.")]
        private Button whisperOption;

        [SerializeField]
        [Tooltip("메뉴의 파티 초대 항목. 고른 플레이어를 파티에 초대함.")]
        private Button partyInviteOption;

        [SerializeField]
        [Tooltip("채팅을 보낸 우주선 위에 띄우는 말풍선")]
        private ChatBubbles bubbles;

        [SerializeField]
        [Tooltip("입력창 열기 (Enter)")]
        private InputActionReference chatAction;

        [SerializeField]
        [Tooltip("탭마다 남기는 최대 줄 수. 넘치면 오래된 줄부터 지움.")]
        [Min(1)]
        private int maxLines = 100;

        [SerializeField]
        [Tooltip("기억하는 최대 입력 수. 넘치면 오래된 입력부터 지움.")]
        [Min(1)]
        private int maxHistory = 100;

        [SerializeField]
        [Tooltip("닫혀 있는 동안 본문에 새 글이 없으면 사라지기 시작할 때까지의 시간(초)")]
        [Min(0.0F)]
        private float fadeDelay = 5.0F;

        [SerializeField]
        [Tooltip("본문이 사라지는 데 걸리는 시간(초)")]
        [Min(0.01F)]
        private float fadeDuration = 3.0F;

        [SerializeField]
        [Tooltip("보고 있지 않은 탭에 귓속말이 오면 재생")]
        private string whisperSound = "Whisper";

        [Header("Colors")]

        [SerializeField]
        private Color publicColor = Color.cyan;

        [SerializeField]
        private Color whisperColor = new Color(1.0F, 0.6F, 0.2F);

        [SerializeField]
        private Color systemColor = Color.red;

        [SerializeField]
        private Color partyColor = Color.green;

        // 탭 하나의 대화 기록
        private sealed class Channel
        {
            // 귓속말 상대 이름. 공개, 파티 채널은 번역된 채널 이름.
            public string Name;
            public ChatTab Tab;

            // 안 읽은 귓속말이 있는지
            public bool Unread;
            public readonly List<string> Lines = new List<string>();
        }

        private EntityManager entityManager;
        private EntityQuery messageQuery;
        private EntityQuery partyStateQuery;
        private EntityQuery stateQuery;
        private EntityQuery inGameQuery;
        private EntityQuery connectionQuery;

        private string publicHex;
        private string whisperHex;
        private string systemHex;
        private string partyHex;

        private readonly ChatCommands commands = new ChatCommands();

        // 상대 이름별 귓속말 채널
        private readonly Dictionary<string, Channel> whispers = new Dictionary<string, Channel>();

        // 모든 채널. 탭이 놓인 순서(왼쪽부터).
        private readonly List<Channel> channels = new List<Channel>();

        // 전송한 입력. 오래된 순.
        private readonly List<string> history = new List<string>();

        // 방향키로 보고 있는 history 위치. history.Count 면 보고 있지 않음.
        private int historyIndex;

        // 본문이 마지막으로 바뀐 시각. 열려 있는 동안엔 계속 갱신됨.
        private float bodyUpdateTime;

        // 메뉴를 띄운 닉네임
        private string menuName;

        private Channel publicChannel;

        // 파티에 있는 동안만 생김.
        private Channel partyChannel;

        // 보고 있는 채널
        private Channel current;

        // 채팅창이 열려 있는지. 열려 있는 동안엔 우주선 조작이 막힘.
        private bool opened;

        // 이번 프레임에 Enter 로 전송했는지
        private bool submitted;

        // 직전 프레임에 입력창에 포커스가 있었는지
        private bool inputFocused;

        private Canvas canvas;

        // 캐시한 쿼리가 속한 클라이언트 월드. 세션 종료 시 Dispose 되므로 매 프레임 확인함.
        private World clientWorld;

        private void Start()
        {
            clientWorld = ClientServerBootstrap.ClientWorld;

            if (clientWorld == null)
            {
                enabled = false;
                return;
            }

            entityManager = clientWorld.EntityManager;
            messageQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<ChatRPC>(),
                ComponentType.ReadOnly<ReceiveRpcCommandRequest>()
            );
            partyStateQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<PartyStateRPC>(),
                ComponentType.ReadOnly<ReceiveRpcCommandRequest>()
            );
            stateQuery = entityManager.CreateEntityQuery(ComponentType.ReadWrite<PlayerStateComponent>());
            inGameQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<NetworkStreamInGame>());
            connectionQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<NetworkId>());

            publicHex  = ColorUtility.ToHtmlStringRGB(publicColor);
            whisperHex = ColorUtility.ToHtmlStringRGB(whisperColor);
            systemHex  = ColorUtility.ToHtmlStringRGB(systemColor);
            partyHex   = ColorUtility.ToHtmlStringRGB(partyColor);

            commands.Register("w", Whisper);
            commands.Register("p", Party);

            canvas = GetComponentInParent<Canvas>();

            input.characterLimit = ChatRPC.MaxLength;
            input.onSubmit.AddListener(Submit);
            whisperOption.onClick.AddListener(() => Select(GetWhisper(menuName)));
            partyInviteOption.onClick.AddListener(() =>
            {
                SendParty(PartyCommand.Invite, menuName);
                nameMenu.gameObject.SetActive(false);
            });

            publicChannel = AddChannel(string.Empty, false);
            Select(publicChannel);
            SetOpened(false);

            OnLocaleChanged(LocalizationSettings.SelectedLocale);
            LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
        }

        private void OnDestroy()
        {
            LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
        }

        private void OnEnable()
        {
            chatAction?.action.Enable();
        }

        private void OnDisable()
        {
            chatAction?.action.Disable();
        }

        private void LateUpdate()
        {
            // 세션을 나가면 클라이언트 월드가 Dispose 됨. 캐시한 쿼리를 더 쓰면 안 됨.
            if (!ClientWorldScope.IsAlive(clientWorld))
            {
                enabled = false;
                return;
            }

            // 파티에 들어가면 파티 탭을 만들고, 나오면 지움. 같은 프레임에 여러 개면 마지막 상태만 봄.
            if (!partyStateQuery.IsEmpty)
            {
                using var states = partyStateQuery.ToComponentDataArray<PartyStateRPC>(Allocator.Temp);
                bool inParty = states[states.Length - 1].InParty;

                entityManager.DestroyEntity(partyStateQuery);

                if (inParty && partyChannel == null)
                {
                    partyChannel = AddChannel(L10n.Get("Chat/Party"), false);
                }
                else if (!inParty && partyChannel != null)
                {
                    RemoveChannel(partyChannel);
                    partyChannel = null;
                }
            }

            if (!messageQuery.IsEmpty && !connectionQuery.IsEmpty)
            {
                using var messages = messageQuery.ToComponentDataArray<ChatRPC>(Allocator.Temp);
                int localNetworkId = connectionQuery.GetSingleton<NetworkId>().Value;

                entityManager.DestroyEntity(messageQuery);

                foreach (ChatRPC message in messages)
                {
                    // 자신의 닉네임은 누를 수 없음.
                    bool mine = message.SenderNetworkId == localNetworkId;

                    switch (message.Channel)
                    {
                        case ChatChannel.Public:
                            Push(publicChannel, Format(publicHex, message, mine));
                            bubbles.Show(message.SenderNetworkId, message.Text.ToString(), Color.white);
                            break;

                        // 공개 탭에도 같은 줄을 쌓음.
                        case ChatChannel.Party:
                            string partyLine = Format(partyHex, message, mine);

                            if (partyChannel != null)
                            {
                                Push(partyChannel, partyLine);
                            }

                            Push(publicChannel, partyLine);
                            bubbles.Show(message.SenderNetworkId, message.Text.ToString(), partyColor);
                            break;

                        case ChatChannel.Whisper:
                            // 내가 보낸 귓속말의 사본은 받는 쪽 이름의 탭에 쌓음.
                            Channel channel = GetWhisper((message.Echo ? message.Target : message.Sender).ToString());

                            string line = Format(whisperHex, message, mine);

                            // 공개 탭에도 같은 줄을 쌓음.
                            Push(channel, line);
                            Push(publicChannel, line);
                            bubbles.Show(message.SenderNetworkId, message.Text.ToString(), whisperColor);

                            if (message.Echo)
                            {
                                Select(channel);
                            }
                            else if (channel != current)
                            {
                                channel.Unread = true;
                                Refresh(channel);

                                if (SoundManager.Instance != null)
                                {
                                    SoundManager.Instance.PlaySfx(whisperSound);
                                }
                            }

                            break;

                        // 본문은 번역 키, Target 은 번역문의 {0}.
                        case ChatChannel.System:
                            string key = message.Text.ToString();
                            string text = string.Format(L10n.Get(key), message.Target);

                            // 초대 안내문 끝에 누르면 파티에 들어가는 [수락] 링크를 붙임.
                            if (key == "Party/Invited")
                            {
                                Push(current, $"<color=#{systemHex}>{Escape(text)}</color> "
                                    + $"<link=\"{AcceptLink}{message.SenderNetworkId}\"><color=#{partyHex}>{L10n.Get("Party/Accept")}</color></link>");
                            }
                            else
                            {
                                PushSystem(text);
                            }

                            break;
                    }
                }
            }

            RefRW<PlayerStateComponent> state = stateQuery.GetSingletonRW<PlayerStateComponent>();

            Keyboard keyboard = Keyboard.current;

            if (opened)
            {
                Mouse mouse = Mouse.current;
                bool pressedOutside = mouse != null
                    && mouse.leftButton.wasPressedThisFrame
                    && !Contains((RectTransform)transform, mouse.position.ReadValue())
                    && !(nameMenu.gameObject.activeSelf && Contains(nameMenu, mouse.position.ReadValue()));

                // 전송(Enter), 취소(ESC), 채팅창 밖 클릭으로 닫음.
                if (submitted || pressedOutside || (inputFocused && !input.isFocused && input.wasCanceled))
                {
                    SetOpened(false);
                }
                // 탭이나 본문을 누르면 입력창 포커스가 풀리므로, 마우스를 떼면 다시 잡음.
                else if (!input.isFocused && (mouse == null || !mouse.leftButton.isPressed))
                {
                    input.ActivateInputField();
                }
                else if (keyboard != null)
                {
                    // 위/아래 방향키로 전송했던 입력을 최근 것부터 불러옴. 맨 아래에서 더 내려가면 비움.
                    int index = historyIndex;

                    if (keyboard.upArrowKey.wasPressedThisFrame)
                    {
                        index--;
                    }
                    else if (keyboard.downArrowKey.wasPressedThisFrame)
                    {
                        index++;
                    }

                    index = Mathf.Clamp(index, 0, history.Count);

                    if (index != historyIndex)
                    {
                        historyIndex = index;
                        input.text = index < history.Count ? history[index] : string.Empty;
                        input.MoveTextEnd(false);
                    }

                    // Tab 으로 오른쪽 탭으로 넘어감. 맨 오른쪽에서는 맨 왼쪽으로.
                    if (keyboard.tabKey.wasPressedThisFrame)
                    {
                        Select(channels[(channels.IndexOf(current) + 1) % channels.Count]);
                    }

                    // 포커스를 다시 잡으면 캐럿이 맨 앞으로 가므로 끝으로 보냄.
                    if (!inputFocused && input.isFocused)
                    {
                        input.MoveTextEnd(false);
                    }
                }
            }
            else if (!inGameQuery.IsEmpty && !state.ValueRO.GameMenuOpened && !state.ValueRO.InventoryOpened)
            {
                // 전송하느라 Enter 를 누른 프레임은 위에서 닫히므로 다시 열리지 않음.
                if (chatAction.action.WasPressedThisFrame())
                {
                    SetOpened(true);
                }
                // `/` 를 누르면 명령어를 바로 칠 수 있게 엶.
                else if (keyboard != null
                    && (keyboard.slashKey.wasPressedThisFrame || keyboard.numpadDivideKey.wasPressedThisFrame))
                {
                    SetOpened(true);
                    input.text = ChatCommands.Prefix.ToString();
                }
            }

            // 닫혀 있을 때 보이는 탭(안 읽은 귓속말) 위에 커서가 있는지
            bool hovered = false;

            if (!opened && Mouse.current != null)
            {
                Vector2 cursor = Mouse.current.position.ReadValue();

                foreach (Channel channel in channels)
                {
                    hovered |= channel.Unread && Contains((RectTransform)channel.Tab.transform, cursor);
                }
            }

            // 열려 있는 동안 본문의 닉네임에 커서를 올리면 강조하고, 누르면 메뉴를 띄움.
            if (opened && Mouse.current != null)
            {
                Vector2 cursor = Mouse.current.position.ReadValue();
                bool pressed = Mouse.current.leftButton.wasPressedThisFrame
                               || Mouse.current.rightButton.wasPressedThisFrame;
                bool overMenu = nameMenu.gameObject.activeSelf && Contains(nameMenu, cursor);

                // 본문 밖으로 밀려나 가려진 줄은 제외.
                int link = overMenu || !Contains((RectTransform)body.transform.parent, cursor)
                    ? -1
                    : TMP_TextUtilities.FindIntersectingLink(body, cursor, canvas.worldCamera);

                TMP_LinkInfo info = link >= 0 ? body.textInfo.linkInfo[link] : default;
                bool accept = link >= 0 && info.GetLinkID().StartsWith(AcceptLink);

                nameHighlight.gameObject.SetActive(link >= 0);

                // 메뉴 밖을 누르면 메뉴를 닫음. 닉네임을 눌렀으면 그 자리에 다시 띄움.
                if (pressed && !overMenu)
                {
                    nameMenu.gameObject.SetActive(link >= 0 && !accept);
                }

                if (link >= 0)
                {
                    TMP_CharacterInfo first = body.textInfo.characterInfo[info.linkTextfirstCharacterIndex];
                    TMP_CharacterInfo last = body.textInfo.characterInfo[
                        info.linkTextfirstCharacterIndex + info.linkTextLength - 1];

                    var min = new Vector3(first.bottomLeft.x, first.descender);
                    var max = new Vector3(last.topRight.x, first.ascender);

                    nameHighlight.position = body.transform.TransformPoint((min + max) * 0.5F);
                    nameHighlight.sizeDelta = max - min;

                    if (pressed && accept)
                    {
                        SendParty(PartyCommand.Accept, string.Empty, int.Parse(info.GetLinkID().Substring(AcceptLink.Length)));
                    }
                    else if (pressed)
                    {
                        menuName = info.GetLinkText();

                        RectTransformUtility.ScreenPointToWorldPointInRectangle(
                            (RectTransform)transform, cursor, canvas.worldCamera, out Vector3 position);
                        nameMenu.position = position;
                    }
                }
            }

            if (opened)
            {
                bodyUpdateTime = Time.time;
            }

            // fadeDelay 가 지나면 fadeDuration 에 걸쳐 본문을 숨김.
            body.alpha = 1.0F - Mathf.Clamp01((Time.time - bodyUpdateTime - fadeDelay) / fadeDuration);

            submitted = false;
            inputFocused = input.isFocused;
            state.ValueRW.ChatFocused = opened;
            state.ValueRW.ChatHovered = hovered;
        }

        private void SetOpened(bool value)
        {
            opened = value;
            historyIndex = history.Count;

            nameHighlight.gameObject.SetActive(false);
            nameMenu.gameObject.SetActive(false);

            bodyBackground.enabled = value;
            bodyOutline.SetActive(value);
            inputRow.SetActive(value);

            if (value)
            {
                tabContainer.SetAsFirstSibling();
            }
            else
            {
                tabContainer.SetAsLastSibling();
            }

            foreach (Channel channel in channels)
            {
                Refresh(channel);
            }

            if (value)
            {
                input.ActivateInputField();
                return;
            }

            input.text = string.Empty;

            // 선택이 남아 있으면 Enter / ESC 가 입력창으로 들어감.
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == input.gameObject)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }
        }

        // 탭의 색과 표시 여부를 맞춤. 닫혀 있는 동안엔 안 읽은 귓속말 탭만 보임.
        private void Refresh(Channel channel)
        {
            channel.Tab.SetState(channel == current, channel.Unread);
            channel.Tab.gameObject.SetActive(opened || channel.Unread);
        }

        private void OnLocaleChanged(Locale locale)
        {
            publicChannel.Name = L10n.Get("Chat/Public");
            publicChannel.Tab.SetTitle(publicChannel.Name);

            ((TMP_Text)input.placeholder).text = L10n.Get("Chat/Placeholder");
            whisperOption.GetComponentInChildren<TMP_Text>().text = L10n.Get("Chat/Whisper");

            if (partyChannel != null)
            {
                partyChannel.Name = L10n.Get("Chat/Party");
                partyChannel.Tab.SetTitle(partyChannel.Name);
            }
            partyInviteOption.GetComponentInChildren<TMP_Text>().text = L10n.Get("Chat/PartyInvite");
            channelLabel.text = $"[{current.Name}]:";
        }

        private bool Contains(RectTransform rect, Vector2 cursor)
        {
            return RectTransformUtility.RectangleContainsScreenPoint(rect, cursor, canvas.worldCamera);
        }

        // 상대 이름의 귓속말 채널. 없으면 탭을 새로 만듦.
        private Channel GetWhisper(string peer)
        {
            if (!whispers.TryGetValue(peer, out Channel channel))
            {
                channel = AddChannel(peer, true);
                whispers[peer] = channel;
            }

            return channel;
        }

        private Channel AddChannel(string name, bool closable)
        {
            var channel = new Channel { Name = name, Tab = Instantiate(tabPrefab, tabContainer) };

            channel.Tab.Set(
                name,
                closable,
                // 탭을 누르면 바로 칠 수 있게 채팅창도 엶.
                () =>
                {
                    Select(channel);
                    SetOpened(true);
                },
                () => RemoveChannel(channel)
            );
            channels.Add(channel);
            Refresh(channel);

            return channel;
        }

        private void RemoveChannel(Channel channel)
        {
            if (channel == current)
            {
                Select(publicChannel);
            }

            // 파티 탭과 이름이 같은 귓속말 탭은 건드리지 않음.
            if (whispers.TryGetValue(channel.Name, out Channel whisper) && whisper == channel)
            {
                whispers.Remove(channel.Name);
            }

            channels.Remove(channel);
            Destroy(channel.Tab.gameObject);
        }

        private void Select(Channel channel)
        {
            Channel previous = current;

            current = channel;
            channel.Unread = false;

            if (previous != null)
            {
                Refresh(previous);
            }

            Refresh(channel);
            channelLabel.text = $"[{channel.Name}]:";
            channelLabel.color = channel == publicChannel ? publicColor
                : channel == partyChannel ? partyColor
                : whisperColor;
            body.text = string.Join("\n", channel.Lines);
            bodyUpdateTime = Time.time;
            nameMenu.gameObject.SetActive(false);

            // 맨 아래 줄로 되돌림.
            body.rectTransform.anchoredPosition = Vector2.zero;
        }

        private void Push(Channel channel, string line)
        {
            channel.Lines.Add(line);

            if (channel.Lines.Count > maxLines)
            {
                channel.Lines.RemoveAt(0);
            }

            if (channel == current)
            {
                body.text = string.Join("\n", channel.Lines);
                bodyUpdateTime = Time.time;
            }
        }

        // 안내문은 보고 있는 탭에 띄움.
        private void PushSystem(string text)
        {
            Push(current, $"<color=#{systemHex}>{Escape(text)}</color>");
        }

        // {Nickname}: {Message}. 다른 플레이어의 닉네임은 누를 수 있게 link 로 감쌈.
        private static string Format(string nameHex, in ChatRPC message, bool mine)
        {
            string name = $"<color=#{nameHex}>{Escape(message.Sender.ToString())}</color>";

            if (!mine)
            {
                name = $"<link=\"name\">{name}</link>";
            }

            return $"{name}: {Escape(message.Text.ToString())}";
        }

        // 유저가 친 글자가 리치 텍스트 태그로 읽히지 않게 함.
        private static string Escape(string text)
        {
            return text.Replace("<", "<noparse><</noparse>");
        }

        /// <summary>
        /// 입력창에서 Enter 를 눌렀을 때 호출됨.
        /// </summary>
        private void Submit(string text)
        {
            submitted = true;
            text = text.Trim();

            if (text.Length == 0 || !ClientWorldScope.IsAlive(clientWorld) || inGameQuery.IsEmpty)
            {
                return;
            }

            // 직전 입력과 같으면 다시 기억하지 않음.
            if (history.Count == 0 || history[history.Count - 1] != text)
            {
                history.Add(text);
            }

            if (history.Count > maxHistory)
            {
                history.RemoveAt(0);
            }

            // 일반 채팅은 보고 있는 탭의 채널로 보냄. 귓속말은 "{받는 이름} {본문}".
            if (text[0] == ChatCommands.Prefix)
            {
                if (!commands.Execute(text))
                {
                    PushSystem(L10n.Get("Chat/UnknownCommand"));
                }
            }
            else if (current == publicChannel)
            {
                Send(ChatChannel.Public, text);
            }
            else if (current == partyChannel)
            {
                Send(ChatChannel.Party, text);
            }
            else
            {
                Send(ChatChannel.Whisper, $"{current.Name} {text}");
            }

            body.rectTransform.anchoredPosition = Vector2.zero;
        }

        // `/w {Username} {Message}`. 받는 이름은 서버가 찾음.
        private void Whisper(string args)
        {
            if (args.IndexOf(' ') < 0)
            {
                PushSystem(L10n.Get("Chat/WhisperUsage"));
                return;
            }

            Send(ChatChannel.Whisper, args);
        }

        // `/p {inv|delegate|kick|auth|revoke|quit} {Username}`. 권한과 대상은 서버가 확인함.
        private void Party(string args)
        {
            int space = args.IndexOf(' ');
            string name = space < 0 ? string.Empty : args.Substring(space + 1).Trim();

            PartyCommand? command = (space < 0 ? args : args.Substring(0, space)).ToLowerInvariant() switch
            {
                "inv"      => PartyCommand.Invite,
                "delegate" => PartyCommand.Delegate,
                "kick"     => PartyCommand.Kick,
                "auth"     => PartyCommand.Authorize,
                "revoke"   => PartyCommand.Revoke,
                "quit"     => PartyCommand.Quit,
                _          => null,
            };

            // Quit 만 대상 이름이 없음.
            if (command == null || (name.Length == 0 && command != PartyCommand.Quit))
            {
                PushSystem(L10n.Get("Party/Usage"));
                return;
            }

            SendParty(command.Value, name);
        }

        private void SendParty(PartyCommand command, string name, int inviter = 0)
        {
            var rpc = new PartyCommandRPC { Command = command, Inviter = inviter };
            rpc.Target.CopyFromTruncated(name);

            Entity rpcEntity = entityManager.CreateEntity(typeof(PartyCommandRPC), typeof(SendRpcCommandRequest));
            entityManager.SetComponentData(rpcEntity, rpc);
        }

        private void Send(ChatChannel channel, string text)
        {
            var chat = new ChatRPC { Channel = channel };
            chat.Text.CopyFromTruncated(text);

            Entity rpcEntity = entityManager.CreateEntity(typeof(ChatRPC), typeof(SendRpcCommandRequest));
            entityManager.SetComponentData(rpcEntity, chat);
        }
    }
}