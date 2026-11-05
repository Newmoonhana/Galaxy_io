using GalaxyIO.DOTS.Components.Singleton;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace GalaxyIO.DOTS.Bots
{
    public enum BotBehavior : byte
    {
        Patrol = 0,
        Base = 1,
        Sniper = 2,
        Rammer = 3,
        Line = 4,
        Raider = 5,
    }

    public enum BotState : byte
    {
        // 진형 자리에 있음. 무리 공동 속력으로 이동.
        Formation = 0,

        // 진형에서 멀어짐. 가속해서 자리로 돌아감.
        Rejoin = 1,

        // 교전 중. 자기 성능대로 움직임.
        Engage = 2,
    }

    /// <summary>
    /// 무리(세력) 하나의 실행 상태. 서버만 가지며 복제하지 않음. 무리마다 Entity 하나.
    /// </summary>
    public struct BotGroupComponent : IComponentData
    {
        // 설정(BotSettings.Groups) 번호
        public int Index;

        public BotBehavior Behavior;

        // 평소 이동이 비행이면 true, 호버링이면 false.
        public bool Fly;

        // 같은 무리끼리 아군이 되는 파티 번호
        public uint Party;

        public float2 Center;
        public float Radius;
        public float Spacing;

        // 살아 있는 구성원 수. 0 이 되면 재생성 대기에 들어감.
        public int Alive;
        public bool Spawned;
        public float RespawnRemaining;

        // 순찰 목표 지점과 진형 방향
        public float2 Waypoint;
        public bool HasWaypoint;
        public float2 Heading;

        // 루트(BotRoutePoint 버퍼)에서 지금 향하는 점의 번호
        public int RouteIndex;

        // 이번 틱의 리더와 그 위치, 속도
        public Entity Leader;
        public float2 LeaderOffset;
        public float2 LeaderPosition;
        public float2 LeaderVelocity;

        // 무리 공동 속력. 가장 느린 구성원 기준(호버링 또는 비행).
        public float CruiseSpeed;

        // 전열(Line) 무리가 함께 노리는 대상
        public Entity Target;

        // 진형에서 크게 벗어난 구성원이 있음. 리더가 기다림.
        public bool Waiting;

        // 마지막으로 전투(공격하거나 공격받음)한 시각(ElapsedTime). 오래 비면 재편성함.
        public double LastCombatTime;

        // 함급별로 격추된 함선을 마지막으로 다시 만든 시각(초계함, 호위함, 순양함)
        public double LastCorvetteRefill;
        public double LastFrigateRefill;
        public double LastCruiserRefill;

        // 반격 대상과 끝나는 시각. 구성원이 맞으면 무리 전체가 이 대상을 쫓음.
        public Entity RetaliateTarget;
        public double RetaliateUntil;

        // 초계함이 잡아(락온) 무리에 공유한 적과 공유가 유효한 시각.
        public Entity SharedTarget;
        public double SharedUntil;
    }

    /// <summary>
    /// 무리 구성원 한 척. 서버만 가짐.
    /// </summary>
    public struct BotComponent : IComponentData
    {
        public Entity Group;
        public BotBehavior Behavior;

        // 진형 번호. 0 이 리더 후보.
        public int Slot;

        // 리더 기준 진형 자리 (x = 오른쪽, y = 앞)
        public float2 Offset;

        public BotState State;
        public Entity Target;
        public float RetargetRemaining;

        // 과열 방지. 열이 일정 이상이면 식을 때까지 쏘지 않음.
        public bool Cooling;

        // 교전 중 옆으로 도는 방향(+1, -1)
        public float StrafeSign;

        // 조종 포탑 사거리와 탄속(앞질러 쏘기)
        public float WeaponRange;
        public float ProjectileSpeed;

        // 기지: 지킬 자리와 기수 방향
        public float2 Home;
        public float2 HomeHeading;

        public bool CraftsRam;

        // 박치기 함재기의 최고 속력, 가속도(SpeedLike 기종의 최대 강화값). 0 이면 원래 값.
        public float CraftSpeed;
        public float CraftAcceleration;

        // 지난 틱 방아쇠. 누르기 시작한 틱을 알리는 데 씀.
        public bool LastTrigger;

        // 격추 드랍을 이미 떨어뜨림.
        public bool Dropped;

        // 지난 틱 체력. 줄었으면 공격받은 것.
        public float LastHealth;

        // 조준 실수. 새 대상을 잡으면 이 시간(초)만큼 늦게 쏘기 시작함.
        public float ReactRemaining;

        // 앞질러 겨누는 비율(1 = 정확)과 조준 흔들림 각도(라디안). 대상을 다시 고를 때마다 새로 굴림.
        public float AimLead;
        public float AimOffset;

        // 지난 틱 대상. 바뀌었으면 반응 지연을 다시 시작함.
        public Entity LastTarget;
    }

    /// <summary>
    /// 무리 격추 드랍 한 줄. 무리 Entity 에 붙음.
    /// </summary>
    public struct BotDropElement : IBufferElementData
    {
        public ItemId Item;
        public int Min;
        public int Max;
        public float Chance;
    }

    /// <summary>
    /// 무리 순찰 루트의 점. 무리 Entity 에 붙음. 차례로 돌고 끝에서 처음으로 이어짐.
    /// </summary>
    public struct BotRoutePoint : IBufferElementData
    {
        public float2 Value;
    }

    /// <summary>
    /// 이번에 만든 무리 구성원 기종. 진형 번호 순서. 기지의 격추된 함선을 다시 만들 때 씀.
    /// </summary>
    public struct BotMemberElement : IBufferElementData
    {
        public SpaceshipId Ship;
    }

    /// <summary>
    /// 자유 모드 봇 서버의 피해 배율 표. Burst 잡(ProjectileDamageSystem, SpaceshipCollisionSystem)이 읽으므로 SharedStatic 에 둠.
    /// 봇 디렉터가 채움. 비어 있으면(봇이 꺼진 서버, 클라이언트) 모든 배율이 1.
    /// </summary>
    public struct BotDamageTable
    {
        public bool Enabled;

        // 봇 투사체 전체 배율, 플레이어 투사체 배율, AI 적(EnemyName) 투사체 배율
        public float Bot;
        public float Player;
        public float Enemy;

        // 무리별 투사체 배율(무리 번호 순서)
        public FixedList512Bytes<float> Groups;

        // 박치기 무리(무리 번호 비트). 모선이 부딪힌 피해 x Ram, 함재기가 부딪힌 피해 x CraftRam.
        public ulong RamGroups;
        public float Ram;
        public float CraftRam;
    }

    /// <summary>
    /// 봇 디렉터가 시작할 때 정해 두는 값. 조종(BotPilotSystem)이 읽음. 둘 다 서버 관리 시스템.
    /// </summary>
    public static class BotRuntime
    {
        // 플레이어 발견 거리 상한(초계함 화면 시야 x PlayerSightScale). 0 이면 상한 없음.
        public static float PlayerSight;
    }

    public static class BotDamage
    {
        private sealed class TableKey
        {
        }

        public static readonly Unity.Burst.SharedStatic<BotDamageTable> Table = Unity.Burst.SharedStatic<BotDamageTable>.GetOrCreate<TableKey>();

        private static int GroupIndex(uint party)
        {
            return (int)(party & 0x0FFFFFFFu) - 1;
        }

        /// <summary>
        /// 투사체 피해 배율. 쏜 쪽의 파티, 쏜 쪽이 플레이어인지, AI 적인지로 정함.
        /// </summary>
        public static float For(uint attackerParty, bool player, bool enemy)
        {
            ref BotDamageTable table = ref Table.Data;

            if (!table.Enabled)
            {
                return 1.0F;
            }

            if (BotRules.IsBotParty(attackerParty))
            {
                int index = GroupIndex(attackerParty);
                float group = index >= 0 && index < table.Groups.Length ? table.Groups[index] : 1.0F;
                return table.Bot * group;
            }

            if (enemy)
            {
                return table.Enemy;
            }

            return player ? table.Player : 1.0F;
        }

        /// <summary>
        /// 박치기 피해 배율. 부딪힌 상대가 박치기 무리의 모선이나 함재기면 커짐.
        /// </summary>
        public static float RamFor(uint otherParty, bool otherIsCraft)
        {
            ref BotDamageTable table = ref Table.Data;

            if (!table.Enabled || !BotRules.IsBotParty(otherParty))
            {
                return 1.0F;
            }

            int index = GroupIndex(otherParty);

            if (index < 0 || index >= 64 || (table.RamGroups & (1UL << index)) == 0)
            {
                return 1.0F;
            }

            return otherIsCraft ? table.CraftRam : table.Ram;
        }
    }

    /// <summary>
    /// 봇 규칙 상수.
    /// </summary>
    public static class BotRules
    {
        // 봇 파티 번호. 일반 파티(1부터), 팀(0x40000000), 솔로(0x80000000) 번호와 겹치지 않음.
        public static uint Party(int groupIndex)
        {
            return 0x20000000u | (uint)(groupIndex + 1);
        }

        /// <summary>
        /// 봇 무리 파티인지. 팀(0x40000000), 솔로(0x80000000) 비트가 없고 봇 비트가 있음.
        /// </summary>
        public static bool IsBotParty(uint party)
        {
            return (party & 0xE0000000u) == 0x20000000u;
        }

        public static float2 Rotate(float2 local, float2 forward)
        {
            float2 right = new float2(forward.y, -forward.x);
            return right * local.x + forward * local.y;
        }
    }
}
