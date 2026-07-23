using System.Threading.Tasks;
using UnityEngine;

namespace DATN.UI
{
    /// <summary>
    /// Abstract MonoBehaviour base for all panels/screens.
    /// All animation, binding, and cleanup hooks live here.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public abstract class ViewBase : MonoBehaviour, IView
    {
        // --- IView Properties ---
        public abstract string ViewId { get; }
        public GameObject RootObject => gameObject;
        public bool IsVisible { get; private set; }

        // --- Cached refs (set in Awake) ---
        protected CanvasGroup _canvasGroup;
        protected IViewModel _viewModel;

        // --- IView Events ---
        public event System.Action OnShowCompleted;
        public event System.Action OnHideCompleted;

        // ----------------------------------------------------------------
        // Lifecycle — sealed entry points call protected virtual hooks
        // ----------------------------------------------------------------

        public async Task InitAsync(IViewModel viewModel)
        {
            _viewModel = viewModel;
            _canvasGroup = GetComponent<CanvasGroup>();
            // TODO: bind viewModel → OnBind, wire reactive properties
            await OnInitAsync();
        }

        public async Task ShowAsync()
        {
            // TODO: set IsVisible = true, call animator, await tween
            await OnShowAsync();
            OnShowCompleted?.Invoke();
        }

        public async Task HideAsync()
        {
            // TODO: await out-animation, set IsVisible = false
            await OnHideAsync();
            OnHideCompleted?.Invoke();
        }

        public async Task DestroyAsync()
        {
            // TODO: unsubscribe events, call viewModel.OnUnbind(), destroy GO
            await OnDestroyAsync();
        }

        // ----------------------------------------------------------------
        // Protected hooks — override in concrete panels
        // ----------------------------------------------------------------

        protected virtual Task OnInitAsync() => Task.CompletedTask;
        protected virtual Task OnShowAsync() => Task.CompletedTask;
        protected virtual Task OnHideAsync() => Task.CompletedTask;
        protected virtual Task OnDestroyAsync() => Task.CompletedTask;
    }
}
