using GalaxyIO.DOTS.Components.Singleton;
using GalaxyIO.Utils.DEBUG;
using UnityEditor;
using UnityEngine;

namespace GalaxyIO.Scripts.Editor
{
    /// <summary>
    /// <see cref="ItemCommandHandlerList"/>를 에디터 메뉴에 노출함.
    /// 패키지 EditorUtils의 "디버그 함수/" 메뉴 트리를 따름.
    /// </summary>
    public static class ItemDebugMenu
    {
        private const string root = "디버그 함수/10_아이템/";

        [MenuItem(root + "파워 셀 50개", false, 0)]
        private static void GivePowerCell() => ItemCommandHandlerList.GivePowerCell(50);

        [MenuItem(root + "모든 재료 10개씩", false, 1)]
        private static void GiveAllIngredients() => ItemCommandHandlerList.GiveAllIngredients(10);

        [MenuItem(root + "모든 재료 100개씩", false, 2)]
        private static void GiveAllIngredientsStack() => ItemCommandHandlerList.GiveAllIngredients(100);

        [MenuItem(root + "인벤토리 로그", false, 20)]
        private static void LogInventory() => ItemCommandHandlerList.LogInventory();

        // 플레이 중이 아니면 서버 월드가 없어 전부 실패하므로 메뉴를 잠가 둠
        [MenuItem(root + "파워 셀 50개", true)]
        [MenuItem(root + "모든 재료 10개씩", true)]
        [MenuItem(root + "모든 재료 100개씩", true)]
        [MenuItem(root + "인벤토리 로그", true)]
        private static bool IsPlaying() => EditorApplication.isPlaying;

        [MenuItem(root + "개별 지급 창 열기", false, 40)]
        private static void OpenGiveWindow() => ItemGiveWindow.Open();
    }

    /// <summary>아이템 종류와 개수를 골라 지급하는 창.</summary>
    public sealed class ItemGiveWindow : EditorWindow
    {
        private ItemId id = ItemId.PowerCell;
        private int count = 1;

        public static void Open()
        {
            ItemGiveWindow window = GetWindow<ItemGiveWindow>(true, "아이템 지급", true);
            window.minSize = new Vector2(280.0F, 110.0F);
        }

        private void OnGUI()
        {
            id = (ItemId)EditorGUILayout.EnumPopup("아이템", id);
            count = EditorGUILayout.IntSlider("개수", count, 1, 99);

            EditorGUILayout.Space();

            using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying))
            {
                if (GUILayout.Button("지급")) ItemCommandHandlerList.GiveItem(id, count);
            }

            if (!EditorApplication.isPlaying)
                EditorGUILayout.HelpBox("플레이 모드에서만 지급할 수 있음.", MessageType.Info);
        }
    }
}