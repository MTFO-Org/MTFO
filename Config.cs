namespace MTFO
{
    internal static class Config
    {
        // --- Configuration ---
        public const bool ShowDebugLines = true;
        public const bool EnableOpticom = true;

        // --- Tuning Constants ---
        public const int OpticomGreenDurationMs = 6000; // How long light is forced
        public const uint StoppedPlayerTimeoutMs = 2000; // How long the player must be stopped before logic is paused.
        public const uint IntersectionDetectionCooldownMs = 1300; // How long to wait to find a new intersection after clearing one. //BUG: required or blips flash

        // Forward detection zone for yield
        public const float DetectionRange = 55f;
        public const float DetectionStartWidth = 7f;
        public const float DetectionEndWidth = 7.5f;
        public const float DetectionHeightOffset = -1f; // How high above the player's position to center the detection box.
        public const float DetectionAreaHeight = 12.0f; // The total vertical size (thickness) of the detection box.

        // Yield params
        public const float ForwardMoveDistance = 35f;
        public const float SideMoveDistance = 6f;
        public const float ForceSideMoveDistance = 6.0f;
        public const float DriveSpeed = 15f;

        // Oncoming traffic brake logic
        public const float OncomingBrakeHeadingDot = -0.7f; // How directly a vehicle must be facing you to brake.
        public const float OncomingBrakeMinLateral = -19.0f; // The farthest distance to the left a vehicle can be.
        public const float OncomingBrakeMaxLateral = -1.5f; // The closest distance to the left a vehicle can be.
        public const int OncomingBrakeDurationMs = 1500; // How long the vehicle should apply the brake.

        // Intersection detection params
        public const float IntersectionSearchMaxDistance = 45f;
        public const float IntersectionSearchMinDistance = 30f;
        public const float IntersectionSearchStepSize = 7.0f;
        public const float IntersectionSearchRadius = 40.0f;
        public const float IntersectionHeadingThreshold = 40.0f; // Heading check for finding signs/traf lights.
        public const float CrossTrafficHeadingDotThreshold = 0.25f; // Threshold to identify cross-traffic.

        // Intersection creep
        public const float MinYieldSpeedMph = 4.0f; // Player must be going faster than this for yielding to activate, values underneath are using for creep

        public const float IntersectionCreepForwardDistance = 7.7f; // How far forward cars at intersection creep.
        public const float IntersectionCreepSideDistance = 5.2f; // How far to the side cars at intersection creep.
        public const float IntersectionCreepDriveSpeed = 10f; // How fast the cars creep.

        public const float CreepTaskCompletionDistance = 2.5f; // How close a vehicle must get to its creep target to complete the task.
        public const float CreepTaskAbandonDistance = 15.0f; // If a creeping vehicle gets this far from its target, assume it's driving off.
        public const uint CreepTaskTimeoutMs = 2000; // How long a vehicle can be in a creep task before it's cancelled.
    }
}