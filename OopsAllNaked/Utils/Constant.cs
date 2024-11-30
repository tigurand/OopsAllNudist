using Dalamud.Game.ClientState.Objects.Enums;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace OopsAllNaked.Utils
{
    internal static class Constant
    {
        public enum Race : byte
        {
            UNKNOWN = 0,
            HYUR = 1,
            ELEZEN = 2,
            LALAFELL = 3,
            MIQOTE = 4,
            ROEGADYN = 5,
            AU_RA = 6,
            HROTHGAR = 7,
            VIERA = 8
        }

        public enum Gender : byte
        {
            UNKNOWN = 0,
            FEMALE = 1,
            MALE = 2
        }

        public enum Clan : byte
        {
            CLAN0 = 0,
            CLAN1 = 1,
            UNKNOWN = 255
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

        [StructLayout(LayoutKind.Explicit)]
        public struct CharaCustomizeData
        {
            [FieldOffset((int)CustomizeIndex.Race)] public Race Race;
            [FieldOffset((int)CustomizeIndex.Gender)] public Gender Gender;
            [FieldOffset((int)CustomizeIndex.ModelType)] public byte ModelType;
            [FieldOffset((int)CustomizeIndex.Tribe)] public byte Tribe;
            [FieldOffset((int)CustomizeIndex.FaceType)] public byte FaceType;
            [FieldOffset((int)CustomizeIndex.HairStyle)] public byte HairStyle;
        }
    }
}
