using System.Collections.Generic;
using System.Linq;
using INIUtility;
using LSPD_First_Response.Mod.API;
using MTFO.Handlers;
using MTFO.Misc;
using Rage;

namespace MTFO
{
    public class EntryPoint : Plugin
    {
        private const string Version = "v3.0.1.0";
        private static MtfoSettings Settings { get; set; }

        public override void Initialize()
        {
            Functions.OnOnDutyStateChanged += LSPDFRFunctions_OnOnDutyStateChanged;
        }

        private void LSPDFRFunctions_OnOnDutyStateChanged(bool onduty)
        {
            ClearAllTrackedVehicles();
            if (!onduty) return;
            Settings = ConfigLoader.LoadSettings<MtfoSettings>("plugins/LSPDFR/MTFO.ini");
            Main();
        }

        public override void Finally()
        {
            if (MtfoSettings.ShowDebugLines) Game.FrameRender -= DebugDisplay.OnFrameRender;
            ClearAllTrackedVehicles();

            if (PluginState.PluginFiber.IsAlive) PluginState.PluginFiber.Abort();
        }

        private static void Main()
        {
            PluginState.PluginFiber = new GameFiber(PluginLogic);
            PluginState.PluginFiber.Start();
            if (MtfoSettings.ShowDebugLines) Game.FrameRender += DebugDisplay.OnFrameRender;
            Game.DisplayNotification("web_lossantospolicedept", "web_lossantospolicedept", "~w~MTFO", "~w~By: ~y~Guess1m~w~/~y~Rohan", "~w~Version: ~y~" + Version + " ~g~Loaded Successfully!");
        }

        private static void PluginLogic()
        {
            while (true)
            {
                GameFiber.Yield();
                var playerCharacter = Game.LocalPlayer.Character;
                var currentVehicle = playerCharacter.CurrentVehicle;

                if (currentVehicle.Exists() && currentVehicle.HasSiren) PluginState.LastPlayerVehicle = currentVehicle;

                var sirenVehicle = PluginState.LastPlayerVehicle;

                if (sirenVehicle.Exists() && sirenVehicle.HasSiren && sirenVehicle.IsSirenOn)
                {
                    AroundPlayerHandler.Process(sirenVehicle);

                    if (!currentVehicle.Exists() || currentVehicle != sirenVehicle) continue;

                    sirenVehicle.ShouldVehiclesYieldToThisVehicle = false;
                    if (!PluginState.IsSilentModeActive) PluginState.IsSilentModeActive = true;

                    var isPlayerStopped = sirenVehicle.Speed < 0.1f;

                    if (isPlayerStopped)
                    {
                        if (PluginState.TimePlayerStopped == 0) PluginState.TimePlayerStopped = Game.GameTime;
                    }
                    else
                    {
                        PluginState.TimePlayerStopped = 0;
                    }

                    var isTimedOutForYielding = PluginState.TimePlayerStopped != 0 && Game.GameTime - PluginState.TimePlayerStopped > MtfoSettings.StoppedPlayerTimeoutMs;

                    if (isTimedOutForYielding)
                    {
                        if (PluginState.TaskedVehicles.Any() || PluginState.IntersectionTaskedVehicles.Any() || PluginState.IntersectionCreepTaskedVehicles.Any())
                            ClearYieldAndIntersectionTasks();
                    }
                    else
                    {
                        IntersectionHandler.Process(sirenVehicle);
                        YieldingHandler.Process(sirenVehicle);
                    }
                }
                else
                {
                    if (!PluginState.IsSilentModeActive) continue;
                    if (sirenVehicle.Exists()) sirenVehicle.ShouldVehiclesYieldToThisVehicle = true;
                    PluginState.IsSilentModeActive = false;
                    PluginState.TimePlayerStopped = 0;
                    ClearAllTrackedVehicles();
                }
            }
        }

        private static void ClearYieldAndIntersectionTasks()
        {
            var vehiclesToUntask = new HashSet<Vehicle>();
            vehiclesToUntask.UnionWith(PluginState.TaskedVehicles.Keys);
            vehiclesToUntask.UnionWith(PluginState.IntersectionTaskedVehicles);
            vehiclesToUntask.UnionWith(PluginState.IntersectionCreepTaskedVehicles.Keys);
            vehiclesToUntask.UnionWith(PluginState.OncomingBrakingVehicles.Keys);

            foreach (var vehicle in vehiclesToUntask)
            {
                if (!vehicle.Exists()) continue;

                if (vehicle.Driver.Exists()) vehicle.Driver.Tasks.Clear();

                if (!PluginState.TaskedVehicleBlips.TryGetValue(vehicle, out var blip)) continue;
                if (blip.Exists()) blip.Delete();
                PluginState.TaskedVehicleBlips.Remove(vehicle);
            }

            PluginState.TaskedVehicles.Clear();
            PluginState.IntersectionTaskedVehicles.Clear();
            PluginState.IntersectionCreepTaskedVehicles.Clear();
            PluginState.OncomingBrakingVehicles.Clear();
            PluginState.FailedCreepCandidates.Clear();

            PluginState.ActiveIntersectionCenter = null;
            PluginState.IsStopSignIntersection = false;
        }

        public static void ClearAllTrackedVehicles()
        {
            var allTrackedVehicles = new HashSet<Vehicle>();
            allTrackedVehicles.UnionWith(PluginState.TaskedVehicles.Keys);
            allTrackedVehicles.UnionWith(PluginState.IntersectionTaskedVehicles);
            allTrackedVehicles.UnionWith(PluginState.IntersectionCreepTaskedVehicles.Keys);
            allTrackedVehicles.UnionWith(PluginState.OncomingBrakingVehicles.Keys);
            allTrackedVehicles.UnionWith(PluginState.AroundPlayerTaskedVehicles.Keys);

            foreach (var vehicle in allTrackedVehicles)
            {
                if (!vehicle.Exists()) continue;

                if (vehicle.Driver.Exists()) vehicle.Driver.Tasks.Clear();

                if (PluginState.TaskedVehicleBlips.TryGetValue(vehicle, out var blip) && blip.Exists()) blip.Delete();
            }

            PluginState.TaskedVehicleBlips.Clear();
            PluginState.TaskedVehicles.Clear();
            PluginState.IntersectionTaskedVehicles.Clear();
            PluginState.IntersectionCreepTaskedVehicles.Clear();
            PluginState.OncomingBrakingVehicles.Clear();
            PluginState.FailedCreepCandidates.Clear();
            PluginState.AroundPlayerTaskedVehicles.Clear();
            PluginState.FailedAroundPlayerCandidates.Clear();

            PluginState.ActiveIntersectionCenter = null;
            PluginState.IsStopSignIntersection = false;
        }
    }
}