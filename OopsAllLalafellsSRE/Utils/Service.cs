using Dalamud.Game;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using OopsAllLalafellsSRE.Windows;

namespace OopsAllLalafellsSRE.Utils;

internal class Service
{
    internal static Plugin plugin { get; set; } = null!;
    internal static ConfigWindow configWindow { get; set; } = null!;
    internal static Configuration configuration { get; set; } = null!;
    internal static Hooking hooking { get; set; } = null!;

    [PluginService] public static DalamudPluginInterface pluginInterface { get; set; } = null!;
    [PluginService] public static IChatGui chatGui { get; private set; } = null!;
    [PluginService] public static IClientState clientState { get; private set; } = null!;
    [PluginService] public static ICommandManager commandManager { get; set; } = null!;
    [PluginService] public static IObjectTable objectTable { get; private set; } = null!;
    [PluginService] public static ISigScanner sigScanner { get; private set; } = null!;
    [PluginService] public static IPluginLog pluginLog { get; private set; } = null!;
    [PluginService] public static IGameInteropProvider gameInteropProvider { get; private set; } = null!;
}
