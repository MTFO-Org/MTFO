using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
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

        private static bool TryFindValidYieldPosition(Vehicle vehicle, Vehicle emergencyVehicle, float lateralOffset, out Vector3 finalTargetPos, out YieldTaskType taskType)
        {
            var checkStart = vehicle.Position + vehicle.ForwardVector * (vehicle.Length / 2f) + new Vector3(0, 0, 0.5f);
            const float sideCheckDistance = 3.5f;
            var rightHit = World.TraceLine(checkStart, checkStart + vehicle.RightVector * sideCheckDistance, TraceFlags.IntersectVehicles | TraceFlags.IntersectObjects, vehicle);
            var leftHit = World.TraceLine(checkStart, checkStart - vehicle.RightVector * sideCheckDistance, TraceFlags.IntersectVehicles | TraceFlags.IntersectObjects, vehicle);

            var canGoRight = !rightHit.Hit;
            var canGoLeft = !leftHit.Hit;
            var preferRight = lateralOffset > -1.5f;

            Vector3 sideDirection;
            float sideDistance;

            if (preferRight)
            {
                if (canGoRight)
                {
                    taskType = YieldTaskType.MoveRight;
                    sideDirection = vehicle.RightVector;
                    sideDistance = Config.SideMoveDistance;
                }
                else if (canGoLeft)
                {
                    taskType = YieldTaskType.MoveLeft;
                    sideDirection = -vehicle.RightVector;
                    sideDistance = Config.SideMoveDistance;
                }
                else
                {
                    taskType = YieldTaskType.ForceMoveRight;
                    sideDirection = vehicle.RightVector;
                    sideDistance = Config.ForceSideMoveDistance;
                }
            }
            else
            {
                if (canGoLeft)
                {
                    taskType = YieldTaskType.MoveLeft;
                    sideDirection = -vehicle.RightVector;
                    sideDistance = Config.SideMoveDistance;
                }
                else if (canGoRight)
                {
                    taskType = YieldTaskType.MoveRight;
                    sideDirection = vehicle.RightVector;
                    sideDistance = Config.SideMoveDistance;
                }
                else
                {
                    taskType = YieldTaskType.ForceMoveLeft;
                    sideDirection = -vehicle.RightVector;
                    sideDistance = Config.ForceSideMoveDistance;
                }
            }

            for (var forwardDistance = Config.ForwardMoveDistance; forwardDistance >= 5f; forwardDistance -= 5f)
            {
                var rawTargetPos = vehicle.Position + sideDirection * sideDistance + vehicle.ForwardVector * forwardDistance;

                var groundZ = World.GetGroundZ(rawTargetPos, false, false);
                if (!groundZ.HasValue) continue;

                var tempTargetPos = new Vector3(rawTargetPos.X, rawTargetPos.Y, groundZ.Value);

                if (!IsPositionSafeFromPlayer(tempTargetPos, emergencyVehicle))
                {
                    if (Config.ShowDebugLines) PluginState.FailedCreepCandidates[vehicle] = tempTargetPos;
                    continue;
                }

                var vehicleLateralOffset = Vector3.Dot(vehicle.Position - emergencyVehicle.Position, emergencyVehicle.RightVector);
                var targetLateralOffset = Vector3.Dot(tempTargetPos - emergencyVehicle.Position, emergencyVehicle.RightVector);
                if (vehicleLateralOffset * targetLateralOffset < 0f && Math.Abs(vehicleLateralOffset) > 0.5f)
                {
                    if (Config.ShowDebugLines) PluginState.FailedCreepCandidates[vehicle] = tempTargetPos;
                    continue;
                }

                if (emergencyVehicle.Speed > 5f)
                {
                    var velocity = emergencyVehicle.Velocity;
                    var futurePlayerFwd = velocity.LengthSquared() > 1.0f ? velocity.ToNormalized() : emergencyVehicle.ForwardVector;
                    var futurePlayerPos = emergencyVehicle.Position + velocity * 1.0f;
                    var futurePlayerRight = Vector3.Cross(futurePlayerFwd, Vector3.WorldUp);

                    var vehicleLateralOffsetFuture = Vector3.Dot(vehicle.Position - futurePlayerPos, futurePlayerRight);
                    var targetLateralOffsetFuture = Vector3.Dot(tempTargetPos - futurePlayerPos, futurePlayerRight);
                    if (vehicleLateralOffsetFuture * targetLateralOffsetFuture < 0f && Math.Abs(vehicleLateralOffsetFuture) > 0.5f)
                    {
                        if (Config.ShowDebugLines) PluginState.FailedCreepCandidates[vehicle] = tempTargetPos;
                        continue;
                    }
                }

                var pathTrace = World.TraceLine(vehicle.Position, tempTargetPos, TraceFlags.IntersectWorld, vehicle);
                if (pathTrace.Hit) continue;

                finalTargetPos = tempTargetPos;
                return true;
            }

            finalTargetPos = Vector3.Zero;
            return false;
        }

        private static bool IsPositionSafeFromPlayer(Vector3 position, Vehicle emergencyVehicle)
        {
            var frontBumperPosition = emergencyVehicle.Position + emergencyVehicle.ForwardVector * (emergencyVehicle.Length / 2f);
            var vectorToTarget = position - frontBumperPosition;

            var forwardDistance = Vector3.Dot(vectorToTarget, emergencyVehicle.ForwardVector);
            var lateralDistance = Vector3.Dot(vectorToTarget, emergencyVehicle.RightVector);

            var unsafeLateralThreshold = emergencyVehicle.Width / 2f + 2.5f;
            if (forwardDistance > -2.0f && forwardDistance < 60f && Math.Abs(lateralDistance) < unsafeLateralThreshold) return false;

            if (emergencyVehicle.Speed > 4f)
            {
                var velocity = emergencyVehicle.Velocity;
                var futureTime = 1.2f;
                var futurePosition = emergencyVehicle.Position + velocity * futureTime;
                var futureForwardVec = velocity.LengthSquared() > 1f ? velocity.ToNormalized() : emergencyVehicle.ForwardVector;
                var futureRightVec = Vector3.Cross(futureForwardVec, Vector3.WorldUp);

                var futureFrontBumperPosition = futurePosition + futureForwardVec * (emergencyVehicle.Length / 2f);
                var vectorToTargetFuture = position - futureFrontBumperPosition;

                var forwardDistanceFuture = Vector3.Dot(vectorToTargetFuture, futureForwardVec);
                var lateralDistanceFuture = Vector3.Dot(vectorToTargetFuture, futureRightVec);

                var futureUnsafeLateralThreshold = emergencyVehicle.Width / 2f + 3.25f;
                if (forwardDistanceFuture > -5.0f && forwardDistanceFuture < 70f && Math.Abs(lateralDistanceFuture) < futureUnsafeLateralThreshold) return false;
            }

            return true;
        }

        private static void ManageExistingYieldingVehicles(Vehicle emergencyVehicle)
        {
            Ped pulloverSuspect = null;
            if (Functions.IsPlayerPerformingPullover()) pulloverSuspect = Functions.GetPulloverSuspect(Functions.GetCurrentPullover());

            List<Vehicle> creepersToUntask = null;
            foreach (var kvp in PluginState.IntersectionCreepTaskedVehicles)
            {
                var vehicle = kvp.Key;
                var task = kvp.Value;

                var isPulloverTarget = pulloverSuspect != null && pulloverSuspect.Exists() && vehicle.Exists() && pulloverSuspect == vehicle.Driver;

                var shouldUntask = isPulloverTarget || !vehicle.Exists() || vehicle.Position.DistanceTo(emergencyVehicle.Position) > Config.DetectionRange + 30f || vehicle.Position.DistanceTo(task.TargetPosition) < Config.CreepTaskCompletionDistance || vehicle.Position.DistanceTo(task.TargetPosition) > Config.CreepTaskAbandonDistance || Game.GameTime - task.GameTimeStarted > Config.CreepTaskTimeoutMs;

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
            var tasksToUpdate = new Dictionary<Vehicle, YieldTask>();

            foreach (var entry in PluginState.OncomingBrakingVehicles)
            {
                var vehicle = entry.Key;
                var timeTasked = entry.Value;

                var isPulloverTarget = pulloverSuspect != null && pulloverSuspect.Exists() && vehicle.Exists() && pulloverSuspect == vehicle.Driver;

                if (isPulloverTarget || !vehicle.Exists() || vehicle.Position.DistanceTo(emergencyVehicle.Position) > Config.DetectionRange + 30f || Game.GameTime - timeTasked > Config.OncomingBrakeDurationMs) brakingVehiclesToUntask.Add(vehicle);
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

                var isPulloverTarget = pulloverSuspect != null && pulloverSuspect.Exists() && vehicle.Exists() && pulloverSuspect == vehicle.Driver;
                if (isPulloverTarget)
                {
                    vehiclesToUntask.Add(vehicle);
                    continue;
                }

                if (!IsPositionSafeFromPlayer(task.TargetPosition, emergencyVehicle))
                {
                    vehiclesToUntask.Add(vehicle);
                    continue;
                }

                if (!task.IsWaiting && vehicle.Position.DistanceTo(task.TargetPosition) < Config.SameSideYieldCompletionDistance)
                {
                    vehicle.Driver.Tasks.PerformDrivingManeuver(vehicle, VehicleManeuver.Wait, (int)Config.SameSideYieldWaitDurationMs);
                    var updatedTask = task;
                    updatedTask.IsWaiting = true;
                    updatedTask.GameTimeStarted = Game.GameTime;
                    tasksToUpdate[vehicle] = updatedTask;
                    continue;
                }

                var hasTimedOut = Game.GameTime - task.GameTimeStarted > (task.IsWaiting ? Config.SameSideYieldWaitDurationMs : Config.SameSideYieldTimeoutMs);
                var vectorToTarget = task.TargetPosition - vehicle.Position;
                var hasPassedTarget = Vector3.Dot(vehicle.ForwardVector, vectorToTarget) < 0f && vehicle.Speed > 1f;
                var shouldUntask = !vehicle.Exists() || vehicle.Position.DistanceTo(emergencyVehicle.Position) > Config.DetectionRange + 20f || vehicle.Position.DistanceTo(task.TargetPosition) > Config.SameSideYieldAbandonDistance || hasTimedOut || hasPassedTarget;

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

            foreach (var entry in tasksToUpdate)
                if (PluginState.TaskedVehicles.ContainsKey(entry.Key))
                    PluginState.TaskedVehicles[entry.Key] = entry.Value;

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
            if (Config.ShowDebugLines) PluginState.FailedCreepCandidates.Clear();

            var nearbyEntities = World.GetEntities(emergencyVehicle.Position, Config.DetectionRange + 5f, GetEntitiesFlags.ConsiderAllVehicles | GetEntitiesFlags.ExcludePlayerVehicle);
            var creepCandidates = new List<Vehicle>();
            var assignedCreepTargetPositions = new List<Vector3>();

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
                    creepCandidates.Add(vehicle);
                }
                else if (Config.EnableSameSideYield && headingDot > 0.2f)
                {
                    if (!TryFindValidYieldPosition(vehicle, emergencyVehicle, lateralOffset, out var finalTargetPos, out var taskType)) continue;
                    driver.Tasks.DriveToPosition(finalTargetPos, Config.DriveSpeed, VehicleDrivingFlags.Normal);
                    PluginState.TaskedVehicles.Add(vehicle, new YieldTask { TargetPosition = finalTargetPos, TaskType = taskType, GameTimeStarted = Game.GameTime, IsWaiting = false });

                    if (!Config.ShowDebugLines || PluginState.TaskedVehicleBlips.ContainsKey(vehicle)) continue;
                    var blip = vehicle.AttachBlip();
                    blip.Color = Color.Green;
                    PluginState.TaskedVehicleBlips.Add(vehicle, blip);
                }
            }

            creepCandidates.Sort((v1, v2) => v1.DistanceTo(emergencyVehicle.Position).CompareTo(v2.DistanceTo(emergencyVehicle.Position)));

            foreach (var vehicle in creepCandidates)
            {
                var checkStartPos = vehicle.Position + new Vector3(0, 0, 0.5f);
                var sideCheckDistance = Config.IntersectionCreepSideDistance + vehicle.Width / 2f;
                const TraceFlags traceFlags = TraceFlags.IntersectVehicles | TraceFlags.IntersectObjects;

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

                if (!sidePushDirection.HasValue) continue;

                if (!TryFindValidCreepPosition(vehicle, emergencyVehicle, sidePushDirection.Value, assignedCreepTargetPositions, out var finalTargetPos)) continue;
                vehicle.Driver.Tasks.DriveToPosition(finalTargetPos, Config.IntersectionCreepDriveSpeed, VehicleDrivingFlags.Normal | VehicleDrivingFlags.StopAtDestination);

                var creepTask = new CreepTask { TargetPosition = finalTargetPos, GameTimeStarted = Game.GameTime };
                PluginState.IntersectionCreepTaskedVehicles.Add(vehicle, creepTask);
                assignedCreepTargetPositions.Add(finalTargetPos);

                if (!Config.ShowDebugLines || PluginState.TaskedVehicleBlips.ContainsKey(vehicle)) continue;
                var blip = vehicle.AttachBlip();
                blip.Color = Color.Fuchsia;
                PluginState.TaskedVehicleBlips.Add(vehicle, blip);
            }
        }

        private static bool TryFindValidCreepPosition(Vehicle vehicle, Vehicle emergencyVehicle, Vector3 sidePushDirection, ICollection<Vector3> assignedCreepTargetPositions, out Vector3 finalTargetPos)
        {
            for (var forwardDistance = Config.IntersectionCreepForwardDistance; forwardDistance >= 3.0f; forwardDistance -= 2.5f)
            {
                var sideDistance = Config.IntersectionCreepSideDistance;
                var tentativeTargetPos = vehicle.Position + vehicle.ForwardVector * forwardDistance + sidePushDirection * sideDistance;

                var groundZ = World.GetGroundZ(tentativeTargetPos, false, false);
                if (!groundZ.HasValue)
                {
                    if (Config.ShowDebugLines) PluginState.FailedCreepCandidates[vehicle] = tentativeTargetPos;
                    continue;
                }

                var tempFinalPos = new Vector3(tentativeTargetPos.X, tentativeTargetPos.Y, groundZ.Value);

                if (!IsPositionSafeFromPlayer(tempFinalPos, emergencyVehicle))
                {
                    if (Config.ShowDebugLines) PluginState.FailedCreepCandidates[vehicle] = tempFinalPos;
                    continue;
                }

                var vehicleLateralOffset = Vector3.Dot(vehicle.Position - emergencyVehicle.Position, emergencyVehicle.RightVector);
                var targetLateralOffset = Vector3.Dot(tempFinalPos - emergencyVehicle.Position, emergencyVehicle.RightVector);
                if (vehicleLateralOffset * targetLateralOffset < 0f || assignedCreepTargetPositions.Any(assignedTarget => tempFinalPos.DistanceTo(assignedTarget) < vehicle.Width + 2.5f) || Math.Abs(tempFinalPos.Z - vehicle.Position.Z) > 3.0f)
                {
                    if (Config.ShowDebugLines) PluginState.FailedCreepCandidates[vehicle] = tempFinalPos;
                    continue;
                }

                var pathTrace = World.TraceLine(vehicle.Position, tempFinalPos, TraceFlags.IntersectWorld, vehicle);
                if (pathTrace.Hit)
                {
                    if (Config.ShowDebugLines) PluginState.FailedCreepCandidates[vehicle] = tempFinalPos;
                    continue;
                }

                finalTargetPos = tempFinalPos;
                return true;
            }

            finalTargetPos = Vector3.Zero;
            return false;
        }
    }
}