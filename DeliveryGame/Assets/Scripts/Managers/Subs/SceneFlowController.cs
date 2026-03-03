using DeliveryRun;
using DeliveryRun.Managers.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeliveryRun.Managers.Subs
{
    public sealed class SceneFlowController : SubManagerBase
    {
        private const string PhaseLoadLoading = "LoadLoadingScene";
        private const string PhaseLoadTarget = "LoadTargetScene";
        private const string PhaseCompleted = "Completed";

        private LoadingState _loadingState;
        private SceneRouter _sceneRouter;

        private bool _isTransitioning;
        private string _pendingTargetScene;
        private string _queuedTargetScene;
        private AsyncOperation _loadingSceneOperation;
        private AsyncOperation _targetLoadOperation;

        public override string Name => nameof(SceneFlowController);
        public override int InitOrder => 15;

        protected override void OnInitialize()
        {
            _loadingState = Services.GetRequired<LoadingState>();
            _sceneRouter = Services.GetRequired<SceneRouter>();

            Subs.Add<BootToStartRequested>(Events, OnBootToStartRequested);
            Subs.Add<BootToLobbyRequested>(Events, OnBootToLobbyRequested);
            Subs.Add<StartRunRequested>(Events, OnStartRunRequested);
            Subs.Add<ReturnToLobbyRequested>(Events, OnReturnToLobbyRequested);
        }

        protected override void OnTick(float unscaledDeltaTime)
        {
            if (!_isTransitioning || _targetLoadOperation == null)
            {
                return;
            }

            float normalized = _targetLoadOperation.isDone
                ? 1f
                : Mathf.Clamp01(_targetLoadOperation.progress / 0.9f);

            _loadingState.Progress01 = normalized;
            _loadingState.Phase = PhaseLoadTarget;
            Events.Publish(new SceneTransitionProgress
            {
                Progress = normalized,
                Phase = PhaseLoadTarget
            });

            if (!_targetLoadOperation.isDone)
            {
                return;
            }

            string completedScene = _pendingTargetScene;
            _targetLoadOperation = null;
            _pendingTargetScene = null;
            _isTransitioning = false;

            _loadingState.Phase = PhaseCompleted;
            _loadingState.Progress01 = 1f;
            Events.Publish(new SceneTransitionProgress
            {
                Progress = 1f,
                Phase = PhaseCompleted
            });

            _loadingState.Reset();
            Events.Publish(new SceneTransitionCompleted { SceneName = completedScene });

            if (!string.IsNullOrEmpty(_queuedTargetScene))
            {
                string queued = _queuedTargetScene;
                _queuedTargetScene = null;
                if (!string.Equals(queued, completedScene, System.StringComparison.Ordinal))
                {
                    BeginTransition(queued);
                }
            }
        }

        protected override void OnShutdown()
        {
            if (_loadingSceneOperation != null)
            {
                _loadingSceneOperation.completed -= OnLoadingSceneLoaded;
                _loadingSceneOperation = null;
            }

            _targetLoadOperation = null;
            _pendingTargetScene = null;
            _queuedTargetScene = null;
            _isTransitioning = false;
            _sceneRouter = null;
            _loadingState = null;
        }

        private void OnBootToStartRequested(BootToStartRequested evt)
        {
            BeginTransition(SceneNames.StartScene);
        }

        private void OnBootToLobbyRequested(BootToLobbyRequested evt)
        {
            BeginTransition(SceneNames.LobbyScene);
        }

        private void OnStartRunRequested(StartRunRequested evt)
        {
            BeginTransition(SceneNames.RunScene);
        }

        private void OnReturnToLobbyRequested(ReturnToLobbyRequested evt)
        {
            BeginTransition(SceneNames.LobbyScene);
        }

        private void BeginTransition(string targetScene)
        {
            if (_isTransitioning)
            {
                if (!string.IsNullOrEmpty(targetScene) &&
                    !string.Equals(targetScene, _pendingTargetScene, System.StringComparison.Ordinal))
                {
                    _queuedTargetScene = targetScene;
                }

                return;
            }

            if (string.IsNullOrEmpty(targetScene))
            {
                Debug.LogError("[SceneFlowController] Target scene is null or empty.");
                return;
            }

            string fromScene = SceneManager.GetActiveScene().name;
            _isTransitioning = true;
            _pendingTargetScene = targetScene;
            _targetLoadOperation = null;
            _queuedTargetScene = null;

            _loadingState.IsTransitioning = true;
            _loadingState.FromScene = fromScene;
            _loadingState.ToScene = targetScene;
            _loadingState.Phase = PhaseLoadLoading;
            _loadingState.Progress01 = 0f;

            Events.Publish(new SceneTransitionStarted
            {
                From = fromScene,
                To = targetScene
            });

            Events.Publish(new SceneTransitionProgress
            {
                Progress = 0f,
                Phase = PhaseLoadLoading
            });

            AsyncOperation loadingOperation = _sceneRouter.LoadSceneAsync(SceneNames.LoadingScene, LoadSceneMode.Single);
            if (loadingOperation == null)
            {
                FailTransition("Failed to start LoadingScene.");
                return;
            }

            _loadingSceneOperation = loadingOperation;
            _loadingSceneOperation.completed += OnLoadingSceneLoaded;
        }

        private void OnLoadingSceneLoaded(AsyncOperation operation)
        {
            if (_loadingSceneOperation != null)
            {
                _loadingSceneOperation.completed -= OnLoadingSceneLoaded;
                _loadingSceneOperation = null;
            }

            if (!_isTransitioning || string.IsNullOrEmpty(_pendingTargetScene))
            {
                return;
            }

            _loadingState.Phase = PhaseLoadTarget;
            _loadingState.Progress01 = 0f;
            Events.Publish(new SceneTransitionProgress
            {
                Progress = 0f,
                Phase = PhaseLoadTarget
            });

            _targetLoadOperation = _sceneRouter.LoadSceneAsync(_pendingTargetScene, LoadSceneMode.Single);
            if (_targetLoadOperation == null)
            {
                FailTransition("Failed to start target scene load: " + _pendingTargetScene);
            }
        }

        private void FailTransition(string reason)
        {
            Debug.LogError("[SceneFlowController] " + reason);
            _targetLoadOperation = null;
            _pendingTargetScene = null;
            _queuedTargetScene = null;
            _isTransitioning = false;

            if (_loadingSceneOperation != null)
            {
                _loadingSceneOperation.completed -= OnLoadingSceneLoaded;
                _loadingSceneOperation = null;
            }

            if (_loadingState != null)
            {
                _loadingState.Reset();
            }
        }
    }
}
