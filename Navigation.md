Player places a miner anywhere in the tunnel network.

The system immediately calculates and displays the safest possible escape route to a designated exit, taking into account:

Blocked segments (debris)
Current methane levels
Tunnel length


Core Components

Component,Responsibility
Miner Placement,"Click → place human prefab, snap to nearest node"
Graph,Existing node + edge network (already built)
Cost Model,Every edge has a dynamic traversal cost
Pathfinder,Finds lowest-cost path from miner’s node to Exit node
Visual Path,"Clear, blue line showing the route"
Exit Nodes,*One or more* designated safe exits (Node_end or similar)



Placement Behaviour

Click the Navigation button → enter placement mode
Left-click snaps to the edge (similar to leak/debris)
Spawns the human prefab at that place
Do not trigger path calculation immediately. 
A "navigation start" button appears
When clicked on the button, path is calculated and drawn instantly
If no path exists → show a clear "No safe route" warning.





Cost Model (what makes a route “safe”)
Each edge receives a cost based on:

Base cost = physical length of the tunnel segment
Methane penalty
Low CH₄ → small or zero penalty
Medium CH₄ → moderate penalty
High CH₄ → very high penalty
Above critical threshold → effectively infinite (avoid completely)

Blocked (debris) → infinite cost (path cannot use it)




Path Calculation

Classic graph search (Dijkstra or A*) on your existing node/edge graph
Starts at the miner’s current node
Ends at the designated Exit node
Respects blocked edges and gas costs
Returns an ordered list of nodes

If no path exists → show a clear “No safe route” warning.



Interaction Flow

User clicks Navigation button
Cursor changes or a placement ghost appears
User left-clicks in a tunnel
Miner appears at the tunnel
A "navigation start" button appears
When clicked on the button, path is calculated and drawn instantly
If no path exists → show a clear "No safe route" warning.