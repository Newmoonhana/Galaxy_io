using Unity.Entities;
using Unity.Mathematics;

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

        // 리스폰 창의 이름 입력란(웹)에 입력 중. 글자 키가 채팅 열기·레이더 조준·점수판을 켜지 않게 막음.
        public bool NameFocused;

        // 커서가 닫힌 채팅창의 탭 위에 있음. 탭을 누르는 클릭으로 포탑이 발사되지 않게 막음.
        public bool ChatHovered;

        // 커서가 레이더 위에 있음. 휠이 카메라 대신 레이더 표시 거리를 바꾸고, 레이더 조준 모드면 우주선이 RadarCursor 를 바라봄.
        public bool RadarHovered;

        // 커서가 가리키는 레이더 위 지점. 로컬 우주선 기준 월드 좌표 차이. RadarHovered 일 때만 씀.
        public float2 RadarCursor;

        // 마우스 조준 모드. 마우스 휠 누르기로 켜고 끔. 카메라를 커서 쪽으로 밀어냄.
        // 레이더 조준 모드와 함께 켜면 우주선이 바라보는 쪽으로 밀어냄.
        public bool MouseAim;

        // 레이더 조준 모드. R 로 켜고 끔. 레이더를 키우고, 레이더 위에서는 커서가 가리키는 레이더 지점을 바라봄.
        public bool RadarAim;

        // 우주선이 파괴됨. 리스폰 창이 이 값을 보고 열리고, 리스폰을 요청하면 꺼짐.
        public bool Dead;

        // 서버 접속(핸드셰이크 전)에 실패함. 시작 화면이 경고를 띄운 뒤 끔.
        public bool ConnectionFailed;
    }
}