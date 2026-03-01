using System;
using System.Collections.Generic;
using DeliveryRun;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeliveryRun.Managers.Core
{
    [DisallowMultipleComponent]
    public sealed partial class CoreRoot : MonoBehaviour
    {
        public static CoreRoot Instance { get; private set; }

        private readonly List<ISubManager> _subManagers = new List<ISubManager>(8);
        private readonly HashSet<Type> _subManagerTypes = new HashSet<Type>();
        private readonly HashSet<string> _subManagerNames = new HashSet<string>(StringComparer.Ordinal);

        private ServiceRegistry _services;
        private EventBus _events;
        private GameClock _clock;
        private LoadingState _loadingState;

        private bool[] _tickDisabled;
        private bool[] _tickErrorLogged;
        private bool _clockErrorLogged;
        private bool _initialized;

        public CoreContext Context { get; private set; }
        public ServiceRegistry Services => _services;
        public EventBus Events => _events;
        public GameClock Clock => _clock;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[CoreRoot] Duplicate instance detected. Destroying duplicate.");
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            Bootstrap();
        }

        private void Start()
        {
            if (!_initialized || _events == null)
            {
                return;
            }

            string activeScene = SceneManager.GetActiveScene().name;
            if (activeScene == SceneNames.CoreScene)
            {
                _events.Publish(new BootToLobbyRequested());
            }
        }

        private void Update()
        {
            if (!_initialized)
            {
                return;
            }

            float dt = Time.unscaledDeltaTime;
            try
            {
                _clock.Tick(dt);
            }
            catch (Exception ex)
            {
                if (!_clockErrorLogged)
                {
                    _clockErrorLogged = true;
                    Debug.LogError("[CoreRoot] GameClock.Tick failed. Disabling clock tick for this session.");
                    Debug.LogException(ex);
                }
            }

            for (int i = 0; i < _subManagers.Count; i++)
            {
                if (_tickDisabled != null && _tickDisabled[i])
                {
                    continue;
                }

                ISubManager manager = _subManagers[i];
                if (manager == null)
                {
                    continue;
                }

                try
                {
                    manager.Tick(dt);
                }
                catch (Exception ex)
                {
                    if (_tickDisabled != null)
                    {
                        _tickDisabled[i] = true;
                    }

                    if (_tickErrorLogged == null || _tickErrorLogged[i])
                    {
                        continue;
                    }

                    _tickErrorLogged[i] = true;
                    Debug.LogError(
                        "[CoreRoot] SubManager tick failed and was disabled: " + manager.Name +
                        " (" + manager.GetType().FullName + ")");
                    Debug.LogException(ex);
                }
            }
        }

        private void OnDestroy()
        {
            if (Instance != this)
            {
                return;
            }

            for (int i = _subManagers.Count - 1; i >= 0; i--)
            {
                ISubManager manager = _subManagers[i];
                if (manager == null)
                {
                    continue;
                }

                try
                {
                    manager.Shutdown();
                }
                catch (Exception ex)
                {
                    Debug.LogError(
                        "[CoreRoot] SubManager shutdown failed: " + manager.Name +
                        " (" + manager.GetType().FullName + ")");
                    Debug.LogException(ex);
                }
            }

            _subManagers.Clear();
            _subManagerTypes.Clear();
            _subManagerNames.Clear();
            _tickDisabled = null;
            _tickErrorLogged = null;

            if (_events != null)
            {
                _events.ClearAll();
            }

            if (_clock != null)
            {
                _clock.Clear();
            }

            if (_loadingState != null)
            {
                _loadingState.Reset();
            }

            _initialized = false;
            _clockErrorLogged = false;
            Context = null;
            Instance = null;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("[CoreRoot] Shutdown complete.");
#endif
        }

    }
}
