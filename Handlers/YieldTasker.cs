using System;
using System.Collections.Generic;
using System.Drawing;
using Rage;
using Rage.Native;

namespace MTFOv4
{
    public class YieldTasker
    {
        private RoadPosition _aiRoadPosition;

        private Color _debugColor = Color.White;
        private string _debugStatus = "Initializing";
        private bool _hasStopped;
        public string CurrentAction => _debugStatus;

        private uint _nextLogicTime;

        private Vector3 _stickyTarget = Vector3.Zero;
        private bool _targetIsLocked;
        private float _lockedSpeed;
        private bool _isSlowingDown;
        private bool _isChangingLanes;

        private List<Vector3> _debugYieldCandidates = new List<Vector3>();

        private Vector3 _debugValidTarget = Vector3.Zero;
        public int CurrentLane => _aiRoadPosition?.CurrentLane ?? 0;
        public int TotalLanes => _aiRoadPosition?.TotalLanes ?? 0;

        public uint VehicleHandle { get; private set; }
        public Vehicle Vehicle { get; }
        public bool IsFinished { get; private set; }

        public YieldTasker(Vehicle vehicle)
        {
            Vehicle = vehicle;
            VehicleHandle = vehicle.Handle;
            if (vehicle.Exists()) _aiRoadPosition = new RoadPosition(vehicle);
        }

        /// <summary>
        /// Attempts to find a safe target position in the lane to the right of the current vehicle.
        /// </summary>
        /// <param name="distanceAhead">How far forward to look for a lane node.</param>
        /// <param name="targetPos">The resulting vector for the right-hand lane position.</param>
        /// <returns>True if a valid right-lane node was found.</returns>
        private bool GetRightLaneTarget(float distanceAhead, out Vector3 targetPos)
        {
            targetPos = Vector3.Zero;
            if (!IsValid()) return false;

            Vector3 searchPos = Vehicle.Position + (Vehicle.ForwardVector * distanceAhead);

            if (NativeFunction.Natives.GET_CLOSEST_VEHICLE_NODE_WITH_HEADING<bool>(searchPos, out Vector3 nodePos, out float nodeHeading, 1, 3.0f, 0))
            {
                Vector3 nodeDir = MathHelper.ConvertHeadingToDirection(nodeHeading).ToNormalized();
                if (Vector3.Dot(nodeDir, Vehicle.ForwardVector) < -0.2f) nodeDir = -nodeDir;

                // get a perpendicular right vector for the road
                Vector3 roadRight = Vector3.Cross(nodeDir, Vector3.WorldUp).ToNormalized();
                float laneWidth = 4.5f;

                if (NativeFunction.Natives.GET_CLOSEST_VEHICLE_NODE_WITH_HEADING<bool>(Vehicle.Position, out Vector3 currentNodePos, out float currentHeading, 1, 3.0f, 0))
                {
                    Vector3 currentDir = MathHelper.ConvertHeadingToDirection(currentHeading).ToNormalized();
                    if (Vector3.Dot(currentDir, Vehicle.ForwardVector) < -0.2f) currentDir = -currentDir;

                    Vector3 currentRoadRight = Vector3.Cross(currentDir, Vector3.WorldUp).ToNormalized();
                    Vector3 toVeh = Vehicle.Position - currentNodePos;

                    // calculate lateral offset from the current node
                    float currentOffset = Vector3.Dot(toVeh, currentRoadRight);
                    float targetOffset = currentOffset + laneWidth;

                    Vector3 rawTarget = nodePos + (roadRight * targetOffset);

                    if (RoadUtilities.GetGroundPosition(rawTarget, out Vector3 groundedTarget))
                    {
                        targetPos = groundedTarget;
                    }
                    else
                    {
                        targetPos = rawTarget;
                    }

                    Vector3 vecToTarget = targetPos - Vehicle.Position;
                    if (Vector3.Dot(Vehicle.RightVector, vecToTarget) < 0.5f) return false;

                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// The main state machine for an individual vehicle, handling lane changes, slowing down, and stopping.
        /// </summary>
        /// <param name="policeVehicle">The player's vehicle to yield to.</param>
        /// <param name="policeLane">The current lane the player is occupying.</param>
        public void Process(Vehicle policeVehicle, int policeLane)
        {
            if (policeVehicle == null || !policeVehicle.Exists() || !IsValid())
            {
                return;
            }

            Vector3 vecToPolice = policeVehicle.Position - Vehicle.Position;

            // check if the ai is way behind player
            float dotBack = Vector3.Dot(Vehicle.ForwardVector, vecToPolice);

            if (dotBack > 20.0f && policeVehicle.Speed > 5.0f)
            {
                IsFinished = true;
                return;
            }

            if (!_targetIsLocked && !_isChangingLanes && Vehicle.Speed < 0.6f)
            {
                _hasStopped = true;
                EnsureStopped();
                return;
            }

            if (_hasStopped)
            {
                if (Vehicle.Speed > 0.5f)
                {
                    EnsureStopped();
                }
                return;
            }

            if (Game.GameTime < _nextLogicTime)
            {
                return;
            }

            _nextLogicTime = Game.GameTime + 150;

            if (_aiRoadPosition == null)
            {
                _aiRoadPosition = new RoadPosition(Vehicle);
            }

            _aiRoadPosition.Process();

            if (_targetIsLocked && _stickyTarget != Vector3.Zero)
            {
                Vector3 dirToTarget = _stickyTarget - Vehicle.Position;
                if (Vector3.Dot(Vehicle.ForwardVector, dirToTarget) < -2.0f)
                {
                    _targetIsLocked = false;
                    _isSlowingDown = false;
                    _isChangingLanes = false;
                    return;
                }

                float distanceToTarget = Vehicle.DistanceTo(_stickyTarget);

                if (_isChangingLanes)
                {
                    _debugStatus = $"Changing Lanes >> | Dist: {distanceToTarget:F1}m";
                    _debugColor = Color.Orange;

                    if ((distanceToTarget < 2.5f && CurrentLane != policeLane && CurrentLane > 0) || distanceToTarget < 1.0f)
                    {
                        _targetIsLocked = false;
                        _isChangingLanes = false;
                        IsFinished = true;
                    }
                    return;
                }

                if (!_isChangingLanes && !IsPositionClear(_stickyTarget, 2.5f))
                {
                    _targetIsLocked = false;
                    _isSlowingDown = false;
                    return;
                }

                if (distanceToTarget < 5.0f || (distanceToTarget < 8.0f && Vehicle.Speed < 0.5f))
                {
                    _hasStopped = true;
                    EnsureStopped();
                    return;
                }

                if (distanceToTarget < 25.0f && !_isSlowingDown)
                {
                    _isSlowingDown = true;
                    _lockedSpeed = 6.0f;
                    ApplyDriveTask(_stickyTarget, _lockedSpeed);
                }

                if (_isSlowingDown)
                {
                    _debugStatus = $"Slowing Down | Dist: {distanceToTarget:F1}m";
                }
                else
                {
                    _debugStatus = $"Locked Yield | Dist: {distanceToTarget:F1}m";
                }

                _debugColor = Color.Lime;
                return;
            }

            if (_aiRoadPosition.IsDeadEnd)
            {
                return;
            }

            if (CurrentLane == policeLane || policeLane == 0)
            {
                ApplyNodeLogic();
            }
            else
            {
                IsFinished = true;
            }
        }

        /// <summary>
        /// Checks if a specific spot in the right lane is physically empty.
        /// </summary>
        /// <param name="distanceAhead">The distance ahead to check for other vehicles.</param>
        /// <returns>True if the space is clear of other entities.</returns>
        private bool IsRightLaneClear(float distanceAhead)
        {
            if (!GetRightLaneTarget(distanceAhead, out Vector3 rightLanePos)) return false;

            Entity[] vehicles = World.GetEntities(rightLanePos, 4.5f, GetEntitiesFlags.ConsiderAllVehicles);

            foreach (Entity v in vehicles)
            {
                if (v != null && v.Exists() && v.Handle != Vehicle.Handle) return false;
            }

            return true;
        }

        /// <summary>
        /// Decides whether the vehicle needs to change lanes or just pull over based on the current road layout.
        /// </summary>
        private void ApplyNodeLogic()
        {
            if (_aiRoadPosition == null || !IsValid())
            {
                return;
            }

            bool isRightmostLane = _aiRoadPosition.LanesToRight == 0;
            bool isOneLaneRoad = _aiRoadPosition.TotalLanes <= 1;

            if (!isRightmostLane && !isOneLaneRoad)
            {
                if (IsRightLaneClear(10.0f) && IsRightLaneClear(22.0f))
                {
                    float forwardDist = Math.Max(45.0f, Vehicle.Speed * 2.5f);

                    if (GetRightLaneTarget(forwardDist, out Vector3 laneChangeTarget))
                    {
                        _stickyTarget = laneChangeTarget;
                        _lockedSpeed = Math.Max(22.0f, Vehicle.Speed * 0.5f);
                        _targetIsLocked = true;
                        _isChangingLanes = true;
                        _isSlowingDown = false;

                        _debugStatus = "Yield: Changing Lane >>";
                        _debugColor = Color.Orange;

                        ApplyDriveTask(_stickyTarget, _lockedSpeed);
                    }
                    else
                    {
                        _lockedSpeed = Math.Max(12.0f, Vehicle.Speed * 0.8f);
                        _debugStatus = "Yield: Waiting to Change Lane";
                        _debugColor = Color.Yellow;

                        ApplyDriveTask(Vehicle.Position + (Vehicle.ForwardVector * 10.0f), _lockedSpeed, false);
                    }
                }
                else
                {
                    _lockedSpeed = Math.Max(12.0f, Vehicle.Speed * 0.8f);
                    _debugStatus = "Yield: Waiting to Change Lane";
                    _debugColor = Color.Yellow;

                    if (NativeFunction.Natives.GET_CLOSEST_VEHICLE_NODE_WITH_HEADING<bool>(Vehicle.Position, out Vector3 nodePos, out float nodeHeading, 1, 3.0f, 0))
                    {
                        Vector3 nodeDir = MathHelper.ConvertHeadingToDirection(nodeHeading).ToNormalized();
                        if (Vector3.Dot(nodeDir, Vehicle.ForwardVector) < -0.2f) nodeDir = -nodeDir;
                        ApplyDriveTask(Vehicle.Position + (nodeDir * 15.0f), _lockedSpeed);
                    }
                    else
                    {
                        ApplyDriveTask(Vehicle.Position + (Vehicle.ForwardVector * 15.0f), _lockedSpeed);
                    }
                }

                return;
            }

            CalculateYieldCandidates();

            if (_debugYieldCandidates == null || _debugYieldCandidates.Count == 0)
            {
                _debugStatus = "Yield: No Valid Path Found";
                _debugColor = Color.Red;
                return;
            }

            float currentSpeed = Vehicle.Speed;
            float minSafeDist = Math.Max(15.0f, currentSpeed * 1.5f);
            float optimalDist = Math.Max(25.0f, currentSpeed * 3.0f);

            Vector3 bestTarget = Vector3.Zero;
            float closestDiff = float.MaxValue;
            bool foundValid = false;

            for (int i = 0; i < _debugYieldCandidates.Count; i++)
            {
                Vector3 pos = _debugYieldCandidates[i];
                if (pos.DistanceTo(Vehicle.Position) > minSafeDist)
                {
                    float diff = Math.Abs(Vehicle.Position.DistanceTo(pos) - optimalDist);
                    if (diff < closestDiff)
                    {
                        closestDiff = diff;
                        bestTarget = pos;
                        foundValid = true;
                    }
                }
            }

            if (!foundValid)
            {
                for (int i = 0; i < _debugYieldCandidates.Count; i++)
                {
                    Vector3 pos = _debugYieldCandidates[i];
                    float diff = Math.Abs(Vehicle.Position.DistanceTo(pos) - optimalDist);
                    if (diff < closestDiff)
                    {
                        closestDiff = diff;
                        bestTarget = pos;
                    }
                }
            }

            bestTarget = EnsureForwardTarget(bestTarget);
            _stickyTarget = bestTarget;
            _lockedSpeed = Math.Max(18.0f, currentSpeed);
            _targetIsLocked = true;
            _isChangingLanes = false;
            _isSlowingDown = false;

            _debugStatus = "Yield: Pulling Over";
            _debugColor = Color.Lime;

            ApplyDriveTask(_stickyTarget, _lockedSpeed);
        }

        /// <summary>
        /// Forces the AI driver to perform a stopping maneuver and stay put.
        /// </summary>
        private void EnsureStopped()
        {
            if (Vehicle != null && Vehicle.Exists() && Vehicle.Driver != null && Vehicle.Driver.Exists())
            {
                Vehicle.Driver.Tasks.PerformDrivingManeuver(Vehicle, VehicleManeuver.Wait, 100000);
            }

            _debugStatus = "Yielded (Stopped)";
            _debugColor = Color.Red;
        }

        /// <summary>
        /// Basic wrapper to give the AI a drive-to-position task with specific flags.
        /// </summary>
        /// <param name="target">The target coordinate.</param>
        /// <param name="speed">The driving speed.</param>
        private void ApplyDriveTask(Vector3 target, float speed)
        {
            if (!IsValid())
            {
                return;
            }

            VehicleDrivingFlags flags = VehicleDrivingFlags.Normal | VehicleDrivingFlags.StopAtDestination | VehicleDrivingFlags.IgnorePathFinding;
            Vehicle.Driver.Tasks.DriveToPosition(target, speed, flags, 1.0f);
        }

        /// <summary>
        /// Overloaded drive task that allows toggling pathfinding and "stop at destination" flags.
        /// </summary>
        /// <param name="target">The target coordinate.</param>
        /// <param name="speed">The driving speed.</param>
        /// <param name="ignorePathFindingStopDestination">Whether to bypass normal pathfinding logic.</param>
        private void ApplyDriveTask(Vector3 target, float speed, bool ignorePathFindingStopDestination = true)
        {
            if (!IsValid())
            {
                return;
            }

            VehicleDrivingFlags flags = VehicleDrivingFlags.Normal;
            if (ignorePathFindingStopDestination) flags |= VehicleDrivingFlags.IgnorePathFinding;
            if (ignorePathFindingStopDestination) flags |= VehicleDrivingFlags.StopAtDestination;
            Vehicle.Driver.Tasks.DriveToPosition(target, speed, flags, 1.0f);
        }

        /// <summary>
        /// Populates the candidate list with valid road positions the vehicle could pull over into.
        /// </summary>
        private void CalculateYieldCandidates()
        {
            if (_aiRoadPosition == null) return;
            // Use the shared static logic, passing our persistent RoadPosition to save resources
            _debugYieldCandidates = ScanForYieldCandidates(Vehicle, _aiRoadPosition);
        }

        /// <summary>
        /// Static method that scans the road and generates a list of potential "pull over" spots.
        /// </summary>
        /// <param name="vehicle">The vehicle to scan for.</param>
        /// <param name="existingRp">Optional cached road data to save performance.</param>
        /// <returns>A list of valid vectors along the road shoulder or right lane.</returns>
        public static List<Vector3> ScanForYieldCandidates(Vehicle vehicle, RoadPosition existingRp = null)
        {
            var results = new List<Vector3>();
            if (vehicle == null || !vehicle.Exists()) return results;

            RoadPosition rp = existingRp;
            if (rp == null)
            {
                rp = new RoadPosition(vehicle);
                rp.Process();
            }

            float[] scanDistances = { 25f, 45f, 65f, 90f };

            bool isLaneChange = rp.LanesToRight > 0;
            Vector3 vehiclePos = vehicle.Position;
            Vector3 vehicleFwd = vehicle.ForwardVector;

            foreach (float dist in scanDistances)
            {
                Vector3 searchPos = vehiclePos + (vehicleFwd * dist);

                if (NativeFunction.Natives.GET_CLOSEST_VEHICLE_NODE_WITH_HEADING<bool>(searchPos, out Vector3 nodePos, out float nodeHeading, 1, 3.0f, 0))
                {
                    Vector3 nodeDir = MathHelper.ConvertHeadingToDirection(nodeHeading);

                    // flip direction if node is backwards
                    if (Vector3.Dot(nodeDir, vehicleFwd) < -0.2f)
                    {
                        nodeDir = -nodeDir;
                        nodeHeading = (nodeHeading + 180f) % 360f;
                    }

                    // cross product for right vector relative to road
                    Vector3 roadRight = Vector3.Cross(nodeDir, Vector3.WorldUp);
                    Vector3 candidatePos;

                    if (isLaneChange)
                    {
                        candidatePos = nodePos + (roadRight * 3.5f);
                    }
                    else
                    {
                        if (RoadUtilities.GetRoadBoundary(nodePos, nodeHeading, out Vector3 boundaryPos))
                        {
                            candidatePos = boundaryPos + (roadRight * 1.5f);
                        }
                        else
                        {
                            candidatePos = nodePos + (roadRight * 5.0f);
                        }
                    }

                    if (RoadUtilities.GetGroundPosition(candidatePos, out Vector3 grounded))
                    {
                        candidatePos = grounded;
                    }

                    if (IsYieldTargetValid(vehicle, candidatePos))
                    {
                        results.Add(candidatePos);
                    }
                }
            }

            return results;
        }

        /// <summary>
        /// Renders debug lines and markers for the current vehicle's intended path and potential yield spots.
        /// </summary>
        public void DrawDebug()
        {
            if (!EntryPoint.Settings.ShowDebugLines) return;
            if (!IsValid()) return;

            // Draw status arrow above vehicle
            Debug.DrawArrow(Vehicle.Position + Vector3.WorldUp * 2.0f, Vector3.WorldDown, Rotator.Zero, 1.0f, _debugColor);

            // Draw the locked target line if active
            if (_targetIsLocked)
            {
                Debug.DrawLine(Vehicle.Position, _stickyTarget, _debugColor);
            }

            // DRAW ALL VALID CANDIDATES (The "Rail")
            foreach (var node in _debugYieldCandidates)
            {
                // If this node is our selected target, draw it Green/Lime. Otherwise draw it Cyan.
                bool isSelected = _targetIsLocked && node.DistanceTo(_stickyTarget) < 1.0f;
                Color nodeColor = isSelected ? Color.Lime : Color.Cyan;

                // Draw a vertical post at the node position
                Debug.DrawLine(node, node + Vector3.WorldUp * 1.5f, nodeColor);

                // Draw a small horizontal cross to mark the ground spot clearly
                Debug.DrawLine(node + new Vector3(0.5f, 0, 0), node + new Vector3(-0.5f, 0, 0), nodeColor);
                Debug.DrawLine(node + new Vector3(0, 0.5f, 0), node + new Vector3(0, -0.5f, 0), nodeColor);
            }
        }

        /// <summary>
        /// Static validation to ensure a target position is actually on the road and safe for a vehicle.
        /// </summary>
        /// <param name="vehicle">The vehicle being moved.</param>
        /// <param name="target">The proposed destination coordinate.</param>
        /// <returns>True if the target is safe and valid.</returns>
        public static bool IsYieldTargetValid(Vehicle vehicle, Vector3 target)
        {
            if (target == Vector3.Zero || vehicle == null || !vehicle.Exists()) return false;

            Vector3 vecToTarget = target - vehicle.Position;

            // lateral dist check to make sure they actually pull over
            float lateralOffset = Vector3.Dot(vehicle.RightVector, vecToTarget);

            if (lateralOffset < 0.5f) return false;

            // TODO: may no longer be needed rare case of hwy nodes being next to eachother for different road names
            if (!IsTargetOnSameRoad(vehicle, target)) return false;

            // filter out spots with crazy elevation changes (issue w/ tunnel nodes)
            float zDiff = Math.Abs(target.Z - vehicle.Position.Z);
            if (zDiff > 3.0f) return false;

            return true;
        }

        /// <summary>
        /// Validates a target specifically for this instance's vehicle.
        /// </summary>
        /// <param name="target">The proposed destination coordinate.</param>
        /// <returns>True if valid.</returns>
        private bool ValidateYieldTarget(Vector3 target)
        {
            return IsYieldTargetValid(Vehicle, target);
        }

        /// <summary>
        /// Checks if a target coordinate is on the same named street and heading as the vehicle.
        /// </summary>
        /// <param name="vehicle">The vehicle to compare against.</param>
        /// <param name="targetPos">The coordinate to check.</param>
        /// <returns>True if the target is on the same road.</returns>
        private static bool IsTargetOnSameRoad(Vehicle vehicle, Vector3 targetPos)
        {
            var myStreet = World.GetStreetName(vehicle.Position);
            var targetStreet = World.GetStreetName(targetPos);

            if (!string.IsNullOrEmpty(myStreet) && !string.IsNullOrEmpty(targetStreet))
            {
                if (myStreet != targetStreet) return false;
            }

            if (NativeFunction.Natives.GET_CLOSEST_VEHICLE_NODE_WITH_HEADING<bool>(targetPos, out Vector3 nodePos, out float nodeHeading, 1, 3.0f, 0))
            {
                var nodeDir = MathHelper.ConvertHeadingToDirection(nodeHeading);
                if (Vector3.Dot(vehicle.ForwardVector, nodeDir) < -0.2f) return false;
            }

            return true;
        }

        /// <summary>
        /// Checks if the vehicle and its driver still exist and are alive.
        /// </summary>
        /// <returns>True if the tasker can still operate.</returns>
        public bool IsValid()
        {
            return Vehicle.Exists() && !Vehicle.IsDead && Vehicle.Driver.Exists() && !Vehicle.Driver.IsDead;
        }

        /// <summary>
        /// Returns a formatted string for debug displays showing vehicle name and current task status.
        /// </summary>
        /// <returns>A string with technical details about the tasker.</returns>
        public string GetDebugInfo()
        {
            if (!IsValid()) return "INVALID_VEHICLE";
            return $"~b~[{Vehicle.Model.Name}]~s~ {_debugStatus} | Spd:{(int)Vehicle.Speed}";
        }

        /// <summary>
        /// Checks a small radius around a point for any other vehicles.
        /// </summary>
        /// <param name="pos">The center point.</param>
        /// <param name="radius">The radius to check.</param>
        /// <returns>True if no other vehicles are found in the area.</returns>
        private bool IsPositionClear(Vector3 pos, float radius)
        {
            var ents = World.GetEntities(pos, radius, GetEntitiesFlags.ConsiderAllVehicles);
            foreach (var e in ents)
                if (e.Handle != Vehicle.Handle)
                    return false;

            return true;
        }

        /// <summary>
        /// Ensures the calculated target is actually in front of the vehicle to prevent the AI from trying to drive backward.
        /// </summary>
        /// <param name="target">The proposed target.</param>
        /// <returns>A target vector guaranteed to be in front of the vehicle.</returns>
        private Vector3 EnsureForwardTarget(Vector3 target)
        {
            var dirToTarget = target - Vehicle.Position;
            if (Vector3.Dot(Vehicle.ForwardVector, dirToTarget) < 2.0f)
                return Vehicle.Position + Vehicle.ForwardVector * 15.0f;
            return target;
        }

        /// <summary>
        /// Stops the custom tasking and clears the AI driver's tasks, unless they are currently being pulled over by the player.
        /// </summary>
        public void ReleaseControl()
        {
            if (IsValid())
            {
                bool isPulloverSuspect = false;

                if (LSPD_First_Response.Mod.API.Functions.IsPlayerPerformingPullover() || LSPD_First_Response.Mod.API.Functions.GetCurrentPullover() != null)
                {
                    LSPD_First_Response.Mod.API.LHandle pullover = LSPD_First_Response.Mod.API.Functions.GetCurrentPullover();
                    if (pullover != null)
                    {
                        Ped suspect = LSPD_First_Response.Mod.API.Functions.GetPulloverSuspect(pullover);
                        if (suspect != null && suspect.Exists() && suspect == Vehicle.Driver)
                        {
                            isPulloverSuspect = true;
                        }
                    }
                }

                if (!isPulloverSuspect && Vehicle.Driver != null && Vehicle.Driver.Exists())
                {
                    Vehicle.Driver.Tasks.Clear();
                }
            }

            _targetIsLocked = false;
            _stickyTarget = Vector3.Zero;
        }
    }
}