using UnityEngine;

/// <summary>
/// Helper script that shows instructions when entering move mode
/// Can be disabled once users are familiar with the system
/// </summary>
public class MoveModeTutorial : MonoBehaviour
{
    private static bool _hasShownTutorial = false;
    
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        // Show tutorial on first use
        if (!_hasShownTutorial)
        {
            ShowWelcomeMessage();
            _hasShownTutorial = true;
        }
    }

    private static void ShowWelcomeMessage()
    {
        Debug.Log("========================================");
        Debug.Log("🎉 CONTEXT MENU SYSTEM LOADED!");
        Debug.Log("========================================");
        Debug.Log("How to use:");
        Debug.Log("1. Click on any placed object");
        Debug.Log("2. Choose from the menu:");
        Debug.Log("   • ✋ Move Object - Drag to reposition");
        Debug.Log("   • 💬 Leave Message - Add comments");
        Debug.Log("");
        Debug.Log("Tips:");
        Debug.Log("• Press ESC to cancel move mode");
        Debug.Log("• Right-click for quick message access");
        Debug.Log("• Objects glow blue when ready to move");
        Debug.Log("• Objects glow yellow when they have messages");
        Debug.Log("========================================");
    }
}
