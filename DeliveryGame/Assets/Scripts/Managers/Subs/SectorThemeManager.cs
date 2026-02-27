using DeliveryRun;
using DeliveryRun.Delivery.World;
using DeliveryRun.Managers.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using DomainRunSessionEnded = DeliveryRun.Delivery.RunSession.RunSessionEnded;
using DomainRunSessionStarted = DeliveryRun.Delivery.RunSession.RunSessionStarted;
using Object = UnityEngine.Object;

namespace DeliveryRun.Managers.Subs
{
    public sealed class SectorThemeManager : SubManagerBase
    {
        private struct SectorDef
        {
            public string RegionId;
            public string DisplayName;
            public string Description;
            public float OfferTtlMul;
            public float OfferRespawnMul;
            public float TempDecayMul;
            public float SpillGainMul;
            public float SpeedMul;
            public float TurnMul;
            public float AccelMul;
            public float RewardMul;
        }

        private static readonly SectorDef[] SectorDefs =
        {
            new SectorDef
            {
                RegionId = "central",
                DisplayName = "Central",
                Description = "Balanced city center.",
                OfferTtlMul = 1f,
                OfferRespawnMul = 1f,
                TempDecayMul = 1f,
                SpillGainMul = 1f,
                SpeedMul = 1f,
                TurnMul = 1f,
                AccelMul = 1f,
                RewardMul = 1f
            },
            new SectorDef
            {
                RegionId = "rushdistrict",
                DisplayName = "Rush District",
                Description = "Orders rotate faster with tighter acceptance windows.",
                OfferTtlMul = 0.72f,
                OfferRespawnMul = 0.82f,
                TempDecayMul = 1.05f,
                SpillGainMul = 1.08f,
                SpeedMul = 1.05f,
                TurnMul = 1f,
                AccelMul = 1.04f,
                RewardMul = 1.02f
            },
            new SectorDef
            {
                RegionId = "frostlands",
                DisplayName = "Frostlands",
                Description = "Cold zone. Food temperature drops much faster.",
                OfferTtlMul = 1f,
                OfferRespawnMul = 1f,
                TempDecayMul = 1.45f,
                SpillGainMul = 1.04f,
                SpeedMul = 0.96f,
                TurnMul = 1.02f,
                AccelMul = 0.94f,
                RewardMul = 1.08f
            },
            new SectorDef
            {
                RegionId = "hillcrest",
                DisplayName = "Hillcrest",
                Description = "Hills and ramps. More spill risk and lower acceleration.",
                OfferTtlMul = 0.94f,
                OfferRespawnMul = 1f,
                TempDecayMul = 1.06f,
                SpillGainMul = 1.35f,
                SpeedMul = 0.95f,
                TurnMul = 0.92f,
                AccelMul = 0.88f,
                RewardMul = 1.12f
            },
            new SectorDef
            {
                RegionId = "stormcoast",
                DisplayName = "Storm Coast",
                Description = "Wet roads. Spill increases quickly.",
                OfferTtlMul = 0.9f,
                OfferRespawnMul = 0.95f,
                TempDecayMul = 1.2f,
                SpillGainMul = 1.55f,
                SpeedMul = 0.97f,
                TurnMul = 0.95f,
                AccelMul = 0.93f,
                RewardMul = 1.15f
            },
            new SectorDef
            {
                RegionId = "oldtown",
                DisplayName = "Old Town",
                Description = "Dense blocks and premium tips for clean deliveries.",
                OfferTtlMul = 1.08f,
                OfferRespawnMul = 1.1f,
                TempDecayMul = 1f,
                SpillGainMul = 1.1f,
                SpeedMul = 0.93f,
                TurnMul = 1.1f,
                AccelMul = 0.92f,
                RewardMul = 1.2f
            },
            new SectorDef
            {
                RegionId = "seaside",
                DisplayName = "Seaside",
                Description = "Sea breeze and wet lanes. Faster chill, cleaner lines earn more.",
                OfferTtlMul = 0.98f,
                OfferRespawnMul = 0.92f,
                TempDecayMul = 1.28f,
                SpillGainMul = 1.22f,
                SpeedMul = 1.01f,
                TurnMul = 0.98f,
                AccelMul = 0.97f,
                RewardMul = 1.24f
            }
        };

        private readonly string[] _appliedSourceIds = new string[16];
        private int _appliedSourceCount;

        private ModifierStackService _stack;
        private MetaProgressionService _meta;
        private bool _runActive;
        private string _activeRegionId;

        public override string Name => nameof(SectorThemeManager);
        public override int InitOrder => 47;

        protected override void OnInitialize()
        {
            Services.TryGet(out _meta);
            Services.TryGet(out _stack);

            Subs.Add<DomainRunSessionStarted>(Events, OnRunStarted);
            Subs.Add<DomainRunSessionEnded>(Events, OnRunEnded);
            Subs.Add<SelectedRegionChanged>(Events, OnSelectedRegionChanged);
            Subs.Add<SceneTransitionStarted>(Events, OnSceneTransitionStarted);
            Subs.Add<SceneTransitionCompleted>(Events, OnSceneTransitionCompleted);

            if (SceneManager.GetActiveScene().name == SceneNames.RunScene)
            {
                ApplySectorVisualOnly(ResolveRegionId());
            }
        }

        protected override void OnShutdown()
        {
            ClearAppliedModifiers(true);
            _runActive = false;
            _activeRegionId = null;
        }

        private void OnRunStarted(DomainRunSessionStarted evt)
        {
            _runActive = true;
            ApplyCurrentSectorTheme();
        }

        private void OnRunEnded(DomainRunSessionEnded evt)
        {
            _runActive = false;
            ClearAppliedModifiers(true);
        }

        private void OnSelectedRegionChanged(SelectedRegionChanged evt)
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

        private void OnSceneTransitionStarted(SceneTransitionStarted evt)
        {
            if (evt.To == SceneNames.RunScene)
            {
                return;
            }

            ClearAppliedModifiers(false);
        }

        private void OnSceneTransitionCompleted(SceneTransitionCompleted evt)
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
                Services.TryGet(out _stack);
            }

            if (_stack == null)
            {
                return;
            }

            string regionId = ResolveRegionId();
            SectorDef def = FindDefinition(regionId);
            _activeRegionId = def.RegionId;

            ClearAppliedModifiers(false);
            ApplyMul("offer_ttl", RunStatId.OfferAcceptTtlMultiplier, def.OfferTtlMul);
            ApplyMul("offer_respawn", RunStatId.OfferRespawnDelayMultiplier, def.OfferRespawnMul);
            ApplyMul("temp_decay", RunStatId.FoodTemperatureDecayMultiplier, def.TempDecayMul);
            ApplyMul("spill_gain", RunStatId.FoodSpillGainMultiplier, def.SpillGainMul);
            ApplyMul("speed", RunStatId.PlayerMoveSpeedMultiplier, def.SpeedMul);
            ApplyMul("turn", RunStatId.BikeTurnSensitivityMultiplier, def.TurnMul);
            ApplyMul("accel", RunStatId.BikeAccelerationMultiplier, def.AccelMul);
            ApplyMul("reward", RunStatId.RewardMultiplier, def.RewardMul);

            ApplySectorVisualOnly(def.RegionId);

            Events.Publish(new RunModifiersChanged { SourceId = "sector_" + def.RegionId });
            Events.Publish(new SectorThemeApplied
            {
                RegionId = def.RegionId,
                DisplayName = def.DisplayName,
                Description = def.Description
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
                Events.Publish(new RunModifiersChanged { SourceId = "sector_clear" });
            }
        }

        private string ResolveRegionId()
        {
            if (_meta == null)
            {
                Services.TryGet(out _meta);
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

        private static SectorDef FindDefinition(string regionId)
        {
            for (int i = 0; i < SectorDefs.Length; i++)
            {
                if (SectorDefs[i].RegionId == regionId)
                {
                    return SectorDefs[i];
                }
            }

            return SectorDefs[0];
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
                // Expanded world mode: keep all region roots visible simultaneously.
                if (!root.activeSelf)
                {
                    root.SetActive(true);
                }
            }
        }
    }
}
