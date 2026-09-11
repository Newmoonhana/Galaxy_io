using GalaxyIO.DOTS.Client.Components;
using GalaxyIO.DOTS.Client.Networking;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

namespace GalaxyIO.Scripts.Managed.UI
{
    /// <summary>
    /// 우주선이 파괴된 동안 리스폰 창을 띄우는 UI 백엔드.
    /// </summary>
    public sealed class RespawnScreenBackend : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("파괴된 동안 띄울 화면")]
        private GameObject screen;

        private EntityQuery deathQuery;

        private void Start()
        {
            World clientWorld = ClientServerBootstrap.ClientWorld;

            if (clientWorld == null)
            {
                enabled = false;
                return;
            }

            deathQuery = clientWorld.EntityManager.CreateEntityQuery(
                ComponentType.ReadOnly<PlayerDeathTag>()
            );

            screen.SetActive(false);
        }

        private void Update()
        {
            bool dead = !deathQuery.IsEmpty;

            if (dead != screen.activeSelf)
            {
                screen.SetActive(dead);
            }
        }

        /// <summary>
        /// 리스폰 버튼. 요청이 접수되면 태그가 지워져 창이 닫힘.
        /// </summary>
        public void OnPressRespawn()
        {
            ClientNetworkRequestManager.RequestRespawn();
        }
    }
}
