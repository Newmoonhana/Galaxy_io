using System;
using GalaxyIO.DOTS.Client.Components.Message;
using GalaxyIO.Tools.PopupUI.Popup;
using Newmoonhana.Runtime.Tools.PopupUI;
using Newmoonhana.Runtime.Utils;
using Newmoonhana.Runtime.Utils.Pattern;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

namespace GalaxyIO.Main
{
    public class GalaxyIO_Main : Singleton<GalaxyIO_Main>
    {
        private const string respawnUIKey = "Respawn_UI";

        // 파괴된 동안(=리스폰 대기)인지 판별하는 쿼리.
        private EntityQuery deathQuery;
        private bool hasDeathQuery;
        private RespawnUI respawnUI;

        protected void Start()
        {
            _ = LoadLobbyUI();
            _ = AddressableAssetsManager.gameObject_lst.LoadData(respawnUIKey);

            World clientWorld = ClientServerBootstrap.ClientWorld;
            if (clientWorld != null)
            {
                deathQuery = clientWorld.EntityManager.CreateEntityQuery(
                    ComponentType.ReadOnly<PlayerDeathTag>()
                );
                hasDeathQuery = true;
            }
        }

        // 파괴 시 리스폰 UI 생성, 리스폰 시 삭제.
        private void Update()
        {
            if (!hasDeathQuery) return;

            bool dead = !deathQuery.IsEmpty;
            if (dead == (respawnUI != null)) return;

            if (dead)
            {
                GameObject prefab = AddressableAssetsManager.gameObject_lst.GetData(respawnUIKey).Data;
                respawnUI = Instantiate(prefab).GetComponent<RespawnUI>();
            }
            else
            {
                Destroy(respawnUI.gameObject);
                respawnUI = null;
            }
        }

        public override void Release()
        {
            base.Release();
            AddressableAssetsManager.gameObject_lst.RemoveData(respawnUIKey);
        }

        private async Awaitable LoadLobbyUI()
        {
            AddressableAssetsData<GameObject> lobbyUIData = new AddressableAssetsData<GameObject>();
            await lobbyUIData.Load("Lobby_UI", true);
            Instantiate(lobbyUIData.Data);
            lobbyUIData.UnLoad();
        }
    }

}
