# Multi-Axis CNC Control System - Setup Guide

## Overview

The new multi-axis control system provides **3-stage sequential manual control** for the CNC machine:

1. **Stage 1 - Cutter X-Axis (Left/Right)**: `J` = Left, `L` = Right
2. **Stage 2 - Spindle Holder Z-Axis (Forward/Backward)**: `I` = Forward, `K` = Backward  
3. **Stage 3 - Spindle Y-Axis (Up/Down)**: `W` = Up, `X` = Down

**Key Features:**
- ✅ Automatic mode switching based on key presses (no manual lock/unlock)
- ✅ Physical movement of spindle holder Transform in Z-axis
- ✅ Independent control of cutter (X), holder (Z), and spindle (Y)
- ✅ Auto mode (PathData following) remains unchanged
- ✅ Backward compatible with legacy joystick control

---

## Unity Setup Instructions

### Step 1: Add the Multi-Axis Controller Component

1. **Locate your CNC machine's Control Panel in the Hierarchy**
   - Usually: `CNC Machine Root / Control Panel`

2. **Create a new GameObject under Control Panel:**
   - Right-click Control Panel → Create Empty
   - Name it: `MultiAxisController`

3. **Add the CNCMultiAxisController component:**
   - Select the `MultiAxisController` GameObject
   - Click "Add Component"
   - Search for: `CNCMultiAxisController`
   - Add it

4. **Configure the component (Inspector):**
   ```
   CNCMultiAxisController
   ├─ Key Bindings (default values):
   │  ├─ Left Key: J
   │  ├─ Right Key: L
   │  ├─ Forward Key: I
   │  ├─ Back Key: K
   │  ├─ Up Key: W
   │  └─ Down Key: X
   ├─ Behavior:
   │  ├─ Auto Mode Switching: ✓ (checked)
   │  └─ Starting Mode: XAxis
   └─ Debug:
      └─ Verbose Logging: ☐ (unchecked, enable for testing)
   ```

---

### Step 2: Update CNCCutterExtended Reference

1. **Locate your CNC cutter GameObject:**
   - Usually: `CNC Machine Root / Spindle Holder / Tool Head` or similar
   - It should have a `CNCCutterExtended` component

2. **Assign the Multi-Axis Controller:**
   - Select the cutter GameObject
   - In the `CNCCutterExtended` component Inspector
   - Find the **"Multi Axis Controller"** field (under References section)
   - Drag the `MultiAxisController` GameObject into this field

3. **Optional: Disable legacy joystick (recommended for clarity):**
   - If you have a `Joystick` GameObject under Control Panel
   - Select it and **disable** the `JoystickController` component
   - This prevents both systems from running simultaneously
   - Note: Auto mode doesn't use either controller

---

### Step 3: Verify Transform Hierarchy

**Critical:** The spindle holder must be the **parent** of the cutter for Z-axis movement to work correctly.

**Expected hierarchy:**
```
CNC Machine Root
├── Control Panel
│   ├── Joystick (JoystickController disabled)
│   └── MultiAxisController (CNCMultiAxisController enabled) ← NEW
├── Spindle Holder Transform ← This must be parent of cutter
│   └── Tool Head / Cutter GameObject ← CNCCutterExtended here
│       └── Meche (drill bit)
└── Other components...
```

**To verify:**
1. Select your cutter GameObject (the one with `CNCCutterExtended`)
2. Look at the Hierarchy panel
3. The **immediate parent** should be the Spindle Holder Transform
4. If not, drag the cutter GameObject under the Spindle Holder

---

### Step 4: Test in Play Mode

1. **Start the game in Unity Editor**

2. **Load a workpiece onto the CNC**

3. **Start the CNC in Manual Mode:**
   - Click the "Start" button on the CNC control panel
   - Ensure the machine enters "Cutting" state

4. **Test each axis:**

   **X-Axis Test (Cutter Left/Right):**
   - Press `J` → Cutter should move **left**
   - Press `L` → Cutter should move **right**
   - Note: Spindle holder and Y position should NOT move

   **Z-Axis Test (Holder Forward/Backward):**
   - Press `I` → Spindle holder should move **forward** (entire assembly including cutter)
   - Press `K` → Spindle holder should move **backward**
   - Note: Cutter's X position (relative to holder) should NOT change

   **Y-Axis Test (Spindle Up/Down):**
   - Press `W` → Spindle should move **up**
   - Press `X` → Spindle should move **down**
   - Note: Holder position should NOT move

5. **Verify automatic mode switching:**
   - Press `J` or `L` → Mode switches to X-Axis
   - Press `I` or `K` → Mode switches to Z-Axis
   - Press `W` or `X` → Mode switches to Y-Axis
   - Mode switching should be seamless (no manual unlock needed)

6. **Test bounds checking:**
   - Try moving each axis to its limits
   - Movement should stop at boundaries (no clipping through machine)

---

## Control Scheme Reference

| Stage | Axis | GameObject Moved | Keys | Function |
|-------|------|------------------|------|----------|
| 1 | X | Cutter Transform | `J` / `L` | Move cutter left/right |
| 2 | Z | Spindle Holder Transform | `I` / `K` | Move holder forward/backward |
| 3 | Y | Cutter Transform | `W` / `X` | Move spindle up/down |

**Movement Logic:**
- **X-axis**: `cutter.transform.localPosition.x` is modified
- **Z-axis**: `cutter.transform.parent.localPosition.z` is modified (moves holder)
- **Y-axis**: `cutter.transform.localPosition.y` is modified

---

## Troubleshooting

### Issue: Keys don't do anything

**Solutions:**
1. Check that `CNCMultiAxisController` component is **enabled** in Inspector
2. Verify the cutter has a reference to the controller (Step 2)
3. Check that the CNC machine is in **"Cutting"** state (not Idle or Done)
4. Enable "Verbose Logging" on both `CNCMultiAxisController` and `CNCCutterExtended` to see debug messages

---

### Issue: Holder doesn't move in Z-axis

**Solutions:**
1. Verify transform hierarchy (Step 3) - cutter must be a **child** of holder
2. Check that `transform.parent` is not null:
   - Select cutter GameObject
   - In Inspector, verify it has a parent (not a root object)
3. Enable "Verbose Logging" and check for "Z-axis (holder) movement" messages

---

### Issue: Wrong keys are mapped

**Solutions:**
1. Select `MultiAxisController` GameObject
2. In Inspector, manually change the key bindings:
   ```
   Left Key: J
   Right Key: L
   Forward Key: I
   Back Key: K
   Up Key: W
   Down Key: X
   ```

---

### Issue: Both joystick and multi-axis respond

**Solution:**
- Disable the legacy `JoystickController` component:
  1. Find the `Joystick` GameObject under Control Panel
  2. In Inspector, **uncheck** the checkbox next to `JoystickController` component name
  3. This disables it without deleting (can re-enable for auto mode if needed)

---

### Issue: Auto mode doesn't work

**Diagnosis:** Auto mode should be **unaffected** by these changes. It uses the `PathData` following system.

**Solutions:**
1. Auto mode doesn't use the multi-axis controller (by design)
2. Verify that PathData is loaded before starting auto cut
3. Check that `CNCMachineExtended` state machine still works:
   - Load a path
   - Click Start
   - Machine should follow the path automatically
4. If auto mode was working before, it should still work now

---

### Issue: Movement is too fast/slow

**Solution:**
1. Select the cutter GameObject (with `CNCCutterExtended` component)
2. In Inspector, find **"Manual Speed"** under Movement section
3. Adjust the value:
   - Default: `0.15` m/s
   - Slower: `0.05 - 0.10` m/s
   - Faster: `0.20 - 0.30` m/s
4. All axes use the same speed value

---

### Issue: Cutter clips through machine boundaries

**Solution:**
1. Check that `WorkAreaBounds` is assigned:
   - Select cutter GameObject
   - In `CNCCutterExtended` Inspector
   - Verify "Work Area Bounds" field has a ScriptableObject assigned
2. Verify the bounds values are correct:
   - Open the `WorkAreaBounds` ScriptableObject
   - Check minX, maxX, minY, maxY, minZ, maxZ values
   - Adjust to match your machine's physical limits

---

## Advanced Configuration

### Customizing Key Bindings

You can change any key in the `CNCMultiAxisController` Inspector:

1. Select `MultiAxisController` GameObject
2. Expand "Key Bindings" section
3. Click on any key field (e.g., "Left Key")
4. Press the desired key on your keyboard
5. The KeyCode will update (e.g., `KeyCode.A`)

**Example alternative layouts:**

**Arrow Keys + WASD:**
```
Left Key: LeftArrow
Right Key: RightArrow
Forward Key: UpArrow
Back Key: DownArrow
Up Key: W
Down Key: S
```

**Numpad:**
```
Left Key: Keypad4
Right Key: Keypad6
Forward Key: Keypad8
Back Key: Keypad2
Up Key: Keypad9
Down Key: Keypad3
```

---

### Disabling Automatic Mode Switching

If you want manual control over mode switching:

1. Select `MultiAxisController` GameObject
2. In Inspector, **uncheck** "Auto Mode Switching"
3. Add UI buttons or other scripts to call:
   ```csharp
   multiAxisController.SwitchMode(CNCMultiAxisController.ControlMode.XAxis);
   multiAxisController.SwitchMode(CNCMultiAxisController.ControlMode.ZAxis);
   multiAxisController.SwitchMode(CNCMultiAxisController.ControlMode.YAxis);
   ```

---

### Enabling Visual Feedback

To add visual indicators showing which axis is active:

1. Subscribe to the `OnModeChanged` event in your UI script:
   ```csharp
   _multiAxisController.OnModeChanged += HandleModeChanged;
   
   private void HandleModeChanged(CNCMultiAxisController.ControlMode mode)
   {
       switch (mode)
       {
           case CNCMultiAxisController.ControlMode.XAxis:
               _modeText.text = "Mode: Cutter X-Axis (Left/Right)";
               break;
           case CNCMultiAxisController.ControlMode.ZAxis:
               _modeText.text = "Mode: Holder Z-Axis (Forward/Back)";
               break;
           case CNCMultiAxisController.ControlMode.YAxis:
               _modeText.text = "Mode: Spindle Y-Axis (Up/Down)";
               break;
       }
   }
   ```

2. Or highlight the active component with materials/colors

---

## Testing Checklist

Use this checklist to verify the implementation:

**Manual Mode:**
- [ ] J key moves cutter left
- [ ] L key moves cutter right
- [ ] I key moves spindle holder forward
- [ ] K key moves spindle holder backward
- [ ] W key moves spindle up
- [ ] X key moves spindle down
- [ ] Cutter stays locked when holder moves (X position relative to holder unchanged)
- [ ] Holder stays locked when cutter moves in X (holder Z unchanged)
- [ ] Holder stays locked when spindle moves in Y (holder Z unchanged)
- [ ] Movement stops at boundaries (bounds checking works)
- [ ] Mode switches automatically when pressing different keys
- [ ] Cutting/recording still works correctly

**Auto Mode:**
- [ ] Auto mode starts correctly (loads PathData)
- [ ] Auto mode follows path without multi-axis input interference
- [ ] Cutting completes successfully
- [ ] Results are generated correctly

**State Management:**
- [ ] Controls only work when machine is in "Cutting" state
- [ ] Controls disabled in "Idle" state
- [ ] Controls disabled in "Positioning" state
- [ ] Controls disabled in "Done" state

---

## Implementation Details

### Files Modified

| File | Type | Changes |
|------|------|---------|
| `Assets/Scripts/Machines/CNCMultiAxisController.cs` | **NEW** | 3-axis input controller with J/L/I/K/W/X key bindings |
| `Assets/Scripts/Machines/CNCCutterExtended.cs` | **MODIFIED** | Added multi-axis input handlers and updated movement logic |

### Code Architecture

**Input Flow:**
```
User presses keys (J/L/I/K/W/X)
    ↓
CNCMultiAxisController.Update()
    ↓ Reads Input.GetKey() for all 6 keys
    ↓ Fires events: OnXAxisInput, OnZAxisInput, OnYAxisInput
CNCCutterExtended event handlers
    ↓ Stores input: _xAxisInput, _zAxisInput, _yAxisInput
    ↓
CNCCutterExtended.MoveCutterManual()
    ↓ Checks which axis has input
    ↓ Modifies appropriate Transform:
    ├─ X-axis: cutter.transform.localPosition.x
    ├─ Z-axis: cutter.transform.parent.localPosition.z
    └─ Y-axis: cutter.transform.localPosition.y
    ↓
Bounds checking and clamping
    ↓
Fire OnCutterMoved event
    ↓
Record position for path recording
```

**Backward Compatibility:**
- If `_multiAxisController` is null, falls back to legacy `_joystick` control
- Auto mode completely unaffected (uses `PathData` system)
- Existing scenes without multi-axis controller continue to work

---

## Support

If you encounter issues not covered in this guide:

1. Enable "Verbose Logging" on both components
2. Check the Unity Console for diagnostic messages
3. Use the Context Menu options:
   - Right-click `CNCMultiAxisController` → "Print Current State"
   - Right-click `CNCCutterExtended` → "Print Startup Diagnostics"

---

**Version:** 1.0  
**Last Updated:** 2026-04-03  
**Compatible with:** Unity 2021.3+
