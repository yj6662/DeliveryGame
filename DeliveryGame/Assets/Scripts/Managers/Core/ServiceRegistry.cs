using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeliveryRun.Managers.Core
{
    public sealed class ServiceRegistry
    {
        private readonly Dictionary<Type, object> _services = new Dictionary<Type, object>(32);

        public void Register<T>(T instance) where T : class
        {
            Register(typeof(T), instance);
        }

        public void Register(Type serviceType, object instance)
        {
            if (serviceType == null)
            {
                throw new ArgumentNullException(nameof(serviceType));
            }

            if (instance == null)
            {
                throw new ArgumentNullException(nameof(instance));
            }

            if (!serviceType.IsInstanceOfType(instance))
            {
                string mismatch = "[ServiceRegistry] Instance type mismatch. ServiceType=" + serviceType.FullName +
                    ", InstanceType=" + instance.GetType().FullName;
                Debug.LogError(mismatch);
                throw new ArgumentException(mismatch, nameof(instance));
            }

            if (_services.ContainsKey(serviceType))
            {
                string message = "[ServiceRegistry] Service already registered: " + serviceType.FullName;
                Debug.LogError(message);
                throw new InvalidOperationException(message);
            }

            _services.Add(serviceType, instance);
        }

        public bool TryGet<T>(out T service) where T : class
        {
            object value;
            if (TryGet(typeof(T), out value))
            {
                service = value as T;
                return service != null;
            }

            service = null;
            return false;
        }

        public bool TryGet(Type serviceType, out object service)
        {
            if (serviceType == null)
            {
                service = null;
                return false;
            }

            return _services.TryGetValue(serviceType, out service);
        }

        public T GetRequired<T>() where T : class
        {
            T service;
            if (TryGet(out service))
            {
                return service;
            }

            string message = "[ServiceRegistry] Required service is not registered: " + typeof(T).FullName;
            Debug.LogError(message);
            throw new KeyNotFoundException(message);
        }

        public int CopyRegisteredServiceTypeNamesNonAlloc(string[] destination)
        {
            if (destination == null || destination.Length == 0)
            {
                return 0;
            }

            int index = 0;
            foreach (KeyValuePair<Type, object> pair in _services)
            {
                if (index >= destination.Length)
                {
                    break;
                }

                destination[index] = pair.Key.FullName;
                index++;
            }

            return index;
        }
    }
}
