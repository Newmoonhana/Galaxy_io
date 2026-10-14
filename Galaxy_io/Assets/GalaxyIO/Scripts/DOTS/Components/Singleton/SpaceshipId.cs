namespace GalaxyIO.DOTS.Components.Singleton
{
    /// <summary>
    /// 플레이어 우주선 기종 키. 모든 월드에서 같은 값.
    /// 값이 곧 우주선 레지스트리 버퍼의 인덱스이므로 0부터 빈틈 없이 이어서 붙일 것.
    /// </summary>
    public enum SpaceshipId : byte
    {
        // 상점에서 살 수 없는 기체(적 등)
        None = 0,

        // 초계함
        Vex = 1,
        Vulture = 2,
        Nighthawk = 3,

        // 호위함
        Vanguard = 4,
        Sentinel = 5,
        Halberd = 6,

        // 순양함
        Leviathan = 7,
        Paladin = 8,

        // 모선
        Behemoth = 9,
        Sovereign = 10,
    }
}
