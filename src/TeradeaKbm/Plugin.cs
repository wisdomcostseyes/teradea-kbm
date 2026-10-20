using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.Injection;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using App.Input;

namespace TeradeaKbm
{
    [BepInPlugin(GUID, NAME, VERSION)]
    public class Plugin : BasePlugin
    {
        public const string GUID = "EchoesOfTeradea.KeyboardMouse";
        public const string NAME = "Echoes of Teradea Keyboard & Mouse";
        public const string VERSION = "0.0.4";

        internal static new ManualLogSource Log;
        internal static KbmConfig Cfg;
        internal static Harmony HarmonyInstance;

        public override void Load()
        {
            Log = base.Log;
            Cfg = new KbmConfig(Config);

            HarmonyInstance = new Harmony(GUID);
            HarmonyInstance.PatchAll(typeof(Patches));
            // Nothing else here: the engine's input isn't up yet. Device tracking and the prompt patches are
            // installed from the InputSystemManager.Awake prefix (Patches.InstallLate).

            AddComponent<Ticker>();
            Log.LogInfo($"{NAME} {VERSION} loaded.");
        }
    }

    /// <summary>Per-frame work and shutdown teardown, as an injected MonoBehaviour.</summary>
    public class Ticker : MonoBehaviour
    {
        public Ticker(IntPtr ptr) : base(ptr) { }

        public void Update()
        {
            try { CursorControl.Tick(); }
            catch (Exception e) { Plugin.Log.LogDebug("Tick failed: " + e.Message); }
            if (Patches.Ready)
            {
                try { MouseLookGate.Tick(); }
                catch (Exception e) { Plugin.Log.LogDebug("Mouse gate tick failed: " + e.Message); }
            }
            try { PromptOverlay.TickAll(); }
            catch (Exception e) { Plugin.Log.LogDebug("Prompt tick failed: " + e.Message); }
        }

        public void OnApplicationQuit()
        {
            try
            {
                PromptOverlay.RestoreAll();
                DeviceTracker.Uninstall();
                Plugin.Log.LogInfo("Shutdown cleanup done.");
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Shutdown cleanup failed: " + e.Message);
            }
        }
    }

    internal class KbmConfig
    {
        public readonly ConfigEntry<bool> DumpAsset;
        public readonly ConfigEntry<bool> LogPrompts;
        public readonly ConfigEntry<bool> StripControlSchemes;
        public readonly ConfigEntry<bool> AllowAllDevices;
        public readonly ConfigEntry<string> ExcludeActions;
        public readonly ConfigEntry<bool> InjectBindings;

        public readonly ConfigEntry<bool> LockCursor;
        public readonly ConfigEntry<string> CursorUnlockKey;
        public readonly ConfigEntry<string> MouseLookActions;
        public readonly ConfigEntry<float> MouseSensitivity;
        public readonly ConfigEntry<bool> MouseInvertY;

        public readonly ConfigEntry<bool> KbmPrompts;
        public readonly ConfigEntry<string> TextPromptFormat;
        public readonly ConfigEntry<string> GlyphCharMap;
        public readonly ConfigEntry<bool> DumpGlyphs;
        public readonly ConfigEntry<string> CustomKeyImageDir;

        public readonly ConfigEntry<string> WalkModifierKey;
        public readonly ConfigEntry<bool> SuppressCancelOnMenuKey;

        public readonly ConfigEntry<string> ButtonSouth;
        public readonly ConfigEntry<string> ButtonEast;
        public readonly ConfigEntry<string> ButtonWest;
        public readonly ConfigEntry<string> ButtonNorth;
        public readonly ConfigEntry<string> LeftShoulder;
        public readonly ConfigEntry<string> RightShoulder;
        public readonly ConfigEntry<string> LeftTrigger;
        public readonly ConfigEntry<string> RightTrigger;
        public readonly ConfigEntry<string> LeftStickPress;
        public readonly ConfigEntry<string> RightStickPress;
        public readonly ConfigEntry<string> Start;
        public readonly ConfigEntry<string> Select;
        // Directions are fixed: the prompt glyphs are drawn as a WASD cluster and an arrow cross.
        public readonly FixedEntry DpadUp = new FixedEntry("upArrow");
        public readonly FixedEntry DpadDown = new FixedEntry("downArrow");
        public readonly FixedEntry DpadLeft = new FixedEntry("leftArrow");
        public readonly FixedEntry DpadRight = new FixedEntry("rightArrow");
        public readonly FixedEntry LeftStick = new FixedEntry("w;s;a;d");
        // The right stick is the camera in the field (that's the mouse) and list/sub-tab navigation in menus.
        // It takes the arrow keys, shared with the d-pad: on the few screens that use both (Tips pages vs sub-tabs)
        // one press does both, which players preferred to learning bracket keys. These keys are only live outside gameplay (see MenuOnlyGate), so arrows in the field stay
        // quick actions and never tilt the camera. Entries are up;down;left;right.
        public readonly FixedEntry RightStick = new FixedEntry("upArrow;downArrow;leftArrow;rightArrow");

        internal class FixedEntry
        {
            public readonly string Value;
            public FixedEntry(string v) { Value = v; }
        }

        public KbmConfig(ConfigFile f)
        {
            DumpAsset = f.Bind("Debug", "DumpInputAsset", false,
                "Write the game's InputActionAsset JSON (before and after injection) to BepInEx/teradea-kbm/ on startup.");
            LogPrompts = f.Bind("Debug", "LogPrompts", false,
                "Log layout details for every glyph image the mod replaces (once per sprite name).");
            StripControlSchemes = f.Bind("Advanced", "StripControlSchemes", true,
                "Remove control schemes from the input asset so PlayerInput pairs every device that has bindings.");
            AllowAllDevices = f.Bind("Advanced", "AllowAllDevices", true,
                "Clear InputSystem.settings.supportedDevices. The game ships with a gamepad-only whitelist, which is why keyboard and mouse are dead without this.");
            InjectBindings = f.Bind("Advanced", "InjectBindings", true,
                "Diagnostic: when false, the input asset is left untouched. Devices are still enabled.");
            ExcludeActions = f.Bind("Advanced", "ExcludeActions", "DebugMenu",
                "Action names that never receive keyboard/mouse bindings (';'-separated).");

            LockCursor = f.Bind("Mouse", "LockCursor", true,
                "Lock and hide the mouse cursor while the game window is focused (needed for mouse look).");
            CursorUnlockKey = f.Bind("Mouse", "CursorUnlockHoldKey", "leftAlt",
                "Hold this keyboard key to temporarily release the cursor. Empty disables.");
            MouseLookActions = f.Bind("Mouse", "MouseLookActions", "Look",
                "Action names that receive <Mouse>/delta. 'Look' is the right-stick camera action, so the camera keeps its follow behaviour. 'LookMouse' is the game's own touch-style drag camera, which never follows the player (0.0.1 used it).");
            MouseSensitivity = f.Bind("Mouse", "Sensitivity", 0.08f,
                "Scale applied to raw mouse delta (pixels per frame) before it is fed to the camera as a stick value.");
            // 0.0.1 defaulted to the drag camera; move users who still have those exact defaults to the new ones.
            if (MouseLookActions.Value == "LookMouse" && Math.Abs(MouseSensitivity.Value - 0.5f) < 0.001f)
            {
                MouseLookActions.Value = "Look";
                MouseSensitivity.Value = 0.08f;
            }
            MouseInvertY = f.Bind("Mouse", "InvertY", false, "Invert vertical mouse look.");

            KbmPrompts = f.Bind("Prompts", "KeyboardPrompts", true,
                "Show key names instead of controller glyphs in button prompts while the keyboard/mouse is the last-used device.");
            TextPromptFormat = f.Bind("Prompts", "TextPromptFormat", "[{0}]",
                "Format for key names inside dialogue/tip text. {0} is the key label. TextMeshPro rich text allowed.");
            GlyphCharMap = f.Bind("Prompts", "GlyphCharMap", "",
                "Extra text glyph characters to rewrite, as hex code = control, ';'-separated (e.g. \"E001=buttonSouth\"). Unmapped glyphs are logged.");
            DumpGlyphs = f.Bind("Prompts", "DumpGlyphs", false,
                "Write the game's original controller glyph sprites as PNGs to BepInEx/teradea-kbm/glyphs/ as they appear on screen.");
            CustomKeyImageDir = f.Bind("Prompts", "CustomKeyImageDir", "",
                "Folder with the blank key shapes and their .json label layouts, named after the original glyph sprites. Empty = BepInEx/plugins/TeradeaKbm/keys.");

            WalkModifierKey = f.Bind("Keyboard", "WalkModifierKey", "leftCtrl",
                "Hold this key to walk instead of run (keyboard movement is always full-magnitude). Empty disables.");
            SuppressCancelOnMenuKey = f.Bind("Keyboard", "SuppressCancelOnMenuKey", true,
                "When a key is bound to both B (cancel) and Start (menu), ignore the B press in the field on the frame the menu key fires.");

            ButtonSouth     = f.Bind("Keys", "A (buttonSouth)",       "f;enter;<Mouse>/leftButton", "Confirm / interact / use");
            ButtonEast      = f.Bind("Keys", "B (buttonEast)",        "escape;<Mouse>/rightButton", "Cancel / back / dismount");
            ButtonWest      = f.Bind("Keys", "X (buttonWest)",        "tab;i", "Item bag / X prompts");
            ButtonNorth     = f.Bind("Keys", "Y (buttonNorth)",       "r", "Y prompts / skip event");
            LeftShoulder    = f.Bind("Keys", "LB (leftShoulder)",     "q", "Previous tab / LB prompts");
            RightShoulder   = f.Bind("Keys", "RB (rightShoulder)",    "e", "Next tab / RB prompts");
            LeftTrigger     = f.Bind("Keys", "LT (leftTrigger)",      "z;<Mouse>/scroll/up", "Camera zoom in / previous tab");
            RightTrigger    = f.Bind("Keys", "RT (rightTrigger)",     "leftShift;<Mouse>/scroll/down", "Dash / camera zoom out / next tab");
            LeftStickPress  = f.Bind("Keys", "L3 (leftStickPress)",   "c;<Mouse>/middleButton", "Reset camera");
            RightStickPress = f.Bind("Keys", "R3 (rightStickPress)",  "v", "Top-down camera");
            Start           = f.Bind("Keys", "Start",                 "escape", "Open menu / options");
            Select          = f.Bind("Keys", "Select",                "m", "Doc pad / album");
        }
    }

    internal static class CursorControl
    {
        private static bool _wasLocked;

        internal static bool InGameplayMap()
        {
            try
            {
                if (!SingletonMonoBehaviour<InputSystemManager>.ExistsInstance) return false;
                var id = SingletonMonoBehaviour<InputSystemManager>.Instance.GetCurrentActionSetId();
                return id == ActionSetId.Player || id == ActionSetId.Photo || id == ActionSetId.Event
                    || id == ActionSetId.CameraOnly || id == ActionSetId.Camp;
            }
            catch
            {
                return false;
            }
        }

        public static void Tick()
        {
            var cfg = Plugin.Cfg;
            if (cfg == null || !cfg.LockCursor.Value || !Patches.Ready) return;

            bool wantLock = Application.isFocused && DeviceTracker.UsingKbm && InGameplayMap();
            string unlockKey = cfg.CursorUnlockKey.Value;
            if (wantLock && !string.IsNullOrEmpty(unlockKey))
            {
                var kb = Keyboard.current;
                if (kb != null)
                {
                    var ctrl = kb.TryGetChildControl(unlockKey);
                    var btn = ctrl != null ? ctrl.TryCast<ButtonControl>() : null;
                    if (btn != null && btn.isPressed) wantLock = false;
                }
            }

            if (wantLock)
            {
                if (Cursor.lockState != CursorLockMode.Locked) Cursor.lockState = CursorLockMode.Locked;
                if (Cursor.visible) Cursor.visible = false;
                _wasLocked = true;
            }
            else if (_wasLocked)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = false;
                _wasLocked = false;
            }
        }
    }

    /// <summary>Tracks whether the player last used keyboard/mouse or a gamepad.</summary>
    internal static class DeviceTracker
    {
        public static bool UsingKbm { get; private set; } = true;
        public static event Action<bool> Changed;

        private static bool _installed;
        private static Il2CppSystem.Action<Il2CppSystem.Object, InputActionChange> _handler;

        public static void Install()
        {
            if (_installed) return;
            _installed = true;
            _handler = DelegateSupport.ConvertDelegate<Il2CppSystem.Action<Il2CppSystem.Object, InputActionChange>>(
                new Action<Il2CppSystem.Object, InputActionChange>(OnActionChange));
            InputSystem.add_onActionChange(_handler);
        }

        public static void Uninstall()
        {
            if (!_installed) return;
            _installed = false;
            try { InputSystem.remove_onActionChange(_handler); } catch { }
            _handler = null;
        }

        public static void Init(bool kbm)
        {
            if (kbm == UsingKbm) return;
            UsingKbm = kbm;
            Changed?.Invoke(kbm);
        }

        private static void OnActionChange(Il2CppSystem.Object obj, InputActionChange change)
        {
            try
            {
                if (change != InputActionChange.ActionPerformed && change != InputActionChange.ActionStarted) return;
                var action = obj?.TryCast<InputAction>();
                var control = action?.activeControl;
                var device = control?.device;
                if (device == null) return;
                bool kbm = device.TryCast<Keyboard>() != null || device.TryCast<Mouse>() != null;
                bool pad = device.TryCast<Gamepad>() != null;
                if (!kbm && !pad) return;
                if (kbm == UsingKbm) return;

                // Only deliberate input flips the mode: a pressed key/button, or a stick pushed past half.
                var b = control.TryCast<ButtonControl>();
                if (b != null)
                {
                    if (!b.isPressed) return;
                }
                else
                {
                    var v2 = control.TryCast<Vector2Control>();
                    if (v2 != null)
                    {
                        if (control.TryCast<DeltaControl>() != null) return;
                        if (v2.ReadValue().magnitude < 0.5f) return;
                    }
                    else
                    {
                        var a = control.TryCast<AxisControl>();
                        if (a == null) return;
                        if (Mathf.Abs(a.ReadValue()) < 0.5f) return;
                    }
                }
                UsingKbm = kbm;
                Plugin.Log.LogInfo("Input device switched to " + (kbm ? "keyboard/mouse" : "gamepad"));
                Changed?.Invoke(kbm);
            }
            catch (Exception e)
            {
                Plugin.Log.LogDebug("Device tracking error: " + e.Message);
            }
        }
    }
}
