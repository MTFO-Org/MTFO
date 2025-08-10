using System;
using System.Collections.Generic;
using System.Drawing;
using MTFO.Misc;
using Rage;

namespace MTFO.Handlers
{
    internal static class YieldingHandler
    {
        // Handles all logic for making vehicles in front of the player yield (pull over).
        public static void Process(Vehicle emergencyVehicle)
        {
            // This part now runs regardless of player speed, so vehicles can finish their tasks.
            ManageExistingYieldingVehicles(emergencyVehicle);

            // --- FIND NEW VEHICLES TO TASK ---
            // Only look for new vehicles to task if the player is moving fast enough.
            if (emergencyVehicle.Speed <= Config.MinYieldSpeedMph) return;

            FindAndTaskNewVehicles(emergencyVehicle);
        }

        private static void ManageExistingYieldingVehicles(Vehicle emergencyVehicle)
        {
            var vehiclesToUntask = new List<Vehicle>();
            foreach (var entry in PluginState.TaskedVehicles)
            {
                var vehicle = entry.Key;
                var task = entry.Value;

                // Check if the vehicle should be un-tasked (it's too far, or has reached its destination).
                if (!vehicle.Exists() || vehicle.Position.DistanceTo(emergencyVehicle.Position) > Config.DetectionRange + 20f || vehicle.Position.DistanceTo(task.TargetPosition) < 8.0f)
                {
                    vehiclesToUntask.Add(vehicle);
                    continue;
                }

                // Persistently set the indicator status to override the game's native AI.
                switch (task.TaskType)
                {
                    case YieldTaskType.MoveRight:
                    case YieldTaskType.ForceMoveRight:
                        vehicle.IndicatorLightsStatus = VehicleIndicatorLightsStatus.RightOnly;
                        break;
                    case YieldTaskType.MoveLeft:
                    case YieldTaskType.ForceMoveLeft:
                        vehicle.IndicatorLightsStatus = VehicleIndicatorLightsStatus.LeftOnly;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            // Clean up vehicles that have completed their task or are out of range.
            foreach (var vehicle in vehiclesToUntask)
            {
                if (vehicle.Exists())
                    vehicle.IndicatorLightsStatus = VehicleIndicatorLightsStatus.Off;

                if (PluginState.TaskedVehicleBlips.TryGetValue(vehicle, out var blip))
                {
                    if (blip.Exists()) blip.Delete();
                    PluginState.TaskedVehicleBlips.Remove(vehicle);
                }

                PluginState.TaskedVehicles.Remove(vehicle);
            }
        }

        private static void FindAndTaskNewVehicles(Vehicle emergencyVehicle)
        {
            var nearbyEntities = World.GetEntities(emergencyVehicle.Position, Config.DetectionRange + 5f, GetEntitiesFlags.ConsiderAllVehicles | GetEntitiesFlags.ExcludePlayerVehicle);

            foreach (var entity in nearbyEntities)
            {
                if (!(entity is Vehicle vehicle)) continue;

                // This check prevents vehicles that already have ANY kind of task from getting a new one.
                if (PluginState.TaskedVehicles.ContainsKey(vehicle) || PluginState.IntersectionTaskedVehicles.Contains(vehicle) || PluginState.IntersectionCreepTaskedVehicles.ContainsKey(vehicle)) continue;

                // Filter out irrelevant vehicles.
                if (!vehicle.Exists() || !vehicle.IsAlive || vehicle.HasSiren || vehicle.Speed < Config.MinYieldSpeedMph)
                    continue;

                var driver = vehicle.Driver;
                if (!driver.Exists() || !driver.IsAlive) continue;

                // Check vertical distance to ignore vehicles on overpasses/underpasses
                var verticalDistance = Math.Abs(vehicle.Position.Z - (emergencyVehicle.Position.Z + Config.DetectionHeightOffset));
                if (verticalDistance > Config.DetectionAreaHeight / 2.0f) continue;

                // Check if the vehicle is in front of us and facing the same general direction.
                var headingDot = Vector3.Dot(emergencyVehicle.ForwardVector, vehicle.ForwardVector);
                if (headingDot < 0.5f) continue;

                var vectorToTarget = vehicle.Position - emergencyVehicle.Position;
                var forwardDistance = Vector3.Dot(vectorToTarget, emergencyVehicle.ForwardVector);
                if (forwardDistance < 0f || forwardDistance > Config.DetectionRange) continue;

                // Check if the vehicle is within the trapezoidal detection area.
                var t = forwardDistance / Config.DetectionRange;
                var maxAllowedWidth = Config.DetectionStartWidth + (Config.DetectionEndWidth - Config.DetectionStartWidth) * t;
                var lateralOffset = Vector3.Dot(vectorToTarget, emergencyVehicle.RightVector);
                if (Math.Abs(lateralOffset) > maxAllowedWidth) continue;

                // Use trace lines to check for obstacles to the left and right of the vehicle.
                var checkStart = vehicle.Position + vehicle.ForwardVector * (vehicle.Length / 2f) + new Vector3(0, 0, 0.5f);
                const float sideCheckDistance = 3.5f;
                var rightHit = World.TraceLine(checkStart, checkStart + vehicle.RightVector * sideCheckDistance, TraceFlags.IntersectVehicles | TraceFlags.IntersectObjects, vehicle);
                var leftHit = World.TraceLine(checkStart, checkStart - vehicle.RightVector * sideCheckDistance, TraceFlags.IntersectVehicles | TraceFlags.IntersectObjects, vehicle);

                var canGoRight = !rightHit.Hit;
                var canGoLeft = !leftHit.Hit;
                Vector3 rawTargetPos;
                YieldTaskType taskType;
                // Prefer moving right unless the vehicle is significantly to our left.
                var preferRight = lateralOffset > -1.5f;

                if (preferRight)
                {
                    if (canGoRight)
                    {
                        taskType = YieldTaskType.MoveRight;
                        rawTargetPos = vehicle.Position + vehicle.RightVector * Config.SideMoveDistance + vehicle.ForwardVector * Config.ForwardMoveDistance;
                    }
                    else if (canGoLeft)
                    {
                        taskType = YieldTaskType.MoveLeft;
                        rawTargetPos = vehicle.Position - vehicle.RightVector * Config.SideMoveDistance + vehicle.ForwardVector * Config.ForwardMoveDistance;
                    }
                    else
                    {
                        taskType = YieldTaskType.ForceMoveRight;
                        rawTargetPos = vehicle.Position + vehicle.RightVector * Config.ForceSideMoveDistance + vehicle.ForwardVector * Config.ForwardMoveDistance;
                    }
                }
                else
                {
                    if (canGoLeft)
                    {
                        taskType = YieldTaskType.MoveLeft;
                        rawTargetPos = vehicle.Position - vehicle.RightVector * Config.SideMoveDistance + vehicle.ForwardVector * Config.ForwardMoveDistance;
                    }
                    else if (canGoRight)
                    {
                        taskType = YieldTaskType.MoveRight;
                        rawTargetPos = vehicle.Position + vehicle.RightVector * Config.SideMoveDistance + vehicle.ForwardVector * Config.ForwardMoveDistance;
                    }
                    else
                    {
                        taskType = YieldTaskType.ForceMoveLeft;
                        rawTargetPos = vehicle.Position - vehicle.RightVector * Config.ForceSideMoveDistance + vehicle.ForwardVector * Config.ForwardMoveDistance;
                    }
                }

                // Ensure the target position is on the ground and the path is clear.
                var groundZ = World.GetGroundZ(rawTargetPos, false, false);
                var finalTargetPos = groundZ.HasValue ? new Vector3(rawTargetPos.X, rawTargetPos.Y, groundZ.Value) : rawTargetPos;

                var pathTrace = World.TraceLine(vehicle.Position, finalTargetPos, TraceFlags.IntersectWorld, vehicle);
                if (pathTrace.Hit) continue;

                // Assign the yield task.
                driver.Tasks.Clear();
                driver.Tasks.DriveToPosition(finalTargetPos, Config.DriveSpeed, VehicleDrivingFlags.Normal);
                PluginState.TaskedVehicles.Add(vehicle, new YieldTask { TargetPosition = finalTargetPos, TaskType = taskType });

                if (PluginState.TaskedVehicleBlips.ContainsKey(vehicle)) continue;
                var blip = vehicle.AttachBlip();
                blip.Color = Color.Green;
                PluginState.TaskedVehicleBlips.Add(vehicle, blip);
            }
        }
    }
}