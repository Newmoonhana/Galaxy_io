using System;
using System.Collections.Generic;
using GalaxyIO.DOTS.Components.Singleton;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace GalaxyIO.DOTS.Authoring.Config
{
    /// <summary>
    /// 게임 모드 설정. 모드 씬의 SubScene 에 하나만 둠. 없으면 자유 모드.
    /// </summary>
    public sealed class GameModeAuthoring : MonoBehaviour
    {
        [Serializable]
        public struct ShipLimit
        {
            public SpaceshipCategory Category;

            [Tooltip("팀당 최대 수")]
            [Min(0)]
            public int Max;

            [Tooltip("이 분류로 격추된 플레이어가 다시 나오기까지의 시간(초)")]
            [Min(0.0F)]
            public float PlayerDelay;

            [Tooltip("격추된 기체 자리가 비기까지의 시간(초). 0 이면 바로 빔.")]
            [Min(0.0F)]
            public float ShipDelay;
        }

        public GameMode Mode = GameMode.Regicide;

        [Header("Rules")]

        [Tooltip("전투 구역 크기. 원점 중심.")]
        public Vector2 MapSize = new Vector2(5000.0F, 5000.0F);

        [Tooltip("두 팀을 합친 최대 인원")]
        [Min(2)]
        public int MaxPlayers = 64;

        [Tooltip("전투 구역 밖에 이 시간(초)보다 오래 있으면 격추됨.")]
        [Min(0.0F)]
        public float OutOfZoneTime = 10.0F;

        [Tooltip("승리 후 다음 판을 시작하기까지의 시간(초)")]
        [Min(0.0F)]
        public float RestartDelay = 50.0F;

        [Tooltip("분류별 팀당 최대 수와 부활 대기 시간. 없는 분류는 고를 수 없음.")]
        public List<ShipLimit> ShipLimits = new List<ShipLimit>();

        [Header("Spawn")]

        [Tooltip("팀별 부활 지점. 대장선이 있으면 대장선 근처에서 부활함.")]
        public Vector2 RedSpawn = new Vector2(0.0F, 900.0F);

        public Vector2 BlueSpawn = new Vector2(0.0F, -900.0F);

        [Tooltip("부활 지점 반경")]
        [Min(0.0F)]
        public float SpawnRadius = 40.0F;

        [Header("Regicide")]

        public SpaceshipId RedKing = SpaceshipId.Behemoth;
        public SpaceshipId BlueKing = SpaceshipId.Colossus;

        [Min(1.0F)]
        public float KingMaxHealth = 50000.0F;

        [Min(0.0F)]
        public float KingRepair = 0.5F;

        [Tooltip("대장선 자동 포탑 칸을 모두 채우는 포탑")]
        public TurretItemAsset KingTurret;

        [Tooltip("적 대장선을 직접 찾는 거리")]
        [Min(0.0F)]
        public float KingDetectRange = 100.0F;

        [Tooltip("처음 두 대장선 사이의 최소 거리")]
        [Min(0.0F)]
        public float KingMinDistance = 2000.0F;

        [Tooltip("정찰 지점까지의 거리 범위 (최소, 최대)")]
        public Vector2 ScoutDistance = new Vector2(280.0F, 320.0F);

        [Tooltip("정찰대가 정찰 지점을 둘러싼 자리에 이만큼 다가가면 돌아옴.")]
        [Min(0.0F)]
        public float ScoutArrival = 30.0F;

        [Tooltip("편대를 내보내는 간격(초)")]
        [Min(0.0F)]
        public float LaunchCooldown = 10.0F;

        [Min(1)]
        public int SquadSize = 5;

        [Min(1)]
        public int MaxSquads = 5;

        [Tooltip("편대 하나의 작전 시간(초). 지나면 돌아옴.")]
        [Min(1.0F)]
        public float SquadTime = 60.0F;

        [Tooltip("탐색 중 한 방향으로 나는 시간 범위(초) (최소, 최대)")]
        public Vector2 CruiseTime = new Vector2(14.0F, 20.0F);

        [Tooltip("적 대장선을 쫓을 때 유지하는 거리")]
        [Min(0.0F)]
        public float ChaseDistance = 300.0F;

        [Tooltip("피격 후 이 시간(초) 동안 쫓기는 중으로 봄.")]
        [Min(0.0F)]
        public float ChasedTime = 5.0F;

        [Header("Conquest")]

        [Tooltip("이 점수에 먼저 닿는 팀이 이김. 0 이면 점수로 이기지 않음.")]
        [Min(0)]
        public int WinScore = 500;

        [Tooltip("위성 프리팹. SatelliteAuthoring 을 가진 Ghost.")]
        public GameObject SatellitePrefab;

        [Tooltip("위성 자동 포탑")]
        public TurretItemAsset SatelliteTurret;

        [Tooltip("위성 배치. A, B, C, ... 순서.")]
        public List<Vector2> Satellites = new List<Vector2>();

        [Min(0.0F)]
        public float CaptureRadius = 15.0F;

        [Tooltip("한 명이 초당 올리는 점령도. 점령도 100 이면 점령.")]
        [Min(0.0F)]
        public float CaptureRate = 3.0F;

        [Tooltip("점령도를 함께 올릴 수 있는 최대 인원")]
        [Min(1)]
        public int MaxCapturers = 4;

        [Tooltip("점령한 위성이 1 점씩 내는 간격(초)")]
        [Min(0.1F)]
        public float ScoreInterval = 5.0F;

        [Min(1.0F)]
        public float TurretHealth = 300.0F;

        [Tooltip("부서진 위성 포탑이 다시 켜지기까지의 시간(초)")]
        [Min(0.0F)]
        public float TurretReviveTime = 15.0F;

        [Tooltip("점령도를 올리던 팀이 이 시간(초) 동안 점령 범위에 없으면 점령도가 0 이 됨.")]
        [Min(0.0F)]
        public float ProgressResetTime = 6.0F;

        [Tooltip("점령 범위 원 머티리얼. Sprite-OccupyArea 셰이더.")]
        public Material OccupyAreaMaterial;
    }

    public sealed class GameModeBaker : Baker<GameModeAuthoring>
    {
        public override void Bake(GameModeAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.None);

            AddComponent(entity, new GameModeComponent
            {
                Mode          = authoring.Mode,
                MapSize       = authoring.MapSize,
                MaxPlayers    = authoring.MaxPlayers,
                OutOfZoneTime = authoring.OutOfZoneTime,
                RestartDelay  = authoring.RestartDelay,
                RedSpawn      = authoring.RedSpawn,
                BlueSpawn     = authoring.BlueSpawn,
                SpawnRadius   = authoring.SpawnRadius,
                WinScore      = authoring.Mode == GameMode.Conquest ? authoring.WinScore : 0,
            });

            DynamicBuffer<ShipLimitElement> limits = AddBuffer<ShipLimitElement>(entity);

            foreach (GameModeAuthoring.ShipLimit limit in authoring.ShipLimits)
            {
                limits.Add(new ShipLimitElement
                {
                    Category    = limit.Category,
                    Max         = limit.Max,
                    PlayerDelay = limit.PlayerDelay,
                    ShipDelay   = limit.ShipDelay,
                });
            }

            if (authoring.Mode == GameMode.Regicide)
            {
                DependsOn(authoring.KingTurret);

                AddComponent(entity, new KingCarrierConfigComponent
                {
                    RedKing               = authoring.RedKing,
                    BlueKing              = authoring.BlueKing,
                    MaxHealth             = authoring.KingMaxHealth,
                    Repair                = authoring.KingRepair,
                    Turret                = authoring.KingTurret != null ? authoring.KingTurret.Id : ItemId.None,
                    DetectRange           = authoring.KingDetectRange,
                    MinDistance           = authoring.KingMinDistance,
                    ScoutDistance         = Sort(authoring.ScoutDistance),
                    ScoutArrival          = authoring.ScoutArrival,
                    LaunchCooldown        = authoring.LaunchCooldown,
                    SquadSize             = authoring.SquadSize,
                    MaxSquads             = authoring.MaxSquads,
                    SquadTime             = authoring.SquadTime,
                    CruiseTime            = Sort(authoring.CruiseTime),
                    ChaseDistance         = authoring.ChaseDistance,
                    ChasedTime            = authoring.ChasedTime,
                });
            }

            if (authoring.Mode == GameMode.Conquest)
            {
                DependsOn(authoring.SatelliteTurret);
                DependsOn(authoring.OccupyAreaMaterial);

                if (authoring.SatellitePrefab == null)
                {
                    Debug.LogWarning("GameMode: 위성 프리팹이 비어 있음.", authoring);
                }

                AddComponent(entity, new ConquestConfigComponent
                {
                    SatellitePrefab = authoring.SatellitePrefab != null
                        ? GetEntity(authoring.SatellitePrefab, TransformUsageFlags.Dynamic)
                        : Entity.Null,
                    SatelliteTurret  = authoring.SatelliteTurret != null ? authoring.SatelliteTurret.Id : ItemId.None,
                    CaptureRadius    = authoring.CaptureRadius,
                    CaptureRate      = authoring.CaptureRate,
                    MaxCapturers     = authoring.MaxCapturers,
                    ScoreInterval    = authoring.ScoreInterval,
                    TurretHealth     = authoring.TurretHealth,
                    TurretReviveTime = authoring.TurretReviveTime,
                    ProgressResetTime = authoring.ProgressResetTime,
                    AreaMaterial     = authoring.OccupyAreaMaterial,
                });

                DynamicBuffer<SatelliteSpawnElement> satellites = AddBuffer<SatelliteSpawnElement>(entity);

                foreach (Vector2 position in authoring.Satellites)
                {
                    satellites.Add(new SatelliteSpawnElement { Position = position });
                }
            }
        }

        // (최소, 최대) 순서로 맞춤.
        private static float2 Sort(Vector2 range)
        {
            return new float2(math.min(range.x, range.y), math.max(range.x, range.y));
        }
    }
}