using DeliveryRun.Managers.Core;
using DomainRunSessionEnded = DeliveryRun.Delivery.RunSession.RunSessionEnded;
using DomainRunSessionStarted = DeliveryRun.Delivery.RunSession.RunSessionStarted;

namespace DeliveryRun.Managers.Subs
{
    public sealed class MetaProgressionManager : SubManagerBase
    {
        private MetaProgressionRuntime _runtime;

        public override string Name => nameof(MetaProgressionManager);
        public override int InitOrder => 46;

        protected override void OnInitialize()
        {
            _runtime = new MetaProgressionRuntime(Services, Events);
            _runtime.Initialize();

            Subs.Add<RunReportReady>(Events, OnRunReportReady);
            Subs.Add<PermanentUpgradePurchaseRequested>(Events, OnPermanentUpgradePurchaseRequested);
            Subs.Add<SelectNextRegionRequested>(Events, OnSelectNextRegionRequested);
            Subs.Add<UnlockRegionRequested>(Events, OnUnlockRegionRequested);
            Subs.Add<MetaSaveSlotLoadRequested>(Events, OnMetaSaveSlotLoadRequested);
            Subs.Add<MetaSaveSlotSaveRequested>(Events, OnMetaSaveSlotSaveRequested);
            Subs.Add<DomainRunSessionStarted>(Events, OnRunStarted);
            Subs.Add<DomainRunSessionEnded>(Events, OnRunEnded);
        }

        protected override void OnShutdown()
        {
            _runtime?.Shutdown();
            _runtime = null;
        }

        private void OnRunReportReady(RunReportReady evt)
        {
            _runtime?.OnRunReportReady(evt);
        }

        private void OnPermanentUpgradePurchaseRequested(PermanentUpgradePurchaseRequested evt)
        {
            _runtime?.OnPermanentUpgradePurchaseRequested(evt);
        }

        private void OnSelectNextRegionRequested(SelectNextRegionRequested evt)
        {
            _runtime?.OnSelectNextRegionRequested(evt);
        }

        private void OnUnlockRegionRequested(UnlockRegionRequested evt)
        {
            _runtime?.OnUnlockRegionRequested(evt);
        }

        private void OnMetaSaveSlotLoadRequested(MetaSaveSlotLoadRequested evt)
        {
            _runtime?.OnMetaSaveSlotLoadRequested(evt);
        }

        private void OnMetaSaveSlotSaveRequested(MetaSaveSlotSaveRequested evt)
        {
            _runtime?.OnMetaSaveSlotSaveRequested(evt);
        }

        private void OnRunStarted(DomainRunSessionStarted evt)
        {
            _runtime?.OnRunStarted(evt);
        }

        private void OnRunEnded(DomainRunSessionEnded evt)
        {
            _runtime?.OnRunEnded(evt);
        }
    }
}
