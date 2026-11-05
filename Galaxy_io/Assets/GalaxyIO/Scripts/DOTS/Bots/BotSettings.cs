using System;
using System.Collections.Generic;

namespace GalaxyIO.DOTS.Bots
{
    /// <summary>
    /// 자유 모드 서버 봇 설정. 데디케이티드 서버 설정(server_config.json)의 Bots 항목.
    /// 항목이 없으면 이 클래스의 기본값(기본 세력 구성)을 씀. Enabled 를 false 로 두면 봇을 만들지 않음.
    /// 세력(무리) 하나가 파티 하나. 같은 무리끼리만 아군이고 다른 무리, 플레이어, AI 적과는 모두 적대함.
    /// </summary>
    [Serializable]
    public sealed class BotSettings
    {
        public bool Enabled = true;

        // 무리가 전멸한 뒤 다시 만들기까지의 시간(초). 무리별 값이 0 이상이면 그 값을 씀.
        public float RespawnDelay = 180.0F;

        // 이 시간(초) 동안 전투(공격하거나 공격받음)가 없으면 무리를 지우고 새로 뽑아 다시 만듦. 0 이면 안 함.
        // 무리별 값이 0 이상이면 그 값을 씀. 제자리 기지(Base)는 기본으로 하지 않음.
        public float IdleRespawn = 180.0F;

        // 기지(Base)가 전멸하지 않았을 때, 이 시간(초) 동안 전투가 없으면 격추된 함선만 다시 만듦. 0 이면 안 함.
        public float BaseRefill = 180.0F;

        // 무리(기지 제외)가 전멸하지 않았을 때, 전투 없이 이 간격(초)마다 격추된 함선을 함급별로 한 척씩 다시 만듦.
        // 리더 옆 진형 자리에 만들고, 그 자리 주위에 플레이어가 있으면 다음 기회로 미룸. 0 이면 안 함.
        public float CorvetteRefill = 30.0F;
        public float FrigateRefill = 60.0F;
        public float CruiserRefill = 120.0F;

        // 무작위 강화 단계 비율 범위 (0 ~ 1). 1 이 그 기종의 항목별 최대 단계이며 넘지 않음.
        // 무리별 값이 0 이상이면 그 값을 씀.
        public float UpgradeMin = 0.0F;
        public float UpgradeMax = 1.0F;

        // 리더에서 이만큼 넘어간 적은 쫓지 않음.
        public float Leash = 500.0F;

        // 맞으면 이 반경 안의 가장 가까운 적을 공격자로 보고, 무리 전체가 최고 속도로 무기가 닿는 거리까지 쫓아감(기지 제외).
        // RetaliateTime(초)이 지나거나 공격자가 반경 x 1.8 밖으로 멀어지면 그만둠.
        public float RetaliateRange = 500.0F;
        public float RetaliateTime = 20.0F;

        // 리스폰 지점 주위 이 거리 안에 플레이어가 없어야 함(봇은 상관없음).
        public float SpawnSafeRadius = 300.0F;

        // 기지 후보 지점에 모두 플레이어가 있을 때 아무 곳에나 만들 범위(원점 기준 반지름).
        public float MapRadius = 1200.0F;

        // 플레이어를 처음 발견하는 거리 배율. 저격(Sniper), 박치기(Rammer) 무리는 적용하지 않음. 봇, AI 적은 원래 거리로 찾음.
        public float PlayerDetectScale = 1.0F;

        // 플레이어 발견 거리 상한: 초계함(Vex, Vulture, Nighthawk) 플레이어의 화면 시야(16:9 가로 반폭) x 이 배율.
        // 레이더가 아니라 클라이언트 화면 기준. 0 이면 상한 없음. 저격, 박치기 무리는 적용하지 않음.
        public float PlayerSightScale = 0.85F;

        // 봇 무리가 쏜 투사체(조종 포탑, 자동 포탑, 함재기, 폭발) 피해 배율. 박치기 피해는 그대로.
        public float DamageScale = 0.8F;

        // 맵 경계(원점 기준 반지름). 넘어가면 교전을 끊고 진형으로 돌아오고, 그 밖의 적은 노리지 않음.
        public float WorldRadius = 1350.0F;

        // 리스폰 지점은 다른 무리(리더)와 이 거리 넘게 떨어진 곳을 고름. 안 되면 가장 먼 곳.
        public float SpawnSeparation = 450.0F;

        // 박치기 무리(CraftsRam): 박치기 피해 배율(모선, 함재기), 적 발견 거리(레이더 x 배율), 함재기를 꺼내는 거리.
        public float RammerRamScale = 2.0F;
        public float RammerCraftRamScale = 3.0F;
        public float RammerDetectScale = 3.0F;
        public float RammerLaunchRange = 350.0F;

        // 플레이어 강화(자유 모드 봇 서버만): 투사체 피해, 최대 체력 배율, 강화 한 단계마다 오르는 양 배율(피해, 체력, 수리).
        // 이동 능력치는 클라이언트 예측과 맞아야 해서 바꾸지 않음.
        public float PlayerDamageScale = 1.1F;
        public float PlayerHealthScale = 1.1F;
        public float PlayerUpgradeGain = 1.25F;

        // 실수: 대상을 다시 고르는 간격(초), 새 대상을 잡고 쏘기 시작할 때까지 걸리는 시간(초) 범위,
        // 앞질러 겨누는 비율 범위(1 = 정확), 조준 흔들림 최대 각도(도).
        public float RetargetInterval = 1.0F;
        public float ReactionMin = 0.4F;
        public float ReactionMax = 1.0F;
        public float AimLeadMin = 0.6F;
        public float AimLeadMax = 1.1F;
        public float AimJitter = 4.0F;

        // AvoidSpawn 무리는 원점(플레이어 스폰 ±100) 이 반경 안의 적을 노리거나 쫓지 않음.
        public float SpawnAvoidRadius = 550.0F;

        // 초계함(Vex, Vulture, Nighthawk)이 무작위로 고르지 않는 포탑(| 로 구분). 직접 지정한 포탑은 그대로.
        public string CorvetteBannedTurrets = "Veylance";

        public List<BotGroupSettings> Groups = BotDefaults.All();
    }

    /// <summary>
    /// 월드 좌표 한 점.
    /// </summary>
    [Serializable]
    public struct BotPoint
    {
        public float X;
        public float Y;

        public BotPoint(float x, float y)
        {
            X = x;
            Y = y;
        }
    }

    /// <summary>
    /// 무리(세력) 하나.
    /// </summary>
    [Serializable]
    public sealed class BotGroupSettings
    {
        // 이름표. "[봇]" 을 앞에 붙임. 플레이어 이름 제한(12자)에 맞춰 짧게.
        public string Name = "무리";

        // Patrol(순찰) | Base(제자리 기지) | Sniper(장거리 저격) | Rammer(돌격 박치기) | Line(전열 사격) | Raider(고속 기동)
        public string Behavior = "Patrol";

        // 평소 이동 방식. Hover(가장 느린 호버링 속력에 맞춤) | Fly(가장 느린 비행 속력에 맞춤)
        public string Move = "Hover";

        public List<BotMemberSettings> Members = new List<BotMemberSettings>();

        // 순찰 루트. 이 점들을 차례로 돌고 마지막 점에서 첫 점으로 이어짐. 리스폰은 루트 위의 무작위 지점.
        // 비어 있으면 아래 중심, 반지름의 원 안을 무작위로 돎.
        public List<BotPoint> Route = new List<BotPoint>();

        // 기지(Base) 후보 지점. 플레이어가 주위에 없는 곳 중 무작위로 고름. 비어 있으면 중심 한 곳.
        public List<BotPoint> SpawnPoints = new List<BotPoint>();

        // 순찰 구역(월드 좌표) 중심과 반지름. Route 가 없을 때만 씀.
        public float CenterX;
        public float CenterY;
        public float Radius = 300.0F;

        // 적을 찾는 거리. 0 이면 행동과 레이더 범위로 정함. 크게 두면 멀리 있는 적까지 찾아가 공격함.
        public float DetectRange;

        // 진형 간격. 0 이면 가장 큰 선체 두께로 정함.
        public float Spacing;

        // 기지 배치 반지름(중심에서 각 함선까지)과 첫 함선의 기수 각도(도, 0 = 북쪽).
        public float BaseRadius = 22.0F;
        public float BaseHeading;

        // 최대 체력 배율
        public float HealthScale = 1.0F;

        // 0 이상이면 전역 값 대신 씀.
        public float RespawnDelay = -1.0F;
        public float IdleRespawn = -1.0F;
        public float UpgradeMin = -1.0F;
        public float UpgradeMax = -1.0F;

        // 항상 최대 단계로 올릴 강화 종류(UpgradeStat 이름, | 로 구분). e.g. "Speed"
        public string MaxUpgrades = "";

        // 조종 포탑. 빈 값이면 상점 포탑 중 무작위, "Keep" 이면 프리팹 그대로, 아니면 아이템 이름.
        // 여러 개를 | 로 나열하면 그중 무작위, "아이템:비중" 이면 비중대로. e.g. "Pyron:6|Bore:1|Raptor:1"
        public string MainTurret = "";

        // 자동 포탑 칸. 규칙은 MainTurret 과 같음. 칸마다 따로 굴림.
        public string AutoTurrets = "";

        // 이 기종의 최대 강화 비행 속력, 가속도를 그대로 가져옴. e.g. "Nighthawk"
        public string SpeedLike = "";

        // 조종 포탑 발사 간격 배율 (0 ~ 1). 1 이면 그대로. e.g. 0.2 면 다섯 배로 빨리 쏨.
        public float ReloadScale = 1.0F;

        // 포탑 과열 없음
        public bool NoOverheat;

        // 이 무리 모선의 함재기가 공격 대상에 박치기함.
        public bool CraftsRam;

        // 크거나 센 무리. 플레이어 스폰 지역(SpawnAvoidRadius) 안의 적은 노리지 않음. 루트도 그 밖으로 잡음.
        public bool AvoidSpawn;

        // 이 무리가 쏜 투사체 피해 배율(전역 DamageScale 에 곱함).
        public float DamageScale = 1.0F;

        // 조종 포탑을 같은 무리에서 이 수까지만 씀. "아이템:수" 를 | 로 구분. e.g. "Veylance:1"
        public string TurretCaps = "";

        public List<BotDropSettings> Drops = new List<BotDropSettings>();
    }

    /// <summary>
    /// 무리 구성원 한 줄. Ships 는 SpaceshipId 이름을 | 로 구분. 여러 개면 리스폰마다 무작위.
    /// SameKind 면 이 줄의 모든 함선이 같은 기종(한 번만 뽑음).
    /// Distinct 면 이 줄의 함선이 서로 다른 기종(겹치지 않게 뽑음. 후보보다 많으면 후보 수까지만).
    /// </summary>
    [Serializable]
    public sealed class BotMemberSettings
    {
        public string Ships = "Vex";
        public int Min = 1;
        public int Max = 1;
        public bool SameKind;
        public bool Distinct;
    }

    /// <summary>
    /// 격추 시 떨어뜨리는 재료. 함선마다 Chance 로 굴림.
    /// </summary>
    [Serializable]
    public sealed class BotDropSettings
    {
        public string Item = "PowerCell";
        public int Min = 1;
        public int Max = 1;
        public float Chance = 1.0F;
    }

    /// <summary>
    /// 기본 세력 구성. 무리마다 맵을 크게 도는 자기 루트가 있고, 루트끼리 여러 곳에서 겹침.
    /// 플레이어 스폰 지점(원점 ±100) 바로 위로는 지나가지 않게 원점에서 300 넘게 떨어뜨림.
    /// 크거나 센 무리(AvoidSpawn)는 루트가 원점에서 570 넘게 떨어져 있고 스폰 지역 안의 적은 노리지 않음.
    /// </summary>
    public static class BotDefaults
    {
        private const string Corvettes = "Vex|Vulture|Nighthawk";
        private const string Frigates = "Vanguard|Sentinel|Halberd";
        private const string Cruisers = "Leviathan|Paladin";

        /// <summary>
        /// 기본 구성 전체: 세력 무리 + 혼자 다니는 기체.
        /// </summary>
        public static List<BotGroupSettings> All()
        {
            List<BotGroupSettings> groups = Groups();
            groups.AddRange(Solos());
            return groups;
        }

        public static List<BotGroupSettings> Groups()
        {
            return new List<BotGroupSettings>
            {
                Routed(Group("벡스 무리", "Patrol", "Hover", Small(), Member("Vex", 3, 5)), Circle(0, 0, 700, 10, false)),
                Routed(Group("벌처 무리", "Patrol", "Hover", Small(), Member("Vulture", 3, 5)), Circle(0, 0, 500, 8, true)),
                Routed(Group("나이트호크무리", "Patrol", "Hover", Small(), Member("Nighthawk", 3, 5)), Rect(-900, -900, 900, 900)),
                Routed(Group("호위함 무리1", "Patrol", "Hover", Medium(), Member(Frigates, 2, 2, distinct: true)),
                    Poly(0, 850, 850, 0, 0, -850, -850, 0)),
                Routed(Group("호위함 무리2", "Patrol", "Hover", Medium(), Member(Frigates, 2, 2, distinct: true)), Rect(-500, -900, 500, 900)),
                Routed(Avoid(Group("순양함 무리", "Patrol", "Hover", Large(),
                    Member("Thor", 1, 1), Member("Paladin", 1, 1), Member("Leviathan", 1, 1))), Rect(-1000, -700, 1000, 700)),
                Routed(Avoid(Group("콜로서스함대", "Patrol", "Hover", Large(),
                    Member("Colossus", 1, 1), Member(Cruisers, 2, 2), Member(Frigates, 3, 3), Member(Corvettes, 5, 5))),
                    Circle(0, 0, 850, 12, false)),
                Routed(Avoid(Group("베히모스함대", "Patrol", "Hover", Large(),
                    Member("Behemoth", 1, 1), Member(Cruisers, 2, 2), Member(Frigates, 3, 3), Member(Corvettes, 5, 5))),
                    Circle(0, 0, 950, 12, true)),
                Base(),
                Routed(Sniper("저격 레비아탄", "Leviathan"), Poly(0, 1150, -996, -575, 996, -575)),
                Routed(Sniper("저격 팔라딘", "Paladin"), Poly(0, -1150, 996, 575, -996, 575)),
                Routed(Rammer("돌격 베히모스"), Ellipse(0, 0, 1000, 1000, 5)),
                Routed(Rammer("돌격 베히모스2"), Ellipse(0, 0, 900, 900, 5, true)),
                Routed(Line(), Ellipse(0, 0, 700, 1050, 10)),
                Routed(Raider(), Circle(0, 0, 1100, 12, true)),
                Routed(Thor(), Ellipse(0, 0, 1050, 700, 10)),

                // 매우 약한 벡스 떼 둘(서로 적). 강화 없음, 기본 포탑, 피해와 체력 최저.
                Routed(WeakSwarm("약한벡스1"), Circle(0, 0, 620, 10, false)),
                Routed(WeakSwarm("약한벡스2"), Circle(0, 0, 720, 10, true)),

                // 3척 편성. 주무기를 모두 같은 포탑으로.
                Routed(Trio("헬버드 화방대", "Halberd", "Vulture", "Pyron"), Ellipse(0, 0, 800, 600, 10)),
                Routed(Trio("뱅가드 저격대", "Vanguard", "Vex", "Vraxx"), Ellipse(0, 0, 600, 850, 10, true)),
                Routed(Trio("센티넬 따발대", "Sentinel", "Nighthawk", "Raptor"), Rect(-750, -650, 750, 650)),

                // 6척 편성. 주로 쓰는 포탑에 비중을 둠. 팔라딘 함대는 레이저를 한 척까지만.
                Routed(Avoid(Fleet6("토르 화방함대", "Thor", "Halberd", "Vulture", "Pyron:6|Bore:1|Raptor:1|Brakkar:1", "")),
                    Circle(0, 0, 900, 12, true)),
                Routed(Avoid(Fleet6("레비아탄 대포함대", "Leviathan", "Sentinel", "Nighthawk", "Brakkar:6|Bore:1|Raptor:1|Pyron:1", "")),
                    Ellipse(0, 0, 1000, 800, 12)),
                Routed(Avoid(Fleet6("팔라딘 저격함대", "Paladin", "Vanguard", "Vex", "Vraxx:6|Bore:2|Veylance:1", "Veylance:1")),
                    Rect(-950, -850, 950, 850)),

                BehemothBase(),
            };
        }

        /// <summary>
        /// 혼자 다니는 기체. 기종마다 강화 없음, 중간, 최대 세 세력. 기본 구성 뒤에 붙임(<see cref="Groups"/>).
        /// </summary>
        public static List<BotGroupSettings> Solos()
        {
            var groups = new List<BotGroupSettings>();
            string[] ships = { "Halberd", "Vulture", "Vanguard", "Vex", "Sentinel", "Nighthawk" };
            string[] names = { "헬버드", "벌처", "뱅가드", "벡스", "센티넬", "나이트호크" };
            string[] tiers = { "노업", "중업", "풀업" };
            float[] levels = { 0.0F, 0.5F, 1.0F };
            int index = 0;

            for (int s = 0; s < ships.Length; s++)
            {
                for (int t = 0; t < tiers.Length; t++)
                {
                    BotGroupSettings group = Group($"{names[s]} {tiers[t]}", "Patrol", "Hover", Small(), Member(ships[s], 1, 1));
                    group.UpgradeMin = levels[t];
                    group.UpgradeMax = levels[t];

                    // 무리마다 다른 크기, 방향의 타원 루트
                    float rx = 480.0F + (index % 6) * 110.0F;
                    float ry = 480.0F + (index * 7 % 6) * 110.0F;
                    group.Route = Ellipse(0, 0, rx, ry, 9, index % 2 == 0);
                    groups.Add(group);
                    index++;
                }
            }

            return groups;
        }

        private static BotGroupSettings WeakSwarm(string name)
        {
            BotGroupSettings group = Group(name, "Patrol", "Hover", Small(), Member("Vex", 30, 30));
            group.MainTurret = "Bore";
            group.UpgradeMin = 0.0F;
            group.UpgradeMax = 0.0F;
            group.DamageScale = 0.3F;
            group.HealthScale = 0.35F;
            return group;
        }

        private static BotGroupSettings Trio(string name, string lead, string wing, string turret)
        {
            BotGroupSettings group = Group(name, "Patrol", "Hover", Medium(), Member(lead, 1, 1), Member(wing, 2, 2));
            group.MainTurret = turret;
            return group;
        }

        private static BotGroupSettings Fleet6(string name, string cruiser, string frigate, string corvette, string turrets, string caps)
        {
            BotGroupSettings group = Group(name, "Patrol", "Hover", Large(),
                Member(cruiser, 1, 1), Member(frigate, 2, 2), Member(corvette, 3, 3));
            group.MainTurret = turrets;
            group.TurretCaps = caps;
            return group;
        }

        private static BotGroupSettings BehemothBase()
        {
            BotGroupSettings group = Group("베히모스기지", "Base", "Hover", Large(), Member("Behemoth", 3, 3));
            group.CenterX = -620;
            group.CenterY = -620;
            group.AvoidSpawn = true;
            group.BaseRadius = 26.0F;
            group.SpawnPoints = new List<BotPoint>
            {
                new BotPoint(-620, -620),
                new BotPoint(-1000, 250),
                new BotPoint(1000, -250),
                new BotPoint(300, -1000),
                new BotPoint(-300, 1000),
            };
            group.HealthScale = 2.5F;
            group.IdleRespawn = 0.0F;
            return group;
        }

        private static BotGroupSettings Avoid(BotGroupSettings group)
        {
            group.AvoidSpawn = true;
            return group;
        }

        private static BotGroupSettings Base()
        {
            BotGroupSettings group = Group("콜로서스기지", "Base", "Hover", Large(), Member("Colossus", 3, 3));
            // 후보 지점은 모두 플레이어 스폰 지역(원점 550) 밖.
            group.CenterX = 620;
            group.CenterY = 620;
            group.AvoidSpawn = true;
            group.SpawnPoints = new List<BotPoint>
            {
                new BotPoint(620, 620),
                new BotPoint(-800, 800),
                new BotPoint(800, -800),
                new BotPoint(-950, -300),
                new BotPoint(950, 350),
            };
            group.HealthScale = 2.5F;

            // 움직이지 않는 기지는 비전투 재편성 대신 격추된 함선만 다시 만듦(BaseRefill).
            group.IdleRespawn = 0.0F;
            return group;
        }

        private static BotGroupSettings Sniper(string name, string ship)
        {
            BotGroupSettings group = Group(name, "Sniper", "Hover", Medium(), Member(ship, 1, 1));
            group.MainTurret = "Keep";
            group.AvoidSpawn = true;
            return group;
        }

        // 돌격 베히모스. 세력 둘이 따로 돎. 전멸하면 1분 30초 뒤 다시 나옴. 자동 포탑 4칸 모두 화방(Pyron).
        private static BotGroupSettings Rammer(string name)
        {
            BotGroupSettings group = Group(name, "Rammer", "Fly", Large(), Member("Behemoth", 1, 1));
            group.MainTurret = "Pyron";
            group.AutoTurrets = "Pyron";
            group.SpeedLike = "Nighthawk";
            group.CraftsRam = true;
            group.RespawnDelay = 90.0F;

            // 함재기 생산(증원, 조립), 체력, 수리, 속력 최대
            group.MaxUpgrades = "Speed|Reinforcement|Assembly|MaxHealth|Repair";
            group.AvoidSpawn = true;
            return group;
        }

        private static BotGroupSettings Line()
        {
            BotGroupSettings group = Group("전열 레비아탄", "Line", "Hover", Large(), Member("Leviathan", 5, 5));
            group.MainTurret = "Keep";
            group.AutoTurrets = "Brakkar";
            group.AvoidSpawn = true;
            return group;
        }

        private static BotGroupSettings Raider()
        {
            BotGroupSettings group = Group("고속나이트호크", "Raider", "Fly", Small(), Member("Nighthawk", 7, 7));
            group.MaxUpgrades = "Speed";
            group.AvoidSpawn = true;
            return group;
        }

        private static BotGroupSettings Thor()
        {
            BotGroupSettings group = Group("폭주 토르", "Patrol", "Hover", Large(), Member("Thor", 1, 1));
            group.MainTurret = "Keep";
            group.ReloadScale = 0.2F;
            group.NoOverheat = true;
            group.AvoidSpawn = true;
            return group;
        }

        private static BotGroupSettings Routed(BotGroupSettings group, List<BotPoint> route)
        {
            group.Route = route;
            return group;
        }

        // 원 위의 점들. ccw 면 반시계 방향.
        private static List<BotPoint> Circle(float x, float y, float radius, int count, bool ccw)
        {
            return Ellipse(x, y, radius, radius, count, ccw);
        }

        private static List<BotPoint> Ellipse(float x, float y, float rx, float ry, int count, bool ccw = false)
        {
            var points = new List<BotPoint>();

            for (int i = 0; i < count; i++)
            {
                float angle = (ccw ? 1.0F : -1.0F) * i * 2.0F * (float)Math.PI / count;
                points.Add(new BotPoint(x + rx * (float)Math.Cos(angle), y + ry * (float)Math.Sin(angle)));
            }

            return points;
        }

        private static List<BotPoint> Rect(float x0, float y0, float x1, float y1)
        {
            return Poly(x0, y0, x1, y0, x1, y1, x0, y1);
        }

        // 꼭짓점 다섯 개를 하나 걸러 잇는 별. 선분이 원점에서 반지름 x 0.31 쯤 떨어져 지나감.
        private static List<BotPoint> Star(float x, float y, float radius)
        {
            var points = new List<BotPoint>();

            for (int i = 0; i < 5; i++)
            {
                float angle = (float)Math.PI * 0.5F + i * 2 * 2.0F * (float)Math.PI / 5;
                points.Add(new BotPoint(x + radius * (float)Math.Cos(angle), y + radius * (float)Math.Sin(angle)));
            }

            return points;
        }

        private static List<BotPoint> Poly(params float[] xy)
        {
            var points = new List<BotPoint>();

            for (int i = 0; i + 1 < xy.Length; i += 2)
            {
                points.Add(new BotPoint(xy[i], xy[i + 1]));
            }

            return points;
        }

        private static BotGroupSettings Group(
            string name, string behavior, string move,
            List<BotDropSettings> drops, params BotMemberSettings[] members)
        {
            return new BotGroupSettings
            {
                Name = name,
                Behavior = behavior,
                Move = move,
                Members = new List<BotMemberSettings>(members),
                Drops = drops,
            };
        }

        private static BotMemberSettings Member(string ships, int min, int max, bool sameKind = false, bool distinct = false)
        {
            return new BotMemberSettings { Ships = ships, Min = min, Max = max, SameKind = sameKind, Distinct = distinct };
        }

        private static List<BotDropSettings> Small()
        {
            return new List<BotDropSettings> { Drop("PowerCell", 1, 3, 1.0F) };
        }

        private static List<BotDropSettings> Medium()
        {
            return new List<BotDropSettings> { Drop("PowerCell", 2, 4, 1.0F), Drop("OverchargedPowerCell", 1, 1, 0.3F) };
        }

        private static List<BotDropSettings> Large()
        {
            return new List<BotDropSettings>
            {
                Drop("PowerCell", 3, 6, 1.0F),
                Drop("OverchargedPowerCell", 1, 2, 0.5F),
                Drop("Reactor", 1, 1, 0.2F),
            };
        }

        private static BotDropSettings Drop(string item, int min, int max, float chance)
        {
            return new BotDropSettings { Item = item, Min = min, Max = max, Chance = chance };
        }
    }
}
