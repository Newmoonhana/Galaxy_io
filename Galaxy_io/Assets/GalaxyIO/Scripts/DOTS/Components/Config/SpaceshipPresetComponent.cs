using Unity.Entities;

namespace GalaxyIO.DOTS.Components.Config
{
    public struct SpaceshipPresetComponent : IComponentData
    {
        public Entity PlayerDefaultShip;
    }
}