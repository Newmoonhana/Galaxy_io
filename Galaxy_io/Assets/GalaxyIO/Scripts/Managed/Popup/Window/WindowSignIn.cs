using System;
using Newmoonhana.Runtime.Tools.PopupUI.Window;
using UnityEngine;
using UnityEngine.UI;

namespace GalaxyIO.Tools.PopupUI.Window
{
    public class WindowSignIn : WindowBase
    {
        [SerializeField] private Button signIn_btn;
        [SerializeField] private Button signUp_btn;
        [SerializeField] private InputField enterId;
        [SerializeField] private InputField enterPw;

        protected override void OnEnable()
        {
            base.OnEnable();
            signIn_btn.onClick.AddListener(OnSignInBtn);
            signUp_btn.onClick.AddListener(OnSignUpBtn);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            signIn_btn.onClick.RemoveListener(OnSignInBtn);
            signUp_btn.onClick.RemoveListener(OnSignUpBtn);
        }

        private void OnSignInBtn()
        {
            
        }
        private void OnSignUpBtn()
        {
            
        }
    }
}