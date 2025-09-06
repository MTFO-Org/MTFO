using System;
using System.Drawing;
using System.Linq;
using LSPD_First_Response.Mod.API;
using MTFO.Misc;
using Rage;
using Rage.Native;
using Object = Rage.Object;

namespace MTFO.Handlers
{
    internal static class IntersectionHandler
    {
        private static void SetTrafficLightGreen(Object trafficLight)
        {
            GameFiber.StartNew(() =>
            {
                const int greenState = 0;
                const int resetState = 3;
                const int yellowState = 2;
                const int redState = 1;

                if (Config.OpticomFlashYellowFirst)
                {
                    var count = Config.OpticomFlashYellowCount;
                    for (var i = 0; i < count; i++)
                    {
                        NativeFunction.Natives.SET_ENTITY_TRAFFICLIGHT_OVERRIDE(trafficLight, yellowState);
                        GameFiber.Wait(Config.OpticomFlashYellowInterval);
                        NativeFunction.Natives.SET_ENTITY_TRAFFICLIGHT_OVERRIDE(trafficLight, redState);
                        GameFiber.Wait(Config.OpticomFlashYellowInterval);
                    }
                }

                NativeFunction.Natives.SET_ENTITY_TRAFFICLIGHT_OVERRIDE(trafficLight, greenState);
                GameFiber.Wait(Config.OpticomGreenDurationMs);
                NativeFunction.Natives.SET_ENTITY_TRAFFICLIGHT_OVERRIDE(trafficLight, resetState);
            });
        }

        public static void Process(Vehicle emergencyVehicle)
        {
            if (!Config.EnableIntersectionControl) return;

            if (PluginState.ActiveIntersectionCenter.HasValue)
            {
                if (CheckIfPastIntersection(emergencyVehicle)) return;

                ManageExistingIntersectionTasks();
            }
            else
            {
                if (Game.GameTime < PluginState.NextIntersectionScanTime) return;
                DetectNewIntersection(emergencyVehicle);
                PluginState.NextIntersectionScanTime = Game.GameTime + 250;
            }

            if (!PluginState.ActiveIntersectionCenter.HasValue) return;

            ProcessNearbyVehicles(emergencyVehicle);
        }

        private static void ManageExistingIntersectionTasks()
        {
            if (PluginState.ActiveIntersectionCenter == null) return;
            var center = PluginState.ActiveIntersectionCenter.Value;

            Ped pulloverSuspect = null;
            if (Functions.IsPlayerPerformingPullover()) pulloverSuspect = Functions.GetPulloverSuspect(Functions.GetCurrentPullover());

            PluginState.IntersectionTaskedVehicles.RemoveWhere(v =>
            {
                var isPulloverSuspect = pulloverSuspect != null && pulloverSuspect.Exists() && v.Exists() && v.Driver.Exists() && pulloverSuspect == v.Driver;

                if (isPulloverSuspect) v.Driver.Tasks.Clear();

                var shouldRemove = !v.Exists() || v.Position.DistanceTo(center) > 80f || isPulloverSuspect;
                if (!shouldRemove || !PluginState.TaskedVehicleBlips.TryGetValue(v, out var blip))
                    return shouldRemove;
                if (blip.Exists()) blip.Delete();
                PluginState.TaskedVehicleBlips.Remove(v);

                return shouldRemove;
            });
        }

        private static bool CheckIfPastIntersection(Vehicle emergencyVehicle)
        {
            if (PluginState.ActiveIntersectionCenter == null) return true;

            var vectorToIntersection = PluginState.ActiveIntersectionCenter.Value - emergencyVehicle.Position;
            var dotPlayerToCenter = Vector3.Dot(emergencyVehicle.ForwardVector, vectorToIntersection);

            var isPastIntersection = dotPlayerToCenter < -10f;
            var isTooFarAway = vectorToIntersection.Length() > Config.IntersectionSearchMaxDistance + 20f;

            if (!isPastIntersection && !isTooFarAway) return false;
            EntryPoint.ClearAllTrackedVehicles();
            PluginState.IntersectionClearTime = Game.GameTime;
            return true;
        }

        private static void DetectNewIntersection(Vehicle emergencyVehicle)
        {
            if (Game.GameTime - PluginState.IntersectionClearTime < Config.IntersectionDetectionCooldownMs) return;

            Object foundObject = null;
            var allIntersectionModels = GameModels.AllIntersectionModels;

            for (var searchDistance = Config.IntersectionSearchMaxDistance; searchDistance > Config.IntersectionSearchMinDistance; searchDistance -= Config.IntersectionSearchStepSize)
            {
                var searchPosition = emergencyVehicle.Position + emergencyVehicle.ForwardVector * searchDistance;
                foreach (var modelHash in allIntersectionModels)
                {
                    foundObject = NativeFunction.Natives.GET_CLOSEST_OBJECT_OF_TYPE<Object>(searchPosition, Config.IntersectionSearchRadius, modelHash, false, false, false);
                    if (foundObject == null) continue;

                    var isHeadingValid = false;
                    var headingDiff = Math.Abs(emergencyVehicle.Heading - foundObject.Heading);
                    if (headingDiff < Config.IntersectionHeadingThreshold || headingDiff > 360 - Config.IntersectionHeadingThreshold)
                        isHeadingValid = true;
                    if (isHeadingValid) break;
                    foundObject = null;
                }

                if (foundObject == null) continue;
                if (foundObject.Position.DistanceTo(emergencyVehicle.Position) > Config.IntersectionSearchMaxDistance + 5f)
                {
                    foundObject = null;
                    continue;
                }

                var vectorToObject = foundObject.Position - emergencyVehicle.Position;
                if (Vector3.Dot(emergencyVehicle.ForwardVector, vectorToObject) < 0)
                    foundObject = null;
                else
                    break;
            }

            if (foundObject == null) return;

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

        private static void ProcessNearbyVehicles(Vehicle emergencyVehicle)
        {
            if (PluginState.ActiveIntersectionCenter == null) return;
            var intersectionCenter = PluginState.ActiveIntersectionCenter.Value;

            if (Config.ShowDebugLines) PluginState.FailedCreepCandidates.Clear();

            var nearbyEntities = World.GetEntities(intersectionCenter, 60f, GetEntitiesFlags.ConsiderAllVehicles | GetEntitiesFlags.ExcludePlayerVehicle);

            foreach (var vehicle in nearbyEntities.OfType<Vehicle>())
            {
                if (!vehicle.Exists() || !vehicle.IsAlive || !vehicle.Driver.Exists() || Functions.IsPlayerPerformingPullover() || vehicle.IsPoliceVehicle || vehicle.Model.IsEmergencyVehicle || Utils.IsDriverInPursuit(vehicle.Driver)) continue;
                if (PluginState.TaskedVehicles.ContainsKey(vehicle) || PluginState.IntersectionTaskedVehicles.Contains(vehicle) || PluginState.IntersectionCreepTaskedVehicles.ContainsKey(vehicle)) continue;

                var headingDot = Vector3.Dot(emergencyVehicle.ForwardVector, vehicle.ForwardVector);

                var isPotentialTarget = false;
                if (PluginState.IsStopSignIntersection)
                {
                    if (headingDot < 0.7f) isPotentialTarget = true;
                }
                else
                {
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
                    var vectorToIntersection = intersectionCenter - vehicle.Position;
                    var dotVehToCenter = Vector3.Dot(vehicle.ForwardVector, vectorToIntersection);
                    if (dotVehToCenter > 0 && distanceToCenter < 35f) shouldStop = true;
                }

                if (!shouldStop) continue;

                var vehicleDriver = vehicle.Driver;
                vehicleDriver.Tasks.Clear();
                vehicleDriver.Tasks.PerformDrivingManeuver(vehicle, VehicleManeuver.GoForwardStraightBraking, 2000);
                PluginState.IntersectionTaskedVehicles.Add(vehicle);

                if (!Config.ShowDebugLines || PluginState.TaskedVehicleBlips.ContainsKey(vehicle)) continue;
                var blip = vehicle.AttachBlip();
                blip.Color = Color.Blue;
                PluginState.TaskedVehicleBlips.Add(vehicle, blip);
            }
        }
    }
}