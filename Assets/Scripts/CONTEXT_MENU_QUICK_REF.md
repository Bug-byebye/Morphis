# Context Menu System - Quick Reference

## 🎯 How to Use

### Click on Object → Menu Appears

```
        Mouse Click
            ↓
    ┌─────────────────────┐
    │  ✋ Move Object      │ ← Click to enter move mode
    │  💬 Leave Message    │ ← Click to add/edit comment
    └─────────────────────┘
```

## 🔄 Workflow Examples

### Moving an Object
```
Click Object
    ↓
Select "✋ Move Object"
    ↓
Object glows BLUE 🔵
    ↓
Click & Drag to new position
    ↓
Release mouse
    ↓
Done! (or press ESC to cancel)
```

### Adding a Message
```
Click Object
    ↓
Select "💬 Leave Message"
    ↓
Dialog opens
    ↓
Type message
    ↓
Click "Save"
    ↓
Object glows YELLOW 🟡
```

## 🎨 Visual States

| State | Color | What it Means |
|-------|-------|---------------|
| Normal | Default | Regular object |
| Move Mode | 🔵 Blue glow | Ready to be moved |
| Has Message | 🟡 Yellow glow | Contains a comment |
| Being Dragged | 🟢 Green | Currently moving |

## ⌨️ Controls

| Input | Action |
|-------|--------|
| **Left Click** on object | Show context menu |
| **Right Click** on object | Quick access to messages |
| **ESC** (in move mode) | Cancel moving |
| **Click outside menu** | Close menu |

## 📋 Quick Tips

✅ **DO:**
- Click object → choose action from menu
- Use ESC to cancel move mode
- Right-click for quick message access

❌ **DON'T:**
- Try to drag without selecting "Move Object" first
- Forget to click "Save" after typing message

## 🔧 Files Created

1. `ObjectContextMenu.cs` - The context menu UI
2. `PlaceableObjectMover.cs` - Updated with menu integration
3. `ContextMenuBootstrap.cs` - Auto-setup
4. `CONTEXT_MENU_GUIDE.md` - Full documentation

## 🎮 Demo Flow

```
┌─────────────────────────────────────────┐
│  1. Place object from Model Library     │
│     ↓                                    │
│  2. Click on placed object              │
│     ↓                                    │
│  3. Context menu appears                │
│     ↓                                    │
│  ┌──────────┐         ┌──────────────┐  │
│  │   Move   │    OR   │   Message    │  │
│  └──────────┘         └──────────────┘  │
│       ↓                      ↓           │
│  Blue glow              Dialog opens    │
│  Drag object            Type & save     │
│  Done!                  Yellow glow     │
└─────────────────────────────────────────┘
```

## 🐛 Common Issues

| Problem | Solution |
|---------|----------|
| Menu doesn't appear | Make sure object was placed from Model Library |
| Can't drag | Did you select "Move Object" first? |
| No message dialog | Make sure you're in Main Scene |
| Menu appears in wrong place | It auto-adjusts - this shouldn't happen |

---

**Happy Building!** 🏗️
