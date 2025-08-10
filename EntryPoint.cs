using System.Collections.Generic;
using System.Linq;
using LSPD_First_Response.Mod.API;
using MTFO.Handlers;
using MTFO.Misc;
using Rage;

namespace MTFO
{
    public class EntryPoint : Plugin
    {
         public override void Initialize()
        {
            Functions.OnOnDutyStateChanged += LSPDFRFunctions_OnOnDutyStateChanged;
        }

        private void LSPDFRFunctions_OnOnDutyStateChanged(bool onduty)
        {
            if (onduty)
            {
                Main();
            }
        }

        public override void Finally()
        {
            Game.FrameRender -= DebugDisplay.OnFrameRender;
            ClearAllTrackedVehicles();
        }
        public static void Main()
        {
            PluginState.PluginFiber = new GameFiber(PluginLogic);
            PluginState.PluginFiber.Start();
            if (Config.ShowDebugLines) Game.FrameRender += DebugDisplay.OnFrameRender;
            Game.DisplayNotification("MTFO by Guess1m/Rohan loaded successfully.");
        }

        private static void PluginLogic()
        {
            while (true)
            {
                GameFiber.Yield();
                var playerVehicle = Game.LocalPlayer.Character.CurrentVehicle;

                if (playerVehicle.Exists() && playerVehicle.HasSiren)
                {
                    if (playerVehicle.IsSirenOn)
                    {
                        if (playerVehicle.Speed < 0.1f)
                        {
                            if (PluginState.TimePlayerStopped == 0) PluginState.TimePlayerStopped = Game.GameTime;
                        }
                        else
                        {
                            PluginState.TimePlayerStopped = 0;
                        }

                        if (PluginState.TimePlayerStopped != 0 && Game.GameTime - PluginState.TimePlayerStopped > Config.StoppedPlayerTimeoutMs)
                        {
                            if (PluginState.TaskedVehicles.Any() || PluginState.IntersectionTaskedVehicles.Any() || PluginState.IntersectionCreepTaskedVehicles.Any())
                                ClearAllTrackedVehicles();

                            continue;
                        }

                        if (!PluginState.IsSilentModeActive)
                        {
                            playerVehicle.ShouldVehiclesYieldToThisVehicle = false;
                            PluginState.IsSilentModeActive = true;
                        }

                        IntersectionHandler.Process(playerVehicle);
                        YieldingHandler.Process(playerVehicle);
                    }
                    else
                    {
                        if (!PluginState.IsSilentModeActive) continue;
                        playerVehicle.ShouldVehiclesYieldToThisVehicle = true;
                        PluginState.IsSilentModeActive = false;
                        PluginState.TimePlayerStopped = 0;
                        ClearAllTrackedVehicles();
                    }
                }
                else
                {
                    if (!PluginState.IsSilentModeActive) continue;
                    PluginState.IsSilentModeActive = false;
                    PluginState.TimePlayerStopped = 0;
                    ClearAllTrackedVehicles();
                }
            }
        }

        public static void ClearAllTrackedVehicles()
        {
            foreach (var blip in PluginState.TaskedVehicleBlips.Values.Where(blip => blip.Exists()))
                blip.Delete();

            PluginState.TaskedVehicleBlips.Clear();

            var allTrackedVehicles = new HashSet<Vehicle>();
            foreach (var v in PluginState.TaskedVehicles.Keys) allTrackedVehicles.Add(v);
            foreach (var v in PluginState.IntersectionTaskedVehicles) allTrackedVehicles.Add(v);
            foreach (var v in PluginState.IntersectionCreepTaskedVehicles.Keys) allTrackedVehicles.Add(v);
            foreach (var v in PluginState.OncomingBrakingVehicles.Keys) allTrackedVehicles.Add(v);

            foreach (var vehicle in allTrackedVehicles)
                if (vehicle.Exists() && vehicle.Driver.Exists())
                    vehicle.Driver.Tasks.Clear();

            PluginState.TaskedVehicles.Clear();
            PluginState.IntersectionTaskedVehicles.Clear();
            PluginState.IntersectionCreepTaskedVehicles.Clear();
            PluginState.OncomingBrakingVehicles.Clear();
            PluginState.FailedCreepCandidates.Clear();

            PluginState.ActiveIntersectionCenter = null;
            PluginState.IsStopSignIntersection = false;
        }
    }
}
