using Dalamud.Game.ClientState.Objects;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using OopsAllNudist.Windows;

namespace OopsAllNudist.Utils;

internal class Service
{
    internal static Plugin plugin { get; set; } = null!;
    internal static ConfigWindow configWindow { get; set; } = null!;
    internal static WhitelistWindow whitelistWindow { get; set; } = null!;
    internal static Configuration configuration { get; set; } = null!;
    internal static Drawer drawer { get; set; } = null!;
    public static PenumbraIpc penumbraApi { get; set; } = null!;
    [PluginService] public static IDalamudPluginInterface pluginInterface { get; set; } = null!;
    [PluginService] public static IChatGui chatGui { get; private set; } = null!;
    [PluginService] public static IClientState clientState { get; private set; } = null!;
    [PluginService] public static ICommandManager commandManager { get; private set; } = null!;
    [PluginService] public static IObjectTable objectTable { get; private set; } = null!;
    [PluginService] public static ITargetManager targetManager { get; private set; } = null!;

    [PluginService] public static IPluginLog Log { get; private set; } = null!;
}
