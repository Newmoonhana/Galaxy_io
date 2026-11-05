using System.Collections.Generic;
using GalaxyIO.DOTS.Client.Networking;
using GalaxyIO.DOTS.Components.Actor;
using GalaxyIO.DOTS.Components.Singleton;
using GalaxyIO.Main;
using GalaxyIO.Scripts.Managed.Background;
using GalaxyIO.Tools;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GalaxyIO.Spectator
{
    /// <summary>
    /// 관찰자 카메라. 플레이어, NPC 함대(같은 이름표의 봇 무리), 함대 안의 함선을 골라 따라가거나(락온) 자유 시점으로 둘러봄.
    /// 카메라가 비추는 곳을 서버에 알려(SpectateRPC) 그 주변을 복제받음.
    /// <para/>
    /// 조작: 휠 확대/축소, WASD·방향키 또는 오른쪽 드래그 이동(자유 시점), 함선 왼쪽 클릭 락온,
    /// Tab / Shift+Tab 다음/이전 대상, F 자유 시점, Z 함대 자동 맞춤, H 목록·HUD 숨기기.
    /// </summary>
    [RequireComponent(typeof(UnityEngine.Camera))]
    public sealed class SpectatorCamera : MonoBehaviour
    {
        private const string BotPrefix = "[봇]";
        private const float MinSize = 6.0F;
        private const float MaxSize = 450.0F;
        private const float ZoomStep = 0.12F;
        private const float SmoothSpeed = 8.0F;
        private const float SendInterval = 0.5F;
        private const float PanelWidth = 300.0F;

        private enum LockMode
        {
            Free,
            Ship,
            Fleet,
        }

        private struct ShipInfo
        {
            public Entity Entity;
            public string Name;
            public SpaceshipId Kind;
            public Vector2 Position;
            public float Health;
            public float MaxHealth;
        }

        private World world;
        private EntityManager entityManager;
        private EntityQuery shipQuery;
        private UnityEngine.Camera targetCamera;

        private readonly List<ShipInfo> ships = new List<ShipInfo>();
        private readonly SortedDictionary<string, List<int>> fleets = new SortedDictionary<string, List<int>>();
        private readonly List<int> players = new List<int>();
        private readonly HashSet<string> expanded = new HashSet<string>();

        private LockMode mode = LockMode.Free;
        private Entity lockedShip = Entity.Null;
        private string lockedFleet;
        private bool autoFit = true;
        private bool hidden;

        private Vector2 focus;
        private float targetSize = 60.0F;
        private float nextSendTime;
        private bool revealed;
        private Vector2 listScroll;

        private GUIStyle labelStyle;
        private GUIStyle buttonStyle;
        private GUIStyle selectedStyle;

        private void Start()
        {
            world = ClientServerBootstrap.ClientWorld;
            targetCamera = GetComponent<UnityEngine.Camera>();

            if (world == null)
            {
                enabled = false;
                return;
            }

            entityManager = world.EntityManager;
            shipQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<PlayerNameComponent>(),
                ComponentType.ReadOnly<LocalTransform>(),
                ComponentType.ReadOnly<SpaceshipKindComponent>(),
                ComponentType.ReadOnly<SpaceshipSpecComponent>()
            );

            focus = transform.position;
            targetSize = Mathf.Clamp(targetCamera.orthographicSize * 3.0F, MinSize, MaxSize);

            // 레이더: 관찰 지점을 중심으로 초계함 최대 강화 수준(나이트호크)의 탐지 거리, 스캔 속도, 반응 시간.
            GalaxyIO.Scripts.Managed.UI.RadarView.SpectatorActive = true;
            ScoutRadar(out GalaxyIO.Scripts.Managed.UI.RadarView.SpectatorRange, out GalaxyIO.Scripts.Managed.UI.RadarView.SpectatorSweepSpeed, out GalaxyIO.Scripts.Managed.UI.RadarView.SpectatorResponse);
        }

        private void OnDestroy()
        {
            GalaxyIO.Scripts.Managed.UI.RadarView.SpectatorActive = false;
        }

        // 나이트호크 최대 강화 레이더. 프리팹을 못 찾으면 대략값.
        private void ScoutRadar(out float range, out float sweep, out float response)
        {
            range = 200.0F;
            sweep = 360.0F;
            response = 0.0F;

            using EntityQuery registryQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<SpaceshipRegistryElement>());

            if (registryQuery.IsEmpty)
            {
                return;
            }

            Entity prefab = registryQuery.GetSingletonBuffer<SpaceshipRegistryElement>(true).Find(SpaceshipId.Nighthawk);

            if (prefab == Entity.Null || !entityManager.HasComponent<SpaceshipRadarComponent>(prefab))
            {
                return;
            }

            SpaceshipRadarComponent radar = entityManager.GetComponentData<SpaceshipRadarComponent>(prefab);
            range = radar.Range;
            sweep = radar.SweepSpeed;
            response = radar.ResponseTime;

            if (entityManager.HasBuffer<UpgradeParamElement>(prefab) && entityManager.HasBuffer<UpgradeElement>(prefab))
            {
                DynamicBuffer<UpgradeParamElement> parameters = entityManager.GetBuffer<UpgradeParamElement>(prefab, true);
                DynamicBuffer<UpgradeElement> limits = entityManager.GetBuffer<UpgradeElement>(prefab, true);

                float Top(UpgradeParam param)
                {
                    UpgradeParamElement p = parameters[(int)param];
                    int level = (int)p.Stat < limits.Length ? limits[(int)p.Stat].MaxLevel : 0;
                    return p.Base + p.Amount * level;
                }

                range += Top(UpgradeParam.RadarRange);
                sweep += Top(UpgradeParam.RadarSweep);
                response = Mathf.Max(response - Top(UpgradeParam.RadarResponse), 0.0F);
            }
        }

        private void Update()
        {
            if (!ClientWorldScope.IsAlive(world))
            {
                return;
            }

            CollectShips();
            HandleInput();
        }

        private void LateUpdate()
        {
            if (!ClientWorldScope.IsAlive(world))
            {
                return;
            }

            Vector2 goal = focus;

            switch (mode)
            {
                case LockMode.Ship:
                    if (TryFindShip(lockedShip, out ShipInfo ship))
                    {
                        goal = ship.Position;
                    }
                    else if (lockedFleet != null && fleets.ContainsKey(lockedFleet))
                    {
                        // 따라가던 함선이 격추됨: 그 함대로 넘어감.
                        mode = LockMode.Fleet;
                    }
                    else
                    {
                        mode = LockMode.Free;
                    }
                    break;

                case LockMode.Fleet:
                    if (lockedFleet != null && fleets.TryGetValue(lockedFleet, out List<int> members) && members.Count > 0)
                    {
                        goal = FleetBounds(members, out float halfHeight);

                        if (autoFit)
                        {
                            targetSize = Mathf.Clamp(halfHeight * 1.3F + 12.0F, MinSize, MaxSize);
                        }
                    }
                    else
                    {
                        mode = LockMode.Free;
                    }
                    break;
            }

            float t = 1.0F - Mathf.Exp(-SmoothSpeed * Time.unscaledDeltaTime);

            // 처음에는 바로 옮김.
            focus = revealed && mode != LockMode.Free ? Vector2.Lerp(focus, goal, t) : goal;
            transform.position = new Vector3(focus.x, focus.y, transform.position.z);

            // 레이더 중심 = 관찰 지점(락온 대상)
            GalaxyIO.Scripts.Managed.UI.RadarView.SpectatorCenter = focus;
            targetCamera.orthographicSize = Mathf.Lerp(targetCamera.orthographicSize, targetSize, t);

            if (Time.unscaledTime >= nextSendTime)
            {
                nextSendTime = Time.unscaledTime + SendInterval;
                ClientNetworkRequestManager.RequestSpectate(focus);
            }

            if (!revealed)
            {
                revealed = true;
                BackgroundView.RevealAcrossScenes();
            }
        }

        private void CollectShips()
        {
            ships.Clear();
            fleets.Clear();
            players.Clear();

            using NativeArray<Entity> entities = shipQuery.ToEntityArray(Allocator.Temp);
            using NativeArray<PlayerNameComponent> names = shipQuery.ToComponentDataArray<PlayerNameComponent>(Allocator.Temp);
            using NativeArray<LocalTransform> transforms = shipQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            using NativeArray<SpaceshipKindComponent> kinds = shipQuery.ToComponentDataArray<SpaceshipKindComponent>(Allocator.Temp);
            using NativeArray<SpaceshipSpecComponent> specs = shipQuery.ToComponentDataArray<SpaceshipSpecComponent>(Allocator.Temp);

            for (int i = 0; i < entities.Length; i++)
            {
                string name = names[i].Value.ToString();

                if (string.IsNullOrEmpty(name))
                {
                    continue;
                }

                int index = ships.Count;

                ships.Add(new ShipInfo
                {
                    Entity = entities[i],
                    Name = name,
                    Kind = kinds[i].Value,
                    Position = new Vector2(transforms[i].Position.x, transforms[i].Position.y),
                    Health = specs[i].Health,
                    MaxHealth = specs[i].MaxHealth,
                });

                if (name.StartsWith(BotPrefix))
                {
                    if (!fleets.TryGetValue(name, out List<int> list))
                    {
                        list = new List<int>();
                        fleets[name] = list;
                    }

                    list.Add(index);
                }
                else
                {
                    players.Add(index);
                }
            }

            players.Sort((a, b) => string.CompareOrdinal(ships[a].Name, ships[b].Name));

            foreach (List<int> list in fleets.Values)
            {
                list.Sort((a, b) => ships[a].Kind != ships[b].Kind
                    ? ((int)ships[b].Kind).CompareTo((int)ships[a].Kind)
                    : ships[a].Entity.Index.CompareTo(ships[b].Entity.Index));
            }
        }

        private void HandleInput()
        {
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;

            if (!Application.isFocused)
            {
                return;
            }

            bool overPanel = !hidden && mouse != null && mouse.position.ReadValue().x <= PanelWidth + 20.0F;

            if (mouse != null && !overPanel)
            {
                float scroll = mouse.scroll.ReadValue().y;

                if (scroll != 0.0F)
                {
                    targetSize = Mathf.Clamp(targetSize * (1.0F - Mathf.Sign(scroll) * ZoomStep), MinSize, MaxSize);

                    // 함대 자동 맞춤 중에 직접 확대/축소하면 맞춤을 끔.
                    if (mode == LockMode.Fleet)
                    {
                        autoFit = false;
                    }
                }

                // 오른쪽 드래그: 자유 시점으로 끌어 옮김.
                if (mouse.rightButton.isPressed)
                {
                    Vector2 delta = mouse.delta.ReadValue();

                    if (delta.sqrMagnitude > 0.0F)
                    {
                        float unitsPerPixel = targetCamera.orthographicSize * 2.0F / Screen.height;
                        FreeAt(focus - delta * unitsPerPixel);
                    }
                }

                // 왼쪽 클릭: 커서 가까이의 함선에 락온.
                if (mouse.leftButton.wasPressedThisFrame)
                {
                    Vector3 clicked = targetCamera.ScreenToWorldPoint(mouse.position.ReadValue());
                    float pick = Mathf.Max(targetCamera.orthographicSize * 0.06F, 3.0F);
                    int best = -1;
                    float bestDistance = pick * pick;

                    for (int i = 0; i < ships.Count; i++)
                    {
                        float distance = (ships[i].Position - new Vector2(clicked.x, clicked.y)).sqrMagnitude;

                        if (distance < bestDistance)
                        {
                            best = i;
                            bestDistance = distance;
                        }
                    }

                    if (best >= 0)
                    {
                        LockShip(ships[best]);
                    }
                }
            }

            if (keyboard == null)
            {
                return;
            }

            Vector2 move = Vector2.zero;

            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) move.y += 1.0F;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) move.y -= 1.0F;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) move.x += 1.0F;
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) move.x -= 1.0F;

            if (move != Vector2.zero)
            {
                FreeAt(focus + move.normalized * (targetCamera.orthographicSize * 1.5F * Time.unscaledDeltaTime));
            }

            if (keyboard.tabKey.wasPressedThisFrame)
            {
                Cycle(keyboard.shiftKey.isPressed ? -1 : 1);
            }

            if (keyboard.fKey.wasPressedThisFrame)
            {
                FreeAt(focus);
            }

            if (keyboard.zKey.wasPressedThisFrame)
            {
                autoFit = !autoFit;
            }

            if (keyboard.hKey.wasPressedThisFrame)
            {
                hidden = !hidden;

                if (GalaxyIO_HUD.HasInstance)
                {
                    GalaxyIO_HUD.Instance.Hud.enabled = !hidden;
                }
            }
        }

        private void FreeAt(Vector2 position)
        {
            mode = LockMode.Free;
            focus = position;
        }

        private void LockShip(in ShipInfo ship)
        {
            mode = LockMode.Ship;
            lockedShip = ship.Entity;
            lockedFleet = ship.Name.StartsWith(BotPrefix) ? ship.Name : null;
        }

        private void LockFleet(string name)
        {
            mode = LockMode.Fleet;
            lockedFleet = name;
            lockedShip = Entity.Null;
            autoFit = true;
        }

        // 대상 순서: 함대들(이름순) → 플레이어들(이름순)
        private void Cycle(int direction)
        {
            var targets = new List<string>();
            targets.AddRange(fleets.Keys);

            foreach (int i in players)
            {
                targets.Add("\u0001" + i);
            }

            if (targets.Count == 0)
            {
                return;
            }

            int current = -1;

            if (mode == LockMode.Fleet && lockedFleet != null)
            {
                current = targets.IndexOf(lockedFleet);
            }
            else if (mode == LockMode.Ship)
            {
                for (int i = 0; i < players.Count; i++)
                {
                    if (ships[players[i]].Entity == lockedShip)
                    {
                        current = fleets.Count + i;
                    }
                }
            }

            int next = ((current + direction) % targets.Count + targets.Count) % targets.Count;

            if (next < fleets.Count)
            {
                LockFleet(targets[next]);
            }
            else
            {
                LockShip(ships[players[next - fleets.Count]]);
            }
        }

        private bool TryFindShip(Entity entity, out ShipInfo ship)
        {
            foreach (ShipInfo info in ships)
            {
                if (info.Entity == entity)
                {
                    ship = info;
                    return true;
                }
            }

            ship = default;
            return false;
        }

        // 함대 중심과, 함대가 다 보이는 데 필요한 화면 절반 높이
        private Vector2 FleetBounds(List<int> members, out float halfHeight)
        {
            Vector2 min = ships[members[0]].Position;
            Vector2 max = min;

            foreach (int i in members)
            {
                min = Vector2.Min(min, ships[i].Position);
                max = Vector2.Max(max, ships[i].Position);
            }

            Vector2 half = (max - min) * 0.5F;
            halfHeight = Mathf.Max(half.y, half.x / Mathf.Max(targetCamera.aspect, 0.1F));
            return (min + max) * 0.5F;
        }

        private void OnGUI()
        {
            if (hidden || !ClientWorldScope.IsAlive(world))
            {
                return;
            }

            if (labelStyle == null)
            {
                labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, richText = true };
                buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 14, alignment = TextAnchor.MiddleLeft };
                selectedStyle = new GUIStyle(buttonStyle) { fontStyle = FontStyle.Bold };
                selectedStyle.normal.textColor = new Color(0.4F, 1.0F, 0.6F);
                selectedStyle.hover.textColor = selectedStyle.normal.textColor;
            }

            GUILayout.BeginArea(new Rect(10.0F, 10.0F, PanelWidth, Screen.height - 20.0F), GUI.skin.box);

            string current = mode switch
            {
                LockMode.Fleet => $"함대 {lockedFleet}" + (autoFit ? " (자동 맞춤)" : ""),
                LockMode.Ship => TryFindShip(lockedShip, out ShipInfo s) ? $"{s.Name} - {s.Kind}" : "함선",
                _ => "자유 시점",
            };

            GUILayout.Label($"<b>관찰자</b>  보는 중: {current}", labelStyle);
            GUILayout.Label($"위치 ({focus.x:0}, {focus.y:0})  시야 {targetSize * 2.0F:0}", labelStyle);

            listScroll = GUILayout.BeginScrollView(listScroll);

            GUILayout.Label($"<b>플레이어 ({players.Count})</b>", labelStyle);

            foreach (int i in players)
            {
                ShipInfo ship = ships[i];
                bool selected = mode == LockMode.Ship && lockedShip == ship.Entity;

                if (GUILayout.Button($"{ship.Name}  {ship.Kind}  {HealthText(ship)}", selected ? selectedStyle : buttonStyle))
                {
                    LockShip(ship);
                }
            }

            GUILayout.Space(8.0F);
            GUILayout.Label($"<b>NPC 함대 ({fleets.Count})</b>", labelStyle);

            foreach (KeyValuePair<string, List<int>> fleet in fleets)
            {
                bool open = expanded.Contains(fleet.Key);
                bool selected = mode == LockMode.Fleet && lockedFleet == fleet.Key;

                GUILayout.BeginHorizontal();

                if (GUILayout.Button(open ? "-" : "+", buttonStyle, GUILayout.Width(26.0F)))
                {
                    if (open) expanded.Remove(fleet.Key);
                    else expanded.Add(fleet.Key);
                }

                if (GUILayout.Button($"{fleet.Key.Substring(BotPrefix.Length)}  ({fleet.Value.Count}척)", selected ? selectedStyle : buttonStyle))
                {
                    LockFleet(fleet.Key);
                }

                GUILayout.EndHorizontal();

                if (!open)
                {
                    continue;
                }

                foreach (int i in fleet.Value)
                {
                    ShipInfo ship = ships[i];
                    bool shipSelected = mode == LockMode.Ship && lockedShip == ship.Entity;

                    GUILayout.BeginHorizontal();
                    GUILayout.Space(30.0F);

                    if (GUILayout.Button($"{ship.Kind}  {HealthText(ship)}", shipSelected ? selectedStyle : buttonStyle))
                    {
                        LockShip(ship);
                    }

                    GUILayout.EndHorizontal();
                }
            }

            GUILayout.EndScrollView();

            GUILayout.Label("휠 확대/축소 · WASD/우클릭 드래그 자유 시점\n클릭 락온 · Tab 다음 대상 · F 자유 · Z 함대 맞춤 · H 숨기기", labelStyle);
            GUILayout.EndArea();
        }

        private static string HealthText(in ShipInfo ship)
        {
            return ship.MaxHealth > 0.0F ? $"{Mathf.Clamp01(ship.Health / ship.MaxHealth) * 100.0F:0}%" : "";
        }
    }
}
