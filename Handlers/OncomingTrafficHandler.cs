using System.Collections.Generic;
using LSPD_First_Response.Mod.API;
using Rage;

namespace MTFOv4
{
    internal static class OncomingTrafficHandler
    {
        private static uint _nextOncomingScanTime;

        /// <summary>
        /// The main loop that monitors oncoming traffic and manages vehicles already tasked to brake.
        /// </summary>
        /// <param name="emergencyVehicle">The player's vehicle used to find oncoming targets.</param>
        public static void Process(Vehicle emergencyVehicle)
        {
            if (!EntryPoint.Settings.EnableOncomingBraking) return;

            ManageExistingBrakingVehicles(emergencyVehicle);

            if (Game.GameTime > _nextOncomingScanTime)
            {
                ScanForNewOncoming(emergencyVehicle);
                _nextOncomingScanTime = Game.GameTime + 750;
            }
        }

        /// <summary>
        /// Resets the handler by clearing tasks for all oncoming vehicles and emptying the tracking list.
        /// </summary>
        public static void ClearAll()
        {
            var keys = new List<Vehicle>(PluginState.OncomingBrakingVehicles.Keys);
            foreach (var veh in keys)
            {
                if (veh != null && veh.Exists() && veh.Driver != null && veh.Driver.Exists())
                {
                    veh.Driver.Tasks.Clear();
                }
            }

            PluginState.OncomingBrakingVehicles.Clear();
        }

        /// <summary>
        /// Checks vehicles currently forced to brake to see if they should be released based on distance, time, or positioning.
        /// </summary>
        /// <param name="emergencyVehicle">The player's vehicle used to check relative distance and passing.</param>
        private static void ManageExistingBrakingVehicles(Vehicle emergencyVehicle)
        {
            var vehiclesToUntask = new List<Vehicle>();

            foreach (var entry in PluginState.OncomingBrakingVehicles)
            {
                var vehicle = entry.Key;
                var timeTasked = entry.Value;

                if (!vehicle.Exists())
                {
                    vehiclesToUntask.Add(vehicle);
                    continue;
                }

                var isTimedOut = Game.GameTime - timeTasked > EntryPoint.Settings.OncomingBrakeDurationMs;
                var isTooFar = vehicle.Position.DistanceTo(emergencyVehicle.Position) > EntryPoint.Settings.DetectionRange + 30f;

                // negative dot product means they drove past player
                var passedPlayer = Vector3.Dot(emergencyVehicle.ForwardVector, vehicle.Position - emergencyVehicle.Position) < -10f;

                if (isTimedOut || isTooFar || passedPlayer) vehiclesToUntask.Add(vehicle);
            }

            foreach (var v in vehiclesToUntask)
            {
                if (v.Exists() && v.Driver.Exists())

                    v.Driver.Tasks.CruiseWithVehicle(v, 20f, VehicleDrivingFlags.Normal);

                PluginState.OncomingBrakingVehicles.Remove(v);
            }
        }

        /// <summary>
        /// Looks for new oncoming vehicles ahead of the player that are in the way and tells them to stop.
        /// </summary>
        /// <param name="emergencyVehicle">The player's vehicle acting as the origin for the search scan.</param>
        private static void ScanForNewOncoming(Vehicle emergencyVehicle)
        {
            if (emergencyVehicle == null || !emergencyVehicle.Exists()) return;

            var searchPos = emergencyVehicle.Position + emergencyVehicle.ForwardVector * (EntryPoint.Settings.DetectionRange * 0.75f);
            var nearbyEntities = World.GetEntities(searchPos, EntryPoint.Settings.DetectionRange, GetEntitiesFlags.ConsiderAllVehicles);

            if (nearbyEntities == null || nearbyEntities.Length == 0) return;

            for (int i = 0; i < nearbyEntities.Length; i++)
            {
                Vehicle vehicle = nearbyEntities[i] as Vehicle;
                if (vehicle == null || !vehicle.Exists() || !vehicle.IsAlive || vehicle.Driver == null || !vehicle.Driver.Exists()) continue;
                if (vehicle.Handle == emergencyVehicle.Handle) continue;
                if (vehicle.IsPoliceVehicle || vehicle.Model.IsEmergencyVehicle) continue;
                if (Misc.IsDriverInPursuit(vehicle.Driver)) continue;
                if (Functions.IsPlayerPerformingPullover() && Functions.GetPulloverSuspect(Functions.GetCurrentPullover()) == vehicle.Driver) continue;

                if (PluginState.OncomingBrakingVehicles.ContainsKey(vehicle) || PluginState.ActiveYieldTaskers.ContainsKey(vehicle) || PluginState.IntersectionTaskedVehicles.Contains(vehicle)) continue;

                // check if they are driving towards player
                var headingDot = Vector3.Dot(emergencyVehicle.ForwardVector, vehicle.ForwardVector);
                if (headingDot >= EntryPoint.Settings.OncomingBrakeHeadingDot) continue;

                var vectorToTarget = vehicle.Position - emergencyVehicle.Position;

                // calculate how far left or right they are
                var lateralOffset = Vector3.Dot(vectorToTarget, emergencyVehicle.RightVector);

                if (lateralOffset < EntryPoint.Settings.OncomingBrakeMinLateral || lateralOffset > EntryPoint.Settings.OncomingBrakeMaxLateral) continue;

                vehicle.Driver.Tasks.PerformDrivingManeuver(vehicle, VehicleManeuver.Wait, EntryPoint.Settings.OncomingBrakeDurationMs);
                PluginState.OncomingBrakingVehicles.Add(vehicle, Game.GameTime);
            }
        }
    }
}