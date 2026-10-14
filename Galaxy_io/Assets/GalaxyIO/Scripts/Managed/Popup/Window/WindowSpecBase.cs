using System;
using Newmoonhana.Runtime.Tools.PopupUI.Window;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GalaxyIO.Tools.PopupUI.Window
{
    /// <summary>
    /// 상품 하나의 상세 사양 창의 공통부. 인벤토리 창 위에 열리고 패널 밖이나 취소를 누르면 닫힘.
    /// 무엇을 보여줄지는 우주선 / 포탑 창이 정함.
    /// </summary>
    public abstract class WindowSpecBase : WindowBase, IPointerDownHandler
    {
        [Header("Spec")]
        [Tooltip("이 영역 밖을 누르면 창을 닫음")]
        [SerializeField] private RectTransform panel_rt;

        [SerializeField] protected TextMeshProUGUI name_tmp;

        [Tooltip("사양 줄. 줄바꿈으로 이어 붙임")]
        [SerializeField] protected TextMeshProUGUI spec_tmp;

        [SerializeField] private Button equip_btn;
        [SerializeField] private TextMeshProUGUI equip_tmp;
        [SerializeField] private Button cancel_btn;

        /// <summary>장착 버튼.</summary>
        public event Action OnEquip;

        /// <summary>취소, 패널 밖 누름.</summary>
        public event Action OnClosed;

        // 장착 버튼 글자의 번역 키. 바뀔 때만 다시 씀
        private string shownLabel;

        public override void Dispose()
        {
            base.Dispose();

            OnEquip = null;
            OnClosed = null;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            equip_btn.onClick.AddListener(OnEquipBtnPress);
            cancel_btn.onClick.AddListener(OnCancelBtnPress);
            shownLabel = null;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            equip_btn.onClick.RemoveListener(OnEquipBtnPress);
            cancel_btn.onClick.RemoveListener(OnCancelBtnPress);
        }

        /// <summary>
        /// 장착 버튼 상태. 글자는 번역 키로 받음.
        /// </summary>
        protected void SetEquip(string _labelKey, bool _isInteractable)
        {
            equip_btn.interactable = _isInteractable;

            if (shownLabel == _labelKey) return;

            shownLabel = _labelKey;
            equip_tmp.text = L10n.Get(_labelKey);
        }

        private void OnEquipBtnPress()
        {
            if (!IsActive()) return;
            OnEquip?.Invoke();
        }

        private void OnCancelBtnPress()
        {
            if (!IsActive()) return;
            OnClosed?.Invoke();
        }

        // 버튼이 아닌 곳을 누름. 패널 밖이면 닫음
        public void OnPointerDown(PointerEventData _eventData)
        {
            if (!IsActive() || !IsOpen()) return;
            if (RectTransformUtility.RectangleContainsScreenPoint(panel_rt, _eventData.position, _eventData.pressEventCamera))
                return;

            OnClosed?.Invoke();
        }
    }
}
