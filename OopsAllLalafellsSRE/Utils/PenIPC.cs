using Dalamud.Game.ClientState.Objects.Types;
using Penumbra.Api;
using Penumbra.Api.Enums;
using Penumbra.Api.Helpers;
using System;

namespace OopsAllLalafellsSRE.Utils;

internal class PenumbraIpc : IDisposable
{
    private ActionSubscriber<GameObject, RedrawType> RedrawObjectSubscriber { get; }
    private ActionSubscriber<RedrawType> RedrawAllSubscriber { get; }
    private EventSubscriber<nint, string, nint, nint, nint>? CreatingCharacterBaseEvent { get; set; }

    internal PenumbraIpc()
    {
        RedrawObjectSubscriber = Ipc.RedrawObject.Subscriber(Service.pluginInterface);
        RedrawAllSubscriber = Ipc.RedrawAll.Subscriber(Service.pluginInterface);
        RegisterEvents();
    }

    public void Dispose()
    {
        CreatingCharacterBaseEvent?.Dispose();
    }

    private void RegisterEvents()
    {
        CreatingCharacterBaseEvent = Ipc.CreatingCharacterBase.Subscriber(Service.pluginInterface,
            Drawer.OnCreatingCharacterBase);
    }

    internal void RedrawObject(GameObject gameObject, RedrawType setting)
    {
        RedrawObjectSubscriber.Invoke(gameObject, setting);
    }

    internal void RedrawAll(RedrawType setting)
    {
        RedrawAllSubscriber.Invoke(setting);
    }
}
