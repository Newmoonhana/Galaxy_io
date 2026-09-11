using Unity.NetCode;
using UnityEngine;

namespace GalaxyIO.Scripts.Managed
{
    /// <summary>
    /// 서버 프로세스에서는 이 GameObject를 비활성화하는 컴포넌트.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
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