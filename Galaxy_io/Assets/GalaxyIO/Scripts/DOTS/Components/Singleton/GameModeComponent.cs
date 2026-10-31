using GalaxyIO.DOTS.Authoring.Config;
using Unity.Entities;
using Unity.Mathematics;

namespace GalaxyIO.DOTS.Components.Singleton
{
    /// <summary>
    /// 게임 모드. 모드마다 씬이 따로 있음.
    /// </summary>
    public enum GameMode : byte
    {
        // 팀 없이 자유롭게 싸움. 모드 설정이 없는 씬.
        Free = 0,

        // 대장선 잡기
        Regicide = 1,

        // 점령전
        Conquest = 2,
    }

    public enum Team : byte
    {
        None = 0,
        Red = 1,
        Blue = 2,
    }

    public static class Teams
    {
        // 팀 파티 번호. 일반 파티 번호(1부터 차례로 붙임), 혼자만의 번호(최상위 비트)와 겹치지 않음.
        private const uint PartyBit = 0x40000000u;

        public static uint Party(Team team)
        {
            return PartyBit | (uint)team;
        }

        /// <summary>
        /// 팀 파티 번호의 팀. 팀 파티가 아니면 None.
        /// </summary>
        public static Team FromParty(uint party)
        {
            return party == Party(Team.Red) ? Team.Red : party == Party(Team.Blue) ? Team.Blue : Team.None;
        }

        /// <summary>
        /// 대장선인지. 주인이 없는(-1) 팀 소속 모선. 클라이언트도 복제된 값으로 알 수 있음.
        /// </summary>
        public static bool IsKing(int ownerNetworkId, uint party, bool carrier)
        {
            return ownerNetworkId < 0 && carrier && FromParty(party) != Team.None;
        }

        public static Team Other(Team team)
        {
            return team == Team.Red ? Team.Blue : team == Team.Blue ? Team.Red : Team.None;
        }
    }

    /// <summary>
    /// <see cref="GameModeAuthoring"/>의 DOTS 구조체. 모드 씬에만 있음. 없으면 <see cref="GameMode.Free"/>.
    /// </summary>
    public struct GameModeComponent : IComponentData
    {
        public GameMode Mode;

        // 전투 구역 크기. 원점 중심.
        public float2 MapSize;

        // 두 팀을 합친 최대 인원
        public int MaxPlayers;

        // 전투 구역 밖에 이 시간(초)보다 오래 있으면 격추됨.
        public float OutOfZoneTime;

        // 승리 후 다음 판을 시작하기까지의 시간(초)
        public float RestartDelay;

        // 팀별 부활 지점과 반경. 대장선이 있으면 대장선 근처에서 부활함.
        public float2 RedSpawn;
        public float2 BlueSpawn;
        public float SpawnRadius;

        // 이 점수에 먼저 닿는 팀이 이김. 0 이면 점수로 이기지 않음.
        public int WinScore;

        /// <summary>
        /// 위치가 전투 구역 안인지.
        /// </summary>
        public bool Contains(float2 position)
        {
            return math.all(math.abs(position) <= MapSize * 0.5F);
        }
    }

    /// <summary>
    /// 분류별 팀당 최대 기체 수와 부활 대기 시간. <see cref="GameModeComponent"/>와 같은 Entity.
    /// 없는 분류는 고를 수 없음.
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct ShipLimitElement : IBufferElementData
    {
        public SpaceshipCategory Category;

        // 팀당 최대 수
        public int Max;

        // 이 분류로 격추된 플레이어가 다시 나오기까지의 시간(초)
        public float PlayerDelay;

        // 격추된 기체 자리가 비기까지의 시간(초). 0 이면 바로 빔.
        public float ShipDelay;
    }

    public static class ShipLimit
    {
        /// <summary>
        /// 분류의 제한. 없으면 false.
        /// </summary>
        public static bool TryGet(this DynamicBuffer<ShipLimitElement> limits, SpaceshipCategory category, out ShipLimitElement limit)
        {
            foreach (ShipLimitElement element in limits)
            {
                if (element.Category == category)
                {
                    limit = element;
                    return true;
                }
            }

            limit = default;
            return false;
        }
    }

    /// <summary>
    /// 대장선 잡기의 대장선 설정. 대장선 잡기 씬에만 있음.
    /// </summary>
    public struct KingCarrierConfigComponent : IComponentData
    {
        // 팀별 대장선 기종
        public SpaceshipId RedKing;
        public SpaceshipId BlueKing;

        public float MaxHealth;
        public float Repair;

        // 자동 포탑 칸을 모두 채우는 포탑
        public ItemId Turret;

        // 적 대장선을 직접 찾는 거리
        public float DetectRange;

        // 처음 두 대장선 사이의 최소 거리
        public float MinDistance;

        // 정찰 지점까지의 거리 범위 (최소, 최대)
        public float2 ScoutDistance;

        // 정찰대가 정찰 지점을 둘러싼 자리에 이만큼 다가가면 돌아옴.
        public float ScoutArrival;

        // 편대를 내보내는 간격(초)
        public float LaunchCooldown;

        // 편대당 함재기 수, 최대 편대 수
        public int SquadSize;
        public int MaxSquads;

        // 편대 하나의 작전 시간(초). 지나면 돌아옴.
        public float SquadTime;

        // 탐색 중 한 방향으로 나는 시간 범위(초) (최소, 최대)
        public float2 CruiseTime;

        // 적 대장선을 쫓을 때 유지하는 거리
        public float ChaseDistance;

        // 피격 후 이 시간(초) 동안 쫓기는 중으로 봄.
        public float ChasedTime;
    }

    /// <summary>
    /// 점령전 설정. 점령전 씬에만 있음.
    /// </summary>
    public struct ConquestConfigComponent : IComponentData
    {
        public Entity SatellitePrefab;

        // 위성 자동 포탑
        public ItemId SatelliteTurret;

        // 점령 반경
        public float CaptureRadius;

        // 한 명이 초당 올리는 점령도. 점령도 100 이면 점령.
        public float CaptureRate;

        // 점령도를 함께 올릴 수 있는 최대 인원
        public int MaxCapturers;

        // 점령한 위성이 1 점씩 내는 간격(초)
        public float ScoreInterval;

        // 위성 포탑 체력
        public float TurretHealth;

        // 부서진 위성 포탑이 다시 켜지기까지의 시간(초)
        public float TurretReviveTime;

        // 점령도를 올리던 팀이 이 시간(초) 동안 점령 범위에 없으면 점령도가 0 이 됨.
        public float ProgressResetTime;

        // 클라이언트만 씀. 점령 범위 원 머티리얼(Sprite-OccupyArea)
        public UnityObjectRef<UnityEngine.Material> AreaMaterial;
    }

    /// <summary>
    /// 점령전 위성 배치. <see cref="ConquestConfigComponent"/>와 같은 Entity.
    /// </summary>
    [InternalBufferCapacity(5)]
    public struct SatelliteSpawnElement : IBufferElementData
    {
        public float2 Position;
    }
}
