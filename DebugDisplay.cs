using System.Drawing;
using System.Linq;
using LSPD_First_Response.Mod.API;
using MTFO.Misc;
using Rage;
using Debug = Rage.Debug;

namespace MTFO
{
    internal static class DebugDisplay
    {
        public static void OnFrameRender(object sender, GraphicsEventArgs e2)
        {
            var playerVehicle = Game.LocalPlayer.Character.CurrentVehicle;
            if (!MtfoSettings.ShowDebugLines) return;
            if (!playerVehicle.Exists()) return;
            if (Functions.IsPlayerPerformingPullover()) return;

            foreach (var entry in PluginState.TaskedVehicles.Where(entry => entry.Key.Exists()))
            {
                Color lineColor;
                switch (entry.Value.TaskType)
                {
                    case YieldTaskType.MoveRight: lineColor = Color.Green; break;
                    case YieldTaskType.MoveLeft: lineColor = Color.Yellow; break;
                    case YieldTaskType.ForceMoveRight: lineColor = Color.Red; break;
                    case YieldTaskType.ForceMoveLeft: lineColor = Color.Orange; break;
                    default: lineColor = Color.White; break;
                }

                Debug.DrawLine(entry.Key.Position, entry.Value.TargetPosition, lineColor);
            }

            foreach (var entry in PluginState.AroundPlayerTaskedVehicles.Where(e => e.Key.Exists())) Debug.DrawLine(entry.Key.Position, entry.Value.TargetPosition, Color.Cyan);

            foreach (var vehicle in PluginState.OncomingBrakingVehicles.Keys.Where(v => v.Exists()))
            {
                var start = vehicle.Position;
                var end = start - vehicle.ForwardVector * 3f;
                Debug.DrawLine(start, end, Color.DarkRed);
            }

            foreach (var entry in PluginState.IntersectionCreepTaskedVehicles.Where(e => e.Key.Exists())) Debug.DrawLine(entry.Key.Position, entry.Value.TargetPosition, Color.Fuchsia);

            foreach (var entry in PluginState.FailedCreepCandidates.Where(e => e.Key.Exists())) Debug.DrawLine(entry.Key.Position, entry.Value, Color.Gray);

            foreach (var entry in PluginState.FailedAroundPlayerCandidates.Where(e => e.Key.Exists()))
            {
                var vehicle = entry.Key;
                var (targetPos, reason) = entry.Value;
                Color lineColor;
                switch (reason)
                {
                    case OvertakeFailureReason.SideTraceHit:
                        lineColor = Color.Yellow;
                        break;
                    case OvertakeFailureReason.NoRoadFound:
                        lineColor = Color.Orange;
                        break;
                    case OvertakeFailureReason.BadHeading:
                        lineColor = Color.Pink;
                        break;
                    case OvertakeFailureReason.TargetTooFarOrHigh:
                        lineColor = Color.Purple;
                        break;
                    case OvertakeFailureReason.PathTraceHit:
                        lineColor = Color.Red;
                        break;
                    default:
                        lineColor = Color.Gray;
                        break;
                }

                Debug.DrawLine(vehicle.Position, targetPos, lineColor);
            }

            if (PluginState.ActiveIntersectionCenter.HasValue)
            {
                var center = PluginState.ActiveIntersectionCenter.Value;

                foreach (var vehicle in PluginState.IntersectionTaskedVehicles.Where(v => v.Exists())) Debug.DrawLine(vehicle.Position, center, Color.Blue);
            }

            if (!PluginState.IsSilentModeActive) return;

            var pos = playerVehicle.Position;
            var forward = playerVehicle.ForwardVector;
            var right = playerVehicle.RightVector;
            var vizColor = Color.Aqua;

            var centerOffset = new Vector3(0, 0, MtfoSettings.DetectionHeightOffset);
            var halfHeight = new Vector3(0, 0, MtfoSettings.DetectionAreaHeight / 2.0f);

            var botBackLeft = pos - right * MtfoSettings.DetectionStartWidth + centerOffset - halfHeight;
            var botBackRight = pos + right * MtfoSettings.DetectionStartWidth + centerOffset - halfHeight;
            var botFrontLeft = pos + forward * MtfoSettings.DetectionRange - right * MtfoSettings.DetectionEndWidth + centerOffset - halfHeight;
            var botFrontRight = pos + forward * MtfoSettings.DetectionRange + right * MtfoSettings.DetectionEndWidth + centerOffset - halfHeight;

            var topBackLeft = pos - right * MtfoSettings.DetectionStartWidth + centerOffset + halfHeight;
            var topBackRight = pos + right * MtfoSettings.DetectionStartWidth + centerOffset + halfHeight;
            var topFrontLeft = pos + forward * MtfoSettings.DetectionRange - right * MtfoSettings.DetectionEndWidth + centerOffset + halfHeight;
            var topFrontRight = pos + forward * MtfoSettings.DetectionRange + right * MtfoSettings.DetectionEndWidth + centerOffset + halfHeight;

            Debug.DrawLine(botBackLeft, botBackRight, vizColor);
            Debug.DrawLine(botBackRight, botFrontRight, vizColor);
            Debug.DrawLine(botFrontRight, botFrontLeft, vizColor);
            Debug.DrawLine(botFrontLeft, botBackLeft, vizColor);

            Debug.DrawLine(topBackLeft, topBackRight, vizColor);
            Debug.DrawLine(topBackRight, topFrontRight, vizColor);
            Debug.DrawLine(topFrontRight, topFrontLeft, vizColor);
            Debug.DrawLine(topFrontLeft, topBackLeft, vizColor);

            Debug.DrawLine(botBackLeft, topBackLeft, vizColor);
            Debug.DrawLine(botBackRight, topBackRight, vizColor);
            Debug.DrawLine(botFrontLeft, topFrontLeft, vizColor);
            Debug.DrawLine(botFrontRight, topFrontRight, vizColor);
        }
    }
}