using Dalamud.Configuration;
using Dalamud.Plugin;
using Newtonsoft.Json;
using System;

namespace OopsAllLalafellsSRE
{
    public class Configuration : IPluginConfiguration
    {
        [NonSerialized]
        private DalamudPluginInterface? pluginInterface;

        public int Version { get; set; } = 1;

        public Race ChangeOthersTargetRace { get; set; } = Race.LALAFELL;

        public bool ShouldChangeOthers { get; set; } = false;

        [JsonIgnore] // Experimental feature - do not load/save
        public bool ImmersiveMode { get; set; } = false;

        public void Initialize(DalamudPluginInterface pluginInterface)
        {
            this.pluginInterface = pluginInterface;
        }

        public void Save()
        {
            pluginInterface?.SavePluginConfig(this);
        }
    }
}
