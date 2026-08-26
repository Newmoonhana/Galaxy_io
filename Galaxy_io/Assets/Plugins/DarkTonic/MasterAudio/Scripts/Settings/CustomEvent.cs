/*! \cond PRIVATE */
using System;
using System.Collections.Generic;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace DarkTonic.MasterAudio {
    [Serializable]
    // ReSharper disable once CheckNamespace
    public class CustomEvent {
        public string EventName;
        public string ProspectiveName;
        public bool IsEditing;
        // ReSharper disable InconsistentNaming
        public bool eventExpanded = true;
        public MasterAudio.CustomEventReceiveMode eventReceiveMode = MasterAudio.CustomEventReceiveMode.Always;
        public float distanceThreshold = 1f;
        public MasterAudio.EventReceiveFilter eventRcvFilterMode = MasterAudio.EventReceiveFilter.All;
        public int filterModeQty = 1;
		public bool isTemporary = false;
		public int frameLastFired = -1;
        public string categoryName = MasterAudio.NoCategory;
        // ReSharper restore InconsistentNaming

#if UNITY_6000_4_OR_NEWER
        private readonly List<EntityId> _actorInstanceIds = new List<EntityId>();
#else
        private readonly List<int> _actorInstanceIds = new List<int>();
#endif

        public CustomEvent(string eventName) {
            EventName = eventName;
            ProspectiveName = eventName;
        }

#if UNITY_6000_4_OR_NEWER
        public void AddActorInstanceId(EntityId instanceId) {
            if (_actorInstanceIds.Contains(instanceId)) {
                return;
            }

            _actorInstanceIds.Add(instanceId);
        }
#else
        public void AddActorInstanceId(int instanceId)
        {
            if (_actorInstanceIds.Contains(instanceId))
            {
                return;
            }

            _actorInstanceIds.Add(instanceId);
        }
#endif

#if UNITY_6000_4_OR_NEWER
        public void RemoveActorInstanceId(EntityId? instanceId) {
            _actorInstanceIds.Remove(instanceId.Value);
        }
#else
        public void RemoveActorInstanceId(int instanceId)
        {
            _actorInstanceIds.Remove(instanceId);
        }
#endif

        public bool HasLiveActors {
            get {
                return _actorInstanceIds.Count > 0;
            }
        }
    }
}
/*! \endcond */