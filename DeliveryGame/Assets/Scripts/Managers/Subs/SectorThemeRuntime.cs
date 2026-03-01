using DeliveryRun;
using DeliveryRun.Delivery.World;
using DeliveryRun.Managers.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;
using DomainRunSessionEnded = DeliveryRun.Delivery.RunSession.RunSessionEnded;
using DomainRunSessionStarted = DeliveryRun.Delivery.RunSession.RunSessionStarted;

namespace DeliveryRun.Managers.Subs
{
    internal sealed class SectorThemeRuntime
    {
        private readonly ServiceRegistry _services;
        private readonly EventBus _events;
        private readonly string[] _appliedSourceIds = new string[16];

        private ModifierStackService _stack;
        private MetaProgressionService _meta;
        private bool _runActive;
        private string _activeRegionId;
        private int _appliedSourceCount;

        internal SectorThemeRuntime(ServiceRegistry services, EventBus events)
        {
            _services = services;
            _events = events;
        }

        internal void Initialize()
        {
            _services.TryGet(out _meta);
            _services.TryGet(out _stack);

            if (SceneManager.GetActiveScene().name == SceneNames.RunScene)
            {
                ApplySectorVisualOnly(ResolveRegionId());
            }
        }

        internal void Shutdown()
        {
            ClearAppliedModifiers(true);
            _runActive = false;
            _activeRegionId = null;
        }

        internal void OnRunStarted(DomainRunSessionStarted evt)
        {
            _runActive = true;
            ApplyCurrentSectorTheme();
        }

        internal void OnRunEnded(DomainRunSessionEnded evt)
        {
            _runActive = false;
            ClearAppliedModifiers(true);
        }

        internal void OnSelectedRegionChanged(SelectedRegionChanged evt)
        {
            string regionId = ResolveRegionId();
            if (_runActive)
            {
                ApplyCurrentSectorTheme();
                return;
            }

            if (SceneManager.GetActiveScene().name == SceneNames.RunScene)
            {
                ApplySectorVisualOnly(regionId);
            }
        }

        internal void OnSceneTransitionStarted(SceneTransitionStarted evt)
        {
            if (evt.To == SceneNames.RunScene)
            {
                return;
            }

            ClearAppliedModifiers(false);
        }

        internal void OnSceneTransitionCompleted(SceneTransitionCompleted evt)
        {
            if (evt.SceneName != SceneNames.RunScene)
            {
                return;
            }

            string regionId = ResolveRegionId();
            ApplySectorVisualOnly(regionId);
            if (_runActive)
            {
                ApplyCurrentSectorTheme();
            }
        }

        private void ApplyCurrentSectorTheme()
        {
            if (_stack == null)
            {
                _services.TryGet(out _stack);
            }

            if (_stack == null)
            {
                return;
            }

            string regionId = ResolveRegionId();
            SectorThemeDefinition definition = SectorThemeDefinitions.Find(regionId);
            _activeRegionId = definition.RegionId;

            ClearAppliedModifiers(false);
            ApplyMul("offer_ttl", RunStatId.OfferAcceptTtlMultiplier, definition.OfferTtlMul);
            ApplyMul("offer_respawn", RunStatId.OfferRespawnDelayMultiplier, definition.OfferRespawnMul);
            ApplyMul("temp_decay", RunStatId.FoodTemperatureDecayMultiplier, definition.TempDecayMul);
            ApplyMul("spill_gain", RunStatId.FoodSpillGainMultiplier, definition.SpillGainMul);
            ApplyMul("speed", RunStatId.PlayerMoveSpeedMultiplier, definition.SpeedMul);
            ApplyMul("turn", RunStatId.BikeTurnSensitivityMultiplier, definition.TurnMul);
            ApplyMul("accel", RunStatId.BikeAccelerationMultiplier, definition.AccelMul);
            ApplyMul("reward", RunStatId.RewardMultiplier, definition.RewardMul);

            ApplySectorVisualOnly(definition.RegionId);

            _events.Publish(new RunModifiersChanged { SourceId = "sector_" + definition.RegionId });
            _events.Publish(new SectorThemeApplied
            {
                RegionId = definition.RegionId,
                DisplayName = definition.DisplayName,
                Description = definition.Description
            });
        }

        private void ApplyMul(string keySuffix, RunStatId stat, float value)
        {
            if (_stack == null)
            {
                return;
            }

            if (Mathf.Abs(value - 1f) < 0.0001f)
            {
                return;
            }

            string sourceId = "sector_" + _activeRegionId + "_" + keySuffix;
            _stack.AddOrReplace(new RunModifier(sourceId, stat, ModifierMode.Mul, value));

            if (_appliedSourceCount < _appliedSourceIds.Length)
            {
                _appliedSourceIds[_appliedSourceCount] = sourceId;
                _appliedSourceCount++;
            }
        }

        private void ClearAppliedModifiers(bool publishChanged)
        {
            if (_stack != null)
            {
                for (int i = 0; i < _appliedSourceCount; i++)
                {
                    string sourceId = _appliedSourceIds[i];
                    if (!string.IsNullOrEmpty(sourceId))
                    {
                        _stack.RemoveSource(sourceId);
                    }

                    _appliedSourceIds[i] = null;
                }
            }
            else
            {
                for (int i = 0; i < _appliedSourceCount; i++)
                {
                    _appliedSourceIds[i] = null;
                }
            }

            _appliedSourceCount = 0;
            if (publishChanged)
            {
                _events.Publish(new RunModifiersChanged { SourceId = "sector_clear" });
            }
        }

        private string ResolveRegionId()
        {
            if (_meta == null)
            {
                _services.TryGet(out _meta);
            }

            if (_meta == null || string.IsNullOrEmpty(_meta.SelectedRegionId))
            {
                return "central";
            }

            if (!_meta.IsRegionUnlocked(_meta.SelectedRegionId))
            {
                return "central";
            }

            return _meta.SelectedRegionId;
        }

        private static void ApplySectorVisualOnly(string activeRegionId)
        {
            SectorThemeMarker[] markers = Object.FindObjectsByType<SectorThemeMarker>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            for (int i = 0; i < markers.Length; i++)
            {
                SectorThemeMarker marker = markers[i];
                if (marker == null || marker.gameObject == null)
                {
                    continue;
                }

                GameObject root = marker.gameObject;
                if (!root.activeSelf)
                {
                    root.SetActive(true);
                }
            }
        }
    }
}
