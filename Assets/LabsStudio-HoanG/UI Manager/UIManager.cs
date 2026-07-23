using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace DATN.UI
{
    /// <summary>
    /// Concrete UIManager: Addressables load strategy + full nav-stack implementation.
    /// Scene setup: one root Canvas → UILayerMap → this component.
    /// </summary>
    [DisallowMultipleComponent]
    public class UIManager : UIManagerBase
    {
        // ----------------------------------------------------------------
        // Inspector
        // ----------------------------------------------------------------

        [Header("Dependencies")]
        [SerializeField] private ViewRegistry _registry;
        [SerializeField] private UILayerMap   _layerMap;

        // ----------------------------------------------------------------
        // Internal: tracks Addressable handles per loaded view
        // ----------------------------------------------------------------

        // Key = ViewId; Value = the AsyncOperationHandle<GameObject> that loaded it
        private readonly Dictionary<string, AsyncOperationHandle<GameObject>> _handles = new();

        // ----------------------------------------------------------------
        // Unity Lifecycle
        // ----------------------------------------------------------------

        private void Awake()
        {
            if (_registry == null)
            {
                Debug.LogError("[UIManager] ViewRegistry not assigned.");
                enabled = false;
                return;
            }
            _registry.Initialize();

            if (_layerMap == null)
            {
                Debug.LogError("[UIManager] UILayerMap not assigned.");
                enabled = false;
                return;
            }
        }

        // ----------------------------------------------------------------
        // IUIManager — Stack Navigation (FULL IMPL)
        // ----------------------------------------------------------------

        public override async Task<T> PushAsync<T>(IViewModel viewModel = null)
        {
            if (IsTransitioning) return default;
            SetTransitioning(true);

            try
            {
                var previous = Current;

                // 1. Obtain (or load) the view
                T view = await GetOrLoadAsync<T>();
                if (view == null) return default;

                // 2. Hide current without destroying
                if (previous != null)
                    await previous.HideAsync();

                // 3. Init (no-op if already inited via PreloadAsync)
                await view.InitAsync(viewModel);

                // 4. Show
                await view.ShowAsync();

                // 5. Push onto stack
                _navStack.Push(view);
                NotifyViewChanged(previous, view);

                return view;
            }
            finally { SetTransitioning(false); }
        }

        public override async Task PopAsync()
        {
            if (IsTransitioning || _navStack.Count == 0) return;
            SetTransitioning(true);

            try
            {
                var previous = _navStack.Pop();
                await previous.HideAsync();
                await DisposeViewIfNeeded(previous);

                var next = Current;
                if (next != null)
                    await next.ShowAsync();

                NotifyViewChanged(previous, next);
            }
            finally { SetTransitioning(false); }
        }

        public override async Task PopToAsync<T>()
        {
            while (_navStack.Count > 0 && Current is not T)
                await PopAsync();
        }

        public override async Task SetRootAsync<T>(IViewModel viewModel = null)
        {
            if (IsTransitioning) return;
            SetTransitioning(true);

            try
            {
                // Destroy everything currently on the stack
                while (_navStack.Count > 0)
                {
                    var v = _navStack.Pop();
                    await v.HideAsync();
                    await DisposeViewIfNeeded(v);
                }
            }
            finally { SetTransitioning(false); }

            // Delegate to normal Push (resets IsTransitioning on its own)
            await PushAsync<T>(viewModel);
        }

        // ----------------------------------------------------------------
        // IUIManager — Overlay
        // ----------------------------------------------------------------

        public override async Task<T> ShowOverlayAsync<T>(IViewModel viewModel = null)
        {
            var t = typeof(T);
            if (_overlays.TryGetValue(t, out var existing))
            {
                await existing.ShowAsync();
                return (T)existing;
            }

            T view = await GetOrLoadAsync<T>();
            if (view == null) return default;

            await view.InitAsync(viewModel);
            await view.ShowAsync();

            _overlays[t] = view;
            return view;
        }

        public override async Task HideOverlayAsync<T>()
        {
            var t = typeof(T);
            if (!_overlays.TryGetValue(t, out var view)) return;

            await view.HideAsync();

            if (!GetDescriptorForView(view, out var desc) || !desc.persistent)
            {
                _overlays.Remove(t);
                await DisposeViewIfNeeded(view);
            }
        }

        // ----------------------------------------------------------------
        // IUIManager — Memory
        // ----------------------------------------------------------------

        public override async Task PreloadAsync<T>()
        {
            string key = typeof(T).Name;
            if (_cache.ContainsKey(key)) return;
            await GetOrLoadAsync<T>();   // loads + caches, does NOT show
        }

        public override async Task UnloadAsync<T>()
        {
            string key = typeof(T).Name;
            if (!_cache.TryGetValue(key, out var view)) return;

            _cache.Remove(key);
            await view.DestroyAsync();
            ReleaseHandle(view);
        }

        // ----------------------------------------------------------------
        // UIManagerBase abstract — Addressables loading strategy
        // ----------------------------------------------------------------

        protected override async Task<T> LoadViewAsync<T>()
        {
            string key = typeof(T).Name;

            if (!_registry.TryGet<T>(out var descriptor))
            {
                Debug.LogError($"[UIManager] No ViewDescriptor found for '{key}'.");
                return default;
            }

            // Load the prefab via Addressables
            AsyncOperationHandle<GameObject> handle =
                Addressables.LoadAssetAsync<GameObject>(descriptor.assetAddress);

            await handle.Task;

            if (handle.Status != AsyncOperationStatus.Succeeded)
            {
                Debug.LogError($"[UIManager] Addressables load failed for '{descriptor.assetAddress}'.");
                Addressables.Release(handle);
                return default;
            }

            // Instantiate into the correct layer container
            RectTransform parent = _layerMap.Get(descriptor.layer);
            GameObject go = Instantiate(handle.Result, parent);
            go.name = key;

            T view = go.GetComponent<T>();
            if (view == null)
            {
                Debug.LogError($"[UIManager] Prefab '{key}' missing component {typeof(T).FullName}.");
                Destroy(go);
                Addressables.Release(handle);
                return default;
            }

            // Cache both the view and its handle
            _cache[key] = view;
            _handles[key] = handle;

            return view;
        }

        protected override Task ReleaseViewAsync<T>(T view)
        {
            ReleaseHandle(view);
            return Task.CompletedTask;
        }

        // ----------------------------------------------------------------
        // Private helpers
        // ----------------------------------------------------------------

        /// <summary> Return cached view if present; otherwise load it. </summary>
        private async Task<T> GetOrLoadAsync<T>() where T : IView
        {
            string key = typeof(T).Name;
            if (_cache.TryGetValue(key, out var cached) && cached is T typed)
                return typed;

            return await LoadViewAsync<T>();
        }

        /// <summary> Destroy + release only when the descriptor marks it non-persistent. </summary>
        private async Task DisposeViewIfNeeded(IView view)
        {
            if (!GetDescriptorForView(view, out var desc) || !desc.persistent)
            {
                string key = view.ViewId;
                _cache.Remove(key);
                await view.DestroyAsync();
                ReleaseHandle(view);
            }
        }

        private void ReleaseHandle(IView view)
        {
            string key = view.ViewId;
            if (_handles.TryGetValue(key, out var handle))
            {
                Addressables.Release(handle);
                _handles.Remove(key);
            }
        }

        private bool GetDescriptorForView(IView view, out ViewDescriptor descriptor)
            => _registry.TryGet(view.ViewId, out descriptor);
    }
}
