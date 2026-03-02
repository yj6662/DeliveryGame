using DeliveryRun;
using DeliveryRun.Delivery.Traffic;
using DeliveryRun.Delivery.Vehicle;
using DeliveryRun.Managers.Core;
using System.Collections.Generic;
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
        private const float NpcHitStopSeconds = 2f;
        private const float NpcCollisionIgnoreSeconds = 3f;
        private const float NpcIgnoreRefreshInterval = 0.25f;
        private const float NpcSpillScale = 12f;
        private const float NpcSpillMin = 0.01f;
        private const float NpcSpillMax = 0.18f;
        private const float NpcHitPushMinSpeed = 1.4f;
        private const float NpcHitPushMaxSpeed = 7f;
        private const float NpcHitPushRelativeVelocityScale = 0.85f;
        private const float NpcHitPushImpulseScale = 0.03f;

        private MotorbikeController _bike;
        private BikeCollisionReporter _collisionReporter;
        private FoodStateService _food;
        private FoodStateConfigSO _foodCfg;
        private bool _runActive;
        private bool _isRunScene;
        private float _scenePollAccum;
        private float _npcCollisionIgnoreUntil;
        private float _npcIgnoreRefreshAccum;

        private readonly HashSet<Collider> _ignoredNpcColliders = new HashSet<Collider>();
        private readonly List<string> _activeFoodOfferIds = new List<string>(8);
        private Collider[] _bikeColliders;

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
            if (ScenePollUtil.ShouldPoll(ref _scenePollAccum, ScenePollInterval, unscaledDeltaTime))
            {
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
            TickCollisionIgnoreWindow(unscaledDeltaTime);
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
            EndCollisionIgnoreWindow();
        }

        private void OnSceneTransitionStarted(SceneTransitionStarted evt)
        {
            if (evt.To == SceneNames.RunScene)
            {
                return;
            }

            _isRunScene = false;
            EndCollisionIgnoreWindow();
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
                EndCollisionIgnoreWindow();
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

            if (_bikeColliders == null || _bikeColliders.Length == 0)
            {
                _bikeColliders = _bike.GetComponentsInChildren<Collider>(true);
            }
        }

        private void UnhookBike()
        {
            EndCollisionIgnoreWindow();

            if (_collisionReporter != null)
            {
                _collisionReporter.CollidedDetailed -= OnBikeCollidedDetailed;
                _collisionReporter = null;
            }

            _bike = null;
            _bikeColliders = null;
        }

        private void OnBikeCollidedDetailed(BikeCollisionInfo info)
        {
            if (!_runActive || !_isRunScene)
            {
                return;
            }

            TrafficNpcVehicle npcVehicle;
            if (!TryGetNpcVehicle(info.Collision, out npcVehicle))
            {
                return;
            }

            if (IsCollisionIgnoreActive())
            {
                IgnoreNpcVehicleCollision(npcVehicle);
                _npcCollisionIgnoreUntil = Time.unscaledTime + NpcCollisionIgnoreSeconds;
                return;
            }

            if (npcVehicle != null)
            {
                npcVehicle.ForceStopForSeconds(NpcHitStopSeconds);
            }

            if (_bike != null)
            {
                _bike.ApplyStun(NpcHitStunSeconds);
                Vector3 pushDirection = ComputeBikePushDirection(info.Collision, npcVehicle);
                float pushSpeed = ComputeBikePushSpeed(info);
                _bike.ApplyCollisionPush(pushDirection, pushSpeed);
            }

            BeginCollisionIgnoreWindow();

            if (_food == null)
            {
                Services.TryGet(out _food);
            }

            if (_food == null || !_food.IsActive || _foodCfg == null)
            {
                return;
            }

            float spillAdd = Mathf.Clamp(info.Impulse * _foodCfg.SpillFromCollision * NpcSpillScale, NpcSpillMin, NpcSpillMax);
            _food.AddSpillToAll(spillAdd, _foodCfg);
            int count = _food.CopyActiveOfferIdsNonAlloc(_activeFoodOfferIds);
            for (int i = 0; i < count; i++)
            {
                string offerId = _activeFoodOfferIds[i];
                float temperature;
                float spill;
                float quality;
                if (!_food.TryGetState(offerId, out temperature, out spill, out quality))
                {
                    continue;
                }

                Events.Publish(new FoodStateTicked
                {
                    OfferId = offerId,
                    Temperature01 = temperature,
                    Spill01 = spill,
                    Quality01 = quality
                });
            }
        }

        private static bool TryGetNpcVehicle(Collision collision, out TrafficNpcVehicle vehicle)
        {
            vehicle = null;
            if (collision == null)
            {
                return false;
            }

            Collider c0 = collision.collider;
            if (c0 != null)
            {
                vehicle = c0.GetComponentInParent<TrafficNpcVehicle>();
                if (vehicle != null)
                {
                    return true;
                }
            }

            Transform otherRoot = collision.transform;
            if (otherRoot != null)
            {
                vehicle = otherRoot.GetComponentInParent<TrafficNpcVehicle>();
                if (vehicle != null)
                {
                    return true;
                }
            }

            return false;
        }

        private Vector3 ComputeBikePushDirection(Collision collision, TrafficNpcVehicle npcVehicle)
        {
            Vector3 direction = Vector3.zero;
            if (collision != null)
            {
                int contactCount = collision.contactCount;
                for (int i = 0; i < contactCount; i++)
                {
                    direction += collision.GetContact(i).normal;
                }
            }

            Transform bikeTransform = _bike != null ? _bike.transform : null;
            Transform npcTransform = npcVehicle != null ? npcVehicle.transform : null;

            if (bikeTransform != null && npcTransform != null)
            {
                Vector3 bikeFromNpc = bikeTransform.position - npcTransform.position;
                bikeFromNpc.y = 0f;

                if (direction.sqrMagnitude > 0.0001f && Vector3.Dot(direction, bikeFromNpc) < 0f)
                {
                    direction = -direction;
                }

                if (direction.sqrMagnitude <= 0.0001f)
                {
                    direction = bikeFromNpc;
                }
            }

            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.0001f && bikeTransform != null)
            {
                direction = bikeTransform.right;
            }

            return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.zero;
        }

        private static float ComputeBikePushSpeed(BikeCollisionInfo info)
        {
            float relativeSpeed = 0f;
            if (info.Collision != null)
            {
                relativeSpeed = info.Collision.relativeVelocity.magnitude;
            }

            float pushFromRelativeSpeed = relativeSpeed * NpcHitPushRelativeVelocityScale;
            float pushFromImpulse = info.Impulse * NpcHitPushImpulseScale;
            float pushSpeed = Mathf.Max(pushFromRelativeSpeed, pushFromImpulse);
            return Mathf.Clamp(pushSpeed, NpcHitPushMinSpeed, NpcHitPushMaxSpeed);
        }

        private bool IsCollisionIgnoreActive() => Time.unscaledTime < _npcCollisionIgnoreUntil;

        private void BeginCollisionIgnoreWindow()
        {
            _npcCollisionIgnoreUntil = Time.unscaledTime + NpcCollisionIgnoreSeconds;
            _npcIgnoreRefreshAccum = NpcIgnoreRefreshInterval;
            RefreshIgnoredNpcCollisions();
        }

        private void TickCollisionIgnoreWindow(float unscaledDeltaTime)
        {
            if (!IsCollisionIgnoreActive())
            {
                EndCollisionIgnoreWindow();
                return;
            }

            _npcIgnoreRefreshAccum += Mathf.Max(0f, unscaledDeltaTime);
            if (_npcIgnoreRefreshAccum < NpcIgnoreRefreshInterval)
            {
                return;
            }

            _npcIgnoreRefreshAccum = 0f;
            RefreshIgnoredNpcCollisions();
        }

        private void RefreshIgnoredNpcCollisions()
        {
            if (_bike == null)
            {
                return;
            }

            if (_bikeColliders == null || _bikeColliders.Length == 0)
            {
                _bikeColliders = _bike.GetComponentsInChildren<Collider>(true);
            }

            if (_bikeColliders == null || _bikeColliders.Length == 0)
            {
                return;
            }

            TrafficNpcVehicle[] npcVehicles = Object.FindObjectsByType<TrafficNpcVehicle>(FindObjectsSortMode.None);
            for (int i = 0; i < npcVehicles.Length; i++)
            {
                IgnoreNpcVehicleCollision(npcVehicles[i]);
            }
        }

        private void IgnoreNpcVehicleCollision(TrafficNpcVehicle npcVehicle)
        {
            if (npcVehicle == null || _bikeColliders == null || _bikeColliders.Length == 0)
            {
                return;
            }

            Collider[] npcColliders = npcVehicle.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < npcColliders.Length; i++)
            {
                Collider npcCol = npcColliders[i];
                if (npcCol == null || npcCol.isTrigger)
                {
                    continue;
                }

                for (int j = 0; j < _bikeColliders.Length; j++)
                {
                    Collider bikeCol = _bikeColliders[j];
                    if (bikeCol == null || bikeCol.isTrigger)
                    {
                        continue;
                    }

                    if (!Physics.GetIgnoreCollision(bikeCol, npcCol))
                    {
                        Physics.IgnoreCollision(bikeCol, npcCol, true);
                    }
                }

                _ignoredNpcColliders.Add(npcCol);
            }
        }

        private void EndCollisionIgnoreWindow()
        {
            if (_ignoredNpcColliders.Count <= 0 || _bikeColliders == null || _bikeColliders.Length == 0)
            {
                _ignoredNpcColliders.Clear();
                _npcCollisionIgnoreUntil = 0f;
                _npcIgnoreRefreshAccum = 0f;
                return;
            }

            foreach (Collider npcCol in _ignoredNpcColliders)
            {
                if (npcCol == null)
                {
                    continue;
                }

                for (int i = 0; i < _bikeColliders.Length; i++)
                {
                    Collider bikeCol = _bikeColliders[i];
                    if (bikeCol == null || bikeCol.isTrigger)
                    {
                        continue;
                    }

                    Physics.IgnoreCollision(bikeCol, npcCol, false);
                }
            }

            _ignoredNpcColliders.Clear();
            _npcCollisionIgnoreUntil = 0f;
            _npcIgnoreRefreshAccum = 0f;
        }
    }
}
