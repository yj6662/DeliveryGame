using System;
using System.Collections.Generic;
using DeliveryRun.Managers.Core;
using DomainRunSessionEnded = DeliveryRun.Delivery.RunSession.RunSessionEnded;
using DomainRunSessionStarted = DeliveryRun.Delivery.RunSession.RunSessionStarted;

namespace DeliveryRun.Managers.Subs
{
    internal sealed partial class MetaProgressionRuntime
    {
        private const int MaxUpgradeLevel = 3;

        private static readonly string[] UpgradeIds = MetaProgressionConstants.UpgradeIds;
        private static readonly RunStatId[] UpgradeStats =
        {
            RunStatId.PlayerMoveSpeedMultiplier,
            RunStatId.BikeTurnSensitivityMultiplier,
            RunStatId.BikeAccelerationMultiplier,
            RunStatId.MusicHighTierChanceMultiplier,
            RunStatId.BikeLateralGripMultiplier,
            RunStatId.BikeBrakeForceMultiplier,
            RunStatId.RewardMultiplier
        };

        private static readonly float[] UpgradePerLevelMulDelta = { 0.06f, 0.08f, 0.07f, 0.15f, 0.05f, 0.05f, 0.04f };
        private static readonly int[] AutoUpgradeLevelCash = { 1800, 6200, 15000 };

        private readonly ServiceRegistry _services;
        private readonly EventBus _events;
        private readonly List<string> _appliedUpgradeSourceIds = new List<string>(8);

        private MetaProgressionService _meta;
        private ModifierStackService _stack;
        private bool _runActive;

        internal MetaProgressionRuntime(ServiceRegistry services, EventBus events)
        {
            _services = services;
            _events = events;
        }

        internal void Initialize()
        {
            _meta = new MetaProgressionService();
            _meta.Load();
            _services.Register(_meta);
            _services.TryGet(out _stack);

            _events.Publish(new MetaBalanceChanged
            {
                TotalCash = _meta.TotalCash,
                Delta = 0
            });

            EvaluateAutomaticUpgrades(false);
            _events.Publish(new SelectedRegionChanged { RegionId = _meta.SelectedRegionId });
            PublishNextRegionUnlockStatus();
        }

        internal void Shutdown()
        {
            RemoveAppliedUpgradeModifiers();
            _runActive = false;
        }

        internal void OnRunStarted(DomainRunSessionStarted evt)
        {
            _runActive = true;
            ApplyPermanentUpgradesToRun();
        }

        internal void OnRunEnded(DomainRunSessionEnded evt)
        {
            _runActive = false;
        }

        private void ApplyPermanentUpgradesToRun()
        {
            if (_stack == null)
            {
                _services.TryGet(out _stack);
            }

            if (_stack == null)
            {
                return;
            }

            RemoveAppliedUpgradeModifiers();

            for (int i = 0; i < UpgradeIds.Length; i++)
            {
                int level = _meta.GetUpgradeLevel(UpgradeIds[i]);
                if (level <= 0)
                {
                    continue;
                }

                float mul = 1f + (UpgradePerLevelMulDelta[i] * level);
                string sourceId = "perm_upgrade_" + UpgradeIds[i];
                _stack.AddOrReplace(new RunModifier(sourceId, UpgradeStats[i], ModifierMode.Mul, mul));
                _appliedUpgradeSourceIds.Add(sourceId);
            }

            _events.Publish(new RunModifiersChanged { SourceId = "permanent_upgrades" });
        }

        private void RemoveAppliedUpgradeModifiers()
        {
            if (_stack == null)
            {
                return;
            }

            for (int i = 0; i < _appliedUpgradeSourceIds.Count; i++)
            {
                _stack.RemoveSource(_appliedUpgradeSourceIds[i]);
            }

            _appliedUpgradeSourceIds.Clear();
        }

        private void EvaluateAutomaticUpgrades(bool publishEvents)
        {
            int total = _meta.TotalCash;
            int targetLevel = 0;
            for (int i = 0; i < AutoUpgradeLevelCash.Length; i++)
            {
                if (total >= AutoUpgradeLevelCash[i])
                {
                    targetLevel = i + 1;
                }
            }

            if (targetLevel <= 0)
            {
                return;
            }

            if (targetLevel > MaxUpgradeLevel)
            {
                targetLevel = MaxUpgradeLevel;
            }

            bool changedAny = false;
            for (int i = 0; i < UpgradeIds.Length; i++)
            {
                int current = _meta.GetUpgradeLevel(UpgradeIds[i]);
                if (current >= targetLevel)
                {
                    continue;
                }

                _meta.SetUpgradeLevel(UpgradeIds[i], targetLevel);
                changedAny = true;
                if (publishEvents)
                {
                    _events.Publish(new PermanentUpgradeChanged
                    {
                        UpgradeId = UpgradeIds[i],
                        Level = targetLevel
                    });
                }
            }

            if (changedAny && _runActive)
            {
                ApplyPermanentUpgradesToRun();
            }
        }

        private static int GetUpgradePrice(int targetLevel)
        {
            if (targetLevel <= 1) return 1200;
            if (targetLevel == 2) return 2800;
            return 5600;
        }

        private static int IndexOfUpgrade(string upgradeId)
        {
            if (string.IsNullOrEmpty(upgradeId))
            {
                return -1;
            }

            for (int i = 0; i < UpgradeIds.Length; i++)
            {
                if (UpgradeIds[i] == upgradeId)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
