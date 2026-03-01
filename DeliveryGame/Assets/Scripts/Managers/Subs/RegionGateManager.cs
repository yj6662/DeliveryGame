using System.Collections.Generic;
using DeliveryRun.Delivery.World;
using DeliveryRun.Managers.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace DeliveryRun.Managers.Subs
{
    public sealed class RegionGateManager : SubManagerBase
    {
        private const float ScenePollInterval = 0.5f;

        private readonly List<RegionGateBarrier> _gates = new List<RegionGateBarrier>(16);
        private MetaProgressionService _meta;
        private bool _isRunScene;
        private bool _missingGateLogged;
        private float _scenePollAccum;

        public override string Name => nameof(RegionGateManager);
        public override int InitOrder => 66;

        protected override void OnInitialize()
        {
            Services.TryGet(out _meta);

            Subs.Add<SceneTransitionStarted>(Events, OnSceneTransitionStarted);
            Subs.Add<SceneTransitionCompleted>(Events, OnSceneTransitionCompleted);
            Subs.Add<RegionUnlocked>(Events, OnRegionUnlocked);

            HandleSceneChanged(SceneManager.GetActiveScene().name, true);
        }

        protected override void OnTick(float unscaledDeltaTime)
        {
            if (!ScenePollUtil.ShouldPoll(ref _scenePollAccum, ScenePollInterval, unscaledDeltaTime))
            {
                return;
            }

            HandleSceneChanged(SceneManager.GetActiveScene().name, false);
        }

        protected override void OnShutdown()
        {
            _gates.Clear();
            _isRunScene = false;
            _missingGateLogged = false;
        }

        private void OnSceneTransitionStarted(SceneTransitionStarted evt)
        {
            if (evt.To == SceneNames.RunScene)
            {
                return;
            }

            _isRunScene = false;
            _gates.Clear();
            _missingGateLogged = false;
        }

        private void OnSceneTransitionCompleted(SceneTransitionCompleted evt)
        {
            HandleSceneChanged(evt.SceneName, true);
        }

        private void OnRegionUnlocked(RegionUnlocked evt)
        {
            if (!_isRunScene)
            {
                return;
            }

            ApplyGateStates();
        }

        private void HandleSceneChanged(string sceneName, bool forceRefresh)
        {
            bool isRun = sceneName == SceneNames.RunScene;
            if (!isRun)
            {
                _isRunScene = false;
                _gates.Clear();
                _missingGateLogged = false;
                return;
            }

            if (!_isRunScene || forceRefresh || _gates.Count == 0)
            {
                RefreshGateCache();
                ApplyGateStates();
            }

            _isRunScene = true;
        }

        private void RefreshGateCache()
        {
            _gates.Clear();

            RegionGateBarrier[] gates = Object.FindObjectsByType<RegionGateBarrier>(FindObjectsSortMode.None);
            for (int i = 0; i < gates.Length; i++)
            {
                RegionGateBarrier gate = gates[i];
                if (gate == null)
                {
                    continue;
                }

                _gates.Add(gate);
            }

            if (_gates.Count == 0 && !_missingGateLogged)
            {
                _missingGateLogged = true;
                Debug.LogWarning("[RegionGateManager] No RegionGateBarrier found in RunScene.");
            }
            else if (_gates.Count > 0)
            {
                _missingGateLogged = false;
            }
        }

        private void ApplyGateStates()
        {
            if (_meta == null)
            {
                Services.TryGet(out _meta);
            }

            if (_gates.Count == 0)
            {
                return;
            }

            for (int i = 0; i < _gates.Count; i++)
            {
                RegionGateBarrier gate = _gates[i];
                if (gate == null)
                {
                    continue;
                }

                bool unlocked = true;
                string regionId = gate.RegionId;
                if (!string.IsNullOrEmpty(regionId) && _meta != null)
                {
                    unlocked = _meta.IsRegionUnlocked(regionId);
                }

                gate.ApplyLockedState(!unlocked);
            }
        }
    }
}
