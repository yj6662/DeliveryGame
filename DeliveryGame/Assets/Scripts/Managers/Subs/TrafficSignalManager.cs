using DeliveryRun;
using DeliveryRun.Managers.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeliveryRun.Managers.Subs
{
    public sealed class TrafficSignalManager : SubManagerBase
    {
        private const float ScenePollInterval = 0.5f;

        private TrafficSignalService _signals;
        private TrafficRoadNetworkService _network;
        private bool _isRunScene;
        private float _scenePollAccum;

        public override string Name => nameof(TrafficSignalManager);
        public override int InitOrder => 68;

        protected override void OnInitialize()
        {
            _signals = new TrafficSignalService();
            Services.Register(_signals);

            Services.TryGet(out _network);

            Subs.Add<SceneTransitionStarted>(Events, OnSceneTransitionStarted);
            Subs.Add<SceneTransitionCompleted>(Events, OnSceneTransitionCompleted);
            Subs.Add<TrafficSystemDefined>(Events, OnTrafficSystemDefined);

            HandleSceneChanged(SceneManager.GetActiveScene().name, true);
        }

        protected override void OnTick(float unscaledDeltaTime)
        {
            _scenePollAccum += unscaledDeltaTime;
            if (_scenePollAccum >= ScenePollInterval)
            {
                _scenePollAccum = 0f;
                HandleSceneChanged(SceneManager.GetActiveScene().name, false);
            }

            if (!_isRunScene || _signals == null)
            {
                return;
            }

            _signals.Tick(unscaledDeltaTime);
        }

        protected override void OnShutdown()
        {
            if (_signals != null)
            {
                _signals.Clear();
            }

            _isRunScene = false;
        }

        private void OnSceneTransitionStarted(SceneTransitionStarted evt)
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
        }

        private void OnSceneTransitionCompleted(SceneTransitionCompleted evt)
        {
            HandleSceneChanged(evt.SceneName, true);
        }

        private void OnTrafficSystemDefined(TrafficSystemDefined evt)
        {
            if (!_isRunScene)
            {
                return;
            }

            RebuildSignals();
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

                return;
            }

            bool enteredNow = !_isRunScene;
            _isRunScene = true;
            if (enteredNow || forceRebuild)
            {
                RebuildSignals();
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
                Services.TryGet(out _network);
            }

            _signals.RebuildFromNetwork(_network);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("[TrafficSignalManager] Rebuilt signals: " + _signals.SignalCount);
#endif
        }
    }
}
