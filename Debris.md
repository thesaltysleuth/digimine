```markdown
# Debris Visual Design Specification
**For IDE Agent / Implementation**

## Goal
When debris is placed on an edge, the airflow arrows must visually stop at the debris location.  
The segment from the upstream node to the debris continues to show flowing arrows (if flow exists).  
The segment from the debris to the downstream node shows **no arrows**.

---

## 1. Core Visual Rule (Option A)

On a blocked edge:

- **Upstream side** (from the node that has flow coming toward the debris → debris position):  
  Keep the normal animated arrow LineRenderer.

- **Downstream side** (debris position → the other node):  
  Show a plain static line with **no arrows** (no arrow texture, no animation).

The debris GameObject sits exactly at the split point.

---

## 2. Required Data on Edge

Each `Edge` must store:

- `bool isBlocked`
- `GameObject debrisInstance` (the spawned debris object)
- `Vector3 debrisWorldPosition` (exact placement point used for splitting)
- Original `LineRenderer` reference (already exists)
- A second `LineRenderer` for the downstream “blocked” segment (create at runtime when blocked)

---

## 3. LineRenderer Splitting Logic

When an edge becomes blocked:

1. Determine flow direction using existing `edge.flowDirection`.
2. Identify which node is upstream and which is downstream at the moment of blocking.
3. Split the original LineRenderer:

   **Upstream LineRenderer** (keeps arrows):
   - Position 0 = upstream node position
   - Position 1 = debrisWorldPosition
   - Uses the normal arrow material + texture + animation
   - Only enabled if `calculatedFlowRate > 0`

   **Downstream LineRenderer** (no arrows):
   - Position 0 = debrisWorldPosition
   - Position 1 = downstream node position
   - Material = simple unlit colour (dark grey or muted)
   - No arrow texture
   - No UV animation
   - Always visible while blocked (so the topology remains readable)

4. Hide or disable the original full-length LineRenderer while the edge is blocked.

When debris is removed:

- Destroy both split LineRenderers
- Restore the original full-length arrow LineRenderer
- Set `isBlocked = false`

---

## 4. Debris Object Requirements

- Prefab name: debris.prefab
- Appearance: rock pile / collapsed supports / rubble that clearly sits inside the tunnel cross-section
- Must be oriented correctly to the tunnel direction when placed

The debris object is parented under the GraphManager.

---

## 5. Placement → Visual Update Flow

1. User places debris via DebrisPlacementManager (same snapping system as LeakPlacementManager).
2. System identifies the target `Edge` and the exact world position on that edge.
3. Spawn `debris` prefab at that position.
4. Store references on the `Edge`:
   - `isBlocked = true`
   - `debrisInstance`
   - `debrisWorldPosition`
5. Immediately call the visual split function for that edge.
6. Call `GraphManager.RunSolverAndRender()` so pressures, flows and methane update.
7. After solver finishes, re-evaluate upstream/downstream and refresh the split LineRenderers (flow direction may have changed).

---

## 6. Summary of Visual States

| Edge State       | Upstream Segment          | Downstream Segment       | Debris Object |
|------------------|---------------------------|--------------------------|---------------|
| Normal           | Full animated arrows      | (same LineRenderer)      | None          |
| Blocked          | Animated arrows (if flow) | Static grey line (no arrows) | Visible rock pile |
| Blocked + zero flow | Static grey line        | Static grey line         | Visible       |

---

## Implementation Notes for Agent

- Reuse the existing arrow material/texture system for the upstream segment.
- Create a simple dark unlit material for the downstream “no-arrow” segment.
- All visual updates must happen inside or immediately after `GraphManager.RunSolverAndRender()`.
- Keep the original `Edge.lineRenderer` as the “master” reference and only activate the split renderers while `isBlocked == true`.
```