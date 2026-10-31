namespace GalaxyIO.DOTS.Components.Singleton
{
    /// <summary>
    /// 드랍 테이블을 가진 적 종류 키. 서버 설정의 드랍 테이블도 이 이름으로 찾음.
    /// 값이 곧 드랍 테이블 레지스트리 버퍼의 인덱스이므로 0부터 빈틈 없이 이어서 붙일 것.
    /// </summary>
    public enum EnemyType : byte
    {
        // 아무 적도 가리키지 않음.
        None = 0,

        Corsair = 1,
    }
}
