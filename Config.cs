namespace MTFO
{
    internal static class Config
    {
        // --- Configuration ---
        /// <summary>
        ///     If true, displays debug lines and shapes in the game world to visualize detection zones and AI targets.
        /// </summary>
        public const bool ShowDebugLines = true;

        /// <summary>
        ///     If true, the plugin will attempt to turn traffic lights green for the player's vehicle.
        /// </summary>
        public const bool EnableOpticom = true;

        // --- Tuning Constants ---
        /// <summary>
        ///     How long a traffic light will remain green (in milliseconds) after being triggered by the player.
        /// </summary>
        public const int OpticomGreenDurationMs = 6000;

        /// <summary>
        ///     If the player's vehicle is stopped for longer than this duration (in milliseconds), all AI tasks are cleared to
        ///     prevent unwanted behavior.
        /// </summary>
        public const uint StoppedPlayerTimeoutMs = 2000;

        /// <summary>
        ///     The cooldown period (in milliseconds) after clearing an intersection before the plugin will scan for a new one.
        /// </summary>
        public const uint IntersectionDetectionCooldownMs = 1300;

        // --- Forward detection zone for yield ---
        /// <summary>
        ///     The maximum distance (in meters) in front of the player's vehicle to detect other vehicles that should yield.
        /// </summary>
        public const float DetectionRange = 55f;

        /// <summary>
        ///     The width (in meters) of the detection zone closest to the player's vehicle.
        /// </summary>
        public const float DetectionStartWidth = 3.5f;

        /// <summary>
        ///     The width (in meters) of the detection zone at its furthest point (at DetectionRange).
        /// </summary>
        public const float DetectionEndWidth = 5.7f;

        /// <summary>
        ///     A vertical offset (in meters) for the detection zone to better align with the road and vehicle heights.
        /// </summary>
        public const float DetectionHeightOffset = -1f;

        /// <summary>
        ///     The total vertical size (in meters) of the detection zone.
        /// </summary>
        public const float DetectionAreaHeight = 12.0f;

        // --- Yield params ---
        /// <summary>
        ///     The forward distance component used when calculating a target pull-over position for an AI vehicle.
        /// </summary>
        public const float ForwardMoveDistance = 35f;

        /// <summary>
        ///     The sideways distance (in meters) an AI vehicle will attempt to move when a clear path is detected.
        /// </summary>
        public const float SideMoveDistance = 6f;

        /// <summary>
        ///     The sideways distance (in meters) an AI vehicle will be forced to move if no clear path is found (e.g., to nudge
        ///     past an obstacle).
        /// </summary>
        public const float ForceSideMoveDistance = 6.0f;

        /// <summary>
        ///     The speed at which an AI vehicle drives towards its calculated yielding position.
        /// </summary>
        public const float DriveSpeed = 12f;

        // --- Same-side yield task params ---
        /// <summary>
        ///     If a yielding vehicle is closer to its target than this distance (in meters), the task is considered complete.
        /// </summary>
        public const float SameSideYieldCompletionDistance = 3.0f;

        /// <summary>
        ///     If a yielding vehicle gets further from its target than this distance (in meters), the task is abandoned.
        /// </summary>
        public const float SameSideYieldAbandonDistance = 45.0f;

        /// <summary>
        ///     The maximum time (in milliseconds) a vehicle will attempt a yield task before giving up.
        /// </summary>
        public const uint SameSideYieldTimeoutMs = 3000;

        // --- Oncoming traffic brake logic ---
        /// <summary>
        ///     The heading dot product threshold to identify a vehicle as "oncoming." A value of -1 is directly oncoming.
        /// </summary>
        public const float OncomingBrakeHeadingDot = -0.7f;

        /// <summary>
        ///     Defines the minimum lateral distance (left side) for an oncoming vehicle to be considered a threat and told to
        ///     brake.
        /// </summary>
        public const float OncomingBrakeMinLateral = -19.0f;

        /// <summary>
        ///     Defines the maximum lateral distance (right side) for an oncoming vehicle to be considered a threat and told to
        ///     brake.
        /// </summary>
        public const float OncomingBrakeMaxLateral = -1.5f;

        /// <summary>
        ///     How long (in milliseconds) an oncoming vehicle should be forced to brake.
        /// </summary>
        public const int OncomingBrakeDurationMs = 1500;

        // --- Intersection detection params ---
        /// <summary>
        ///     The furthest distance (in meters) ahead of the player to scan for intersections.
        /// </summary>
        public const float IntersectionSearchMaxDistance = 45f;

        /// <summary>
        ///     The closest distance (in meters) ahead of the player to scan for intersections.
        /// </summary>
        public const float IntersectionSearchMinDistance = 30f;

        /// <summary>
        ///     The size of the steps (in meters) taken when scanning for an intersection between the min and max distances.
        /// </summary>
        public const float IntersectionSearchStepSize = 7.0f;

        /// <summary>
        ///     The radius (in meters) around a search point used to find intersection objects like traffic lights or stop signs.
        /// </summary>
        public const float IntersectionSearchRadius = 40.0f;

        /// <summary>
        ///     The maximum allowed heading difference (in degrees) between the player and a traffic light to consider it relevant.
        /// </summary>
        public const float IntersectionHeadingThreshold = 40.0f;

        /// <summary>
        ///     The heading dot product threshold used to identify vehicles that are part of cross-traffic at an intersection.
        /// </summary>
        public const float CrossTrafficHeadingDotThreshold = 0.25f;

        // --- Intersection creep ---
        /// <summary>
        ///     If a vehicle in the player's path is moving slower than this speed (in MPH), it can be told to "creep" forward.
        /// </summary>
        public const float MinYieldSpeedMph = 4.0f;

        /// <summary>
        ///     The forward distance (in meters) a slow vehicle will be instructed to creep.
        /// </summary>
        public const float IntersectionCreepForwardDistance = 8.5f;

        /// <summary>
        ///     The sideways distance (in meters) a slow vehicle will be instructed to creep.
        public const float IntersectionCreepSideDistance = 6.5f;

        /// <summary>
        ///     The speed at which a vehicle performs the creep maneuver.
        /// </summary>
        public const float IntersectionCreepDriveSpeed = 12f;

        /// <summary>
        ///     If a creeping vehicle is closer to its target than this distance (in meters), the task is considered complete.
        /// </summary>
        public const float CreepTaskCompletionDistance = 1.5f;

        /// <summary>
        ///     If a creeping vehicle gets further from its target than this distance (in meters), the task is abandoned.
        /// </summary>
        public const float CreepTaskAbandonDistance = 13.0f;

        /// <summary>
        ///     The maximum time (in milliseconds) a vehicle will attempt a creep task before giving up.
        /// </summary>
        public const uint CreepTaskTimeoutMs = 2500;
    }
}