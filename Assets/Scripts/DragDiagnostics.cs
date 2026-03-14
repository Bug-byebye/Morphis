using UnityEngine;
using Morphis.ModelPlacement;

/// <summary>
/// Diagnostic tool to debug why dragging isn't working in MainScene
/// Attach this to any object in the scene or run from Unity menu
/// </summary>
public class DragDiagnostics : MonoBehaviour
{
    [Header("Run Diagnostics")]
    [SerializeField] private bool runOnStart = true;
    
    private void Start()
    {
        if (runOnStart)
        {
            RunDiagnostics();
        }
    }

    [ContextMenu("Run Drag Diagnostics")]
    public void RunDiagnostics()
    {
        Debug.Log("========== DRAG DIAGNOSTICS START ==========");
        
        // 1. Check for PlaceableObjectMover components
        var movers = FindObjectsOfType<PlaceableObjectMover>();
        Debug.Log($"✓ Found {movers.Length} objects with PlaceableObjectMover component:");
        foreach (var mover in movers)
        {
            Debug.Log($"  - {mover.gameObject.name} at position {mover.transform.position}");
            
            // Check if it has colliders
            var colliders = mover.GetComponentsInChildren<Collider>();
            Debug.Log($"    Colliders: {colliders.Length}");
            foreach (var col in colliders)
            {
                Debug.Log($"      * {col.GetType().Name} on {col.gameObject.name} (enabled: {col.enabled})");
            }
        }
        
        // 2. Check for ObjectInteractionManager
        var interactionManager = FindObjectOfType<ObjectInteractionManager>();
        if (interactionManager != null)
        {
            Debug.Log($"✓ ObjectInteractionManager found: {interactionManager.gameObject.name}");
        }
        else
        {
            Debug.Log("⚠ ObjectInteractionManager NOT found");
        }
        
        // 3. Check for Camera
        var cam = Camera.main;
        if (cam != null)
        {
            Debug.Log($"✓ Main Camera found: {cam.gameObject.name}");
        }
        else
        {
            Debug.Log("❌ Main Camera NOT found - This will break dragging!");
        }
        
        // 4. Check Input System
        bool newInputAvailable = UnityEngine.InputSystem.Mouse.current != null;
        Debug.Log($"Input System: {(newInputAvailable ? "New Input System" : "Old Input System")}");
        
        // 5. Check for EventSystem
        var eventSystem = UnityEngine.EventSystems.EventSystem.current;
        if (eventSystem != null)
        {
            Debug.Log($"✓ EventSystem found: {eventSystem.gameObject.name}");
        }
        else
        {
            Debug.Log("⚠ EventSystem NOT found (UI interactions may not work)");
        }
        
        // 6. List all InteractableObjects
        var interactables = FindObjectsOfType<InteractableObject>();
        Debug.Log($"✓ Found {interactables.Length} objects with InteractableObject component:");
        foreach (var obj in interactables)
        {
            Debug.Log($"  - {obj.gameObject.name} (HasComment: {obj.HasComment})");
        }
        
        Debug.Log("========== DRAG DIAGNOSTICS END ==========");
        Debug.Log("Instructions:");
        Debug.Log("1. Make sure objects have PlaceableObjectMover component");
        Debug.Log("2. Make sure Camera.main exists");
        Debug.Log("3. Try left-clicking and dragging an object with PlaceableObjectMover");
        Debug.Log("4. Check Console for '[PlaceableObjectMover] Started dragging...' message");
    }

    private void Update()
    {
        // Real-time click detection for debugging
        if (Input.GetMouseButtonDown(0) || (UnityEngine.InputSystem.Mouse.current?.leftButton.wasPressedThisFrame ?? false))
        {
            Vector2 mousePos = UnityEngine.InputSystem.Mouse.current?.position.ReadValue() ?? (Vector2)Input.mousePosition;
            Debug.Log($"[DragDiagnostics] Mouse clicked at screen position: {mousePos}");
            
            var cam = Camera.main;
            if (cam != null)
            {
                var ray = cam.ScreenPointToRay(mousePos);
                if (Physics.Raycast(ray, out var hit))
                {
                    Debug.Log($"[DragDiagnostics] Raycast hit: {hit.collider.gameObject.name}");
                    
                    var mover = hit.collider.GetComponentInParent<PlaceableObjectMover>();
                    if (mover != null)
                    {
                        Debug.Log($"[DragDiagnostics] ✓ Hit object HAS PlaceableObjectMover - should be draggable!");
                    }
                    else
                    {
                        Debug.Log($"[DragDiagnostics] ✗ Hit object does NOT have PlaceableObjectMover - not draggable");
                    }
                }
                else
                {
                    Debug.Log($"[DragDiagnostics] Raycast missed - clicked on empty space or UI");
                }
            }
        }
    }
}
