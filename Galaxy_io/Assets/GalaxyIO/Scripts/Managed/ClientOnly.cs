using JWServer.Runtime.Utils;
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
        private void Awake()
        {
            if (Definitions.IsServerProcess)
            {
                gameObject.SetActive(false);
            }
        }
    }
}