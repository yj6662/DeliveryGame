using DeliveryRun;
using DeliveryRun.Managers.Core;
using DomainRunLastOrderStarted = DeliveryRun.Delivery.RunSession.RunLastOrderStarted;
using DomainRunTimerTicked = DeliveryRun.Delivery.RunSession.RunTimerTicked;

namespace DeliveryRun.UI.Features
{
    internal sealed class UiStateFeature : UiFeatureBase
    {
        private const double ToastDurationSeconds = 2.0d;

        private string _currentScreen;
        private string _lastToast;
        private double _toastExpireAt;
        private int _activeOrderCount;
        private int _completedOrderCount;
        private int _failedOrderCount;
        private float _runRemainingSeconds;
        private float _ratingValue;
        private int _sessionCoins;
        private int _totalCoins;
        private int _pendingMusicChoiceIndex;

        internal string CurrentScreen => _currentScreen;
        internal string LastToast => _lastToast;
        internal int ActiveOrderCount => _activeOrderCount;
        internal int CompletedOrderCount => _completedOrderCount;
        internal int FailedOrderCount => _failedOrderCount;
        internal float RunRemainingSeconds => _runRemainingSeconds;
        internal float RatingValue => _ratingValue;
        internal int SessionCoins => _sessionCoins;
        internal int TotalCoins => _totalCoins;
        internal int PendingMusicChoiceIndex => _pendingMusicChoiceIndex;

        protected override void OnInitialize()
        {
            _currentScreen = SceneNames.CoreScene;
            _lastToast = null;
            _toastExpireAt = 0d;
            _activeOrderCount = 0;
            _completedOrderCount = 0;
            _failedOrderCount = 0;
            _runRemainingSeconds = 0f;
            _ratingValue = 5f;
            _sessionCoins = 0;
            _totalCoins = 0;
            _pendingMusicChoiceIndex = -1;

            Subs.Add<SceneTransitionStarted>(Events, OnSceneTransitionStarted);
            Subs.Add<SceneTransitionCompleted>(Events, OnSceneTransitionCompleted);
            Subs.Add<DomainRunTimerTicked>(Events, OnRunTimerTicked);
            Subs.Add<DomainRunLastOrderStarted>(Events, OnRunLastOrderStarted);
            Subs.Add<DeliveryOrderSpawned>(Events, OnDeliveryOrderSpawned);
            Subs.Add<DeliveryOrderCompleted>(Events, OnDeliveryOrderCompleted);
            Subs.Add<DeliveryOrderFailed>(Events, OnDeliveryOrderFailed);
            Subs.Add<RatingChanged>(Events, OnRatingChanged);
            Subs.Add<EconomyChanged>(Events, OnEconomyChanged);
            Subs.Add<MusicChoiceRequested>(Events, OnMusicChoiceRequested);
            Subs.Add<MusicChoiceApplied>(Events, OnMusicChoiceApplied);
        }

        protected override void OnTick(float unscaledDeltaTime)
        {
            if (_lastToast == null)
            {
                return;
            }

            if (Clock.Now >= _toastExpireAt)
            {
                _lastToast = null;
                _toastExpireAt = 0d;
            }
        }

        protected override void OnShutdown()
        {
            _currentScreen = null;
            _lastToast = null;
            _toastExpireAt = 0d;
            _activeOrderCount = 0;
            _completedOrderCount = 0;
            _failedOrderCount = 0;
            _runRemainingSeconds = 0f;
            _ratingValue = 5f;
            _sessionCoins = 0;
            _totalCoins = 0;
            _pendingMusicChoiceIndex = -1;
        }

        private void OnSceneTransitionStarted(SceneTransitionStarted evt)
        {
            _currentScreen = SceneNames.LoadingScene;
            PushToast("Loading...");
        }

        private void OnSceneTransitionCompleted(SceneTransitionCompleted evt)
        {
            _currentScreen = evt.SceneName;

            if (evt.SceneName == SceneNames.LobbyScene)
            {
                PushToast("Lobby Ready");
                return;
            }

            if (evt.SceneName == SceneNames.RunScene)
            {
                PushToast("Run Started");
                _activeOrderCount = 0;
                _runRemainingSeconds = 420f;
            }
        }

        private void OnRunTimerTicked(DomainRunTimerTicked evt)
        {
            _runRemainingSeconds = evt.RemainingSeconds;
        }

        private void OnRunLastOrderStarted(DomainRunLastOrderStarted evt)
        {
            PushToast("Last Order Start");
        }

        private void OnDeliveryOrderSpawned(DeliveryOrderSpawned evt)
        {
            _activeOrderCount++;
            PushToast("New Order #" + evt.OrderSequence);
        }

        private void OnDeliveryOrderCompleted(DeliveryOrderCompleted evt)
        {
            if (_activeOrderCount > 0)
            {
                _activeOrderCount--;
            }

            _completedOrderCount++;
            PushToast("Order #" + evt.OrderSequence + " Completed");
        }

        private void OnDeliveryOrderFailed(DeliveryOrderFailed evt)
        {
            if (_activeOrderCount > 0)
            {
                _activeOrderCount--;
            }

            _failedOrderCount++;
            PushToast("Order #" + evt.OrderSequence + " Failed");
        }

        private void OnRatingChanged(RatingChanged evt)
        {
            _ratingValue = evt.Value;
            if (evt.Value <= 1.0f)
            {
                PushToast("Rating Critical");
            }
        }

        private void OnEconomyChanged(EconomyChanged evt)
        {
            _sessionCoins = evt.SessionCoins;
            _totalCoins = evt.TotalCoins;
        }

        private void OnMusicChoiceRequested(MusicChoiceRequested evt)
        {
            _pendingMusicChoiceIndex = evt.ChoiceIndex;
            PushToast("Music Choice #" + (evt.ChoiceIndex + 1));
        }

        private void OnMusicChoiceApplied(MusicChoiceApplied evt)
        {
            _pendingMusicChoiceIndex = -1;
            PushToast("Modifier Applied");
        }

        private void PushToast(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                return;
            }

            _lastToast = message;
            _toastExpireAt = Clock.Now + ToastDurationSeconds;
        }
    }
}
