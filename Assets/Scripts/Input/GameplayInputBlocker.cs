using System.Collections.Generic;
using UnityEngine;

namespace Morphis.InputControl
{
    /// <summary>
    /// Global gameplay input gate for modal UI.
    /// When any UI owner blocks input, movement/look/jump/sprint should be ignored.
    /// </summary>
    public static class GameplayInputBlocker
    {
        private static readonly HashSet<int> Owners = new HashSet<int>();

        public static bool IsBlocked => Owners.Count > 0;

        public static void SetBlocked(Object owner, bool blocked)
        {
            if (owner == null) return;

            int key = owner.GetInstanceID();
            if (blocked) Owners.Add(key);
            else Owners.Remove(key);
        }
    }
}
