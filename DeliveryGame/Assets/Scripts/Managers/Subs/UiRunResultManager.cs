using DeliveryRun;
using DeliveryRun.Managers.Core;
using DeliveryRun.UI;
using DeliveryRun.UI.Run;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeliveryRun.Managers.Subs
{
    public sealed class UiRunResultManager : SubManagerBase
    {
        private const float ScenePollInterval = 0.25f;

        private UiPrefabCatalogSO _catalog;
        private AddressablesService _addressables;
        private AudioManager _audioManager;
        private RunReport _pendingReport;
        private bool _hasPendingReport;
        private bool _modalLoadRequested;
        private GameObject _modalInstance;
        private RunResultModalView _modalView;
        private float _scenePollElapsed;
        private bool _isLobbyScene;

        public override string Name => nameof(UiRunResultManager);
        public override int InitOrder => 80;

        protected override void OnInitialize()
        {
            _catalog = UiPrefabCatalogLoader.LoadOrNull();
            Services.TryGet(out _addressables);
            Services.TryGet(out _audioManager);
            Subs.Add<RunReportReady>(Events, OnRunReportReady);
            Subs.Add<SceneTransitionStarted>(Events, OnSceneTransitionStarted);
            Subs.Add<SceneTransitionCompleted>(Events, OnSceneTransitionCompleted);

            HandleSceneChanged(SceneManager.GetActiveScene().name);
        }

        protected override void OnTick(float unscaledDeltaTime)
        {
            _scenePollElapsed += unscaledDeltaTime;
            if (_scenePollElapsed >= ScenePollInterval)
            {
                _scenePollElapsed = 0f;
                HandleSceneChanged(SceneManager.GetActiveScene().name);
            }
        }

        protected override void OnShutdown()
        {
            DestroyModal();
            _hasPendingReport = false;
        }

        private void OnRunReportReady(RunReportReady evt)
        {
            _pendingReport = evt.Report;
            _hasPendingReport = true;

            if (_isLobbyScene)
            {
                TryShowPendingReport();
            }
        }

        private void OnSceneTransitionStarted(SceneTransitionStarted evt)
        {
            if (evt.To == SceneNames.LobbyScene)
            {
                return;
            }

            _isLobbyScene = false;
            DestroyModal();
        }

        private void OnSceneTransitionCompleted(SceneTransitionCompleted evt)
        {
            HandleSceneChanged(evt.SceneName);
        }

        private void HandleSceneChanged(string sceneName)
        {
            bool isLobby = sceneName == SceneNames.LobbyScene;
            if (_isLobbyScene == isLobby)
            {
                if (_isLobbyScene)
                {
                    TryShowPendingReport();
                }

                return;
            }

            _isLobbyScene = isLobby;
            if (_isLobbyScene)
            {
                TryShowPendingReport();
                return;
            }

            DestroyModal();
        }

        private void TryShowPendingReport()
        {
            if (!_hasPendingReport || _modalView != null || _modalLoadRequested)
            {
                return;
            }

            if (_catalog == null)
            {
                _catalog = UiPrefabCatalogLoader.LoadOrNull();
            }

            if (_catalog == null)
            {
                return;
            }

            if (_addressables == null)
            {
                Services.TryGet(out _addressables);
            }

            if (_addressables != null && _addressables.IsAvailable && !string.IsNullOrEmpty(_catalog.RunResultModalKey))
            {
                _modalLoadRequested = true;
                _addressables.InstantiatePrefab(_catalog.RunResultModalKey, null, OnModalInstantiated);
                return;
            }

            if (_catalog.RunResultModalPrefab == null)
            {
                Debug.LogError("[UiRunResultManager] RunResultModal key/prefab is not available.");
                return;
            }

            GameObject modalObject = Object.Instantiate(_catalog.RunResultModalPrefab);
            _modalInstance = modalObject;
            _modalView = modalObject.GetComponent<RunResultModalView>();
            if (_modalView == null)
            {
                Debug.LogError("[UiRunResultManager] RunResultModal prefab missing RunResultModalView.");
                Object.Destroy(modalObject);
                return;
            }

            ShowModal();
        }

        private void OnModalInstantiated(GameObject modalObject)
        {
            _modalLoadRequested = false;
            if (modalObject == null)
            {
                return;
            }

            if (!_isLobbyScene || !_hasPendingReport)
            {
                ReleaseModalObject(modalObject);
                return;
            }

            _modalInstance = modalObject;
            _modalView = modalObject.GetComponent<RunResultModalView>();
            if (_modalView == null)
            {
                Debug.LogError("[UiRunResultManager] Addressables modal missing RunResultModalView.");
                ReleaseModalObject(modalObject);
                return;
            }

            ShowModal();
        }

        private void ShowModal()
        {
            if (_modalView == null)
            {
                return;
            }

            string summary =
                "Cash: $" + _pendingReport.EarnedCash +
                "\nOrders: " + _pendingReport.OrdersCompleted +
                "\nMusic Option: " + _pendingReport.MusicOptionIndex;

            _modalView.Show(summary, OnModalOk);
        }

        private void OnModalOk()
        {
            TryPlayUiClick();
            DestroyModal();
            _hasPendingReport = false;
        }

        private void DestroyModal()
        {
            if (_modalInstance != null)
            {
                ReleaseModalObject(_modalInstance);
            }

            _modalView = null;
            _modalInstance = null;
            _modalLoadRequested = false;
        }

        private void TryPlayUiClick()
        {
            if (_audioManager == null)
            {
                Services.TryGet(out _audioManager);
            }

            if (_audioManager == null || _catalog == null)
            {
                return;
            }

            _audioManager.PlayUiClick(_catalog.UiClickKey);
        }

        private void ReleaseModalObject(GameObject modalObject)
        {
            if (modalObject == null)
            {
                return;
            }

            if (_addressables == null)
            {
                Services.TryGet(out _addressables);
            }

            if (_addressables != null)
            {
                _addressables.ReleaseInstance(modalObject);
                return;
            }

            Object.Destroy(modalObject);
        }
    }
}
