using GalaxyIO.DOTS.Authoring.Config;
using Unity.Entities;

namespace GalaxyIO.DOTS.Components.Singleton
{
    /// <summary>
    /// <see cref="PrefabRegistryAsset"/>의 DOTS 구조체. 런타임에 스폰하는 프리팹 Entity 모음. 월드당 한 개.
    /// 비어 있는 칸은 Entity.Null.
    /// </summary>
    public struct PrefabRegistryComponent : IComponentData
    {
        // 우주선 파괴 폭발 연출
        public Entity ExplosionEffect;

        // 주기적으로 스폰되는 Strafing AI 적
        public Entity EnemyStrafingShip;

        // 월드에 떨어진 아이템. 모든 아이템 종류가 공유함.
        public Entity ItemEntity;
    }
}
