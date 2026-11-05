using UnityEngine;

namespace OOPSample
{
    /// <summary>
    /// "이번 프레임에 봇 X가 플레이어와 겹쳤다"는 메시지.
    /// <see cref="BotBehaviour"/>가 생산하고 <c>BotDespawner</c>와 <c>ScoreKeeper</c>가 소비합니다.
    ///
    /// ECS 버전의 <c>BotHitPlayerEvent</c>가 <c>Entity</c> 핸들을 담았던 자리에 여기서는 직접
    /// 참조가 들어갑니다. 그래서 컨슈머는 <c>EntityManager.Exists</c> 대신
    /// <see cref="BotBehaviour.IsAlive"/>로 유효성을 확인해야 합니다. 메시지는 스냅샷이고,
    /// 스냅샷은 낡을 수 있습니다.
    /// </summary>
    public readonly struct BotHitPlayerEvent
    {
        public readonly BotBehaviour Bot;
        public readonly PlayerBehaviour Player;
        public readonly Vector2 Position;

        public BotHitPlayerEvent(BotBehaviour bot, PlayerBehaviour player, Vector2 position)
        {
            Bot = bot;
            Player = player;
            Position = position;
        }
    }
}
