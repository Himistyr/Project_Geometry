using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GeometryDetection
{
    // Utilizes the already detected geometry and only tries to find holes or tunnels
    public class HoleAndTunnelDetector : MonoBehaviour
    {
        #region Variables
        [Header("Detection Parameters")]
        [SerializeField] private float _maxDiameter = 10;
        public float MaxDiameter 
        { 
            get { return _maxDiameter; } 
            set { _maxDiameter = value; }
        }

        [SerializeField] private int _minimumSuccesfullChecks = 4;
        public int MinimumSuccesfullChecks
        {
            get { return _minimumSuccesfullChecks; }
            set { _minimumSuccesfullChecks = value; }
        }

        [Header("Visualization Parameters")]
        [SerializeField] private bool _visualizeTunnels = true;
        public bool VisualizeTunnels
        {
            get { return _visualizeTunnels; }
            set { _visualizeTunnels = value;}
        }

        [SerializeField] private bool _visualizeExits = true;
        public bool VisualizeExits
        {
            get { return _visualizeExits; }
            set { _visualizeExits = value; }
        }

        [SerializeField] private Material _tunnelMaterial = null;
        public Material TunnelMaterial
        {
            get { return _tunnelMaterial; }
        }

        [SerializeField] private Material _exitMaterial = null;
        public Material ExitMaterial
        {
            get { return _exitMaterial; }
        }

        private GeometryDetector _geometryDetector;
        public GeometryDetector GeometryDetector 
        { 
            get { return _geometryDetector; } 
        }

        private List<DetectedGeometry> _holes = new List<DetectedGeometry>();
        public List<DetectedGeometry> Holes
        {
            get { return _holes; }
            private set { _holes = value; }
        }

        private List<DetectedGeometry> _tunnels = new List<DetectedGeometry>();
        public List<DetectedGeometry> Tunnels
        {
            get { return _tunnels; }
            private set { _tunnels = value; }
        }
        #endregion

        private void Start()
        {
            _geometryDetector = GetComponent<GeometryDetector>();
            if (!_geometryDetector)
                Debug.LogError("HoleDetector: No GeometryDetector has been found on this object!");
        }

        public void DetectHoles()
        {
            // Create new wrapper class that stores Nodes and extra information unique to the hole detector
            List<GeometryNode> geometry = GeometryDetector.EmptyNodes;
            Debug.Log("Pulled in data.");

            // Start detecting the holes.
            foreach (GeometryNode node in geometry)
            {
                List<GeometryNode> potentialExits = new List<GeometryNode>();
                DetectedGeometry detectedGeometry = new DetectedGeometry(new List<GeometryNode>());
                DetectTunnelOrHole(node, ref detectedGeometry, potentialExits);

                if (detectedGeometry.Nodes.Count > 0)
                {
                    // Add the exit nodes to the detected geometry
                    foreach (GeometryNode exit in potentialExits)
                    {
                        DetectIfPartOfExit(exit);
                    }

                    foreach (GeometryType geoType in detectedGeometry.MarkedGeometry)
                    {
                        switch (geoType)
                        {
                            case GeometryType.Hole:
                                Holes.Add(detectedGeometry);
                                break;
                            case GeometryType.Tunnel:
                                Tunnels.Add(detectedGeometry);
                                break;
                        }
                    }
                }

                
            }

            

            VisualizeData();
            Debug.Log("Finished Tunnel Detection with " + Tunnels.Count + " tunnels and " + Holes.Count + " holes.");
        }

        #region DirectionalChecks
        private void DetectTunnelOrHole(GeometryNode node, ref DetectedGeometry detectedGeometry, List<GeometryNode> potentialExits)
        {
            if (node.ContainsGeometry
                || node.MarkedAs.Contains(GeometryType.Hole)
                || node.MarkedAs.Contains(GeometryType.Tunnel))
            {
                return;
            }

            bool isNeighbor = false;
            int neighboringHits = 0;
            DetectionInfo TerrainChecks = new DetectionInfo();

            // Check Top for terrain.
            TerrainChecks.UpHit = CheckDirectionForTerrain(NeighborDirection.Up, node, out isNeighbor);
            neighboringHits += isNeighbor ? 1 : 0;

            // Check Bottom for terrain.
            TerrainChecks.DownHit = CheckDirectionForTerrain(NeighborDirection.Down, node, out isNeighbor);
            neighboringHits += isNeighbor ? 1 : 0;

            // Check Left for terrain.
            TerrainChecks.LeftHit = CheckDirectionForTerrain(NeighborDirection.Left, node, out isNeighbor);
            neighboringHits += isNeighbor ? 1 : 0;

            // Check Right for terrain.
            TerrainChecks.RightHit = CheckDirectionForTerrain(NeighborDirection.Right, node, out isNeighbor);
            neighboringHits += isNeighbor ? 1 : 0;

            // Check Front for terrain.
            TerrainChecks.FrontHit = CheckDirectionForTerrain(NeighborDirection.Front, node, out isNeighbor);
            neighboringHits += isNeighbor ? 1 : 0;

            // Check Back for terrain.
            TerrainChecks.BackHit = CheckDirectionForTerrain(NeighborDirection.Back, node, out isNeighbor);
            neighboringHits += isNeighbor ? 1 : 0;

            // To speed up the checks for the exits, we save the nodes that directly neighbor geometry on 2 sides.
            if (neighboringHits == 2)
            {
                potentialExits.Add(node);
            }

            if (CheckGeometry(ref detectedGeometry, TerrainChecks))
            {
                detectedGeometry.Nodes.Add(node);
                foreach (GeometryType GeoType in detectedGeometry.MarkedGeometry)
                {
                    node.MarkedAs.Add(GeoType);
                }

                List<NeighborInfo> neighbors = node.GetNeighbors();
                foreach (NeighborInfo neighbor in neighbors)
                {
                    DetectTunnelOrHole(neighbor.Neighbor, ref detectedGeometry, potentialExits);
                }
            }
        }

        private bool CheckDirectionForTerrain(NeighborDirection direction, GeometryNode node, out bool foundOnFirstIteration)
        {
            List<NeighborInfo> neighbors = node.GetNeighbors(direction);

            foreach (NeighborInfo neighbor in neighbors)
            {
                // We only check if we are already marked as hole or tunnel AFTER we check our neighbor.
                // This is needed to make sure this node is not a potential exit!!
                if (neighbor.Neighbor.ContainsGeometry
                    || CheckDirectionForTerrain(direction, neighbor.Neighbor, out foundOnFirstIteration))
                {
                    // If the first iteration instantly finds geometry, it will return true.
                    // It doesn't matter if geometry is found in the recursion as it will always bubble back up to the first iteration.
                    foundOnFirstIteration = neighbor.Neighbor.ContainsGeometry;
                    return true;
                }
            }

            foundOnFirstIteration = false;
            return false;
        }

        private void DetectIfPartOfExit(GeometryNode node, bool failedBefore = false)
        {
            if ((!node.MarkedAs.Contains(GeometryType.Hole) && !node.MarkedAs.Contains(GeometryType.Tunnel))
                || node.MarkedAs.Contains(GeometryType.Exit))
                return;

            DetectionInfo exitChecks = new DetectionInfo();
            List<NeighborInfo> neighborsToCheck = new List<NeighborInfo>();

            exitChecks.UpHit = CheckNeighborsForTunnelOrHole(NeighborDirection.Up, node, neighborsToCheck);
            exitChecks.DownHit = CheckNeighborsForTunnelOrHole(NeighborDirection.Down, node, neighborsToCheck);
            exitChecks.LeftHit = CheckNeighborsForTunnelOrHole(NeighborDirection.Left, node, neighborsToCheck);
            exitChecks.RightHit = CheckNeighborsForTunnelOrHole(NeighborDirection.Right, node, neighborsToCheck);
            exitChecks.FrontHit = CheckNeighborsForTunnelOrHole(NeighborDirection.Front, node, neighborsToCheck);
            exitChecks.BackHit = CheckNeighborsForTunnelOrHole(NeighborDirection.Back, node, neighborsToCheck);

            // When we find Geometry or a Node with the Tunnel or Hole mark in all but one direction, we have an exit.
            if (exitChecks.SuccesfullChecks == 4
                || exitChecks.SuccesfullChecks == 5)
            {
                node.MarkedAs.Add(GeometryType.Exit);

                foreach (NeighborInfo neighbor in neighborsToCheck)
                {
                    DetectIfPartOfExit(neighbor.Neighbor);
                }
            }
            else if (!failedBefore)
            {
                foreach (NeighborInfo neighbor in neighborsToCheck)
                {
                    DetectIfPartOfExit(neighbor.Neighbor, true);
                }
            }
        }

        private bool CheckNeighborsForTunnelOrHole(NeighborDirection direction, GeometryNode node, List<NeighborInfo> neighborsToCheck)
        {
            List<NeighborInfo> neighbors = node.GetNeighbors(direction);

            foreach (NeighborInfo neighbor in neighbors)
            {
                // If we find Geometry or a Node with the Hole or Tunnel mark, we need to continue looking in this direction later on.
                if (neighbor.Neighbor.ContainsGeometry
                    || neighbor.Neighbor.MarkedAs.Contains(GeometryType.Hole)
                    || neighbor.Neighbor.MarkedAs.Contains(GeometryType.Tunnel))
                {
                    neighborsToCheck.AddRange(neighbors);
                    return true;
                }
            }

            return false;
        }
        #endregion
        #region GeometryChecks
        private bool CheckGeometry(ref DetectedGeometry detectedGeometry, DetectionInfo detectedTerrain)
        {
            if (detectedTerrain.SuccesfullChecks < MinimumSuccesfullChecks)
                return false;

            //DetectExits( ref detectedGeometry);
            bool succesfull = HoleCheck(ref detectedGeometry, detectedTerrain);
            succesfull = TunnelCheck(ref detectedGeometry, detectedTerrain) || succesfull;

            return succesfull;
        }

        private bool TunnelCheck(ref DetectedGeometry detectedGeometry, DetectionInfo detectedTerrain)
        {
            if (detectedTerrain.SuccesfullChecks == 4)
            {
                detectedGeometry.MarkedGeometry.Add(GeometryType.Tunnel);
                return true;
            }

            return false;
        }


        private bool HoleCheck(ref DetectedGeometry detectedGeometry, DetectionInfo detectedTerrain)
        {
            if (detectedTerrain.SuccesfullChecks > 4)
            {
                detectedGeometry.MarkedGeometry.Add(GeometryType.Hole);
                return true;
            }

            return false;
        }
        #endregion
        #region Visualization
        private void VisualizeData()
        {
            HashSet<DetectedGeometry> allDetected = new HashSet<DetectedGeometry>();
            foreach(DetectedGeometry tunnel in Tunnels)
            {
                allDetected.Add(tunnel);
            }

            foreach (DetectedGeometry hole in Holes)
            {
                allDetected.Add(hole);
            }

            foreach (DetectedGeometry geo in allDetected)
            {
                foreach (GeometryNode node in geo.Nodes)
                {
                    if (VisualizeExits
                && node.MarkedAs.Contains(GeometryType.Exit))
                    {
                        node.NodeRenderer.sharedMaterial = ExitMaterial;
                    }
                    else if (VisualizeTunnels
                        && (node.MarkedAs.Contains(GeometryType.Hole) || node.MarkedAs.Contains(GeometryType.Tunnel)))
                    {
                        node.NodeRenderer.sharedMaterial = TunnelMaterial;
                    }
                }
            }
        }
        #endregion
    }
}