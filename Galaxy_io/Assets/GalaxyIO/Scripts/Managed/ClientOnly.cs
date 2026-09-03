using Unity.NetCode;
using UnityEngine;

namespace GalaxyIO.Scripts.Managed
{
    [DisallowMultipleComponent]
    public sealed class ClientOnly : MonoBehaviour
    {
        private static bool IsServerProcess =>
            ClientServerBootstrap.RequestedPlayType == ClientServerBootstrap.PlayType.Server;

        private void Awake()
        {
            if (IsServerProcess)
            {
                gameObject.SetActive(false);
            }
        }
    }
}