using System.Collections.Generic;
using Newmoonhana.Runtime.Core;
using Newmoonhana.Runtime.Tools.PopupUI.Window;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;
using UnityEngine.UI.Extensions;

namespace GalaxyIO.Tools.PopupUI.Window
{
    /// <summary>
    /// 옵션(환경설정) 창. 볼륨 3종과 언어를 다룸.
    /// </summary>
    public class WindowOption : WindowBase
    {
        // PlayerPrefLocaleSelector가 다음 실행 때 읽는 키.
        private const string localePrefsKey = "selected-locale";

        // 슬라이더 하나가 담당하는 믹서 채널 묶음(이번 프로젝트 기준 분류).
        private static readonly SoundChannel[] masterChannels = { SoundChannel.Master };
        private static readonly SoundChannel[] backgroundChannels =
            { SoundChannel.BGM, SoundChannel.Ambience, SoundChannel.Jingle };
        private static readonly SoundChannel[] ingameChannels =
            { SoundChannel.SFX, SoundChannel.Voice };

        [SerializeField] private Slider master_sld;
        [SerializeField] private Slider background_sld;
        [SerializeField] private Slider ingame_sld;
        [SerializeField] private DropDownList language_ddl;
        [SerializeField] private Button save_btn;
        [SerializeField] private Button exit_btn;

        [Header("Language")]
        // 콤보박스에 노출할 로케일. 테이블에는 예비 언어가 더 있지만 공식 지원만 보여줌.
        [SerializeField] private string[] supportedLocaleCodes = { "en", "ko" };

        private UnityEvent closeOption;

        private List<Locale> locales;
        // 진입 시점 값. Exit(취소) 시 여기로 되돌림.
        private float enteredMaster;
        private float enteredBackground;
        private float enteredIngame;
        private Locale enteredLocale;
        // 목록 초기화 중 콜백이 도는 것을 막음.
        private bool isRefreshing;

        protected override void Awake()
        {
            base.Awake();
            closeOption = new UnityEvent();
        }

        protected override void Start()
        {
            base.Start();
            Refresh();
        }

        public override void Dispose()
        {
            base.Dispose();
            closeOption?.RemoveAllListeners();
            closeOption = null;
            locales = null;
            enteredLocale = null;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            master_sld.onValueChanged.AddListener(OnMasterChanged);
            background_sld.onValueChanged.AddListener(OnBackgroundChanged);
            ingame_sld.onValueChanged.AddListener(OnIngameChanged);
            language_ddl.OnSelectionChanged.AddListener(OnLanguageChanged);
            save_btn.onClick.AddListener(OnSaveBtnPress);
            exit_btn.onClick.AddListener(OnExitBtnPress);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            master_sld.onValueChanged.RemoveListener(OnMasterChanged);
            background_sld.onValueChanged.RemoveListener(OnBackgroundChanged);
            ingame_sld.onValueChanged.RemoveListener(OnIngameChanged);
            language_ddl.OnSelectionChanged.RemoveListener(OnLanguageChanged);
            save_btn.onClick.RemoveListener(OnSaveBtnPress);
            exit_btn.onClick.RemoveListener(OnExitBtnPress);
        }

        public void AddListener(UnityAction _action)
        {
            closeOption.AddListener(_action);
        }

        #region 초기화

        /// <summary>
        /// 저장된 값으로 UI를 맞추고, 되돌리기용 진입 값을 기록.
        /// </summary>
        private void Refresh()
        {
            isRefreshing = true;

            enteredMaster = SoundManager.Instance.GetVolume(SoundChannel.Master);
            enteredBackground = SoundManager.Instance.GetVolume(SoundChannel.BGM);
            enteredIngame = SoundManager.Instance.GetVolume(SoundChannel.SFX);

            master_sld.SetValueWithoutNotify(enteredMaster);
            background_sld.SetValueWithoutNotify(enteredBackground);
            ingame_sld.SetValueWithoutNotify(enteredIngame);

            RefreshLanguages();

            isRefreshing = false;
        }

        private void RefreshLanguages()
        {
            locales = new List<Locale>();
            foreach (string code in supportedLocaleCodes)
            {
                Locale locale = LocalizationSettings.AvailableLocales.GetLocale(code);
                if (locale != null) locales.Add(locale);
            }
            // 설정이 비었거나 코드가 다 안 맞으면 전체 목록으로 대체.
            if (locales.Count == 0)
                locales.AddRange(LocalizationSettings.AvailableLocales.Locales);

            // OS 언어를 따라가다 예비 언어로 잡히면 목록에 없음. 첫 번째 공식 지원 언어로 맞춤.
            int selected = locales.IndexOf(LocalizationSettings.SelectedLocale);
            if (selected < 0)
            {
                selected = 0;
                LocalizationSettings.SelectedLocale = locales[0];
            }
            enteredLocale = LocalizationSettings.SelectedLocale;

            DropDownListItem[] items = new DropDownListItem[locales.Count];
            for (int i = 0; i < locales.Count; i++)
                items[i] = new DropDownListItem(LocaleCaption(locales[i]), locales[i].Identifier.Code);

            language_ddl.RefreshItems(items);
            language_ddl.SelectItemIndex(selected);
            RestoreDropDownSkin(language_ddl, selected);
        }

        // 각 언어를 그 언어로 표기. CultureInfo가 없으면 Locale 이름으로 대체.
        private static string LocaleCaption(Locale _locale)
        {
            System.Globalization.CultureInfo culture = _locale.Identifier.CultureInfo;
            return culture != null ? culture.NativeName : _locale.LocaleName;
        }

        // DropDownList 가 생성 항목의 배경 스프라이트를 지움. ItemTemplate 값으로 되돌리고
        // 고른 항목만 강조색으로 둠. 강조색은 MainButton 의 Highlighted 값을 따라감
        private static void RestoreDropDownSkin(DropDownList _ddl, int _selected)
        {
            Transform template = _ddl.transform.Find("ItemTemplate");
            Transform items = _ddl.transform.Find("Overlay/ScrollPanel/Items");
            if (template == null || items == null) return;

            Image skin = template.GetComponent<Image>();
            if (skin == null) return;

            Button main = _ddl.transform.Find("MainButton")?.GetComponent<Button>();
            Color selectedColor = main != null ? main.colors.highlightedColor : skin.color;

            for (int i = 0; i < items.childCount; i++)
            {
                Image img = items.GetChild(i).GetComponent<Image>();
                if (img == null) continue;

                img.sprite = skin.sprite;
                img.type = skin.type;
                img.pixelsPerUnitMultiplier = skin.pixelsPerUnitMultiplier;
                img.color = i == _selected ? selectedColor : skin.color;
            }
        }

        #endregion

        #region 볼륨

        private void OnMasterChanged(float _value)
        {
            if (isRefreshing) return;

            ApplyChannels(masterChannels, _value);
            // 하위 볼륨은 마스터 볼륨보다 높을 수 없음.
            ClampToMaster(background_sld, backgroundChannels, _value);
            ClampToMaster(ingame_sld, ingameChannels, _value);
        }

        private void OnBackgroundChanged(float _value)
            => OnSubChanged(background_sld, backgroundChannels, _value);

        private void OnIngameChanged(float _value)
            => OnSubChanged(ingame_sld, ingameChannels, _value);

        private void OnSubChanged(Slider _slider, SoundChannel[] _channels, float _value)
        {
            if (isRefreshing) return;

            if (_value > master_sld.value)
            {
                _value = master_sld.value;
                _slider.SetValueWithoutNotify(_value);
            }
            ApplyChannels(_channels, _value);
        }

        private void ClampToMaster(Slider _slider, SoundChannel[] _channels, float _master)
        {
            if (_slider.value <= _master) return;

            _slider.SetValueWithoutNotify(_master);
            ApplyChannels(_channels, _master);
        }

        private static void ApplyChannels(SoundChannel[] _channels, float _volume)
        {
            foreach (SoundChannel channel in _channels)
                SoundManager.Instance.ApplyVolume(channel, _volume);
        }

        private static void SaveChannels(SoundChannel[] _channels, float _volume)
        {
            foreach (SoundChannel channel in _channels)
                SoundManager.Instance.SaveVolume(channel, _volume);
        }

        #endregion

        #region 언어

        private void OnLanguageChanged(int _index)
        {
            if (isRefreshing) return;
            if (_index < 0 || _index >= locales.Count) return;

            LocalizationSettings.SelectedLocale = locales[_index];
            RestoreDropDownSkin(language_ddl, _index);
        }

        #endregion

        #region 버튼

        private void OnSaveBtnPress()
        {
            if (!IsActive()) return;

            SaveChannels(masterChannels, master_sld.value);
            SaveChannels(backgroundChannels, background_sld.value);
            SaveChannels(ingameChannels, ingame_sld.value);

            // 로케일 변경만으로는 PlayerPrefs에 기록되지 않음. 직접 씀.
            Locale locale = LocalizationSettings.SelectedLocale;
            if (locale != null) PlayerPrefs.SetString(localePrefsKey, locale.Identifier.Code);

            PlayerPrefs.Save();
            closeOption?.Invoke();
        }

        private void OnExitBtnPress()
        {
            if (!IsActive()) return;

            ApplyChannels(masterChannels, enteredMaster);
            ApplyChannels(backgroundChannels, enteredBackground);
            ApplyChannels(ingameChannels, enteredIngame);
            if (enteredLocale != null) LocalizationSettings.SelectedLocale = enteredLocale;

            closeOption?.Invoke();
        }

        /// <summary>나가기 버튼과 같은 처리. ESC 용.</summary>
        public void Cancel() => OnExitBtnPress();

        #endregion
    }
}
