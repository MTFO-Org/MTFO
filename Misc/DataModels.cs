using Rage;

namespace MTFO.Misc
{
    // Defines the different types of yielding maneuvers.
    internal enum YieldTaskType
    {
        MoveRight,
        MoveLeft,
        ForceMoveRight,
        ForceMoveLeft
    }

    // Holds the data for a single vehicle's yield task.
    internal struct YieldTask
    {
        public Vector3 TargetPosition;
        public YieldTaskType TaskType;
    }

    // Holds the data for a single vehicle's creep task; uses game timestamp for timeouts.
    internal struct CreepTask
    {
        public Vector3 TargetPosition;
        public uint GameTimeStarted;
    }

    internal static class GameModels
    {
        // Traffic light hashes
        public static readonly uint[] TrafficLightModels =
        {
            0x3e2b73a4, 0x336e5e2a, 0xd8eba922, 0xd4729f50,
            0x272244b2, 0x33986eae, 0x2323cdc5
        };

        // Stop sign hashes
        public static readonly uint[] StopSignModels =
        {
            0xC76BD3AB,
            0x78F4B6BE
        };
    }
}