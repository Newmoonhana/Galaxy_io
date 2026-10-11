using Unity.Entities;

namespace GalaxyIO.DOTS.Client.Components.Singleton
{
    /// <summary>
    /// 로컬 플레이어의 클라이언트 상태. 클라이언트 월드당 한 개. 복제되지 않음.
    /// </summary>
    public struct PlayerStateComponent : IComponentData
    {
        // 게임 메뉴가 열려 있음. 열린 동안 우주선 조작을 막음.
        public bool GameMenuOpened;

        // 인벤토리가 열려 있음. 열린 동안 우주선 조작을 막음.
        public bool InventoryOpened;

        // 채팅 입력창이 열려 있음. 열린 동안 우주선 조작과 메뉴 단축키를 막음.
        public bool ChatFocused;

        // 커서가 닫힌 채팅창의 탭 위에 있음. 탭을 누르는 클릭으로 포탑이 발사되지 않게 막음.
        public bool ChatHovered;

        // 우주선이 파괴됨. 리스폰 창이 이 값을 보고 열리고, 리스폰을 요청하면 꺼짐.
        public bool Dead;

        // 서버 접속(핸드셰이크 전)에 실패함. 시작 화면이 경고를 띄운 뒤 끔.
        public bool ConnectionFailed;
    }
}
