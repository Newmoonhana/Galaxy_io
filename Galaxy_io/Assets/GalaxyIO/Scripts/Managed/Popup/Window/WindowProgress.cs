using Newmoonhana.Runtime.Tools.PopupUI.Window;
using UnityEngine;

namespace GalaxyIO.Tools.PopupUI.Window
{
    /// <summary>
    /// 진행(로딩) 표시 창.
    /// </summary>
    public class WindowProgress : WindowBase
    {
        [SerializeField] private GameObject loadingCircle_obj;

        protected override void Awake()
        {
            base.Awake();
            loadingCircle_obj.SetActive(false);
        }

        protected override void Start()
        {
            base.Start();
            loadingCircle_obj.SetActive(true);
        }
    }
}
