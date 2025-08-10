using System.Drawing;
using System.Linq;
using MTFO.Misc;
using Rage;
using Debug = Rage.Debug;

namespace MTFO
{
    internal static class DebugDisplay
    {
        // Handles drawing all debug lines and shapes. This runs every frame.
        public static void OnFrameRender(object sender, GraphicsEventArgs e2)
        {
            // Only run if debug lines are enabled in the configuration
            if (!Config.ShowDebugLines) return;
            var playerVehicle = Game.LocalPlayer.Character.CurrentVehicle;

            // Don't draw if the player isn't in a vehicle
            if (!playerVehicle.Exists()) return;

            // Draw lines for yielding vehicles, color-coded by task type.
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

            // Draw lines for intersection-managed vehicles.
            if (PluginState.ActiveIntersectionCenter.HasValue)
            {
                var center = PluginState.ActiveIntersectionCenter.Value;

                // Draw a blue line from stopped cross-traffic to the intersection center.
                foreach (var vehicle in PluginState.IntersectionTaskedVehicles.Where(v => v.Exists()))
                    Debug.DrawLine(vehicle.Position, center, Color.Blue);

                // Draw a fuchsia line from a "creeping" vehicle to its target destination.
                // This now points to their actual target position from the CreepTask struct.
                foreach (var entry in PluginState.IntersectionCreepTaskedVehicles.Where(e => e.Key.Exists()))
                    Debug.DrawLine(entry.Key.Position, entry.Value.TargetPosition, Color.Fuchsia);
            }

            // Only draw the detection area if the main logic is active
            if (!PluginState.IsSilentModeActive) return;

            // Draw lines for detection for yielding vehicles.
            var pos = playerVehicle.Position;
            var forward = playerVehicle.ForwardVector;
            var right = playerVehicle.RightVector;
            var vizColor = Color.Aqua;

            // Calculate the top and bottom offsets for the 3D box
            var centerOffset = new Vector3(0, 0, Config.DetectionHeightOffset);
            var halfHeight = new Vector3(0, 0, Config.DetectionAreaHeight / 2.0f);

            // Calculate the 8 corners of the 3D trapezoidal prism
            var botBackLeft = pos - right * Config.DetectionStartWidth + centerOffset - halfHeight;
            var botBackRight = pos + right * Config.DetectionStartWidth + centerOffset - halfHeight;
            var botFrontLeft = pos + forward * Config.DetectionRange - right * Config.DetectionEndWidth + centerOffset - halfHeight;
            var botFrontRight = pos + forward * Config.DetectionRange + right * Config.DetectionEndWidth + centerOffset - halfHeight;

            var topBackLeft = pos - right * Config.DetectionStartWidth + centerOffset + halfHeight;
            var topBackRight = pos + right * Config.DetectionStartWidth + centerOffset + halfHeight;
            var topFrontLeft = pos + forward * Config.DetectionRange - right * Config.DetectionEndWidth + centerOffset + halfHeight;
            var topFrontRight = pos + forward * Config.DetectionRange + right * Config.DetectionEndWidth + centerOffset + halfHeight;

            // Draw bottom rectangle
            Debug.DrawLine(botBackLeft, botBackRight, vizColor);
            Debug.DrawLine(botBackRight, botFrontRight, vizColor);
            Debug.DrawLine(botFrontRight, botFrontLeft, vizColor);
            Debug.DrawLine(botFrontLeft, botBackLeft, vizColor);

            // Draw top rectangle
            Debug.DrawLine(topBackLeft, topBackRight, vizColor);
            Debug.DrawLine(topBackRight, topFrontRight, vizColor);
            Debug.DrawLine(topFrontRight, topFrontLeft, vizColor);
            Debug.DrawLine(topFrontLeft, topBackLeft, vizColor);

            // Draw vertical connectors
            Debug.DrawLine(botBackLeft, topBackLeft, vizColor);
            Debug.DrawLine(botBackRight, topBackRight, vizColor);
            Debug.DrawLine(botFrontLeft, topFrontLeft, vizColor);
            Debug.DrawLine(botFrontRight, topFrontRight, vizColor);
        }
    }
}