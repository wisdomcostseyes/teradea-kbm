using System;
using HarmonyLib;
using UnityEngine.InputSystem;
using App.UI;

namespace TeradeaKbm
{
    /// <summary>
    /// The in-game circle menus (dowsing wheel, riding-animal wheel) are held open with a d-pad key and picked with
    /// the right stick: UIUnitCircleSelectMenuRadialCursorSelector.GetCursorSelectType turns the UI right-stick
    /// navigation into Up/Down/Left/Right. On the keyboard the right stick is the arrow keys, which share the d-pad
    /// key that is being held, so pick with WASD instead while keyboard/mouse is the active device.
    /// Installed late (see Patches.InstallLate) when Keys.CircleMenuWasd is on.
    /// </summary>
    internal static class CircleMenuPatches
    {
        [HarmonyPatch(typeof(UIUnitCircleSelectMenuRadialCursorSelector), nameof(UIUnitCircleSelectMenuRadialCursorSelector.GetCursorSelectType))]
        [HarmonyPostfix]
        private static void GetCursorSelectType_Postfix(ref UIUnitCircleSelectMenuRadialSelectType __result)
        {
            try
            {
                if (!DeviceTracker.UsingKbm) return;
                var kb = Keyboard.current;
                if (kb == null) return;
                // Keyboard owns the pick: the arrow keys (right stick in menus) are ignored here, since one of them is
                // the key holding the wheel open.
                if (kb.wKey.isPressed) __result = UIUnitCircleSelectMenuRadialSelectType.Up;
                else if (kb.sKey.isPressed) __result = UIUnitCircleSelectMenuRadialSelectType.Down;
                else if (kb.aKey.isPressed) __result = UIUnitCircleSelectMenuRadialSelectType.Left;
                else if (kb.dKey.isPressed) __result = UIUnitCircleSelectMenuRadialSelectType.Right;
                else __result = UIUnitCircleSelectMenuRadialSelectType.None;
            }
            catch (Exception)
            {
                // never break the menu over a nicety
            }
        }
    }
}
