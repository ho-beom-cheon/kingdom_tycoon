using System;
using System.Collections.Generic;
using KingdomTycoon.Services;
using NUnit.Framework;

namespace KingdomTycoon.Tests.EditMode
{
    public sealed class ServiceRegistryTests
    {
        [Test]
        public void InitializeAndShutdown_RespectServiceOrder()
        {
            var calls = new List<string>();
            var registry = new ServiceRegistry();
            registry.Register(new LateService(calls));
            registry.Register(new EarlyService(calls));

            registry.InitializeAll();
            registry.ShutdownAll();

            Assert.That(calls, Is.EqualTo(new[]
            {
                "early.initialize",
                "late.initialize",
                "late.shutdown",
                "early.shutdown",
            }));
        }

        [Test]
        public void Register_DuplicateServiceType_Throws()
        {
            var registry = new ServiceRegistry();
            registry.Register(new EarlyService(new List<string>()));

            Assert.Throws<InvalidOperationException>(() =>
                registry.Register(new EarlyService(new List<string>())));
        }

        private abstract class RecordingService : IAppService
        {
            private readonly IList<string> calls;
            private readonly string name;

            protected RecordingService(IList<string> calls, string name)
            {
                this.calls = calls;
                this.name = name;
            }

            public abstract int InitializationOrder { get; }

            public void Initialize(ServiceRegistry services)
            {
                calls.Add($"{name}.initialize");
            }

            public void Shutdown()
            {
                calls.Add($"{name}.shutdown");
            }
        }

        private sealed class EarlyService : RecordingService
        {
            public EarlyService(IList<string> calls) : base(calls, "early")
            {
            }

            public override int InitializationOrder => 10;
        }

        private sealed class LateService : RecordingService
        {
            public LateService(IList<string> calls) : base(calls, "late")
            {
            }

            public override int InitializationOrder => 100;
        }
    }
}
