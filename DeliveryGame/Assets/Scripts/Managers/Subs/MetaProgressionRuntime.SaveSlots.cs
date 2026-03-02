using DeliveryRun.Managers.Core;

namespace DeliveryRun.Managers.Subs
{
    internal sealed partial class MetaProgressionRuntime
    {
        internal void OnMetaSaveSlotLoadRequested(MetaSaveSlotLoadRequested evt)
        {
            if (_meta == null)
            {
                return;
            }

            int requestedSlot = evt.SlotIndex;
            if (!_meta.IsValidSaveSlot(requestedSlot))
            {
                return;
            }

            bool hadData = _meta.SlotHasData(requestedSlot);
            if (!_meta.LoadSlot(requestedSlot))
            {
                return;
            }

            EvaluateAutomaticUpgrades(false);
            if (_runActive)
            {
                ApplyPermanentUpgradesToRun();
            }

            PublishMetaSnapshot();
            _events.Publish(new MetaSaveSlotLoaded
            {
                SlotIndex = _meta.ActiveSaveSlotIndex,
                HadData = hadData
            });
        }

        internal void OnMetaSaveSlotSaveRequested(MetaSaveSlotSaveRequested evt)
        {
            if (_meta == null)
            {
                return;
            }

            if (!_meta.SaveToSlot(evt.SlotIndex))
            {
                return;
            }

            PublishMetaSnapshot();
            _events.Publish(new MetaSaveSlotSaved
            {
                SlotIndex = _meta.ActiveSaveSlotIndex
            });
        }

        private void PublishMetaSnapshot()
        {
            if (_meta == null)
            {
                return;
            }

            _events.Publish(new MetaBalanceChanged
            {
                TotalCash = _meta.TotalCash,
                Delta = 0
            });

            _events.Publish(new SelectedRegionChanged
            {
                RegionId = _meta.SelectedRegionId
            });

            for (int i = 0; i < UpgradeIds.Length; i++)
            {
                _events.Publish(new PermanentUpgradeChanged
                {
                    UpgradeId = UpgradeIds[i],
                    Level = _meta.GetUpgradeLevel(UpgradeIds[i])
                });
            }

            PublishNextRegionUnlockStatus();
        }
    }
}
