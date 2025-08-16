using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using LSPD_First_Response.Engine.Scripting.Entities;
using LSPD_First_Response.Mod.API;
using MTFO.Misc;
using Rage;
using Rage.Native;

namespace MTFO.Handlers
{
    internal static class AroundPlayerHandler
    {
        public static void Process(Entity playerEntity)
        {
            if (!Config.EnableAroundPlayerLogic) return;

            if (Config.AroundPlayerLogicOnlyInVehicle && !(playerEntity is Vehicle))
            {
                if (PluginState.AroundPlayerTaskedVehicles.Any()) ClearAroundPlayerTasks();

                return;
            }

            ManageExistingTasks(playerEntity);

            if (playerEntity.Speed < 0.1f) FindAndTaskNewVehicles(playerEntity);
        }

        private static Vector3 GetClosestPointOnLineSegment(Vector3 a, Vector3 b, Vector3 p)
        {
            var ap = p - a;
            var ab = b - a;
            var ab2 = ab.LengthSquared();
            if (ab2 == 0.0f) return a;

            var apAb = Vector3.Dot(ap, ab);
            var t = apAb / ab2;
            t = Math.Max(0, Math.Min(1, t));
            return a + ab * t;
        }

        private static bool TryFindOvertakePosition(Vehicle vehicleToTask, Entity playerEntity, out Vector3 overtakePosition)
        {
            overtakePosition = Vector3.Zero;
            var overtakeDirections = new[] { -vehicleToTask.RightVector, vehicleToTask.RightVector };
            var candidatePositions = new List<(Vector3 position, float score)>();

            (Vector3 point, OvertakeFailureReason reason)? bestFailure = null;

            foreach (var direction in overtakeDirections)
            {
                var laneOffset = vehicleToTask.Width / 2f + 3.0f;
                var moveOutPos = vehicleToTask.Position + direction * laneOffset;

                var sideTrace = World.TraceLine(vehicleToTask.Position, moveOutPos, TraceFlags.IntersectVehicles, vehicleToTask, playerEntity);
                if (sideTrace.Hit)
                {
                    if (!bestFailure.HasValue || OvertakeFailureReason.SideTraceHit > bestFailure.Value.reason) bestFailure = (moveOutPos, OvertakeFailureReason.SideTraceHit);
                    continue;
                }

                var rawTargetPoint = playerEntity.Position + playerEntity.ForwardVector * Config.AroundPlayerOvertakeDistance + direction * laneOffset;

                if (!NativeFunction.Natives.GET_CLOSEST_ROAD<bool>(rawTargetPoint, 1.0f, 1, out Vector3 nodeA, out Vector3 nodeB, out int _, out int _, out float _, false))
                {
                    if (!bestFailure.HasValue || OvertakeFailureReason.NoRoadFound > bestFailure.Value.reason) bestFailure = (rawTargetPoint, OvertakeFailureReason.NoRoadFound);
                    continue;
                }

                var roadDir = (nodeB - nodeA).ToNormalized();
                if (Vector3.Dot(playerEntity.ForwardVector, roadDir) < 0)
                {
                    (nodeA, nodeB) = (nodeB, nodeA);
                    roadDir = -roadDir;
                }

                var roadHeading = roadDir.ToHeading();
                var vehicleHeading = playerEntity.Heading;
                var headingDifference = Math.Abs(MathHelper.NormalizeHeading(vehicleHeading) - MathHelper.NormalizeHeading(roadHeading));
                if (headingDifference > 180) headingDifference = 360 - headingDifference;

                if (headingDifference > 90.0f)
                {
                    if (!bestFailure.HasValue || OvertakeFailureReason.BadHeading > bestFailure.Value.reason) bestFailure = (rawTargetPoint, OvertakeFailureReason.BadHeading);
                    continue;
                }

                var centerlinePoint = GetClosestPointOnLineSegment(nodeA, nodeB, rawTargetPoint);

                var roadSideVector = Vector3.Cross(roadDir, Vector3.WorldUp).ToNormalized();
                var vectorToRawTarget = rawTargetPoint - centerlinePoint;
                var lateralOffsetFromCenter = Vector3.Dot(vectorToRawTarget, roadSideVector);

                var finalTargetPoint = centerlinePoint + roadSideVector * lateralOffsetFromCenter;

                if (finalTargetPoint.DistanceTo(rawTargetPoint) > 12.0f || Math.Abs(finalTargetPoint.Z - vehicleToTask.Position.Z) > 4.5f)
                {
                    if (!bestFailure.HasValue || OvertakeFailureReason.TargetTooFarOrHigh > bestFailure.Value.reason) bestFailure = (finalTargetPoint, OvertakeFailureReason.TargetTooFarOrHigh);
                    continue;
                }

                var pathTrace = World.TraceLine(moveOutPos, finalTargetPoint, TraceFlags.IntersectObjects, vehicleToTask, playerEntity);
                if (pathTrace.Hit)
                {
                    if (!bestFailure.HasValue || OvertakeFailureReason.PathTraceHit > bestFailure.Value.reason) bestFailure = (finalTargetPoint, OvertakeFailureReason.PathTraceHit);
                    continue;
                }

                var score = finalTargetPoint.DistanceTo(rawTargetPoint);
                candidatePositions.Add((finalTargetPoint, score));
            }

            if (candidatePositions.Any())
            {
                var bestCandidate = candidatePositions.OrderBy(c => c.score).First();
                overtakePosition = bestCandidate.position;
                return true;
            }

            if (Config.ShowDebugLines && bestFailure.HasValue) PluginState.FailedAroundPlayerCandidates[vehicleToTask] = bestFailure.Value;

            return false;
        }

        private static void ManageExistingTasks(Entity playerEntity)
        {
            var vehiclesToUntask = new List<Vehicle>();
            var tasksToUpdate = new Dictionary<Vehicle, AroundPlayerTask>();

            Ped pulloverSuspect = null;
            if (Functions.IsPlayerPerformingPullover()) pulloverSuspect = Functions.GetPulloverSuspect(Functions.GetCurrentPullover());

            foreach (var entry in PluginState.AroundPlayerTaskedVehicles)
            {
                var vehicle = entry.Key;
                var task = entry.Value;

                if (!vehicle) continue;

                var isPulloverTarget = pulloverSuspect != null && pulloverSuspect.Exists() && vehicle.Exists() && pulloverSuspect == vehicle.Driver;
                var isTimedOut = Game.GameTime - task.GameTimeStarted > Config.AroundPlayerTaskTimeoutMs;
                var hasCompleted = vehicle.Position.DistanceTo(task.TargetPosition) < Config.AroundPlayerTaskCompletionDistance;
                var isTooFar = !playerEntity.Exists() || vehicle.Position.DistanceTo(playerEntity.Position) > Config.AroundPlayerDetectionRange + 40f;
                var vectorToTaskedVeh = vehicle.Position - playerEntity.Position;
                var hasPassedPlayer = Vector3.Dot(playerEntity.ForwardVector, vectorToTaskedVeh) > 2.0f;

                if (isPulloverTarget || !vehicle.Exists() || isTimedOut || hasCompleted || isTooFar || hasPassedPlayer)
                {
                    vehiclesToUntask.Add(vehicle);
                    continue;
                }

                if (task.GameTimeBackupStarted == 0)
                {
                    var isStuck = false;
                    if (playerEntity is Vehicle playerVehicle && vehicle.Speed < 0.2f && vehicle.Position.DistanceTo(playerVehicle.Position) < 5.0f)
                    {
                        var traceStart = vehicle.Position + vehicle.ForwardVector * (vehicle.Length / 2f);
                        var trace = World.TraceLine(traceStart, traceStart + vehicle.ForwardVector * 1.5f, TraceFlags.IntersectVehicles, vehicle);
                        if (trace.Hit && trace.HitEntity == playerVehicle) isStuck = true;
                    }

                    if (!isStuck) continue;
                    var updatedTask = task;
                    updatedTask.GameTimeBackupStarted = Game.GameTime;
                    tasksToUpdate[vehicle] = updatedTask;

                    var backupPos = vehicle.Position - vehicle.ForwardVector * 3.5f;
                    vehicle.Driver.Tasks.DriveToPosition(backupPos, 5f, VehicleDrivingFlags.Reverse | VehicleDrivingFlags.StopAtDestination);
                }
                else
                {
                    if (Game.GameTime - task.GameTimeBackupStarted <= 2500) continue;
                    var updatedTask = task;
                    updatedTask.GameTimeBackupStarted = 0;
                    tasksToUpdate[vehicle] = updatedTask;

                    vehicle.Driver.Tasks.DriveToPosition(task.TargetPosition, 15f, VehicleDrivingFlags.Normal);
                }
            }

            foreach (var vehicle in vehiclesToUntask) UntaskVehicle(vehicle);

            foreach (var entry in tasksToUpdate)
                if (PluginState.AroundPlayerTaskedVehicles.ContainsKey(entry.Key))
                    PluginState.AroundPlayerTaskedVehicles[entry.Key] = entry.Value;
        }

        private static void FindAndTaskNewVehicles(Entity playerEntity)
        {
            if (Config.ShowDebugLines) PluginState.FailedAroundPlayerCandidates.Clear();

            var nearbyVehicles = World.GetEntities(playerEntity.Position, Config.AroundPlayerDetectionRange + 5f, GetEntitiesFlags.ConsiderAllVehicles | GetEntitiesFlags.ExcludePlayerVehicle).OfType<Vehicle>();

            foreach (var vehicle in nearbyVehicles)
            {
                if (!ShouldTaskVehicle(vehicle, playerEntity)) continue;

                if (TryFindOvertakePosition(vehicle, playerEntity, out var targetPosition)) TaskVehicle(vehicle, targetPosition);
            }
        }

        private static bool ShouldTaskVehicle(Vehicle vehicle, Entity playerEntity)
        {
            if (!vehicle.Exists() || !vehicle.IsAlive || !vehicle.Driver.Exists()) return false;

            if (vehicle.IsPoliceVehicle || vehicle.Model.IsEmergencyVehicle)
            {
                return false;
            }

            if (PluginState.TaskedVehicles.ContainsKey(vehicle) || PluginState.IntersectionTaskedVehicles.Contains(vehicle) || PluginState.IntersectionCreepTaskedVehicles.ContainsKey(vehicle) || PluginState.OncomingBrakingVehicles.ContainsKey(vehicle) || PluginState.AroundPlayerTaskedVehicles.ContainsKey(vehicle)) return false;

            var vectorToVehicle = vehicle.Position - playerEntity.Position;
            if (Vector3.Dot(playerEntity.ForwardVector, vectorToVehicle) > 0f) return false;

            var backwardDistance = Vector3.Dot(playerEntity.ForwardVector, vectorToVehicle) * -1;
            if (backwardDistance > Config.AroundPlayerDetectionRange || backwardDistance < 1f) return false;

            var lateralDistance = Math.Abs(Vector3.Dot(playerEntity.RightVector, vectorToVehicle));
            if (lateralDistance > Config.AroundPlayerDetectionWidth / 2f) return false;

            return !(vehicle.Speed > 4f) && !(Vector3.Dot(playerEntity.ForwardVector, vehicle.ForwardVector) < 0.8f);
        }

        private static void TaskVehicle(Vehicle vehicle, Vector3 targetPosition)
        {
            if (PluginState.AroundPlayerTaskedVehicles.ContainsKey(vehicle)) return;

            vehicle.Driver.Tasks.DriveToPosition(targetPosition, 15f, VehicleDrivingFlags.Normal);
            PluginState.AroundPlayerTaskedVehicles.Add(vehicle, new AroundPlayerTask
            {
                TargetPosition = targetPosition,
                GameTimeStarted = Game.GameTime,
                GameTimeBackupStarted = 0
            });

            if (!Config.ShowDebugLines || PluginState.TaskedVehicleBlips.ContainsKey(vehicle)) return;
            var blip = vehicle.AttachBlip();
            blip.Color = Color.Cyan;
            PluginState.TaskedVehicleBlips.Add(vehicle, blip);
        }

        private static void UntaskVehicle(Vehicle vehicle)
        {
            if (vehicle.Exists() && vehicle.Driver.Exists()) vehicle.Driver.Tasks.Clear();

            if (PluginState.TaskedVehicleBlips.TryGetValue(vehicle, out var blip))
            {
                if (blip.Exists()) blip.Delete();
                PluginState.TaskedVehicleBlips.Remove(vehicle);
            }

            PluginState.AroundPlayerTaskedVehicles.Remove(vehicle);
        }

        private static void ClearAroundPlayerTasks()
        {
            var vehiclesToUntask = new List<Vehicle>(PluginState.AroundPlayerTaskedVehicles.Keys);
            foreach (var vehicle in vehiclesToUntask) UntaskVehicle(vehicle);
        }
    }
}