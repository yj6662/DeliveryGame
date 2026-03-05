using System.Collections;
using DeliveryRun.Managers.Core;
using DeliveryRun.Managers.Subs;
using UnityEngine;

namespace DeliveryRun.UI.Lobby
{
    [DisallowMultipleComponent]
    public sealed class LobbyUIBootstrap : MonoBehaviour
    {
        public const string DefaultAddressKey = "ui/lobby/lobby_ui_root";

        [SerializeField] private string addressKey = DefaultAddressKey;
        [SerializeField] private string resourcesPrefabPath = "UI/Lobby/LobbyUIRoot";
        [SerializeField] private GameObject fallbackPrefab;

        private GameObject _spawnedRoot;
        private bool _spawnedByAddressables;
        private bool _started;

        private void Awake()
        {
            UiEventSystemBootstrap.EnsureNow();
        }

        private void Start()
        {
            if (_started)
            {
                return;
            }

            _started = true;
            StartCoroutine(BootstrapRoutine());
        }

        private void OnDestroy()
        {
            if (!_spawnedByAddressables || _spawnedRoot == null)
            {
                return;
            }

            CoreRoot root = CoreRoot.Instance;
            AddressablesService addressables;
            if (root != null && root.Services != null && root.Services.TryGet(out addressables) && addressables != null)
            {
                addressables.ReleaseInstance(_spawnedRoot);
            }

            _spawnedRoot = null;
            _spawnedByAddressables = false;
        }

        public void ConfigureForEditor(GameObject fallback, string key)
        {
            fallbackPrefab = fallback;
            addressKey = string.IsNullOrEmpty(key) ? DefaultAddressKey : key;
        }

        private IEnumerator BootstrapRoutine()
        {
            LobbyUIRootView existingView = FindFirstObjectByType<LobbyUIRootView>();
            if (existingView != null)
            {
                _spawnedRoot = existingView.gameObject;
                EnsureController(_spawnedRoot);
                yield break;
            }

            yield return InstantiateFromAddressablesIfPossible();

            if (_spawnedRoot == null)
            {
                _spawnedRoot = InstantiateFromFallback();
                _spawnedByAddressables = false;
            }

            if (_spawnedRoot == null)
            {
                Debug.LogError("[LobbyUIBootstrap] Failed to load LobbyUIRoot from Addressables/fallback/resources.");
                yield break;
            }

            _spawnedRoot.name = "LobbyUIRoot";
            EnsureController(_spawnedRoot);
        }

        private IEnumerator InstantiateFromAddressablesIfPossible()
        {
            CoreRoot root = CoreRoot.Instance;
            AddressablesService addressables;
            if (root == null || root.Services == null ||
                !root.Services.TryGet(out addressables) ||
                addressables == null ||
                !addressables.IsAvailable ||
                string.IsNullOrEmpty(addressKey))
            {
                yield break;
            }

            bool completed = false;
            GameObject instance = null;
            addressables.InstantiatePrefab(addressKey, transform, go =>
            {
                instance = go;
                completed = true;
            });

            while (!completed)
            {
                yield return null;
            }

            if (instance != null)
            {
                _spawnedRoot = instance;
                _spawnedByAddressables = true;
                yield break;
            }

            Debug.LogWarning("[LobbyUIBootstrap] Addressables load failed for key: " + addressKey);
        }

        private GameObject InstantiateFromFallback()
        {
            if (fallbackPrefab != null)
            {
                return Instantiate(fallbackPrefab, transform);
            }

            if (string.IsNullOrEmpty(resourcesPrefabPath))
            {
                return null;
            }

            GameObject resourcePrefab = Resources.Load<GameObject>(resourcesPrefabPath);
            if (resourcePrefab == null)
            {
                Debug.LogWarning("[LobbyUIBootstrap] Resources fallback prefab not found: " + resourcesPrefabPath);
                return null;
            }

            return Instantiate(resourcePrefab, transform);
        }

        private static void EnsureController(GameObject root)
        {
            if (root == null)
            {
                return;
            }

            LobbyUIRootView view = root.GetComponent<LobbyUIRootView>();
            if (view == null)
            {
                view = root.AddComponent<LobbyUIRootView>();
            }

            LobbyUIController controller = root.GetComponent<LobbyUIController>();
            if (controller == null)
            {
                controller = root.AddComponent<LobbyUIController>();
            }

            controller.SetView(view);
            view.ShowPanel(LobbyPanel.MainMenu);
        }
    }
}
