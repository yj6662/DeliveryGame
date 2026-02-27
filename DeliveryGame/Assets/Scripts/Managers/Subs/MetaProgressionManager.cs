using System.Collections.Generic;
using DeliveryRun.Managers.Core;
using UnityEngine;
using DomainRunSessionEnded = DeliveryRun.Delivery.RunSession.RunSessionEnded;
using DomainRunSessionStarted = DeliveryRun.Delivery.RunSession.RunSessionStarted;

namespace DeliveryRun.Managers.Subs
{
    public sealed class MetaProgressionManager : SubManagerBase
    {
        private const int MaxUpgradeLevel = 3;

        private static readonly string[] UpgradeIds =
        {
            "bike_speed",
            "bike_turn",
            "bike_accel",
            "music_luck",
            "bike_grip",
            "bike_brake",
            "reward_bonus"
        };

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

        private static readonly string[] RegionIds =
        {
            "rushdistrict",
            "frostlands",
            "hillcrest",
            "stormcoast",
            "oldtown"
        };

        private static readonly int[] RegionUnlockCash = { 1200, 3000, 5500, 9000, 14000 };
        private static readonly int[] AutoUpgradeLevelCash = { 1800, 6200, 15000 };

        private readonly List<string> _appliedUpgradeSourceIds = new List<string>(8);

        private MetaProgressionService _meta;
        private ModifierStackService _stack;
        private bool _runActive;

        public override string Name => nameof(MetaProgressionManager);
        public override int InitOrder => 46;

        protected override void OnInitialize()
        {
            _meta = new MetaProgressionService();
            _meta.Load();
            Services.Register(_meta);
            Services.TryGet(out _stack);

            Subs.Add<RunReportReady>(Events, OnRunReportReady);
            Subs.Add<PermanentUpgradePurchaseRequested>(Events, OnPermanentUpgradePurchaseRequested);
            Subs.Add<SelectNextRegionRequested>(Events, OnSelectNextRegionRequested);
            Subs.Add<DomainRunSessionStarted>(Events, OnRunStarted);
            Subs.Add<DomainRunSessionEnded>(Events, OnRunEnded);

            Events.Publish(new MetaBalanceChanged
            {
                TotalCash = _meta.TotalCash,
                Delta = 0
            });

            EvaluateRegionUnlocks(false);
            EvaluateAutomaticUpgrades(false);
            Events.Publish(new SelectedRegionChanged { RegionId = _meta.SelectedRegionId });
        }

        protected override void OnShutdown()
        {
            RemoveAppliedUpgradeModifiers();
            _runActive = false;
        }

        private void OnRunReportReady(RunReportReady evt)
        {
            int delta = evt.Report.EarnedCash;
            if (delta <= 0)
            {
                return;
            }

            _meta.AddTotalCash(delta);
            Events.Publish(new MetaBalanceChanged
            {
                TotalCash = _meta.TotalCash,
                Delta = delta
            });

            EvaluateRegionUnlocks(true);
            EvaluateAutomaticUpgrades(true);
        }

        private void OnPermanentUpgradePurchaseRequested(PermanentUpgradePurchaseRequested evt)
        {
            int upgradeIndex = IndexOfUpgrade(evt.UpgradeId);
            if (upgradeIndex < 0)
            {
                return;
            }

            int currentLevel = _meta.GetUpgradeLevel(UpgradeIds[upgradeIndex]);
            if (currentLevel >= MaxUpgradeLevel)
            {
                return;
            }

            int nextLevel = currentLevel + 1;
            int price = GetUpgradePrice(nextLevel);
            if (!_meta.TrySpendTotalCash(price))
            {
                return;
            }

            _meta.SetUpgradeLevel(UpgradeIds[upgradeIndex], nextLevel);
            Events.Publish(new MetaBalanceChanged
            {
                TotalCash = _meta.TotalCash,
                Delta = -price
            });

            Events.Publish(new PermanentUpgradeChanged
            {
                UpgradeId = UpgradeIds[upgradeIndex],
                Level = nextLevel
            });

            if (_runActive)
            {
                ApplyPermanentUpgradesToRun();
            }
        }

        private void OnSelectNextRegionRequested(SelectNextRegionRequested evt)
        {
            if (_meta == null)
            {
                return;
            }

            if (_meta.SelectNextUnlockedRegion())
            {
                Events.Publish(new SelectedRegionChanged { RegionId = _meta.SelectedRegionId });
            }
        }

        private void OnRunStarted(DomainRunSessionStarted evt)
        {
            _runActive = true;
            ApplyPermanentUpgradesToRun();
        }

        private void OnRunEnded(DomainRunSessionEnded evt)
        {
            _runActive = false;
        }

        private void ApplyPermanentUpgradesToRun()
        {
            if (_stack == null)
            {
                Services.TryGet(out _stack);
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

            Events.Publish(new RunModifiersChanged { SourceId = "permanent_upgrades" });
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

        private void EvaluateRegionUnlocks(bool publishEvents)
        {
            int total = _meta.TotalCash;
            for (int i = 0; i < RegionIds.Length; i++)
            {
                if (total < RegionUnlockCash[i])
                {
                    continue;
                }

                if (!_meta.UnlockRegion(RegionIds[i]))
                {
                    continue;
                }

                if (publishEvents)
                {
                    Events.Publish(new RegionUnlocked
                    {
                        RegionId = RegionIds[i],
                        RequiredTotalCash = RegionUnlockCash[i]
                    });
                }
            }
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
                    Events.Publish(new PermanentUpgradeChanged
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
