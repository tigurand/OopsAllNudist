using Dalamud.Plugin;
using Glamourer.Api.Helpers;
using Glamourer.Api.IpcSubscribers;
using System;

namespace OopsAllNudist.Utils
{
    internal class GlamourerService : IDisposable
    {
        private readonly ApiVersion apiVersion;
        private EventSubscriber<nint>? stateChangedSubscriber;
        private readonly EventSubscriber initializedSubscriber;

        public bool IsAvailable { get; private set; }

        public GlamourerService(IDalamudPluginInterface pluginInterface)
        {
            apiVersion = new ApiVersion(pluginInterface);

            initializedSubscriber = Initialized.Subscriber(pluginInterface, SubscribeToStateChanged);

            try
            {
                IsAvailable = apiVersion.Invoke().Major >= 1;
                if (IsAvailable)
                {
                    SubscribeToStateChanged();
                }
            }
            catch { IsAvailable = false; }
        }

        private void SubscribeToStateChanged()
        {
            stateChangedSubscriber?.Dispose();

            stateChangedSubscriber = StateChanged.Subscriber(Service.pluginInterface, Drawer.OnGlamourerStateChanged);
        }

        public void Dispose()
        {
            stateChangedSubscriber?.Dispose();
            initializedSubscriber.Dispose();
        }
    }
}
