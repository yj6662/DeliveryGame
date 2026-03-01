using DeliveryRun.Managers.Core;
using UnityEngine;
using DomainRunSessionEnded = DeliveryRun.Delivery.RunSession.RunSessionEnded;
using DomainRunSessionStarted = DeliveryRun.Delivery.RunSession.RunSessionStarted;

namespace DeliveryRun.Managers.Subs
{
    public sealed class DeliveryManager : SubManagerBase
    {
        private const float DemoOrderSpawnDelaySeconds = 2f;
        private const float DemoOrderTimeLimitSeconds = 12f;
        private const int DemoOrderRewardCoins = 120;

        private bool _runActive;
        private bool _orderActive;
        private int _runSequence;
        private int _orderSequence;
        private float _spawnCountdown;
        private float _remainingSeconds;

        public override string Name => nameof(DeliveryManager);
        public override int InitOrder => 40;

        public bool IsRunActive => _runActive;
        public bool HasActiveOrder => _orderActive;
        public int ActiveOrderSequence => _orderActive ? _orderSequence : 0;
        public float ActiveOrderRemainingSeconds => _orderActive ? _remainingSeconds : 0f;

        protected override void OnInitialize()
        {
            Subs.Add<DomainRunSessionStarted>(Events, OnRunSessionStarted);
            Subs.Add<DomainRunSessionEnded>(Events, OnRunSessionEnded);
            ResetState(clearRunSequence: false);
        }

        protected override void OnTick(float unscaledDeltaTime)
        {
            if (!_runActive)
            {
                return;
            }

            float dt = unscaledDeltaTime;
            if (dt < 0f)
            {
                dt = 0f;
            }

            if (_orderActive)
            {
                _remainingSeconds -= dt;
                if (_remainingSeconds <= 0f)
                {
                    FailActiveOrderInternal(DeliveryFailReason.Timeout);
                }

                return;
            }

            _spawnCountdown -= dt;
            if (_spawnCountdown <= 0f)
            {
                SpawnDemoOrder();
            }
        }

        protected override void OnShutdown()
        {
            ResetState(clearRunSequence: true);
        }

        public void CompleteActiveOrder()
        {
            if (!_runActive || !_orderActive)
            {
                return;
            }

            int completedOrder = _orderSequence;
            _orderActive = false;
            _remainingSeconds = 0f;
            _spawnCountdown = DemoOrderSpawnDelaySeconds;

            Events.Publish(new DeliveryOrderCompleted
            {
                OrderSequence = completedOrder,
                RewardCoins = DemoOrderRewardCoins
            });
        }

        public void FailActiveOrder(DeliveryFailReason reason)
        {
            if (!_runActive || !_orderActive)
            {
                return;
            }

            if (reason == DeliveryFailReason.None)
            {
                reason = DeliveryFailReason.ManualAbort;
            }

            FailActiveOrderInternal(reason);
        }

        private void OnRunSessionStarted(DomainRunSessionStarted evt)
        {
            BeginRunSession();
        }

        private void OnRunSessionEnded(DomainRunSessionEnded evt)
        {
            EndRunSession();
        }

        private void BeginRunSession()
        {
            _runSequence++;
            _runActive = true;
            _orderActive = false;
            _spawnCountdown = DemoOrderSpawnDelaySeconds;
            _remainingSeconds = 0f;

            Events.Publish(new DeliveryRunStarted { RunSequence = _runSequence });
        }

        private void EndRunSession()
        {
            if (_runActive)
            {
                Events.Publish(new DeliveryRunEnded { RunSequence = _runSequence });
            }

            _runActive = false;
            _orderActive = false;
            _spawnCountdown = 0f;
            _remainingSeconds = 0f;
        }

        private void SpawnDemoOrder()
        {
            _orderSequence++;
            _orderActive = true;
            _remainingSeconds = DemoOrderTimeLimitSeconds;
            _spawnCountdown = 0f;

            Events.Publish(new DeliveryOrderSpawned
            {
                OrderSequence = _orderSequence,
                TimeLimitSeconds = DemoOrderTimeLimitSeconds
            });
        }

        private void FailActiveOrderInternal(DeliveryFailReason reason)
        {
            int failedOrder = _orderSequence;
            _orderActive = false;
            _remainingSeconds = 0f;
            _spawnCountdown = DemoOrderSpawnDelaySeconds;

            Events.Publish(new DeliveryOrderFailed
            {
                OrderSequence = failedOrder,
                Reason = reason
            });
        }

        private void ResetState(bool clearRunSequence)
        {
            _runActive = false;
            _orderActive = false;
            _spawnCountdown = 0f;
            _remainingSeconds = 0f;

            if (clearRunSequence)
            {
                _runSequence = 0;
                _orderSequence = 0;
            }
        }
    }
}
