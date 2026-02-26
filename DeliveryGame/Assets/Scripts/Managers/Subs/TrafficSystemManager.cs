using DeliveryRun;
using DeliveryRun.Delivery.World;
using DeliveryRun.Managers.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace DeliveryRun.Managers.Subs
{
    public sealed class TrafficSystemManager : SubManagerBase
    {
        private const float ScenePollInterval = 0.5f;

        private TrafficRoadNetworkService _network;
        private bool _isRunScene;
        private bool _missingRoadLogged;
        private float _scenePollAccum;

        public override string Name => nameof(TrafficSystemManager);
        public override int InitOrder => 67;

        protected override void OnInitialize()
        {
            _network = new TrafficRoadNetworkService();
            Services.Register(_network);

            Subs.Add<SceneTransitionCompleted>(Events, OnSceneTransitionCompleted);
            Subs.Add<SceneTransitionStarted>(Events, OnSceneTransitionStarted);

            HandleSceneChanged(SceneManager.GetActiveScene().name, true);
        }

        protected override void OnTick(float unscaledDeltaTime)
        {
            _scenePollAccum += unscaledDeltaTime;
            if (_scenePollAccum < ScenePollInterval)
            {
                return;
            }

            _scenePollAccum = 0f;
            HandleSceneChanged(SceneManager.GetActiveScene().name, false);
        }

        protected override void OnShutdown()
        {
            if (_network != null)
            {
                _network.Clear();
            }

            _isRunScene = false;
            _missingRoadLogged = false;
        }

        private void OnSceneTransitionStarted(SceneTransitionStarted evt)
        {
            if (evt.To == SceneNames.RunScene)
            {
                return;
            }

            _isRunScene = false;
            _missingRoadLogged = false;
            if (_network != null)
            {
                _network.Clear();
            }
        }

        private void OnSceneTransitionCompleted(SceneTransitionCompleted evt)
        {
            HandleSceneChanged(evt.SceneName, true);
        }

        private void HandleSceneChanged(string sceneName, bool forceRebuild)
        {
            bool isRunScene = sceneName == SceneNames.RunScene;
            if (!isRunScene)
            {
                _isRunScene = false;
                _missingRoadLogged = false;
                if (_network != null)
                {
                    _network.Clear();
                }

                return;
            }

            if (!_isRunScene || forceRebuild)
            {
                RebuildNetwork();
            }

            _isRunScene = true;
        }

        private void RebuildNetwork()
        {
            if (_network == null)
            {
                return;
            }

            RoadSurface[] roadSurfaces = Object.FindObjectsByType<RoadSurface>(FindObjectsSortMode.None);
            _network.Rebuild(roadSurfaces);

            if (_network.LaneCount <= 0)
            {
                if (!_missingRoadLogged)
                {
                    _missingRoadLogged = true;
                    Debug.LogWarning("[TrafficSystemManager] No traffic lanes could be built from RoadSurface colliders.");
                }

                return;
            }

            _missingRoadLogged = false;
            Events.Publish(new TrafficSystemDefined
            {
                LaneCount = _network.LaneCount,
                NodeCount = _network.NodeCount,
                SpawnPointCount = _network.SpawnPointCount
            });

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log(
                "[TrafficSystemManager] Traffic network rebuilt. Lanes=" + _network.LaneCount +
                ", Nodes=" + _network.NodeCount +
                ", SpawnPoints=" + _network.SpawnPointCount);
#endif
        }
    }
}
