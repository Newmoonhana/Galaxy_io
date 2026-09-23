using Unity.Entities;

namespace GalaxyIO.DOTS.Components.Actor
{
    /// <summary>
    /// 플레이어 우주선이 다른 플레이어에게 받은 데미지 기록. 킬 로그의 막타/어시스트 판정에 씀.
    /// 서버만 채움.
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct DamageRecordElement : IBufferElementData
    {
        // 데미지를 입힌 플레이어
        public int NetworkId;

        // 누적 데미지
        public float Amount;

        // 마지막으로 때린 시각(ElapsedTime)
        public double LastHitTime;
    }

    /// <summary>
    /// <see cref="DamageRecordElement"/> 기록 규칙.
    /// </summary>
    public static class DamageRecord
    {
        // 기록 유지 시간(초). 마지막 피격 후 이 시간이 지나면 버림.
        public const double Duration = 10.0;

        public static bool IsAlive(in DamageRecordElement record, double time)
        {
            return time - record.LastHitTime <= Duration;
        }

        /// <summary>
        /// 데미지를 기록함. 같은 플레이어면 누적하고 유지 시간을 다시 채움.
        /// </summary>
        public static void Add(DynamicBuffer<DamageRecordElement> records, int networkId, float amount, double time)
        {
            // 만료된 기록 정리
            for (int i = records.Length - 1; i >= 0; i--)
            {
                if (!IsAlive(records[i], time))
                {
                    records.RemoveAtSwapBack(i);
                }
            }

            for (int i = 0; i < records.Length; i++)
            {
                if (records[i].NetworkId != networkId)
                {
                    continue;
                }

                DamageRecordElement record = records[i];
                record.Amount += amount;
                record.LastHitTime = time;
                records[i] = record;

                return;
            }

            records.Add(new DamageRecordElement
            {
                NetworkId   = networkId,
                Amount      = amount,
                LastHitTime = time,
            });
        }
    }
}
