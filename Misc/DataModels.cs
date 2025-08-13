using Rage;

namespace MTFO.Misc
{
    internal enum YieldTaskType
    {
        MoveRight,
        MoveLeft,
        ForceMoveRight,
        ForceMoveLeft
    }

    internal struct YieldTask
    {
        public Vector3 TargetPosition;
        public YieldTaskType TaskType;
        public uint GameTimeStarted;
    }

    internal struct CreepTask
    {
        public Vector3 TargetPosition;
        public uint GameTimeStarted;
    }

    internal struct AroundPlayerTask
    {
        public Vector3 TargetPosition;
        public uint GameTimeStarted;
        public uint GameTimeBackupStarted;
    }

    internal enum OvertakeFailureReason
    {
        SideTraceHit,
        NoRoadFound,
        BadHeading,
        TargetTooFarOrHigh,
        PathTraceHit
    }

    internal static class GameModels
    {
        private static readonly uint[] TrafficLightModels =
        {
            0x3e2b73a4, 0x336e5e2a, 0xd8eba922, 0xd4729f50,
            0x272244b2, 0x33986eae, 0x2323cdc5
        };

        public static readonly uint[] StopSignModels =
        {
            0xC76BD3AB,
            0x78F4B6BE
        };

        public static readonly uint[] AllIntersectionModels;

        static GameModels()
        {
            AllIntersectionModels = new uint[TrafficLightModels.Length + StopSignModels.Length];
            TrafficLightModels.CopyTo(AllIntersectionModels, 0);
            StopSignModels.CopyTo(AllIntersectionModels, TrafficLightModels.Length);
        }
    }
}