using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace GalaxyIO
{
    /// <summary>
    /// Lets the HUD mute gameplay input while a text field owns the keyboard, so typing
    /// "1500" into the bot-count field does not also drive the player around.
    /// </summary>
    public static class SimulationInputGate
    {
        public static bool UiHasFocus;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() => UiHasFocus = false;
    }

    /// <summary>
    /// The bridge between the Input System (managed, main thread only) and the simulation
    /// (unmanaged, Burst, worker threads).
    ///
    /// It is a <see cref="SystemBase"/> rather than an <c>ISystem</c> precisely because it
    /// has to hold a managed object - the generated <c>SimControls</c> action asset wrapper.
    /// Everything downstream stays Burst-friendly because all this system does is copy the
    /// sampled axis into the plain-data <see cref="PlayerInputState"/> singleton.
    ///
    /// It runs in <c>InitializationSystemGroup</c> so the value is already published before
    /// any simulation system looks at it.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class PlayerInputSystem : SystemBase
    {
        SimControls m_Controls;

        protected override void OnCreate()
        {
            RequireForUpdate<PlayerInputState>();
            m_Controls = new SimControls();
        }

        protected override void OnStartRunning()
        {
            m_Controls.Gameplay.Enable();
        }

        protected override void OnStopRunning()
        {
            m_Controls.Gameplay.Disable();
        }

        protected override void OnDestroy()
        {
            if (m_Controls == null)
                return;

            m_Controls.Dispose();
            m_Controls = null;
        }

        protected override void OnUpdate()
        {
            var move = float2.zero;

            if (!SimulationInputGate.UiHasFocus)
            {
                var raw = m_Controls.Gameplay.Move.ReadValue<Vector2>();
                move = new float2(raw.x, raw.y);
            }

            SystemAPI.SetSingleton(new PlayerInputState { Move = move });
        }
    }
}