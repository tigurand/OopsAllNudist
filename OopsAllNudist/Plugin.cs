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

            if (Service.configuration.debugMode)
            {
                Service.pluginInterface.UiBuilder.DisableAutomaticUiHide = true;
                Service.pluginInterface.UiBuilder.DisableCutsceneUiHide = true;
                Service.pluginInterface.UiBuilder.DisableGposeUiHide = true;
                Service.pluginInterface.UiBuilder.DisableUserUiHide = true;
            }
            else
            {
                Service.pluginInterface.UiBuilder.DisableAutomaticUiHide = false;
                Service.pluginInterface.UiBuilder.DisableCutsceneUiHide = false;
                Service.pluginInterface.UiBuilder.DisableGposeUiHide = false;
                Service.pluginInterface.UiBuilder.DisableUserUiHide = false;
            }
            Service.configuration.Initialize(pluginInterface);

            _ = pluginInterface.Create<Service>();
            Service.plugin = this;
            Service.penumbraApi = new PenumbraIpc(pluginInterface);
            Service.glamourerApi = new GlamourerService(pluginInterface);
            Service.configWindow = new ConfigWindow(this);
            Service.whitelistWindow = new WhitelistWindow(this);
            WindowSystem.AddWindow(Service.configWindow);
            WindowSystem.AddWindow(Service.whitelistWindow);

            _ = pluginInterface.Create<Drawer>();

            pluginInterface.UiBuilder.Draw += DrawUI;
            pluginInterface.UiBuilder.OpenMainUi += DrawConfigUI;
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
            Drawer.RefreshAllPlayers(false);

            WindowSystem.RemoveAllWindows();

            Service.penumbraApi?.Dispose();
            Service.glamourerApi.Dispose();
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
                Service.configuration.enabled = !(Service.configuration.enabled);
                Service.configuration.Save();
                Service.configWindow.InvokeConfigChanged();
                return;
            }
            if (args == "debug on")
            {
                Service.configuration.debugMode = true;
                Service.configuration.Save();
                Service.configWindow.InvokeConfigChanged();
                Service.pluginInterface.UiBuilder.DisableAutomaticUiHide = true;
                Service.pluginInterface.UiBuilder.DisableCutsceneUiHide = true;
                Service.pluginInterface.UiBuilder.DisableGposeUiHide = true;
                Service.pluginInterface.UiBuilder.DisableUserUiHide = true;
                OutputChatLine("Debug mode on.");
                return;
            }
            if (args == "debug off")
            {
                Service.configuration.debugMode = false;
                Service.configuration.Save();
                Service.configWindow.InvokeConfigChanged();
                Service.pluginInterface.UiBuilder.DisableAutomaticUiHide = false;
                Service.pluginInterface.UiBuilder.DisableCutsceneUiHide = false;
                Service.pluginInterface.UiBuilder.DisableGposeUiHide = false;
                Service.pluginInterface.UiBuilder.DisableUserUiHide = false;
                OutputChatLine("Debug mode off.");
                return;
            }
            Service.configWindow.IsOpen = true;
        }

        private void DrawUI() => WindowSystem.Draw();

        public static void DrawConfigUI() => Service.configWindow.IsOpen = true;
    }
}
