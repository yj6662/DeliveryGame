using UnityEngine;

namespace DeliveryRun.Managers.Core
{
    public abstract class SubManagerBase : ISubManager
    {
        private bool _initialized;
        private bool _shutdown;

        public abstract string Name { get; }
        public virtual int InitOrder => 0;

        protected CoreContext Ctx { get; private set; }
        protected EventBus Events => Ctx.Events;
        protected ServiceRegistry Services => Ctx.Services;
        protected GameClock Clock => Ctx.Clock;
        protected SubscriptionBag Subs { get; private set; }

        public void Initialize(CoreContext ctx)
        {
            if (_initialized)
            {
                Debug.LogWarning("[" + Name + "] Initialize called more than once.");
                return;
            }

            if (ctx == null)
            {
                Debug.LogError("[" + Name + "] Initialize failed: CoreContext is null.");
                return;
            }

            Ctx = ctx;
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
