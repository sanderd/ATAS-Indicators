# Label Distribution Algorithm

This document describes the algorithm used to position labels for price levels on a trading chart, ensuring no overlaps while minimizing the visual distance (branch length) from each label to its corresponding price level.

## Problem Statement

Given N price levels with labels that may overlap when rendered:
- Each label has a **target Y position** (the price level's screen coordinate)
- Each label has a **height** (text height in pixels)
- Labels must maintain a **minimum spacing** between them
- **Goal**: Position labels to minimize total deviation from targets while ensuring no overlaps

This is a 1D constraint satisfaction problem with optimization.

## Algorithm: Iterative Relaxation with Proportional Distribution

### Overview

The algorithm uses iterative relaxation where overlapping labels "push" each other apart. The key insight is that the required shift is **distributed proportionally** based on available room in each direction, and shifts **propagate** to neighbors.

### Pseudo-Implementation

```
function CalculateLabelPositions(levels):
    // Initialize: each label starts at its target position
    positions = []
    for each level in levels:
        positions.add(LabelPosition(
            LevelY = level.screenY,      // Original target
            LabelY = level.screenY,      // Current position (starts at target)
            LabelHeight = measureText(level.label).height
        ))
    
    // Sort by target Y (top to bottom) - maintains stable order
    sort(positions, by: LevelY)
    
    // Iterative relaxation
    for iteration in 1..MAX_ITERATIONS:
        changed = false
        
        // Check each adjacent pair
        for i in 0..(positions.count - 2):
            upper = positions[i]
            lower = positions[i + 1]
            
            overlap = (upper.LabelY + upper.height/2 + MIN_SPACING) 
                    - (lower.LabelY - lower.height/2)
            
            if overlap > 0:
                changed = true
                
                // Calculate available room in each direction
                upperRoom = CalculateUpwardRoom(positions, i)
                lowerRoom = CalculateDownwardRoom(positions, i + 1)
                totalRoom = upperRoom + lowerRoom
                
                if totalRoom > 0:
                    // Distribute proportionally
                    upperShift = overlap * (upperRoom / totalRoom)
                    lowerShift = overlap - upperShift
                else:
                    // No room - split evenly
                    upperShift = overlap / 2
                    lowerShift = overlap / 2
                
                // Apply shifts with neighbor propagation
                ShiftWithPropagation(positions, i, -upperShift, UP)
                ShiftWithPropagation(positions, i + 1, +lowerShift, DOWN)
        
        if not changed:
            break  // Converged
    
    // Safety net: ensure no overlaps remain
    EnforceNoOverlaps(positions)
    
    return positions
```

### Room Calculation

```
function CalculateUpwardRoom(positions, index):
    pos = positions[index]
    
    // Can move up but not too far above original position
    minAllowed = pos.LevelY - pos.LabelHeight * 2
    
    // Also limited by label above (if any)
    if index > 0:
        prev = positions[index - 1]
        prevConstraint = prev.LabelY + prev.height/2 + MIN_SPACING + pos.height/2
        minAllowed = max(minAllowed, prevConstraint)
    
    return max(0, pos.LabelY - minAllowed)

function CalculateDownwardRoom(positions, index):
    pos = positions[index]
    
    // Can move down but not too far below original position
    maxAllowed = pos.LevelY + pos.LabelHeight * 2
    
    // Also limited by label below (if any)
    if index < positions.count - 1:
        next = positions[index + 1]
        nextConstraint = next.LabelY - next.height/2 - MIN_SPACING - pos.height/2
        maxAllowed = min(maxAllowed, nextConstraint)
    
    return max(0, maxAllowed - pos.LabelY)
```

### Shift Propagation

```
function ShiftWithPropagation(positions, index, shift, direction):
    if shift == 0:
        return
    
    positions[index].LabelY += shift
    
    // Check if we now overlap with neighbor in shift direction
    if direction == UP and index > 0:
        curr = positions[index]
        prev = positions[index - 1]
        
        newOverlap = (prev.LabelY + prev.height/2 + MIN_SPACING) 
                   - (curr.LabelY - curr.height/2)
        
        if newOverlap > 0:
            // Propagate the shift upward
            ShiftWithPropagation(positions, index - 1, -newOverlap, UP)
    
    else if direction == DOWN and index < positions.count - 1:
        curr = positions[index]
        next = positions[index + 1]
        
        newOverlap = (curr.LabelY + curr.height/2 + MIN_SPACING) 
                   - (next.LabelY - next.height/2)
        
        if newOverlap > 0:
            // Propagate the shift downward
            ShiftWithPropagation(positions, index + 1, +newOverlap, DOWN)
```

## Key Properties

| Property | Description |
|----------|-------------|
| **Stable ordering** | Labels maintain their relative order (sorted by price level) |
| **Minimal deviation** | Proportional distribution minimizes total branch length |
| **Symmetric distribution** | Clusters spread both up AND down around their center |
| **Neighbor awareness** | Shifts propagate to make room, affecting entire chains |
| **Convergence** | Algorithm terminates when no overlaps remain |

## Complexity

- **Time**: O(N² × I) where N = number of labels, I = iterations (typically < 20)
- **Space**: O(N) for position storage

## Visual Example

```
Before (overlapping):          After (distributed):
                               
    ─── LevelA                     ─┐ LabelA (shifted up)
    ─── LevelB                      │
    ─── LevelC  ← cluster          ─┼─ LabelB (at target)
    ─── LevelD                      │
                                   ─┘ LabelC (shifted down)
                                    
    ─── LevelE                     ─── LabelD (at target)
                                    
                                   ─── LabelE (at target)
```

The algorithm recognizes that shifting LevelA up creates room for the cluster to be more centered, minimizing the maximum branch length.
