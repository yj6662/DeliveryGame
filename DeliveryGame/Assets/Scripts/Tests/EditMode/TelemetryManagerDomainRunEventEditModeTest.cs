using DeliveryRun.Managers.Core;
using DeliveryRun.Managers.Subs;
using NUnit.Framework;
using DomainRunEndReason = DeliveryRun.Delivery.RunSession.RunEndReason;
using DomainRunLastOrderStarted = DeliveryRun.Delivery.RunSession.RunLastOrderStarted;
using DomainRunSessionEnded = DeliveryRun.Delivery.RunSession.RunSessionEnded;
using DomainRunSessionStarted = DeliveryRun.Delivery.RunSession.RunSessionStarted;

namespace DeliveryRun.Tests.EditMode
{
    public sealed class TelemetryManagerDomainRunEventEditModeTest
    {
        [Test]
        public void Telemetry_TracksDomainRunEvents_WithSyntheticRunSequence()
        {
            CoreContext context = CreateContext();
            var manager = new TelemetryManager();
            manager.Initialize(context);

            context.Events.Publish(new DomainRunSessionStarted { DurationSeconds = 420f });
            context.Events.Publish(new DomainRunLastOrderStarted { AtElapsedSeconds = 360f });
            context.Events.Publish(new DomainRunSessionEnded
            {
                Reason = DomainRunEndReason.OutOfFuel,
                ElapsedSeconds = 370f
            });

            var entries = new string[16];
            int count = manager.CopyRecentEntriesNonAlloc(entries);

            Assert.GreaterOrEqual(count, 3);
            Assert.IsTrue(Contains(entries, count, "RunSessionStarted | run=1"));
            Assert.IsTrue(Contains(entries, count, "RunSessionLastOrderStarted | run=1"));
            Assert.IsTrue(Contains(entries, count, "RunSessionEnded | run=1,reason=OutOfFuel"));

            manager.Shutdown();
        }

        [Test]
        public void Telemetry_IncrementsSyntheticRunSequence_AcrossRuns()
        {
            CoreContext context = CreateContext();
            var manager = new TelemetryManager();
            manager.Initialize(context);

            context.Events.Publish(new DomainRunSessionStarted { DurationSeconds = 420f });
            context.Events.Publish(new DomainRunSessionEnded
            {
                Reason = DomainRunEndReason.TimeExpired,
                ElapsedSeconds = 420f
            });
            context.Events.Publish(new DomainRunSessionStarted { DurationSeconds = 420f });

            var entries = new string[16];
            int count = manager.CopyRecentEntriesNonAlloc(entries);

            Assert.GreaterOrEqual(count, 3);
            Assert.IsTrue(Contains(entries, count, "RunSessionStarted | run=1"));
            Assert.IsTrue(Contains(entries, count, "RunSessionStarted | run=2"));

            manager.Shutdown();
        }

        private static bool Contains(string[] entries, int count, string snippet)
        {
            for (int i = 0; i < count; i++)
            {
                string entry = entries[i];
                if (string.IsNullOrEmpty(entry))
                {
                    continue;
                }

                if (entry.Contains(snippet))
                {
                    return true;
                }
            }

            return false;
        }

        private static CoreContext CreateContext()
        {
            var services = new ServiceRegistry();
            var events = new EventBus();
            var clock = new GameClock();

            services.Register(services);
            services.Register(events);
            services.Register(clock);

            return new CoreContext(services, events, clock, null);
        }
    }
}
