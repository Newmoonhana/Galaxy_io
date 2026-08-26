#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;

namespace ImpossibleRobert.Common.Editor.Timing
{
    public static class EditorTickBridge
    {
        static readonly Dictionary<Action, EditorApplication.CallbackFunction> s_Callbacks =
            new Dictionary<Action, EditorApplication.CallbackFunction>();

        public static double TimeSinceStartup => EditorApplication.timeSinceStartup;

        public static void Subscribe(Action callback)
        {
            if (callback == null)
                return;

            if (!s_Callbacks.TryGetValue(callback, out EditorApplication.CallbackFunction wrapped))
            {
                wrapped = () => callback();
                s_Callbacks[callback] = wrapped;
            }

            EditorApplication.update -= wrapped;
            EditorApplication.update += wrapped;
        }

        public static void Unsubscribe(Action callback)
        {
            if (callback == null)
                return;

            if (!s_Callbacks.TryGetValue(callback, out EditorApplication.CallbackFunction wrapped))
                return;

            EditorApplication.update -= wrapped;
            s_Callbacks.Remove(callback);
        }
    }
}
#endif
