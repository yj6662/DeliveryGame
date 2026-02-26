using System;
using System.Collections.Generic;
using DeliveryRun.Managers.Core;
using UnityEngine;
#if DELIVERYRUN_ADDRESSABLES
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
#endif

namespace DeliveryRun.Managers.Subs
{
    public sealed class AddressablesService : SubManagerBase
    {
#if DELIVERYRUN_ADDRESSABLES
        private readonly Dictionary<string, AsyncOperationHandle> _audioHandlesByKey =
            new Dictionary<string, AsyncOperationHandle>(16);
        private readonly Dictionary<GameObject, AsyncOperationHandle<GameObject>> _instanceHandlesByObject =
            new Dictionary<GameObject, AsyncOperationHandle<GameObject>>(16);
#endif

        public override string Name => nameof(AddressablesService);
        public override int InitOrder => 30;
        public bool IsAvailable
        {
            get
            {
#if DELIVERYRUN_ADDRESSABLES
                return true;
#else
                return false;
#endif
            }
        }

        protected override void OnShutdown()
        {
            ReleaseAllCached();
        }

        public void InstantiatePrefab(string key, Transform parent, Action<GameObject> onDone)
        {
            if (string.IsNullOrEmpty(key))
            {
                Debug.LogError("[AddressablesService] Prefab key is null or empty.");
                onDone?.Invoke(null);
                return;
            }

#if DELIVERYRUN_ADDRESSABLES
            AsyncOperationHandle<GameObject> handle = Addressables.InstantiateAsync(key, parent);
            handle.Completed += h =>
            {
                if (h.Status == AsyncOperationStatus.Succeeded && h.Result != null)
                {
                    _instanceHandlesByObject[h.Result] = h;
                    onDone?.Invoke(h.Result);
                    return;
                }

                Debug.LogError("[AddressablesService] Instantiate failed for key: " + key);
                if (h.IsValid())
                {
                    Addressables.Release(h);
                }

                onDone?.Invoke(null);
            };
#else
            Debug.LogError("[AddressablesService] Addressables not available. key=" + key);
            onDone?.Invoke(null);
#endif
        }

        public void ReleaseInstance(GameObject instance)
        {
            if (instance == null)
            {
                return;
            }

#if DELIVERYRUN_ADDRESSABLES
            _instanceHandlesByObject.Remove(instance);

            bool released = false;
            try
            {
                released = Addressables.ReleaseInstance(instance);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }

            if (!released)
            {
                UnityEngine.Object.Destroy(instance);
            }
#else
            UnityEngine.Object.Destroy(instance);
#endif
        }

        public void LoadAudioClip(string key, Action<AudioClip> onDone)
        {
            if (string.IsNullOrEmpty(key))
            {
                Debug.LogError("[AddressablesService] Audio key is null or empty.");
                onDone?.Invoke(null);
                return;
            }

#if DELIVERYRUN_ADDRESSABLES
            AsyncOperationHandle existingHandle;
            if (_audioHandlesByKey.TryGetValue(key, out existingHandle) && existingHandle.IsValid())
            {
                if (existingHandle.IsDone)
                {
                    if (existingHandle.Status == AsyncOperationStatus.Succeeded)
                    {
                        onDone?.Invoke(existingHandle.Result as AudioClip);
                    }
                    else
                    {
                        onDone?.Invoke(null);
                    }

                    return;
                }

                existingHandle.Completed += op =>
                {
                    if (op.Status == AsyncOperationStatus.Succeeded)
                    {
                        onDone?.Invoke(op.Result as AudioClip);
                    }
                    else
                    {
                        onDone?.Invoke(null);
                    }
                };
                return;
            }

            AsyncOperationHandle<AudioClip> handle = Addressables.LoadAssetAsync<AudioClip>(key);
            _audioHandlesByKey[key] = handle;
            handle.Completed += h =>
            {
                if (h.Status == AsyncOperationStatus.Succeeded)
                {
                    onDone?.Invoke(h.Result);
                    return;
                }

                Debug.LogError("[AddressablesService] Audio load failed for key: " + key);
                _audioHandlesByKey.Remove(key);
                if (h.IsValid())
                {
                    Addressables.Release(h);
                }

                onDone?.Invoke(null);
            };
#else
            Debug.LogError("[AddressablesService] Addressables not available. key=" + key);
            onDone?.Invoke(null);
#endif
        }

        public void ReleaseAllCached()
        {
#if DELIVERYRUN_ADDRESSABLES
            if (_instanceHandlesByObject.Count > 0)
            {
                var releaseInstances = new List<GameObject>(_instanceHandlesByObject.Keys);
                for (int i = 0; i < releaseInstances.Count; i++)
                {
                    GameObject instance = releaseInstances[i];
                    if (instance == null)
                    {
                        continue;
                    }

                    try
                    {
                        Addressables.ReleaseInstance(instance);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogException(ex);
                        UnityEngine.Object.Destroy(instance);
                    }
                }

                _instanceHandlesByObject.Clear();
            }

            if (_audioHandlesByKey.Count > 0)
            {
                var releaseKeys = new List<string>(_audioHandlesByKey.Keys);
                for (int i = 0; i < releaseKeys.Count; i++)
                {
                    string key = releaseKeys[i];
                    AsyncOperationHandle handle;
                    if (!_audioHandlesByKey.TryGetValue(key, out handle))
                    {
                        continue;
                    }

                    if (handle.IsValid())
                    {
                        Addressables.Release(handle);
                    }
                }

                _audioHandlesByKey.Clear();
            }
#endif
        }
    }
}
