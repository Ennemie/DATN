using UnityEngine;

namespace DATN.UI
{
    public enum UILayer { Background, Main, Overlay, Modal, Debug }

    /// <summary>
    /// One entry in the ViewRegistry asset.
    /// Maps a View type to its Addressable address + render layer.
    /// </summary>
    [CreateAssetMenu(fileName = "ViewDescriptor", menuName = "DATN/UI/View Descriptor")]
    public class ViewDescriptor : ScriptableObject
    {
        [Tooltip("Matches ViewBase.ViewId")]
        public string viewId;

        [Tooltip("Addressable key (or Resources path)")]
        public string assetAddress;

        public UILayer layer;

        [Tooltip("Keep in cache after Hide (false = unload on pop)")]
        public bool persistent;

        // TODO: Add optional transition-override reference (ITransition SO)
    }
}
