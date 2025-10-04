using INIUtility;

namespace MTFO
{
    public sealed class MtfoSettings
    {
        #region Configuration

        [ConfigOption("Configuration", "ShowDebugLines", "Show debug lines and shapes for visualization.")]
        public static bool ShowDebugLines { get; set; }

        [ConfigOption("Configuration", "EnableOpticom", "Enable Opticom feature to change traffic lights to green.")]
        public static bool EnableOpticom { get; set; } = true;

        [ConfigOption("Configuration", "EnableSameSideYield", "Enable vehicles on the same side as the player to yield.")]
        public static bool EnableSameSideYield { get; set; } = true;

        [ConfigOption("Configuration", "EnableOncomingBraking", "Enable oncoming traffic to brake for the player.")]
        public static bool EnableOncomingBraking { get; set; } = true;

        [ConfigOption("Configuration", "EnableIntersectionCreep", "Enable stopped vehicles to creep forward to clear a path.")]
        public static bool EnableIntersectionCreep { get; set; } = true;

        [ConfigOption("Configuration", "EnableIntersectionControl", "Enable logic to stop cross-traffic at intersections.")]
        public static bool EnableIntersectionControl { get; set; } = true;

        #endregion

        #region Around Player Configuration

        [ConfigOption("Configuration", "EnableAroundPlayerLogic", "Enable logic for vehicles stuck behind the player to overtake.")]
        public static bool EnableAroundPlayerLogic { get; set; }

        [ConfigOption("Configuration", "AroundPlayerLogicOnlyInVehicle", "If true, 'Around Player' logic only runs when player is in a vehicle.")]
        public static bool AroundPlayerLogicOnlyInVehicle { get; set; } = true;

        [ConfigOption("AroundPlayer", "AroundPlayerDetectionRange", "How far behind the player to look for vehicles to overtake.")]
        public static float AroundPlayerDetectionRange { get; set; } = 30f;

        [ConfigOption("AroundPlayer", "AroundPlayerDetectionWidth", "The width of the detection area behind the player.")]
        public static float AroundPlayerDetectionWidth { get; set; } = 8f;

        [ConfigOption("AroundPlayer", "AroundPlayerOvertakeDistance", "How far ahead of the player vehicles will target when overtaking.")]
        public static float AroundPlayerOvertakeDistance { get; set; } = 20f;

        [ConfigOption("AroundPlayer", "AroundPlayerTaskTimeoutMs", "Time in milliseconds before an overtake task is cancelled.")]
        public static uint AroundPlayerTaskTimeoutMs { get; set; } = 4000;

        [ConfigOption("AroundPlayer", "AroundPlayerTaskCompletionDistance", "How close a vehicle must get to its target to complete the task.")]
        public static float AroundPlayerTaskCompletionDistance { get; set; } = 3.0f;

        #endregion

        #region Opticom

        [ConfigOption("Opticom", "OpticomGreenDurationMs", "Duration in milliseconds the traffic light stays green.")]
        public static int OpticomGreenDurationMs { get; set; } = 6000;

        [ConfigOption("Opticom", "OpticomFlashYellowCount", "How many times the light flashes yellow before turning green.")]
        public static int OpticomFlashYellowCount { get; set; } = 1;

        [ConfigOption("Opticom", "OpticomFlashYellowInterval", "Interval in milliseconds for the yellow flashing.")]
        public static int OpticomFlashYellowInterval { get; set; } = 500;

        [ConfigOption("Opticom", "OpticomFlashYellowFirst", "Whether to flash yellow before turning green.")]
        public static bool OpticomFlashYellowFirst { get; set; } = false;

        #endregion

        #region Tuning Constants

        [ConfigOption("TuningConstants", "StoppedPlayerTimeoutMs", "Time player must be stopped before yielding/intersection logic is paused.")]
        public static uint StoppedPlayerTimeoutMs { get; set; } = 2000;

        [ConfigOption("TuningConstants", "IntersectionDetectionCooldownMs", "Cooldown after clearing an intersection before a new one can be detected.")]
        public static uint IntersectionDetectionCooldownMs { get; set; } = 1300;

        [ConfigOption("TuningConstants", "DetectionRange", "Forward detection range for vehicles.")]
        public static float DetectionRange { get; set; } = 40f;

        [ConfigOption("TuningConstants", "DetectionStartWidth", "Width of the detection cone near the player vehicle.")]
        public static float DetectionStartWidth { get; set; } = 3.5f;

        [ConfigOption("TuningConstants", "DetectionEndWidth", "Width of the detection cone at max range.")]
        public static float DetectionEndWidth { get; set; } = 5f;

        [ConfigOption("TuningConstants", "DetectionHeightOffset", "Vertical offset for the center of the detection area.")]
        public static float DetectionHeightOffset { get; set; } = -1f;

        [ConfigOption("TuningConstants", "DetectionAreaHeight", "Total height of the detection area.")]
        public static float DetectionAreaHeight { get; set; } = 12.0f;

        #endregion

        #region Yield Params

        [ConfigOption("YieldParams", "ForwardMoveDistance", "How far forward yielding vehicles will try to move.")]
        public static float ForwardMoveDistance { get; set; } = 35f;

        [ConfigOption("YieldParams", "SideMoveDistance", "How far to the side yielding vehicles will try to move.")]
        public static float SideMoveDistance { get; set; } = 6f;

        [ConfigOption("YieldParams", "ForceSideMoveDistance", "How far to the side yielding vehicles will move if path is blocked.")]
        public static float ForceSideMoveDistance { get; set; } = 6.0f;

        [ConfigOption("YieldParams", "DriveSpeed", "The speed at which yielding vehicles drive to their target position.")]
        public static float DriveSpeed { get; set; } = 15f;

        #endregion

        #region Same-side Yield Params

        [ConfigOption("Same-sideYieldParams", "SameSideYieldCompletionDistance", "Distance to target to consider a yield task complete.")]
        public static float SameSideYieldCompletionDistance { get; set; } = 3.0f;

        [ConfigOption("Same-sideYieldParams", "SameSideYieldAbandonDistance", "Distance from target where a yield task is cancelled.")]
        public static float SameSideYieldAbandonDistance { get; set; } = 45.0f;

        [ConfigOption("Same-sideYieldParams", "SameSideYieldTimeoutMs", "Time in milliseconds before a same-side yield task is cancelled.")]
        public static uint SameSideYieldTimeoutMs { get; set; } = 3000;

        #endregion

        #region Oncoming Traffic

        [ConfigOption("OncomingTraffic", "OncomingBrakeHeadingDot", "Heading dot product threshold to be considered oncoming traffic.")]
        public static float OncomingBrakeHeadingDot { get; set; } = -0.7f;

        [ConfigOption("OncomingTraffic", "OncomingBrakeMinLateral", "Minimum lateral offset for oncoming traffic to brake.")]
        public static float OncomingBrakeMinLateral { get; set; } = -19.0f;

        [ConfigOption("OncomingTraffic", "OncomingBrakeMaxLateral", "Maximum lateral offset for oncoming traffic to brake.")]
        public static float OncomingBrakeMaxLateral { get; set; } = -1.5f;

        [ConfigOption("OncomingTraffic", "OncomingBrakeDurationMs", "Duration in milliseconds for the braking maneuver.")]
        public static int OncomingBrakeDurationMs { get; set; } = 1500;

        #endregion

        #region Intersection Detection

        [ConfigOption("IntersectionDetection", "IntersectionSearchMinDistance", "Minimum distance to start searching for intersections.")]
        public static float IntersectionSearchMinDistance { get; set; } = 30f;

        [ConfigOption("IntersectionDetection", "IntersectionSearchMaxDistance", "Maximum distance to search for intersections.")]
        public static float IntersectionSearchMaxDistance { get; set; } = 45f;

        [ConfigOption("IntersectionDetection", "IntersectionSearchStepSize", "Step size when searching forward for intersections.")]
        public static float IntersectionSearchStepSize { get; set; } = 7.0f;

        [ConfigOption("IntersectionDetection", "IntersectionSearchRadius", "Radius to search for traffic light/stop sign objects.")]
        public static float IntersectionSearchRadius { get; set; } = 45.0f;

        [ConfigOption("IntersectionDetection", "IntersectionHeadingThreshold", "Allowed heading difference between player and traffic light.")]
        public static float IntersectionHeadingThreshold { get; set; } = 40.0f;

        [ConfigOption("IntersectionDetection", "CrossTrafficHeadingDotThreshold", "Heading dot product to be considered cross-traffic.")]
        public static float CrossTrafficHeadingDotThreshold { get; set; } = 0.25f;

        #endregion

        #region Intersection Creep

        [ConfigOption("IntersectionCreep", "MinYieldSpeedMph", "Speed below which a vehicle is considered 'stopped' and can creep.")]
        public static float MinYieldSpeedMph { get; set; } = 4.0f;

        [ConfigOption("IntersectionCreep", "IntersectionCreepForwardDistance", "How far forward vehicles will creep.")]
        public static float IntersectionCreepForwardDistance { get; set; } = 8.5f;

        [ConfigOption("IntersectionCreep", "IntersectionCreepSideDistance", "How far to the side vehicles will creep.")]
        public static float IntersectionCreepSideDistance { get; set; } = 6.5f;

        [ConfigOption("IntersectionCreep", "IntersectionCreepDriveSpeed", "The speed at which vehicles perform the creep maneuver.")]
        public static float IntersectionCreepDriveSpeed { get; set; } = 12f;

        [ConfigOption("IntersectionCreep", "CreepTaskCompletionDistance", "Distance to target to consider a creep task complete.")]
        public static float CreepTaskCompletionDistance { get; set; } = 1.5f;

        [ConfigOption("IntersectionCreep", "CreepTaskAbandonDistance", "Distance from target where a creep task is cancelled.")]
        public static float CreepTaskAbandonDistance { get; set; } = 13.0f;

        [ConfigOption("IntersectionCreep", "CreepTaskTimeoutMs", "Time in milliseconds before a creep task is cancelled.")]
        public static uint CreepTaskTimeoutMs { get; set; } = 2500;

        #endregion
    }
}