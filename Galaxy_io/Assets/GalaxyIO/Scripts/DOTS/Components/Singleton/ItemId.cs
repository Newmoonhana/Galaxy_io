namespace GalaxyIO.DOTS.Components.Singleton
{
    /// <summary>
    /// 아이템 종류 키. 모든 월드에서 같은 값.
    /// 값이 곧 아이템 레지스트리 버퍼의 인덱스이므로 0부터 빈틈 없이 이어서 붙일 것.
    /// </summary>
    public enum ItemId : ushort
    {
        // 아무 아이템도 가리키지 않음.
        None = 0,

        // 재료(화폐). 플레이어 인벤토리에 개수로 쌓임.
        PowerCell = 1,
        OverchargedPowerCell = 2,
        Reactor = 3,

        // 포탑. 포탑 상점에서 재료로 사서 장착함.
        Bore = 4,
        Brakkar = 5,
        Pyron = 6,
        Raptor = 7,
        Veylance = 8,
        Vraxx = 9,

        // 적 전용 포탑. 드랍 테이블, 상점에 넣지 않음.
        CorsairTurret = 10,

        // 우주선 고정 포탑(함포). 드랍 테이블, 상점에 넣지 않음.
        LeviathanArtillery = 11,
        PaladinArtillery = 12,
    }
}
