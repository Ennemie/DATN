using System;
using System.Collections.Generic;
using UnityEngine;

namespace DATN.UI
{
    /// <summary>
    /// Single SO asset that maps every C# View type → its ViewDescriptor.
    /// Drag onto the UIManager component in the scene.
    /// </summary>
    [CreateAssetMenu(fileName = "ViewRegistry", menuName = "DATN/UI/View Registry")]
    public class ViewRegistry : ScriptableObject
    {
        [SerializeField] private List<ViewDescriptor> _descriptors = new();

        // Runtime lookup: built in Awake, never traverses list again.
        private Dictionary<string, ViewDescriptor> _byViewId;

        public void Initialize()
        {
            _byViewId = new Dictionary<string, ViewDescriptor>(_descriptors.Count);
            foreach (var d in _descriptors)
            {
                if (string.IsNullOrEmpty(d.viewId))
                {
                    Debug.LogWarning($"[ViewRegistry] Descriptor '{d.name}' has no viewId — skipped.");
                    continue;
                }
                if (!_byViewId.TryAdd(d.viewId, d))
                    Debug.LogWarning($"[ViewRegistry] Duplicate viewId '{d.viewId}' — second entry ignored.");
            }
        }

        /// <summary> Resolve by ViewBase.ViewId (string key). </summary>
        public bool TryGet(string viewId, out ViewDescriptor descriptor)
            => _byViewId.TryGetValue(viewId, out descriptor);

        /// <summary>
        /// Convenience: resolve by C# type using the type's name as viewId convention.
        /// Expects typeof(T).Name == descriptor.viewId.
        /// </summary>
        public bool TryGet<T>(out ViewDescriptor descriptor) where T : IView
            => TryGet(typeof(T).Name, out descriptor);
    }
}
