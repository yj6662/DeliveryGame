using System;
using System.Collections.Generic;
using DeliveryRun.Managers.Core;
using DeliveryRun.UI;
using DeliveryRun.UI.Features;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DeliveryRun.Managers.Subs
{
    public sealed class UIManager : SubManagerBase
    {
        private readonly List<IUiFeature> _features = new List<IUiFeature>(8);

        private UiPrefabCatalogSO _catalog;
        private AddressablesService _addressables;

        private UiStateFeature _stateFeature;
        private RunHudUiFeature _runHudFeature;

        public override string Name => nameof(UIManager);
        public override int InitOrder => 35;

        public string CurrentScreen => _stateFeature != null ? _stateFeature.CurrentScreen : null;
        public string LastToast => _stateFeature != null ? _stateFeature.LastToast : null;
        public int ActiveOrderCount => _stateFeature != null ? _stateFeature.ActiveOrderCount : 0;
        public int CompletedOrderCount => _stateFeature != null ? _stateFeature.CompletedOrderCount : 0;
        public int FailedOrderCount => _stateFeature != null ? _stateFeature.FailedOrderCount : 0;
        public float RunRemainingSeconds => _stateFeature != null ? _stateFeature.RunRemainingSeconds : 0f;
        public float RatingValue => _stateFeature != null ? _stateFeature.RatingValue : 5f;
        public int SessionCoins => _stateFeature != null ? _stateFeature.SessionCoins : 0;
        public int TotalCoins => _stateFeature != null ? _stateFeature.TotalCoins : 0;
        public int PendingMusicChoiceIndex => _stateFeature != null ? _stateFeature.PendingMusicChoiceIndex : -1;
        public bool IsPauseMenuOpen => _runHudFeature != null && _runHudFeature.IsPauseMenuOpen;

        internal UiPrefabCatalogSO Catalog => _catalog;

        protected override void OnInitialize()
        {
            EnsureUiBootstrap();

            RegisterFeature(_stateFeature = new UiStateFeature());
            RegisterFeature(new MusicChoiceUiFeature());
            RegisterFeature(new LobbyUiFeature());
            RegisterFeature(_runHudFeature = new RunHudUiFeature());
            RegisterFeature(new RunResultUiFeature());
        }

        protected override void OnTick(float unscaledDeltaTime)
        {
            for (int i = 0; i < _features.Count; i++)
            {
                IUiFeature feature = _features[i];
                if (feature == null)
                {
                    continue;
                }

                feature.Tick(unscaledDeltaTime);
            }
        }

        protected override void OnShutdown()
        {
            for (int i = _features.Count - 1; i >= 0; i--)
            {
                IUiFeature feature = _features[i];
                if (feature == null)
                {
                    continue;
                }

                try
                {
                    feature.Shutdown();
                }
                catch (Exception ex)
                {
                    Debug.LogError("[UIManager] UI feature shutdown failed: " + feature.GetType().FullName);
                    Debug.LogException(ex);
                }
            }

            _features.Clear();
            _stateFeature = null;
            _runHudFeature = null;
            _catalog = null;
            _addressables = null;
        }

        private void RegisterFeature(IUiFeature feature)
        {
            if (feature == null)
            {
                return;
            }

            _features.Add(feature);
            feature.Initialize(Ctx, this);
        }

        internal bool TryGetService<T>(out T service) where T : class
        {
            service = null;
            return Services != null && Services.TryGet(out service);
        }

        internal void EnsureUiBootstrap()
        {
            EnsureCatalogLoaded();
            EnsureEventSystem();
            EnsureAddressablesService();
        }

        internal void EnsureEventSystem()
        {
            UiEventSystemBootstrap.EnsureNow();
        }

        internal void EnsureCatalogLoaded()
        {
            if (_catalog != null)
            {
                return;
            }

            _catalog = UiPrefabCatalogLoader.LoadOrNull();
        }

        internal void InstantiateRunHud(Action<GameObject> onDone)
        {
            EnsureCatalogLoaded();
            InstantiateCatalogPrefab(
                _catalog != null ? _catalog.RunHudKey : null,
                _catalog != null ? _catalog.RunHudPrefab : null,
                onDone);
        }

        internal void InstantiateMusicSelectionModal(Action<GameObject> onDone)
        {
            EnsureCatalogLoaded();
            InstantiateCatalogPrefab(
                _catalog != null ? _catalog.MusicSelectionModalKey : null,
                _catalog != null ? _catalog.MusicSelectionModalPrefab : null,
                onDone);
        }

        internal void InstantiateRunResultModal(Action<GameObject> onDone)
        {
            EnsureCatalogLoaded();
            InstantiateCatalogPrefab(
                _catalog != null ? _catalog.RunResultModalKey : null,
                _catalog != null ? _catalog.RunResultModalPrefab : null,
                onDone);
        }

        internal void InstantiateCatalogPrefab(string addressableKey, GameObject fallbackPrefab, Action<GameObject> onDone)
        {
            EnsureUiBootstrap();

            if (onDone == null)
            {
                return;
            }

            if (_addressables != null && _addressables.IsAvailable && !string.IsNullOrEmpty(addressableKey))
            {
                _addressables.InstantiatePrefab(addressableKey, null, instance =>
                {
                    if (instance != null)
                    {
                        onDone(instance);
                        return;
                    }

                    onDone(fallbackPrefab != null ? Object.Instantiate(fallbackPrefab) : null);
                });
                return;
            }

            onDone(fallbackPrefab != null ? Object.Instantiate(fallbackPrefab) : null);
        }

        internal void ReleaseUiInstance(GameObject instance)
        {
            if (instance == null)
            {
                return;
            }

            EnsureAddressablesService();
            if (_addressables != null)
            {
                _addressables.ReleaseInstance(instance);
                return;
            }

            Object.Destroy(instance);
        }

        internal void PlayUiClick()
        {
            EnsureCatalogLoaded();

            AudioManager audioManager;
            if (!TryGetService(out audioManager) || audioManager == null || _catalog == null)
            {
                return;
            }

            audioManager.PlayUiClick(_catalog.UiClickKey);
        }

        private void EnsureAddressablesService()
        {
            if (_addressables != null)
            {
                return;
            }

            Services.TryGet(out _addressables);
        }
    }
}
