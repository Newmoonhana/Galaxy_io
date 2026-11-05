using System;

namespace GalaxyIO.DOTS.Spectator
{
    /// <summary>
    /// 관찰자 접속 설정. 데디케이티드 서버 설정(server_config.json)의 Spectator 항목.
    /// 이 이름으로 접속하면 우주선 없이 관찰자로 들어옴. 관찰자 클라이언트(-spectator)가 이 이름을 씀.
    /// </summary>
    [Serializable]
    public sealed class SpectatorSettings
    {
        public bool Enabled = true;

        // 관찰자 접속 이름. 클라이언트 실행 인자 -spectate-name 과 같아야 함.
        public string Name = "@spectator";

        // 관찰자에게 복제해 주는 반경(월드 유닛). 카메라 위치가 중심. 크면 멀리 있는 함대까지 목록에 나오지만 트래픽이 늚.
        public float Radius = 100000.0F;
    }
}
