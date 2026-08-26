/*! \cond PRIVATE */
using System;
using System.Collections.Generic;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace DarkTonic.MasterAudio {
	[Serializable]
	// ReSharper disable once CheckNamespace
	public class CustomEventCategory {
		public string CatName = MasterAudio.NoCategory;
		public bool IsExpanded = true;
		public bool IsEditing = false;
		public bool IsTemporary = false;
		public string ProspectiveName = MasterAudio.NoCategory;

#if UNITY_6000_4_OR_NEWER
        private readonly List<EntityId> _actorInstanceIds = new List<EntityId>();
#else
        private readonly List<int> _actorInstanceIds = new List<int>();
#endif

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