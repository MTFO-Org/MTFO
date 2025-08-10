using System.Linq;
using MTFO.Handlers;
using MTFO.Misc;
using Rage;
using Rage.Attributes;

[assembly: Plugin("MTFO", Description = "Enhances emergency vehicle realism by making traffic yield and intersections clear with optional traffic light control", Author = "Guess1m, Rohan")]

namespace MTFO
{
    public static class Entry
    {
        // Entry point for the plugin
        public static void Main()
        {
            // Creates and starts the main game fiber where our logic will run
            PluginState.PluginFiber = new GameFiber(PluginLogic);
            PluginState.PluginFiber.Start();
            // Subscribes our drawing method to the game's rendering event
            if (Config.ShowDebugLines) Game.FrameRender += DebugDisplay.OnFrameRender;
            Game.DisplayNotification("MTFO by Guess1m/Rohan loaded successfully.");
        }

        // Cleanup logic that runs when the plugin is unloaded
        public static void OnUnload(bool unloading)
        {
            // Unsubscribe from the rendering event to prevent errors
            Game.FrameRender -= DebugDisplay.OnFrameRender;
            // Run the master cleanup function to clear all tasks and blips
            ClearAllTrackedVehicles();
        }

        // The main logic loop that runs continuously in the background
        private static void PluginLogic()
        {
            while (true)
            {
                // Yield to the game engine to prevent freezing
                GameFiber.Yield();
                var playerVehicle = Game.LocalPlayer.Character.CurrentVehicle;

                // Check if player is in a vehicle with a siren (i.e., an emergency vehicle).
                if (playerVehicle.Exists() && playerVehicle.HasSiren)
                {
                    // If the siren is on, activate the plugin's logic.
                    if (playerVehicle.IsSirenOn)
                    {
                        // If player is stopped, start a timer. If moving, reset it.
                        if (playerVehicle.Speed < 0.1f)
                        {
                            if (PluginState.TimePlayerStopped == 0) PluginState.TimePlayerStopped = Game.GameTime;
                        }
                        else
                        {
                            PluginState.TimePlayerStopped = 0;
                        }

                        // If the timer has been running for more than the timeout, clear any tasks and pause the logic.
                        if (PluginState.TimePlayerStopped != 0 && Game.GameTime - PluginState.TimePlayerStopped > Config.StoppedPlayerTimeoutMs)
                        {
                            if (PluginState.TaskedVehicles.Any() || PluginState.IntersectionTaskedVehicles.Any() || PluginState.IntersectionCreepTaskedVehicles.Any())
                                ClearAllTrackedVehicles();

                            continue; // Skip the main logic handlers until the player moves again
                        }

                        if (!PluginState.IsSilentModeActive)
                        {
                            // Disable the game's default yielding behavior so ours can take over.
                            playerVehicle.ShouldVehiclesYieldToThisVehicle = false;
                            PluginState.IsSilentModeActive = true;
                        }

                        // Run the main logic handlers.
                        YieldingHandler.Process(playerVehicle);
                        IntersectionHandler.Process(playerVehicle);
                    }
                    // If the siren is off, deactivate the plugin and clean up.
                    else
                    {
                        if (!PluginState.IsSilentModeActive) continue;
                        // Restore the game's default yielding behavior.
                        playerVehicle.ShouldVehiclesYieldToThisVehicle = true;
                        PluginState.IsSilentModeActive = false;
                        PluginState.TimePlayerStopped = 0; // Reset stopped timer
                        ClearAllTrackedVehicles();
                    }
                }
                // If the player is not in an emergency vehicle, perform cleanup as a failsafe.
                else
                {
                    if (!PluginState.IsSilentModeActive) continue;
                    PluginState.IsSilentModeActive = false;
                    PluginState.TimePlayerStopped = 0; // Reset stopped timer
                    ClearAllTrackedVehicles();
                }
            }
        }

        // A utility function to clear all tasks, blips, and state.
        public static void ClearAllTrackedVehicles()
        {
            // Delete all blips from the map.
            foreach (var blip in PluginState.TaskedVehicleBlips.Values.Where(blip => blip.Exists()))
                blip.Delete();

            PluginState.TaskedVehicleBlips.Clear();

            // Get a combined list of all vehicles we are currently managing.
            var allTrackedVehicles = PluginState.TaskedVehicles.Keys.Concat(PluginState.IntersectionTaskedVehicles).Concat(PluginState.IntersectionCreepTaskedVehicles.Keys).Distinct();
            // Clear the AI tasks for every managed vehicle.
            foreach (var vehicle in allTrackedVehicles.Where(v => v.Exists() && v.Driver.Exists()))
                vehicle.Driver.Tasks.Clear();

            // Clear our internal tracking lists.
            PluginState.TaskedVehicles.Clear();
            PluginState.IntersectionTaskedVehicles.Clear();
            PluginState.IntersectionCreepTaskedVehicles.Clear();

            // Reset the intersection state.
            PluginState.ActiveIntersectionCenter = null;
            PluginState.IsStopSignIntersection = false;
        }
    }
}