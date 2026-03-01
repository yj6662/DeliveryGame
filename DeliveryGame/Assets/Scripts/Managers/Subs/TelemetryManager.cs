using DeliveryRun.Managers.Core;
using DomainRunLastOrderStarted = DeliveryRun.Delivery.RunSession.RunLastOrderStarted;
using DomainRunSessionEnded = DeliveryRun.Delivery.RunSession.RunSessionEnded;
using DomainRunSessionStarted = DeliveryRun.Delivery.RunSession.RunSessionStarted;

namespace DeliveryRun.Managers.Subs
{
    public sealed class TelemetryManager : SubManagerBase
    {
        private const int BufferCapacity = 64;

        private readonly string[] _entries = new string[BufferCapacity];
        private int _writeIndex;
        private int _count;
        private int _runSequence;
        private bool _runActive;

        public override string Name => nameof(TelemetryManager);
        public override int InitOrder => 90;

        protected override void OnInitialize()
        {
            ClearBuffer();
            _runSequence = 0;
            _runActive = false;

            Subs.Add<SceneTransitionStarted>(Events, OnSceneTransitionStarted);
            Subs.Add<SceneTransitionCompleted>(Events, OnSceneTransitionCompleted);
            Subs.Add<DomainRunSessionStarted>(Events, OnRunSessionStarted);
            Subs.Add<DomainRunLastOrderStarted>(Events, OnRunSessionLastOrderStarted);
            Subs.Add<DomainRunSessionEnded>(Events, OnRunSessionEnded);
            Subs.Add<DeliveryOrderSpawned>(Events, OnDeliveryOrderSpawned);
            Subs.Add<DeliveryOrderCompleted>(Events, OnDeliveryOrderCompleted);
            Subs.Add<DeliveryOrderFailed>(Events, OnDeliveryOrderFailed);
            Subs.Add<MusicChoiceRequested>(Events, OnMusicChoiceRequested);
            Subs.Add<MusicChoiceApplied>(Events, OnMusicChoiceApplied);
            Subs.Add<RatingChanged>(Events, OnRatingChanged);
            Subs.Add<EconomyChanged>(Events, OnEconomyChanged);

            RunSessionManager runSessionManager;
            if (Services.TryGet(out runSessionManager) && runSessionManager != null && runSessionManager.HasActiveRun)
            {
                _runActive = true;
                _runSequence = 1;
                Add("RunSessionStarted", "run=1");
            }
        }

        protected override void OnShutdown()
        {
            ClearBuffer();
            _runSequence = 0;
            _runActive = false;
        }

        public int CopyRecentEntriesNonAlloc(string[] destination)
        {
            if (destination == null || destination.Length == 0)
            {
                return 0;
            }

            int copyCount = _count < destination.Length ? _count : destination.Length;
            for (int i = 0; i < copyCount; i++)
            {
                int index = _writeIndex - 1 - i;
                if (index < 0)
                {
                    index += BufferCapacity;
                }

                destination[i] = _entries[index];
            }

            return copyCount;
        }

        private void OnSceneTransitionStarted(SceneTransitionStarted evt)
        {
            Add("SceneTransitionStarted", "from=" + evt.From + ",to=" + evt.To);
        }

        private void OnSceneTransitionCompleted(SceneTransitionCompleted evt)
        {
            Add("SceneTransitionCompleted", "scene=" + evt.SceneName);
        }

        private void OnRunSessionStarted(DomainRunSessionStarted evt)
        {
            _runSequence++;
            _runActive = true;
            Add("RunSessionStarted", "run=" + _runSequence);
        }

        private void OnRunSessionLastOrderStarted(DomainRunLastOrderStarted evt)
        {
            if (!_runActive)
            {
                return;
            }

            Add("RunSessionLastOrderStarted", "run=" + _runSequence);
        }

        private void OnRunSessionEnded(DomainRunSessionEnded evt)
        {
            if (!_runActive)
            {
                return;
            }

            Add("RunSessionEnded", "run=" + _runSequence + ",reason=" + evt.Reason);
            _runActive = false;
        }

        private void OnDeliveryOrderSpawned(DeliveryOrderSpawned evt)
        {
            Add("DeliveryOrderSpawned", "order=" + evt.OrderSequence);
        }

        private void OnDeliveryOrderCompleted(DeliveryOrderCompleted evt)
        {
            Add("DeliveryOrderCompleted", "order=" + evt.OrderSequence + ",reward=" + evt.RewardCoins);
        }

        private void OnDeliveryOrderFailed(DeliveryOrderFailed evt)
        {
            Add("DeliveryOrderFailed", "order=" + evt.OrderSequence + ",reason=" + evt.Reason);
        }

        private void OnMusicChoiceRequested(MusicChoiceRequested evt)
        {
            Add("MusicChoiceRequested", "index=" + evt.ChoiceIndex);
        }

        private void OnMusicChoiceApplied(MusicChoiceApplied evt)
        {
            Add("MusicChoiceApplied", "index=" + evt.ChoiceIndex + ",auto=" + evt.AutoSelected);
        }

        private void OnRatingChanged(RatingChanged evt)
        {
            Add("RatingChanged", "value=" + evt.Value.ToString("F2"));
        }

        private void OnEconomyChanged(EconomyChanged evt)
        {
            Add("EconomyChanged", "session=" + evt.SessionCoins + ",total=" + evt.TotalCoins);
        }

        private void Add(string name, string details)
        {
            string entry = Clock.Now.ToString("F2") + " | " + name + " | " + details;
            _entries[_writeIndex] = entry;
            _writeIndex++;
            if (_writeIndex >= BufferCapacity)
            {
                _writeIndex = 0;
            }

            if (_count < BufferCapacity)
            {
                _count++;
            }
        }

        private void ClearBuffer()
        {
            for (int i = 0; i < _entries.Length; i++)
            {
                _entries[i] = null;
            }

            _writeIndex = 0;
            _count = 0;
        }
    }
}
