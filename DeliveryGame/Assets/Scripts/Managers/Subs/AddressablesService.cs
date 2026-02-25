using System;
using System.Collections.Generic;
using DeliveryRun.Managers.Core;
using UnityEngine;

namespace DeliveryRun.Managers.Subs
{
    public sealed class AddressablesService : SubManagerBase
    {
        private readonly Dictionary<string, UnityEngine.Object> _loadedAssets =
            new Dictionary<string, UnityEngine.Object>(32);

        public override string Name => nameof(AddressablesService);
        public override int InitOrder => 30;

        protected override void OnInitialize()
        {
        }

        protected override void OnShutdown()
        {
            _loadedAssets.Clear();
        }

        public void LoadAssetAsync<T>(string key, Action<T> onCompleted) where T : UnityEngine.Object
        {
            if (string.IsNullOrEmpty(key))
            {
                Debug.LogError("[AddressablesService] Key is null or empty.");
                if (onCompleted != null)
                {
                    onCompleted(default(T));
                }

                return;
            }

            // Stub path: keeps behavior deterministic even when Addressables package is absent.
            T loaded = Resources.Load<T>(key);
            if (loaded == null)
            {
                Debug.LogError("[AddressablesService] Failed to load key: " + key);
            }
            else
            {
                _loadedAssets[key] = loaded;
            }

            if (onCompleted != null)
            {
                onCompleted(loaded);
            }
        }

        public void Release(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return;
            }

            _loadedAssets.Remove(key);
        }
    }
}
