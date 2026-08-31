using System.Collections.Generic;

namespace MineVent.Domain
{
    public class Loop
    {
        public int Id;

        // Each entry: an airway id + the traversal sign for this loop
        // (+1 if traversed start->end matches the loop's chosen direction, -1 if opposite)
        public List<(int AirwayId, int Sign)> AirwaySigns;

        public Loop(int id, List<(int AirwayId, int Sign)> airwaySigns)
        {
            Id = id;
            AirwaySigns = airwaySigns;
        }
    }
}