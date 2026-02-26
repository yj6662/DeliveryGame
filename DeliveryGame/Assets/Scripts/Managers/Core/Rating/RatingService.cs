using UnityEngine;

namespace DeliveryRun.Managers.Core
{
    public sealed class RatingService
    {
        public float Rating { get; private set; }

        public void Reset(float start)
        {
            Rating = Mathf.Clamp(start, 0f, 5f);
        }

        public float ApplyDelta(float delta)
        {
            Rating = Mathf.Clamp(Rating + delta, 0f, 5f);
            return Rating;
        }
    }
}
