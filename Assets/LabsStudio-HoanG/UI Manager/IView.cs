using System.Threading.Tasks;
using UnityEngine;

namespace DATN.UI
{
    /// <summary>
    /// Contract for every UI panel/screen.
    /// </summary>
    public interface IView
    {
        // --- Identity ---
        string ViewId { get; }
        GameObject RootObject { get; }
        bool IsVisible { get; }

        // --- Lifecycle ---

        /// <summary> Called once after the prefab is loaded and injected. </summary>
        Task InitAsync(IViewModel viewModel);

        /// <summary> Animate / activate the view. </summary>
        Task ShowAsync();

        /// <summary> Animate / deactivate the view (panel stays in memory). </summary>
        Task HideAsync();

        /// <summary> Release all resources; prefab will be unloaded after this. </summary>
        Task DestroyAsync();

        // --- Events (subscribe before Show) ---
        event System.Action OnShowCompleted;
        event System.Action OnHideCompleted;
    }
}
