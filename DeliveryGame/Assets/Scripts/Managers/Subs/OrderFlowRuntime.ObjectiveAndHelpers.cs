using DeliveryRun.Delivery.Vehicle;
using DeliveryRun.Managers.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DeliveryRun.Managers.Subs
{
    internal sealed partial class OrderFlowRuntime
    {
        private void PublishPrimaryObjectiveUpdate()
        {
            _objectiveSnapshots.Clear();
            for (int i = 0; i < _activeOrders.Count; i++)
            {
                OrderRuntimeActiveOrder order = _activeOrders[i];
                if (order == null)
                {
                    continue;
                }

                _objectiveSnapshots.Add(new OrderObjectivePublisher.ObjectiveOrderSnapshot
                {
                    OfferId = order.OfferId,
                    PickupName = order.PickupName,
                    DeliveryName = order.DeliveryName,
                    BaseReward = order.BaseReward,
                    IsCarrying = order.Stage == OrderStage.Carrying,
                    PickupInteract = order.PickupInteract,
                    DeliveryInteract = order.DeliveryInteract
                });
            }

            bool hasPlayer = _player != null;
            Vector3 playerPosition = hasPlayer ? _player.transform.position : Vector3.zero;
            _objectivePublisher.Publish(_events, _objectiveSnapshots, hasPlayer, playerPosition);
        }

        private void PublishNoObjectives()
        {
            _objectiveSnapshots.Clear();
            _objectivePublisher.Publish(_events, _objectiveSnapshots, false, Vector3.zero);
        }

        private void EnsureRuntimeReferences()
        {
            if (_player == null)
            {
                _player = Object.FindAnyObjectByType<MotorbikeController>();
            }

            if (_roadQuery == null)
            {
                _services.TryGet(out _roadQuery);
            }

            if (_meta == null)
            {
                _services.TryGet(out _meta);
            }

            _anchorCatalog.EnsureReady(_meta, _services);
        }

        private int FindOrderIndex(int orderId)
        {
            for (int i = 0; i < _activeOrders.Count; i++)
            {
                if (_activeOrders[i].OrderId == orderId)
                {
                    return i;
                }
            }

            return -1;
        }

        private string ResolveCurrentRunRegionId()
        {
            if (_meta == null)
            {
                _services.TryGet(out _meta);
            }

            if (_meta != null && !string.IsNullOrEmpty(_meta.SelectedRegionId))
            {
                if (_meta.IsRegionUnlocked(_meta.SelectedRegionId))
                {
                    return _meta.SelectedRegionId;
                }
            }

            return "central";
        }

        private ModifierStackService ResolveModifierStack()
        {
            if (_stack == null)
            {
                _services.TryGet(out _stack);
            }

            return _stack;
        }
    }
}
