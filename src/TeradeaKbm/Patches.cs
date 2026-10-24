using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using App.Input;
using App.Chara;

namespace TeradeaKbm
{
    internal static class Patches
    {
        private static bool _lateInstalled;
        internal static bool Ready;

        /// <summary>Everything that must not run from the BepInEx chainloader (engine input not initialised yet).</summary>
        private static void InstallLate()
        {
            if (_lateInstalled) return;
            _lateInstalled = true;
            DeviceTracker.Install();
            if (Plugin.Cfg.KbmPrompts.Value)
            {
                try
                {
                    Plugin.HarmonyInstance.PatchAll(typeof(PromptPatches));
                    Plugin.Log.LogInfo("Prompt patches installed.");
                }
                catch (Exception e)
                {
                    Plugin.Log.LogError("Prompt patches failed: " + e);
                }
            }
            if (Plugin.Cfg.CircleMenuWasd.Value)
            {
                try
                {
                    Plugin.HarmonyInstance.PatchAll(typeof(CircleMenuPatches));
                    Plugin.Log.LogInfo("Circle menu patch installed (WASD picks).");
                }
                catch (Exception e)
                {
                    Plugin.Log.LogError("Circle menu patch failed: " + e);
                }
            }
            Ready = true;
        }

        // Runs before the game caches its action maps and before PlayerInput.OnEnable pairs devices.
        [HarmonyPatch(typeof(InputSystemManager), nameof(InputSystemManager.Awake))]
        [HarmonyPrefix]
        private static void InputSystemManager_Awake_Prefix(InputSystemManager __instance)
        {
            try
            {
                InstallLate();
                var pi = __instance._playerInput;
                if (pi == null) pi = __instance.GetComponent<PlayerInput>();
                KbmInjector.Apply(pi);
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("KBM injection failed: " + e);
            }
        }

        [HarmonyPatch(typeof(InputSystemManager), nameof(InputSystemManager.Awake))]
        [HarmonyPostfix]
        private static void InputSystemManager_Awake_Postfix(InputSystemManager __instance)
        {
            try
            {
                var pi = __instance._playerInput;
                if (pi == null) { Plugin.Log.LogInfo("InputSystemManager.Awake done; no PlayerInput yet."); return; }
                var user = pi.user;
                string devs = "(user invalid)";
                if (user.valid)
                {
                    var paired = user.pairedDevices;
                    var names = new List<string>();
                    for (int i = 0; i < paired.Count; i++) names.Add(paired[i].layout + ":" + paired[i].name);
                    devs = string.Join(", ", names);
                }
                var map = pi.currentActionMap;
                Plugin.Log.LogInfo("InputSystemManager ready: map=" + (map != null ? map.name : "null")
                    + " scheme=" + (pi.currentControlScheme ?? "none") + " paired=[" + devs + "]");
            }
            catch (Exception e)
            {
                Plugin.Log.LogDebug("Awake postfix log failed: " + e.Message);
            }
        }

        // Keyboard movement is always magnitude 1. Hold a modifier to walk (half magnitude).
        [HarmonyPatch(typeof(PlayerInputController), nameof(PlayerInputController.Update))]
        [HarmonyPostfix]
        private static void PlayerInputController_Update_Postfix(PlayerInputController __instance)
        {
            try
            {
                var key = Plugin.Cfg?.WalkModifierKey.Value;
                if (string.IsNullOrEmpty(key) || !DeviceTracker.UsingKbm) return;
                var kb = Keyboard.current;
                if (kb == null) return;
                var ctrl = kb.TryGetChildControl(key);
                var btn = ctrl != null ? ctrl.TryCast<ButtonControl>() : null;
                if (btn == null || !btn.isPressed) return;
                var v = __instance._rawMoveVec;
                if (v.sqrMagnitude > 0.25f) __instance._rawMoveVec = v.normalized * 0.4f;
            }
            catch (Exception e)
            {
                Plugin.Log.LogDebug("Walk modifier failed: " + e.Message);
            }
        }

        // Escape is both B (cancel) and Start (menu) in the field. When the menu action fires, swallow the cancel press.
        [HarmonyPatch(typeof(PlayerInputController), nameof(PlayerInputController.IsTriggerCancel))]
        [HarmonyPostfix]
        private static void PlayerInputController_IsTriggerCancel_Postfix(ref bool __result)
        {
            if (!__result || Plugin.Cfg == null || !Plugin.Cfg.SuppressCancelOnMenuKey.Value) return;
            try
            {
                if (!SingletonMonoBehaviour<InputSystemManager>.ExistsInstance) return;
                var mgr = SingletonMonoBehaviour<InputSystemManager>.Instance;
                var map = mgr.GetActionMap(ActionSetId.Player);
                var menu = map?.FindAction(ActionMapLabels.Menu, false);
                if (menu != null && menu.triggered && IsKeyboardOrMouse(menu.activeControl)) __result = false;
            }
            catch
            {
                // never break input over a nicety
            }
        }

        private static bool IsKeyboardOrMouse(InputControl c)
        {
            var d = c?.device;
            return d != null && (d.TryCast<Keyboard>() != null || d.TryCast<Mouse>() != null);
        }
    }
}
