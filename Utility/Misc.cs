using System.Collections.Generic;
using System.Text;
using INIUtility;
using LSPD_First_Response.Mod.API;
using Rage;

namespace MTFOv4
{
    [ConfigHeader("MTFO", "Author: Guess1m", "Config Version: 4.0.0")]
    public sealed class MtfoSettings
    {
        [ConfigOption("Debug", "ShowDebugLines", "Toggles the display of 3D debugging lines, arrows, boundaries, and on-screen status subtitles.")]
        public bool ShowDebugLines { get; set; } = false;

        [ConfigOption("Debug", "ShowDebugNodes", "Toggles the display of pathfinding nodes and road logic visuals for debugging purposes.")]
        public bool ShowDebugNodes { get; set; } = false;


        [ConfigOption("Features", "EnableOpticom", "Toggles the system that forces traffic lights to turn green when approached with active sirens.")]
        public bool EnableOpticom { get; set; } = true;

        [ConfigOption("Features", "EnableIntersectionControl", "Toggles the logic that forces cross-traffic to brake and yield at upcoming intersections or stop signs.")]
        public bool EnableIntersectionControl { get; set; } = true;

        [ConfigOption("Features", "EnableAroundPlayerLogic", "Toggles the logic that commands AI vehicles to dynamically overtake or navigate around a stationary or slow-moving player.")]
        public bool EnableAroundPlayerLogic { get; set; } = true;

        [ConfigOption("Features", "AroundPlayerLogicOnlyInVehicle", "If true, the around-player overtaking logic will only trigger when the player ped is actively seated inside a vehicle.")]
        public bool AroundPlayerLogicOnlyInVehicle { get; set; } = false;

        [ConfigOption("Features", "EnableOncomingBraking", "Toggles the logic that forces oncoming traffic to pull over or brake when approached by an emergency vehicle.")]
        public bool EnableOncomingBraking { get; set; } = true;


        [ConfigOption("AroundPlayer", "AroundPlayerDetectionRange", "The maximum distance (in meters) behind the player to scan for vehicles that need to navigate around them.")]
        public float AroundPlayerDetectionRange { get; set; } = 30f;

        [ConfigOption("AroundPlayer", "AroundPlayerDetectionWidth", "The maximum lateral width (in meters) to detect blocked vehicles behind the player.")]
        public float AroundPlayerDetectionWidth { get; set; } = 8f;

        [ConfigOption("AroundPlayer", "AroundPlayerOvertakeDistance", "The forward distance (in meters) from the player where the AI will attempt to plot its overtake target position.")]
        public float AroundPlayerOvertakeDistance { get; set; } = 20f;

        [ConfigOption("AroundPlayer", "AroundPlayerTaskTimeoutMs", "The maximum time (in milliseconds) an AI vehicle will attempt the around-player maneuver before the task expires.")]
        public uint AroundPlayerTaskTimeoutMs { get; set; } = 4000;

        [ConfigOption("AroundPlayer", "AroundPlayerTaskCompletionDistance", "The distance (in meters) from the target node at which the around-player task is considered successfully completed.")]
        public float AroundPlayerTaskCompletionDistance { get; set; } = 3f;


        [ConfigOption("Opticom", "OpticomGreenDurationMs", "The duration (in milliseconds) the traffic light will remain forced green after being triggered.")]
        public int OpticomGreenDurationMs { get; set; } = 6000;

        [ConfigOption("Opticom", "OpticomFlashYellowCount", "The number of times the traffic light will cycle through the warning flash sequence before turning green.")]
        public int OpticomFlashYellowCount { get; set; } = 1;

        [ConfigOption("Opticom", "OpticomFlashYellowInterval", "The delay (in milliseconds) between the yellow and red states during the pre-green warning flash sequence.")]
        public int OpticomFlashYellowInterval { get; set; } = 300;

        [ConfigOption("Opticom", "OpticomFlashYellowFirst", "If true, the traffic light will perform a yellow-to-red warning flash sequence to alert cross-traffic before turning green.")]
        public bool OpticomFlashYellowFirst { get; set; } = false;


        [ConfigOption("OncomingBrake", "OncomingBrakeHeadingDot", "The minimum heading dot product required to classify a vehicle as traveling in the oncoming direction.")]
        public float OncomingBrakeHeadingDot { get; set; } = -0.7f;

        [ConfigOption("OncomingBrake", "OncomingBrakeMinLateral", "The minimum lateral offset (in meters) from the player required to trigger oncoming braking.")]
        public float OncomingBrakeMinLateral { get; set; } = -19f;

        [ConfigOption("OncomingBrake", "OncomingBrakeMaxLateral", "The maximum lateral offset (in meters) from the player required to trigger oncoming braking.")]
        public float OncomingBrakeMaxLateral { get; set; } = -1.5f;

        [ConfigOption("OncomingBrake", "OncomingBrakeDurationMs", "The duration (in milliseconds) that an oncoming vehicle will hold its brakes before resuming normal driving.")]
        public int OncomingBrakeDurationMs { get; set; } = 1500;


        [ConfigOption("Intersection", "IntersectionDetectionCooldownMs", "The cooldown time (in milliseconds) after passing an intersection before the system begins scanning for the next one.")]
        public uint IntersectionDetectionCooldownMs { get; set; } = 900;

        [ConfigOption("Intersection", "DetectionRange", "The standard forward search radius (in meters) used to find oncoming traffic and yield candidates.")]
        public float DetectionRange { get; set; } = 45f;

        [ConfigOption("Intersection", "IntersectionSearchMinDistance", "The minimum distance (in meters) ahead of the player to start scanning for intersection objects like lights and stop signs.")]
        public float IntersectionSearchMinDistance { get; set; } = 30f;

        [ConfigOption("Intersection", "IntersectionSearchMaxDistance", "The maximum distance (in meters) ahead of the player to stop scanning for intersection objects.")]
        public float IntersectionSearchMaxDistance { get; set; } = 45f;

        [ConfigOption("Intersection", "IntersectionSearchStepSize", "The increment (in meters) used when stepping from the maximum to minimum distance during the intersection object raycast.")]
        public float IntersectionSearchStepSize { get; set; } = 7f;

        [ConfigOption("Intersection", "IntersectionSearchRadius", "The spherical radius (in meters) around each search step used to detect valid intersection props.")]
        public float IntersectionSearchRadius { get; set; } = 65f;

        [ConfigOption("Intersection", "IntersectionHeadingThreshold", "The maximum heading difference (in degrees) allowed between the player and an intersection prop to consider it relevant.")]
        public float IntersectionHeadingThreshold { get; set; } = 40f;

        [ConfigOption("Intersection", "CrossTrafficHeadingDotThreshold", "The absolute dot product threshold used to identify perpendicular cross-traffic vehicles approaching the intersection.")]
        public float CrossTrafficHeadingDotThreshold { get; set; } = 0.25f;

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine("--- MTFO Loaded Settings ---");
            foreach (var prop in GetType().GetProperties())
                if (prop.GetCustomAttributes(typeof(ConfigOptionAttribute), false).Length > 0)
                    sb.AppendLine($" {prop.Name}: '{prop.GetValue(this)}'");
            sb.AppendLine("-----------------------------------");
            return sb.ToString();
        }
    }

    internal static class Misc
    {
        /// <summary>
        /// Checks if the driver of a vehicle is currently involved in an active LSPDFR pursuit.
        /// </summary>
        /// <param name="driver">The ped to check for pursuit status.</param>
        /// <returns>True if the ped is in a pursuit; otherwise, false.</returns>
        public static bool IsDriverInPursuit(Ped driver)
        {
            if (!driver.Exists()) return false;
            return Functions.IsPedInPursuit(driver);
        }
    }

    public class GameModels
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

    /// <summary>   
    /// Tracks a vehicle that has been tasked to move around the player.
    /// </summary>
    public struct AroundPlayerTask
    {
        public Vector3 TargetPosition;
        public uint GameTimeStarted;
        public uint GameTimeBackupStarted;
    }

    internal static class PluginState
    {
        public static readonly Dictionary<Vehicle, YieldTasker> ActiveYieldTaskers = new Dictionary<Vehicle, YieldTasker>();
        public static readonly Dictionary<Vehicle, uint> OncomingBrakingVehicles = new Dictionary<Vehicle, uint>();
        public static readonly Dictionary<Vehicle, AroundPlayerTask> AroundPlayerTaskedVehicles = new Dictionary<Vehicle, AroundPlayerTask>();
        public static readonly Dictionary<Vehicle, Blip> TaskedVehicleBlips = new Dictionary<Vehicle, Blip>();
        public static readonly HashSet<Vehicle> IntersectionTaskedVehicles = new HashSet<Vehicle>();

        public static Vector3? ActiveIntersectionCenter;
        public static bool IsStopSignIntersection;
        public static uint NextIntersectionScanTime;
        public static uint IntersectionClearTime;
    }

    /// <summary>
    /// Defines the reasons why an AI might fail to find a valid overtake path around the player.
    /// </summary>
    internal enum OvertakeFailureReason
    {
        SideTraceHit,
        NoRoadFound,
        BadHeading,
        TargetTooFarOrHigh,
        PathTraceHit
    }
}