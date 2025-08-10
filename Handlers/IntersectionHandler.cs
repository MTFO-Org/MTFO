using System;
using System.Drawing;
using System.Linq;
using MTFO.Misc;
using Rage;
using Rage.Native;
using Object = Rage.Object;

namespace MTFO.Handlers
{
    internal static class IntersectionHandler
    {
        //TODO: fix creeping deadzone when directly behind targetvehicle

        // Opticom: Forces a nearby traffic light to turn green
        private static void SetTrafficLightGreen(Object trafficLight)
        {
            // Set light green fiber
            GameFiber.StartNew(() =>
            {
                // Force Green
                NativeFunction.Natives.SET_ENTITY_TRAFFICLIGHT_OVERRIDE(trafficLight, 0);
                // Wait for the configured duration
                GameFiber.Wait(Config.OpticomGreenDurationMs);
                // Revert the light back to its normal state
                NativeFunction.Natives.SET_ENTITY_TRAFFICLIGHT_OVERRIDE(trafficLight, 3);
            });
        }

        // Handles all logic related to approaching and managing intersections
        public static void Process(Vehicle emergencyVehicle)
        {
            if (PluginState.ActiveIntersectionCenter.HasValue)
            {
                // If we have an active intersection, check if we've passed it.
                if (CheckIfPastIntersection(emergencyVehicle)) return;

                // If an intersection is active, manage the vehicles associated with it.
                ManageExistingIntersectionTasks();
            }
            else
            {
                // If no intersection is active, try to detect one.
                DetectNewIntersection(emergencyVehicle);
            }

            // If we don't have an active intersection at this point, do nothing further.
            if (!PluginState.ActiveIntersectionCenter.HasValue) return;

            // Process vehicles near the active intersection.
            ProcessNearbyVehicles(emergencyVehicle);
        }

        private static bool CheckIfPastIntersection(Vehicle emergencyVehicle)
        {
            var vectorToIntersection = PluginState.ActiveIntersectionCenter.Value - emergencyVehicle.Position;
            var dotPlayerToCenter = Vector3.Dot(emergencyVehicle.ForwardVector, vectorToIntersection);

            // If we are moving away from the intersection or are too far past it...
            if (dotPlayerToCenter < -10f || vectorToIntersection.Length() > Config.IntersectionSearchMaxDistance + 20f)
            {
                // ...clear all tasks and start a cooldown to prevent re-detecting the same intersection.
                Entry.ClearAllTrackedVehicles();
                PluginState.IntersectionClearTime = Game.GameTime; // Start the cooldown timer
                return true;
            }

            return false;
        }

        private static void ManageExistingIntersectionTasks()
        {
            var center = PluginState.ActiveIntersectionCenter.Value;
            // Remove cross-traffic vehicles that are too far away.
            PluginState.IntersectionTaskedVehicles.RemoveWhere(v =>
            {
                var shouldRemove = !v.Exists() || v.Position.DistanceTo(center) > 80f;
                if (!shouldRemove || !PluginState.TaskedVehicleBlips.TryGetValue(v, out var blip))
                    return shouldRemove;
                if (blip.Exists()) blip.Delete();
                PluginState.TaskedVehicleBlips.Remove(v);

                return shouldRemove;
            });
            // Remove "creeping" vehicles if they are too far, have finished, have been abandoned, or have timed out.
            var creepersToUntask = PluginState.IntersectionCreepTaskedVehicles.Where(kvp => !kvp.Key.Exists() || kvp.Key.Position.DistanceTo(center) > 80f || kvp.Key.Position.DistanceTo(kvp.Value.TargetPosition) < Config.CreepTaskCompletionDistance || kvp.Key.Position.DistanceTo(kvp.Value.TargetPosition) > Config.CreepTaskAbandonDistance || Game.GameTime - kvp.Value.GameTimeStarted > Config.CreepTaskTimeoutMs).Select(kvp => kvp.Key).ToList();
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
        }

        private static void DetectNewIntersection(Vehicle emergencyVehicle)
        {
            // Don't search for a new intersection if the cooldown is active.
            if (Game.GameTime - PluginState.IntersectionClearTime < Config.IntersectionDetectionCooldownMs) return;

            Object foundObject = null;
            var allIntersectionModels = GameModels.TrafficLightModels.Concat(GameModels.StopSignModels).ToArray();
            // Search in steps ahead of the player to find a traffic light or stop sign prop.
            for (var searchDistance = Config.IntersectionSearchMaxDistance; searchDistance > Config.IntersectionSearchMinDistance; searchDistance -= Config.IntersectionSearchStepSize)
            {
                var searchPosition = emergencyVehicle.Position + emergencyVehicle.ForwardVector * searchDistance;
                foreach (var modelHash in allIntersectionModels)
                {
                    foundObject = NativeFunction.Natives.GET_CLOSEST_OBJECT_OF_TYPE<Object>(searchPosition, Config.IntersectionSearchRadius, modelHash, false, false, false);
                    if (foundObject == null) continue;
                    // Check if the found object's heading is aligned with the player's.
                    var isHeadingValid = false;
                    var headingDiff = Math.Abs(emergencyVehicle.Heading - foundObject.Heading);
                    if (headingDiff < Config.IntersectionHeadingThreshold || headingDiff > 360 - Config.IntersectionHeadingThreshold)
                        isHeadingValid = true;
                    if (isHeadingValid) break;
                    foundObject = null;
                }

                if (foundObject != null) break;
            }

            // If a valid object was found, set it as the active intersection.
            if (foundObject != null)
            {
                PluginState.IsStopSignIntersection = GameModels.StopSignModels.Contains(foundObject.Model.Hash);
                if (PluginState.IsStopSignIntersection)
                {
                    var rawCenter = foundObject.Position + foundObject.ForwardVector * 12f;
                    var groundZ = World.GetGroundZ(rawCenter, false, false);
                    PluginState.ActiveIntersectionCenter = groundZ.HasValue ? new Vector3(rawCenter.X, rawCenter.Y, groundZ.Value) : rawCenter;
                }
                else
                {
                    PluginState.ActiveIntersectionCenter = foundObject.Position;
                    if (Config.EnableOpticom) SetTrafficLightGreen(foundObject);
                }
            }
        }

        private static void ProcessNearbyVehicles(Vehicle emergencyVehicle)
        {
            var intersectionCenter = PluginState.ActiveIntersectionCenter.Value;
            // Get all vehicles near the active intersection.
            var nearbyEntities = World.GetEntities(intersectionCenter, 60f, GetEntitiesFlags.ConsiderAllVehicles | GetEntitiesFlags.ExcludePlayerVehicle);

            // Process each vehicle near the intersection.
            foreach (var vehicle in nearbyEntities.OfType<Vehicle>())
            {
                // Basic filtering for invalid or already-tasked vehicles.
                if (!vehicle.Exists() || !vehicle.IsAlive || !vehicle.Driver.Exists() || vehicle.HasSiren) continue;
                if (PluginState.TaskedVehicles.ContainsKey(vehicle) || PluginState.IntersectionTaskedVehicles.Contains(vehicle) || PluginState.IntersectionCreepTaskedVehicles.ContainsKey(vehicle)) continue;

                var headingDot = Vector3.Dot(emergencyVehicle.ForwardVector, vehicle.ForwardVector);

                // Check for "creep" candidates: vehicles stopped in front of us, facing the same direction.
                if (headingDot > 0.8f && vehicle.Speed < Config.MinYieldSpeedMph)
                {
                    var vectorToVehicle = vehicle.Position - emergencyVehicle.Position;
                    var forwardDist = Vector3.Dot(emergencyVehicle.ForwardVector, vectorToVehicle);
                    if (forwardDist > 2f && forwardDist < Config.IntersectionSearchMaxDistance)
                    {
                        var driver = vehicle.Driver;
                        var lateralOffset = Vector3.Dot(vectorToVehicle, emergencyVehicle.RightVector);
                        var sidePushVector = lateralOffset > 0 ? emergencyVehicle.RightVector : -emergencyVehicle.RightVector;
                        var targetPos = vehicle.Position + vehicle.ForwardVector * Config.IntersectionCreepForwardDistance + sidePushVector * Config.IntersectionCreepSideDistance;

                        // Make sure the target position is on the ground and the path is clear.
                        var groundZ = World.GetGroundZ(targetPos, false, false);
                        var finalTargetPos = groundZ.HasValue ? new Vector3(targetPos.X, targetPos.Y, groundZ.Value) : targetPos;
                        var pathTrace = World.TraceLine(vehicle.Position, finalTargetPos, TraceFlags.IntersectWorld, vehicle);
                        if (pathTrace.Hit) continue;

                        // Assign the creep task.
                        driver.Tasks.Clear();
                        driver.Tasks.DriveToPosition(finalTargetPos, Config.IntersectionCreepDriveSpeed, VehicleDrivingFlags.Emergency | VehicleDrivingFlags.StopAtDestination);

                        var creepTask = new CreepTask { TargetPosition = finalTargetPos, GameTimeStarted = Game.GameTime };
                        PluginState.IntersectionCreepTaskedVehicles.Add(vehicle, creepTask);

                        if (!PluginState.TaskedVehicleBlips.ContainsKey(vehicle))
                        {
                            var blip = vehicle.AttachBlip();
                            blip.Color = Color.Purple;
                            PluginState.TaskedVehicleBlips.Add(vehicle, blip);
                        }

                        // Skip to the next vehicle since this one has been tasked.
                        continue;
                    }
                }

                // Check for "stop" candidates: cross-traffic that needs to be stopped.
                var isPotentialTarget = false;
                if (PluginState.IsStopSignIntersection)
                {
                    // For stop signs, stop any vehicle not going our direction.
                    if (headingDot < 0.7f) isPotentialTarget = true;
                }
                else
                {
                    // For traffic lights, only stop vehicles that are nearly perpendicular.
                    if (Math.Abs(headingDot) < Config.CrossTrafficHeadingDotThreshold) isPotentialTarget = true;
                }

                if (!isPotentialTarget) continue;
                var distanceToCenter = vehicle.Position.DistanceTo(intersectionCenter);
                var shouldStop = false;
                if (PluginState.IsStopSignIntersection)
                {
                    if (distanceToCenter < 35f) shouldStop = true;
                }
                else
                {
                    // For traffic lights, also ensure the vehicle is heading towards the intersection.
                    var vectorToIntersection = intersectionCenter - vehicle.Position;
                    var dotVehToCenter = Vector3.Dot(vehicle.ForwardVector, vectorToIntersection);
                    if (dotVehToCenter > 0 && distanceToCenter < 35f) shouldStop = true;
                }

                if (!shouldStop) continue;

                // Assign the stop task.
                var vehicleDriver = vehicle.Driver;
                vehicleDriver.Tasks.Clear();
                vehicleDriver.Tasks.PerformDrivingManeuver(vehicle, VehicleManeuver.GoForwardStraightBraking, 2000);
                PluginState.IntersectionTaskedVehicles.Add(vehicle);

                if (PluginState.TaskedVehicleBlips.ContainsKey(vehicle)) continue;
                {
                    var blip = vehicle.AttachBlip();
                    blip.Color = Color.Blue;
                    PluginState.TaskedVehicleBlips.Add(vehicle, blip);
                }
            }
        }
    }
}