using Dalamud.Plugin;
using Glamourer.Api.IpcSubscribers;
using System;

namespace OopsAllNudist.Utils
{
    internal class GlamourerService : IDisposable
    {
        public SetItem? SetItemApi { get; private set; }
        public RevertState? RevertStateApi { get; private set; }
        public RevertToAutomation? RevertToAutomationApi { get; private set; }
        public bool IsAvailable { get; private set; }

        public GlamourerService(IDalamudPluginInterface pluginInterface)
        {
            SetItemApi = new SetItem(pluginInterface);
            RevertStateApi = new RevertState(pluginInterface);
            RevertToAutomationApi = new RevertToAutomation(pluginInterface);
        }

        public void Dispose()
        {
        }
    }
}
