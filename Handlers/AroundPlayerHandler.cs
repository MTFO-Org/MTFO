using System;
using System.Collections.Generic;
using System.Drawing;
using LSPD_First_Response.Mod.API;
using Rage;
using Rage.Native;

namespace MTFOv4
{
    internal static class AroundPlayerHandler
    {
        private static uint _nextScanTime;

        /// <summary>
        /// Main logic loop that runs every frame to manage how AI vehicles behave around the player.
        /// </summary>
        /// <param name="playerEntity">The player's current vehicle or the player's ped.</param>
        public static void Process(Entity playerEntity)
        {
            if (!EntryPoint.Settings.EnableAroundPlayerLogic) return;

            if (EntryPoint.Settings.AroundPlayerLogicOnlyInVehicle && !(playerEntity is Vehicle))
            {
                ClearAll();
                return;
            }

            ManageExistingTasks(playerEntity);

            if (playerEntity.Speed < 0.1f && Game.GameTime > _nextScanTime)
            {
                FindAndTaskNewVehicles(playerEntity);
                _nextScanTime = Game.GameTime + 1000;
            }
        }

        /// <summary>
        /// Fully resets the handler by clearing all currently tasked vehicles and removing their data from the state.
        /// </summary>
        public static void ClearAll()
        {
            if (PluginState.AroundPlayerTaskedVehicles.Count == 0) return;

            var vehiclesToUntask = new List<Vehicle>(PluginState.AroundPlayerTaskedVehicles.Keys);
            foreach (var vehicle in vehiclesToUntask) UntaskVehicle(vehicle);
            PluginState.AroundPlayerTaskedVehicles.Clear();
        }

        /// <summary>
        /// A math helper that calculates the point on a specific line segment that is closest to a given position.
        /// </summary>
        /// <param name="a">The starting point of the line segment.</param>
        /// <param name="b">The ending point of the line segment.</param>
        /// <param name="p">The target point to check against.</param>
        /// <returns>A vector representing the closest point on the line.</returns>
        private static Vector3 GetClosestPointOnLineSegment(Vector3 a, Vector3 b, Vector3 p)
        {
            var ap = p - a;
            var ab = b - a;

            // get squared length of the line segment
            var ab2 = ab.LengthSquared();
            if (ab2 == 0.0f) return a;

            // project point onto the line
            var apAb = Vector3.Dot(ap, ab);
            // clamp it so it doesnt go past the segment bounds
            var t = apAb / ab2;
            t = Math.Max(0, Math.Min(1, t));

            return a + ab * t;
        }

        /// <summary>
        /// Figures out if there is a safe spot on the road for an AI vehicle to drive to so it can get around the player.
        /// </summary>
        /// <param name="vehicleToTask">The AI vehicle that needs instructions.</param>
        /// <param name="playerEntity">The player's entity for proximity checking.</param>
        /// <param name="playerRoadPosition">Cached data about what road and lane the player is currently in.</param>
        /// <param name="overtakePosition">The output vector where the AI should drive to.</param>
        /// <returns>True if a clear path and valid position were found.</returns>
        private static bool TryFindOvertakePosition(Vehicle vehicleToTask, Entity playerEntity, RoadPosition playerRoadPosition, out Vector3 overtakePosition)
        {
            overtakePosition = Vector3.Zero;
            if (vehicleToTask == null || !vehicleToTask.Exists() || playerEntity == null || !playerEntity.Exists() || playerRoadPosition == null) return false;

            RoadPosition aiRoadPosition = new RoadPosition(vehicleToTask);
            aiRoadPosition.Process();

            bool playerHasLanes = playerRoadPosition.TotalLanes > 0;
            float baseLaneWidth = 4.5f;
            int laneOffsetMultiplier;

            if (playerHasLanes && playerRoadPosition.CurrentLane == aiRoadPosition.CurrentLane)
            {
                if (playerRoadPosition.LanesToLeft > 0)
                {
                    laneOffsetMultiplier = -1;
                }
                else if (playerRoadPosition.LanesToRight > 0)
                {
                    laneOffsetMultiplier = 1;
                }
                else
                {
                    laneOffsetMultiplier = -1;
                }
            }
            else if (playerHasLanes && aiRoadPosition.CurrentLane != playerRoadPosition.CurrentLane && aiRoadPosition.CurrentLane > 0)
            {
                laneOffsetMultiplier = aiRoadPosition.CurrentLane - playerRoadPosition.CurrentLane;
            }
            else
            {
                laneOffsetMultiplier = -1;
            }

            Vector3 searchPosition = playerEntity.Position + (playerEntity.ForwardVector * EntryPoint.Settings.AroundPlayerOvertakeDistance);

            if (!NativeFunction.Natives.GET_CLOSEST_VEHICLE_NODE_WITH_HEADING<bool>(searchPosition, out Vector3 nodePosition, out float nodeHeading, 1, 3.0f, 0)) return false;

            Vector3 nodeDirection = MathHelper.ConvertHeadingToDirection(nodeHeading).ToNormalized();

            // check if the node is facing the opposite way
            if (Vector3.Dot(nodeDirection, playerEntity.ForwardVector) < -0.2f)
            {
                nodeDirection = -nodeDirection;
            }

            // get the right vector relative to the road direction
            Vector3 roadRightVector = Vector3.Cross(nodeDirection, Vector3.WorldUp).ToNormalized();
            float targetLateralOffset = laneOffsetMultiplier * baseLaneWidth;

            if (laneOffsetMultiplier == -1 && !playerRoadPosition.OneWayRoad && playerRoadPosition.LanesToLeft == 0)
            {
                targetLateralOffset = -(baseLaneWidth * 1.5f);
            }

            // push the target out depending on the lane offset
            Vector3 rawTargetPosition = nodePosition + (roadRightVector * targetLateralOffset);

            if (RoadUtilities.GetGroundPosition(rawTargetPosition, out Vector3 groundedTarget))
            {
                overtakePosition = groundedTarget;
            }
            else
            {
                overtakePosition = rawTargetPosition;
            }

            // dont pick spot too high or low from veh
            if (Math.Abs(overtakePosition.Z - vehicleToTask.Position.Z) > 4.5f) return false;

            Vector3 traceStart = vehicleToTask.Position + (Vector3.WorldUp * 0.5f);
            Vector3 traceEnd = overtakePosition + (Vector3.WorldUp * 0.5f);
            HitResult pathTrace = World.TraceLine(traceStart, traceEnd, TraceFlags.IntersectObjects | TraceFlags.IntersectVehicles, vehicleToTask, playerEntity);

            if (pathTrace.Hit) return false;

            Vector3 directionToTarget = (overtakePosition - vehicleToTask.Position).ToNormalized();

            // make sure the target isnt straight behind ai veh
            if (Vector3.Dot(vehicleToTask.ForwardVector, directionToTarget) < -0.5f) return false;

            return true;
        }

        /// <summary>
        /// Monitors vehicles that are already tasked, handling timeouts, completion, or stuck-vehicle logic like reversing.
        /// </summary>
        /// <param name="playerEntity">Used to check if the player has already passed the tasked vehicle.</param>
        private static void ManageExistingTasks(Entity playerEntity)
        {
            var vehiclesToUntask = new List<Vehicle>();
            var tasksToUpdate = new Dictionary<Vehicle, AroundPlayerTask>();

            foreach (var entry in PluginState.AroundPlayerTaskedVehicles)
            {
                var vehicle = entry.Key;
                var task = entry.Value;

                if (vehicle == null || !vehicle.Exists() || vehicle.Driver == null || !vehicle.Driver.Exists())
                {
                    vehiclesToUntask.Add(vehicle);
                    continue;
                }

                var timeElapsed = Game.GameTime - task.GameTimeStarted;
                var isTimedOut = timeElapsed > EntryPoint.Settings.AroundPlayerTaskTimeoutMs;
                var hasCompleted = vehicle.Position.DistanceTo(task.TargetPosition) < EntryPoint.Settings.AroundPlayerTaskCompletionDistance;
                var isTooFar = vehicle.Position.DistanceTo(playerEntity.Position) > EntryPoint.Settings.AroundPlayerDetectionRange + 40f;
                var vectorToTaskedVeh = vehicle.Position - playerEntity.Position;

                // check if the ai passed player
                var hasPassedPlayer = Vector3.Dot(playerEntity.ForwardVector, vectorToTaskedVeh) > 2.0f;

                if (isTimedOut || hasCompleted || isTooFar || hasPassedPlayer)
                {
                    vehiclesToUntask.Add(vehicle);
                    continue;
                }

                if (task.GameTimeBackupStarted == 0)
                {
                    var isStuck = timeElapsed > 1500 && vehicle.Speed < 0.5f;

                    if (isStuck)
                    {
                        var updatedTask = task;
                        updatedTask.GameTimeBackupStarted = Game.GameTime;
                        tasksToUpdate[vehicle] = updatedTask;

                        var reverseStart = vehicle.Position + (Vector3.WorldUp * 0.5f);
                        var reverseEnd = reverseStart - (vehicle.ForwardVector * 8.0f);
                        var backTrace = World.TraceLine(reverseStart, reverseEnd, TraceFlags.IntersectVehicles | TraceFlags.IntersectWorld, vehicle);

                        var safeReverseDistance = backTrace.Hit ? Math.Max(1.0f, vehicle.Position.DistanceTo(backTrace.HitPosition) - 2.5f) : 6.0f;
                        var backupPos = vehicle.Position - (vehicle.ForwardVector * safeReverseDistance);

                        vehicle.Driver.Tasks.DriveToPosition(backupPos, 4f, VehicleDrivingFlags.Reverse | VehicleDrivingFlags.IgnorePathFinding);
                    }
                }
                else
                {
                    if (Game.GameTime - task.GameTimeBackupStarted > 2500)
                    {
                        var updatedTask = task;
                        updatedTask.GameTimeBackupStarted = 0;
                        updatedTask.GameTimeStarted = Game.GameTime;

                        if (NativeFunction.Natives.GET_CLOSEST_VEHICLE_NODE_WITH_HEADING<bool>(task.TargetPosition, out Vector3 nodePos, out float nodeHeading, 1, 3.0f, 0))
                        {
                            var nodeDir = MathHelper.ConvertHeadingToDirection(nodeHeading).ToNormalized();
                            if (Vector3.Dot(nodeDir, vehicle.ForwardVector) < -0.2f)
                            {
                                nodeDir = -nodeDir;
                                nodeHeading = (nodeHeading + 180f) % 360f;
                            }

                            var roadRight = Vector3.Cross(nodeDir, Vector3.WorldUp).ToNormalized();

                            if (RoadUtilities.GetRoadBoundary(nodePos, nodeHeading, out Vector3 boundaryPos))
                            {
                                updatedTask.TargetPosition = boundaryPos + (roadRight * 1.5f);
                            }
                            else
                            {
                                updatedTask.TargetPosition = nodePos + (roadRight * 4.0f);
                            }

                            if (RoadUtilities.GetGroundPosition(updatedTask.TargetPosition, out Vector3 groundedTarget))
                            {
                                updatedTask.TargetPosition = groundedTarget;
                            }
                        }

                        tasksToUpdate[vehicle] = updatedTask;
                        vehicle.Driver.Tasks.DriveToPosition(updatedTask.TargetPosition, 15f, VehicleDrivingFlags.Normal);
                    }
                }

                if (EntryPoint.Settings.ShowDebugLines)
                {
                    Debug.DrawLine(vehicle.Position, task.TargetPosition, Color.Cyan);
                }
            }

            for (int i = 0; i < vehiclesToUntask.Count; i++)
            {
                UntaskVehicle(vehiclesToUntask[i]);
            }

            foreach (var entry in tasksToUpdate)
            {
                if (PluginState.AroundPlayerTaskedVehicles.ContainsKey(entry.Key))
                {
                    PluginState.AroundPlayerTaskedVehicles[entry.Key] = entry.Value;
                }
            }
        }

        /// <summary>
        /// Scans the area for new AI vehicles that are stuck behind the player and should be told to go around.
        /// </summary>
        /// <param name="playerEntity">The center point for the radius scan.</param>
        private static void FindAndTaskNewVehicles(Entity playerEntity)
        {
            if (playerEntity == null || !playerEntity.Exists()) return;

            Entity[] nearbyEntities = World.GetEntities(playerEntity.Position, EntryPoint.Settings.AroundPlayerDetectionRange + 5f, GetEntitiesFlags.ConsiderAllVehicles | GetEntitiesFlags.ExcludePlayerVehicle);
            if (nearbyEntities == null || nearbyEntities.Length == 0) return;

            RoadPosition playerRoadPosition = new RoadPosition(playerEntity);
            playerRoadPosition.Process();

            for (int i = 0; i < nearbyEntities.Length; i++)
            {
                Vehicle vehicle = nearbyEntities[i] as Vehicle;
                if (vehicle == null) continue;
                if (!ShouldTaskVehicle(vehicle, playerEntity)) continue;

                if (TryFindOvertakePosition(vehicle, playerEntity, playerRoadPosition, out Vector3 targetPosition))
                {
                    TaskVehicle(vehicle, targetPosition);
                }
            }
        }

        /// <summary>
        /// Checks several conditions to see if a specific vehicle is a good candidate to be given an overtake task.
        /// </summary>
        /// <param name="vehicle">The vehicle to evaluate.</param>
        /// <param name="playerEntity">The player to check positioning against.</param>
        /// <returns>True if the vehicle is eligible for tasking.</returns>
        private static bool ShouldTaskVehicle(Vehicle vehicle, Entity playerEntity)
        {
            if (!vehicle.Exists() || !vehicle.IsAlive || vehicle.Driver == null || !vehicle.Driver.Exists()) return false;

            if (PluginState.AroundPlayerTaskedVehicles.ContainsKey(vehicle)) return false;

            if (vehicle.IsPoliceVehicle || vehicle.Model.IsEmergencyVehicle) return false;

            var vectorToVehicle = vehicle.Position - playerEntity.Position;
            if (Vector3.Dot(playerEntity.ForwardVector, vectorToVehicle) > 0f) return false;

            // dot product gives dist straight back
            var backwardDistance = Vector3.Dot(playerEntity.ForwardVector, vectorToVehicle) * -1;
            if (backwardDistance > EntryPoint.Settings.AroundPlayerDetectionRange || backwardDistance < 1f) return false;

            // grab lateral dist using right vector
            var lateralDistance = Math.Abs(Vector3.Dot(playerEntity.RightVector, vectorToVehicle));
            if (lateralDistance > EntryPoint.Settings.AroundPlayerDetectionWidth / 2f) return false;

            if (Functions.IsPedInPursuit(vehicle.Driver)) return false;

            return !(vehicle.Speed > 6f) && !(Vector3.Dot(playerEntity.ForwardVector, vehicle.ForwardVector) < 0.8f);
        }

        /// <summary>
        /// Gives the AI driver the actual task to drive to the calculated overtake position and adds them to the tracking list.
        /// </summary>
        /// <param name="vehicle">The vehicle to task.</param>
        /// <param name="targetPosition">The coordinate the AI should drive toward.</param>
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
        }

        /// <summary>
        /// Clears a specific vehicle's tasks and removes them from the tracking dictionary.
        /// </summary>
        /// <param name="vehicle">The vehicle to stop tracking.</param>
        private static void UntaskVehicle(Vehicle vehicle)
        {
            if (vehicle != null && vehicle.Exists() && vehicle.Driver != null && vehicle.Driver.Exists())
            {
                vehicle.Driver.Tasks.Clear();
            }

            PluginState.AroundPlayerTaskedVehicles.Remove(vehicle);
        }
    }
}