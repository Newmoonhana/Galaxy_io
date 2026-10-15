namespace GalaxyIO.DOTS.Components.Singleton
{
    /// <summary>
    /// 런타임에 프리팹을 고르는 키. 모든 월드에서 같은 값.
    /// 값이 곧 레지스트리 버퍼의 인덱스이므로 0부터 빈틈 없이 이어서 붙일 것.
    /// </summary>
    public enum PrefabId : ushort
    {
        // 값을 지정하지 않은 인스펙터 슬롯이 실제 프리팹을 가리키지 않게 하는 기본값.
        None = 0,

        // 플레이어 우주선. 기본 기체는 PlayerSpawnConfigAuthoring 이 정함.
        Vex = 1,

        // 우주선 파괴 폭발 연출
        ExplosionEffect = 2,

        // 5초마다 스폰되는 Strafing AI 적
        EnemyStrafingShip = 3,

        // 월드에 떨어진 아이템. 모든 아이템 종류가 공유함.
        ItemEntity = 4,

        // 폭발 투사체 명중 연출
        ProjectileExplosionEffect = 5,

        // 플레이어 우주선
        Colossus = 6,
    }
}