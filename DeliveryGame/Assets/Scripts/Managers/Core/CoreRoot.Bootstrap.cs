using System;
using DeliveryRun.Managers.Subs;
using UnityEngine;

namespace DeliveryRun.Managers.Core
{
    public sealed partial class CoreRoot
    {
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
            AddSubManager(new MusicDraftManager());
            AddSubManager(new LobbyHubManager());
            AddSubManager(new AddressablesService());
            AddSubManager(new RunSessionManager());
            AddSubManager(new RunSessionLegacyBridgeManager());
            AddSubManager(new RunDayNightLightingManager());
            AddSubManager(new RunModifierManager());
            AddSubManager(new MetaProgressionManager());
            AddSubManager(new SectorThemeManager());
            AddSubManager(new RegionGateManager());
            AddSubManager(new RunRegionMiniatureManager());
            AddSubManager(new PlayerBikeModifierLink());
            AddSubManager(new TrafficSystemManager());
            AddSubManager(new TrafficSignalManager());
            AddSubManager(new TrafficNpcManager());
            AddSubManager(new RoadQueryManager());
            AddSubManager(new OrderFlowManager());
            AddSubManager(new FuelManager());
            AddSubManager(new FoodStateManager());
            AddSubManager(new NpcCollisionPenaltyManager());
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
    }
}
