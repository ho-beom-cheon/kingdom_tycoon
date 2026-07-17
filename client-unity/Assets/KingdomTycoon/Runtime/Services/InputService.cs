using System;
using UnityEngine.InputSystem;

namespace KingdomTycoon.Services
{
    public sealed class InputService : IAppService
    {
        private readonly InputActionAsset actions;

        public InputService(InputActionAsset actions)
        {
            this.actions = actions ?? throw new ArgumentNullException(nameof(actions));
        }

        public int InitializationOrder => 10;

        public bool IsEnabled { get; private set; }

        public void Initialize(ServiceRegistry services)
        {
            actions.Enable();
            IsEnabled = true;
        }

        public void Shutdown()
        {
            actions.Disable();
            IsEnabled = false;
        }
    }
}
