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

                if (!iniFile.DoesKeyExist("Configuration", "ShowDebugLines")) iniFile.Write("Configuration", "ShowDebugLines", true);
                if (!iniFile.DoesKeyExist("Configuration", "EnableOpticom")) iniFile.Write("Configuration", "EnableOpticom", true);
                if (!iniFile.DoesKeyExist("Configuration", "EnableSameSideYield")) iniFile.Write("Configuration", "EnableSameSideYield", true);
                if (!iniFile.DoesKeyExist("Configuration", "EnableOncomingBraking")) iniFile.Write("Configuration", "EnableOncomingBraking", true);
                if (!iniFile.DoesKeyExist("Configuration", "EnableIntersectionCreep")) iniFile.Write("Configuration", "EnableIntersectionCreep", true);
                if (!iniFile.DoesKeyExist("Configuration", "EnableIntersectionControl")) iniFile.Write("Configuration", "EnableIntersectionControl", true);
                if (!iniFile.DoesKeyExist("Configuration", "EnableAroundPlayerLogic")) iniFile.Write("Configuration", "EnableAroundPlayerLogic", false);
                if (!iniFile.DoesKeyExist("Configuration", "AroundPlayerLogicOnlyInVehicle")) iniFile.Write("Configuration", "AroundPlayerLogicOnlyInVehicle", true);

                if (!iniFile.DoesKeyExist("TuningConstants", "OpticomGreenDurationMs")) iniFile.Write("TuningConstants", "OpticomGreenDurationMs", 6000);
                if (!iniFile.DoesKeyExist("TuningConstants", "StoppedPlayerTimeoutMs")) iniFile.Write("TuningConstants", "StoppedPlayerTimeoutMs", 2000);
                if (!iniFile.DoesKeyExist("TuningConstants", "IntersectionDetectionCooldownMs")) iniFile.Write("TuningConstants", "IntersectionDetectionCooldownMs", 1300);
                if (!iniFile.DoesKeyExist("TuningConstants", "DetectionRange")) iniFile.Write("TuningConstants", "DetectionRange", 55f);
                if (!iniFile.DoesKeyExist("TuningConstants", "DetectionStartWidth")) iniFile.Write("TuningConstants", "DetectionStartWidth", 3.5f);
                if (!iniFile.DoesKeyExist("TuningConstants", "DetectionEndWidth")) iniFile.Write("TuningConstants", "DetectionEndWidth", 6.25f);
                if (!iniFile.DoesKeyExist("TuningConstants", "DetectionHeightOffset")) iniFile.Write("TuningConstants", "DetectionHeightOffset", -1f);
                if (!iniFile.DoesKeyExist("TuningConstants", "DetectionAreaHeight")) iniFile.Write("TuningConstants", "DetectionAreaHeight", 12.0f);

                if (!iniFile.DoesKeyExist("YieldParams", "ForwardMoveDistance")) iniFile.Write("YieldParams", "ForwardMoveDistance", 35f);
                if (!iniFile.DoesKeyExist("YieldParams", "SideMoveDistance")) iniFile.Write("YieldParams", "SideMoveDistance", 6f);
                if (!iniFile.DoesKeyExist("YieldParams", "ForceSideMoveDistance")) iniFile.Write("YieldParams", "ForceSideMoveDistance", 6.0f);
                if (!iniFile.DoesKeyExist("YieldParams", "DriveSpeed")) iniFile.Write("YieldParams", "DriveSpeed", 12f);

                if (!iniFile.DoesKeyExist("Same-sideYieldParams", "SameSideYieldCompletionDistance")) iniFile.Write("Same-sideYieldParams", "SameSideYieldCompletionDistance", 3.0f);
                if (!iniFile.DoesKeyExist("Same-sideYieldParams", "SameSideYieldAbandonDistance")) iniFile.Write("Same-sideYieldParams", "SameSideYieldAbandonDistance", 45.0f);
                if (!iniFile.DoesKeyExist("Same-sideYieldParams", "SameSideYieldTimeoutMs")) iniFile.Write("Same-sideYieldParams", "SameSideYieldTimeoutMs", 3000);

                if (!iniFile.DoesKeyExist("OncomingTraffic", "OncomingBrakeHeadingDot")) iniFile.Write("OncomingTraffic", "OncomingBrakeHeadingDot", -0.7f);
                if (!iniFile.DoesKeyExist("OncomingTraffic", "OncomingBrakeMinLateral")) iniFile.Write("OncomingTraffic", "OncomingBrakeMinLateral", -19.0f);
                if (!iniFile.DoesKeyExist("OncomingTraffic", "OncomingBrakeMaxLateral")) iniFile.Write("OncomingTraffic", "OncomingBrakeMaxLateral", -1.5f);
                if (!iniFile.DoesKeyExist("OncomingTraffic", "OncomingBrakeDurationMs")) iniFile.Write("OncomingTraffic", "OncomingBrakeDurationMs", 1500);

                if (!iniFile.DoesKeyExist("IntersectionDetection", "IntersectionSearchMinDistance")) iniFile.Write("IntersectionDetection", "IntersectionSearchMinDistance", 30f);
                if (!iniFile.DoesKeyExist("IntersectionDetection", "IntersectionSearchMaxDistance")) iniFile.Write("IntersectionDetection", "IntersectionSearchMaxDistance", 45f);
                if (!iniFile.DoesKeyExist("IntersectionDetection", "IntersectionSearchStepSize")) iniFile.Write("IntersectionDetection", "IntersectionSearchStepSize", 7.0f);
                if (!iniFile.DoesKeyExist("IntersectionDetection", "IntersectionSearchRadius")) iniFile.Write("IntersectionDetection", "IntersectionSearchRadius", 40.0f);
                if (!iniFile.DoesKeyExist("IntersectionDetection", "IntersectionHeadingThreshold")) iniFile.Write("IntersectionDetection", "IntersectionHeadingThreshold", 40.0f);
                if (!iniFile.DoesKeyExist("IntersectionDetection", "CrossTrafficHeadingDotThreshold")) iniFile.Write("IntersectionDetection", "CrossTrafficHeadingDotThreshold", 0.25f);

                if (!iniFile.DoesKeyExist("IntersectionCreep", "MinYieldSpeedMph")) iniFile.Write("IntersectionCreep", "MinYieldSpeedMph", 4.0f);
                if (!iniFile.DoesKeyExist("IntersectionCreep", "IntersectionCreepForwardDistance")) iniFile.Write("IntersectionCreep", "IntersectionCreepForwardDistance", 8.5f);
                if (!iniFile.DoesKeyExist("IntersectionCreep", "IntersectionCreepSideDistance")) iniFile.Write("IntersectionCreep", "IntersectionCreepSideDistance", 6.5f);
                if (!iniFile.DoesKeyExist("IntersectionCreep", "IntersectionCreepDriveSpeed")) iniFile.Write("IntersectionCreep", "IntersectionCreepDriveSpeed", 12f);
                if (!iniFile.DoesKeyExist("IntersectionCreep", "CreepTaskCompletionDistance")) iniFile.Write("IntersectionCreep", "CreepTaskCompletionDistance", 1.5f);
                if (!iniFile.DoesKeyExist("IntersectionCreep", "CreepTaskAbandonDistance")) iniFile.Write("IntersectionCreep", "CreepTaskAbandonDistance", 13.0f);
                if (!iniFile.DoesKeyExist("IntersectionCreep", "CreepTaskTimeoutMs")) iniFile.Write("IntersectionCreep", "CreepTaskTimeoutMs", 2500);

                if (!iniFile.DoesKeyExist("AroundPlayer", "AroundPlayerDetectionRange")) iniFile.Write("AroundPlayer", "AroundPlayerDetectionRange", 30f);
                if (!iniFile.DoesKeyExist("AroundPlayer", "AroundPlayerDetectionWidth")) iniFile.Write("AroundPlayer", "AroundPlayerDetectionWidth", 8f);
                if (!iniFile.DoesKeyExist("AroundPlayer", "AroundPlayerOvertakeDistance")) iniFile.Write("AroundPlayer", "AroundPlayerOvertakeDistance", 20f);
                if (!iniFile.DoesKeyExist("AroundPlayer", "AroundPlayerTaskTimeoutMs")) iniFile.Write("AroundPlayer", "AroundPlayerTaskTimeoutMs", 4000);
                if (!iniFile.DoesKeyExist("AroundPlayer", "AroundPlayerTaskCompletionDistance")) iniFile.Write("AroundPlayer", "AroundPlayerTaskCompletionDistance", 3.0f);

                ShowDebugLines = iniFile.ReadBoolean("Configuration", "ShowDebugLines", ShowDebugLines);
                EnableOpticom = iniFile.ReadBoolean("Configuration", "EnableOpticom", EnableOpticom);
                EnableSameSideYield = iniFile.ReadBoolean("Configuration", "EnableSameSideYield", EnableSameSideYield);
                EnableOncomingBraking = iniFile.ReadBoolean("Configuration", "EnableOncomingBraking", EnableOncomingBraking);
                EnableIntersectionCreep = iniFile.ReadBoolean("Configuration", "EnableIntersectionCreep", EnableIntersectionCreep);
                EnableIntersectionControl = iniFile.ReadBoolean("Configuration", "EnableIntersectionControl", EnableIntersectionControl);
                EnableAroundPlayerLogic = iniFile.ReadBoolean("Configuration", "EnableAroundPlayerLogic", EnableAroundPlayerLogic);
                AroundPlayerLogicOnlyInVehicle = iniFile.ReadBoolean("Configuration", "AroundPlayerLogicOnlyInVehicle", AroundPlayerLogicOnlyInVehicle);

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

                AroundPlayerDetectionRange = iniFile.ReadSingle("AroundPlayer", "AroundPlayerDetectionRange", AroundPlayerDetectionRange);
                AroundPlayerDetectionWidth = iniFile.ReadSingle("AroundPlayer", "AroundPlayerDetectionWidth", AroundPlayerDetectionWidth);
                AroundPlayerOvertakeDistance = iniFile.ReadSingle("AroundPlayer", "AroundPlayerOvertakeDistance", AroundPlayerOvertakeDistance);
                AroundPlayerTaskTimeoutMs = iniFile.ReadUInt32("AroundPlayer", "AroundPlayerTaskTimeoutMs", AroundPlayerTaskTimeoutMs);
                AroundPlayerTaskCompletionDistance = iniFile.ReadSingle("AroundPlayer", "AroundPlayerTaskCompletionDistance", AroundPlayerTaskCompletionDistance);
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

        #region Around Player Configuration

        public static bool EnableAroundPlayerLogic;

        public static bool AroundPlayerLogicOnlyInVehicle = true;

        public static float AroundPlayerDetectionRange = 30f;

        public static float AroundPlayerDetectionWidth = 8f;

        public static float AroundPlayerOvertakeDistance = 20f;

        public static uint AroundPlayerTaskTimeoutMs = 4000;

        public static float AroundPlayerTaskCompletionDistance = 3.0f;

        #endregion

        #region Tuning Constants

        public static int OpticomGreenDurationMs = 6000;

        public static uint StoppedPlayerTimeoutMs = 2000;

        public static uint IntersectionDetectionCooldownMs = 1300;

        public static float DetectionRange = 55f;

        public static float DetectionStartWidth = 3.5f;

        public static float DetectionEndWidth = 6.25f;

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