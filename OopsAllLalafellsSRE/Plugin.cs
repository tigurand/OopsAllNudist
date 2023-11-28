using Dalamud.Game.Command;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using OopsAllLalafellsSRE.Utils;
using OopsAllLalafellsSRE.Windows;

namespace OopsAllLalafellsSRE
{
    internal sealed class Plugin : IDalamudPlugin
    {
        public static string Name => "OopsAllLalafellsSRE";
        private const string CommandName = "/polala";

        public WindowSystem WindowSystem { get; } = new("OopsAllLalafellsSRE");

        public Plugin([RequiredVersion("1.0")] DalamudPluginInterface pluginInterface)
        {
            Service.pluginInterface = pluginInterface;
            Service.configuration = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            Service.configuration.Initialize(pluginInterface);

            _ = pluginInterface.Create<Service>();
            Service.plugin = this;
            Service.penumbraApi = new PenumbraIpc();
            Service.configWindow = new ConfigWindow(this);
            WindowSystem.AddWindow(Service.configWindow);

            _ = pluginInterface.Create<Drawer>();

            pluginInterface.UiBuilder.Draw += DrawUI;
            pluginInterface.UiBuilder.OpenConfigUi += DrawConfigUI;

            Service.commandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
            {
                HelpMessage = "Opens OopsAllLalafellsSRE config menu."
            });
        }

        public static void OutputChatLine(SeString message)
        {
            var sb = new SeStringBuilder().AddUiForeground("[OAL] ", 58).Append(message);
            Service.chatGui.Print(new XivChatEntry { Message = sb.BuiltString });
        }

        public void Dispose()
        {
            WindowSystem.RemoveAllWindows();
            Service.configWindow.Dispose();
            Service.penumbraApi?.Dispose();
            Service.drawer?.Dispose();
            Service.commandManager.RemoveHandler(CommandName);
        }

        private void OnCommand(string command, string args)
        {
            Service.configWindow.IsOpen = true;
        }

        private void DrawUI() => WindowSystem.Draw();

        public static void DrawConfigUI() => Service.configWindow.IsOpen = true;
    }
}
