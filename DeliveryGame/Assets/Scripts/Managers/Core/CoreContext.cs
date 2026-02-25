using System;

namespace DeliveryRun.Managers.Core
{
    public sealed class CoreContext
    {
        public ServiceRegistry Services { get; }
        public EventBus Events { get; }
        public GameClock Clock { get; }
        public CoreRoot Root { get; }

        public CoreContext(ServiceRegistry services, EventBus events, GameClock clock, CoreRoot root)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            if (events == null)
            {
                throw new ArgumentNullException(nameof(events));
            }

            if (clock == null)
            {
                throw new ArgumentNullException(nameof(clock));
            }

            Services = services;
            Events = events;
            Clock = clock;
            Root = root;
        }
    }
}
