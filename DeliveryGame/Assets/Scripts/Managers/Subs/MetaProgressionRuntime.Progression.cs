using System;
using DeliveryRun.Managers.Core;

namespace DeliveryRun.Managers.Subs
{
    internal sealed partial class MetaProgressionRuntime
    {
        internal void OnRunReportReady(RunReportReady evt)
        {
            string runRegion = string.IsNullOrEmpty(evt.Report.RegionId) ? _meta.SelectedRegionId : evt.Report.RegionId;
            _meta.RegisterRegionRunResult(runRegion, evt.Report.EarnedCash, evt.Report.EndRating);

            int delta = evt.Report.EarnedCash;
            if (delta > 0)
            {
                _meta.AddTotalCash(delta);
                _events.Publish(new MetaBalanceChanged
                {
                    TotalCash = _meta.TotalCash,
                    Delta = delta
                });

                EvaluateAutomaticUpgrades(true);
            }

            PublishNextRegionUnlockStatus();
        }

        internal void OnPermanentUpgradePurchaseRequested(PermanentUpgradePurchaseRequested evt)
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
            _events.Publish(new MetaBalanceChanged
            {
                TotalCash = _meta.TotalCash,
                Delta = -price
            });

            _events.Publish(new PermanentUpgradeChanged
            {
                UpgradeId = UpgradeIds[upgradeIndex],
                Level = nextLevel
            });

            if (_runActive)
            {
                ApplyPermanentUpgradesToRun();
            }

            PublishNextRegionUnlockStatus();
        }
    }
}
