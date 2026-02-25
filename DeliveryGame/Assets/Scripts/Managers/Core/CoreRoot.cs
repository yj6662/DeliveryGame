using System;
using System.Collections.Generic;
using System.Text;
using DeliveryRun;
using DeliveryRun.Managers.Subs;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeliveryRun.Managers.Core
{
    [DisallowMultipleComponent]
    public sealed class CoreRoot : MonoBehaviour
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

        private void Bootstrap()
        {
            _services = new ServiceRegistry();
            _events = new EventBus();
            _clock = new GameClock();
            _loadingState = new LoadingState();

            _services.Register(_services);
            _services.Register(_events);
            _services.Register(_clock);
            _services.Register(_loadingState);

            Context = new CoreContext(_services, _events, _clock, this);

            AddSubManager(new SceneRouter());
            AddSubManager(new SceneFlowController());
            AddSubManager(new AudioManager());
            AddSubManager(new SoundManager());
            AddSubManager(new AddressablesService());
            AddSubManager(new RunSessionManager());
            AddSubManager(new UIManager());
            AddSubManager(new MusicChoiceManager());
            AddSubManager(new DeliveryManager());
            AddSubManager(new RatingManager());
            AddSubManager(new EconomyManager());
            AddSubManager(new TelemetryManager());

            _subManagers.Sort(SubManagerSort.Compare);

            _tickDisabled = new bool[_subManagers.Count];
            _tickErrorLogged = new bool[_subManagers.Count];
            _clockErrorLogged = false;

            for (int i = 0; i < _subManagers.Count; i++)
            {
                ISubManager manager = _subManagers[i];
                if (manager == null)
                {
                    _tickDisabled[i] = true;
                    continue;
                }

                try
                {
                    manager.Initialize(Context);
                }
                catch (Exception ex)
                {
                    _tickDisabled[i] = true;
                    _tickErrorLogged[i] = true;
                    Debug.LogError(
                        "[CoreRoot] SubManager initialize failed: " + manager.Name +
                        " (" + manager.GetType().FullName + ")");
                    Debug.LogException(ex);

                    try
                    {
                        manager.Shutdown();
                    }
                    catch (Exception shutdownEx)
                    {
                        Debug.LogException(shutdownEx);
                    }
                }
            }

            _initialized = true;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("[CoreRoot] Initialized with " + _subManagers.Count + " sub-managers.");
            LogSubManagerInitOrder();
            LogServiceSnapshot();
#endif
        }

        private void AddSubManager(ISubManager manager, bool registerAsService = true)
        {
            if (manager == null)
            {
                Debug.LogError("[CoreRoot] Cannot add null SubManager.");
                return;
            }

            Type managerType = manager.GetType();
            if (_subManagerTypes.Contains(managerType))
            {
                Debug.LogWarning("[CoreRoot] Duplicate SubManager type ignored: " + managerType.FullName);
                return;
            }

            if (!string.IsNullOrEmpty(manager.Name) && _subManagerNames.Contains(manager.Name))
            {
                Debug.LogWarning("[CoreRoot] Duplicate SubManager name detected: " + manager.Name);
            }

            if (registerAsService)
            {
                try
                {
                    _services.Register(managerType, manager);
                }
                catch (Exception ex)
                {
                    Debug.LogError("[CoreRoot] Failed to register SubManager service: " + managerType.FullName);
                    Debug.LogException(ex);
                    return;
                }
            }

            _subManagers.Add(manager);
            _subManagerTypes.Add(managerType);
            if (!string.IsNullOrEmpty(manager.Name))
            {
                _subManagerNames.Add(manager.Name);
            }
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void LogSubManagerInitOrder()
        {
            var builder = new StringBuilder(256);
            builder.Append("[CoreRoot] SubManager Init Order:");
            for (int i = 0; i < _subManagers.Count; i++)
            {
                ISubManager manager = _subManagers[i];
                if (manager == null)
                {
                    continue;
                }

                builder.Append("\n - ");
                builder.Append(manager.InitOrder);
                builder.Append(" | ");
                builder.Append(manager.Name);
                builder.Append(" | ");
                builder.Append(manager.GetType().FullName);
            }

            Debug.Log(builder.ToString());
        }

        private void LogServiceSnapshot()
        {
            const int maxServiceCount = 64;
            var names = new string[maxServiceCount];
            int count = _services.CopyRegisteredServiceTypeNamesNonAlloc(names);

            var builder = new StringBuilder(256);
            builder.Append("[CoreRoot] Registered Services:");
            for (int i = 0; i < count; i++)
            {
                if (string.IsNullOrEmpty(names[i]))
                {
                    continue;
                }

                builder.Append("\n - ");
                builder.Append(names[i]);
            }

            Debug.Log(builder.ToString());
        }
#endif
    }
}
