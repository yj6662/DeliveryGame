using DeliveryRun.Managers.Core;
using UnityEngine.SceneManagement;

namespace DeliveryRun.Managers.Subs
{
    internal sealed class TrafficSignalRuntime
    {
        private const float ScenePollInterval = 0.5f;

        private readonly ServiceRegistry _services;
        private readonly TrafficSignalVisualRuntime _visualRuntime;

        private TrafficSignalService _signals;
        private TrafficRoadNetworkService _network;
        private bool _isRunScene;
        private float _scenePollAccum;

        internal TrafficSignalRuntime(ServiceRegistry services)
        {
            _services = services;
            _visualRuntime = new TrafficSignalVisualRuntime();
        }

        internal void Initialize()
        {
            _signals = new TrafficSignalService();
            _services.Register(_signals);
            _services.TryGet(out _network);

            _visualRuntime.Bind(_signals);
            HandleSceneChanged(SceneManager.GetActiveScene().name, forceRebuild: true);
        }

        internal void Tick(float unscaledDeltaTime)
        {
            if (ScenePollUtil.ShouldPoll(ref _scenePollAccum, ScenePollInterval, unscaledDeltaTime))
            {
                HandleSceneChanged(SceneManager.GetActiveScene().name, forceRebuild: false);
            }

            if (!_isRunScene || _signals == null)
            {
                return;
            }

            _signals.Tick(unscaledDeltaTime);
            _visualRuntime.TickVisuals();
        }

        internal void Shutdown()
        {
            if (_signals != null)
            {
                _signals.Clear();
            }

            _visualRuntime.Shutdown();
            _isRunScene = false;
        }

        internal void OnSceneTransitionStarted(SceneTransitionStarted evt)
        {
            if (evt.To == SceneNames.RunScene)
            {
                return;
            }

            _isRunScene = false;
            if (_signals != null)
            {
                _signals.Clear();
            }

            _visualRuntime.DestroyVisuals();
        }

        internal void OnSceneTransitionCompleted(SceneTransitionCompleted evt)
        {
            HandleSceneChanged(evt.SceneName, forceRebuild: true);
        }

        internal void OnTrafficSystemDefined()
        {
            if (!_isRunScene)
            {
                return;
            }

            RebuildSignals();
            _visualRuntime.RebuildVisuals(_network);
        }

        private void HandleSceneChanged(string sceneName, bool forceRebuild)
        {
            bool isRun = sceneName == SceneNames.RunScene;
            if (!isRun)
            {
                _isRunScene = false;
                if (_signals != null)
                {
                    _signals.Clear();
                }

                _visualRuntime.DestroyVisuals();
                return;
            }

            bool enteredNow = !_isRunScene;
            _isRunScene = true;
            if (enteredNow || forceRebuild)
            {
                RebuildSignals();
                _visualRuntime.RebuildVisuals(_network);
            }
        }

        private void RebuildSignals()
        {
            if (_signals == null)
            {
                return;
            }

            if (_network == null)
            {
                _services.TryGet(out _network);
            }

            _signals.RebuildFromNetwork(_network);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            UnityEngine.Debug.Log("[TrafficSignalManager] Rebuilt signals: " + _signals.SignalCount);
#endif
        }
    }
}
