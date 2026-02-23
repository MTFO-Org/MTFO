using System;
using System.Collections.Generic;
using System.Drawing;
using LSPD_First_Response.Mod.API;
using Rage;
using Rage.Native;
using Object = Rage.Object;

namespace MTFOv4
{
    internal static class IntersectionHandler
    {
        private static uint _nextVehicleScanTime;

        /// <summary>
        /// The main logic loop for managing intersections, looking for new ones or processing traffic at an active one.
        /// </summary>
        /// <param name="emergencyVehicle">The player's emergency vehicle driving the logic.</param>
        public static void Process(Vehicle emergencyVehicle)
        {
            if (!EntryPoint.Settings.EnableIntersectionControl) return;

            if (PluginState.ActiveIntersectionCenter.HasValue)
            {
                if (CheckIfPastIntersection(emergencyVehicle)) return;

                ManageExistingIntersectionTasks();

                if (Game.GameTime > _nextVehicleScanTime)
                {
                    ProcessNearbyVehicles(emergencyVehicle);
                    _nextVehicleScanTime = Game.GameTime + 750;
                }

                if (EntryPoint.Settings.ShowDebugLines && PluginState.ActiveIntersectionCenter.HasValue)
                    Debug.DrawLine(emergencyVehicle.Position, PluginState.ActiveIntersectionCenter.Value, Color.Blue);
            }
            else
            {
                if (Game.GameTime < PluginState.NextIntersectionScanTime) return;
                DetectNewIntersection(emergencyVehicle);
                PluginState.NextIntersectionScanTime = Game.GameTime + 250;
            }
        }

        /// <summary>
        /// Resets the intersection state, clearing all tasked vehicles and removing any debug blips from the map.
        /// </summary>
        public static void ClearAll()
        {
            foreach (var blip in PluginState.TaskedVehicleBlips.Values)
            {
                if (blip != null && blip.Exists()) blip.Delete();
            }

            PluginState.TaskedVehicleBlips.Clear();

            foreach (var veh in PluginState.IntersectionTaskedVehicles)
            {
                if (veh != null && veh.Exists() && veh.Driver != null && veh.Driver.Exists())
                {
                    veh.Driver.Tasks.Clear();
                }
            }

            PluginState.IntersectionTaskedVehicles.Clear();
            PluginState.ActiveIntersectionCenter = null;
            PluginState.IsStopSignIntersection = false;
        }

        /// <summary>
        /// Cleans up the list of vehicles at an intersection if they've moved away or are part of a player stop.
        /// </summary>
        private static void ManageExistingIntersectionTasks()
        {
            if (PluginState.ActiveIntersectionCenter == null) return;
            var center = PluginState.ActiveIntersectionCenter.Value;

            Ped pulloverSuspect = null;
            if (Functions.IsPlayerPerformingPullover() || Functions.GetCurrentPullover() != null)
            {
                var pullover = Functions.GetCurrentPullover();
                if (pullover != null)
                {
                    pulloverSuspect = Functions.GetPulloverSuspect(pullover);
                }
            }

            List<Vehicle> vehiclesToRemove = new List<Vehicle>();

            foreach (var v in PluginState.IntersectionTaskedVehicles)
            {
                var isPulloverSuspect = pulloverSuspect != null && pulloverSuspect.Exists() && v != null && v.Exists() && v.Driver != null && v.Driver.Exists() && pulloverSuspect == v.Driver;

                var shouldRemove = v == null || !v.Exists() || v.Position.DistanceTo(center) > 80f || isPulloverSuspect;

                if (shouldRemove)
                {
                    if (!isPulloverSuspect && v != null && v.Exists() && v.Driver != null && v.Driver.Exists())
                    {
                        v.Driver.Tasks.Clear();
                    }

                    if (v != null && PluginState.TaskedVehicleBlips.TryGetValue(v, out var blip))
                    {
                        if (blip.Exists()) blip.Delete();
                        PluginState.TaskedVehicleBlips.Remove(v);
                    }

                    vehiclesToRemove.Add(v);
                }
            }

            for (int i = 0; i < vehiclesToRemove.Count; i++)
            {
                PluginState.IntersectionTaskedVehicles.Remove(vehiclesToRemove[i]);
            }
        }

        /// <summary>
        /// Checks if the player's vehicle has successfully passed through the intersection or moved too far away.
        /// </summary>
        /// <param name="emergencyVehicle">The player's vehicle to check against the intersection center.</param>
        /// <returns>True if the player is past the intersection.</returns>
        private static bool CheckIfPastIntersection(Vehicle emergencyVehicle)
        {
            if (PluginState.ActiveIntersectionCenter == null) return true;

            var vectorToIntersection = PluginState.ActiveIntersectionCenter.Value - emergencyVehicle.Position;

            // negative dot product means player is past center
            var dotPlayerToCenter = Vector3.Dot(emergencyVehicle.ForwardVector, vectorToIntersection);

            var isPastIntersection = dotPlayerToCenter < -10f;
            var isTooFarAway = vectorToIntersection.Length() > EntryPoint.Settings.IntersectionSearchMaxDistance + EntryPoint.Settings.IntersectionSearchRadius + 20f;

            if (!isPastIntersection && !isTooFarAway) return false;

            ClearAll();
            PluginState.IntersectionClearTime = Game.GameTime;
            return true;
        }

        /// <summary>
        /// Scans ahead of the vehicle to find the next traffic light or stop sign to take control of.
        /// </summary>
        /// <param name="emergencyVehicle">The player's vehicle used as the starting point for the scan.</param>
        private static void DetectNewIntersection(Vehicle emergencyVehicle)
        {
            if (emergencyVehicle == null || !emergencyVehicle.Exists()) return;
            if (Game.GameTime - PluginState.IntersectionClearTime < EntryPoint.Settings.IntersectionDetectionCooldownMs) return;

            Object foundObject = null;
            var allIntersectionModels = GameModels.AllIntersectionModels;

            for (var searchDistance = EntryPoint.Settings.IntersectionSearchMaxDistance; searchDistance > EntryPoint.Settings.IntersectionSearchMinDistance; searchDistance -= EntryPoint.Settings.IntersectionSearchStepSize)
            {
                var searchPosition = emergencyVehicle.Position + emergencyVehicle.ForwardVector * searchDistance;

                for (int i = 0; i < allIntersectionModels.Length; i++)
                {
                    uint modelHash = allIntersectionModels[i];
                    foundObject = NativeFunction.Natives.GET_CLOSEST_OBJECT_OF_TYPE<Object>(searchPosition, EntryPoint.Settings.IntersectionSearchRadius, modelHash, false, false, false);
                    if (foundObject == null || !foundObject.Exists()) continue;

                    // find absolute heading difference
                    var headingDiff = Math.Abs(emergencyVehicle.Heading - foundObject.Heading);
                    if (headingDiff < EntryPoint.Settings.IntersectionHeadingThreshold || headingDiff > 360 - EntryPoint.Settings.IntersectionHeadingThreshold)
                        break;

                    foundObject = null;
                }

                if (foundObject != null)
                {
                    var vectorToObject = foundObject.Position - emergencyVehicle.Position;

                    // filter out objects that are behind player
                    if (Vector3.Dot(emergencyVehicle.ForwardVector, vectorToObject) > 0)
                        break;

                    foundObject = null;
                }
            }

            if (foundObject == null) return;

            PluginState.IsStopSignIntersection = false;
            for (int i = 0; i < GameModels.StopSignModels.Length; i++)
            {
                if (GameModels.StopSignModels[i] == foundObject.Model.Hash)
                {
                    PluginState.IsStopSignIntersection = true;
                    break;
                }
            }

            if (PluginState.IsStopSignIntersection)
            {
                var rawCenter = foundObject.Position + foundObject.ForwardVector * 12f;
                var groundZ = World.GetGroundZ(rawCenter, false, false);
                PluginState.ActiveIntersectionCenter = groundZ.HasValue ? new Vector3(rawCenter.X, rawCenter.Y, groundZ.Value) : rawCenter;
            }
            else
            {
                PluginState.ActiveIntersectionCenter = foundObject.Position;
                if (EntryPoint.Settings.EnableOpticom) SetTrafficLightGreen(foundObject);
            }
        }

        /// <summary>
        /// Scans for AI vehicles entering the intersection and forces them to stop so the player has a clear path.
        /// </summary>
        /// <param name="emergencyVehicle">The player's vehicle used to determine relevant cross-traffic.</param>
        private static void ProcessNearbyVehicles(Vehicle emergencyVehicle)
        {
            if (PluginState.ActiveIntersectionCenter == null || emergencyVehicle == null || !emergencyVehicle.Exists()) return;
            var intersectionCenter = PluginState.ActiveIntersectionCenter.Value;

            var nearbyEntities = World.GetEntities(intersectionCenter, 60f, GetEntitiesFlags.ConsiderAllVehicles | GetEntitiesFlags.ExcludePlayerVehicle);
            if (nearbyEntities == null || nearbyEntities.Length == 0) return;

            for (int i = 0; i < nearbyEntities.Length; i++)
            {
                Vehicle vehicle = nearbyEntities[i] as Vehicle;
                if (vehicle == null || !vehicle.Exists() || !vehicle.IsAlive || vehicle.Driver == null || !vehicle.Exists()) continue;
                if (vehicle.IsPoliceVehicle || vehicle.Model.IsEmergencyVehicle) continue;
                if (Functions.IsPlayerPerformingPullover() && Functions.GetPulloverSuspect(Functions.GetCurrentPullover()) == vehicle.Driver) continue;
                if (Misc.IsDriverInPursuit(vehicle.Driver)) continue;

                if (PluginState.ActiveYieldTaskers.ContainsKey(vehicle) || PluginState.IntersectionTaskedVehicles.Contains(vehicle) || PluginState.AroundPlayerTaskedVehicles.ContainsKey(vehicle) || PluginState.OncomingBrakingVehicles.ContainsKey(vehicle))
                    continue;

                // check if they are perpendicular or facing player
                var headingDot = Vector3.Dot(emergencyVehicle.ForwardVector, vehicle.ForwardVector);

                var isPotentialTarget = false;
                if (PluginState.IsStopSignIntersection)
                {
                    if (headingDot < 0.7f) isPotentialTarget = true;
                }
                else
                {
                    if (Math.Abs(headingDot) < EntryPoint.Settings.CrossTrafficHeadingDotThreshold) isPotentialTarget = true;
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
                    var vectorToIntersection = intersectionCenter - vehicle.Position;
                    var dotVehToCenter = Vector3.Dot(vehicle.ForwardVector, vectorToIntersection);

                    if (dotVehToCenter > 0 && distanceToCenter < 35f) shouldStop = true;
                }

                if (!shouldStop) continue;

                var vehicleDriver = vehicle.Driver;
                vehicleDriver.Tasks.Clear();

                vehicleDriver.Tasks.PerformDrivingManeuver(vehicle, VehicleManeuver.GoForwardStraightBraking, 5000);
                PluginState.IntersectionTaskedVehicles.Add(vehicle);

                if (EntryPoint.Settings.ShowDebugLines && !PluginState.TaskedVehicleBlips.ContainsKey(vehicle))
                {
                    var blip = vehicle.AttachBlip();
                    if (blip != null && blip.Exists())
                    {
                        blip.Color = Color.Blue;
                        PluginState.TaskedVehicleBlips.Add(vehicle, blip);
                    }
                }
            }
        }

        /// <summary>
        /// Asynchronously handles changing a traffic light to green, including optional yellow flashing for an Opticom effect.
        /// </summary>
        /// <param name="trafficLight">The specific traffic light object to manipulate.</param>
        private static void SetTrafficLightGreen(Object trafficLight)
        {
            GameFiber.StartNew(() =>
            {
                const int greenState = 0;
                const int resetState = 3;
                const int yellowState = 2;
                const int redState = 1;

                if (EntryPoint.Settings.OpticomFlashYellowFirst)
                {
                    var count = EntryPoint.Settings.OpticomFlashYellowCount;
                    for (var i = 0; i < count; i++)
                    {
                        if (!trafficLight.Exists()) break;
                        NativeFunction.Natives.SET_ENTITY_TRAFFICLIGHT_OVERRIDE(trafficLight, yellowState);
                        GameFiber.Wait(EntryPoint.Settings.OpticomFlashYellowInterval);

                        if (!trafficLight.Exists()) break;
                        NativeFunction.Natives.SET_ENTITY_TRAFFICLIGHT_OVERRIDE(trafficLight, redState);
                        GameFiber.Wait(EntryPoint.Settings.OpticomFlashYellowInterval);
                    }
                }

                if (trafficLight.Exists())
                {
                    NativeFunction.Natives.SET_ENTITY_TRAFFICLIGHT_OVERRIDE(trafficLight, greenState);
                    GameFiber.Wait(EntryPoint.Settings.OpticomGreenDurationMs);
                    if (trafficLight.Exists())
                        NativeFunction.Natives.SET_ENTITY_TRAFFICLIGHT_OVERRIDE(trafficLight, resetState);
                }
            });
        }
    }
}