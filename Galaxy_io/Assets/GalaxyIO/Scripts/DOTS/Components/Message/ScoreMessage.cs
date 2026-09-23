using Unity.Entities;

namespace GalaxyIO.DOTS.Components.Message
{
    public enum ScoreOperation : byte
    {
        // 현재 점수에 더함
        Add = 0,

        // 현재 점수를 덮어씀
        Set = 1,
    }

    /// <summary>
    /// 플레이어 점수 변경 요청. PlayerScoreSystem 이 읽고 제거함. 서버 전용.
    /// </summary>
    public struct ScoreMessage : IComponentData
    {
        public int NetworkId;

        public ScoreOperation Operation;

        public int Value;

        public static ScoreMessage Add(int networkId, int amount)
        {
            return new ScoreMessage
            {
                NetworkId = networkId,
                Operation = ScoreOperation.Add,
                Value     = amount,
            };
        }

        public static ScoreMessage Set(int networkId, int score)
        {
            return new ScoreMessage
            {
                NetworkId = networkId,
                Operation = ScoreOperation.Set,
                Value     = score,
            };
        }
    }
}
