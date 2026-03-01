using DeliveryRun.Managers.Core;
using DomainRunEndReason = DeliveryRun.Delivery.RunSession.RunEndReason;
using DomainRunLastOrderStarted = DeliveryRun.Delivery.RunSession.RunLastOrderStarted;
using DomainRunSessionEnded = DeliveryRun.Delivery.RunSession.RunSessionEnded;
using DomainRunSessionStarted = DeliveryRun.Delivery.RunSession.RunSessionStarted;
using DomainRunTimerTicked = DeliveryRun.Delivery.RunSession.RunTimerTicked;

namespace DeliveryRun.Managers.Subs
{
    public sealed class RunSessionLegacyBridgeManager : SubManagerBase
    {
        private bool _hasActiveRun;
        private bool _lastOrderStarted;
        private int _runSequence;

        public override string Name => nameof(RunSessionLegacyBridgeManager);
        public override int InitOrder => 40;

        protected override void OnInitialize()
        {
            _hasActiveRun = false;
            _lastOrderStarted = false;

            Subs.Add<DomainRunSessionStarted>(Events, OnDomainRunSessionStarted);
            Subs.Add<DomainRunTimerTicked>(Events, OnDomainRunTimerTicked);
            Subs.Add<DomainRunLastOrderStarted>(Events, OnDomainRunLastOrderStarted);
            Subs.Add<DomainRunSessionEnded>(Events, OnDomainRunSessionEnded);
        }

        protected override void OnShutdown()
        {
            _hasActiveRun = false;
            _lastOrderStarted = false;
            _runSequence = 0;
        }

        private void OnDomainRunSessionStarted(DomainRunSessionStarted evt)
        {
            _runSequence++;
            _hasActiveRun = true;
            _lastOrderStarted = false;

            Events.Publish(new RunSessionStarted
            {
                RunSequence = _runSequence,
                DurationSeconds = evt.DurationSeconds
            });
        }

        private void OnDomainRunTimerTicked(DomainRunTimerTicked evt)
        {
            if (!_hasActiveRun)
            {
                return;
            }

            Events.Publish(new RunSessionTick
            {
                RunSequence = _runSequence,
                ElapsedSeconds = evt.ElapsedSeconds,
                RemainingSeconds = evt.RemainingSeconds,
                IsLastOrderPhase = _lastOrderStarted
            });
        }

        private void OnDomainRunLastOrderStarted(DomainRunLastOrderStarted evt)
        {
            if (!_hasActiveRun)
            {
                return;
            }

            _lastOrderStarted = true;
            Events.Publish(new RunSessionLastOrderStarted
            {
                RunSequence = _runSequence,
                ElapsedSeconds = evt.AtElapsedSeconds
            });
        }

        private void OnDomainRunSessionEnded(DomainRunSessionEnded evt)
        {
            if (!_hasActiveRun)
            {
                return;
            }

            Events.Publish(new RunSessionEnded
            {
                RunSequence = _runSequence,
                Reason = ToLegacyEndReason(evt.Reason)
            });

            _hasActiveRun = false;
            _lastOrderStarted = false;
        }

        private static RunEndReason ToLegacyEndReason(DomainRunEndReason reason)
        {
            if (reason == DomainRunEndReason.TimeExpired)
            {
                return RunEndReason.TimeUp;
            }

            if (reason == DomainRunEndReason.RatingZero)
            {
                return RunEndReason.RatingDepleted;
            }

            if (reason == DomainRunEndReason.OutOfFuel)
            {
                return RunEndReason.FuelDepleted;
            }

            return RunEndReason.SceneLeft;
        }
    }
}
