using System;
using UnityEngine;

namespace DeliveryRun.Delivery.Vehicle
{
    public readonly struct BikeCollisionInfo
    {
        public readonly float Impulse;
        public readonly Collision Collision;

        public BikeCollisionInfo(float impulse, Collision collision)
        {
            Impulse = impulse;
            Collision = collision;
        }
    }

    [DisallowMultipleComponent]
    public sealed class BikeCollisionReporter : MonoBehaviour
    {
        public event Action<float> Collided;
        public event Action<BikeCollisionInfo> CollidedDetailed;

        private void OnCollisionEnter(Collision collision)
        {
            if (collision == null)
            {
                return;
            }

            float impulse = collision.impulse.magnitude;

            Action<float> callback = Collided;
            if (callback != null)
            {
                callback(impulse);
            }

            Action<BikeCollisionInfo> detailed = CollidedDetailed;
            if (detailed != null)
            {
                detailed(new BikeCollisionInfo(impulse, collision));
            }
        }
    }
}
