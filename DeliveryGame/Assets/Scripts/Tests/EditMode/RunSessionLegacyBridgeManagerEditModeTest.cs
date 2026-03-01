using DeliveryRun.Managers.Core;
using DeliveryRun.Managers.Subs;
using NUnit.Framework;
using DomainRunEndReason = DeliveryRun.Delivery.RunSession.RunEndReason;
using DomainRunLastOrderStarted = DeliveryRun.Delivery.RunSession.RunLastOrderStarted;
using DomainRunSessionEnded = DeliveryRun.Delivery.RunSession.RunSessionEnded;
using DomainRunSessionStarted = DeliveryRun.Delivery.RunSession.RunSessionStarted;
using DomainRunTimerTicked = DeliveryRun.Delivery.RunSession.RunTimerTicked;
using LegacyRunEndReason = DeliveryRun.Managers.Core.RunEndReason;
using LegacyRunSessionEnded = DeliveryRun.Managers.Core.RunSessionEnded;
using LegacyRunSessionLastOrderStarted = DeliveryRun.Managers.Core.RunSessionLastOrderStarted;
using LegacyRunSessionStarted = DeliveryRun.Managers.Core.RunSessionStarted;
using LegacyRunSessionTick = DeliveryRun.Managers.Core.RunSessionTick;

namespace DeliveryRun.Tests.EditMode
{
    public sealed class RunSessionLegacyBridgeManagerEditModeTest
    {
        [Test]
        public void Bridge_MapsDomainRunEvents_ToLegacyRunEvents()
        {
            CoreContext context = CreateContext();
            var manager = new RunSessionLegacyBridgeManager();
            manager.Initialize(context);

            int startedCount = 0;
            int tickCount = 0;
            int lastOrderCount = 0;
            int endedCount = 0;
            LegacyRunSessionStarted started = default;
            LegacyRunSessionTick firstTick = default;
            LegacyRunSessionTick secondTick = default;
            LegacyRunSessionEnded ended = default;

            context.Events.Subscribe<LegacyRunSessionStarted>(evt =>
            {
                startedCount++;
                started = evt;
            });
            context.Events.Subscribe<LegacyRunSessionTick>(evt =>
            {
                tickCount++;
                if (tickCount == 1)
                {
                    firstTick = evt;
                    return;
                }

                secondTick = evt;
            });
            context.Events.Subscribe<LegacyRunSessionLastOrderStarted>(evt => { lastOrderCount++; });
            context.Events.Subscribe<LegacyRunSessionEnded>(evt =>
            {
                endedCount++;
                ended = evt;
            });

            context.Events.Publish(new DomainRunSessionStarted
            {
                DurationSeconds = 420f
            });
            context.Events.Publish(new DomainRunTimerTicked
            {
                ElapsedSeconds = 10f,
                RemainingSeconds = 410f
            });
            context.Events.Publish(new DomainRunLastOrderStarted
            {
                AtElapsedSeconds = 360f
            });
            context.Events.Publish(new DomainRunTimerTicked
            {
                ElapsedSeconds = 365f,
                RemainingSeconds = 55f
            });
            context.Events.Publish(new DomainRunSessionEnded
            {
                Reason = DomainRunEndReason.OutOfFuel,
                ElapsedSeconds = 370f
            });

            Assert.AreEqual(1, startedCount);
            Assert.AreEqual(2, tickCount);
            Assert.AreEqual(1, lastOrderCount);
            Assert.AreEqual(1, endedCount);

            Assert.AreEqual(1, started.RunSequence);
            Assert.AreEqual(420f, started.DurationSeconds, 0.001f);
            Assert.IsFalse(firstTick.IsLastOrderPhase);
            Assert.IsTrue(secondTick.IsLastOrderPhase);
            Assert.AreEqual(LegacyRunEndReason.FuelDepleted, ended.Reason);

            manager.Shutdown();
        }

        [Test]
        public void Bridge_IncrementsLegacyRunSequence_PerDomainRunStart()
        {
            CoreContext context = CreateContext();
            var manager = new RunSessionLegacyBridgeManager();
            manager.Initialize(context);

            int firstRunSequence = 0;
            int secondRunSequence = 0;
            int startedCount = 0;
            context.Events.Subscribe<LegacyRunSessionStarted>(evt =>
            {
                startedCount++;
                if (startedCount == 1)
                {
                    firstRunSequence = evt.RunSequence;
                }
                else if (startedCount == 2)
                {
                    secondRunSequence = evt.RunSequence;
                }
            });

            context.Events.Publish(new DomainRunSessionStarted { DurationSeconds = 420f });
            context.Events.Publish(new DomainRunSessionEnded
            {
                Reason = DomainRunEndReason.TimeExpired,
                ElapsedSeconds = 420f
            });
            context.Events.Publish(new DomainRunSessionStarted { DurationSeconds = 420f });

            Assert.AreEqual(2, startedCount);
            Assert.AreEqual(1, firstRunSequence);
            Assert.AreEqual(2, secondRunSequence);

            manager.Shutdown();
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
