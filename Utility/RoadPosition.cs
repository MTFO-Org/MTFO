/*
Credit to TheMaybeast and PNWParksFan for much of the logic for road node detection and lane calculation was inspired by DLSv2. 
The original code can be found here: https://github.com/TheMaybeast/DLSv2
*/
using System;
using Rage;
using Rage.Native;

namespace MTFOv4
{
    /// <summary>
    /// Defines properties associated with vehicle nodes.
    /// </summary>
    [Flags]
    public enum NodeProperties
    {
        NONE = 0,
        offroad = 1,
        dead_end = 32,
        highway = 64,
        junction = 128,
        traffic_light = 256,
        stop_sign = 512,
    }

    /// <summary>
    /// Calculates and stores the road position, lane details, and node properties for a given entity.
    /// </summary>
    public class RoadPosition
    {
        /// <summary>
        /// Flags used when searching for vehicle nodes.
        /// </summary>
        [Flags]
        public enum NodeFlags
        {
            NONE = 0,
            INCLUDE_SWITCHED_OFF_NODES = 1,
            INCLUDE_BOAT_NODES = 2,
            IGNORE_SLIP_LANES = 4,
            IGNORE_SWITCHED_OFF_DEADENDS = 8
        }

        private const float ZMeasureMult = 10f;
        private const uint MinUpdateWait = 250;
        private const uint MaxUpdateWait = 60000;
        private const float MinMoveDist = 1f;
        private const float MinHeadingChange = 15f;
        private const NodeFlags DefaultNodeSearchFlags = NodeFlags.INCLUDE_SWITCHED_OFF_NODES;
        private const int TryNextNodes = 5;

        private float _abHeading;
        private float _baHeading;
        private float _distToPlane;
        private int _lanesAb;
        private int _lanesBa;
        private float _laneWidthAb;
        private float _laneWidthBa;
        private float _lastHeading;
        private Vector3 _lastLocation;
        private uint _lastUpdate;
        private float _median;
        private Vector3 _n;
        private float _nearestNodeHeading;
        private int _nearestNodeLanes;
        private Vector3 _nearestNodePos;
        private NodeProperties _nearestNodeProperties;
        private float _p;
        private Entity _targetEntity;

        /// <summary>
        /// Gets a value indicating whether the nearest node is flagged as a dead end.
        /// </summary>
        public bool IsDeadEnd => _nearestNodeProperties.HasFlag(NodeProperties.dead_end);

        /// <summary>
        /// Gets the total number of lanes on the current road segment.
        /// </summary>
        public int TotalLanes { get; private set; }

        /// <summary>
        /// Gets the number of lanes to the left of the entity's current position.
        /// </summary>
        public int LanesToLeft { get; private set; }

        /// <summary>
        /// Gets the number of lanes to the right of the entity's current position.
        /// </summary>
        public int LanesToRight { get; private set; }

        /// <summary>
        /// Gets the current lane number the entity is occupying.
        /// </summary>
        public int CurrentLane { get; private set; }

        /// <summary>
        /// Gets a value indicating whether the current road segment is a one-way road.
        /// </summary>
        public bool OneWayRoad { get; private set; }

        /// <summary>
        /// Gets a string representation of the current road type for debugging purposes.
        /// </summary>
        public string DebugRoadType
        {
            get
            {
                if (_nearestNodeProperties == NodeProperties.NONE) return "SCANNING";

                string info = "";

                if (_nearestNodeProperties.HasFlag(NodeProperties.highway)) info += "HWY";
                else if (_nearestNodeProperties.HasFlag(NodeProperties.offroad)) info += "OFFROAD";
                else info += "STREET";

                bool isIntersection = _nearestNodeProperties.HasFlag(NodeProperties.junction) ||
                                      _nearestNodeProperties.HasFlag(NodeProperties.traffic_light) ||
                                      _nearestNodeProperties.HasFlag(NodeProperties.stop_sign);

                if (isIntersection) info += " (INT)";
                if (_nearestNodeProperties.HasFlag(NodeProperties.dead_end)) info += " (DEADEND)";

                return info;
            }
        }

        /// <summary>
        /// Initializes a new instance of the RoadPosition class. Setting the target entity to track.
        /// </summary>
        /// <param name="entity">The entity to track.</param>
        public RoadPosition(Entity entity)
        {
            SetTarget(entity);
        }

        /// <summary>
        /// Sets the target entity for road position tracking.
        /// </summary>
        /// <param name="entity">The entity to track.</param>
        public void SetTarget(Entity entity)
        {
            _targetEntity = entity;
        }

        /// <summary>
        /// Processes the current road position, updating lane information and road properties based on the entity's location.
        /// </summary>
        public void Process()
        {
            if (_targetEntity == null || !_targetEntity.Exists()) return;

            uint timeSinceUpdate = Game.GameTime - _lastUpdate;
            if (timeSinceUpdate < MinUpdateWait) return;

            Vector3 pos = _targetEntity.Position;
            float heading = _targetEntity.Heading;

            if (pos.DistanceTo(_lastLocation) < MinMoveDist && Math.Abs(NormalizeHeadingDiff(_lastHeading, heading, false)) < MinHeadingChange && timeSinceUpdate < MaxUpdateWait) return;

            if (!GetNearestNode(pos, DefaultNodeSearchFlags, 0, out _nearestNodePos, out _nearestNodeHeading, out _nearestNodeLanes, ZMeasureMult)) return;
            if (!GetNodeProperties(_nearestNodePos, out _nearestNodeProperties)) return;

            if (!GetRoadSegment(_nearestNodePos, 0, _nearestNodeLanes, !DefaultNodeSearchFlags.HasFlag(NodeFlags.INCLUDE_SWITCHED_OFF_NODES), out Vector3 nodePosA, out Vector3 nodePosB, out _lanesBa, out _lanesAb, out _median)) return;

            if (TryNextNodes > 0)
            {
                Vector3 dirNearest = MathHelper.ConvertHeadingToDirection(_nearestNodeHeading).ToNormalized();
                Vector3 testPosFwd = _nearestNodePos + (dirNearest * 2f);
                Vector3 testPosBack = _nearestNodePos - (dirNearest * 2f);
                bool posCloserToFwd = pos.DistanceTo2D(testPosFwd) < pos.DistanceTo2D(testPosBack);
                bool segCloserToFwd = Math.Max(nodePosB.DistanceTo2D(testPosFwd), nodePosA.DistanceTo2D(testPosFwd)) < Math.Max(nodePosB.DistanceTo2D(testPosBack), nodePosA.DistanceTo2D(testPosBack));

                if (posCloserToFwd != segCloserToFwd)
                {
                    for (int n = 1; n <= TryNextNodes; n++)
                    {
                        GetNearestNode(pos, DefaultNodeSearchFlags, n, out Vector3 newSegStartPos, out _, out _, ZMeasureMult);
                        GetRoadSegment(newSegStartPos, 0, _nearestNodeLanes, !DefaultNodeSearchFlags.HasFlag(NodeFlags.INCLUDE_SWITCHED_OFF_NODES), out Vector3 newNodePosA, out Vector3 newNodePosB, out int newLanesBa, out int newLanesAb, out float newMedianWidth);

                        if ((newNodePosA == _nearestNodePos || newNodePosB == _nearestNodePos) && !(newNodePosA == nodePosA && newNodePosB == nodePosB))
                        {
                            nodePosA = newNodePosA;
                            nodePosB = newNodePosB;
                            _lanesAb = newLanesAb;
                            _lanesBa = newLanesBa;
                            _median = newMedianWidth;
                            break;
                        }
                    }
                }
            }

            Vector3 dirAb = nodePosB - nodePosA;
            Vector3 dirBa = nodePosA - nodePosB;
            _abHeading = MathHelper.NormalizeHeading(MathHelper.ConvertDirectionToHeading(dirAb));
            _baHeading = MathHelper.NormalizeHeading(MathHelper.ConvertDirectionToHeading(dirBa));

            _lastUpdate = Game.GameTime;
            _lastLocation = pos;
            _lastHeading = heading;

            bool gotBoundaryA = RoadUtilities.GetRoadBoundary(nodePosA, _abHeading, out Vector3 boundaryPosA);
            bool gotBoundaryB = RoadUtilities.GetRoadBoundary(nodePosB, _baHeading, out Vector3 boundaryPosB);

            _laneWidthAb = 5.4f;
            _laneWidthBa = 5.4f;
            Vector3 normAb, normBa;

            if (gotBoundaryA)
            {
                _laneWidthAb = _lanesAb > 0 ? (boundaryPosA.DistanceTo(nodePosA) - 0.5f * _median) / _lanesAb : 0;
                normAb = (boundaryPosA - nodePosA).ToNormalized() * _laneWidthAb;
            }
            else
            {
                normAb = MathHelper.ConvertHeadingToDirection(MathHelper.RotateHeading(_abHeading, -90)).ToNormalized() * _laneWidthAb;
                boundaryPosA = nodePosA + (normAb * _lanesAb);
            }

            if (gotBoundaryB)
            {
                _laneWidthBa = _lanesBa > 0 ? (boundaryPosB.DistanceTo(nodePosB) - 0.5f * _median) / _lanesBa : 0;
                normBa = (boundaryPosB - nodePosB).ToNormalized() * _laneWidthBa;
            }
            else
            {
                normBa = MathHelper.ConvertHeadingToDirection(MathHelper.RotateHeading(_baHeading, -90)).ToNormalized() * _laneWidthBa;
                boundaryPosB = nodePosB + (normBa * _lanesBa);
            }

            Vector3 medNormAb = normAb.ToNormalized() * (_median / 2);
            Vector3 medNormBa = normBa.ToNormalized() * (_median / 2);
            Vector3 medianAbOrigin = nodePosA + medNormAb;

            OneWayRoad = _lanesAb == 0 || _lanesBa == 0;
            if (OneWayRoad)
            {
                _laneWidthAb *= 2;
                _laneWidthBa *= 2;
                normAb *= 2;
                normBa *= 2;

                if (_lanesAb > 0)
                {
                    medianAbOrigin = nodePosA - (normAb * 0.5f * _lanesAb) + (medNormAb * 2);
                    _laneWidthBa = 0;
                }
                else
                {
                    medianAbOrigin = nodePosB - (normBa * 0.5f * _lanesBa) + (medNormBa * 2);
                    _laneWidthAb = 0;
                }
            }

            float nodeHeadingA, nodeHeadingB;
            Vector3 nearestBoundaryPos;
            if (nodePosA.DistanceTo2D(_nearestNodePos) < nodePosB.DistanceTo2D(_nearestNodePos))
            {
                nodeHeadingA = _nearestNodeHeading;
                nearestBoundaryPos = boundaryPosA;
                GetNearestNode(nodePosB, DefaultNodeSearchFlags, out _, out nodeHeadingB);
            }
            else
            {
                nodeHeadingB = _nearestNodeHeading;
                nearestBoundaryPos = boundaryPosB;
                GetNearestNode(nodePosA, DefaultNodeSearchFlags, out _, out nodeHeadingA);
            }

            float headingDiffA = NormalizeHeadingDiff(_abHeading, nodeHeadingA);
            float headingDiffB = NormalizeHeadingDiff(_baHeading, nodeHeadingB);
            float minHeadingDiff = Math.Min(headingDiffA, headingDiffB);
            bool segmentOk = minHeadingDiff <= 10 && (_lanesAb + _lanesBa) == _nearestNodeLanes;

            if (segmentOk)
            {
                Vector3 planeOrigin = OneWayRoad ? medianAbOrigin : nodePosA;
                Vector3 planeNorm = OneWayRoad && _lanesBa > 0 ? normBa : normAb;
                if (!OneWayRoad && normAb == Vector3.Zero) planeNorm = -normBa;

                CalculatePlane(planeOrigin, dirAb, planeNorm);
                UpdateFinalPosition();
                return;
            }

            HandleFallbackGeometry(nearestBoundaryPos);
        }

        /// <summary>
        /// Handles geometry calculations when standard road segment detection fails.
        /// </summary>
        /// <param name="boundaryRef">The reference boundary position.</param>
        private void HandleFallbackGeometry(Vector3 boundaryRef)
        {
            float fullWidth = boundaryRef.DistanceTo2D(_nearestNodePos) * 2;
            float guessLaneWidth = Math.Round(fullWidth / 5.4f, 1) % 1 != 0 && Math.Round(fullWidth / 4f, 1) % 1 == 0 ? 4f : 5.4f;
            int guessLanes = (int)Math.Round(fullWidth / guessLaneWidth);

            OneWayRoad = true;
            Vector3 guessDir = MathHelper.ConvertHeadingToDirection(_nearestNodeHeading).ToNormalized() * 20f;
            Vector3 guessNorm = MathHelper.ConvertHeadingToDirection(MathHelper.RotateHeading(_nearestNodeHeading, -90)).ToNormalized() * guessLaneWidth;
            Vector3 medianOrigin = _nearestNodePos - (guessNorm * 0.5f * guessLanes);

            _lanesAb = guessLanes;
            _laneWidthAb = guessLaneWidth;
            _lanesBa = 0;
            _laneWidthBa = 0;
            _median = 0;
            _abHeading = _nearestNodeHeading;
            _baHeading = -_nearestNodeHeading;

            CalculatePlane(medianOrigin, guessDir, guessNorm);
            UpdateFinalPosition();
        }

        /// <summary>
        /// Updates the final calculated lane positions based on plane distance.
        /// </summary>
        private void UpdateFinalPosition()
        {
            _distToPlane = Vector3.Dot(_n, _lastLocation) + _p;

            float laneWidth = 0;
            int lanes = 0;
            float lanePos = 0;

            if (OneWayRoad)
            {
                laneWidth = _lanesAb > 0 ? _laneWidthAb : _laneWidthBa;
                lanes = _lanesAb > 0 ? _lanesAb : _lanesBa;

                lanePos = Math.Max(_distToPlane, 0) / laneWidth;
            }
            else
            {
                if (_distToPlane >= 0)
                {
                    laneWidth = _laneWidthAb;
                    lanes = _lanesAb;
                }
                else
                {
                    laneWidth = _laneWidthBa;
                    lanes = _lanesBa;
                }

                lanePos = (Math.Abs(_distToPlane) - (_median / 2)) / laneWidth;
            }

            TotalLanes = _lanesAb + _lanesBa;
            float clampedLanePos = MathHelper.Clamp(lanePos, 0, lanes);

            LanesToLeft = (int)Math.Floor(clampedLanePos);
            LanesToRight = lanes - (int)Math.Ceiling(clampedLanePos);
            CurrentLane = (int)Math.Floor(clampedLanePos) + 1;
        }

        /// <summary>
        /// Calculates the normal and distance for a geometric plane used in lane detection.
        /// </summary>
        /// <param name="origin">The origin point of the plane.</param>
        /// <param name="dir1">The first direction vector.</param>
        /// <param name="dir2">The second direction vector.</param>
        private void CalculatePlane(Vector3 origin, Vector3 dir1, Vector3 dir2)
        {
            Vector3 n1 = Vector3.Cross(dir1, dir2).ToNormalized();
            _n = Vector3.Cross(n1, dir1).ToNormalized();
            _p = -Vector3.Dot(_n, origin);
        }

        /// <summary>
        /// Normalizes the difference between two headings.
        /// </summary>
        /// <param name="heading1">The first heading.</param>
        /// <param name="heading2">The second heading.</param>
        /// <param name="ignoreDirection">Whether to ignore the direction of the difference.</param>
        /// <returns>The normalized heading difference.</returns>
        private float NormalizeHeadingDiff(float heading1, float heading2, bool ignoreDirection = true)
        {
            float diff = (MathHelper.NormalizeHeading(heading1) - MathHelper.NormalizeHeading(heading2)) % (ignoreDirection ? 180 : 360);
            if (ignoreDirection) diff = Math.Abs(diff);
            if (ignoreDirection && diff > 90) diff = Math.Abs(diff - 180);
            return diff;
        }

        /// <summary>
        /// Retrieves the properties of a vehicle node at the specified coordinates.
        /// </summary>
        /// <param name="coords">The coordinates to check.</param>
        /// <param name="properties">The resulting node properties.</param>
        /// <returns>True if the properties were successfully retrieved; otherwise, false.</returns>
        public static bool GetNodeProperties(Vector3 coords, out NodeProperties properties)
        {
            bool result = NativeFunction.Natives.GET_VEHICLE_NODE_PROPERTIES<bool>(coords, out int density, out int iNodeProperties);
            properties = (NodeProperties)iNodeProperties;
            return result;
        }

        /// <summary>
        /// Retrieves the Nth closest vehicle node to the specified coordinates.
        /// </summary>
        /// <param name="coords">The coordinates to search from.</param>
        /// <param name="searchMode">The search flags to apply.</param>
        /// <param name="n">The Nth node to retrieve.</param>
        /// <param name="nodePosition">The resulting node position.</param>
        /// <param name="nodeHeading">The resulting node heading.</param>
        /// <param name="numLanes">The resulting number of lanes.</param>
        /// <param name="zMeasureMult">The Z-axis multiplier for the search.</param>
        /// <returns>True if a node was found; otherwise, false.</returns>
        public static bool GetNearestNode(Vector3 coords, NodeFlags searchMode, int n, out Vector3 nodePosition, out float nodeHeading, out int numLanes, float zMeasureMult = 3.0f)
        {
            return NativeFunction.Natives.GET_NTH_CLOSEST_VEHICLE_NODE_WITH_HEADING<bool>(coords, n, out nodePosition, out nodeHeading, out numLanes, (int)searchMode, zMeasureMult, 0.0f);
        }

        /// <summary>
        /// Retrieves the closest vehicle node to the specified coordinates.
        /// </summary>
        /// <param name="coords">The coordinates to search from.</param>
        /// <param name="searchMode">The search flags to apply.</param>
        /// <param name="nodePosition">The resulting node position.</param>
        /// <param name="nodeHeading">The resulting node heading.</param>
        /// <returns>True if a node was found; otherwise, false.</returns>
        public static bool GetNearestNode(Vector3 coords, NodeFlags searchMode, out Vector3 nodePosition, out float nodeHeading)
        {
            return NativeFunction.Natives.GET_CLOSEST_VEHICLE_NODE_WITH_HEADING<bool>(coords, out nodePosition, out nodeHeading, (int)searchMode);
        }

        /// <summary>
        /// Retrieves information about the closest road segment.
        /// </summary>
        /// <param name="coords">The coordinates to search from.</param>
        /// <param name="minLength">The minimum length of the road segment.</param>
        /// <param name="minLanes">The minimum number of lanes.</param>
        /// <param name="ignoreDisabledNodes">Whether to ignore disabled nodes.</param>
        /// <param name="nodePosA">The starting position of the road segment.</param>
        /// <param name="nodePosB">The ending position of the road segment.</param>
        /// <param name="lanesBtoA">The number of lanes from B to A.</param>
        /// <param name="lanesAtoB">The number of lanes from A to B.</param>
        /// <param name="medianWidth">The width of the median.</param>
        /// <returns>True if a road segment was found; otherwise, false.</returns>
        public static bool GetRoadSegment(Vector3 coords, float minLength, int minLanes, bool ignoreDisabledNodes, out Vector3 nodePosA, out Vector3 nodePosB, out int lanesBtoA, out int lanesAtoB, out float medianWidth)
        {
            return NativeFunction.Natives.GET_CLOSEST_ROAD<bool>(coords, minLength, minLanes, out nodePosA, out nodePosB, out lanesBtoA, out lanesAtoB, out medianWidth, ignoreDisabledNodes);
        }
    }

    /// <summary>
    /// Provides utility methods for road-related calculations.
    /// </summary>
    public static class RoadUtilities
    {
        /// <summary>
        /// Attempts to find the true ground position for a given set of coordinates.
        /// </summary>
        /// <param name="position">The coordinates to check.</param>
        /// <param name="groundPos">The resulting ground coordinates.</param>
        /// <returns>True if a ground position was found; otherwise, false.</returns>
        public static bool GetGroundPosition(Vector3 position, out Vector3 groundPos)
        {
            if (NativeFunction.Natives.GET_GROUND_Z_FOR_3D_COORD<bool>(position.X, position.Y, position.Z + 5.0f, out float z, false))
            {
                groundPos = new Vector3(position.X, position.Y, z);
                return true;
            }

            HitResult hit = World.TraceLine(new Vector3(position.X, position.Y, position.Z + 50f), new Vector3(position.X, position.Y, position.Z - 50f), TraceFlags.IntersectWorld);
            if (hit.Hit)
            {
                groundPos = hit.HitPosition;
                return true;
            }

            groundPos = position;
            return false;
        }

        /// <summary>
        /// Attempts to find the road boundary for a given node position and heading.
        /// </summary>
        /// <param name="nodePos">The node position.</param>
        /// <param name="nodeHeading">The heading of the node.</param>
        /// <param name="roadBoundaryPos">The resulting road boundary coordinates.</param>
        /// <returns>True if a boundary was found; otherwise, false.</returns>
        public static bool GetRoadBoundary(Vector3 nodePos, float nodeHeading, out Vector3 roadBoundaryPos)
        {
            return NativeFunction.Natives.GET_ROAD_BOUNDARY_USING_HEADING<bool>(nodePos, nodeHeading, out roadBoundaryPos);
        }
    }
}