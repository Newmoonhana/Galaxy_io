using System.Collections.Generic;
using System.Text;
using GalaxyIO.DOTS.Client.Components.Singleton;
using GalaxyIO.DOTS.Components.Actor;
using GalaxyIO.DOTS.Components.Singleton;
using GalaxyIO.Tools;
using TMPro;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;

namespace GalaxyIO.Scripts.Managed.UI
{
    /// <summary>
    /// 팀 모드 HUD. 전투 구역 이탈 경고와 남은 시간, 팀 점수, 위성 점령 상태, 두 대장선의 체력을 보여 줌.
    /// </summary>
    public sealed class GameModeHudView : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("화면 가운데의 전투 구역 이탈 경고. 빨간 글씨.")]
        private TMP_Text zoneLabel;

        [SerializeField]
        [Tooltip("팀 점수. 점수로 이기는 모드에서만 보임.")]
        private TMP_Text scoreLabel;

        [SerializeField]
        [Tooltip("위성 이름과 점령 상태. 점령전에서만 보임.")]
        private TMP_Text satelliteLabel;

        [SerializeField]
        [Tooltip("팀 점수와 위성 상태 뒤의 반투명 배경. 둘 중 하나라도 보이면 켬.")]
        private GameObject scoreBackground;

        [Header("Flagship Health")]

        [SerializeField]
        [Tooltip("대장선 체력 막대 묶음. 대장선 잡기에서만 보임.")]
        private GameObject kingBars;

        [SerializeField]
        [Tooltip("빨강 대장선 체력. 왼쪽부터 가로 비율만큼 채움.")]
        private RectTransform redKingFill;

        [SerializeField]
        [Tooltip("파랑 대장선 체력. 오른쪽부터 가로 비율만큼 채움.")]
        private RectTransform blueKingFill;

        [SerializeField]
        private TMP_Text redKingLabel;

        [SerializeField]
        private TMP_Text blueKingLabel;

        [Header("Colors")]

        [SerializeField]
        private Color redColor = new Color(1.0F, 0.3F, 0.3F);

        [SerializeField]
        private Color blueColor = new Color(0.3F, 0.6F, 1.0F);

        [SerializeField]
        private Color neutralColor = Color.white;

        [Header("Capture")]

        [SerializeField]
        [Tooltip("경보가 울릴 때 위성 글자를 흔드는 세기. 글자 크기 대비 비율. 번쩍임과 함께 줄어듦.")]
        private float shakeStrength = 0.08F;

        private EntityQuery modeQuery;
        private EntityQuery stateQuery;
        private EntityQuery shipQuery;
        private EntityQuery satelliteQuery;
        private EntityQuery kingQuery;

        // 마지막으로 그린 대장선 체력. 바뀔 때만 글자를 다시 만듦.
        private int shownRedHealth = -1;
        private int shownBlueHealth = -1;

        // 캐시한 쿼리가 속한 클라이언트 월드. 세션 종료 시 Dispose 되므로 매 프레임 확인함.
        private World clientWorld;

        // 전투 구역 밖에 있던 시간(초). 서버도 같은 시간을 세어 격추함.
        private float outside;

        private readonly StringBuilder builder = new StringBuilder();

        // 흔들 위성 글자의 위치(태그를 뺀 글자 순번). 경보가 울린 직후에만 채움.
        private readonly List<int> shaking = new List<int>();

        // 지난 프레임에 글자를 흔들었는지. 멈출 때 메시를 한 번 되돌림.
        private bool shook;

        private void Start()
        {
            clientWorld = ClientServerBootstrap.ClientWorld;

            if (clientWorld == null)
            {
                enabled = false;
                return;
            }

            EntityManager entityManager = clientWorld.EntityManager;
            modeQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<GameModeComponent>());
            stateQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<GameModeStateComponent>());
            shipQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<GhostOwnerIsLocal>(),
                ComponentType.ReadOnly<PlayerSpaceshipMovementComponent>(),
                ComponentType.ReadOnly<LocalTransform>()
            );
            satelliteQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<SatelliteComponent>());
            kingQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<GhostOwner>(),
                ComponentType.ReadOnly<FactionComponent>(),
                ComponentType.ReadOnly<CarrierComponent>(),
                ComponentType.ReadOnly<SpaceshipSpecComponent>()
            );
        }

        private void LateUpdate()
        {
            if (!ClientWorldScope.IsAlive(clientWorld) || modeQuery.IsEmpty)
            {
                zoneLabel.enabled = false;
                scoreLabel.enabled = false;
                satelliteLabel.enabled = false;
                scoreBackground.SetActive(false);
                kingBars.SetActive(false);
                return;
            }

            GameModeComponent mode = modeQuery.GetSingleton<GameModeComponent>();
            GameModeStateComponent state = stateQuery.GetSingleton<GameModeStateComponent>();

            bool escaped = false;

            if (!shipQuery.IsEmpty)
            {
                using var transforms = shipQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
                escaped = !mode.Contains(transforms[0].Position.xy);
            }

            outside = escaped ? outside + Time.deltaTime : 0.0F;
            zoneLabel.enabled = escaped;

            if (escaped)
            {
                zoneLabel.text = string.Format(L10n.Get("Zone/Return"), Mathf.Max(Mathf.CeilToInt(mode.OutOfZoneTime - outside), 0));
            }

            // 판이 끝나면 결과 화면과 겹치지 않게 위쪽 표시를 감춤. 최종 점수는 점수판에 있음.
            bool playing = state.Winner == Team.None;

            scoreLabel.enabled = playing && mode.WinScore > 0;

            if (scoreLabel.enabled)
            {
                scoreLabel.text = $"<color=#{Hex(redColor)}>{state.RedScore}</color>  :  <color=#{Hex(blueColor)}>{state.BlueScore}</color>   / {mode.WinScore}";
            }

            satelliteLabel.enabled = playing && !satelliteQuery.IsEmpty;

            if (satelliteLabel.enabled)
            {
                using var satellites = satelliteQuery.ToComponentDataArray<SatelliteComponent>(Allocator.Temp);

                // 경보가 울린 순간부터 CaptureZap.Duration 동안. 경보를 듣는 팀(위성 주인)에게만 보임.
                float zap = CaptureZap.Strength;

                builder.Clear();
                shaking.Clear();

                // 태그를 뺀 글자 수. TMP 글자 순번과 맞춤.
                int plain = 0;

                for (byte index = 0; index < satellites.Length; index++)
                {
                    foreach (SatelliteComponent satellite in satellites)
                    {
                        if (satellite.Index != index) continue;

                        // 평소에는 점령한 팀 색. 우리 위성에 상대 점령도가 쌓이면 경보가 울릴 때 점령하는 팀 색으로 번쩍이며 흔들림.
                        Color color = TeamColor(satellite.Owner);

                        if (zap > 0.0F && CaptureZap.IsAlarming(satellite, state.Team))
                        {
                            color = Color.Lerp(color, TeamColor(satellite.Capturing), zap);
                            shaking.Add(plain);
                        }

                        // 점령도는 위성 아래에 그림(SpaceshipNameLabels).
                        string letter = SatelliteComponent.Name(index);
                        builder.Append($"<color=#{Hex(color)}>{letter}</color>");
                        plain += letter.Length;

                        builder.Append("   ");
                        plain += 3;
                    }
                }

                satelliteLabel.text = builder.ToString();
            }
            else
            {
                shaking.Clear();
            }

            ShakeLetters();

            scoreBackground.SetActive(scoreLabel.enabled || satelliteLabel.enabled);
            kingBars.SetActive(playing && mode.Mode == GameMode.Regicide);

            if (kingBars.activeSelf)
            {
                UpdateKingBars();
            }
        }

        // 경보가 울린 위성 글자의 정점을 매 프레임 무작위로 옮김. 메시를 새로 만든 뒤 옮기므로 쌓이지 않음.
        private void ShakeLetters()
        {
            if (shaking.Count == 0 && !shook) return;

            satelliteLabel.ForceMeshUpdate();
            shook = shaking.Count > 0;

            if (!shook) return;

            TMP_TextInfo info = satelliteLabel.textInfo;
            float amplitude = satelliteLabel.fontSize * shakeStrength * CaptureZap.Strength;

            foreach (int index in shaking)
            {
                if (index >= info.characterCount) continue;

                TMP_CharacterInfo character = info.characterInfo[index];

                if (!character.isVisible) continue;

                Vector3[] vertices = info.meshInfo[character.materialReferenceIndex].vertices;
                Vector3 offset = Random.insideUnitCircle * amplitude;

                for (int i = 0; i < 4; i++)
                {
                    vertices[character.vertexIndex + i] += offset;
                }
            }

            satelliteLabel.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices);
        }

        // 두 대장선의 체력 막대. 격추된 대장선은 0.
        private void UpdateKingBars()
        {
            SpaceshipSpecComponent red = default;
            SpaceshipSpecComponent blue = default;

            using (var kings = kingQuery.ToEntityArray(Allocator.Temp))
            {
                EntityManager entityManager = clientWorld.EntityManager;

                foreach (Entity king in kings)
                {
                    uint party = entityManager.GetComponentData<FactionComponent>(king).Party;

                    if (!Teams.IsKing(entityManager.GetComponentData<GhostOwner>(king).NetworkId, party, true))
                    {
                        continue;
                    }

                    if (Teams.FromParty(party) == Team.Red) red = entityManager.GetComponentData<SpaceshipSpecComponent>(king);
                    else blue = entityManager.GetComponentData<SpaceshipSpecComponent>(king);
                }
            }

            SetKingBar(redKingFill, redKingLabel, red, false, ref shownRedHealth);
            SetKingBar(blueKingFill, blueKingLabel, blue, true, ref shownBlueHealth);
        }

        private static void SetKingBar(RectTransform fill, TMP_Text label, in SpaceshipSpecComponent spec, bool fromRight, ref int shown)
        {
            float ratio = spec.MaxHealth > 0.0F ? Mathf.Clamp01(spec.Health / spec.MaxHealth) : 0.0F;
            Vector2 max = fill.anchorMax;
            Vector2 min = fill.anchorMin;

            // 빨강은 왼쪽에서, 파랑은 오른쪽에서 채움.
            if (fromRight) min.x = 1.0F - ratio;
            else max.x = ratio;

            fill.anchorMin = min;
            fill.anchorMax = max;

            int health = Mathf.CeilToInt(Mathf.Max(spec.Health, 0.0F));

            if (health == shown) return;

            shown = health;
            label.text = $"{health}/{Mathf.CeilToInt(spec.MaxHealth)}";
        }

        private Color TeamColor(Team team)
        {
            return team == Team.Red ? redColor : team == Team.Blue ? blueColor : neutralColor;
        }

        private static string Hex(Color color)
        {
            return ColorUtility.ToHtmlStringRGB(color);
        }
    }
}
