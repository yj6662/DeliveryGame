using System;
using DeliveryRun.Managers.Core;

namespace DeliveryRun.Managers.Subs
{
    internal sealed partial class MetaProgressionRuntime
    {
        internal void OnSelectNextRegionRequested(SelectNextRegionRequested evt)
        {
            if (_meta == null)
            {
                return;
            }

            if (_meta.SelectNextUnlockedRegion())
            {
                _events.Publish(new SelectedRegionChanged { RegionId = _meta.SelectedRegionId });
            }
        }

        internal void OnUnlockRegionRequested(UnlockRegionRequested evt)
        {
            if (_meta == null)
            {
                return;
            }

            string regionId = evt.RegionId;
            if (string.IsNullOrEmpty(regionId))
            {
                if (!RegionProgressionCatalog.TryGetNextLockedRegion(_meta, out regionId))
                {
                    PublishNextRegionUnlockStatus();
                    return;
                }
            }

            RegionUnlockRule rule;
            if (!RegionProgressionCatalog.TryGetRule(regionId, out rule))
            {
                _events.Publish(new RegionUnlockFailed
                {
                    RegionId = regionId,
                    Reason = "unknown_region"
                });
                PublishNextRegionUnlockStatus();
                return;
            }

            if (_meta.IsRegionUnlocked(rule.RegionId))
            {
                PublishNextRegionUnlockStatus();
                return;
            }

            if (!_meta.IsRegionUnlocked(rule.PreviousRegionId))
            {
                _events.Publish(new RegionUnlockFailed
                {
                    RegionId = rule.RegionId,
                    Reason = "previous_region_locked"
                });
                PublishNextRegionUnlockStatus();
                return;
            }

            int bestCash = _meta.GetBestRunCash(rule.PreviousRegionId);
            float bestRating = _meta.GetBestRunRating(rule.PreviousRegionId);
            if (bestCash < rule.RequiredRunCash || bestRating < rule.RequiredRunRating)
            {
                _events.Publish(new RegionUnlockFailed
                {
                    RegionId = rule.RegionId,
                    Reason = "requirements_not_met"
                });
                PublishNextRegionUnlockStatus();
                return;
            }

            if (!_meta.TrySpendTotalCash(rule.UnlockCost))
            {
                _events.Publish(new RegionUnlockFailed
                {
                    RegionId = rule.RegionId,
                    Reason = "not_enough_cash"
                });
                PublishNextRegionUnlockStatus();
                return;
            }

            if (_meta.UnlockRegion(rule.RegionId))
            {
                _events.Publish(new MetaBalanceChanged
                {
                    TotalCash = _meta.TotalCash,
                    Delta = -rule.UnlockCost
                });

                _events.Publish(new RegionUnlocked
                {
                    RegionId = rule.RegionId,
                    RequiredTotalCash = rule.UnlockCost
                });
            }

            PublishNextRegionUnlockStatus();
        }

        private void PublishNextRegionUnlockStatus()
        {
            if (_meta == null)
            {
                return;
            }

            string nextRegionId;
            if (!RegionProgressionCatalog.TryGetNextLockedRegion(_meta, out nextRegionId))
            {
                _events.Publish(new RegionUnlockStatusChanged
                {
                    RegionId = string.Empty
                });
                return;
            }

            RegionUnlockRule rule;
            if (!RegionProgressionCatalog.TryGetRule(nextRegionId, out rule))
            {
                return;
            }

            int bestRunCash = _meta.GetBestRunCash(rule.PreviousRegionId);
            float bestRunRating = _meta.GetBestRunRating(rule.PreviousRegionId);
            bool previousUnlocked = _meta.IsRegionUnlocked(rule.PreviousRegionId);
            bool meetsPerformance = previousUnlocked &&
                                    bestRunCash >= rule.RequiredRunCash &&
                                    bestRunRating >= rule.RequiredRunRating;
            bool canAfford = _meta.TotalCash >= rule.UnlockCost;

            _events.Publish(new RegionUnlockStatusChanged
            {
                RegionId = rule.RegionId,
                PreviousRegionId = rule.PreviousRegionId,
                UnlockCost = rule.UnlockCost,
                RequiredRunCash = rule.RequiredRunCash,
                RequiredRunRating = rule.RequiredRunRating,
                BestRunCashInPrevious = bestRunCash,
                BestRunRatingInPrevious = bestRunRating,
                PreviousRegionUnlocked = previousUnlocked,
                MeetsPerformance = meetsPerformance,
                CanAfford = canAfford,
                CanUnlockNow = meetsPerformance && canAfford
            });
        }
    }
}
