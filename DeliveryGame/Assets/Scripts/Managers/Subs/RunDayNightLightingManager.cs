using DeliveryRun;
using DeliveryRun.Managers.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;
using DomainRunSessionEnded = DeliveryRun.Delivery.RunSession.RunSessionEnded;
using DomainRunSessionStarted = DeliveryRun.Delivery.RunSession.RunSessionStarted;
using DomainRunTimerTicked = DeliveryRun.Delivery.RunSession.RunTimerTicked;

namespace DeliveryRun.Managers.Subs
{
    public sealed class RunDayNightLightingManager : SubManagerBase
    {
        private const float ScenePollInterval = 0.5f;

        private static readonly Color NoonSunColor = new Color(1f, 0.98f, 0.93f, 1f);
        private static readonly Color EveningSunColor = new Color(1f, 0.67f, 0.44f, 1f);
        private static readonly Color NoonAmbientSky = new Color(0.72f, 0.79f, 0.90f, 1f);
        private static readonly Color EveningAmbientSky = new Color(0.28f, 0.30f, 0.36f, 1f);
        private static readonly Color NoonAmbientEquator = new Color(0.67f, 0.71f, 0.76f, 1f);
        private static readonly Color EveningAmbientEquator = new Color(0.20f, 0.20f, 0.24f, 1f);
        private static readonly Color NoonAmbientGround = new Color(0.40f, 0.39f, 0.35f, 1f);
        private static readonly Color EveningAmbientGround = new Color(0.16f, 0.15f, 0.15f, 1f);

        private Light _sun;
        private float _runDurationSeconds = 420f;
        private bool _runActive;
        private bool _isRunScene;
        private float _scenePollAccum;

        public override string Name => nameof(RunDayNightLightingManager);
        public override int InitOrder => 58;

        protected override void OnInitialize()
        {
            Subs.Add<SceneTransitionCompleted>(Events, OnSceneTransitionCompleted);
            Subs.Add<SceneTransitionStarted>(Events, OnSceneTransitionStarted);
            Subs.Add<DomainRunSessionStarted>(Events, OnRunSessionStarted);
            Subs.Add<DomainRunTimerTicked>(Events, OnRunTimerTicked);
            Subs.Add<DomainRunSessionEnded>(Events, OnRunSessionEnded);

            HandleSceneChanged(SceneManager.GetActiveScene().name, true);
        }

        protected override void OnTick(float unscaledDeltaTime)
        {
            if (!ScenePollUtil.ShouldPoll(ref _scenePollAccum, ScenePollInterval, unscaledDeltaTime))
            {
                return;
            }

            HandleSceneChanged(SceneManager.GetActiveScene().name, false);
        }

        protected override void OnShutdown()
        {
            _runActive = false;
            _isRunScene = false;
            _sun = null;
        }

        private void OnSceneTransitionStarted(SceneTransitionStarted evt)
        {
            if (evt.To == SceneNames.RunScene)
            {
                return;
            }

            _runActive = false;
            _isRunScene = false;
            _sun = null;
        }

        private void OnSceneTransitionCompleted(SceneTransitionCompleted evt)
        {
            HandleSceneChanged(evt.SceneName, true);
        }

        private void OnRunSessionStarted(DomainRunSessionStarted evt)
        {
            _runDurationSeconds = Mathf.Max(1f, evt.DurationSeconds);
            _runActive = true;

            if (!_isRunScene)
            {
                return;
            }

            EnsureSun();
            ApplyDayProgress(0f);
        }

        private void OnRunTimerTicked(DomainRunTimerTicked evt)
        {
            if (!_runActive || !_isRunScene)
            {
                return;
            }

            EnsureSun();
            if (_sun == null)
            {
                return;
            }

            float progress = Mathf.Clamp01(evt.ElapsedSeconds / Mathf.Max(1f, _runDurationSeconds));
            ApplyDayProgress(progress);
        }

        private void OnRunSessionEnded(DomainRunSessionEnded evt)
        {
            _runActive = false;
        }

        private void HandleSceneChanged(string sceneName, bool forceReset)
        {
            bool isRun = sceneName == SceneNames.RunScene;
            if (!isRun)
            {
                _isRunScene = false;
                _sun = null;
                return;
            }

            bool firstEnter = !_isRunScene;
            _isRunScene = true;
            EnsureSun();

            if (firstEnter || forceReset)
            {
                ApplyDayProgress(0f);
            }
        }

        private void EnsureSun()
        {
            if (_sun != null)
            {
                return;
            }

            Light[] lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
            for (int i = 0; i < lights.Length; i++)
            {
                Light light = lights[i];
                if (light == null || light.type != LightType.Directional)
                {
                    continue;
                }

                _sun = light;
                break;
            }

            if (_sun != null)
            {
                return;
            }

            GameObject sunGo = new GameObject("RunSunLight");
            _sun = sunGo.AddComponent<Light>();
            _sun.type = LightType.Directional;
            _sun.shadows = LightShadows.Soft;
        }

        private void ApplyDayProgress(float t)
        {
            if (_sun == null)
            {
                return;
            }

            float pitch = Mathf.Lerp(58f, 16f, t);
            float yaw = Mathf.Lerp(-30f, 34f, t);

            _sun.transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
            _sun.color = Color.Lerp(NoonSunColor, EveningSunColor, t);
            _sun.intensity = Mathf.Lerp(1.15f, 0.45f, t);

            RenderSettings.ambientSkyColor = Color.Lerp(NoonAmbientSky, EveningAmbientSky, t);
            RenderSettings.ambientEquatorColor = Color.Lerp(NoonAmbientEquator, EveningAmbientEquator, t);
            RenderSettings.ambientGroundColor = Color.Lerp(NoonAmbientGround, EveningAmbientGround, t);
            RenderSettings.fogColor = Color.Lerp(new Color(0.74f, 0.81f, 0.9f, 1f), new Color(0.42f, 0.38f, 0.35f, 1f), t);

            if (RenderSettings.sun != _sun)
            {
                RenderSettings.sun = _sun;
            }
        }
    }
}
