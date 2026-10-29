using GalaxyIO.DOTS.Components.Actor;
using GalaxyIO.DOTS.Components.Singleton;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace GalaxyIO.DOTS.Components.Message
{
    /// <summary>
    /// 서버가 쏜 투사체의 발사 정보. 근처 클라이언트가 받아서 같은 투사체를 쏨.
    /// <see cref="ProjectileReplication.Shot"/> 투사체만 보냄.
    /// </summary>
    public struct ProjectileShotRPC : IRpcCommand
    {
        // 쏜 우주선. 클라이언트에 복제되지 않았으면 Entity.Null.
        public Entity Owner;

        // 쏜 포탑. 투사체 프리팹과 발사음을 찾음.
        public ItemId Turret;

        // 쏜 우주선의 진영. 명중 연출 판정에 씀.
        public FactionID Faction;
        public uint Party;

        // 투사체가 총구에 놓인 서버 틱
        public uint Tick;

        // 발사 위치, 방향(Z 축 회전, 라디안), 속력
        public float2 Position;
        public float Angle;
        public float Speed;
    }
}
