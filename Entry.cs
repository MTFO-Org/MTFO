using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Rage;
using Rage.Attributes;
using Rage.Native;
using Debug = Rage.Debug;
using Object = Rage.Object;

[assembly:
    Plugin("MTFO",
        Description =
            "Enhances emergency vehicle realism by making traffic yield and intersections clear with optional traffic light control",
        Author = "Guess1m, Rohan")]

namespace MTFO
{
    public static class Entry
    {
        // --- Configuration ---
        private const bool ShowDebugLines = true;
        private const bool EnableOpticom = true;

        // --- Tuning Constants ---
        private const int OpticomGreenDurationMs = 5000; // How long light is forced
        private const uint StoppedPlayerTimeoutMs = 2000; // How long the player must be stopped before logic is paused.

        private const uint
            IntersectionDetectionCooldownMs =
                1300; // How long to wait to find a new intersection after clearing one. //BUG: required or blips flash

        // Forward detection zone for yield
        private const float DetectionRange = 52f;
        private const float DetectionStartWidth = 7f;
        private const float DetectionEndWidth = 6f;

        private const float
            DetectionHeightOffset = -0.5f; // How high above the player's position to center the detection box.

        private const float DetectionAreaHeight = 7.0f; // The total vertical size (thickness) of the detection box.

        // Yield params
        private const float ForwardMoveDistance = 35f;
        private const float SideMoveDistance = 6f;
        private const float ForceSideMoveDistance = 6.0f;
        private const float DriveSpeed = 15f;

        // Intersection detection params
        private const float IntersectionSearchMaxDistance = 45f;
        private const float IntersectionSearchMinDistance = 30f;
        private const float IntersectionSearchStepSize = 7.0f;
        private const float IntersectionSearchRadius = 40.0f;
        private const float IntersectionHeadingThreshold = 40.0f; // Heading check for finding signs/traf lights.
        private const float CrossTrafficHeadingDotThreshold = 0.25f; // Threshold to identify cross-traffic.

        // Intersection creep
        private const float
            MinYieldSpeedMph =
                4.0f; // Player must be going faster than this for yielding to activate, values underneath are using for creep

        private const float IntersectionCreepForwardDistance = 7.7f; // How far forward cars at intersection creep.
        private const float IntersectionCreepSideDistance = 5.2f; // How far to the side cars at intersection creep.
        private const float IntersectionCreepDriveSpeed = 10f; // How fast the cars creep.

        private const float
            CreepTaskCompletionDistance =
                2.5f; // How close a vehicle must get to its creep target to complete the task.

        private const float
            CreepTaskAbandonDistance =
                15.0f; // If a creeping vehicle gets this far from its target, assume it's driving off.

        private const uint
            CreepTaskTimeoutMs = 2000; // How long a vehicle can be in a creep task before it's cancelled.

        // --- Plugin State ---
        private static bool _isSilentModeActive; // Is the main logic running?
        private static GameFiber _pluginFiber; // Primary fiber
        private static uint _timePlayerStopped; // Timer for when the player vehicle stops.

        // Tracked vehicles during a yield
        private static readonly Dictionary<Vehicle, YieldTask>
            TaskedVehicles = new Dictionary<Vehicle, YieldTask>();

        // Tracks vehicles stopped at an intersection for cross-traffic
        private static readonly HashSet<Vehicle>
            IntersectionTaskedVehicles = new HashSet<Vehicle>();

        // Tracks vehicles told to creep at an intersection, and where they're going.
        private static readonly Dictionary<Vehicle, CreepTask>
            IntersectionCreepTaskedVehicles = new Dictionary<Vehicle, CreepTask>();

        private static Vector3? _activeIntersectionCenter; // The current intersection location
        private static bool _isStopSignIntersection; // Differentiates between stop signs and traffic lights.
        private static uint _intersectionClearTime; // Cooldown timer for intersection detection.

        // Tracks blips attached to any tasked vehicle for easy cleanup.
        private static readonly Dictionary<Vehicle, Blip>
            TaskedVehicleBlips = new Dictionary<Vehicle, Blip>();

        // Traffic light hashes
        private static readonly uint[] TrafficLightModels =
        {
            0x3e2b73a4, 0x336e5e2a, 0xd8eba922, 0xd4729f50,
            0x272244b2, 0x33986eae, 0x2323cdc5
        };

// Stop sign hashes
        private static readonly uint[] StopSignModels =
        {
            0xC76BD3AB,
            0x78F4B6BE
        };

        // Entry point for the plugin
        public static void Main()
        {
            // Creates and starts the main game fiber where our logic will run
            _pluginFiber = new GameFiber(PluginLogic);
            _pluginFiber.Start();
            // Subscribes our drawing method to the game's rendering event
            Game.FrameRender += OnFrameRender;
            Game.DisplayNotification("MTFO by Guess1m/Rohan loaded successfully.");
        }

        // Cleanup logic that runs when the plugin is unloaded
        public static void OnUnload(bool unloading)
        {
            // Unsubscribe from the rendering event to prevent errors
            Game.FrameRender -= OnFrameRender;
            // Run the master cleanup function to clear all tasks and blips
            ClearAllTrackedVehicles();
        }

        // Opticom: Forces a nearby traffic light to turn green
        private static void SetTrafficLightGreen(Object trafficLight)
        {
            // Set light green fiber
            GameFiber.StartNew(() =>
            {
                // Force Green
                NativeFunction.Natives.SET_ENTITY_TRAFFICLIGHT_OVERRIDE(trafficLight, 0);
                // Wait for the configured duration
                GameFiber.Wait(OpticomGreenDurationMs);
                // Revert the light back to its normal state
                NativeFunction.Natives.SET_ENTITY_TRAFFICLIGHT_OVERRIDE(trafficLight, 3);
            });
        }

        // Handles drawing all debug lines and shapes. This runs every frame.
        private static void OnFrameRender(object sender, GraphicsEventArgs e2)
        {
            // Only run if debug lines are enabled in the configuration
            if (!ShowDebugLines) return;
            var playerVehicle = Game.LocalPlayer.Character.CurrentVehicle;

            // Don't draw if the player isn't in a vehicle
            if (!playerVehicle.Exists()) return;

            // Draw lines for yielding vehicles, color-coded by task type.
            foreach (var entry in TaskedVehicles.Where(entry => entry.Key.Exists()))
            {
                Color lineColor;
                switch (entry.Value.TaskType)
                {
                    case YieldTaskType.MoveRight: lineColor = Color.Green; break;
                    case YieldTaskType.MoveLeft: lineColor = Color.Yellow; break;
                    case YieldTaskType.ForceMoveRight: lineColor = Color.Red; break;
                    case YieldTaskType.ForceMoveLeft: lineColor = Color.Orange; break;
                    default: lineColor = Color.White; break;
                }

                Debug.DrawLine(entry.Key.Position, entry.Value.TargetPosition, lineColor);
            }

            // Draw lines for intersection-managed vehicles.
            if (_activeIntersectionCenter.HasValue)
            {
                var center = _activeIntersectionCenter.Value;

                // Draw a blue line from stopped cross-traffic to the intersection center.
                foreach (var vehicle in IntersectionTaskedVehicles.Where(v => v.Exists()))
                    Debug.DrawLine(vehicle.Position, center, Color.Blue);

                // Draw a fuchsia line from a "creeping" vehicle to its target destination.
                // This now points to their actual target position from the CreepTask struct.
                foreach (var entry in IntersectionCreepTaskedVehicles.Where(e => e.Key.Exists()))
                    Debug.DrawLine(entry.Key.Position, entry.Value.TargetPosition, Color.Fuchsia);
            }

            // Only draw the detection area if the main logic is active
            if (!_isSilentModeActive) return;

            // Draw lines for detection for yielding vehicles.
            var pos = playerVehicle.Position;
            var forward = playerVehicle.ForwardVector;
            var right = playerVehicle.RightVector;
            var vizColor = Color.Aqua;

            // Calculate the top and bottom offsets for the 3D box
            var centerOffset = new Vector3(0, 0, DetectionHeightOffset);
            var halfHeight = new Vector3(0, 0, DetectionAreaHeight / 2.0f);

            // Calculate the 8 corners of the 3D trapezoidal prism
            var botBackLeft = pos - right * DetectionStartWidth + centerOffset - halfHeight;
            var botBackRight = pos + right * DetectionStartWidth + centerOffset - halfHeight;
            var botFrontLeft = pos + forward * DetectionRange - right * DetectionEndWidth + centerOffset - halfHeight;
            var botFrontRight = pos + forward * DetectionRange + right * DetectionEndWidth + centerOffset - halfHeight;

            var topBackLeft = pos - right * DetectionStartWidth + centerOffset + halfHeight;
            var topBackRight = pos + right * DetectionStartWidth + centerOffset + halfHeight;
            var topFrontLeft = pos + forward * DetectionRange - right * DetectionEndWidth + centerOffset + halfHeight;
            var topFrontRight = pos + forward * DetectionRange + right * DetectionEndWidth + centerOffset + halfHeight;

            // Draw bottom rectangle
            Debug.DrawLine(botBackLeft, botBackRight, vizColor);
            Debug.DrawLine(botBackRight, botFrontRight, vizColor);
            Debug.DrawLine(botFrontRight, botFrontLeft, vizColor);
            Debug.DrawLine(botFrontLeft, botBackLeft, vizColor);

            // Draw top rectangle
            Debug.DrawLine(topBackLeft, topBackRight, vizColor);
            Debug.DrawLine(topBackRight, topFrontRight, vizColor);
            Debug.DrawLine(topFrontRight, topFrontLeft, vizColor);
            Debug.DrawLine(topFrontLeft, topBackLeft, vizColor);

            // Draw vertical connectors
            Debug.DrawLine(botBackLeft, topBackLeft, vizColor);
            Debug.DrawLine(botBackRight, topBackRight, vizColor);
            Debug.DrawLine(botFrontLeft, topFrontLeft, vizColor);
            Debug.DrawLine(botFrontRight, topFrontRight, vizColor);
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
                            if (_timePlayerStopped == 0) _timePlayerStopped = Game.GameTime;
                        }
                        else
                        {
                            _timePlayerStopped = 0;
                        }

                        // If the timer has been running for more than the timeout, clear any tasks and pause the logic.
                        if (_timePlayerStopped != 0 && Game.GameTime - _timePlayerStopped > StoppedPlayerTimeoutMs)
                        {
                            if (TaskedVehicles.Any() || IntersectionTaskedVehicles.Any() ||
                                IntersectionCreepTaskedVehicles.Any())
                                ClearAllTrackedVehicles();

                            continue; // Skip the main logic handlers until the player moves again
                        }

                        if (!_isSilentModeActive)
                        {
                            // Disable the game's default yielding behavior so ours can take over.
                            playerVehicle.ShouldVehiclesYieldToThisVehicle = false;
                            _isSilentModeActive = true;
                        }

                        // Run the main logic handlers.
                        HandleCustomYielding(playerVehicle);
                        HandleIntersectionLogic(playerVehicle);
                    }
                    // If the siren is off, deactivate the plugin and clean up.
                    else
                    {
                        if (!_isSilentModeActive) continue;
                        // Restore the game's default yielding behavior.
                        playerVehicle.ShouldVehiclesYieldToThisVehicle = true;
                        _isSilentModeActive = false;
                        _timePlayerStopped = 0; // Reset stopped timer
                        ClearAllTrackedVehicles();
                    }
                }
                // If the player is not in an emergency vehicle, perform cleanup as a failsafe.
                else
                {
                    if (!_isSilentModeActive) continue;
                    _isSilentModeActive = false;
                    _timePlayerStopped = 0; // Reset stopped timer
                    ClearAllTrackedVehicles();
                }
            }
        }

        // Handles all logic related to approaching and managing intersections
        private static void HandleIntersectionLogic(Vehicle emergencyVehicle)
        {
            // If we have an active intersection, check if we've passed it.
            if (_activeIntersectionCenter.HasValue)
            {
                var vectorToIntersection = _activeIntersectionCenter.Value - emergencyVehicle.Position;
                var dotPlayerToCenter = Vector3.Dot(emergencyVehicle.ForwardVector, vectorToIntersection);
                // If we are moving away from the intersection or are too far past it...
                if (dotPlayerToCenter < -10f || vectorToIntersection.Length() > IntersectionSearchMaxDistance + 20f)
                {
                    // ...clear all tasks and start a cooldown to prevent re-detecting the same intersection.
                    ClearAllTrackedVehicles();
                    _intersectionClearTime = Game.GameTime; // Start the cooldown timer
                    return;
                }
            }

            // If an intersection is active, manage the vehicles associated with it.
            if (_activeIntersectionCenter.HasValue)
            {
                var center = _activeIntersectionCenter.Value;
                // Remove cross-traffic vehicles that are too far away.
                IntersectionTaskedVehicles.RemoveWhere(v =>
                {
                    var shouldRemove = !v.Exists() || v.Position.DistanceTo(center) > 80f;
                    if (!shouldRemove || !TaskedVehicleBlips.TryGetValue(v, out var blip)) return shouldRemove;
                    if (blip.Exists()) blip.Delete();
                    TaskedVehicleBlips.Remove(v);

                    return shouldRemove;
                });
                // Remove "creeping" vehicles if they are too far, have finished, have been abandoned, or have timed out.
                var creepersToUntask = IntersectionCreepTaskedVehicles
                                       .Where(kvp => !kvp.Key.Exists() ||
                                                     kvp.Key.Position.DistanceTo(center) > 80f ||
                                                     kvp.Key.Position.DistanceTo(kvp.Value.TargetPosition) <
                                                     CreepTaskCompletionDistance ||
                                                     kvp.Key.Position.DistanceTo(kvp.Value.TargetPosition) >
                                                     CreepTaskAbandonDistance ||
                                                     Game.GameTime - kvp.Value.GameTimeStarted > CreepTaskTimeoutMs)
                                       .Select(kvp => kvp.Key)
                                       .ToList();
                foreach (var v in creepersToUntask)
                {
                    if (v.Exists() && v.Driver.Exists()) v.Driver.Tasks.Clear();
                    if (TaskedVehicleBlips.TryGetValue(v, out var blip))
                    {
                        if (blip.Exists()) blip.Delete();
                        TaskedVehicleBlips.Remove(v);
                    }

                    IntersectionCreepTaskedVehicles.Remove(v);
                }
            }

            // If no intersection is active, try to detect one.
            if (!_activeIntersectionCenter.HasValue)
            {
                // Don't search for a new intersection if the cooldown is active.
                if (Game.GameTime - _intersectionClearTime < IntersectionDetectionCooldownMs) return;

                Object foundObject = null;
                var allIntersectionModels = TrafficLightModels.Concat(StopSignModels).ToArray();
                // Search in steps ahead of the player to find a traffic light or stop sign prop.
                for (var searchDistance = IntersectionSearchMaxDistance;
                     searchDistance > IntersectionSearchMinDistance;
                     searchDistance -= IntersectionSearchStepSize)
                {
                    var searchPosition =
                        emergencyVehicle.Position + emergencyVehicle.ForwardVector * searchDistance;
                    foreach (var modelHash in allIntersectionModels)
                    {
                        foundObject =
                            NativeFunction.Natives.GET_CLOSEST_OBJECT_OF_TYPE<Object>(searchPosition,
                                IntersectionSearchRadius, modelHash, false, false, false);
                        if (foundObject == null) continue;
                        // Check if the found object's heading is aligned with the player's.
                        var isHeadingValid = false;
                        var headingDiff = Math.Abs(emergencyVehicle.Heading - foundObject.Heading);
                        if (headingDiff < IntersectionHeadingThreshold ||
                            headingDiff > 360 - IntersectionHeadingThreshold)
                            isHeadingValid = true;
                        if (isHeadingValid) break;
                        foundObject = null;
                    }

                    if (foundObject != null) break;
                }

                // If a valid object was found, set it as the active intersection.
                if (foundObject != null)
                {
                    _isStopSignIntersection = StopSignModels.Contains(foundObject.Model.Hash);
                    if (_isStopSignIntersection)
                    {
                        var rawCenter = foundObject.Position + foundObject.ForwardVector * 12f;
                        var groundZ = World.GetGroundZ(rawCenter, false, false);
                        _activeIntersectionCenter = groundZ.HasValue
                            ? new Vector3(rawCenter.X, rawCenter.Y, groundZ.Value)
                            : rawCenter;
                    }
                    else
                    {
                        _activeIntersectionCenter = foundObject.Position;
                        if (EnableOpticom) SetTrafficLightGreen(foundObject);
                    }
                }
            }

            // If we don't have an active intersection at this point, do nothing further.
            if (!_activeIntersectionCenter.HasValue) return;
            var intersectionCenter = _activeIntersectionCenter.Value;
            // Get all vehicles near the active intersection.
            var nearbyEntities = World.GetEntities(intersectionCenter, 60f,
                GetEntitiesFlags.ConsiderAllVehicles | GetEntitiesFlags.ExcludePlayerVehicle);
            // Process each vehicle near the intersection.
            foreach (var vehicle in nearbyEntities.OfType<Vehicle>())
            {
                // Basic filtering for invalid or already-tasked vehicles.
                if (!vehicle.Exists() || !vehicle.IsAlive || !vehicle.Driver.Exists() || vehicle.HasSiren) continue;
                if (TaskedVehicles.ContainsKey(vehicle) || IntersectionTaskedVehicles.Contains(vehicle) ||
                    IntersectionCreepTaskedVehicles.ContainsKey(vehicle)) continue;

                var headingDot = Vector3.Dot(emergencyVehicle.ForwardVector, vehicle.ForwardVector);

                // Check for "creep" candidates: vehicles stopped in front of us, facing the same direction.
                if (headingDot > 0.8f && vehicle.Speed < MinYieldSpeedMph)
                {
                    var vectorToVehicle = vehicle.Position - emergencyVehicle.Position;
                    var forwardDist = Vector3.Dot(emergencyVehicle.ForwardVector, vectorToVehicle);
                    if (forwardDist > 2f && forwardDist < IntersectionSearchMaxDistance)
                    {
                        var driver = vehicle.Driver;
                        var lateralOffset = Vector3.Dot(vectorToVehicle, emergencyVehicle.RightVector);
                        var sidePushVector = lateralOffset > 0
                            ? emergencyVehicle.RightVector
                            : -emergencyVehicle.RightVector;
                        var targetPos = vehicle.Position
                                        + vehicle.ForwardVector * IntersectionCreepForwardDistance
                                        + sidePushVector * IntersectionCreepSideDistance;

                        // Make sure the target position is on the ground and the path is clear.
                        var groundZ = World.GetGroundZ(targetPos, false, false);
                        var finalTargetPos = groundZ.HasValue
                            ? new Vector3(targetPos.X, targetPos.Y, groundZ.Value)
                            : targetPos;
                        var pathTrace = World.TraceLine(vehicle.Position, finalTargetPos, TraceFlags.IntersectWorld,
                            vehicle);
                        if (pathTrace.Hit) continue;

                        // Assign the creep task.
                        driver.Tasks.Clear();
                        driver.Tasks.DriveToPosition(finalTargetPos, IntersectionCreepDriveSpeed,
                            VehicleDrivingFlags.Emergency | VehicleDrivingFlags.StopAtDestination);

                        var creepTask = new CreepTask
                            { TargetPosition = finalTargetPos, GameTimeStarted = Game.GameTime };
                        IntersectionCreepTaskedVehicles.Add(vehicle, creepTask);

                        if (!TaskedVehicleBlips.ContainsKey(vehicle))
                        {
                            var blip = vehicle.AttachBlip();
                            blip.Color = Color.Purple;
                            TaskedVehicleBlips.Add(vehicle, blip);
                        }

                        // Skip to the next vehicle since this one has been tasked.
                        continue;
                    }
                }

                // Check for "stop" candidates: cross-traffic that needs to be stopped.
                var isPotentialTarget = false;
                if (_isStopSignIntersection)
                {
                    // For stop signs, stop any vehicle not going our direction.
                    if (headingDot < 0.7f) isPotentialTarget = true;
                }
                else
                {
                    // For traffic lights, only stop vehicles that are nearly perpendicular.
                    if (Math.Abs(headingDot) < CrossTrafficHeadingDotThreshold) isPotentialTarget = true;
                }

                if (!isPotentialTarget) continue;
                var distanceToCenter = vehicle.Position.DistanceTo(intersectionCenter);
                var shouldStop = false;
                if (_isStopSignIntersection)
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
                IntersectionTaskedVehicles.Add(vehicle);

                if (TaskedVehicleBlips.ContainsKey(vehicle)) continue;
                {
                    var blip = vehicle.AttachBlip();
                    blip.Color = Color.Blue;
                    TaskedVehicleBlips.Add(vehicle, blip);
                }
            }
        }

        // Handles all logic for making vehicles in front of the player yield (pull over).
        private static void HandleCustomYielding(Vehicle emergencyVehicle)
        {
            // This part now runs regardless of player speed, so vehicles can finish their tasks.
            var vehiclesToUntask = new List<Vehicle>();
            foreach (var entry in TaskedVehicles)
            {
                var vehicle = entry.Key;
                var task = entry.Value;

                // Check if the vehicle should be un-tasked (it's too far, or has reached its destination).
                if (!vehicle.Exists() ||
                    vehicle.Position.DistanceTo(emergencyVehicle.Position) > DetectionRange + 20f ||
                    vehicle.Position.DistanceTo(task.TargetPosition) < 8.0f)
                {
                    vehiclesToUntask.Add(vehicle);
                    continue;
                }

                // Persistently set the indicator status to override the game's native AI.
                switch (task.TaskType)
                {
                    case YieldTaskType.MoveRight:
                    case YieldTaskType.ForceMoveRight:
                        vehicle.IndicatorLightsStatus = VehicleIndicatorLightsStatus.RightOnly;
                        break;
                    case YieldTaskType.MoveLeft:
                    case YieldTaskType.ForceMoveLeft:
                        vehicle.IndicatorLightsStatus = VehicleIndicatorLightsStatus.LeftOnly;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            // Clean up vehicles that have completed their task or are out of range.
            foreach (var vehicle in vehiclesToUntask)
            {
                if (vehicle.Exists())
                    vehicle.IndicatorLightsStatus = VehicleIndicatorLightsStatus.Off;

                if (TaskedVehicleBlips.TryGetValue(vehicle, out var blip))
                {
                    if (blip.Exists()) blip.Delete();
                    TaskedVehicleBlips.Remove(vehicle);
                }

                TaskedVehicles.Remove(vehicle);
            }

            // --- FIND NEW VEHICLES TO TASK ---
            // Only look for new vehicles to task if the player is moving fast enough.
            if (emergencyVehicle.Speed <= MinYieldSpeedMph) return;

            var nearbyEntities = World.GetEntities(emergencyVehicle.Position, DetectionRange + 5f,
                GetEntitiesFlags.ConsiderAllVehicles | GetEntitiesFlags.ExcludePlayerVehicle);

            foreach (var entity in nearbyEntities)
            {
                if (!(entity is Vehicle vehicle)) continue;

                // This check prevents vehicles that already have ANY kind of task from getting a new one.
                if (TaskedVehicles.ContainsKey(vehicle) || IntersectionTaskedVehicles.Contains(vehicle) ||
                    IntersectionCreepTaskedVehicles.ContainsKey(vehicle)) continue;

                // Filter out irrelevant vehicles.
                if (!vehicle.Exists() || !vehicle.IsAlive || vehicle.HasSiren || vehicle.Speed < MinYieldSpeedMph)
                    continue;

                var driver = vehicle.Driver;
                if (!driver.Exists() || !driver.IsAlive) continue;

                // Check vertical distance to ignore vehicles on overpasses/underpasses
                var verticalDistance =
                    Math.Abs(vehicle.Position.Z - (emergencyVehicle.Position.Z + DetectionHeightOffset));
                if (verticalDistance > DetectionAreaHeight / 2.0f) continue;

                // Check if the vehicle is in front of us and facing the same general direction.
                var headingDot = Vector3.Dot(emergencyVehicle.ForwardVector, vehicle.ForwardVector);
                if (headingDot < 0.5f) continue;

                var vectorToTarget = vehicle.Position - emergencyVehicle.Position;
                var forwardDistance = Vector3.Dot(vectorToTarget, emergencyVehicle.ForwardVector);
                if (forwardDistance < 0f || forwardDistance > DetectionRange) continue;

                // Check if the vehicle is within the trapezoidal detection area.
                var t = forwardDistance / DetectionRange;
                var maxAllowedWidth = DetectionStartWidth + (DetectionEndWidth - DetectionStartWidth) * t;
                var lateralOffset = Vector3.Dot(vectorToTarget, emergencyVehicle.RightVector);
                if (Math.Abs(lateralOffset) > maxAllowedWidth) continue;

                // Use trace lines to check for obstacles to the left and right of the vehicle.
                var checkStart = vehicle.Position + vehicle.ForwardVector * (vehicle.Length / 2f) +
                                 new Vector3(0, 0, 0.5f);
                const float sideCheckDistance = 3.5f;
                var rightHit = World.TraceLine(checkStart, checkStart + vehicle.RightVector * sideCheckDistance,
                    TraceFlags.IntersectVehicles | TraceFlags.IntersectObjects, vehicle);
                var leftHit = World.TraceLine(checkStart, checkStart - vehicle.RightVector * sideCheckDistance,
                    TraceFlags.IntersectVehicles | TraceFlags.IntersectObjects, vehicle);

                var canGoRight = !rightHit.Hit;
                var canGoLeft = !leftHit.Hit;
                Vector3 rawTargetPos;
                YieldTaskType taskType;
                // Prefer moving right unless the vehicle is significantly to our left.
                var preferRight = lateralOffset > -1.5f;

                if (preferRight)
                {
                    if (canGoRight)
                    {
                        taskType = YieldTaskType.MoveRight;
                        rawTargetPos = vehicle.Position + vehicle.RightVector * SideMoveDistance +
                                       vehicle.ForwardVector * ForwardMoveDistance;
                    }
                    else if (canGoLeft)
                    {
                        taskType = YieldTaskType.MoveLeft;
                        rawTargetPos = vehicle.Position - vehicle.RightVector * SideMoveDistance +
                                       vehicle.ForwardVector * ForwardMoveDistance;
                    }
                    else
                    {
                        taskType = YieldTaskType.ForceMoveRight;
                        rawTargetPos = vehicle.Position + vehicle.RightVector * ForceSideMoveDistance +
                                       vehicle.ForwardVector * ForwardMoveDistance;
                    }
                }
                else
                {
                    if (canGoLeft)
                    {
                        taskType = YieldTaskType.MoveLeft;
                        rawTargetPos = vehicle.Position - vehicle.RightVector * SideMoveDistance +
                                       vehicle.ForwardVector * ForwardMoveDistance;
                    }
                    else if (canGoRight)
                    {
                        taskType = YieldTaskType.MoveRight;
                        rawTargetPos = vehicle.Position + vehicle.RightVector * SideMoveDistance +
                                       vehicle.ForwardVector * ForwardMoveDistance;
                    }
                    else
                    {
                        taskType = YieldTaskType.ForceMoveLeft;
                        rawTargetPos = vehicle.Position - vehicle.RightVector * ForceSideMoveDistance +
                                       vehicle.ForwardVector * ForwardMoveDistance;
                    }
                }

                // Ensure the target position is on the ground and the path is clear.
                var groundZ = World.GetGroundZ(rawTargetPos, false, false);
                var finalTargetPos = groundZ.HasValue
                    ? new Vector3(rawTargetPos.X, rawTargetPos.Y, groundZ.Value)
                    : rawTargetPos;

                var pathTrace = World.TraceLine(vehicle.Position, finalTargetPos, TraceFlags.IntersectWorld, vehicle);
                if (pathTrace.Hit) continue;

                // Assign the yield task.
                driver.Tasks.Clear();
                driver.Tasks.DriveToPosition(finalTargetPos, DriveSpeed, VehicleDrivingFlags.Normal);
                TaskedVehicles.Add(vehicle, new YieldTask { TargetPosition = finalTargetPos, TaskType = taskType });

                if (TaskedVehicleBlips.ContainsKey(vehicle)) continue;
                var blip = vehicle.AttachBlip();
                blip.Color = Color.Green;
                TaskedVehicleBlips.Add(vehicle, blip);
            }
        }

        // A utility function to clear all tasks, blips, and state.
        private static void ClearAllTrackedVehicles()
        {
            // Delete all blips from the map.
            foreach (var blip in TaskedVehicleBlips.Values.Where(blip => blip.Exists()))
                blip.Delete();

            TaskedVehicleBlips.Clear();

            // Get a combined list of all vehicles we are currently managing.
            var allTrackedVehicles = TaskedVehicles.Keys
                                                   .Concat(IntersectionTaskedVehicles)
                                                   .Concat(IntersectionCreepTaskedVehicles.Keys)
                                                   .Distinct();
            // Clear the AI tasks for every managed vehicle.
            foreach (var vehicle in allTrackedVehicles.Where(v => v.Exists() && v.Driver.Exists()))
                vehicle.Driver.Tasks.Clear();

            // Clear our internal tracking lists.
            TaskedVehicles.Clear();
            IntersectionTaskedVehicles.Clear();
            IntersectionCreepTaskedVehicles.Clear();

            // Reset the intersection state.
            _activeIntersectionCenter = null;
            _isStopSignIntersection = false;
        }

        // Defines the different types of yielding maneuvers.
        private enum YieldTaskType
        {
            MoveRight,
            MoveLeft,
            ForceMoveRight,
            ForceMoveLeft
        }

        // Holds the data for a single vehicle's yield task.
        private struct YieldTask
        {
            public Vector3 TargetPosition;
            public YieldTaskType TaskType;
        }

        // Holds the data for a single vehicle's creep task; uses game timestamp for timeouts.
        private struct CreepTask
        {
            public Vector3 TargetPosition;
            public uint GameTimeStarted;
        }
    }
}