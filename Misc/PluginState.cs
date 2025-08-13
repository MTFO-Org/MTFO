using System.Collections.Generic;
using Rage;

namespace MTFO.Misc
{
    internal static class PluginState
    {
        public static bool IsSilentModeActive;
        public static GameFiber PluginFiber;
        public static uint TimePlayerStopped;
        public static Vehicle LastPlayerVehicle;

        public static uint NextYieldScanTime;
        public static uint NextIntersectionScanTime;

        public static readonly Dictionary<Vehicle, YieldTask> TaskedVehicles = new Dictionary<Vehicle, YieldTask>();

        public static readonly Dictionary<Vehicle, uint> OncomingBrakingVehicles = new Dictionary<Vehicle, uint>();

        public static readonly HashSet<Vehicle> IntersectionTaskedVehicles = new HashSet<Vehicle>();

        public static readonly Dictionary<Vehicle, CreepTask> IntersectionCreepTaskedVehicles = new Dictionary<Vehicle, CreepTask>();

        public static readonly Dictionary<Vehicle, Vector3> FailedCreepCandidates = new Dictionary<Vehicle, Vector3>();

        public static readonly Dictionary<Vehicle, (Vector3, OvertakeFailureReason)> FailedAroundPlayerCandidates = new Dictionary<Vehicle, (Vector3, OvertakeFailureReason)>();

        public static Vector3? ActiveIntersectionCenter;
        public static bool IsStopSignIntersection;
        public static uint IntersectionClearTime;

        public static readonly Dictionary<Vehicle, Blip> TaskedVehicleBlips = new Dictionary<Vehicle, Blip>();

        public static readonly Dictionary<Vehicle, AroundPlayerTask> AroundPlayerTaskedVehicles = new Dictionary<Vehicle, AroundPlayerTask>();
    }
}