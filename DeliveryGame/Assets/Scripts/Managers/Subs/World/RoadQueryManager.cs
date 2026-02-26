using System.Collections.Generic;
using DeliveryRun.Delivery.World;
using DeliveryRun.Managers.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace DeliveryRun.Managers.Subs
{
    public sealed class RoadQueryManager : SubManagerBase
    {
        private const float ScenePollInterval = 0.5f;

        private readonly List<Collider> _roadColliders = new List<Collider>(64);
        private float _scenePollAccum;
        private bool _isRunScene;
        private bool _missingRoadLogged;

        public override string Name => nameof(RoadQueryManager);
        public override int InitOrder => 68;

        protected override void OnInitialize()
        {
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
            _roadColliders.Clear();
            _isRunScene = false;
            _missingRoadLogged = false;
        }

        public bool GetNearestRoadPoint(Vector3 from, out Vector3 nearestPoint)
        {
            nearestPoint = from;

            if (_roadColliders.Count == 0)
            {
                return false;
            }

            bool found = false;
            float nearestSqr = float.MaxValue;

            for (int i = 0; i < _roadColliders.Count; i++)
            {
                Collider roadCollider = _roadColliders[i];
                if (roadCollider == null || !roadCollider.enabled)
                {
                    continue;
                }

                GameObject owner = roadCollider.gameObject;
                if (owner == null || !owner.activeInHierarchy)
                {
                    continue;
                }

                Vector3 roadPoint = roadCollider.ClosestPoint(from);
                float sqrDistance = (roadPoint - from).sqrMagnitude;
                if (sqrDistance >= nearestSqr)
                {
                    continue;
                }

                nearestSqr = sqrDistance;
                nearestPoint = roadPoint;
                found = true;
            }

            return found;
        }

        private void OnSceneTransitionStarted(SceneTransitionStarted evt)
        {
            if (evt.To == SceneNames.RunScene)
            {
                return;
            }

            _isRunScene = false;
            _roadColliders.Clear();
            _missingRoadLogged = false;
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
                _roadColliders.Clear();
                _missingRoadLogged = false;
                return;
            }

            if (!_isRunScene || forceRebuild || _roadColliders.Count == 0)
            {
                RebuildRoadCache();
            }

            _isRunScene = true;
        }

        private void RebuildRoadCache()
        {
            _roadColliders.Clear();
            _missingRoadLogged = false;

            RoadSurface[] roadSurfaces = Object.FindObjectsByType<RoadSurface>(FindObjectsSortMode.None);
            for (int i = 0; i < roadSurfaces.Length; i++)
            {
                RoadSurface roadSurface = roadSurfaces[i];
                if (roadSurface == null)
                {
                    continue;
                }

                Collider roadCollider = roadSurface.CachedCollider;
                if (roadCollider == null)
                {
                    roadCollider = roadSurface.GetComponent<Collider>();
                }

                if (roadCollider == null)
                {
                    continue;
                }

                _roadColliders.Add(roadCollider);
            }

            if (_roadColliders.Count == 0 && !_missingRoadLogged)
            {
                _missingRoadLogged = true;
                Debug.LogWarning("[RoadQueryManager] No RoadSurface colliders found in RunScene.");
            }
        }
    }
}
