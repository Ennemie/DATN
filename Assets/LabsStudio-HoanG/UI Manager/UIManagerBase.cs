using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace DATN.UI
{
    /// <summary>
    /// Singleton-ready abstract UIManager.
    /// Concrete subclass chooses Addressables vs Resources loading strategy.
    /// </summary>
    public abstract class UIManagerBase : MonoBehaviour, IUIManager
    {
        // --- Nav Stack ---
        protected readonly Stack<IView> _navStack = new();

        // --- Overlay registry ---
        protected readonly Dictionary<Type, IView> _overlays = new();

        // --- Asset cache (key = ViewId / type name) ---
        protected readonly Dictionary<string, IView> _cache = new();

        // --- IUIManager Properties ---
        public IView Current => _navStack.Count > 0 ? _navStack.Peek() : null;
        public int StackDepth => _navStack.Count;
        public bool IsTransitioning { get; private set; }

        // --- Events ---
        public event Action<IView, IView> OnViewChanged;

        // ----------------------------------------------------------------
        // IUIManager — Stack Navigation
        // ----------------------------------------------------------------

        public virtual async Task<T> PushAsync<T>(IViewModel viewModel = null) where T : IView
        {
            // TODO: guard IsTransitioning, load/cache T, hide Current, init+show T, push stack, fire OnViewChanged
            await Task.CompletedTask;
            return default;
        }

        public virtual async Task PopAsync()
        {
            // TODO: guard empty stack, hide+destroy Current, pop, show new Current, fire OnViewChanged
            await Task.CompletedTask;
        }

        public virtual async Task PopToAsync<T>() where T : IView
        {
            // TODO: pop+destroy until typeof(T) is at top
            await Task.CompletedTask;
        }

        public virtual async Task SetRootAsync<T>(IViewModel viewModel = null) where T : IView
        {
            // TODO: clear entire stack (destroy all), push T as root
            await Task.CompletedTask;
        }

        // ----------------------------------------------------------------
        // IUIManager — Overlay
        // ----------------------------------------------------------------

        public virtual async Task<T> ShowOverlayAsync<T>(IViewModel viewModel = null) where T : IView
        {
            // TODO: load+cache T, init+show, register in _overlays
            await Task.CompletedTask;
            return default;
        }

        public virtual async Task HideOverlayAsync<T>() where T : IView
        {
            // TODO: hide T overlay, optionally unload
            await Task.CompletedTask;
        }

        // ----------------------------------------------------------------
        // IUIManager — Memory
        // ----------------------------------------------------------------

        public virtual async Task PreloadAsync<T>() where T : IView
        {
            // TODO: load asset, store in _cache, do NOT push or show
            await Task.CompletedTask;
        }

        public virtual async Task UnloadAsync<T>() where T : IView
        {
            // TODO: remove from _cache, call DestroyAsync, release Addressable handle
            await Task.CompletedTask;
        }

        // ----------------------------------------------------------------
        // Abstract — loading strategy (Addressables or Resources)
        // ----------------------------------------------------------------

        /// <summary> Load prefab and instantiate as IView; must NOT push or show. </summary>
        protected virtual Task<T> LoadViewAsync<T>() where T : IView => Task.FromResult(default(T));

        /// <summary> Release the underlying asset handle after DestroyAsync. </summary>
        protected virtual Task ReleaseViewAsync<T>(T view) where T : IView => Task.CompletedTask;

        // ----------------------------------------------------------------
        // Helpers
        // ----------------------------------------------------------------

        /// <summary> Raise OnViewChanged safely. </summary>
        protected void NotifyViewChanged(IView previous, IView next)
        {
            // TODO: marshal to main thread if called off-thread
            OnViewChanged?.Invoke(previous, next);
        }

        protected void SetTransitioning(bool value) => IsTransitioning = value;
    }
}
