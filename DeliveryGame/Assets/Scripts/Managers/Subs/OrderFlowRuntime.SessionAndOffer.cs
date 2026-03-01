using DeliveryRun.Managers.Core;
using UnityEngine;

namespace DeliveryRun.Managers.Subs
{
    internal sealed partial class OrderFlowRuntime
    {
        private void BeginRunSession()
        {
            _runActive = true;
            _ordersCompleted = 0;
            _lastMusicOptionIndex = -1;
            _objectiveTickAccum = 0f;
            _interactController.RetryAccum = 0f;

            CleanupRunState();
            _anchorCatalog.Clear();
            _offerSpawner.Reset();

            _economy.ResetSession();
            _events.Publish(new SessionBalanceChanged
            {
                Balance = 0,
                Delta = 0,
                Reason = "run_start"
            });

            EnsureRuntimeReferences();
            DoSpawnOffer();
        }

        private void DoSpawnOffer()
        {
            _offerSpawner.SpawnOffer(
                _runActive,
                _activeOrders,
                _anchorCatalog,
                ResolveCurrentRunRegionId(),
                ResolveModifierStack(),
                _clock,
                _events);

            if (!_offerSpawner.HasPendingOffer)
            {
                ScheduleRespawnIfNeeded();
            }
        }

        private void TickPendingOffer(float unscaledDeltaTime)
        {
            string offerId;
            float remainingSeconds;
            OrderOfferSpawner.TickResult result =
                _offerSpawner.TickPendingOffer(unscaledDeltaTime, _clock, out offerId, out remainingSeconds);

            if (result == OrderOfferSpawner.TickResult.Expired)
            {
                ExpirePendingOffer();
                return;
            }

            if (result == OrderOfferSpawner.TickResult.Ticked)
            {
                _events.Publish(new OfferTicked
                {
                    OfferId = offerId,
                    RemainingSeconds = remainingSeconds
                });
            }
        }

        private void ExpirePendingOffer()
        {
            string expiredOfferId = _offerSpawner.ClearPendingOffer();
            if (expiredOfferId != null)
            {
                _events.Publish(new OfferExpired { OfferId = expiredOfferId });
            }

            ScheduleRespawnIfNeeded();
        }

        private void ScheduleRespawnIfNeeded()
        {
            _offerSpawner.ScheduleRespawnIfNeeded(
                _runActive,
                _isRunScene,
                _activeOrders,
                ResolveModifierStack(),
                _clock,
                OnRespawnDue);
        }

        private void OnRespawnDue()
        {
            if (!_runActive || !_isRunScene || _offerSpawner.HasPendingOffer || _activeOrders.Count >= MaxActiveOrders)
            {
                return;
            }

            DoSpawnOffer();
        }

        private void CompleteOrderSuccess(int orderIndex, OrderRuntimeActiveOrder order)
        {
            if (order == null)
            {
                return;
            }

            _ordersCompleted++;
            _settlementService.ProcessSuccess(
                _services,
                _events,
                _clock,
                _economy,
                _foodCfg,
                ResolveModifierStack(),
                order.OfferId,
                order.BaseReward,
                order.AcceptedAt,
                order.PickedUpAt);

            _interactController.DespawnPickup(order);
            _interactController.DespawnDelivery(order);
            _activeOrders.RemoveAt(orderIndex);
            ScheduleRespawnIfNeeded();
            PublishPrimaryObjectiveUpdate();
        }

        private void FailOrderByTimeout(int orderIndex, OrderRuntimeActiveOrder order)
        {
            if (order == null)
            {
                return;
            }

            _interactController.DespawnPickup(order);
            _interactController.DespawnDelivery(order);

            if (_activeOrders.Count > orderIndex)
            {
                _activeOrders.RemoveAt(orderIndex);
            }

            _settlementService.ProcessTimeout(
                _services,
                _events,
                order.OfferId,
                order.FoodName,
                order.DeliveryLimitSeconds);
            ScheduleRespawnIfNeeded();
            PublishPrimaryObjectiveUpdate();
        }

        private void HandleSceneChanged(string sceneName)
        {
            bool isRun = SceneNames.IsRunSceneLike(sceneName);
            if (_isRunScene == isRun)
            {
                return;
            }

            _isRunScene = isRun;
            if (!_isRunScene)
            {
                CleanupSceneRefsAndPoints();
                return;
            }

            EnsureRuntimeReferences();
        }

        private void PublishRunReport()
        {
            if (_meta == null)
            {
                _services.TryGet(out _meta);
            }

            RatingManager ratingManager = null;
            _services.TryGet(out ratingManager);

            RunReport report = new RunReport
            {
                EarnedCash = _economy.SessionBalance,
                OrdersCompleted = _ordersCompleted,
                MusicOptionIndex = _lastMusicOptionIndex,
                EndRating = ratingManager != null ? ratingManager.CurrentRating : 0f,
                RegionId = _meta != null ? _meta.SelectedRegionId : "central"
            };

            _events.Publish(new RunReportReady { Report = report });
        }

        private void CleanupSceneRefsAndPoints()
        {
            _offerSpawner.CancelRespawn(_clock);
            _interactController.DespawnAll(_activeOrders);
            PublishNoObjectives();
            _activeOrders.Clear();
            _offerSpawner.ClearPendingOffer();
            _interactController.RetryAccum = 0f;
            _player = null;
            _anchorCatalog.Clear();
        }

        private void CleanupRunState()
        {
            _offerSpawner.CancelRespawn(_clock);
            _interactController.DespawnAll(_activeOrders);
            PublishNoObjectives();
            _activeOrders.Clear();
            _offerSpawner.ClearPendingOffer();
            _objectiveTickAccum = 0f;
            _interactController.RetryAccum = 0f;
        }
    }
}
