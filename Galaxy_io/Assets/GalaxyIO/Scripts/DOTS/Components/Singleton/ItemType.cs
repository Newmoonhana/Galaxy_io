namespace GalaxyIO.DOTS.Components.Singleton
{
    /// <summary>
    /// 아이템 분류. 아이템이 들어갈 수 있는 인벤토리 칸을 정함.
    /// </summary>
    public enum ItemType : byte
    {
        // 재료. 일반 칸.
        Material = 0,

        // 폭탄. 일반 칸, 폭탄 칸.
        Bomb = 1,

        // 완성된 포탑. 일반 칸, 포탑 칸.
        Turret = 2,
    }
}
