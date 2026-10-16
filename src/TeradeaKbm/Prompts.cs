using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Msg;
using Table;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using App.UI;

namespace TeradeaKbm
{
    /// <summary>Human-readable labels for the keys bound to each canonical gamepad control.</summary>
    internal static class KeyLabels
    {
        private static readonly Dictionary<string, string> KeyNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "escape", "Esc" }, { "enter", "Enter" }, { "space", "Space" }, { "tab", "Tab" }, { "backspace", "Bksp" },
            { "leftShift", "Shift" }, { "rightShift", "RShift" }, { "leftCtrl", "Ctrl" }, { "rightCtrl", "RCtrl" },
            { "leftAlt", "Alt" }, { "rightAlt", "RAlt" }, { "home", "Home" }, { "end", "End" }, { "insert", "Ins" }, { "delete", "Del" },
            { "pageUp", "PgUp" }, { "pageDown", "PgDn" }, { "capsLock", "Caps" },
            { "upArrow", "↑" }, { "downArrow", "↓" }, { "leftArrow", "←" }, { "rightArrow", "→" },
            { "leftButton", "LMB" }, { "rightButton", "RMB" }, { "middleButton", "MMB" },
            { "scroll/up", "Wheel↑" }, { "scroll/down", "Wheel↓" }, { "delta", "Mouse" },
        };

        public static string ForPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return "?";
            string name = path;
            int slash = path.IndexOf("/", StringComparison.Ordinal);
            if (path.StartsWith("<") && slash >= 0) name = path.Substring(slash + 1);
            if (KeyNames.TryGetValue(name, out var label)) return label;
            if (name.Length == 1) return name.ToUpperInvariant();
            if (name.StartsWith("digit") && name.Length == 6) return name.Substring(5);
            if (name.StartsWith("numpad")) return "Num" + name.Substring(6);
            if (name.StartsWith("f") && name.Length <= 3 && int.TryParse(name.Substring(1), out _)) return name.ToUpperInvariant();
            return char.ToUpperInvariant(name[0]) + name.Substring(1);
        }

        public static string ForControl(string control)
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
                case "dpad": return Stick(cfg.DpadUp.Value, cfg.DpadDown.Value, cfg.DpadLeft.Value, cfg.DpadRight.Value);
                case "dpad/ud": return ForPath(First(cfg.DpadUp.Value)) + ForPath(First(cfg.DpadDown.Value));
                case "dpad/lr": return ForPath(First(cfg.DpadLeft.Value)) + ForPath(First(cfg.DpadRight.Value));
                case "leftStick": { var p = KbmInjector.Split(cfg.LeftStick.Value).ToArray(); return p.Length == 4 ? Stick(p[0], p[1], p[2], p[3]) : "Move"; }
                case "leftStick/ud": { var p = KbmInjector.Split(cfg.LeftStick.Value).ToArray(); return p.Length == 4 ? ForPath(p[0]) + "/" + ForPath(p[1]) : "Move"; }
                case "leftStick/lr": { var p = KbmInjector.Split(cfg.LeftStick.Value).ToArray(); return p.Length == 4 ? ForPath(p[2]) + "/" + ForPath(p[3]) : "Move"; }
                case "rightStick": { var p = KbmInjector.Split(cfg.RightStick.Value).ToArray(); return p.Length == 4 ? Stick(p[0], p[1], p[2], p[3]) : "Mouse"; }
                case "rightStick/ud": { var p = KbmInjector.Split(cfg.RightStick.Value).ToArray(); return p.Length == 4 ? ForPath(p[0]) + ForPath(p[1]) : "Mouse"; }
                case "rightStick/lr": { var p = KbmInjector.Split(cfg.RightStick.Value).ToArray(); return p.Length == 4 ? ForPath(p[2]) + ForPath(p[3]) : "Mouse"; }
                case "leftStick/up": return StickPart(cfg.LeftStick.Value, 0);
                case "leftStick/down": return StickPart(cfg.LeftStick.Value, 1);
                case "leftStick/left": return StickPart(cfg.LeftStick.Value, 2);
                case "leftStick/right": return StickPart(cfg.LeftStick.Value, 3);
                case "rightStick/up": return StickPart(cfg.RightStick.Value, 0);
                case "rightStick/down": return StickPart(cfg.RightStick.Value, 1);
                case "rightStick/left": return StickPart(cfg.RightStick.Value, 2);
                case "rightStick/right": return StickPart(cfg.RightStick.Value, 3);
                default: return "?";
            }
            return ForPath(First(raw));
        }

        private static string StickPart(string raw, int idx)
        {
            var p = KbmInjector.Split(raw).ToArray();
            return p.Length == 4 ? ForPath(p[idx]) : "?";
        }

        private static string Stick(string u, string d, string l, string r)
        {
            string lu = ForPath(First(u)), ld = ForPath(First(d)), ll = ForPath(First(l)), lr = ForPath(First(r));
            if (lu == "↑") return "↑↓←→";
            string s = lu + ll + ld + lr;
            return s.Length <= 4 ? s : lu + "/" + ld + "/" + ll + "/" + lr;
        }

        private static string First(string raw) => KbmInjector.Split(raw).FirstOrDefault() ?? "";

        /// <summary>Text-prompt table (MSGKEYACTIONPARAM.Defs, Switch labels) → canonical Xbox control.</summary>
        public static string ControlForMsgKeyAction(int idx)
        {
            switch ((MSGKEYACTIONPARAM.Defs)idx)
            {
                case MSGKEYACTIONPARAM.Defs.ZLbtn: return "leftTrigger";
                case MSGKEYACTIONPARAM.Defs.Lbtn: return "leftShoulder";
                case MSGKEYACTIONPARAM.Defs.L3btn: return "leftStickPress";
                case MSGKEYACTIONPARAM.Defs.Lstick: return "leftStick";
                case MSGKEYACTIONPARAM.Defs.Cross: return "dpad";
                case MSGKEYACTIONPARAM.Defs.Down: return "dpad/down";
                case MSGKEYACTIONPARAM.Defs.Minus: return "select";
                case MSGKEYACTIONPARAM.Defs.Abtn: return "buttonSouth";
                case MSGKEYACTIONPARAM.Defs.Bbtn: return "buttonEast";
                case MSGKEYACTIONPARAM.Defs.Xbtn: return "buttonWest";
                case MSGKEYACTIONPARAM.Defs.Ybtn: return "buttonNorth";
                case MSGKEYACTIONPARAM.Defs.Plus: return "start";
                case MSGKEYACTIONPARAM.Defs.Rstick: return "rightStick";
                case MSGKEYACTIONPARAM.Defs.R3btn: return "rightStickPress";
                case MSGKEYACTIONPARAM.Defs.Rbtn: return "rightShoulder";
                case MSGKEYACTIONPARAM.Defs.ZRbtn: return "rightTrigger";
                case MSGKEYACTIONPARAM.Defs.Left: return "dpad/left";
                case MSGKEYACTIONPARAM.Defs.Right: return "dpad/right";
                case MSGKEYACTIONPARAM.Defs.Up: return "dpad/up";
                default: return null;
            }
        }

        /// <summary>Glyph sprite name (e.g. "ui_common_icon_guide_btn_A_001_00") → canonical Xbox control, or null.</summary>
        public static string ControlForSpriteName(string spriteName)
        {
            if (string.IsNullOrEmpty(spriteName)) return null;
            // Riding menu prompts have their own numbered set: 001 R-stick (Switch), 002 ZR, 003 R2, 004 RT, 005 RS.
            int rm = spriteName.IndexOf("ridingmenu_icon_guide_00", StringComparison.OrdinalIgnoreCase);
            if (rm >= 0 && rm + 24 < spriteName.Length)
            {
                switch (spriteName[rm + 24])
                {
                    case '1': case '5': return "rightStick";
                    case '2': case '3': case '4': return "rightTrigger";
                    default: return null;
                }
            }
            int at = spriteName.IndexOf("icon_guide_", StringComparison.OrdinalIgnoreCase);
            if (at < 0) return null;
            string s = spriteName.Substring(at + "icon_guide_".Length);
            bool stick = false;
            foreach (var prefix in new[] { "toptab_btn_", "actioncom_btn_", "btn_", "stick_" })
            {
                if (s.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    stick = prefix == "stick_";
                    s = s.Substring(prefix.Length);
                    break;
                }
            }
            bool ps = spriteName.EndsWith("_ps", StringComparison.OrdinalIgnoreCase) || spriteName.EndsWith("_ps5", StringComparison.OrdinalIgnoreCase);
            int tail = s.IndexOf("_0", StringComparison.Ordinal);
            if (tail > 0) s = s.Substring(0, tail);
            s = s.ToLowerInvariant();
            if (!stick && s.StartsWith("stick_")) { s = s.Substring(6) + "3"; stick = true; }
            if (ps && s == "x") return "buttonEast";

            if (stick)
            {
                switch (s)
                {
                    case "l": case "ls": return "leftStick";
                    case "r": case "rs": return "rightStick";
                    case "l3": return "leftStickPress";
                    case "lupdown": case "lsupdown": return "leftStick/ud";
                    case "lleftright": case "lsleftright": return "leftStick/lr";
                    case "r3": return "rightStickPress";
                    case "rupdown": case "rsupdown": return "rightStick/ud";
                    case "rleftright": case "rsleftright": return "rightStick/lr";
                    default: return null;
                }
            }
            switch (s)
            {
                case "a": case "circle": return "buttonSouth";
                case "b": case "x_ps": return "buttonEast";
                case "x": return "buttonWest";
                case "square": return "buttonWest";
                case "y": case "triangle": return "buttonNorth";
                case "l": case "lb": case "l1": return "leftShoulder";
                case "r": case "rb": case "r1": return "rightShoulder";
                case "zl": case "lt": case "l2": return "leftTrigger";
                case "zr": case "rt": case "r2": return "rightTrigger";
                case "minus": case "back": case "pad": case "view": case "share": case "touchpad": return "select";
                case "plus": case "start": case "option": case "menu": return "start";
                case "dir_updown": return "dpad/ud";
                case "dir_leftright": return "dpad/lr";
                case "dir_up": return "dpad/up";
                case "dir_down": return "dpad/down";
                case "dir_left": return "dpad/left";
                case "dir_right": return "dpad/right";
                default: return null;
            }
        }
    }

    internal class GlyphLayout
    {
        public int[] canvas;
        public GlyphLabelBox[] labels;
    }

    internal class GlyphLabelBox
    {
        public string control;
        public float x, y, w, h;
    }

    /// <summary>Loads the blank key shapes (one per original glyph) and their label layouts; dumps originals on request.</summary>
    internal static class GlyphAssets
    {
        private static readonly Dictionary<string, GlyphLayout> _layouts = new Dictionary<string, GlyphLayout>(StringComparer.OrdinalIgnoreCase);
        private static readonly System.Text.RegularExpressions.Regex CanvasRx =
            new System.Text.RegularExpressions.Regex(@"""canvas""\s*:\s*\[\s*(\d+)\s*,\s*(\d+)\s*\]");
        private static readonly System.Text.RegularExpressions.Regex BoxRx =
            new System.Text.RegularExpressions.Regex(@"\{\s*""control""\s*:\s*""([^""]+)""\s*,\s*""x""\s*:\s*([-\d.]+)\s*,\s*""y""\s*:\s*([-\d.]+)\s*,\s*""w""\s*:\s*([-\d.]+)\s*,\s*""h""\s*:\s*([-\d.]+)\s*\}");

        private static GlyphLayout ParseLayout(string json)
        {
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            var cm = CanvasRx.Match(json);
            if (!cm.Success) return null;
            var layout = new GlyphLayout { canvas = new[] { int.Parse(cm.Groups[1].Value, inv), int.Parse(cm.Groups[2].Value, inv) } };
            var boxes = new List<GlyphLabelBox>();
            foreach (System.Text.RegularExpressions.Match m in BoxRx.Matches(json))
            {
                boxes.Add(new GlyphLabelBox
                {
                    control = m.Groups[1].Value,
                    x = float.Parse(m.Groups[2].Value, inv),
                    y = float.Parse(m.Groups[3].Value, inv),
                    w = float.Parse(m.Groups[4].Value, inv),
                    h = float.Parse(m.Groups[5].Value, inv),
                });
            }
            layout.labels = boxes.ToArray();
            return layout;
        }

        public static GlyphLayout LayoutFor(string originalName)
        {
            string name = CleanSpriteName(originalName);
            if (string.IsNullOrEmpty(name)) return null;
            if (_layouts.TryGetValue(name, out var cached)) return cached;
            GlyphLayout layout = null;
            try
            {
                string path = System.IO.Path.Combine(CustomDir, name + ".json");
                if (System.IO.File.Exists(path)) layout = ParseLayout(System.IO.File.ReadAllText(path));
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Bad glyph sidecar for " + name + ": " + e.Message);
            }
            _layouts[name] = layout;
            return layout;
        }

        private static readonly HashSet<string> _dumped = new HashSet<string>();
        private static readonly Dictionary<string, Sprite> _custom = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> _customMissing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> _loggedMiss = new HashSet<string>();
        private static readonly Dictionary<int, Texture> TextureBySprite = new Dictionary<int, Texture>();
        private static int _failLogs;

        public static string DumpDir => System.IO.Path.Combine(BepInEx.Paths.BepInExRootPath, "teradea-kbm", "glyphs");
        public static string CustomDir
        {
            get
            {
                var d = Plugin.Cfg.CustomKeyImageDir.Value;
                if (string.IsNullOrEmpty(d)) d = System.IO.Path.Combine(BepInEx.Paths.PluginPath, "TeradeaKbm", "keys");
                return d;
            }
        }

        public static string CleanSpriteName(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;
            int i = name.IndexOf('(');
            if (i > 0) name = name.Substring(0, i);
            return name.Trim();
        }

        public static void RememberTexture(Sprite s, Texture t)
        {
            if (s != null && t != null) TextureBySprite[s.GetInstanceID()] = t;
        }

        /// <summary>Read the glyph's pixels (works for non-readable/compressed atlas textures).</summary>
        public static Texture2D ReadPixels(Sprite s)
        {
            Texture tex = s.texture;
            if (tex == null) TextureBySprite.TryGetValue(s.GetInstanceID(), out tex);
            if (tex == null) throw new InvalidOperationException("sprite has no texture (atlas not bound yet)");
            var tr = s.textureRect;
            int w = Mathf.RoundToInt(tr.width), h = Mathf.RoundToInt(tr.height);
            var rt = RenderTexture.GetTemporary(tex.width, tex.height, 0, RenderTextureFormat.ARGB32);
            var prev = RenderTexture.active;
            Graphics.Blit(tex, rt);
            RenderTexture.active = rt;
            var copy = new Texture2D(w, h, TextureFormat.RGBA32, false);
            copy.ReadPixels(new Rect(tr.x, tr.y, w, h), 0, 0);
            copy.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            return copy;
        }

        public static void DumpIfWanted(Sprite s)
        {
            if (!Plugin.Cfg.DumpGlyphs.Value || s == null) return;
            string name = CleanSpriteName(s.name);
            if (!_dumped.Add(name)) return;
            try
            {
                System.IO.Directory.CreateDirectory(DumpDir);
                var tex = ReadPixels(s);
                var png = ImageConversion.EncodeToPNG(tex);
                UnityEngine.Object.Destroy(tex);
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(DumpDir, name + ".png"), png.ToArray());
            }
            catch (Exception e)
            {
                _dumped.Remove(name);
                if (_failLogs++ < 3) Plugin.Log.LogWarning("Glyph dump failed for " + name + ": " + e.Message);
            }
        }

        private static bool _dumpedAll;

        /// <summary>
        /// Load every guide sprite the game's asset table knows about through the game's own sprite loader and
        /// dump it, so the full set is available for generating shapes without hunting each screen.
        /// </summary>
        public static void DumpAllOnce()
        {
            if (_dumpedAll || !Plugin.Cfg.DumpGlyphs.Value) return;
            _dumpedAll = true;
            int n = 0, failed = 0;
            try
            {
                var loader = new App.Util.UISpriteLoader();
                foreach (var name in Enum.GetNames(typeof(ASSETDATA.Defs)))
                {
                    if (name.IndexOf("guide", StringComparison.OrdinalIgnoreCase) < 0) continue;
                    try
                    {
                        var def = (ASSETDATA.Defs)Enum.Parse(typeof(ASSETDATA.Defs), name);
                        var sp = loader.LoadSprite(def);
                        if (sp == null) { failed++; continue; }
                        DumpIfWanted(sp);
                        n++;
                    }
                    catch (Exception e)
                    {
                        failed++;
                        if (failed <= 3) Plugin.Log.LogWarning("Glyph load failed for " + name + ": " + e.Message);
                    }
                }
                Plugin.Log.LogInfo("Dumped " + n + " guide sprites (" + failed + " failed) to " + DumpDir);
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Glyph dump-all failed: " + e);
            }
        }

        /// <summary>The blank shape for an original glyph, "&lt;original name&gt;.png", on the same canvas. Null if none.</summary>
        public static Sprite ShapeFor(Sprite like)
        {
            if (like == null) return null;
            string name = CleanSpriteName(like.name);
            if (_custom.TryGetValue(name, out var sp) && sp != null) return sp;
            if (_customMissing.Contains(name)) return null;
            string path = System.IO.Path.Combine(CustomDir, name + ".png");
            if (!System.IO.File.Exists(path))
            {
                _customMissing.Add(name);
                if (_loggedMiss.Add(name)) Plugin.Log.LogInfo("No key image for glyph '" + name + "' (looked for " + path + ").");
                return null;
            }
            try
            {
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                if (!ImageConversion.LoadImage(tex, System.IO.File.ReadAllBytes(path))) { _customMissing.Add(name); return null; }
                float ppu = like.pixelsPerUnit * (tex.width / like.rect.width);
                sp = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), ppu);
                sp.name = "teradea_kbm_" + name;
                _custom[name] = sp;
                return sp;
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Failed to load key image " + path + ": " + e.Message);
                _customMissing.Add(name);
                return null;
            }
        }

        /// <summary>Anything the mod produced: never treat as a game glyph.</summary>
        public static bool IsOurs(Sprite s) => s != null && s.name.StartsWith("teradea_kbm_", StringComparison.Ordinal);
    }

    /// <summary>Manages one UI Image that shows a controller glyph; swaps between glyph and key shape by last-used device.</summary>
    internal class PromptOverlay
    {
        private static readonly Dictionary<int, PromptOverlay> All = new Dictionary<int, PromptOverlay>();
        private static bool _hooked;
        internal static bool Applying;

        private readonly Image _image;
        private Sprite _glyph;
        private string _glyphName;
        private string _control;
        private readonly List<TextMeshProUGUI> _labels = new List<TextMeshProUGUI>();
        private Vector2 _fitSize;       // image rect size the labels were last fitted to
        private readonly List<float> _fitFont = new List<float>();
        private bool _labelsShown;
        private bool _fitMeasured = true;   // false while a label is sized by estimate and awaits a real measurement
        private static readonly HashSet<string> _diagLogged = new HashSet<string>();

        /// <summary>
        /// Label sizes are derived from the image rect, which layout groups change after the sprite is assigned
        /// (first frame of a screen, resolution changes). Re-fit whenever the rect moves.
        /// </summary>
        public static void TickAll()
        {
            if (All.Count == 0 || !DeviceTracker.UsingKbm) return;
            foreach (var kv in All.ToList())
            {
                var o = kv.Value;
                if (!o._labelsShown || o._control == null) continue;
                try
                {
                    var size = o._image.rectTransform.rect.size;
                    bool refit = Mathf.Abs(size.x - o._fitSize.x) > 0.5f || Mathf.Abs(size.y - o._fitSize.y) > 0.5f;
                    if (!refit && !o._fitMeasured && o._image.isActiveAndEnabled) refit = true; // estimate in place: re-measure now that it's live
                    if (!refit)
                    {
                        // The game's text styling restyles child TMP objects when a prompt text refreshes (our
                        // labels under its TextMeshProEx icons came back at 4x). Put ours back when that happens.
                        for (int i = 0; i < o._labels.Count && i < o._fitFont.Count; i++)
                        {
                            var l = o._labels[i];
                            if (l == null || !l.gameObject.activeSelf) continue;
                            if (Mathf.Abs(l.fontSize - o._fitFont[i]) > 0.01f)
                            {
                                if (_diagLogged.Add("restyle:" + o._glyphName))
                                    Plugin.Log.LogInfo("Prompt " + o._glyphName + ": label font size changed by the game " + o._fitFont[i] + " -> " + l.fontSize + "; restoring.");
                                refit = true; break;
                            }
                            var ls = l.rectTransform.localScale;
                            if (Mathf.Abs(ls.x - 1f) > 0.01f || Mathf.Abs(ls.y - 1f) > 0.01f)
                            {
                                if (_diagLogged.Add("rescale:" + o._glyphName))
                                    Plugin.Log.LogInfo("Prompt " + o._glyphName + ": label scale changed by the game to " + ls + "; restoring.");
                                l.rectTransform.localScale = Vector3.one;
                            }
                        }
                    }
                    if (refit) o.Apply(true);
                }
                catch
                {
                    All.Remove(kv.Key); // image destroyed
                }
            }
        }

        private PromptOverlay(Image image) { _image = image; }

        /// <summary>Called whenever an Image gets a sprite or is enabled.</summary>
        public static void Consider(Image image)
        {
            if (image == null || Applying) return;
            var sprite = image.sprite;
            if (sprite == null || GlyphAssets.IsOurs(sprite)) return;
            string control = KeyLabels.ControlForSpriteName(sprite.name);
            int id = image.GetInstanceID();
            All.TryGetValue(id, out var ov);
            if (sprite.name.IndexOf("icon_guide_", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                // dump every guide sprite, mapped or not, so new ones can be given shapes
                GlyphAssets.RememberTexture(sprite, image.mainTexture);
                GlyphAssets.DumpIfWanted(sprite);
                GlyphAssets.DumpAllOnce(); // the UI is up by now; pull the whole set from the asset table
            }
            if (control == null)
            {
                if (ov != null) { ov.Detach(); All.Remove(id); }
                if (sprite.name.IndexOf("icon_guide", StringComparison.OrdinalIgnoreCase) >= 0 && _diagLogged.Add("unknown:" + sprite.name))
                    Plugin.Log.LogInfo("Unrecognised glyph sprite '" + sprite.name + "' (no control mapping); left as is.");
                return;
            }
            if (!_hooked)
            {
                DeviceTracker.Changed += _ => RefreshAll();
                _hooked = true;
            }
            if (ov == null)
            {
                ov = new PromptOverlay(image);
                All[id] = ov;
            }
            ov._glyph = sprite;
            ov._glyphName = GlyphAssets.CleanSpriteName(sprite.name);
            ov._control = control;
            ov.Apply(DeviceTracker.UsingKbm);
        }

        public static void RestoreAll()
        {
            foreach (var o in All.Values.ToList())
            {
                try { o.Apply(false); } catch { }
            }
        }

        private static void RefreshAll()
        {
            foreach (var kv in All.ToList())
            {
                try { kv.Value.Apply(DeviceTracker.UsingKbm); }
                catch (Exception e)
                {
                    All.Remove(kv.Key);
                    Plugin.Log.LogDebug("Prompt refresh dropped an image: " + e.Message);
                }
            }
        }

        private void Detach()
        {
            HideLabels(0);
            _control = null;
            _glyph = null;
        }

        private void HideLabels(int fromIndex)
        {
            if (fromIndex == 0) _labelsShown = false;
            for (int i = fromIndex; i < _labels.Count; i++)
            {
                if (_labels[i] != null) _labels[i].gameObject.SetActive(false);
            }
        }

        private void Apply(bool kbm)
        {
            if (_image == null || _control == null) return;
            Applying = true;
            try
            {
                if (kbm && Plugin.Cfg.KbmPrompts.Value)
                {
                    var shape = GlyphAssets.ShapeFor(_glyph);
                    if (shape == null)
                    {
                        // No shape for this glyph: leave the controller glyph in place rather than show nothing.
                        if (_glyph != null) _image.sprite = _glyph;
                        HideLabels(0);
                        return;
                    }
                    _image.sprite = shape;
                    var layout = GlyphAssets.LayoutFor(_glyphName);
                    if (layout != null && layout.labels != null && layout.canvas != null && layout.canvas.Length == 2)
                    {
                        float W = layout.canvas[0], H = layout.canvas[1];
                        var rect = _image.rectTransform.rect;
                        _fitSize = rect.size;
                        _labelsShown = true;
                        _fitMeasured = true;
                        for (int i = 0; i < layout.labels.Length; i++)
                        {
                            var box = layout.labels[i];
                            var label = EnsureLabel(i);
                            var rt = label.rectTransform;
                            rt.anchorMin = new Vector2(box.x / W, 1f - (box.y + box.h) / H);
                            rt.anchorMax = new Vector2((box.x + box.w) / W, 1f - box.y / H);
                            rt.offsetMin = Vector2.zero;
                            rt.offsetMax = Vector2.zero;
                            bool measured = FitLabel(label, KeyLabels.ForControl(box.control), box.w * rect.width / W, box.h * rect.height / H);
                            if (!measured) _fitMeasured = false;
                            while (_fitFont.Count <= i) _fitFont.Add(0f);
                            _fitFont[i] = label.fontSize;
                            label.gameObject.SetActive(true);
                            if (Plugin.Cfg.LogPrompts.Value && measured && _diagLogged.Add("label:" + _glyphName + ":" + i))
                            {
                                Plugin.Log.LogInfo("Prompt " + _glyphName + " label " + i + " '" + label.text + "': box=" + (box.w * rect.width / W) + "x" + (box.h * rect.height / H) + " fontSize=" + label.fontSize);
                            }
                        }
                        HideLabels(layout.labels.Length);
                    }
                    else
                    {
                        HideLabels(0);
                    }
                }
                else
                {
                    if (_glyph != null) _image.sprite = _glyph;
                    HideLabels(0);
                }
            }
            finally
            {
                Applying = false;
            }
        }

        /// <summary>Size a label to its box explicitly: measure at a reference size and scale (no autosize, no wrapping).</summary>
        private static bool FitLabel(TextMeshProUGUI label, string text, float boxW, float boxH)
        {
            label.enableAutoSizing = false;
            label.text = text;
            if (boxW <= 0f || boxH <= 0f) { label.fontSize = 1f; return true; }
            const float refSize = 100f;
            label.fontSize = refSize;
            var pv = label.GetPreferredValues(text);
            // A label created while its prompt is still hidden measures nonsense (TMP isn't initialised yet),
            // which produced 10x font sizes. Only trust a measurement in the plausible range for this font;
            // otherwise estimate from geometry and let the caller re-measure once the label is live.
            int n = Mathf.Max(1, text.Length);
            bool plausible = pv.y > 60f && pv.y < 200f && pv.x > 15f * n && pv.x < 130f * n;
            if (!plausible)
            {
                label.fontSize = Mathf.Max(1f, Mathf.Min(boxH * 1.05f, boxW * 0.95f / (0.62f * n)));
                return false;
            }
            float byHeight = refSize * boxH / pv.y;
            float byWidth = refSize * (boxW * 0.95f) / pv.x;
            label.fontSize = Mathf.Max(1f, Mathf.Min(byHeight, byWidth));
            return true;
        }

        private TextMeshProUGUI EnsureLabel(int index)
        {
            while (_labels.Count <= index) _labels.Add(null);
            if (_labels[index] != null) return _labels[index];
            var go = new GameObject("TeradeaKbmKeyLabel" + index);
            go.transform.SetParent(_image.transform, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            var rt = t.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            TMP_FontAsset font = null;
            try { font = MessageManager.Instance != null ? MessageManager.Instance.GetFontAsset() : null; } catch { }
            if (font == null)
            {
                var sibling = _image.GetComponentInParent<TMP_Text>();
                if (sibling != null) font = sibling.font;
            }
            if (font != null) t.font = font;
            if (index == 0 && _diagLogged.Add("font:" + (font != null ? font.name : "null")))
            {
                Plugin.Log.LogInfo("Label font: " + (font != null ? font.name : "null (TMP default)"));
            }
            t.alignment = TextAlignmentOptions.Center;
            t.enableAutoSizing = false;
            t.fontStyle = FontStyles.Normal;
            t.color = new Color32(239, 231, 181, 255);
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;
            t.characterSpacing = -2f;
            t.margin = Vector4.zero;
            _labels[index] = t;
            return t;
        }
    }

    /// <summary>Rewrites controller glyph characters embedded in text (circled letters, private-use icons) into key labels.</summary>
    internal static class GlyphText
    {
        private static readonly Dictionary<char, string> Map = new Dictionary<char, string>
        {
            { 'Ⓐ', "buttonSouth" }, { 'Ⓑ', "buttonEast" }, { 'Ⓧ', "buttonWest" }, { 'Ⓨ', "buttonNorth" },
            { 'Ⓛ', "leftShoulder" }, { 'Ⓡ', "rightShoulder" },
            { 'ⓐ', "buttonSouth" }, { 'ⓑ', "buttonEast" }, { 'ⓧ', "buttonWest" }, { 'ⓨ', "buttonNorth" },
            { 'ⓛ', "leftShoulder" }, { 'ⓡ', "rightShoulder" },
            { '⊕', "start" }, { '⊖', "select" },
        };
        private static readonly HashSet<char> _loggedUnknown = new HashSet<char>();
        private static Dictionary<char, string> _userMap;
        private static readonly Dictionary<char, string> _padMap = new Dictionary<char, string>();
        private static readonly HashSet<char> _padMapTried = new HashSet<char>();

        /// <summary>
        /// The game writes button prompts into text as private-use characters from its font, chosen per
        /// controller type from the MSGKEYACTIONPARAM table. MessageManager.keyAction holds the current set,
        /// indexed like MSGKEYACTIONPARAM.Defs, so the character → control map can be read back from it.
        /// </summary>
        private static void BuildPadMap()
        {
            try
            {
                var mm = MessageManager.Instance;
                if (mm == null) return;
                var arr = mm.keyAction;
                if (arr == null) return;
                for (int idx = 0; idx < arr.Length; idx++)
                {
                    string control = KeyLabels.ControlForMsgKeyAction(idx);
                    string s = arr[idx];
                    if (control == null || string.IsNullOrEmpty(s)) continue;
                    foreach (char c in s)
                    {
                        if (c >= '' && c <= '') _padMap[c] = control;
                    }
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogDebug("Pad glyph map build failed: " + e.Message);
            }
        }

        private static Dictionary<char, string> UserMap()
        {
            if (_userMap != null) return _userMap;
            _userMap = new Dictionary<char, string>();
            foreach (var item in KbmInjector.Split(Plugin.Cfg.GlyphCharMap.Value))
            {
                var kv = item.Split('=');
                if (kv.Length == 2 && int.TryParse(kv[0].Trim(), System.Globalization.NumberStyles.HexNumber, null, out int code))
                {
                    _userMap[(char)code] = kv[1].Trim();
                }
            }
            return _userMap;
        }

        public static bool NeedsRewrite(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            foreach (char c in s) if (IsGlyphCandidate(c)) return true;
            return false;
        }

        private static bool IsGlyphCandidate(char c)
        {
            return (c >= 'Ⓐ' && c <= 'ⓩ') || c == '⊕' || c == '⊖' || (c >= '' && c <= '');
        }

        public static string Rewrite(string s)
        {
            var sb = new System.Text.StringBuilder(s.Length + 16);
            var user = UserMap();
            foreach (char c in s)
            {
                string control = null;
                if (user.TryGetValue(c, out var uc)) control = uc;
                else if (Map.TryGetValue(c, out var mc)) control = mc;
                else if (c >= '' && c <= '')
                {
                    if (!_padMap.TryGetValue(c, out control) && _padMapTried.Add(c))
                    {
                        BuildPadMap();
                        _padMap.TryGetValue(c, out control);
                    }
                }

                if (control != null)
                {
                    sb.Append(string.Format(Plugin.Cfg.TextPromptFormat.Value, KeyLabels.ForControl(control)));
                }
                else
                {
                    if (IsGlyphCandidate(c) && _loggedUnknown.Add(c))
                    {
                        Plugin.Log.LogInfo(string.Format("Unmapped glyph character U+{0:X4} in text: \"{1}\"", (int)c, Trim(s)));
                    }
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }

        private static string Trim(string s)
        {
            s = s.Replace("\n", " ");
            return s.Length > 60 ? s.Substring(0, 60) + "..." : s;
        }
    }

    internal static class PromptPatches
    {
        private static bool _inTextSetter;

        [HarmonyPatch(typeof(TMP_Text), nameof(TMP_Text.text), MethodType.Setter)]
        [HarmonyPrefix]
        private static void TMP_Text_set_text_Prefix(ref string value)
        {
            try
            {
                if (_inTextSetter || !DeviceTracker.UsingKbm || Plugin.Cfg == null || !Plugin.Cfg.KbmPrompts.Value) return;
                if (GlyphText.NeedsRewrite(value))
                {
                    _inTextSetter = true;
                    value = GlyphText.Rewrite(value);
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Text glyph rewrite failed: " + e.Message);
            }
            finally
            {
                _inTextSetter = false;
            }
        }

        [HarmonyPatch(typeof(MessageManager), nameof(MessageManager.GetKeyAction))]
        [HarmonyPostfix]
        private static void MessageManager_GetKeyAction_Postfix(int idx, ref string __result)
        {
            try
            {
                if (!Plugin.Cfg.KbmPrompts.Value || !DeviceTracker.UsingKbm) return;
                string control = KeyLabels.ControlForMsgKeyAction(idx);
                if (control == null) return;
                __result = string.Format(Plugin.Cfg.TextPromptFormat.Value, KeyLabels.ForControl(control));
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("GetKeyAction patch failed: " + e.Message);
            }
        }

        [HarmonyPatch(typeof(Image), nameof(Image.sprite), MethodType.Setter)]
        [HarmonyPostfix]
        private static void Image_set_sprite_Postfix(Image __instance)
        {
            try { PromptOverlay.Consider(__instance); }
            catch (Exception e) { Plugin.Log.LogWarning("Image.sprite hook failed: " + e.Message); }
        }

        [HarmonyPatch(typeof(Image), nameof(Image.OnEnable))]
        [HarmonyPostfix]
        private static void Image_OnEnable_Postfix(Image __instance)
        {
            try { PromptOverlay.Consider(__instance); }
            catch (Exception e) { Plugin.Log.LogWarning("Image.OnEnable hook failed: " + e.Message); }
        }

        [HarmonyPatch(typeof(OperateGuideButton), nameof(OperateGuideButton.Initialize))]
        [HarmonyPostfix]
        private static void OperateGuideButton_Initialize_Postfix(OperateGuideButton __instance)
        {
            try
            {
                var imgs = __instance._iconImage;
                if (imgs == null) return;
                for (int i = 0; i < imgs.Count; i++) PromptOverlay.Consider(imgs[i]);
            }
            catch (Exception e) { Plugin.Log.LogWarning("OperateGuideButton hook failed: " + e.Message); }
        }

        [HarmonyPatch(typeof(OperateGuideHoldButton), nameof(OperateGuideHoldButton.Initialize))]
        [HarmonyPostfix]
        private static void OperateGuideHoldButton_Initialize_Postfix(OperateGuideHoldButton __instance)
        {
            try
            {
                var imgs = __instance._iconImage;
                if (imgs == null) return;
                for (int i = 0; i < imgs.Count; i++) PromptOverlay.Consider(imgs[i]);
            }
            catch (Exception e) { Plugin.Log.LogWarning("OperateGuideHoldButton hook failed: " + e.Message); }
        }
    }
}
