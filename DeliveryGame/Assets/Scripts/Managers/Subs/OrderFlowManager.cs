using DeliveryRun.Delivery.Orders;
using DeliveryRun.Managers.Core;
using DomainRunSessionEnded = DeliveryRun.Delivery.RunSession.RunSessionEnded;
using DomainRunSessionStarted = DeliveryRun.Delivery.RunSession.RunSessionStarted;

namespace DeliveryRun.Managers.Subs
{
    public sealed class OrderFlowManager : SubManagerBase
    {
        private OrderFlowRuntime _runtime;

        public override string Name => nameof(OrderFlowManager);
        public override int InitOrder => 70;

        protected override void OnInitialize()
        {
            _runtime = new OrderFlowRuntime(Services, Events, Clock);
            _runtime.Initialize();

            Subs.Add<DomainRunSessionStarted>(Events, OnRunStarted);
            Subs.Add<DomainRunSessionEnded>(Events, OnRunEnded);
            Subs.Add<AcceptOfferRequested>(Events, OnAcceptRequested);
            Subs.Add<OrderInteractRequested>(Events, OnOrderInteractRequested);
            Subs.Add<MusicChoiceSelected>(Events, OnMusicChoiceSelected);
            Subs.Add<MusicChoiceModalStateChanged>(Events, OnMusicChoiceModalStateChanged);
            Subs.Add<SceneTransitionStarted>(Events, OnSceneTransitionStarted);
            Subs.Add<SceneTransitionCompleted>(Events, OnSceneTransitionCompleted);
        }

        protected override void OnTick(float unscaledDeltaTime)
        {
            _runtime?.Tick(unscaledDeltaTime);
        }

        protected override void OnShutdown()
        {
            _runtime?.Shutdown();
            _runtime = null;
        }

        public int CopyActiveOrderTimerViewsNonAlloc(ActiveOrderTimerView[] destination)
        {
            return _runtime != null ? _runtime.CopyActiveOrderTimerViewsNonAlloc(destination) : 0;
        }

        private void OnRunStarted(DomainRunSessionStarted evt)
        {
            _runtime?.OnRunStarted(evt);
        }

        private void OnRunEnded(DomainRunSessionEnded evt)
        {
            _runtime?.OnRunEnded(evt);
        }

        private void OnAcceptRequested(AcceptOfferRequested evt)
        {
            _runtime?.OnAcceptRequested(evt);
        }

        private void OnOrderInteractRequested(OrderInteractRequested evt)
        {
            _runtime?.OnOrderInteractRequested(evt);
        }

        private void OnMusicChoiceSelected(MusicChoiceSelected evt)
        {
            _runtime?.OnMusicChoiceSelected(evt);
        }

        private void OnMusicChoiceModalStateChanged(MusicChoiceModalStateChanged evt)
        {
            _runtime?.OnMusicChoiceModalStateChanged(evt);
        }

        private void OnSceneTransitionStarted(SceneTransitionStarted evt)
        {
            _runtime?.OnSceneTransitionStarted(evt);
        }

        private void OnSceneTransitionCompleted(SceneTransitionCompleted evt)
        {
            _runtime?.OnSceneTransitionCompleted(evt);
        }
    }
}
