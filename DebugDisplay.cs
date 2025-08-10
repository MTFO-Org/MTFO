using System.Drawing;
using System.Linq;
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
            if (!Config.ShowDebugLines) return;
            if (!playerVehicle.Exists()) return;

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

            foreach (var vehicle in PluginState.OncomingBrakingVehicles.Keys.Where(v => v.Exists()))
            {
                var start = vehicle.Position;
                var end = start - vehicle.ForwardVector * 3f;
                Debug.DrawLine(start, end, Color.DarkRed);
            }

            foreach (var entry in PluginState.IntersectionCreepTaskedVehicles.Where(e => e.Key.Exists())) Debug.DrawLine(entry.Key.Position, entry.Value.TargetPosition, Color.Fuchsia);

            foreach (var entry in PluginState.FailedCreepCandidates.Where(e => e.Key.Exists())) Debug.DrawLine(entry.Key.Position, entry.Value, Color.Gray);

            if (PluginState.ActiveIntersectionCenter.HasValue)
            {
                var center = PluginState.ActiveIntersectionCenter.Value;

                foreach (var vehicle in PluginState.IntersectionTaskedVehicles.Where(v => v.Exists()))
                {
                    Debug.DrawLine(vehicle.Position, center, Color.Blue);
                }
            }

            if (!PluginState.IsSilentModeActive) return;

            var pos = playerVehicle.Position;
            var forward = playerVehicle.ForwardVector;
            var right = playerVehicle.RightVector;
            var vizColor = Color.Aqua;

            var centerOffset = new Vector3(0, 0, Config.DetectionHeightOffset);
            var halfHeight = new Vector3(0, 0, Config.DetectionAreaHeight / 2.0f);

            var botBackLeft = pos - right * Config.DetectionStartWidth + centerOffset - halfHeight;
            var botBackRight = pos + right * Config.DetectionStartWidth + centerOffset - halfHeight;
            var botFrontLeft = pos + forward * Config.DetectionRange - right * Config.DetectionEndWidth + centerOffset - halfHeight;
            var botFrontRight = pos + forward * Config.DetectionRange + right * Config.DetectionEndWidth + centerOffset - halfHeight;

            var topBackLeft = pos - right * Config.DetectionStartWidth + centerOffset + halfHeight;
            var topBackRight = pos + right * Config.DetectionStartWidth + centerOffset + halfHeight;
            var topFrontLeft = pos + forward * Config.DetectionRange - right * Config.DetectionEndWidth + centerOffset + halfHeight;
            var topFrontRight = pos + forward * Config.DetectionRange + right * Config.DetectionEndWidth + centerOffset + halfHeight;

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
