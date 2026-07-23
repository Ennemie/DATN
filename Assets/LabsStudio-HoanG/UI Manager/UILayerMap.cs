using System;
using System.Collections.Generic;
using UnityEngine;

namespace DATN.UI
{
    /// <summary>
    /// Holds one RectTransform container per UILayer.
    /// Attach to the root Canvas GameObject alongside UIManager.
    /// </summary>
    public class UILayerMap : MonoBehaviour
    {
        [Serializable]
        public struct LayerEntry
        {
            public UILayer layer;
            public RectTransform container;
        }

        [SerializeField] private List<LayerEntry> _entries = new();

        private Dictionary<UILayer, RectTransform> _map;

        private void Awake()
        {
            _map = new Dictionary<UILayer, RectTransform>(_entries.Count);
            foreach (var e in _entries)
                _map[e.layer] = e.container;
        }

        /// <summary> Returns the RectTransform container for the requested layer. </summary>
        public RectTransform Get(UILayer layer)
        {
            if (_map.TryGetValue(layer, out var rt)) return rt;
            Debug.LogError($"[UILayerMap] No container registered for layer '{layer}'.");
            return null;
        }
    }
}
