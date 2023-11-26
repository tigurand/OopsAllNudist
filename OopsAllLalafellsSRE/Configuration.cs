using Dalamud.Configuration;
using Dalamud.Plugin;
using System;
using static OopsAllLalafellsSRE.Windows.Constant;

namespace OopsAllLalafellsSRE
{
    [Serializable]
    public class Configuration : IPluginConfiguration
    {
        public int Version { get; set; } = 0;
        public Race SelectedRace { get; set; } = Race.LALAFELL;

        public bool changeSelf { get; set; } = false;
        public bool changeOthers { get; set; } = false;
        public bool memorizeConfig { get; set; } = false;
        public bool immersiveMode { get; set; } = false;

        // the below exist just to make saving less cumbersome
        [NonSerialized]
        private DalamudPluginInterface? pluginInterface;

        public void Initialize(DalamudPluginInterface pluginInterface)
        {
            this.pluginInterface = pluginInterface;
        }

        public void Save()
        {
            pluginInterface!.SavePluginConfig(this);
        }
    }
}
