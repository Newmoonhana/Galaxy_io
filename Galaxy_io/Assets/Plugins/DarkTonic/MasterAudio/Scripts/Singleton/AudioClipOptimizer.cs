/*! \cond PRIVATE */

using System.Collections.Generic;
using UnityEngine;

public static class AudioClipOptimizer {
#if UNITY_6000_4_OR_NEWER
    private static readonly Dictionary<EntityId, string> AudioClipNameByInstanceId = new Dictionary<EntityId, string>();
#else
    private static readonly Dictionary<int, string> AudioClipNameByInstanceId = new Dictionary<int, string>();
#endif

    public static string CachedName(this AudioClip clip)
    {
#if UNITY_6000_4_OR_NEWER
        var instanceId = clip.GetEntityId();
#else
        var instanceId = clip.GetInstanceID();
#endif
        if (AudioClipNameByInstanceId.ContainsKey(instanceId))
        {
            return AudioClipNameByInstanceId[instanceId];
        }

        var clipName = clip.name; // allocate
        AudioClipNameByInstanceId.Add(instanceId, clipName);

        return clipName;
    }
}
/*! \endcond */