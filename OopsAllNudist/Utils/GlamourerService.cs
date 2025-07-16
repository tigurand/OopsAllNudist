using Dalamud.Plugin;
using Glamourer.Api.Helpers;
using Glamourer.Api.IpcSubscribers;
using System;

namespace OopsAllNudist.Utils
{
    internal class GlamourerService : IDisposable
    {
        private readonly ApiVersion apiVersion;
        private readonly EventSubscriber<nint> stateChangedSubscriber;

        public bool IsAvailable { get; private set; }

        public GlamourerService(IDalamudPluginInterface pluginInterface)
        {
            apiVersion = new ApiVersion(pluginInterface);

            try
            {
                IsAvailable = apiVersion.Invoke().Major >= 1;
            }
            catch { IsAvailable = false; }

            if (!IsAvailable)
            {
                stateChangedSubscriber = new EventSubscriber<nint>(pluginInterface, "Glamourer.DummyEvent", Array.Empty<Action<nint>>());
                return;
            }

            stateChangedSubscriber = StateChanged.Subscriber(pluginInterface, Drawer.OnGlamourerStateChanged);
            Plugin.OutputChatLine("Hooked into Glamourer to trigger Penumbra redraws.");
        }

        public void Dispose()
        {
            stateChangedSubscriber.Dispose();
        }
    }
}
