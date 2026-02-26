using DeliveryRun;
using DeliveryRun.Delivery.Traffic;
using DeliveryRun.Delivery.Vehicle;
using DeliveryRun.Managers.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;
using DomainRunSessionEnded = DeliveryRun.Delivery.RunSession.RunSessionEnded;
using DomainRunSessionStarted = DeliveryRun.Delivery.RunSession.RunSessionStarted;

namespace DeliveryRun.Managers.Subs
{
    public sealed class NpcCollisionPenaltyManager : SubManagerBase
    {
        private const float ScenePollInterval = 0.25f;
        private const float NpcHitStunSeconds = 2f;
        private const float NpcSpillScale = 12f;
        private const float NpcSpillMin = 0.01f;
        private const float NpcSpillMax = 0.18f;

        private MotorbikeController _bike;
        private BikeCollisionReporter _collisionReporter;
        private FoodStateService _food;
        private FoodStateConfigSO _foodCfg;
        private bool _runActive;
        private bool _isRunScene;
        private float _scenePollAccum;

        public override string Name => nameof(NpcCollisionPenaltyManager);
        public override int InitOrder => 76;

        protected override void OnInitialize()
        {
            _foodCfg = Resources.Load<FoodStateConfigSO>("Bootstrap/FoodStateConfig");
            Services.TryGet(out _food);

            Subs.Add<DomainRunSessionStarted>(Events, OnRunStarted);
            Subs.Add<DomainRunSessionEnded>(Events, OnRunEnded);
            Subs.Add<SceneTransitionStarted>(Events, OnSceneTransitionStarted);
            Subs.Add<SceneTransitionCompleted>(Events, OnSceneTransitionCompleted);

            _isRunScene = SceneManager.GetActiveScene().name == SceneNames.RunScene;
            if (_isRunScene)
            {
                EnsureBikeHooked();
            }
        }

        protected override void OnTick(float unscaledDeltaTime)
        {
            _scenePollAccum += unscaledDeltaTime;
            if (_scenePollAccum >= ScenePollInterval)
            {
                _scenePollAccum = 0f;
                bool runScene = SceneManager.GetActiveScene().name == SceneNames.RunScene;
                if (_isRunScene != runScene)
                {
                    _isRunScene = runScene;
                    if (_isRunScene)
                    {
                        EnsureBikeHooked();
                    }
                    else
                    {
                        UnhookBike();
                    }
                }
            }

            if (!_runActive || !_isRunScene)
            {
                return;
            }

            EnsureBikeHooked();
        }

        protected override void OnShutdown()
        {
            _runActive = false;
            UnhookBike();
        }

        private void OnRunStarted(DomainRunSessionStarted evt)
        {
            _runActive = true;
            EnsureBikeHooked();
        }

        private void OnRunEnded(DomainRunSessionEnded evt)
        {
            _runActive = false;
        }

        private void OnSceneTransitionStarted(SceneTransitionStarted evt)
        {
            if (evt.To == SceneNames.RunScene)
            {
                return;
            }

            _isRunScene = false;
            UnhookBike();
        }

        private void OnSceneTransitionCompleted(SceneTransitionCompleted evt)
        {
            _isRunScene = evt.SceneName == SceneNames.RunScene;
            if (_isRunScene)
            {
                EnsureBikeHooked();
            }
            else
            {
                UnhookBike();
            }
        }

        private void EnsureBikeHooked()
        {
            if (_bike == null)
            {
                _bike = Object.FindAnyObjectByType<MotorbikeController>();
            }

            if (_bike == null)
            {
                return;
            }

            if (_collisionReporter == null)
            {
                _collisionReporter = _bike.GetComponent<BikeCollisionReporter>();
                if (_collisionReporter == null)
                {
                    _collisionReporter = _bike.gameObject.AddComponent<BikeCollisionReporter>();
                }

                _collisionReporter.CollidedDetailed -= OnBikeCollidedDetailed;
                _collisionReporter.CollidedDetailed += OnBikeCollidedDetailed;
            }
        }

        private void UnhookBike()
        {
            if (_collisionReporter != null)
            {
                _collisionReporter.CollidedDetailed -= OnBikeCollidedDetailed;
                _collisionReporter = null;
            }

            _bike = null;
        }

        private void OnBikeCollidedDetailed(BikeCollisionInfo info)
        {
            if (!_runActive || !_isRunScene)
            {
                return;
            }

            if (!IsNpcCollision(info.Collision))
            {
                return;
            }

            if (_bike != null)
            {
                _bike.ApplyStun(NpcHitStunSeconds);
            }

            if (_food == null)
            {
                Services.TryGet(out _food);
            }

            if (_food == null || !_food.IsActive || _foodCfg == null)
            {
                return;
            }

            float spillAdd = Mathf.Clamp(info.Impulse * _foodCfg.SpillFromCollision * NpcSpillScale, NpcSpillMin, NpcSpillMax);
            _food.AddSpill(spillAdd, _foodCfg);

            Events.Publish(new FoodStateTicked
            {
                Temperature01 = _food.Temperature01,
                Spill01 = _food.Spill01,
                Quality01 = _food.ComputeQuality01()
            });
        }

        private static bool IsNpcCollision(Collision collision)
        {
            if (collision == null)
            {
                return false;
            }

            Collider c0 = collision.collider;
            if (c0 != null && c0.GetComponentInParent<TrafficNpcVehicle>() != null)
            {
                return true;
            }

            Transform otherRoot = collision.transform;
            if (otherRoot != null && otherRoot.GetComponentInParent<TrafficNpcVehicle>() != null)
            {
                return true;
            }

            return false;
        }
    }
}
