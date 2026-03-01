using DeliveryRun;
using DeliveryRun.Managers.Core;
using DeliveryRun.Managers.Subs;
using DeliveryRun.UI;
using DeliveryRun.UI.Run;
using UnityEngine;
using UnityEngine.SceneManagement;


namespace DeliveryRun.UI.Features
{
    internal sealed class RunResultUiFeature : UiFeatureBase
    {
        protected override void OnInitialize()
        {
            InitializeRunResultModule();
        }
        protected override void OnTick(float unscaledDeltaTime)
        {
            TickRunResultModule(unscaledDeltaTime);
        }
        protected override void OnShutdown()
        {
            ShutdownRunResultModule();
        }

        private const float RunResultScenePollInterval = 0.25f;

        private RunReport _runResultPendingReport;
        private bool _runResultHasPendingReport;
        private bool _runResultModalLoadRequested;
        private GameObject _runResultModalInstance;
        private RunResultModalView _runResultModalView;
        private float _runResultScenePollElapsed;
        private bool _runResultIsLobbyScene;

        private void InitializeRunResultModule()
        {
            Subs.Add<RunReportReady>(Events, OnRunResultRunReportReady);
            Subs.Add<SceneTransitionStarted>(Events, OnRunResultSceneTransitionStarted);
            Subs.Add<SceneTransitionCompleted>(Events, OnRunResultSceneTransitionCompleted);

            HandleRunResultSceneChanged(SceneManager.GetActiveScene().name);
        }

        private void TickRunResultModule(float unscaledDeltaTime)
        {
            if (ScenePollUtil.ShouldPoll(
                ref _runResultScenePollElapsed,
                RunResultScenePollInterval,
                unscaledDeltaTime))
            {
                HandleRunResultSceneChanged(SceneManager.GetActiveScene().name);
            }
        }

        private void ShutdownRunResultModule()
        {
            DestroyRunResultModal();
            _runResultHasPendingReport = false;
        }

        private void OnRunResultRunReportReady(RunReportReady evt)
        {
            _runResultPendingReport = evt.Report;
            _runResultHasPendingReport = true;

            if (_runResultIsLobbyScene)
            {
                TryShowRunResultPendingReport();
            }
        }

        private void OnRunResultSceneTransitionStarted(SceneTransitionStarted evt)
        {
            if (evt.To == SceneNames.LobbyScene)
            {
                return;
            }

            _runResultIsLobbyScene = false;
            DestroyRunResultModal();
        }

        private void OnRunResultSceneTransitionCompleted(SceneTransitionCompleted evt)
        {
            HandleRunResultSceneChanged(evt.SceneName);
        }

        private void HandleRunResultSceneChanged(string sceneName)
        {
            bool isLobby = sceneName == SceneNames.LobbyScene;
            if (_runResultIsLobbyScene == isLobby)
            {
                if (_runResultIsLobbyScene)
                {
                    TryShowRunResultPendingReport();
                }

                return;
            }

            _runResultIsLobbyScene = isLobby;
            if (_runResultIsLobbyScene)
            {
                TryShowRunResultPendingReport();
                return;
            }

            DestroyRunResultModal();
        }

        private void TryShowRunResultPendingReport()
        {
            if (!_runResultHasPendingReport || _runResultModalView != null || _runResultModalLoadRequested)
            {
                return;
            }

            _runResultModalLoadRequested = true;
            Ui.InstantiateRunResultModal(OnRunResultModalInstantiated);
        }

        private void OnRunResultModalInstantiated(GameObject modalObject)
        {
            _runResultModalLoadRequested = false;
            if (modalObject == null)
            {
                return;
            }

            if (!_runResultIsLobbyScene || !_runResultHasPendingReport)
            {
                ReleaseRunResultModalObject(modalObject);
                return;
            }

            _runResultModalInstance = modalObject;
            _runResultModalView = modalObject.GetComponent<RunResultModalView>();
            if (_runResultModalView == null)
            {
                Debug.LogError("[UiRunResultManager] Addressables modal missing RunResultModalView.");
                ReleaseRunResultModalObject(modalObject);
                return;
            }

            ShowRunResultModal();
        }

        private void ShowRunResultModal()
        {
            if (_runResultModalView == null)
            {
                return;
            }

            string summary =
                "Cash: $" + _runResultPendingReport.EarnedCash +
                "\nOrders: " + _runResultPendingReport.OrdersCompleted +
                "\nMusic Option: " + _runResultPendingReport.MusicOptionIndex;

            _runResultModalView.Show(summary, OnRunResultModalOk);
        }

        private void OnRunResultModalOk()
        {
            TryPlayRunResultUiClick();
            DestroyRunResultModal();
            _runResultHasPendingReport = false;
        }

        private void DestroyRunResultModal()
        {
            if (_runResultModalInstance != null)
            {
                ReleaseRunResultModalObject(_runResultModalInstance);
            }

            _runResultModalView = null;
            _runResultModalInstance = null;
            _runResultModalLoadRequested = false;
        }

        private void TryPlayRunResultUiClick()
        {
            Ui.PlayUiClick();
        }

        private void ReleaseRunResultModalObject(GameObject modalObject)
        {
            if (modalObject == null)
            {
                return;
            }

            Ui.ReleaseUiInstance(modalObject);
        }
    }
}
