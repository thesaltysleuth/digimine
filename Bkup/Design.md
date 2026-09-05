# Underground Mine Ventilation & Gas Safety Simulator — System Architecture & Design Document

---

## System Architecture & Data Flow

The application uses a decoupled Model-View-Controller (MVC) architecture. The mathematical physics engine runs independently of Unity’s visual rendering layer, allowing for real-time network recalculations when interactive sandbox tools modify the mine graph.

```
                  ┌─────────────────────────────────────────┐
                  │            CSV Sensor Data              │
                  └────────────────────┬────────────────────┘
                                       │
                                       ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                              APP STATE MANAGER                              │
│                                                                             │
│   ┌──────────────────────────────────┐   ┌──────────────────────────────┐   │
│   │           REPLAY MODE            │   │       SIMULATION MODE        │   │
│   │  • Steps through CSV timestamps  │   │  • Interactive graph tweaks  │   │
│   │  • Locks network editing         │   │  • Scenario injection engine │   │
│   └────────────────┬─────────────────┘   └──────────────┬───────────────┘   │
└────────────────────┼────────────────────────────────────┼───────────────────┘
                     │                                    │
                     └──────────────────┬─────────────────┘
                                        │
                                        ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                            GRAPH SOLVER ENGINE                              │
│                                                                             │
│   1. Pressure Solver         2. Airflow Vectorizer     3. Advection & Gas   │
│      (Nodal Relaxation)         (Q = ΔP / R)              Mixing Solver     │
└──────────────────────────────────────┬──────────────────────────────────────┘
                                       │
                                       ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                            UNITY RENDERING LAYER                            │
│                                                                             │
│   ┌──────────────────────────┐  ┌────────────────┐  ┌───────────────────┐   │
│   │ Arrow Velocity Animators │  │ Gas Volumetrics│  │ NavMesh Evacuation│   │
│   │ (Dynamic Spline Speeds)  │  │ & Hazard Tints │  │   Agent Router    │   │
│   └──────────────────────────┘  └────────────────┘  └───────────────────┘   │
└──────────────────────────────────────┬──────────────────────────────────────┘
                                       │
                                       ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                            UI & HUD INTERACTION                             │
│                                                                             │
│   • Gas Sampler Eyedropper Card        • Interactive Tool Palette           │
│   • Scenario Action Toggles            • Real-time Alert Banner             │
└──────────────────────────────────────┴──────────────────────────────────────┘

```

---

## Core Graph Model

To achieve high-frame-rate performance during interactive live demos without requiring computationally heavy 3D Navier-Stokes CFD calculations, the mine is modeled as a 1D Directed Graph embedded in 3D Space:

* **Nodes:** Airway junctions, dead ends, fan stations, sensor points, or gas leaks.
* **Edges:** Roadways and galleries connecting nodes, each assigned an aerodynamic resistance value ($R$).

```
       Edge E12 (R = 1.0)                      Edge E24 (R = 1.0)
[Node 1: P=100] ───► ───► ───► [Node 2: P=50] ───► ───► ───► [Node 4: P=0]
                                     ▲
                                     │ Edge E32 (R = 4.0)
                               [Node 3: P=80]

```

### Graph Data Attributes

* **Node Parameters:**
* Node ID & World Position Vector $(X, Y, Z)$
* Barometric Pressure ($P$, in Pa) & Fixed Boundary Flag (for fans/shafts)
* Multi-Gas Concentrations: $\text{CH}_4\%$, $\text{CO}_2\%$, $\text{CO ppm}$, $\text{O}_2\%$
* Local Gas Generation/Leak Rate ($G_{\text{CH4}}$, in $\text{m}^3/\text{s}$)
* List of Connected Edges


* **Edge Parameters:**
* Edge ID, Source Node, and Target Node
* Aerodynamic Resistance ($R$, in $\text{Pa}\cdot\text{s}/\text{m}^3$)
* Volumetric Airflow Rate ($Q$, in $\text{m}^3/\text{s}$)
* Flow Vector Direction ($\text{Node}_A \to \text{Node}_B$, $\text{Node}_B \to \text{Node}_A$, or Static)
* Obstruction / Ventilation Controls State (Door Open/Closed, Blocked by Debris)



---

## Mathematical Solver Design

The engine executes a three-pass calculation sequence whenever network resistances, fan statuses, or boundary pressures are altered:

```
   [Pass 1: Pressure Solver]
   Iterative Nodal Relaxation until convergence (ΔP balance across loops)
              │
              ▼
   [Pass 2: Airflow Calculation]
   Calculate Q = ΔP / R and resolve direction vectors for arrows
              │
              ▼
   [Pass 3: Gas Advection & Mixing]
   Propagate CH4/CO2 downstream from high-pressure nodes to low-pressure nodes

```

### Pass 1: Iterative Nodal Pressure Solver (Gauss-Seidel Relaxation)

For any interior node $i$, conservation of mass demands that incoming airflow equals outgoing airflow ($\sum Q_{\text{in}} = \sum Q_{\text{out}}$). Expressed in terms of nodal pressure and edge conductance ($1/R$):

$$P_i = \frac{\sum_{j \in \text{Neighbors}} \left( \frac{P_j}{R_{i,j}} \right)}{\sum_{j \in \text{Neighbors}} \left( \frac{1}{R_{i,j}} \right)}$$

The solver iteratively updates all non-boundary node pressures until the maximum pressure shift across the entire network falls below a convergence tolerance threshold ($< 0.001\text{ Pa}$).

### Pass 2: Edge Airflow Vectorizer

Computes airflow magnitude $Q$ and sets arrow directions based on the pressure differential across each edge ($\Delta P = P_A - P_B$):

$$Q = \frac{\vert{}\Delta P\vert{}}{R}$$

* If $\Delta P > 0$: Direction is $\text{Node}_A \to \text{Node}_B$.
* If $\Delta P < 0$: Direction is $\text{Node}_B \to \text{Node}_A$.
* If $\Delta P = 0$ or $R = \infty$ (Blocked): Direction is static ($Q = 0$).

### Pass 3: Gas Advection & Downstream Mixing

Calculates gas concentrations along incoming air streams. Incoming gas volumes are weighted by flow rate and mixed at downstream target junctions:

$$\text{CH}_4^{\text{Node}} = \frac{\sum_{\text{Inflows}} \left( Q_{\text{in}} \times \text{CH}_4^{\text{Source}} \right) + G_{\text{CH4}}}{\sum Q_{\text{in}}}$$

---

## Multi-Gas Visualization Strategy

```
                          VISUALIZATION PIPELINE
                          
┌───────────────────────────────┐     ┌──────────────────────────────────┐
│        AIRFLOW VECTOR         │     │         HAZARD VOLUMETRICS       │
│                               │     │                                  │
│  • Directional Green Arrows   │     │  • Volumetric Amber/Red Smoke    │
│  • Animated along Edge Path   │     │  • Opacity = Gas Concentration   │
│  • Speed = Air Velocity (Q)   │     │  • Active only on Danger Nodes   │
└───────────────┬───────────────┘     └────────────────┬─────────────────┘
                │                                      │
                └───────────────────┬──────────────────┘
                                    │
                                    ▼
┌────────────────────────────────────────────────────────────────────────┐
│                        GAS SAMPLER EYEDROPPER                          │
│                                                                        │
│  • Click anywhere on mesh to inspect exact multi-gas breakdown         │
│  • Displays CH4 %, CO2 %, CO ppm, O2 %, Pressure (Pa), Air Velocity   │
└────────────────────────────────────────────────────────────────────────┘

```

### Airflow Direction & Velocity Layer

* **Representation:** Quad/Sprite arrow sequences animated along the central spine of roadway galleries.
* **Color States:**
* **Green:** Safe airflow ($\text{CH}_4 < 0.75\%$).
* **Yellow/Amber:** Warning threshold ($0.75\% \le \text{CH}_4 \le 1.25\%$).
* **Red:** Danger / Evacuation threshold ($\text{CH}_4 > 1.25\%$).


* **Movement Speed:** Linear translation speed of arrow textures is mapped directly to volumetric flow rate $Q$. Static air ($Q \approx 0$) halts arrow translation completely.

### Hazard Anomaly Layer

* **Representation:** Volumetric particle clouds spawned at leak sources or junctions exceeding safety thresholds ($\text{CH}_4 > 1.0\%$).
* **Color Palette:** Amber / Yellow-Orange Smoke shifting to dense Red at explosive concentrations ($> 2.5\%$). Avoids bright green to eliminate visual confusion with safe airflow paths.
* **Density Coupling:** Particle density and opacity scale proportionally with local $\text{CH}_4$ concentration.

### Gas Sampler Eyedropper Card

Clicking any gallery segment with the Eyedropper Tool opens a focused HUD inspector card displaying real-time multi-gas species breakdowns and airflow metrics:

| Parameter | Metric Type | Threshold Status |
| --- | --- | --- |
| **Barometric Pressure** | Pascals ($\text{Pa}$) / Differential ($\Delta P$) | Nominal Flow Differential |
| **Airflow Velocity ($Q$)** | Cubic Meters per Second ($\text{m}^3/\text{s}$) | Main Intake / Return Baseline |
| **Methane ($\text{CH}_4$)** | Percentage by Volume ($\%$) | Alert Threshold Check ($1.25\%$ Limit) |
| **Carbon Dioxide ($\text{CO}_2$)** | Percentage by Volume ($\%$) | Threshold Check ($0.50\%$ Limit) |
| **Carbon Monoxide ($\text{CO}$)** | Parts Per Million ($\text{ppm}$) | Threshold Check ($25\text{ ppm}$ Limit) |
| **Oxygen ($\text{O}_2$)** | Percentage by Volume ($\%$) | Normal Baseline ($\ge 19.5\%$) |

---

## Simulation Scenarios & Tool Specifications

| Scenario | Tool / Trigger Action | Under-the-Hood Graph Adjustment | Visual Output & Rendering |
| --- | --- | --- | --- |
| **1. Methane Leak** | Select Methane Tool $\to$ Click Roadway Mesh | Injects local gas generation ($G_{\text{CH4}} > 0$) at target node | Volumetric amber smoke spawns. Downstream airflow arrows transition Green $\to$ Amber $\to$ Red. Escape routes recalculate. |
| **2. Door Opened (Short-Circuit)** | Select Edit Tool $\to$ Click Ventilation Door | Reduces door edge resistance $R$ from $100.0\,\Omega$ to $0.05\,\Omega$ | Door mesh rotates $90^\circ$. High-velocity arrows shortcut through the door; main working face airflow drops to near zero ($Q \approx 0$). |
| **3. Fan Malfunction** | Click Fan Toggle on Control Dashboard | Sets fan boundary node pressure $P_{\text{fan}}$ from $250\text{ Pa}$ to $0\text{ Pa}$ | Return fan arrows freeze. System-wide pressure drops to static equilibrium ($\Delta P \to 0$), initiating slow gas accumulation across all faces. |
| **4. Debris Fall (Blockage)** | Select Blast Tool $\to$ Click Gallery Mesh | Sets edge resistance $R$ to infinite ($9999.0\,\Omega$) | Rubble/debris mesh instantiates on gallery floor. Airflow arrows disappear on blocked segment. Graph routing marks path impassable. |

---

## Dynamic Evacuation Routing Design

Evacuation route calculations use a modified $A^*$ pathfinding algorithm over the graph network, incorporating distance and hazard penalties into the edge traversal cost:

$$\text{Edge Traversal Cost} = \text{Segment Length} \times \left( 1.0 + \alpha \cdot (\text{CH}_4\%)^2 \right) + \text{Obstruction Penalty}$$

* **Clear Path ($\text{CH}_4 < 0.75\%$):** $\alpha = 1.0$ (Standard geometric distance pathing).
* **Hazard Path ($\text{CH}_4 \ge 1.25\%$):** $\alpha = 50.0$ (Heavy weight penalty; router avoids unless no other path exists to an exhaust shaft).
* **Blocked Path ($R = \infty$):** Weight set to $\infty$ (Path strictly forbidden).

The resulting route is projected into Unity's scene space as an animated, high-contrast evacuation vector leading to the nearest safe intake shaft or surface portal.