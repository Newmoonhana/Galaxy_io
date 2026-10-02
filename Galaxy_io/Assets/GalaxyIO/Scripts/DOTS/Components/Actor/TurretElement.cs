using GalaxyIO.DOTS.Components.Singleton;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using Unity.Transforms;
using Unity.Mathematics;
using Random = Unity.Mathematics.Random;

namespace GalaxyIO.DOTS.Components.Actor
{
    /// <summary>
    /// 우주선 총구 하나. 사양(발사체, 발사 간격, 탄퍼짐, 발사음)은
    /// <see cref="EquippedTurretComponent"/>의 포탑에서 총구 번호로 찾음.
    /// 쿨다운은 같은 번호의 <see cref="TurretCooldownElement"/>에 있음.
    /// </summary>
    public struct TurretElement : IBufferElementData
    {
        // 발사체 발사 위치 (로컬)
        public LocalTransform MuzzleLocation;
    }

    /// <summary>
    /// 총구별 현재 발사 쿨다운. <see cref="TurretElement"/>와 같은 순서.
    /// 주인에게만 복제함. 예측 롤백 시 서버 값으로 되감아 발사 틱을 서버와 맞춤.
    /// </summary>
    [GhostComponent(OwnerSendType = SendToOwnerType.SendToOwner)]
    public struct TurretCooldownElement : IBufferElementData
    {
        // 발사 틱이 어긋나지 않도록 양자화하지 않음.
        [GhostField(Quantization = 0)]
        public float Value;
    }

    /// <summary>
    /// 이번 틱에 쏠 한 발. 한 틱에 같은 총구가 여러 번 쏘면 Shot 이 0, 1, 2 ... 로 늘어남.
    /// </summary>
    public struct TurretShotRequest
    {
        public int Muzzle;
        public uint Shot;
    }

    /// <summary>
    /// 총구 쿨다운 진행 규칙. 서버/예측 발사와 클라이언트 연출 발사가 같이 씀.
    /// 혼자 쏘는 총구는 RPS 대로 쏘고, 따라 쏘는 총구는 앞 총구가 쏠 때마다 FollowDelay 뒤에 한 번 쏨(방아쇠를 떼도 쏨).
    /// 따라 쏘는 총구는 자기 타이머가 없어 앞 총구와의 간격이 어긋나지 않음.
    /// </summary>
    public static class TurretCycle
    {
        // 따라 쏘는 총구가 앞 총구를 기다리는 중. 줄어들지 않음.
        public const float Waiting = float.PositiveInfinity;

        /// <summary>
        /// 쿨다운을 한 틱 진행하고 이번 틱에 쏠 발을 shots 에 담음.
        /// </summary>
        public static void Advance(
            NativeArray<float> cooldowns,
            DynamicBuffer<ItemRegistryElement> registry,
            DynamicBuffer<TurretSpecElement> specs,
            ItemId turret,
            bool firing,
            float deltaTime,
            ref NativeList<TurretShotRequest> shots)
        {
            // 따라 쏘는 총구 먼저. 이번 틱에 앞 총구가 건 대기는 다음 틱부터 줄어듦.
            for (int i = 0; i < cooldowns.Length; i++)
            {
                TurretSpecElement spec = registry.GetTurretSpec(specs, turret, i);

                if (spec.ProjectilePrefab == Entity.Null || spec.FollowMuzzle < 0)
                {
                    continue;
                }

                // 앞 총구가 쏜 뒤 걸린 따라 쏘기는 방아쇠를 떼도 마저 쏨.
                cooldowns[i] -= deltaTime;

                if (cooldowns[i] <= 0.0F)
                {
                    shots.Add(new TurretShotRequest { Muzzle = i });
                    cooldowns[i] = Waiting;
                }
            }

            for (int i = 0; i < cooldowns.Length; i++)
            {
                TurretSpecElement spec = registry.GetTurretSpec(specs, turret, i);

                // 발사 간격이 없는 총구(RPS 0)는 혼자 쏠 수 없음.
                if (spec.ProjectilePrefab == Entity.Null || spec.FollowMuzzle >= 0 || spec.MaxFireCooldown <= 0.0F)
                {
                    continue;
                }

                cooldowns[i] -= deltaTime;

                // 쏘지 않는 동안은 0 아래로 쌓아 두지 않음. 다시 쏠 때 몰아 쏘는 것 방지.
                if (!firing)
                {
                    cooldowns[i] = math.max(cooldowns[i], 0.0F);
                    continue;
                }

                // 발사 간격이 한 틱보다 짧으면 한 틱에 여러 번 쏨.
                for (uint shot = 0; cooldowns[i] <= 0.0F; shot++)
                {
                    // 0 아래로 내려간 만큼이 틱 안에서 실제로 쏜 뒤 지난 시간. 따라 쏘는 총구 대기에서 뺌.
                    float late = cooldowns[i];

                    cooldowns[i] += spec.MaxFireCooldown;
                    shots.Add(new TurretShotRequest { Muzzle = i, Shot = shot });

                    for (int f = 0; f < cooldowns.Length; f++)
                    {
                        TurretSpecElement follower = registry.GetTurretSpec(specs, turret, f);

                        if (follower.FollowMuzzle == i && follower.ProjectilePrefab != Entity.Null)
                        {
                            cooldowns[f] = follower.FollowDelay + late;
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// 한 발의 탄퍼짐, 속력 분포 규칙. 플레이어, AI 발사 시스템이 같이 씀.
    /// </summary>
    public static class TurretShot
    {
        /// <summary>
        /// 총구 방향을 탄퍼짐만큼 돌리고 속력 배율을 정함. 같은 시드면 클라이언트와 서버가 같은 값을 냄.
        /// </summary>
        public static LocalTransform Aim(LocalTransform muzzle, in TurretSpecElement spec, uint seed, out float speedScale)
        {
            var random = Random.CreateFromIndex(seed & 0x7FFFFFFFu);
            float halfSpread = spec.Spread * 0.5F;

            speedScale = 1.0F + random.NextFloat(-spec.SpeedSpread, spec.SpeedSpread);

            return halfSpread > 0.0F ? muzzle.RotateZ(random.NextFloat(-halfSpread, halfSpread)) : muzzle;
        }
    }
}
