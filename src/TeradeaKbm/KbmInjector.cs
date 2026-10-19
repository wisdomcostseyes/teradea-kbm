using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;

namespace TeradeaKbm
{
    /// <summary>
    /// Adds keyboard/mouse bindings to the game's InputActionAsset. Same approach as the Winds of Anthos
    /// mod: canonicalise every action on the Xbox physical layout and add keys beside each gamepad binding.
    /// </summary>
    internal static class KbmInjector
    {
        private static readonly HashSet<int> _done = new HashSet<int>();

        private static readonly string[] XboxLikeLayouts =
        {
            "Gamepad", "XInputController", "GXDKGamepad", "DualShockGamepad", "PS4DualShockGamepad", "PS5DualSenseGamepad"
        };

        public static void Apply(PlayerInput playerInput)
        {
            var log = Plugin.Log;
            var cfg = Plugin.Cfg;

            if (playerInput == null)
            {
                log.LogWarning("InputSystemManager has no PlayerInput component; nothing to patch.");
                return;
            }
            var asset = playerInput.actions;
            if (asset == null)
            {
                log.LogWarning("PlayerInput has no actions asset; nothing to patch.");
                return;
            }
            if (!_done.Add(asset.GetInstanceID()))
            {
                log.LogInfo("Asset already patched; skipping.");
                return;
            }

            string dumpDir = Path.Combine(Paths.BepInExRootPath, "teradea-kbm");
            if (cfg.DumpAsset.Value)
            {
                try
                {
                    Directory.CreateDirectory(dumpDir);
                    File.WriteAllText(Path.Combine(dumpDir, "actions.before.json"), asset.ToJson());
                }
                catch (Exception e) { log.LogError("Dump failed: " + e); }
            }

            var settings = InputSystem.settings;
            var supported = settings.supportedDevices;
            if (cfg.AllowAllDevices.Value && supported.Count > 0)
            {
                var names = new List<string>();
                for (int i = 0; i < supported.Count; i++) names.Add(supported[i]);
                log.LogInfo("Clearing supportedDevices (was: " + string.Join(", ", names) + ")");
                settings.supportedDevices = new ReadOnlyArray<string>(new Il2CppStringArray(0));
            }
            log.LogInfo("Devices now: " + DeviceList());

            if (!cfg.InjectBindings.Value)
            {
                log.LogWarning("InjectBindings=false: leaving the input asset untouched (diagnostic mode).");
                DeviceTracker.Init(Gamepad.current == null);
                return;
            }

            if (cfg.StripControlSchemes.Value)
            {
                var schemes = asset.controlSchemes;
                var names = new List<string>();
                for (int i = 0; i < schemes.Count; i++) names.Add(schemes[i].name);
                foreach (var name in names)
                {
                    try { asset.RemoveControlScheme(name); }
                    catch (Exception e) { log.LogWarning("RemoveControlScheme(" + name + ") failed: " + e.Message); }
                }
                log.LogInfo("Removed control schemes: " + string.Join(", ", names));
            }

            int erased = 0, added = 0;
            var maps = asset.actionMaps;
            for (int m = 0; m < maps.Count; m++) erased += EraseTemplateBindings(maps[m]);
            for (int m = 0; m < maps.Count; m++) added += InjectMap(maps[m]);
            log.LogInfo("Erased " + erased + " leftover non-gamepad template bindings; injected " + added + " keyboard/mouse bindings.");

            // Drop the PlayerInput user so its control-scheme mask doesn't hide the new bindings.
            try
            {
                var user = playerInput.user;
                if (user.valid)
                {
                    log.LogInfo("Dropping PlayerInput user (scheme=" + (playerInput.currentControlScheme ?? "none") + ").");
                    user.UnpairDevicesAndRemoveUser();
                }
            }
            catch (Exception e) { log.LogWarning("Could not reset PlayerInput user: " + e.Message); }
            try { asset.bindingMask = new Il2CppSystem.Nullable<InputBinding>(); }
            catch (Exception e) { log.LogWarning("Could not clear binding mask: " + e.Message); }
            try { asset.devices = new Il2CppSystem.Nullable<ReadOnlyArray<InputDevice>>(); }
            catch (Exception e) { log.LogWarning("Could not clear device mask: " + e.Message); }

            DeviceTracker.Init(Gamepad.current == null);

            if (cfg.DumpAsset.Value)
            {
                try
                {
                    File.WriteAllText(Path.Combine(dumpDir, "actions.after.json"), asset.ToJson());
                    File.WriteAllLines(Path.Combine(dumpDir, "playerinput.txt"), new[]
                    {
                        "asset: " + asset.name,
                        "defaultControlScheme: " + playerInput.defaultControlScheme,
                        "defaultActionMap: " + playerInput.defaultActionMap,
                        "neverAutoSwitchControlSchemes: " + playerInput.neverAutoSwitchControlSchemes,
                        "devices: " + DeviceList(),
                    });
                }
                catch (Exception e) { log.LogError("Dump (after) failed: " + e); }
            }
        }

        private static string DeviceList()
        {
            var devs = InputSystem.devices;
            var parts = new List<string>();
            for (int i = 0; i < devs.Count; i++) parts.Add(devs[i].layout + ":" + devs[i].name);
            return string.Join(", ", parts);
        }

        private static List<InputBinding> Snapshot(InputActionMap map)
        {
            var bs = map.bindings;
            var list = new List<InputBinding>(bs.Count);
            for (int i = 0; i < bs.Count; i++) list.Add(bs[i]);
            return list;
        }

        /// <summary>
        /// Disable the game's own keyboard/mouse/touch bindings so our config is the single source of truth.
        /// (The asset ships with a complete Keyboard&amp;Mouse scheme that the device whitelist made unreachable.)
        /// Erasing through BindingSyntax doesn't survive the IL2CPP interop, so bindings are disabled with an
        /// empty override path instead, which the Input System treats as "no binding".
        /// </summary>
        private static int EraseTemplateBindings(InputActionMap map)
        {
            int disabled = 0;
            var actions = map.actions;
            for (int a = 0; a < actions.Count; a++)
            {
                var action = actions[a];
                var bs = action.bindings;
                for (int i = 0; i < bs.Count; i++)
                {
                    var b = bs[i];
                    if (b.isComposite || !IsNonGamepadPath(b.path)) continue;
                    try
                    {
                        action.ApplyBindingOverride(i, "");
                        disabled++;
                    }
                    catch (Exception e)
                    {
                        Plugin.Log.LogWarning("Disable binding " + b.path + " on '" + action.name + "' failed: " + e.Message);
                    }
                }
            }
            return disabled;
        }

        private static bool IsNonGamepadPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            return path.StartsWith("<Keyboard>", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("<Mouse>", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("<Pointer>", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("<Touchscreen>", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("<Joystick>", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("<XRController>", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("<Pen>", StringComparison.OrdinalIgnoreCase);
        }

        private static int InjectMap(InputActionMap map)
        {
            var cfg = Plugin.Cfg;
            var log = Plugin.Log;
            int added = 0;

            var excluded = new HashSet<string>(Split(cfg.ExcludeActions.Value), StringComparer.OrdinalIgnoreCase);
            var mouseLookActions = new HashSet<string>(Split(cfg.MouseLookActions.Value), StringComparer.OrdinalIgnoreCase);

            var bindings = Snapshot(map);
            var have = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var b in bindings)
            {
                if (b.isComposite || string.IsNullOrEmpty(b.action)) continue;
                if (IsNonGamepadPath(b.path)) continue; // disabled above; ours may re-add the same path
                Set(have, b.action).Add(b.path);
            }

            // Mouse look. The game has its own mouse-look action (LookMouse, a raw pointer delta the devs left
            // bound but unreachable); feed it from the mouse with our sensitivity. Any other action listed gets
            // the delta too (for games/maps where the camera reads the stick action instead).
            foreach (var lookName in mouseLookActions)
            {
                var look = map.FindAction(lookName, false);
                if (look == null) continue;
                if (!Set(have, look.name).Add("<Mouse>/delta")) continue;
                float s = cfg.MouseSensitivity.Value;
                float sy = cfg.MouseInvertY.Value ? -s : s;
                string proc = string.Format(System.Globalization.CultureInfo.InvariantCulture, "scaleVector2(x={0},y={1})", s, sy);
                look.AddBinding("<Mouse>/delta", null, proc, null);
                MouseLookGate.Register(look, look.bindings.Count - 1);
                added++;
            }

            for (int i = 0; i < bindings.Count; i++)
            {
                var b = bindings[i];
                string actionName = b.action;
                if (string.IsNullOrEmpty(actionName)) continue;
                if (excluded.Contains(actionName)) continue;
                var action = map.FindAction(actionName, false);
                if (action == null) continue;

                if (b.isComposite)
                {
                    var parts = new List<InputBinding>();
                    for (int j = i + 1; j < bindings.Count && bindings[j].isPartOfComposite; j++) parts.Add(bindings[j]);
                    i += parts.Count;

                    var translated = new List<KeyValuePair<string, string>>();
                    foreach (var p in parts)
                    {
                        string canon = Canonical(p.path);
                        if (canon == null) continue;
                        foreach (var kp in KeysFor(canon))
                        {
                            if (kp.StartsWith("<Mouse>/scroll", StringComparison.OrdinalIgnoreCase)) continue;
                            translated.Add(new KeyValuePair<string, string>(p.name, kp));
                        }
                    }
                    if (translated.Count == 0) continue;
                    var partNames = parts.Select(p => p.name).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                    if (partNames.Any(pn => !translated.Any(t => string.Equals(t.Key, pn, StringComparison.OrdinalIgnoreCase)))) continue;

                    string key = "composite:" + b.path + ":" + string.Join(",", translated.Select(t => t.Key + "=" + t.Value));
                    if (!Set(have, action.name).Add(key)) continue;

                    var syntax = action.AddCompositeBinding(b.path, b.interactions, b.processors);
                    foreach (var t in translated) syntax = syntax.With(t.Key, t.Value, null);
                    added++;
                    continue;
                }
                if (b.isPartOfComposite) continue;

                string control = Canonical(b.path);
                if (control == null) continue;

                if (control == "leftStick" || control == "rightStick" || control == "dpad")
                {
                    var keys = StickKeys(control);
                    if (keys != null && Set(have, action.name).Add("stick:" + control))
                    {
                        var syntax = action.AddCompositeBinding("2DVector(mode=2)", b.interactions, null);
                        string[] partNames = { "Up", "Down", "Left", "Right" };
                        for (int k = 0; k < 4; k++)
                        {
                            if (!string.IsNullOrEmpty(keys[k])) syntax = syntax.With(partNames[k], keys[k], null);
                        }
                        added++;
                    }
                    continue;
                }

                foreach (var path in KeysFor(control))
                {
                    if (!Set(have, action.name).Add(path)) continue;
                    string processors = b.processors;
                    if (path.StartsWith("<Mouse>/scroll", StringComparison.OrdinalIgnoreCase)) processors = "clamp(min=0,max=1)";
                    action.AddBinding(path, b.interactions, processors, null);
                    added++;
                }
            }

            if (added > 0) log.LogDebug("Map '" + map.name + "': +" + added);
            return added;
        }

        private static HashSet<string> Set(Dictionary<string, HashSet<string>> d, string key)
        {
            if (!d.TryGetValue(key, out var set)) d[key] = set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            return set;
        }

        /// <summary>Binding path → canonical Xbox-layout control name, or null to ignore (Nintendo layouts etc.).</summary>
        private static string Canonical(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            if (path.StartsWith("*/{", StringComparison.Ordinal))
            {
                string usage = path.Substring(3).TrimEnd('}');
                if (usage.Equals("Submit", StringComparison.OrdinalIgnoreCase)) return "buttonSouth";
                if (usage.Equals("Cancel", StringComparison.OrdinalIgnoreCase)) return "buttonEast";
                if (usage.Equals("Menu", StringComparison.OrdinalIgnoreCase)) return "start";
                return null;
            }
            if (!path.StartsWith("<")) return null;
            int close = path.IndexOf('>');
            if (close < 0 || close + 1 >= path.Length || path[close + 1] != '/') return null;
            string layout = path.Substring(1, close - 1);
            if (!XboxLikeLayouts.Contains(layout)) return null;
            string control = path.Substring(close + 2);
            if (control == "touchpadButton") return "select";
            return control;
        }

        // "<Gamepad>/*" style wildcard bindings ("Any": press anything) get the keyboard's anyKey and the mouse buttons.
        private static readonly string[] AnyKeyPaths = { "<Keyboard>/anyKey", "<Mouse>/leftButton", "<Mouse>/rightButton" };

        private static string[] StickKeys(string control)
        {
            var cfg = Plugin.Cfg;
            string raw;
            switch (control)
            {
                case "leftStick": raw = cfg.LeftStick.Value; break;
                case "rightStick": raw = cfg.RightStick.Value; break;
                case "dpad": raw = First(cfg.DpadUp.Value) + ";" + First(cfg.DpadDown.Value) + ";" + First(cfg.DpadLeft.Value) + ";" + First(cfg.DpadRight.Value); break;
                default: return null;
            }
            var parts = SplitKeep(raw).Select(p => p.Length == 0 ? "" : ToPath(p)).ToArray();
            return parts.Length == 4 && parts.Any(p => p.Length > 0) ? parts : null;
        }

        private static string First(string raw) => Split(raw).FirstOrDefault() ?? "";

        /// <summary>Like Split but keeps empty entries, for positional lists (up;down;left;right).</summary>
        internal static string[] SplitKeep(string raw)
        {
            if (raw == null) return new string[0];
            return raw.Split(';').Select(s => s.Trim()).ToArray();
        }

        private static IEnumerable<string> KeysFor(string control)
        {
            var cfg = Plugin.Cfg;
            string raw;
            switch (control)
            {
                case "buttonSouth": raw = cfg.ButtonSouth.Value; break;
                case "buttonEast": raw = cfg.ButtonEast.Value; break;
                case "buttonWest": raw = cfg.ButtonWest.Value; break;
                case "buttonNorth": raw = cfg.ButtonNorth.Value; break;
                case "leftShoulder": raw = cfg.LeftShoulder.Value; break;
                case "rightShoulder": raw = cfg.RightShoulder.Value; break;
                case "leftTrigger": raw = cfg.LeftTrigger.Value; break;
                case "rightTrigger": raw = cfg.RightTrigger.Value; break;
                case "leftStickPress": raw = cfg.LeftStickPress.Value; break;
                case "rightStickPress": raw = cfg.RightStickPress.Value; break;
                case "start": raw = cfg.Start.Value; break;
                case "select": raw = cfg.Select.Value; break;
                case "dpad/up": raw = cfg.DpadUp.Value; break;
                case "dpad/down": raw = cfg.DpadDown.Value; break;
                case "dpad/left": raw = cfg.DpadLeft.Value; break;
                case "dpad/right": raw = cfg.DpadRight.Value; break;
                case "leftStick/up": raw = Pick(cfg.LeftStick.Value, 0); break;
                case "leftStick/down": raw = Pick(cfg.LeftStick.Value, 1); break;
                case "leftStick/left": raw = Pick(cfg.LeftStick.Value, 2); break;
                case "leftStick/right": raw = Pick(cfg.LeftStick.Value, 3); break;
                case "rightStick/up": raw = Pick(cfg.RightStick.Value, 0); break;
                case "rightStick/down": raw = Pick(cfg.RightStick.Value, 1); break;
                case "rightStick/left": raw = Pick(cfg.RightStick.Value, 2); break;
                case "rightStick/right": raw = Pick(cfg.RightStick.Value, 3); break;
                case "*": return AnyKeyPaths;
                default:
                    Plugin.Log.LogDebug("No KBM translation for gamepad control '" + control + "'");
                    return Enumerable.Empty<string>();
            }
            return Split(raw).Select(ToPath).ToList();
        }

        private static string Pick(string raw, int idx)
        {
            var parts = SplitKeep(raw);
            return idx < parts.Length && parts[idx].Length > 0 ? parts[idx] : null;
        }

        internal static IEnumerable<string> Split(string raw)
        {
            if (string.IsNullOrEmpty(raw)) yield break;
            foreach (var s in raw.Split(';'))
            {
                var t = s.Trim();
                if (t.Length > 0) yield return t;
            }
        }

        internal static string ToPath(string item) => item.StartsWith("<") ? item : "<Keyboard>/" + item;
    }

    /// <summary>
    /// The game feeds its mouse-look action into menus too (tab strips page with it), so the mouse-delta
    /// bindings are only live while a gameplay action set is current.
    /// </summary>
    internal static class MouseLookGate
    {
        private struct Entry { public InputAction Action; public int Index; }
        private static readonly List<Entry> _entries = new List<Entry>();
        private static bool _enabled = true;

        public static void Register(InputAction action, int bindingIndex)
        {
            _entries.Add(new Entry { Action = action, Index = bindingIndex });
        }

        public static void Tick()
        {
            if (_entries.Count == 0) return;
            bool want = CursorControl.InGameplayMap();
            if (want == _enabled) return;
            _enabled = want;
            foreach (var e in _entries)
            {
                try
                {
                    if (want) e.Action.RemoveBindingOverride(e.Index);
                    else e.Action.ApplyBindingOverride(e.Index, "");
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogDebug("Mouse look gate failed: " + ex.Message);
                }
            }
        }
    }
}
