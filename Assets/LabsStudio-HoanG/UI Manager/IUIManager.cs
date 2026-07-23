using System.Threading.Tasks;

namespace DATN.UI
{
    /// <summary>
    /// Stack-based navigation + async asset lifecycle contract.
    /// </summary>
    public interface IUIManager
    {
        // --- Stack Navigation ---

        /// <summary> Load (if needed), init, and push <typeparamref name="T"/> onto the nav stack. </summary>
        Task<T> PushAsync<T>(IViewModel viewModel = null) where T : IView;

        /// <summary> Hide current, pop it, resume the previous view. </summary>
        Task PopAsync();

        /// <summary> Pop all panels until <typeparamref name="T"/> is at the top. </summary>
        Task PopToAsync<T>() where T : IView;

        /// <summary> Clear entire stack and push <typeparamref name="T"/> as root. </summary>
        Task SetRootAsync<T>(IViewModel viewModel = null) where T : IView;

        // --- Overlay / Additive ---

        /// <summary> Show a non-stack panel (tooltip, dialog) above the stack. </summary>
        Task<T> ShowOverlayAsync<T>(IViewModel viewModel = null) where T : IView;

        Task HideOverlayAsync<T>() where T : IView;

        // --- Query ---

        IView Current { get; }
        int StackDepth { get; }
        bool IsTransitioning { get; }

        // --- Memory ---

        /// <summary> Preload an asset without pushing. </summary>
        Task PreloadAsync<T>() where T : IView;

        /// <summary> Explicitly unload a cached view. </summary>
        Task UnloadAsync<T>() where T : IView;

        // --- Events ---
        event System.Action<IView, IView> OnViewChanged; // (previous, next)
    }
}
