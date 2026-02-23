using System;
using System.Collections.Generic;
using System.Drawing;
using LSPD_First_Response.Mod.API;
using Rage;

namespace MTFOv4
{
    public static class TrafficYieldController
    {
        private const float LogicSwitchSpeedThreshold = 2.5f;
        private static GameFiber _processFiber;
        private static bool _isRunning;
        private static RoadPosition _playerRoadPosition;
        private static uint _nextCandidateScanTime;
        private static uint _nextRoadCheckTime;
        private static bool _isCustomYieldingActive;

        /// <summary>
        /// Initializes the yield controller and starts the background fiber that processes traffic logic.
        /// </summary>
        public static void Start()
        {
            if (_isRunning) return;
            _isRunning = true;
            _isCustomYieldingActive = false;
            _nextRoadCheckTime = 0;
            _processFiber = GameFiber.StartNew(ProcessLoop);
        }

        /// <summary>
        /// Shuts down the controller, resets the player's native yield behavior, and clears all active AI tasks.
        /// </summary>
        public static void Stop()
        {
            _isRunning = false;
            try
            {
                var player = Game.LocalPlayer.Character;
                if (player != null && player.Exists() && player.IsInAnyVehicle(false))
                {
                    var veh = player.CurrentVehicle;
                    if (veh != null && veh.Exists()) veh.ShouldVehiclesYieldToThisVehicle = true;
                }
            }
            catch { }

            ClearYieldTasks();
            AroundPlayerHandler.ClearAll();
            IntersectionHandler.ClearAll();
            OncomingTrafficHandler.ClearAll();
        }

        /// <summary>
        /// Helper to check if the player is currently involved in a traffic stop.
        /// </summary>
        /// <returns>True if a pullover is active.</returns>
        private static bool IsPulloverActive()
        {
            return Functions.IsPlayerPerformingPullover() || Functions.GetCurrentPullover() != null;
        }

        /// <summary>
        /// The persistent background loop that determines which logic (Intersection, Oncoming, or Yielding) should be active.
        /// </summary>
        private static void ProcessLoop()
        {
            while (_isRunning)
            {
                GameFiber.Yield();
                try
                {
                    var playerPed = Game.LocalPlayer.Character;
                    if (playerPed == null || !playerPed.Exists())
                    {
                        ClearAllLogic();
                        continue;
                    }

                    bool isPedInVehicle = playerPed.IsInAnyVehicle(false);
                    Vehicle activeVehicle = isPedInVehicle ? playerPed.CurrentVehicle : playerPed.LastVehicle;

                    if (activeVehicle == null || !activeVehicle.Exists())
                    {
                        ClearAllLogic();
                        continue;
                    }

                    if (_playerRoadPosition == null) _playerRoadPosition = new RoadPosition(activeVehicle);
                    _playerRoadPosition.SetTarget(activeVehicle);
                    _playerRoadPosition.Process();

                    if (Game.GameTime > _nextRoadCheckTime)
                    {
                        _isCustomYieldingActive = _playerRoadPosition != null && !_playerRoadPosition.DebugRoadType.Contains("STREET");
                        _nextRoadCheckTime = Game.GameTime + 1000;
                    }

                    if (EntryPoint.Settings.ShowDebugLines)
                    {
                        if (_isCustomYieldingActive)
                        {
                            DrawNearbyNodes(activeVehicle);
                        }
                        DisplayDebugSubtitle(activeVehicle);
                    }

                    if (Functions.IsPedInPursuit(playerPed))
                    {
                        if (!activeVehicle.ShouldVehiclesYieldToThisVehicle)
                            activeVehicle.ShouldVehiclesYieldToThisVehicle = true;

                        ClearAllLogic();
                        continue;
                    }

                    if (IsPulloverActive())
                    {
                        if (!activeVehicle.ShouldVehiclesYieldToThisVehicle)
                            activeVehicle.ShouldVehiclesYieldToThisVehicle = true;

                        ClearAllLogic();
                        continue;
                    }

                    var sirensActive = activeVehicle.HasSiren && activeVehicle.IsSirenOn;

                    if (sirensActive)
                    {
                        if (_isCustomYieldingActive)
                        {
                            if (activeVehicle.ShouldVehiclesYieldToThisVehicle)
                                activeVehicle.ShouldVehiclesYieldToThisVehicle = false;
                        }
                        else
                        {
                            if (!activeVehicle.ShouldVehiclesYieldToThisVehicle)
                                activeVehicle.ShouldVehiclesYieldToThisVehicle = true;
                        }

                        if (isPedInVehicle)
                        {
                            IntersectionHandler.Process(activeVehicle);
                            OncomingTrafficHandler.Process(activeVehicle);

                            if (activeVehicle.Speed > LogicSwitchSpeedThreshold)
                            {
                                AroundPlayerHandler.ClearAll();
                                ProcessMovingLogic(activeVehicle);
                            }
                            else
                            {
                                ClearYieldTasks();
                                AroundPlayerHandler.Process(activeVehicle);
                            }
                        }
                        else
                        {
                            ClearYieldTasks();
                            IntersectionHandler.ClearAll();
                            OncomingTrafficHandler.ClearAll();

                            if (EntryPoint.Settings.AroundPlayerLogicOnlyInVehicle)
                            {
                                AroundPlayerHandler.ClearAll();
                            }
                            else
                            {
                                AroundPlayerHandler.Process(activeVehicle);
                            }
                        }
                    }
                    else
                    {
                        if (!activeVehicle.ShouldVehiclesYieldToThisVehicle)
                            activeVehicle.ShouldVehiclesYieldToThisVehicle = true;

                        ClearAllLogic();
                    }
                }
                catch (Exception ex)
                {
                    Game.LogTrivial($"MTFO Manager Error: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Draws the on-screen debug text showing road data, lane info, and the status of the closest tasked vehicle.
        /// </summary>
        /// <param name="playerVehicle">The player's vehicle used for distance calculations in the debug view.</param>
        private static void DisplayDebugSubtitle(Vehicle playerVehicle)
        {
            if (playerVehicle == null || !playerVehicle.Exists()) return;

            var roadType = _playerRoadPosition?.DebugRoadType ?? "N/A";
            var pLane = _playerRoadPosition?.CurrentLane ?? 0;
            var pTotal = _playerRoadPosition?.TotalLanes ?? 0;
            var yieldStatus = _isCustomYieldingActive ? "~g~ON~s~" : "~r~OFF~s~";

            var pStr = $"~y~[{roadType}]~s~ Ln:{pLane}/{pTotal} | Custom Yield: {yieldStatus}";

            var taskCount = PluginState.ActiveYieldTaskers.Count;
            var vehStr = "";

            if (taskCount > 0)
            {
                YieldTasker closestTasker = null;
                var closestDist = 9999f;

                foreach (var tasker in PluginState.ActiveYieldTaskers.Values)
                {
                    if (tasker == null || !tasker.IsValid()) continue;
                    var d = tasker.Vehicle.DistanceTo(playerVehicle);
                    if (d < closestDist)
                    {
                        closestDist = d;
                        closestTasker = tasker;
                    }
                }

                if (closestTasker != null && closestTasker.IsValid())
                    vehStr = $" | ~b~Veh[{closestTasker.Vehicle.Model.Name}]: {closestTasker.CurrentAction} | Ln {closestTasker.CurrentLane}/{closestTasker.TotalLanes}~s~";
            }

            Game.DisplaySubtitle($"{pStr} | Tasks: {taskCount}{vehStr}", 100);
        }

        /// <summary>
        /// Visualizes road nodes, headings, and boundaries on the map for debugging road detection.
        /// </summary>
        /// <param name="playerVehicle">The player's vehicle used to center the node scan.</param>
        private static void DrawNearbyNodes(Vehicle playerVehicle)
        {
            var center = playerVehicle.Position;

            for (var i = 1; i <= 30; i++)
                if (RoadPosition.GetNearestNode(center, RoadPosition.NodeFlags.INCLUDE_SWITCHED_OFF_NODES, i, out var nodePos, out var heading, out var lanes))
                {
                    if (nodePos.DistanceTo(center) > 80f) continue;

                    var boundaryColor = Color.Purple;

                    var nodeDir = MathHelper.ConvertHeadingToDirection(heading);
                    if (Vector3.Dot(nodeDir, playerVehicle.ForwardVector) < -0.5f)
                    {
                        boundaryColor = Color.Orange;
                    }
                    else if (RoadPosition.GetNodeProperties(nodePos, out var props))
                    {
                        var isIntersection = props.HasFlag(NodeProperties.junction) || props.HasFlag(NodeProperties.traffic_light) || props.HasFlag(NodeProperties.stop_sign);
                        var isDeadEnd = props.HasFlag(NodeProperties.dead_end);

                        if (isIntersection || isDeadEnd) boundaryColor = Color.Red;
                    }

                    Debug.DrawLine(nodePos, nodePos + Vector3.WorldUp * 1.5f, Color.Magenta);

                    Debug.DrawArrow(nodePos + Vector3.WorldUp, nodeDir, Rotator.Zero, 1.0f, Color.Yellow);

                    if (RoadUtilities.GetRoadBoundary(nodePos, heading, out var boundaryPos))
                    {
                        Debug.DrawLine(nodePos, boundaryPos, Color.FromArgb(100, 255, 255, 255));

                        Debug.DrawLine(boundaryPos, boundaryPos + Vector3.WorldUp * 3.0f, boundaryColor);
                    }
                }
        }

        /// <summary>
        /// A "kill-switch" for all custom logic, used to immediately stop all background AI behaviors.
        /// </summary>
        private static void ClearAllLogic()
        {
            ClearYieldTasks();
            AroundPlayerHandler.ClearAll();
            IntersectionHandler.ClearAll();
            OncomingTrafficHandler.ClearAll();
        }

        /// <summary>
        /// High-level logic for handling vehicles while the player is moving, primarily focusing on lane yielding.
        /// </summary>
        /// <param name="playerVehicle">The player's vehicle used for scanning and lane comparison.</param>
        private static void ProcessMovingLogic(Vehicle playerVehicle)
        {
            if (playerVehicle == null || !playerVehicle.Exists()) return;

            if (!_isCustomYieldingActive)
            {
                ClearYieldTasks();
                return;
            }

            if (Game.GameTime > _nextCandidateScanTime)
            {
                ScanForNewCandidates(playerVehicle);
                _nextCandidateScanTime = Game.GameTime + 750;
            }

            ProcessActiveTaskers(playerVehicle);
        }

        /// <summary>
        /// Iterates through all vehicles currently pulling over or changing lanes and updates their individual logic.
        /// </summary>
        /// <param name="playerVehicle">The player's vehicle to check against for task completion.</param>
        private static void ProcessActiveTaskers(Vehicle playerVehicle)
        {
            var taskers = PluginState.ActiveYieldTaskers;
            var keysToRemove = new List<Vehicle>();

            var playerLane = _playerRoadPosition?.CurrentLane ?? 0;

            foreach (var kvp in taskers)
            {
                var vehicle = kvp.Key;
                var tasker = kvp.Value;

                if (!tasker.IsValid() || tasker.IsFinished)
                {
                    tasker.ReleaseControl();
                    keysToRemove.Add(vehicle);
                    continue;
                }

                tasker.Process(playerVehicle, playerLane);
                tasker.DrawDebug();
            }

            foreach (var veh in keysToRemove) taskers.Remove(veh);
        }

        /// <summary>
        /// Validates if a nearby AI vehicle is a suitable candidate to be told to yield.
        /// </summary>
        /// <param name="player">The player's vehicle.</param>
        /// <param name="target">The AI vehicle being evaluated.</param>
        /// <returns>True if the AI is in a position where it should yield to the player.</returns>
        private static bool IsValidCandidate(Vehicle player, Vehicle target)
        {
            if (target == null || !target.Exists() || target.IsDead || target.IsSirenOn) return false;
            if (target.Driver == null || !target.Driver.Exists() || target.Driver.IsPlayer) return false;

            if (Functions.IsPedInPursuit(target.Driver)) return false;

            if (PluginState.IntersectionTaskedVehicles.Contains(target)) return false;
            if (PluginState.AroundPlayerTaskedVehicles.ContainsKey(target)) return false;
            if (PluginState.OncomingBrakingVehicles.ContainsKey(target)) return false;

            if (IsPulloverActive())
            {
                var suspect = Functions.GetPulloverSuspect(Functions.GetCurrentPullover());
                if (suspect == target.Driver) return false;
            }

            if (player.DistanceTo(target) > 60f) return false;

            // filter out ai facing wrong way
            if (Vector3.Dot(player.ForwardVector, target.ForwardVector) < 0.5f) return false;

            var toTarget = target.Position - player.Position;

            // get lateral dist to see if they are in the way
            var lateralDist = Math.Abs(Vector3.Dot(toTarget, player.RightVector));
            if (lateralDist > 12.0f) return false;

            if (_playerRoadPosition != null)
            {
                var targetRoadPos = new RoadPosition(target);
                targetRoadPos.Process();
                if (targetRoadPos.CurrentLane > 0 && _playerRoadPosition.CurrentLane > 0 && targetRoadPos.CurrentLane != _playerRoadPosition.CurrentLane) return false;
            }

            return true;
        }

        /// <summary>
        /// Releases control of all currently tasked yield vehicles, returning them to normal AI driving.
        /// </summary>
        private static void ClearYieldTasks()
        {
            foreach (var tasker in PluginState.ActiveYieldTaskers.Values) tasker.ReleaseControl();
            PluginState.ActiveYieldTaskers.Clear();
        }

        /// <summary>
        /// Performs a search for vehicles ahead of the player that should be added to the yielding system.
        /// </summary>
        /// <param name="playerVehicle">The player's vehicle used to determine search distance based on speed.</param>
        private static void ScanForNewCandidates(Vehicle playerVehicle)
        {
            if (playerVehicle == null || !playerVehicle.Exists()) return;
            if (PluginState.ActiveYieldTaskers.Count >= 5) return;

            var speed = playerVehicle.Speed;

            // scale search dist based on player speed
            var searchDist = Math.Min(50f, Math.Max(30f, speed * 2.5f));

            var centerPos = playerVehicle.Position + playerVehicle.ForwardVector * (searchDist * 0.5f);
            var nearbyEntities = World.GetEntities(centerPos, searchDist, GetEntitiesFlags.ConsiderAllVehicles);

            if (nearbyEntities == null || nearbyEntities.Length == 0) return;

            for (int i = 0; i < nearbyEntities.Length; i++)
            {
                Vehicle veh = nearbyEntities[i] as Vehicle;
                if (veh == null || !veh.Exists() || veh.Handle == playerVehicle.Handle) continue;
                if (PluginState.ActiveYieldTaskers.ContainsKey(veh)) continue;

                if (IsValidCandidate(playerVehicle, veh))
                    PluginState.ActiveYieldTaskers.Add(veh, new YieldTasker(veh));
            }
        }
    }
}