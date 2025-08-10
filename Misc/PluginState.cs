using System.Collections.Generic;
using Rage;

namespace MTFO.Misc
{
    internal static class PluginState
    {
        // --- Plugin State ---
        public static bool IsSilentModeActive; // Is the main logic running?
        public static GameFiber PluginFiber; // Primary fiber
        public static uint TimePlayerStopped; // Timer for when the player vehicle stops.

        // Tracked vehicles during a yield
        public static readonly Dictionary<Vehicle, YieldTask> TaskedVehicles = new Dictionary<Vehicle, YieldTask>();

        // Tracks vehicles stopped at an intersection for cross-traffic
        public static readonly HashSet<Vehicle> IntersectionTaskedVehicles = new HashSet<Vehicle>();

        // Tracks vehicles told to creep at an intersection, and where they're going.
        public static readonly Dictionary<Vehicle, CreepTask> IntersectionCreepTaskedVehicles = new Dictionary<Vehicle, CreepTask>();

        public static Vector3? ActiveIntersectionCenter; // The current intersection location
        public static bool IsStopSignIntersection; // Differentiates between stop signs and traffic lights.
        public static uint IntersectionClearTime; // Cooldown timer for intersection detection.

        // Tracks blips attached to any tasked vehicle for easy cleanup.
        public static readonly Dictionary<Vehicle, Blip> TaskedVehicleBlips = new Dictionary<Vehicle, Blip>();
    }
}