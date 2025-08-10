using System;
using System.Collections.Generic;
using System.Drawing;
using LSPD_First_Response.Mod.API;
using MTFO.Misc;
using Rage;

namespace MTFO.Handlers
{
    internal static class YieldingHandler
    {
        public static void Process(Vehicle emergencyVehicle)
        {
            ManageExistingYieldingVehicles(emergencyVehicle);

            if (emergencyVehicle.Speed <= Config.MinYieldSpeedMph) return;

            if (Game.GameTime < PluginState.NextYieldScanTime) return;

            FindAndTaskNewVehicles(emergencyVehicle);
            PluginState.NextYieldScanTime = Game.GameTime + 200;
        }

        private static void ManageExistingYieldingVehicles(Vehicle emergencyVehicle)
        {
            List<Vehicle> creepersToUntask = null;
            foreach (var kvp in PluginState.IntersectionCreepTaskedVehicles)
            {
                var vehicle = kvp.Key;
                var task = kvp.Value;
                var shouldUntask = !vehicle.Exists() || vehicle.Position.DistanceTo(emergencyVehicle.Position) > Config.DetectionRange + 30f || vehicle.Position.DistanceTo(task.TargetPosition) < Config.CreepTaskCompletionDistance || vehicle.Position.DistanceTo(task.TargetPosition) > Config.CreepTaskAbandonDistance || Game.GameTime - task.GameTimeStarted > Config.CreepTaskTimeoutMs;

                if (!shouldUntask) continue;

                if (creepersToUntask == null) creepersToUntask = new List<Vehicle>();
                creepersToUntask.Add(vehicle);
            }

            if (creepersToUntask != null)
                foreach (var v in creepersToUntask)
                {
                    if (v.Exists() && v.Driver.Exists()) v.Driver.Tasks.Clear();
                    if (PluginState.TaskedVehicleBlips.TryGetValue(v, out var blip))
                    {
                        if (blip.Exists()) blip.Delete();
                        PluginState.TaskedVehicleBlips.Remove(v);
                    }

                    PluginState.IntersectionCreepTaskedVehicles.Remove(v);
                }

            var vehiclesToUntask = new List<Vehicle>();
            var brakingVehiclesToUntask = new List<Vehicle>();

            foreach (var entry in PluginState.OncomingBrakingVehicles)
            {
                var vehicle = entry.Key;
                var timeTasked = entry.Value;

                if (!vehicle.Exists() || vehicle.Position.DistanceTo(emergencyVehicle.Position) > Config.DetectionRange + 30f || Game.GameTime - timeTasked > Config.OncomingBrakeDurationMs) brakingVehiclesToUntask.Add(vehicle);
            }

            foreach (var vehicle in brakingVehiclesToUntask)
            {
                if (vehicle.Exists() && vehicle.Driver.Exists()) vehicle.Driver.Tasks.Clear();

                PluginState.OncomingBrakingVehicles.Remove(vehicle);

                if (!PluginState.TaskedVehicleBlips.TryGetValue(vehicle, out var blip)) continue;
                if (blip.Exists()) blip.Delete();
                PluginState.TaskedVehicleBlips.Remove(vehicle);
            }

            foreach (var entry in PluginState.TaskedVehicles)
            {
                var vehicle = entry.Key;
                var task = entry.Value;

                var shouldUntask = !vehicle.Exists() || vehicle.Position.DistanceTo(emergencyVehicle.Position) > Config.DetectionRange + 20f || vehicle.Position.DistanceTo(task.TargetPosition) < Config.SameSideYieldCompletionDistance || vehicle.Position.DistanceTo(task.TargetPosition) > Config.SameSideYieldAbandonDistance || Game.GameTime - task.GameTimeStarted > Config.SameSideYieldTimeoutMs;

                if (shouldUntask)
                {
                    vehiclesToUntask.Add(vehicle);
                    continue;
                }

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

            foreach (var vehicle in vehiclesToUntask)
            {
                if (vehicle.Exists())
                {
                    vehicle.IndicatorLightsStatus = VehicleIndicatorLightsStatus.Off;
                    if (vehicle.Driver.Exists()) vehicle.Driver.Tasks.Clear();
                }

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

                if (PluginState.TaskedVehicles.ContainsKey(vehicle) || PluginState.IntersectionTaskedVehicles.Contains(vehicle) || PluginState.IntersectionCreepTaskedVehicles.ContainsKey(vehicle) || PluginState.OncomingBrakingVehicles.ContainsKey(vehicle))
                    continue;

                if (!vehicle.Exists() || vehicle.IsPoliceVehicle || !vehicle.IsAlive || Functions.IsPlayerPerformingPullover() || vehicle.Model.IsEmergencyVehicle || Utils.IsDriverInPursuit(vehicle.Driver))
                    continue;

                var driver = vehicle.Driver;
                if (!driver.Exists() || !driver.IsAlive) continue;

                var verticalDistance = Math.Abs(vehicle.Position.Z - (emergencyVehicle.Position.Z + Config.DetectionHeightOffset));
                if (verticalDistance > Config.DetectionAreaHeight / 2.0f) continue;

                var headingDot = Vector3.Dot(emergencyVehicle.ForwardVector, vehicle.ForwardVector);
                var vectorToTarget = vehicle.Position - emergencyVehicle.Position;
                var forwardDistance = Vector3.Dot(vectorToTarget, emergencyVehicle.ForwardVector);

                if (forwardDistance < 0f || forwardDistance > Config.DetectionRange) continue;

                var lateralOffset = Vector3.Dot(vectorToTarget, emergencyVehicle.RightVector);

                if (Config.EnableOncomingBraking && headingDot < Config.OncomingBrakeHeadingDot)
                {
                    if (!(lateralOffset < Config.OncomingBrakeMaxLateral) || !(lateralOffset > Config.OncomingBrakeMinLateral)) continue;

                    driver.Tasks.PerformDrivingManeuver(vehicle, VehicleManeuver.Wait, Config.OncomingBrakeDurationMs);
                    PluginState.OncomingBrakingVehicles.Add(vehicle, Game.GameTime);

                    if (Config.ShowDebugLines && !PluginState.TaskedVehicleBlips.ContainsKey(vehicle))
                    {
                        var blip = vehicle.AttachBlip();
                        blip.Color = Color.DarkRed;
                        PluginState.TaskedVehicleBlips.Add(vehicle, blip);
                    }

                    continue;
                }

                var t = forwardDistance / Config.DetectionRange;
                var maxAllowedWidth = Config.DetectionStartWidth + (Config.DetectionEndWidth - Config.DetectionStartWidth) * t;
                if (Math.Abs(lateralOffset) > maxAllowedWidth) continue;

                if (Config.EnableIntersectionCreep && headingDot > 0.8f && vehicle.Speed < Config.MinYieldSpeedMph)
                {
                    var checkStartPos = vehicle.Position + new Vector3(0, 0, 0.5f);
                    var sideCheckDistance = Config.IntersectionCreepSideDistance + vehicle.Width / 2f;
                    var traceFlags = TraceFlags.IntersectVehicles | TraceFlags.IntersectObjects;

                    var rightHit = World.TraceLine(checkStartPos, checkStartPos + vehicle.RightVector * sideCheckDistance, traceFlags, vehicle);
                    var leftHit = World.TraceLine(checkStartPos, checkStartPos - vehicle.RightVector * sideCheckDistance, traceFlags, vehicle);

                    var canGoRight = !rightHit.Hit;
                    var canGoLeft = !leftHit.Hit;

                    var vectorFromVehicleToPlayer = emergencyVehicle.Position - vehicle.Position;
                    var playerLateralOffset = Vector3.Dot(vectorFromVehicleToPlayer, vehicle.RightVector);

                    Vector3? sidePushDirection = null;

                    if (playerLateralOffset < 0)
                    {
                        if (canGoRight) sidePushDirection = vehicle.RightVector;
                        else if (canGoLeft) sidePushDirection = -vehicle.RightVector;
                    }
                    else
                    {
                        if (canGoLeft) sidePushDirection = -vehicle.RightVector;
                        else if (canGoRight) sidePushDirection = vehicle.RightVector;
                    }

                    var tentativeTargetPos = vehicle.Position;
                    if (sidePushDirection.HasValue) tentativeTargetPos += vehicle.ForwardVector * Config.IntersectionCreepForwardDistance + sidePushDirection.Value * Config.IntersectionCreepSideDistance;

                    if (!sidePushDirection.HasValue)
                    {
                        if (Config.ShowDebugLines) PluginState.FailedCreepCandidates[vehicle] = tentativeTargetPos;
                        continue;
                    }

                    var groundZ = World.GetGroundZ(tentativeTargetPos, false, false);
                    if (!groundZ.HasValue)
                    {
                        if (Config.ShowDebugLines) PluginState.FailedCreepCandidates[vehicle] = tentativeTargetPos;
                        continue;
                    }

                    var finalTargetPos = new Vector3(tentativeTargetPos.X, tentativeTargetPos.Y, groundZ.Value);

                    if (Math.Abs(finalTargetPos.Z - vehicle.Position.Z) > 3.0f)
                    {
                        if (Config.ShowDebugLines) PluginState.FailedCreepCandidates[vehicle] = finalTargetPos;
                        continue;
                    }

                    var pathTrace = World.TraceLine(vehicle.Position, finalTargetPos, TraceFlags.IntersectWorld, vehicle);
                    if (pathTrace.Hit)
                    {
                        if (Config.ShowDebugLines) PluginState.FailedCreepCandidates[vehicle] = finalTargetPos;
                        continue;
                    }

                    driver.Tasks.Clear();
                    driver.Tasks.DriveToPosition(finalTargetPos, Config.IntersectionCreepDriveSpeed, VehicleDrivingFlags.Emergency | VehicleDrivingFlags.StopAtDestination);

                    var creepTask = new CreepTask { TargetPosition = finalTargetPos, GameTimeStarted = Game.GameTime };
                    PluginState.IntersectionCreepTaskedVehicles.Add(vehicle, creepTask);

                    if (Config.ShowDebugLines && !PluginState.TaskedVehicleBlips.ContainsKey(vehicle))
                    {
                        var blip = vehicle.AttachBlip();
                        blip.Color = Color.Fuchsia;
                        PluginState.TaskedVehicleBlips.Add(vehicle, blip);
                    }
                }
                else if (Config.EnableSameSideYield && headingDot > 0.2f)
                {
                    var checkStart = vehicle.Position + vehicle.ForwardVector * (vehicle.Length / 2f) + new Vector3(0, 0, 0.5f);
                    const float sideCheckDistance = 3.5f;
                    var rightHit = World.TraceLine(checkStart, checkStart + vehicle.RightVector * sideCheckDistance, TraceFlags.IntersectVehicles | TraceFlags.IntersectObjects, vehicle);
                    var leftHit = World.TraceLine(checkStart, checkStart - vehicle.RightVector * sideCheckDistance, TraceFlags.IntersectVehicles | TraceFlags.IntersectObjects, vehicle);

                    var canGoRight = !rightHit.Hit;
                    var canGoLeft = !leftHit.Hit;
                    Vector3 rawTargetPos;
                    YieldTaskType taskType;

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

                    var groundZ = World.GetGroundZ(rawTargetPos, false, false);
                    var finalTargetPos = groundZ.HasValue ? new Vector3(rawTargetPos.X, rawTargetPos.Y, groundZ.Value) : rawTargetPos;

                    var pathTrace = World.TraceLine(vehicle.Position, finalTargetPos, TraceFlags.IntersectWorld, vehicle);
                    if (pathTrace.Hit) continue;

                    driver.Tasks.Clear();
                    driver.Tasks.DriveToPosition(finalTargetPos, Config.DriveSpeed, VehicleDrivingFlags.Normal);
                    PluginState.TaskedVehicles.Add(vehicle, new YieldTask { TargetPosition = finalTargetPos, TaskType = taskType, GameTimeStarted = Game.GameTime });

                    if (Config.ShowDebugLines && !PluginState.TaskedVehicleBlips.ContainsKey(vehicle))
                    {
                        var blip = vehicle.AttachBlip();
                        blip.Color = Color.Green;
                        PluginState.TaskedVehicleBlips.Add(vehicle, blip);
                    }
                }
            }
        }
    }
}