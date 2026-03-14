# Interactive Context Menu System - User Guide

## Overview

When you click on a placed object, a context menu appears with two options:
- **✋ Move Object** - Enter move mode to drag the object
- **💬 Leave Message** - Open dialog to add/edit comments

## How It Works

### Step 1: Click on Object
Click on any object you placed from the Model Library.

A blue menu appears at your mouse position:
```
┌─────────────────────┐
│  ✋ Move Object      │
│  💬 Leave Message    │
└─────────────────────┘
```

### Step 2: Choose Action

#### Option A: Move Object
1. Click **"✋ Move Object"**
2. Object gets a blue highlight
3. Click and drag anywhere to move the object
4. Release mouse to place
5. **Press ESC** to cancel move mode

#### Option B: Leave Message
1. Click **"💬 Leave Message"**
2. Dialog opens with text input
3. Type your message
4. Click "Save" to save (object will glow)
5. Click "Cancel" to close without saving

## Features

### Context Menu
- ✅ Appears at mouse position
- ✅ Auto-positions to stay on screen
- ✅ Closes when clicking outside
- ✅ Clean, modern design

### Move Mode
- ✅ Blue highlight shows object is in move mode
- ✅ Click and drag to reposition
- ✅ Object snaps to ground automatically
- ✅ Press ESC to cancel
- ✅ Automatically exits after placing

### Message System
- ✅ Same as before - right-click still works too!
- ✅ Objects with messages glow yellow
- ✅ Hover to see tooltip
- ✅ Can edit or delete messages

## Keyboard Shortcuts

| Key | Action |
|-----|--------|
| **Left Click** | Show context menu |
| **ESC** | Cancel move mode |

## Tips

### Quick Access
- **Want to move?** Click object → "Move Object" → Drag → Done
- **Want to comment?** Click object → "Leave Message" → Type → Save

### Move Mode Tips
- While in move mode (blue highlight):
  - Object follows your mouse when dragging
  - Press ESC if you change your mind
  - Click once to pick up, move mouse, click again to drop

### Menu Position
- Menu appears at your mouse cursor
- If too close to edge, automatically repositions
- Menu is always fully visible on screen

## Visual Feedback

### Object States
| State | Visual |
|-------|--------|
| Normal | Default appearance |
| Move Mode | Blue glow |
| Has Comment | Yellow glow |
| Being Dragged | Green wireframe (in Scene view) |

### Menu Colors
- **Move button**: Blue (🔵)
- **Message button**: Green (🟢)

## Compatibility

Works in both:
- ✅ Demo Scene
- ✅ Main Scene

Works with:
- ✅ Old Unity Input System
- ✅ New Unity Input System

## Technical Details

### Components Required
Every placed object needs:
1. `PlaceableObjectMover` - Handles movement and menu
2. `InteractableObject` - Handles messages and glow
3. `Collider` - For click detection

These are automatically added when placing from Model Library.

### Scene Setup
The following are auto-created if missing:
- `ObjectContextMenu` - The menu system (singleton)
- `ObjectInteractionManager` - Message dialog system (Main Scene only)

## Troubleshooting

### Context menu doesn't appear
- Make sure object has `PlaceableObjectMover` component
- Check object has a collider
- Verify you're not clicking on UI elements

### Can't drag object
- Make sure you clicked "Move Object" first
- Check console for "Entered move mode" message
- Try pressing ESC and starting over

### Message dialog doesn't open
- Make sure object has `InteractableObject` component
- Check that `ObjectInteractionManager` exists (Main Scene)
- Look for error messages in console

### Menu appears off-screen
- This shouldn't happen - menu auto-positions
- If it does, please report the screen resolution you're using

## Comparison: Old vs New

### Before (Old System)
- Left-click immediately starts dragging
- Right-click for messages
- No choice, had to remember which click does what

### After (New System)
- Left-click shows menu with choices
- Clear options: "Move" or "Message"
- More intuitive and beginner-friendly
- Right-click still works for quick message access

## Examples

### Example 1: Moving a Ring
```
1. Click on wedding_ring
2. Menu appears
3. Click "✋ Move Object"
4. Ring glows blue
5. Click and drag to new position
6. Release mouse
7. Ring placed, blue glow disappears
```

### Example 2: Leaving a Message
```
1. Click on wedding_ring
2. Menu appears
3. Click "💬 Leave Message"
4. Dialog opens
5. Type: "Wedding ring from grandmother"
6. Click "Save"
7. Ring now glows yellow
8. Hover over ring to see message tooltip
```

### Example 3: Editing a Message
```
1. Click on ring (that already has message)
2. Menu appears
3. Click "💬 Leave Message"
4. Dialog shows existing message
5. Edit text
6. Click "Save" to update
   OR "Delete" to remove message
   OR "Cancel" to keep original
```

## Advanced Usage

### Canceling Move Mode
If you're in move mode but change your mind:
1. Press **ESC** key
2. Blue highlight disappears
3. Object stays in current position
4. No changes are made

### Quick Message Access
For faster access to messages:
- **Right-click** still works!
- No need to use the context menu
- Directly opens message dialog

### Combining Actions
You can:
1. Move an object (using context menu)
2. Then add a message (using context menu or right-click)
3. Then move it again if needed
4. Message stays with the object

## Future Enhancements

Potential additions:
- Delete object option
- Duplicate object option
- Rotate object option
- Scale object option
- Copy/paste object properties

---

**Enjoy your new interactive object system!** 🎉
