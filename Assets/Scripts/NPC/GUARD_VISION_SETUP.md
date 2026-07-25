# Guard Vision View Setup Guide

## Overview
The `GuardVisionView` system creates a red, adjustable vision cone for bodyguards. The vision is obscured by walls and can be fully customized in the Inspector.

## Setup Instructions

### 1. Add the Component
- Select the Guard gameobject in the scene
- In the Inspector, click **Add Component**
- Search for and add **GuardVisionView**

### 2. Configure Vision Properties
In the Inspector, you'll find adjustable parameters:

#### Vision Properties
- **Vision Length**: How far the guard can see forward (default: 10)
- **Vision Width**: Horizontal width of the vision cone (default: 5)
- **Vision Height**: Vertical height of the vision cone (default: 3)

#### Visual Properties
- **Opacity**: Transparency of the vision view (0 = invisible, 1 = opaque) (default: 0.3)
- **Vision Color**: The color of the vision cone (default: Red)

#### Occlusion Settings
- **Wall Layer**: Optional legacy mask; the triangle now clips against any collider in front of it
- **Use Wall Occlusion**: Enable/disable blocker clipping for the triangle view
- **Occlusion Check Density**: How many rays to cast for occlusion (higher = more accurate but slower)

### 3. Configure Walls
- Ensure all walls/obstacles have a collider
- Keep blockers on a raycastable layer so the triangle can be clipped by them
- Put objects that should not hide the triangle on the `Ignore Raycast` layer if needed

### 4. Position the Vision View
The vision view is positioned relative to the Guard's transform:
- The vision extends **forward** (along the Z-axis)
- It's centered on the guard's position
- Adjust the guard's rotation to change vision direction

## Features

✓ **Red Color**: Default red color with fully adjustable opacity
✓ **Inspector Control**: All properties adjustable in real-time during play
✓ **Wall Occlusion**: Vision respects walls and obstacles
✓ **Triangle View**: Vision now renders as a flat triangle
✓ **Gizmo Display**: Visual guide in Scene view when selected

## API Methods

```csharp
// Programmatically control the vision view
GuardVisionView visionView = guard.GetComponent<GuardVisionView>();

visionView.SetVisionLength(15f);      // Change how far they see
visionView.SetVisionWidth(8f);        // Change horizontal FOV
visionView.SetVisionHeight(5f);       // Change vertical FOV
visionView.SetOpacity(0.5f);          // Change transparency
visionView.SetVisionColor(Color.red); // Change color
```

## Tips
- Increase opacity for better visibility during testing
- Adjust width and height to create narrow or wide vision cones
- Use the Scene view gizmo to visualize the vision area while developing
- The vision cone expands as it extends farther, creating a realistic sight cone
