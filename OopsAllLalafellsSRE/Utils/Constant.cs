using Dalamud.Game.ClientState.Objects.Enums;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace OopsAllLalafellsSRE.Windows
{
    public class Constant
    {
        // from Plugin.cs
        public const uint FLAG_INVIS = (1 << 1) | (1 << 11);
        public const uint CHARA_WINDOW_ACTOR_ID = 0xE0000000;
        public const int OFFSET_RENDER_TOGGLE = 0x104;

        public static readonly short[,] RACE_STARTER_GEAR_ID_MAP =
        {
            {84, 85}, // Hyur
            {86, 87}, // Elezen
            {92, 93}, // Lalafell
            {88, 89}, // Miqo
            {90, 91}, // Roe
            {257, 258}, // Au Ra
            {597, -1}, // Hrothgar
            {-1, 581}, // Viera
        };

        public static readonly short[] RACE_STARTER_GEAR_IDS;
        static Constant()
        {
            RACE_STARTER_GEAR_IDS = RACE_STARTER_GEAR_ID_MAP.Cast<short>().Where(id => id != -1).ToArray();
        }

        // from Race.cs
        public enum Race : byte
        {
            HYUR = 1,
            ELEZEN = 2,
            LALAFELL = 3,
            MIQOTE = 4,
            ROEGADYN = 5,
            AU_RA = 6,
            HROTHGAR = 7,
            VIERA = 8
        }

        public class RaceMappings
        {
            public static readonly Dictionary<Race, int> RaceHairs = new()
            {
                { Race.HYUR, 13 },
                { Race.ELEZEN, 12 },
                { Race.LALAFELL, 13 },
                { Race.MIQOTE, 12 },
                { Race.ROEGADYN, 13 },
                { Race.AU_RA, 12 },
                { Race.HROTHGAR, 8 },
                { Race.VIERA, 17 },
            };
        }

        // from EquipData.cs
        public class EquipDataOffsets
        {
            public const int Model = 0x0;
            public const int Variant = 0x2;
            public const int Dye = 0x3;
        }

        // from CharaCustomizeData.cs
        [StructLayout(LayoutKind.Explicit)]
        public struct EquipData
        {
            [FieldOffset(EquipDataOffsets.Model)] public short model;
            [FieldOffset(EquipDataOffsets.Variant)] public byte variant;
            [FieldOffset(EquipDataOffsets.Dye)] public byte dye;
        }

        [StructLayout((LayoutKind.Explicit))]
        public struct CharaCustomizeData
        {
            [FieldOffset((int)CustomizeIndex.Race)] public Race Race;
            [FieldOffset((int)CustomizeIndex.Gender)] public byte Gender;
            [FieldOffset((int)CustomizeIndex.ModelType)] public byte ModelType;
            [FieldOffset((int)CustomizeIndex.Tribe)] public byte Tribe;
            [FieldOffset((int)CustomizeIndex.FaceType)] public byte FaceType;
            [FieldOffset((int)CustomizeIndex.HairStyle)] public byte HairStyle;
            [FieldOffset((int)CustomizeIndex.LipColor)] public byte LipColor;
        }
    }
}
