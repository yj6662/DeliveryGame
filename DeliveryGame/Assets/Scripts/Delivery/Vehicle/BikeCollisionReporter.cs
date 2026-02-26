using System;
using UnityEngine;

namespace DeliveryRun.Delivery.Vehicle
{
    [DisallowMultipleComponent]
    public sealed class BikeCollisionReporter : MonoBehaviour
    {
        public event Action<float> Collided;

        private void OnCollisionEnter(Collision collision)
        {
            if (collision == null)
            {
                return;
            }

            Action<float> callback = Collided;
            if (callback != null)
            {
                callback(collision.impulse.magnitude);
            }
        }
    }
}
