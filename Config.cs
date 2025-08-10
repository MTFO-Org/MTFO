using Rage;

namespace MTFO
{
    internal static class Config
    {
        #region Configuration
        /// <summary>
        ///     If true, displays debug lines and shapes in the game world to visualize detection zones and AI targets.
        /// </summary>
        public static bool ShowDebugLines = true;

        /// <summary>
        ///     If true, the plugin will attempt to turn traffic lights green for the player's vehicle.
        /// </summary>
        public static bool EnableOpticom = true;

        #endregion
        
        #region Tuning Constants
        /// <summary>
        ///     How long a traffic light will remain green (in milliseconds) after being triggered by the player.
        /// </summary>
        public static int OpticomGreenDurationMs = 6000;

        /// <summary>
        ///     If the player's vehicle is stopped for longer than this duration (in milliseconds), all AI tasks are cleared to
        ///     prevent unwanted behavior.
        /// </summary>
        public static uint StoppedPlayerTimeoutMs = 2000;

        /// <summary>
        ///     The cooldown period (in milliseconds) after clearing an intersection before the plugin will scan for a new one.
        /// </summary>
        public static uint IntersectionDetectionCooldownMs = 1300;

        // --- Forward detection zone for yield ---
        /// <summary>
        ///     The maximum distance (in meters) in front of the player's vehicle to detect other vehicles that should yield.
        /// </summary>
        public static float DetectionRange = 55f;

        /// <summary>
        ///     The width (in meters) of the detection zone closest to the player's vehicle.
        /// </summary>
        public static float DetectionStartWidth = 3.5f;

        /// <summary>
        ///     The width (in meters) of the detection zone at its furthest point (at DetectionRange).
        /// </summary>
        public static float DetectionEndWidth = 5.7f;

        /// <summary>
        ///     A vertical offset (in meters) for the detection zone to better align with the road and vehicle heights.
        /// </summary>
        public static float DetectionHeightOffset = -1f;

        /// <summary>
        ///     The total vertical size (in meters) of the detection zone.
        /// </summary>
        public static float DetectionAreaHeight = 12.0f;
        #endregion

        #region Yield params 
        /// <summary>
        ///     The forward distance component used when calculating a target pull-over position for an AI vehicle.
        /// </summary>
        public static float ForwardMoveDistance = 35f;

        /// <summary>
        ///     The sideways distance (in meters) an AI vehicle will attempt to move when a clear path is detected.
        /// </summary>
        public static float SideMoveDistance = 6f;

        /// <summary>
        ///     The sideways distance (in meters) an AI vehicle will be forced to move if no clear path is found (e.g., to nudge
        ///     past an obstacle).
        /// </summary>
        public static float ForceSideMoveDistance = 6.0f;

        /// <summary>
        ///     The speed at which an AI vehicle drives towards its calculated yielding position.
        /// </summary>
        public static float DriveSpeed = 12f;
        #endregion

        #region Same-side yield params
        /// <summary>
        ///     If a yielding vehicle is closer to its target than this distance (in meters), the task is considered complete.
        /// </summary>
        public static float SameSideYieldCompletionDistance = 3.0f;

        /// <summary>
        ///     If a yielding vehicle gets further from its target than this distance (in meters), the task is abandoned.
        /// </summary>
        public static float SameSideYieldAbandonDistance = 45.0f;

        /// <summary>
        ///     The maximum time (in milliseconds) a vehicle will attempt a yield task before giving up.
        /// </summary>
        public static uint SameSideYieldTimeoutMs = 3000;
        #endregion

        #region Oncoming traffic brake logic
        /// <summary>
        ///     The heading dot product threshold to identify a vehicle as "oncoming." A value of -1 is directly oncoming.
        /// </summary>
        public static float OncomingBrakeHeadingDot = -0.7f;

        /// <summary>
        ///     Defines the minimum lateral distance (left side) for an oncoming vehicle to be considered a threat and told to
        ///     brake.
        /// </summary>
        public static float OncomingBrakeMinLateral = -19.0f;

        /// <summary>
        ///     Defines the maximum lateral distance (right side) for an oncoming vehicle to be considered a threat and told to
        ///     brake.
        /// </summary>
        public static float OncomingBrakeMaxLateral = -1.5f;

        /// <summary>
        ///     How long (in milliseconds) an oncoming vehicle should be forced to brake.
        /// </summary>
        public static int OncomingBrakeDurationMs = 1500;
        #endregion

        #region Intersection detection params 
        /// <summary>
        ///     The furthest distance (in meters) ahead of the player to scan for intersections.
        /// </summary>
        public static float IntersectionSearchMaxDistance = 45f;

        /// <summary>
        ///     The closest distance (in meters) ahead of the player to scan for intersections.
        /// </summary>
        public static float IntersectionSearchMinDistance = 30f;

        /// <summary>
        ///     The size of the steps (in meters) taken when scanning for an intersection between the min and max distances.
        /// </summary>
        public static float IntersectionSearchStepSize = 7.0f;

        /// <summary>
        ///     The radius (in meters) around a search point used to find intersection objects like traffic lights or stop signs.
        /// </summary>
        public static float IntersectionSearchRadius = 40.0f;

        /// <summary>
        ///     The maximum allowed heading difference (in degrees) between the player and a traffic light to consider it relevant.
        /// </summary>
        public static float IntersectionHeadingThreshold = 40.0f;

        /// <summary>
        ///     The heading dot product threshold used to identify vehicles that are part of cross-traffic at an intersection.
        /// </summary>
        public static float CrossTrafficHeadingDotThreshold = 0.25f;
        #endregion

        #region Intersection creep 
        /// <summary>
        ///     If a vehicle in the player's path is moving slower than this speed (in MPH), it can be told to "creep" forward.
        /// </summary>
        public static float MinYieldSpeedMph = 4.0f;

        /// <summary>
        ///     The forward distance (in meters) a slow vehicle will be instructed to creep.
        /// </summary>
        public static float IntersectionCreepForwardDistance = 8.5f;

        /// <summary>
        ///     The sideways distance (in meters) a slow vehicle will be instructed to creep.
        public static float IntersectionCreepSideDistance = 6.5f;

        /// <summary>
        ///     The speed at which a vehicle performs the creep maneuver.
        /// </summary>
        public static float IntersectionCreepDriveSpeed = 12f;

        /// <summary>
        ///     If a creeping vehicle is closer to its target than this distance (in meters), the task is considered complete.
        /// </summary>
        public static float CreepTaskCompletionDistance = 1.5f;

        /// <summary>
        ///     If a creeping vehicle gets further from its target than this distance (in meters), the task is abandoned.
        /// </summary>
        public static float CreepTaskAbandonDistance = 13.0f;

        /// <summary>
        ///     The maximum time (in milliseconds) a vehicle will attempt a creep task before giving up.
        /// </summary>
        public static uint CreepTaskTimeoutMs = 2500;
        #endregion

        internal static InitializationFile iniFile;
        internal static void Initialize()
        {
            try
            {
                iniFile = new InitializationFile(@"Plugins/LSPDFR/MTFO.ini");
                iniFile.Create();
                ShowDebugLines = iniFile.ReadBoolean("Configuration", "ShowDebugLines", ShowDebugLines);
                EnableOpticom = iniFile.ReadBoolean("Configuration", "EnableOpticom", EnableOpticom);
                
                OpticomGreenDurationMs = iniFile.ReadInt32("TuningConstants", "OpticomGreenDurationMs", OpticomGreenDurationMs);
                StoppedPlayerTimeoutMs = iniFile.ReadUInt32("TuningConstants", "StoppedPlayerTimeoutMs", StoppedPlayerTimeoutMs);
                IntersectionDetectionCooldownMs = iniFile.ReadUInt32("TuningConstants", "IntersectionDetectionCooldownMs", IntersectionDetectionCooldownMs);
                DetectionRange = iniFile.ReadSingle("TuningConstants", "DetectionRange", DetectionRange);
                DetectionStartWidth = iniFile.ReadSingle("TuningConstants", "DetectionStartWidth", DetectionStartWidth);
                DetectionEndWidth = iniFile.ReadSingle("TuningConstants", "DetectionEndWidth", DetectionEndWidth);
                DetectionHeightOffset = iniFile.ReadSingle("TuningConstants", "DetectionHeightOffset", DetectionHeightOffset);
                DetectionAreaHeight = iniFile.ReadSingle("TuningConstants", "DetectionAreaHeight", DetectionAreaHeight);
                
                ForwardMoveDistance = iniFile.ReadSingle("YieldParams", "ForwardMoveDistance", ForwardMoveDistance);
                SideMoveDistance = iniFile.ReadSingle("YieldParams", "SideMoveDistance", SideMoveDistance);
                ForceSideMoveDistance = iniFile.ReadSingle("YieldParams", "ForceSideMoveDistance", ForceSideMoveDistance);
                DriveSpeed = iniFile.ReadSingle("YieldParams", "DriveSpeed", DriveSpeed);
                
                SameSideYieldCompletionDistance = iniFile.ReadSingle("Same-sideYieldParams", "SameSideYieldCompletionDistance", SameSideYieldCompletionDistance);
                SameSideYieldAbandonDistance = iniFile.ReadSingle("Same-sideYieldParams", "SameSideYieldAbandonDistance", SameSideYieldAbandonDistance);
                SameSideYieldTimeoutMs = iniFile.ReadUInt32("Same-sideYieldParams", "SameSideYieldTimeoutMs", SameSideYieldTimeoutMs);
                
                OncomingBrakeHeadingDot = iniFile.ReadSingle("OncomingTraffic", "OncomingBrakeHeadingDot", OncomingBrakeHeadingDot);
                OncomingBrakeMinLateral = iniFile.ReadSingle("OncomingTraffic", "OncomingBrakeMinLateral", OncomingBrakeMinLateral);
                OncomingBrakeMaxLateral = iniFile.ReadSingle("OncomingTraffic", "OncomingBrakeMaxLateral", OncomingBrakeMaxLateral);
                OncomingBrakeDurationMs = iniFile.ReadInt32("OncomingTraffic", "OncomingBrakeDurationMs", OncomingBrakeDurationMs);
                
                IntersectionSearchMinDistance = iniFile.ReadSingle("IntersectionDetection", "IntersectionSearchMinDistance", IntersectionSearchMinDistance);
                IntersectionSearchMaxDistance = iniFile.ReadSingle("IntersectionDetection", "IntersectionSearchMaxDistance", IntersectionSearchMaxDistance);
                IntersectionSearchStepSize = iniFile.ReadSingle("IntersectionDetection", "IntersectionSearchStepSize", IntersectionSearchStepSize);
                IntersectionSearchRadius = iniFile.ReadSingle("IntersectionDetection", "IntersectionSearchRadius", IntersectionSearchRadius);
                IntersectionHeadingThreshold = iniFile.ReadSingle("IntersectionDetection", "IntersectionHeadingThreshold", IntersectionHeadingThreshold);
                CrossTrafficHeadingDotThreshold = iniFile.ReadSingle("IntersectionDetection", "CrossTrafficHeadingDotThreshold", CrossTrafficHeadingDotThreshold);
                
                MinYieldSpeedMph = iniFile.ReadSingle("IntersectionCreep", "MinYieldSpeedMph", MinYieldSpeedMph);
                IntersectionCreepForwardDistance = iniFile.ReadSingle("IntersectionCreep", "IntersectionCreepForwardDistance", IntersectionCreepForwardDistance);
                IntersectionCreepSideDistance = iniFile.ReadSingle("IntersectionCreep", "IntersectionCreepSideDistance", IntersectionCreepSideDistance);
                IntersectionCreepDriveSpeed = iniFile.ReadSingle("IntersectionCreep", "IntersectionCreepDriveSpeed", IntersectionCreepDriveSpeed);
                CreepTaskCompletionDistance = iniFile.ReadSingle("IntersectionCreep", "CreepTaskCompletionDistance", CreepTaskCompletionDistance);
                CreepTaskAbandonDistance = iniFile.ReadSingle("IntersectionCreep", "CreepTaskAbandonDistance", CreepTaskAbandonDistance);
                CreepTaskTimeoutMs = iniFile.ReadUInt32("IntersectionCreep", "CreepTaskTimeoutMs", CreepTaskTimeoutMs);
            }
            catch (System.Exception e)
            {
                string error = e.ToString();
                Game.LogTrivial("Opticom: ERROR IN 'Settings.cs, Initialize()': " + error);
                Game.DisplayNotification("Opticom: Error Occured");
            }
        }
    }
}
