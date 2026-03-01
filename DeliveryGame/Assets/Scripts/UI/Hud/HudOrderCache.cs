using System.Collections.Generic;

namespace DeliveryRun.Managers.Subs
{
    internal sealed class HudOrderCache
    {
        private readonly Dictionary<string, string> _pickupNames;
        private readonly Dictionary<string, string> _deliveryNames;
        private readonly Dictionary<string, int> _baseRewards;
        private readonly Dictionary<string, bool> _carryingByOffer;

        internal HudOrderCache(int capacity)
        {
            int size = capacity > 0 ? capacity : 1;
            _pickupNames = new Dictionary<string, string>(size);
            _deliveryNames = new Dictionary<string, string>(size);
            _baseRewards = new Dictionary<string, int>(size);
            _carryingByOffer = new Dictionary<string, bool>(size);
        }

        internal Dictionary<string, bool> CarryingByOffer => _carryingByOffer;

        internal void RecordOffer(string offerId, string pickupName, string deliveryName, int baseReward)
        {
            if (string.IsNullOrEmpty(offerId))
            {
                return;
            }

            _pickupNames[offerId] = pickupName ?? string.Empty;
            _deliveryNames[offerId] = deliveryName ?? string.Empty;
            _baseRewards[offerId] = baseReward;
        }

        internal string ResolvePickupName(string offerId, string fallback)
        {
            string pickupName;
            return _pickupNames.TryGetValue(offerId, out pickupName) ? pickupName : fallback;
        }

        internal string ResolveDeliveryName(string offerId, string fallback)
        {
            string deliveryName;
            return _deliveryNames.TryGetValue(offerId, out deliveryName) ? deliveryName : fallback;
        }

        internal int ResolveBaseReward(string offerId, int fallback)
        {
            int baseReward;
            if (_baseRewards.TryGetValue(offerId, out baseReward) && baseReward > 0)
            {
                return baseReward;
            }

            return fallback;
        }

        internal void SetCarrying(string offerId, bool carrying)
        {
            if (string.IsNullOrEmpty(offerId))
            {
                return;
            }

            _carryingByOffer[offerId] = carrying;
        }

        internal void RemoveOffer(string offerId)
        {
            if (string.IsNullOrEmpty(offerId))
            {
                return;
            }

            _pickupNames.Remove(offerId);
            _deliveryNames.Remove(offerId);
            _baseRewards.Remove(offerId);
            _carryingByOffer.Remove(offerId);
        }

        internal void Clear()
        {
            _pickupNames.Clear();
            _deliveryNames.Clear();
            _baseRewards.Clear();
            _carryingByOffer.Clear();
        }
    }
}
