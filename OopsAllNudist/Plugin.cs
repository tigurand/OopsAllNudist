using Dalamud.Game.Command;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using OopsAllNudist.Utils;
using OopsAllNudist.Windows;
using Penumbra.Api.Enums;

namespace OopsAllNudist
{
    internal sealed class Plugin : IDalamudPlugin
    {
        public static string Name => "OopsAllNudist";
        private const string CommandName = "/nudist";

        public WindowSystem WindowSystem { get; } = new("OopsAllNudist");

        public Plugin(IDalamudPluginInterface pluginInterface)
        {
            Service.pluginInterface = pluginInterface;

            Service.configuration = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

            if (!Service.configuration.stayOn)
            {
                Service.configuration.enabled = false;
            }

            Service.configuration.Initialize(pluginInterface);

            _ = pluginInterface.Create<Service>();
            Service.plugin = this;
            Service.penumbraApi = new PenumbraIpc(pluginInterface);
            Service.configWindow = new ConfigWindow(this);
            Service.whitelistWindow = new WhitelistWindow(this);
            WindowSystem.AddWindow(Service.configWindow);
            WindowSystem.AddWindow(Service.whitelistWindow);

            _ = pluginInterface.Create<Drawer>();

            pluginInterface.UiBuilder.Draw += DrawUI;
            pluginInterface.UiBuilder.OpenConfigUi += DrawConfigUI;

            Service.commandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
            {
                HelpMessage = "Opens OopsAllNudist config menu. Can also be used with args on, off, or toggle."
            });
        }

        public static void OutputChatLine(SeString message)
        {
            var sb = new SeStringBuilder().AddUiForeground("[OAN] ", 58).Append(message);
            Service.chatGui.Print(new XivChatEntry { Message = sb.BuiltString });
        }

        public void Dispose()
        {
            Service.penumbraApi?.RedrawAll(RedrawType.Redraw);

            WindowSystem.RemoveAllWindows();

            Service.penumbraApi?.Dispose();
            Service.drawer?.Dispose();
            Service.commandManager?.RemoveHandler(CommandName);
        }

        private void OnCommand(string command, string args)
        {
            if (args == "on")
            {
                Service.configuration.enabled = true;
                Service.configuration.Save();
                Service.configWindow.InvokeConfigChanged();
                return;
            }
            if (args == "off")
            {
                Service.configuration.enabled = false;
                Service.configuration.Save();
                Service.configWindow.InvokeConfigChanged();
                return;
            }
            if (args == "toggle")
            {
                Service.configuration.enabled = (Service.configuration.enabled) ? false : true;
                Service.configuration.Save();
                Service.configWindow.InvokeConfigChanged();
                return;
            }
            Service.configWindow.IsOpen = true;
        }

        private void DrawUI() => WindowSystem.Draw();

        public static void DrawConfigUI() => Service.configWindow.IsOpen = true;
    }
}
