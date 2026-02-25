using DeliveryRun.Managers.Core;

namespace DeliveryRun.Managers.Subs
{
    public sealed class EconomyManager : SubManagerBase
    {
        private bool _runActive;
        private int _sessionCoins;
        private int _totalCoins;

        public override string Name => nameof(EconomyManager);
        public override int InitOrder => 50;

        public bool IsRunActive => _runActive;
        public int SessionCoins => _sessionCoins;
        public int TotalCoins => _totalCoins;

        protected override void OnInitialize()
        {
            _runActive = false;
            _sessionCoins = 0;
            _totalCoins = 0;

            Subs.Add<RunSessionStarted>(Events, OnRunSessionStarted);
            Subs.Add<RunSessionEnded>(Events, OnRunSessionEnded);
            Subs.Add<DeliveryOrderCompleted>(Events, OnDeliveryOrderCompleted);
        }

        protected override void OnShutdown()
        {
            _runActive = false;
            _sessionCoins = 0;
            _totalCoins = 0;
        }

        private void OnRunSessionStarted(RunSessionStarted evt)
        {
            _runActive = true;
            _sessionCoins = 0;
            PublishChanged(0);
        }

        private void OnRunSessionEnded(RunSessionEnded evt)
        {
            if (_runActive)
            {
                _totalCoins += _sessionCoins;
                _sessionCoins = 0;
            }

            _runActive = false;
            PublishChanged(0);
        }

        private void OnDeliveryOrderCompleted(DeliveryOrderCompleted evt)
        {
            if (!_runActive)
            {
                return;
            }

            if (evt.RewardCoins <= 0)
            {
                return;
            }

            _sessionCoins += evt.RewardCoins;
            PublishChanged(evt.RewardCoins);
        }

        private void PublishChanged(int delta)
        {
            Events.Publish(new EconomyChanged
            {
                SessionCoins = _sessionCoins,
                TotalCoins = _totalCoins,
                Delta = delta,
                IsRunActive = _runActive
            });
        }
    }
}
