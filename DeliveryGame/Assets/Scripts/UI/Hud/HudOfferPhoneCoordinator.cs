using DeliveryRun.Managers.Core;
using DeliveryRun.UI.Run;

namespace DeliveryRun.Managers.Subs
{
    internal sealed class HudOfferPhoneCoordinator
    {
        private readonly HudPhoneState _state;
        private HudPhonePanel _panel;

        internal HudOfferPhoneCoordinator(int maxTrackedOrders)
        {
            _state = new HudPhoneState(maxTrackedOrders);
        }

        internal int SessionBonus => _state.SessionBonus;

        internal void ResetAll()
        {
            _state.ResetAll();
            _panel?.ResetRuntimeState();
            MarkDirty();
        }

        internal void ResetForSceneExit()
        {
            _state.ResetForSceneExit();
            _panel?.ResetRuntimeState();
            MarkDirty();
        }

        internal void ResetForRunSession()
        {
            _state.ResetForRunSession();
            _panel?.ResetRuntimeState();
            MarkDirty();
        }

        internal void OnMusicChoiceModalStateChanged(bool isOpen, double now)
        {
            _state.OnMusicChoiceModalStateChanged(isOpen, now);
        }

        internal void OnOfferSpawned(OfferSpawned evt, double now)
        {
            _state.OnOfferSpawned(evt, now);
            MarkDirty();
        }

        internal void OnOfferTicked(OfferTicked evt)
        {
            if (_state.OnOfferTicked(evt))
            {
                MarkDirty();
            }
        }

        internal void OnOfferExpired(OfferExpired evt)
        {
            if (_state.OnOfferExpired(evt))
            {
                MarkDirty();
            }
        }

        internal void OnOfferAccepted(OfferAccepted evt)
        {
            _state.OnOfferAccepted(evt);
            MarkDirty();
        }

        internal void OnOrderPickupReached(OrderPickupReached evt)
        {
            _state.OnOrderPickupReached(evt);
            MarkDirty();
        }

        internal void OnOrderCompleted(OrderCompleted evt)
        {
            _state.OnOrderCompleted(evt);
            MarkDirty();
        }

        internal void OnOrderTimedOut(OrderTimedOut evt)
        {
            _state.OnOrderTimedOut(evt);
            MarkDirty();
        }

        internal void OnFoodStateTicked(FoodStateTicked evt)
        {
            _state.OnFoodStateTicked(evt);
            MarkDirty();
        }

        internal void OnOrderObjectiveUpdated(OrderObjectiveUpdated evt)
        {
            if (_state.OnOrderObjectiveUpdated(evt))
            {
                MarkDirty();
            }
        }

        internal bool TryConsumeOfferAccept(bool musicChoiceModalOpen, out string offerId)
        {
            return _state.TryConsumeOfferAccept(musicChoiceModalOpen, out offerId);
        }

        internal void TickOfferCountdown(double now)
        {
            if (_state.TickOfferCountdown(now))
            {
                MarkDirty();
            }
        }

        internal void BuildUiIfNeeded(RunHudView view)
        {
            if (_panel == null)
            {
                _panel = new HudPhonePanel(_state.ActiveOrders, _state.CarryingByOffer, _state.FoodStateByOffer);
            }

            _panel.BuildIfNeeded(view);
        }

        internal void UpdateUiLayout(float dt)
        {
            if (_panel == null)
            {
                return;
            }

            _panel.UpdateLayout(dt, _state.BuildViewState());
        }

        internal void RefreshUiText(bool force)
        {
            if (_panel == null)
            {
                return;
            }

            _panel.RebuildTextIfNeeded(force, _state.BuildViewState());
        }

        internal void Cleanup()
        {
            _panel?.Cleanup();
            _panel = null;
        }

        private void MarkDirty()
        {
            _panel?.SetDirty();
        }
    }
}
