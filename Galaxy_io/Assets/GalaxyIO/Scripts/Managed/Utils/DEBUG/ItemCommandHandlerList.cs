using System.Collections.Generic;
using System.Text;
using GalaxyIO.DOTS.Client.Components.Singleton;
using GalaxyIO.DOTS.Components.Actor;
using GalaxyIO.DOTS.Components.Singleton;
using GalaxyIO.DOTS.Inventory;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

namespace GalaxyIO.Utils.DEBUG
{
    /// <summary>
    /// 아이템 지급 디버그 커맨드. 패키지 <c>GlobalCommandHandlerList</c>와 같은 역할이지만
    /// partial 이 아니라 별도 클래스임 — 그 클래스는 Newmoonhana.Runtime 어셈블리 소속이라
    /// Assembly-CSharp 쪽에서 partial 로 이어붙일 수 없고, 거기서는 ItemId 도 보이지 않음.
    /// <para/>
    /// 아이템 삽입은 서버 권한이므로 서버 월드의 <see cref="InventoryService"/>에 직접 넣음.
    /// 호스트 / 로컬(Loopback) 세션에서만 동작함.
    /// </summary>
    public static class ItemCommandHandlerList
    {
        /// <summary>
        /// 접속한 모든 플레이어에게 아이템을 넣음. 실제로 들어간 개수를 반환.
        /// </summary>
        public static int GiveItem(ItemId _id, int _count = 1)
        {
            if (_count <= 0)
            {
                Debug.LogWarning($"[Command] GiveItem: 개수가 0 이하임 ({_count})");
                return 0;
            }

            if (!TryGetService(out InventoryService service, out List<int> networkIds))
            {
                return 0;
            }

            int inserted = 0;
            foreach (int networkId in networkIds)
            {
                inserted += service.Insert(networkId, _id, _count);
            }

            if (inserted < _count * networkIds.Count)
            {
                Debug.LogWarning($"[Command] GiveItem: {_id} 일부만 들어감. " +
                                 $"요청 {_count * networkIds.Count} / 실제 {inserted} (재료가 아니거나 최대 개수)");
            }
            else
            {
                Debug.Log($"[Command] GiveItem: {_id} x{_count} -> 플레이어 {networkIds.Count}명 (총 {inserted}개)");
            }

            return inserted;
        }

        /// <summary>파워 셀 지급. 강화, 구매 확인용.</summary>
        public static void GivePowerCell(int _count = 50) => GiveItem(ItemId.PowerCell, _count);

        /// <summary>모든 재료 지급. 상점 구매 확인용.</summary>
        public static void GiveAllIngredients(int _count = 10)
        {
            foreach (ItemId id in PlayerInventory.Ingredients)
            {
                GiveItem(id, _count);
            }
        }

        /// <summary>로컬 클라이언트가 들고 있는 인벤토리를 로그로 출력.</summary>
        public static void LogInventory()
        {
            World client = ClientServerBootstrap.ClientWorld;
            if (client == null || !client.IsCreated)
            {
                Debug.LogWarning("[Command] LogInventory: 클라이언트 월드가 없음. 플레이 중인지 확인");
                return;
            }

            EntityQuery query = client.EntityManager.CreateEntityQuery(
                ComponentType.ReadOnly<LocalInventoryComponent>());

            if (query.IsEmpty)
            {
                Debug.LogWarning("[Command] LogInventory: LocalInventoryComponent 가 없음");
                return;
            }

            // 관리형 IComponentData 라 GetSingleton<T> 을 쓸 수 없음
            PlayerInventory inventory = client.EntityManager
                .GetComponentObject<LocalInventoryComponent>(query.GetSingletonEntity()).Value;

            if (inventory == null)
            {
                Debug.LogWarning("[Command] LogInventory: 아직 서버에서 인벤토리를 못 받았음");
                return;
            }

            StringBuilder sb = new StringBuilder("[Command] 로컬 인벤토리\n");
            foreach (ItemId id in PlayerInventory.Ingredients)
            {
                sb.AppendLine($"  {id} x{inventory.Count(id)}");
            }

            Debug.Log(sb.ToString());
        }

        /// <summary>
        /// 서버 월드의 서비스와 접속한 플레이어 NetworkId 목록을 가져옴.
        /// </summary>
        private static bool TryGetService(out InventoryService _service, out List<int> _networkIds)
        {
            _service = null;
            _networkIds = new List<int>();

            World server = ClientServerBootstrap.ServerWorld;
            if (server == null || !server.IsCreated)
            {
                Debug.LogWarning("[Command] 서버 월드가 없음. 호스트 / Loopback 세션에서만 쓸 수 있음");
                return false;
            }

            EntityQuery serviceQuery = server.EntityManager.CreateEntityQuery(
                ComponentType.ReadOnly<InventoryServiceComponent>());

            if (serviceQuery.IsEmpty)
            {
                Debug.LogWarning("[Command] InventoryServiceComponent 가 없음. 아직 인게임이 아님");
                return false;
            }

            // 관리형 IComponentData 라 GetSingleton<T> 을 쓸 수 없음
            _service = server.EntityManager
                .GetComponentObject<InventoryServiceComponent>(serviceQuery.GetSingletonEntity()).Value;

            // 인벤토리는 우주선이 스폰된 뒤 NetworkId 기준으로 만들어짐
            EntityQuery shipQuery = server.EntityManager.CreateEntityQuery(
                ComponentType.ReadOnly<GhostOwner>(),
                ComponentType.ReadOnly<PlayerNameComponent>());

            using (NativeArray<Entity> ships = shipQuery.ToEntityArray(Allocator.Temp))
            {
                foreach (Entity ship in ships)
                {
                    _networkIds.Add(server.EntityManager.GetComponentData<GhostOwner>(ship).NetworkId);
                }
            }

            if (_networkIds.Count == 0)
            {
                Debug.LogWarning("[Command] 플레이어 우주선이 없음. 스폰 후에 다시 호출");
                return false;
            }

            return true;
        }
    }
}
