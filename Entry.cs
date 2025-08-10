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

        // Forward detection zone for yield
        private const float DetectionRange = 40f;
        private const float DetectionStartWidth = 5.5f;
        private const float DetectionEndWidth = 2f;

        // Yield params
        private const float ForwardMoveDistance = 35f;
        private const float SideMoveDistance = 6f;
        private const float ForceSideMoveDistance = 6.0f;
        private const float DriveSpeed = 10f;

        // Intersection detection params
        private const float IntersectionSearchMaxDistance = 45f;
        private const float IntersectionSearchMinDistance = 30f;
        private const float IntersectionSearchStepSize = 7.0f;
        private const float IntersectionSearchRadius = 40.0f;
        private const float IntersectionHeadingThreshold = 40.0f; // Heading check for finding signs/traf lights.
        private const float CrossTrafficHeadingDotThreshold = 0.25f; // Threshold to identify cross-traffic.

        // --- Plugin State ---
        private static bool _isSilentModeActive; // Is the main logic running?
        private static GameFiber _pluginFiber; // Primary fiber

        private static readonly Dictionary<Vehicle, YieldTask>
            TaskedVehicles = new Dictionary<Vehicle, YieldTask>(); // Tracked vehicles during a yield

        private static readonly HashSet<Vehicle>
            IntersectionTaskedVehicles = new HashSet<Vehicle>(); // Tracks vehicles stopped at an intersection

        private static Vector3? _activeIntersectionCenter; // The current intersection location
        private static bool _isStopSignIntersection; // Differentiates between stop signs and traffic lights.

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

        // Entry point
        public static void Main()
        {
            _pluginFiber = new GameFiber(PluginLogic);
            _pluginFiber.Start();
            Game.FrameRender += OnFrameRender;
            Game.DisplayNotification("TrafficControl Plugin by Gemini loaded successfully.");
        }

        // Cleanup logic.
        public static void OnUnload(bool unloading)
        {
            Game.FrameRender -= OnFrameRender;

            // Clear tasks for vehicles being tasked where driver is still alive
            foreach (var vehicle in IntersectionTaskedVehicles.Where(v => v.Exists() && v.Driver.Exists()))
                vehicle.Driver.Tasks.Clear();

            // Clear others
            TaskedVehicles.Clear();
            IntersectionTaskedVehicles.Clear();
            _activeIntersectionCenter = null;
            _isStopSignIntersection = false;
        }

        // Opticom: Forces light green.
        private static void SetTrafficLightGreen(Object trafficLight)
        {
            // Set light green fiber
            GameFiber.StartNew(() =>
            {
                // Force Green
                NativeFunction.Natives.SET_ENTITY_TRAFFICLIGHT_OVERRIDE(trafficLight, 0);
                GameFiber.Wait(OpticomGreenDurationMs);
                NativeFunction.Natives.SET_ENTITY_TRAFFICLIGHT_OVERRIDE(trafficLight, 3);
            });
        }

        // Debug rendering.
        private static void OnFrameRender(object sender, GraphicsEventArgs e)
        {
            if (!ShowDebugLines) return;
            var playerVehicle = Game.LocalPlayer.Character.CurrentVehicle;

            if (!playerVehicle.Exists()) return;

            // Draw lines for yielding vehicles.
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

            // Draw lines for intersection-stopped vehicles.
            if (_activeIntersectionCenter.HasValue)
            {
                var center = _activeIntersectionCenter.Value;
                foreach (var vehicle in IntersectionTaskedVehicles.Where(v => v.Exists()))
                    Debug.DrawLine(vehicle.Position, center, Color.Blue);
            }

            if (!_isSilentModeActive) return;

            // Draw lines for detection for yielding vehicles.
            var pos = playerVehicle.Position;
            var forward = playerVehicle.ForwardVector;
            var right = playerVehicle.RightVector;
            var offset = new Vector3(0, 0, 0.2f);

            var backLeft = pos - right * DetectionStartWidth + offset;
            var backRight = pos + right * DetectionStartWidth + offset;
            var frontLeft = pos + forward * DetectionRange - right * DetectionEndWidth + offset;
            var frontRight = pos + forward * DetectionRange + right * DetectionEndWidth + offset;
            var vizColor = Color.Aqua;

            Debug.DrawLine(backLeft, backRight, vizColor);
            Debug.DrawLine(backRight, frontRight, vizColor);
            Debug.DrawLine(frontRight, frontLeft, vizColor);
            Debug.DrawLine(frontLeft, backLeft, vizColor);
        }

        // Handles yielding for vehicles in path of player
        private static void HandleCustomYielding(Vehicle emergencyVehicle)
        {
            // --- UPDATE AND MAINTAIN EXISTING TASKS ---
            var vehiclesToUntask = new List<Vehicle>();
            foreach (var entry in TaskedVehicles)
            {
                var vehicle = entry.Key;
                var task = entry.Value;

                // Check if the vehicle should be un-tasked (is too far away or has finished the maneuver)
                if (!vehicle.Exists() ||
                    vehicle.Position.DistanceTo(emergencyVehicle.Position) > DetectionRange + 20f ||
                    vehicle.Position.DistanceTo(task.TargetPosition) < 8.0f)
                {
                    vehiclesToUntask.Add(vehicle);
                    continue;
                }

                // Persistently set the indicator status on each frame for the duration of the task.
                // This is necessary to fight the game's native AI from turning them off.
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
                }
            }

            // Clean up vehicles that have completed their task or are out of range.
            foreach (var vehicle in vehiclesToUntask)
            {
                if (vehicle.Exists())
                    // Ensure the indicator is off after the task is done.
                    vehicle.IndicatorLightsStatus = VehicleIndicatorLightsStatus.Off;

                TaskedVehicles.Remove(vehicle);
            }

            // --- FIND NEW VEHICLES TO TASK ---
            // Find vehicles near player
            var nearbyEntities = World.GetEntities(emergencyVehicle.Position, DetectionRange + 5f,
                GetEntitiesFlags.ConsiderAllVehicles | GetEntitiesFlags.ExcludePlayerVehicle);

            foreach (var entity in nearbyEntities)
            {
                if (!(entity is Vehicle vehicle)) continue;
                // Filter out already yielded vehicles
                if (TaskedVehicles.ContainsKey(vehicle) || IntersectionTaskedVehicles.Contains(vehicle)) continue;
                if (!vehicle.Exists() || !vehicle.IsAlive || vehicle.HasSiren || vehicle.Speed < 1.0f)
                    continue; //BUG: detecting tow trucks

                var driver = vehicle.Driver;
                if (!driver.Exists() || !driver.IsAlive) continue;

                // Check direction matches player
                var headingDot = Vector3.Dot(emergencyVehicle.ForwardVector, vehicle.ForwardVector);
                if (headingDot < 0.5f) continue;

                // Check vehicle in detection range
                var vectorToTarget = vehicle.Position - emergencyVehicle.Position;
                var forwardDistance = Vector3.Dot(vectorToTarget, emergencyVehicle.ForwardVector);
                if (forwardDistance < 0f || forwardDistance > DetectionRange) continue;

                var t = forwardDistance / DetectionRange;
                var maxAllowedWidth = DetectionStartWidth + (DetectionEndWidth - DetectionStartWidth) * t;
                var lateralOffset = Vector3.Dot(vectorToTarget, emergencyVehicle.RightVector);
                if (Math.Abs(lateralOffset) > maxAllowedWidth) continue;

                // Side to side checks
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
                // Prefer moving right unless car is far to the left.
                var preferRight = lateralOffset > -1.5f;

                // Determine yieldtasktype
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
                    else // No room so force veh right
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
                    else // No room so force veh left
                    {
                        taskType = YieldTaskType.ForceMoveLeft;
                        rawTargetPos = vehicle.Position - vehicle.RightVector * ForceSideMoveDistance +
                                       vehicle.ForwardVector * ForwardMoveDistance;
                    }
                }

                // Get target position on ground
                var groundZ = World.GetGroundZ(rawTargetPos, false, false);
                var finalTargetPos = groundZ.HasValue
                    ? new Vector3(rawTargetPos.X, rawTargetPos.Y, groundZ.Value)
                    : rawTargetPos;

                // Check obstacles in path - NOT ENTIRELY RELIABLE
                var pathTrace = World.TraceLine(vehicle.Position, finalTargetPos, TraceFlags.IntersectWorld, vehicle);
                if (pathTrace.Hit) continue;

                // Task driver with the driveto task
                driver.Tasks.Clear();
                driver.Tasks.DriveToPosition(finalTargetPos, DriveSpeed, VehicleDrivingFlags.Normal);
                TaskedVehicles.Add(vehicle, new YieldTask { TargetPosition = finalTargetPos, TaskType = taskType });
            }
        }

        // Logic for intersection handling
        private static void HandleIntersectionLogic(Vehicle emergencyVehicle)
        {
            // Clear active intersection if already passed
            if (_activeIntersectionCenter.HasValue)
            {
                var vectorToIntersection = _activeIntersectionCenter.Value - emergencyVehicle.Position;
                var dotPlayerToCenter = Vector3.Dot(emergencyVehicle.ForwardVector, vectorToIntersection);

                // Check moving away from or are too far past intersection.
                if (dotPlayerToCenter < -10f || vectorToIntersection.Length() > IntersectionSearchMaxDistance + 20f)
                {
                    foreach (var vehicle in IntersectionTaskedVehicles.Where(v => v.Exists() && v.Driver.Exists()))
                        vehicle.Driver.Tasks.Clear();

                    IntersectionTaskedVehicles.Clear();
                    _activeIntersectionCenter = null;
                    _isStopSignIntersection = false;
                    return;
                }
            }

            // Remove too far away vehicles
            if (_activeIntersectionCenter.HasValue)
            {
                var vehiclesToUntask = IntersectionTaskedVehicles
                    .Where(v => !v.Exists() || v.Position.DistanceTo(_activeIntersectionCenter.Value) > 80f).ToList();
                foreach (var v in vehiclesToUntask) IntersectionTaskedVehicles.Remove(v);
            }

            // Search for intersection ahead
            if (!_activeIntersectionCenter.HasValue)
            {
                Object foundObject = null;
                var allIntersectionModels = TrafficLightModels.Concat(StopSignModels).ToArray();

                // Search in steps ahead of player. - REQUIRES MORE TESTING
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

                        // Check heading of obj to make sure its parallel
                        var isHeadingValid = false;
                        var headingDiff = Math.Abs(emergencyVehicle.Heading - foundObject.Heading);

                        if (headingDiff < IntersectionHeadingThreshold ||
                            headingDiff > 360 - IntersectionHeadingThreshold)
                            isHeadingValid = true;

                        if (isHeadingValid) break;

                        foundObject = null; // Not valid target
                    }

                    if (foundObject != null) break; // stop searching.
                }

                // Set active intersection
                if (foundObject != null)
                {
                    _isStopSignIntersection = StopSignModels.Contains(foundObject.Model.Hash);

                    if (_isStopSignIntersection)
                    {
                        // Stop sign center is a bit in front of the sign itself.
                        var rawCenter = foundObject.Position + foundObject.ForwardVector * 12f;
                        var groundZ = World.GetGroundZ(rawCenter, false, false);
                        _activeIntersectionCenter = groundZ.HasValue
                            ? new Vector3(rawCenter.X, rawCenter.Y, groundZ.Value)
                            : rawCenter;
                    }
                    else
                    {
                        // Traffic light center at the light's position.
                        _activeIntersectionCenter = foundObject.Position;
                        if (EnableOpticom) SetTrafficLightGreen(foundObject);
                    }
                }
            }

            if (!_activeIntersectionCenter.HasValue) return; // No intersection to manage.

            // Stop vehicles at the active intersection.
            {
                var nearbyEntities = World.GetEntities(_activeIntersectionCenter.Value, 50f,
                    GetEntitiesFlags.ConsiderAllVehicles | GetEntitiesFlags.ExcludePlayerVehicle);

                foreach (var vehicle in nearbyEntities.OfType<Vehicle>())
                {
                    if (!vehicle.Exists() || !vehicle.IsAlive || !vehicle.Driver.Exists() || vehicle.HasSiren) continue;
                    if (IntersectionTaskedVehicles.Contains(vehicle) || TaskedVehicles.ContainsKey(vehicle)) continue;

                    var headingDot = Vector3.Dot(emergencyVehicle.ForwardVector, vehicle.ForwardVector);
                    var isPotentialTarget = false;

                    // Apply different targeting logic based on intersection type.
                    if (_isStopSignIntersection)
                    {
                        // Stop signs: stop everyone not going our direction.
                        if (headingDot < 0.7f) isPotentialTarget = true;
                    }
                    else
                    {
                        // Traffic lights: only stop cross-traffic.
                        if (Math.Abs(headingDot) < CrossTrafficHeadingDotThreshold) isPotentialTarget = true;
                    }

                    if (!isPotentialTarget) continue;

                    var distanceToCenter = vehicle.Position.DistanceTo(_activeIntersectionCenter.Value);
                    var shouldStop = false;

                    if (_isStopSignIntersection)
                    {
                        // For stop signs, just use a radius check.
                        if (distanceToCenter < 35f) shouldStop = true;
                    }
                    else
                    {
                        // Traffic light detected: ensure they are close AND heading towards the light in question.
                        var vectorToIntersection = _activeIntersectionCenter.Value - vehicle.Position;
                        var dotVehToCenter = Vector3.Dot(vehicle.ForwardVector, vectorToIntersection);
                        if (dotVehToCenter > 0 && distanceToCenter < 35f) shouldStop = true;
                    }

                    if (!shouldStop) continue;

                    // Apply braking task
                    var driver = vehicle.Driver;
                    driver.Tasks.Clear();
                    driver.Tasks.PerformDrivingManeuver(vehicle, VehicleManeuver.GoForwardStraightBraking, 2000);
                    IntersectionTaskedVehicles.Add(vehicle);
                }
            }
        }

        // Main plugin tick fiber
        private static void PluginLogic()
        {
            while (true)
            {
                GameFiber.Yield();

                var playerVehicle = Game.LocalPlayer.Character.CurrentVehicle;

                if (playerVehicle.Exists() && playerVehicle.HasSiren)
                {
                    // Siren is ON.
                    if (playerVehicle.IsSirenOn)
                    {
                        if (!_isSilentModeActive)
                        {
                            // Activate our custom logic.
                            playerVehicle.ShouldVehiclesYieldToThisVehicle = false; // Disable default AI yielding.
                            _isSilentModeActive = true;
                        }

                        HandleCustomYielding(playerVehicle);
                        HandleIntersectionLogic(playerVehicle);
                    }
                    // Siren is OFF.
                    else
                    {
                        if (!_isSilentModeActive) continue;
                        // Deactivate and cleanup.
                        playerVehicle.ShouldVehiclesYieldToThisVehicle = true; // Restore default behavior.
                        _isSilentModeActive = false;

                        foreach (var vehicle in IntersectionTaskedVehicles.Where(v => v.Exists() && v.Driver.Exists()))
                            vehicle.Driver.Tasks.Clear();

                        TaskedVehicles.Clear();
                        IntersectionTaskedVehicles.Clear();
                        _activeIntersectionCenter = null;
                        _isStopSignIntersection = false;
                    }
                }
                // Player not emergency vehicle.
                else
                {
                    if (!_isSilentModeActive) continue;
                    // Failsafe cleanup.
                    _isSilentModeActive = false;

                    foreach (var vehicle in IntersectionTaskedVehicles.Where(v => v.Exists() && v.Driver.Exists()))
                        vehicle.Driver.Tasks.Clear();

                    TaskedVehicles.Clear();
                    IntersectionTaskedVehicles.Clear();
                    _activeIntersectionCenter = null;
                    _isStopSignIntersection = false;
                }
            }
        }

        // Types of yielding maneuvers
        private enum YieldTaskType
        {
            MoveRight,
            MoveLeft,
            ForceMoveRight,
            ForceMoveLeft
        }

        // Data for a single yield task.
        private struct YieldTask
        {
            public Vector3 TargetPosition;
            public YieldTaskType TaskType;
        }
    }
}