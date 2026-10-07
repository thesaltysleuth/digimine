# DigiMine — Project Description

## What It Is

DigiMine is a Unity 3D application (version 0.1.0, by "rooklabs") that simulates underground mine ventilation as an interactive digital twin. The mine is modeled as a **graph network**: tunnel junctions are **Nodes**, tunnel segments (airways) are **Edges**. A numerical solver computes airflow pressure, volumetric flow rate, and methane gas concentration across the network in real-time whenever the user changes something.

It has a WebGL build for browser access and runs in the Unity Editor during development. There are 3 scenes: `SampleScene`, `MainScene`, and `VisualScene`.

---

## What It Does — Feature by Feature

### 1. Ventilation Network Solver ([Solver.cs](file:///d:/Projects/digimine/Assets/Scripts/Solver.cs))

Solves the entire mine ventilation network iteratively:

- **Nodal pressure calculation** using Successive Over-Relaxation (SOR) with ω = 1.4, tolerance 0.0001, max 2000 iterations. Each non-fixed node's pressure converges to the conductance-weighted average of its neighbors: `P_new = lerp(P_old, Σ(P_neighbor / R_edge) / Σ(1/R_edge), ω)`.
- **Flow rate on each edge**: `Q = |ΔP| / R`, where R is the edge's resistance value. Flow direction is assigned as `A_To_B`, `B_To_A`, or `Static` based on the sign of the pressure difference.
- **Nodal airflow**: Computes total inflow and outflow per node by summing contributing edge flows.
- **Methane advection**: Nodes are sorted by descending pressure. For each non-fixed-CH₄ node, concentration is computed as a flow-weighted average of upstream neighbor concentrations plus any local methane generation: `CH₄ = (Σ(Q_in × CH₄_upstream) + (gen_rate × 100)) / (Σ(Q_in) + gen_rate)`.

The solver treats blocked edges (debris) and closed doors as zero-flow — they are skipped entirely during pressure relaxation and assigned `flowRate = 0`.

### 2. Graph Structure

- **[Node.cs](file:///d:/Projects/digimine/Assets/Scripts/Node.cs)** (316 lines): Each node stores pressure (Pa), CH₄ concentration (%), methane generation rate (m³/s), total inflow/outflow, and boundary flags (`isFixedPressure`, `isFixedCh4`, `isExit`). Each node spawns a 3D info icon (from `Assets/objects/info.prefab`) with a SphereCollider for click interaction. It also creates a floating info panel (dark background cube + TextMeshPro label) showing pressure, airflow, and CH₄ with color-coded text:
  - Green `#00E676` for CH₄ < 0.75%
  - Yellow `#FFEA00` for 0.75% ≤ CH₄ ≤ 1.25%
  - Red `#FF1744` for CH₄ > 1.25%

  Clicking the node icon toggles the info panel. A static method `SetAllNodeIconCollidersEnabled()` disables all node colliders during placement modes to prevent accidental info toggles.

- **[Edge.cs](file:///d:/Projects/digimine/Assets/Scripts/Edge.cs)** (33 lines): Simple data class — two Node references, resistance (default 1.0), optional Door reference, calculated flow rate, flow direction, blocking state, debris instance/position, and LineRenderer references (main + downstream split).

- **[GraphManager.cs](file:///d:/Projects/digimine/Assets/Scripts/GraphManager.cs)** (347 lines): Central orchestrator. Holds the lists of all nodes and edges. On `RunSolverAndRender()`: runs the solver, updates all node labels, then sets up LineRenderers on every edge. Edges with flow get animated arrow textures (green/yellow/red based on CH₄ thresholds 0.75% and 1.25%). Edges with zero flow get a static grey material. Blocked edges get split rendering — upstream LineRenderer from the upstream node to the debris position (with arrows if flow exists), downstream LineRenderer from debris to downstream node (always grey, no arrows). Arrow animation speed in `Update()` is proportional to flow rate.

### 3. Methane Leak Placement ([LeakPlacementManager.cs](file:///d:/Projects/digimine/Assets/Scripts/LeakPlacementManager.cs), [MethaneLeak.cs](file:///d:/Projects/digimine/Assets/Scripts/MethaneLeak.cs))

- User clicks a "Leak" UI button to enter placement mode.
- Mouse raycast hits the ground plane; the system finds the nearest edge within `maxSnapDistance` (25 units) and projects the mouse position onto the edge line.
- A preview object follows the cursor with a yellow snap indicator sphere.
- On left-click, a `MethaneLeak` prefab (`Assets/objects/MethaneLeak.prefab`) is spawned at the snapped position. It finds the nearest node and adds its `emissionRate` (default 0.5 m³/s) to that node's `methaneGenerationRate`.
- The solver re-runs immediately — methane propagates downstream through the network.
- On destroy, the emission is subtracted back.
- Invalid placement shows a prohibition cursor texture.

### 4. Debris / Tunnel Blockage ([DebrisPlacementManager.cs](file:///d:/Projects/digimine/Assets/Scripts/DebrisPlacementManager.cs), [Debris.cs](file:///d:/Projects/digimine/Assets/Scripts/Debris.cs))

- Same snapping system as leak placement but snaps to edges specifically.
- Spawns a `debris.prefab` (stylized rocks from `JC_StylizedRocks_Lite`) at the exact position on the edge.
- Sets `edge.isBlocked = true`, stores the debris world position on the edge.
- The solver treats blocked edges as infinite resistance (skips them). The GraphManager splits the LineRenderer visually at the debris point.
- On destroy, unblocks the edge and cleans up the downstream LineRenderer.

### 5. Door System ([Door.cs](file:///d:/Projects/digimine/Assets/Scripts/Door.cs), [DoorSwitch.cs](file:///d:/Projects/digimine/Assets/Scripts/DoorSwitch.cs))

- Doors are attached to specific edges via the `Edge.door` field.
- Two states: `Open` and `Closed`. A closed door acts identically to debris in the solver (zero flow, skipped in pressure calculation).
- Each door can have a 3D toggle switch GameObject. Switch color changes: green when open, red when closed. Supports either dedicated materials or runtime color changes via MaterialPropertyBlock.
- `DoorSwitch.cs` is a thin click-handler component — clicking calls `door.Toggle()`.
- Any state change calls `GraphManager.RunSolverAndRender()` to recalculate the network.

### 6. Airflow / Fan Control ([AirflowPlacementManager.cs](file:///d:/Projects/digimine/Assets/Scripts/AirflowPlacementManager.cs), [FanController.cs](file:///d:/Projects/digimine/Assets/Scripts/FanController.cs))

- An "Airflow" UI button toggles visibility of a 3D controller panel (`airflowControllersRoot`) containing a world-space UI slider and door toggle switches.
- The slider controls the inlet node's pressure directly: `inletNode.pressure = sliderValue`. Changing it re-runs the solver, which recalculates all flows and methane concentrations network-wide.
- A `TextMeshPro` label displays the current speed value.
- `FanController.cs` (194 lines): Drives visual rotation of a 3D fan model. Auto-detects the rotor child transform by searching for names containing "rotor", "blade", "fan", "propeller", or "spin". Calculates the mesh geometric center via combined renderer bounds and uses `RotateAround()` so even models with off-center pivots spin correctly. Rotation speed is proportional to `fanSpeed` (synced from the slider), with smooth acceleration/deceleration via `MoveTowards()`. Optional AudioSource modulates volume and pitch with rotation speed.
- `AirflowPlacementManager` also handles door-switch clicks via `RaycastAll` — checks for `DoorSwitch`, `Door`, or toggleSwitch match on every left click.

### 7. Miner Evacuation / Navigation ([NavigationPlacementManager.cs](file:///d:/Projects/digimine/Assets/Scripts/NavigationPlacementManager.cs), [Pathfinder.cs](file:///d:/Projects/digimine/Assets/Scripts/Pathfinder.cs))

- "Navigation" UI button enters placement mode. User clicks to place a `human.prefab` miner on a tunnel edge (same snap system).
- A "Start Navigation" button triggers pathfinding.
- **Pathfinder.cs** implements Dijkstra's algorithm. Exit nodes are identified by `isExit` flag, or fallback to names containing "exit"/"end" or `isFixedPressure` nodes.
- Edge cost function:
  - Blocked edge or closed door → infinite (impassable)
  - CH₄ > 2.0% → infinite (completely avoided)
  - CH₄ between 1.25%–2.0% → length × 15
  - CH₄ between 0.75%–1.25% → length × 3
  - CH₄ < 0.75% → length × 1
- Returns an ordered list of nodes from miner to nearest exit, or null if no safe path exists.
- Path visualization supports two modes:
  - **3D arrow objects** (default, `use3DArrowNavigation = true`): Uses `arrowstart.prefab`, `arrowbody.prefab`, `arrowhead.prefab` placed and scaled along the path with configurable floating height, width/height/length scales, corner overlap, material override, and color tint. Colliders on arrow pieces are disabled so they don't block mouse interaction.
  - **Legacy LineRenderer**: Cyan/blue line drawn through all path node positions.
- "No safe route" warning panel shown when no path exists.

### 8. Camera System ([CameraController.cs](file:///d:/Projects/digimine/Assets/Scripts/CameraController.cs))

Orbit camera around a pivot point:
- **WASD**: Keyboard pan (projected onto ground plane), Shift for speed boost.
- **Middle Mouse**: Drag to pan.
- **Right Mouse**: Drag to orbit (yaw/pitch), clamped between 10°–85° pitch.
- **Scroll Wheel**: Zoom in/out (clamped 5–150 units).
- **Left Click on Node**: Toggles info panel. **Double-click**: Smooth lerp focus to that node (SmoothStep over 0.4s), adjusts zoom to `defaultFocusDistance` (25 units).
- Focus is interrupted by any manual camera input.
- Skips all input when pointer is over UI (`IsPointerOverGameObject`) or when leak placement mode is active.

### 9. WebGL Build ([index.html](file:///d:/Projects/digimine/index.html))

Standard Unity WebGL template. Full-viewport canvas (100vw × 100vh). Loads from `Build/` directory (compressed `.unityweb` files). Company: "rooklabs", Product: "digimine", Version: "0.1.0". Supports mobile detection and fullscreen toggle.

### 10. 3D Assets

| Prefab | Purpose |
|---|---|
| `info.prefab` | Node info icon (spawned above every node) |
| `MethaneLeak.prefab` | Methane gas leak visual |
| `debris.prefab` | Rock pile blockage |
| `human.prefab` | Miner character for evacuation |
| `arrowstart/body/head.prefab` | 3D navigation path arrows |
| `Fan N091219.obj` | Fan mesh (3.7 MB OBJ) |

Third-party assets: `JC_StylizedRocks_Lite` (stylized rocks), `HQP Studios` (unknown), `WorldMaterialsFree` (world materials).

---

## What Actually Works

1. **Real-time ventilation solving** — pressure, flow, and direction update instantly when you change fan pressure, toggle doors, place debris, or add leaks. Not pre-baked animation.
2. **Methane advection** — gas concentrations propagate downstream through the network based on flow. Color coding (green/yellow/red) updates on both node labels and edge arrows.
3. **Interactive placement** — all four placement tools (leak, debris, navigation, airflow) share a consistent snapping system (raycast → ground plane → project to nearest edge → snap indicator).
4. **Debris splits the visual** — LineRenderers physically split at the debris point showing flow stopping.
5. **Doors block/unblock flow** — toggling a door acts as opening/closing an airway, solver recalculates.
6. **Fan slider changes inlet pressure** — drives the entire network pressure distribution.
7. **Evacuation pathfinding** — Dijkstra with multi-factor cost weighting actually avoids high-methane zones and blocked tunnels. 3D arrow visualization along the safe path.
8. **Fan rotation** — visual fan model spins proportionally to slider value with auto-centered pivot and smooth inertia.
9. **Camera controls** — full orbit/pan/zoom/WASD/focus-on-node.
10. **WebGL deployment** — runs in-browser with no install.
11. **Mutual exclusion** — placement modes deactivate each other (can't place leak and debris simultaneously). Node colliders disable during placement to prevent accidental panel toggles.

---

## Limitations & Known Issues

### Solver / Physics
- **Linear flow model**: Uses `Q = ΔP / R` (Ohm's law analogy). Real mine ventilation follows Atkinson's square law `H = RQ²` (non-linear). The current solver will produce correct flow *directions* but not physically accurate *magnitudes* for real mine geometries.
- **No shock loss or natural ventilation pressure**: Junctions are treated as lossless. No NVP (temperature/density-driven buoyancy effects).
- **Methane is first-order advection only**: No diffusion, no time-stepping, no accumulation over time. It's an instantaneous steady-state calculation — CH₄ at each node is the flow-weighted average of upstream concentrations plus local generation. No transient gas build-up simulation.
- **No Atkinson friction factor calculation**: Edge resistance is a single scalar set in the Inspector. There's no computation from airway dimensions (area, perimeter, length, roughness).

### Graph / Network
- **Manually configured**: All nodes and edges are set up by hand in the Unity scene and wired in `GraphManager`'s Inspector lists. There's no import from mine survey data (DXF/DWG), no procedural generation, no graph editor UI.
- **Fixed topology**: You can't add or remove nodes/edges at runtime. Debris blocks an edge but doesn't split the graph. Doors toggle existing edges but don't create new connections.
- **No multi-level / 3D network**: The graph is effectively 2D (planar layout). No vertical shafts modeled differently from horizontal airways.

### Placement System
- **Leaks snap to nearest node, not exact edge position**: `MethaneLeak` adds its emission rate to the nearest Node's `methaneGenerationRate` rather than modeling a point-source on the edge itself. The visual spawns on the edge but the gas generation is on the node.
- **No undo/remove**: There's no UI to remove a placed leak or debris without destroying the GameObject manually. The `OnDestroy` handlers exist and clean up correctly, but there's no user-facing "delete" tool.
- **One debris per edge**: The system sets `edge.isBlocked = true` — there's no partial blockage or multiple debris on one edge.

### Pathfinding
- **Simple Dijkstra with linear scan**: Uses O(V²) implementation (iterates all unvisited nodes to find minimum). No priority queue / min-heap. Fine for small graphs but won't scale to thousands of nodes.
- **Miner placed on edge but path starts from nearest node**: There's a gap between where the miner visually stands (on an edge) and where the path actually starts (the nearest node).
- **Exit detection fallback is broad**: If no `isExit` node exists, it falls back to *any* node whose name contains "exit" or "end" or has `isFixedPressure`. This could match unintended nodes.

### Visual / UI
- **Arrow materials are per-instance but shared across edges with same material reference**: `edge.lineRenderer.sharedMaterial` modifications affect all edges sharing that material instance. The code creates new materials when needed but doesn't always guarantee isolation.
- **No terrain / tunnel geometry**: The mine is represented as nodes (spheres in gizmos) and edges (LineRenderers with arrow textures). There are no actual 3D tunnel meshes. The visualization is abstract — lines with arrows between junction points.
- **Node info panels are 3D world-space cubes + TextMeshPro**: They don't billboard toward the camera automatically, so they may be unreadable from certain viewing angles.
- **No minimap, no legend, no data dashboard**: All information is displayed directly in 3D. No 2D overlay showing network state, no graphs of pressure or CH₄ over the network.

### Platform / Deployment
- **WebGL-only build present**: No standalone Windows/Linux build in the repo. The `Build/` directory has the WebGL output.
- **No backend / no live data**: Entirely client-side. No server, no database, no API for real-time sensor data ingestion. All data is configured in the Unity scene at design time.
- **No save/load**: Network state, placed hazards, fan settings — none of this persists. Refreshing the browser resets everything.

### Code Quality
- **Heavy use of `FindFirstObjectByType` and `FindObjectsByType`**: Components find each other at runtime by searching all objects. Works but is fragile — depends on naming conventions and scene hierarchy.
- **No unit tests**: No test framework or automated testing of any kind.
- **15 script files total**, all in a flat `Assets/Scripts/` directory. No namespaces, no assembly definitions.
