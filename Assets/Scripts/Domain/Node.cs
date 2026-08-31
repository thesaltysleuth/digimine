using System.Collections.Generic;
using UnityEngine;

namespace MineVent.Domain
{
    public class Node
    {
        public int Id;
        public Vector3 Position;              // for 3D visualization & future elevation-based NVP calcs
        public List<int> ConnectedAirwayIds;  // ids into MineNetwork's airway collection

        public bool IsBoundary;               // true if this node connects to surface/atmosphere
        public float FixedPressure;           // reference pressure if boundary (typically 0 = atmospheric)

        public Node(int id, Vector3 position, bool isBoundary = false, float fixedPressure = 0f)
        {
            Id = id;
            Position = position;
            ConnectedAirwayIds = new List<int>();
            IsBoundary = isBoundary;
            FixedPressure = fixedPressure;
        }

        public void RegisterAirway(int airwayId)
        {
            if (!ConnectedAirwayIds.Contains(airwayId))
                ConnectedAirwayIds.Add(airwayId);
        }
    }
}