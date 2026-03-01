using DeliveryRun.Managers.Core;
using UnityEngine.SceneManagement;
using DomainRunSessionEnded = DeliveryRun.Delivery.RunSession.RunSessionEnded;
using DomainRunSessionStarted = DeliveryRun.Delivery.RunSession.RunSessionStarted;

namespace DeliveryRun.Managers.Subs
{
    public sealed class FuelManager : SubManagerBase
    {
        private const float ScenePollInterval = 0.25f;

        private FuelService _fuel;
        private FuelConsumptionRuntime _consumptionRuntime;
        private FuelStationRuntime _stationRuntime;

        private bool _runActive;
        private bool _isRunScene;
        private float _scenePollAccum;

        public override string Name => nameof(FuelManager);
        public override int InitOrder => 72;

        protected override void OnInitialize()
        {
            _fuel = new FuelService();
            Services.Register(_fuel);

            _consumptionRuntime = new FuelConsumptionRuntime(_fuel, Events);
            _stationRuntime = new FuelStationRuntime(_fuel, Services, Events);

            Subs.Add<DomainRunSessionStarted>(Events, OnRunStarted);
            Subs.Add<DomainRunSessionEnded>(Events, OnRunEnded);
            Subs.Add<SceneTransitionStarted>(Events, OnSceneTransitionStarted);
            Subs.Add<SceneTransitionCompleted>(Events, OnSceneTransitionCompleted);

            _runActive = false;
            _isRunScene = SceneManager.GetActiveScene().name == SceneNames.RunScene;
            _consumptionRuntime.Initialize();
            if (_isRunScene)
            {
                _stationRuntime.OnEnterRunScene();
            }
        }

        protected override void OnTick(float unscaledDeltaTime)
        {
            if (ScenePollUtil.ShouldPoll(ref _scenePollAccum, ScenePollInterval, unscaledDeltaTime))
            {
                bool activeRunScene = SceneManager.GetActiveScene().name == SceneNames.RunScene;
                if (_isRunScene != activeRunScene)
                {
                    _isRunScene = activeRunScene;
                    if (_isRunScene)
                    {
                        _stationRuntime.OnEnterRunScene();
                    }
                    else
                    {
                        _stationRuntime.OnExitRunScene();
                    }
                }
            }

            if (!_runActive || !_isRunScene)
            {
                return;
            }

            _consumptionRuntime.Tick(unscaledDeltaTime);
            _stationRuntime.Tick(unscaledDeltaTime, _runActive, _isRunScene);
        }

        protected override void OnShutdown()
        {
            _runActive = false;
            _stationRuntime.OnShutdown();
            _consumptionRuntime.OnShutdown();
        }

        private void OnRunStarted(DomainRunSessionStarted evt)
        {
            _runActive = true;
            _consumptionRuntime.OnRunStarted();
            _stationRuntime.OnRunStarted(_isRunScene);
        }

        private void OnRunEnded(DomainRunSessionEnded evt)
        {
            _runActive = false;
            _consumptionRuntime.OnRunEnded();
            _stationRuntime.OnRunEnded();
        }

        private void OnSceneTransitionStarted(SceneTransitionStarted evt)
        {
            if (evt.To == SceneNames.RunScene)
            {
                return;
            }

            _isRunScene = false;
            _stationRuntime.OnExitRunScene();
        }

        private void OnSceneTransitionCompleted(SceneTransitionCompleted evt)
        {
            _isRunScene = evt.SceneName == SceneNames.RunScene;
            if (_isRunScene)
            {
                _stationRuntime.OnEnterRunScene();
            }
        }
    }
}
