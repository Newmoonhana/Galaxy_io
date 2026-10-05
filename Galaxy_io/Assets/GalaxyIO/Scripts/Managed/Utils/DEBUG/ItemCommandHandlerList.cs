using System;
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
        // 적 전용이라 플레이어에게 줄 의미가 없는 아이템
        private static readonly ItemId[] excluded = { ItemId.None, ItemId.CorsairTurret };

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
                                 $"요청 {_count * networkIds.Count} / 실제 {inserted} (칸이 모자람)");
            }
            else
            {
                Debug.Log($"[Command] GiveItem: {_id} x{_count} -> 플레이어 {networkIds.Count}명 (총 {inserted}개)");
            }

            return inserted;
        }

        /// <summary>강화 모듈 지급. 제작 재료용.</summary>
        public static void GiveUpgradeModule(int _count = 50) => GiveItem(ItemId.UpgradeModule, _count);

        /// <summary>설계도 6종 지급. 제작 창(Window_Craft) 확인용.</summary>
        public static void GiveAllBlueprints(int _count = 1) => GiveEach(IsBlueprint, _count, "설계도");

        /// <summary>포탑 6종 지급. 포탑 칸 확인용.</summary>
        public static void GiveAllTurrets(int _count = 1) => GiveEach(IsTurret, _count, "포탑");

        /// <summary>모든 아이템 지급. 인벤토리 창을 가득 채워 확인할 때 씀.</summary>
        public static void GiveEverything(int _count = 1) => GiveEach(_ => true, _count, "전체");

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
            for (int i = 0; i < inventory.Length; i++)
            {
                ItemSlot slot = inventory[i];
                if (slot.Id == ItemId.None) continue;

                sb.AppendLine($"  [{i:D2}] {slot.Type,-7} {slot.Id} x{slot.Count}");
            }

            Debug.Log(sb.ToString());
        }

        private static void GiveEach(Func<ItemId, bool> _filter, int _count, string _label)
        {
            int kinds = 0;
            foreach (ItemId id in Enum.GetValues(typeof(ItemId)))
            {
                if (Array.IndexOf(excluded, id) >= 0) continue;
                if (!_filter(id)) continue;

                if (GiveItem(id, _count) > 0) kinds++;
            }

            Debug.Log($"[Command] {_label} 지급 완료. 종류 {kinds}개 x{_count}");
        }

        private static bool IsBlueprint(ItemId _id) => _id.ToString().EndsWith("Blueprint");

        private static bool IsTurret(ItemId _id)
            => !IsBlueprint(_id) && _id != ItemId.UpgradeModule;

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
