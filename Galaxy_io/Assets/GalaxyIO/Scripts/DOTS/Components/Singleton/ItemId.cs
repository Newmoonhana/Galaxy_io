namespace GalaxyIO.DOTS.Components.Singleton
{
    /// <summary>
    /// 아이템 종류 키. 모든 월드에서 같은 값.
    /// 값이 곧 아이템 레지스트리 버퍼의 인덱스이므로 0부터 빈틈 없이 이어서 붙일 것.
    /// </summary>
    public enum ItemId : ushort
    {
        // 빈 칸. 실제 아이템을 가리키지 않음.
        None = 0,

        // 강화 모듈
        UpgradeModule = 1,

        // 포탑 설계도
        BoreBlueprint = 2,
        BrakkarBlueprint = 3,
        PyronBlueprint = 4,
        RaptorBlueprint = 5,
        VeylanceBlueprint = 6,
        VraxxBlueprint = 7,

        // 포탑
        Bore = 8,
        Brakkar = 9,
        Pyron = 10,
        Raptor = 11,
        Veylance = 12,
        Vraxx = 13,

        // 적 전용 포탑. 드랍 테이블에 넣지 않음.
        CorsairTurret = 14,
    }
}
