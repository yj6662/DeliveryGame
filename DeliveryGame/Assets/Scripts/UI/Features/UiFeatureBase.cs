using DeliveryRun.Managers.Core;
using DeliveryRun.Managers.Subs;
using UnityEngine;

namespace DeliveryRun.UI.Features
{
    internal abstract class UiFeatureBase : IUiFeature
    {
        private bool _initialized;
        private bool _shutdown;

        protected CoreContext Ctx { get; private set; }
        protected UIManager Ui { get; private set; }
        protected EventBus Events => Ctx != null ? Ctx.Events : null;
        protected ServiceRegistry Services => Ctx != null ? Ctx.Services : null;
        protected GameClock Clock => Ctx != null ? Ctx.Clock : null;
        protected SubscriptionBag Subs { get; private set; }

        public void Initialize(CoreContext ctx, UIManager ui)
        {
            if (_initialized)
            {
                Debug.LogWarning("[" + GetType().Name + "] Initialize called more than once.");
                return;
            }

            if (ctx == null || ui == null)
            {
                Debug.LogError("[" + GetType().Name + "] Initialize failed: context or UIManager is null.");
                return;
            }

            Ctx = ctx;
            Ui = ui;
            Subs = new SubscriptionBag();
            _shutdown = false;
            _initialized = true;

            OnInitialize();
        }

        public void Tick(float unscaledDeltaTime)
        {
            if (!_initialized || _shutdown)
            {
                return;
            }

            OnTick(unscaledDeltaTime);
        }

        public void Shutdown()
        {
            if (!_initialized || _shutdown)
            {
                return;
            }

            try
            {
                OnShutdown();
            }
            finally
            {
                if (Subs != null)
                {
                    Subs.Clear();
                    Subs = null;
                }

                Ctx = null;
                Ui = null;
                _shutdown = true;
            }
        }

        protected virtual void OnInitialize()
        {
        }

        protected virtual void OnTick(float unscaledDeltaTime)
        {
        }

        protected virtual void OnShutdown()
        {
        }
    }
}
