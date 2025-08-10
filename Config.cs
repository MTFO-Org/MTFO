using System;
using Rage;

namespace MTFO
{
    internal static class Config
    {
        internal static InitializationFile iniFile;

        internal static void Initialize()
        {
            try
            {
                iniFile = new InitializationFile(@"Plugins/LSPDFR/MTFO.ini");
                iniFile.Create();
                ShowDebugLines = iniFile.ReadBoolean("Configuration", "ShowDebugLines", ShowDebugLines);
                EnableOpticom = iniFile.ReadBoolean("Configuration", "EnableOpticom", EnableOpticom);
                EnableSameSideYield = iniFile.ReadBoolean("Configuration", "EnableSameSideYield", EnableSameSideYield);
                EnableOncomingBraking = iniFile.ReadBoolean("Configuration", "EnableOncomingBraking", EnableOncomingBraking);
                EnableIntersectionCreep = iniFile.ReadBoolean("Configuration", "EnableIntersectionCreep", EnableIntersectionCreep);
                EnableIntersectionControl = iniFile.ReadBoolean("Configuration", "EnableIntersectionControl", EnableIntersectionControl);

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
            catch (Exception e)
            {
                var error = e.ToString();
                Game.LogTrivial("MTFO: ERROR IN 'Config.cs, Initialize()': " + error);
                Game.DisplayNotification("MTFO: Error Occured");
            }
        }

        #region Configuration

        public static bool ShowDebugLines = true;

        public static bool EnableOpticom = true;

        public static bool EnableSameSideYield = true;

        public static bool EnableOncomingBraking = true;

        public static bool EnableIntersectionCreep = true;

        public static bool EnableIntersectionControl = true;

        #endregion

        #region Tuning Constants

        public static int OpticomGreenDurationMs = 6000;

        public static uint StoppedPlayerTimeoutMs = 2000;

        public static uint IntersectionDetectionCooldownMs = 1300;

        public static float DetectionRange = 55f;

        public static float DetectionStartWidth = 3.5f;

        public static float DetectionEndWidth = 5.7f;

        public static float DetectionHeightOffset = -1f;

        public static float DetectionAreaHeight = 12.0f;

        #endregion

        #region Yield params

        public static float ForwardMoveDistance = 35f;

        public static float SideMoveDistance = 6f;

        public static float ForceSideMoveDistance = 6.0f;

        public static float DriveSpeed = 12f;

        #endregion

        #region Same-side yield params

        public static float SameSideYieldCompletionDistance = 3.0f;

        public static float SameSideYieldAbandonDistance = 45.0f;

        public static uint SameSideYieldTimeoutMs = 3000;

        #endregion

        #region Oncoming traffic brake logic

        public static float OncomingBrakeHeadingDot = -0.7f;

        public static float OncomingBrakeMinLateral = -19.0f;

        public static float OncomingBrakeMaxLateral = -1.5f;

        public static int OncomingBrakeDurationMs = 1500;

        #endregion

        #region Intersection detection params

        public static float IntersectionSearchMaxDistance = 45f;

        public static float IntersectionSearchMinDistance = 30f;

        public static float IntersectionSearchStepSize = 7.0f;

        public static float IntersectionSearchRadius = 40.0f;

        public static float IntersectionHeadingThreshold = 40.0f;

        public static float CrossTrafficHeadingDotThreshold = 0.25f;

        #endregion

        #region Intersection creep

        public static float MinYieldSpeedMph = 4.0f;

        public static float IntersectionCreepForwardDistance = 8.5f;

        public static float IntersectionCreepSideDistance = 6.5f;

        public static float IntersectionCreepDriveSpeed = 12f;

        public static float CreepTaskCompletionDistance = 1.5f;

        public static float CreepTaskAbandonDistance = 13.0f;

        public static uint CreepTaskTimeoutMs = 2500;

        #endregion
    }
}