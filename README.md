# Echoes of Teradea: Keyboard & Mouse

Keyboard and mouse support, with matching on-screen key prompts, for **Harvest Moon: Echoes of Teradea** (Steam).

Powered by BepInEx and spite.

## Why this exists

In the year of our lord and savior 2023, Natsume shipped Harvest Moon: The Winds of Anthos on PC with a yellow
banner on the store page that said *"requires a controller."* The keyboard, a device attached to every PC since before the first Harvest Moon
existed, didn't count. People [complained](https://steamcommunity.com/app/2140650/discussions/0/6792047411326997218).
A [mod](https://github.com/wisdomcostseyes/woa-kbm) fixed it.

In the year of our lord and savior 2026 they did it again. Same banner, same engine, same fifty dollars, and the Steam forum had a thread
titled "No keyboard and mouse support" two weeks *before* release. The response was to ship anyway.

Here is the part that should annoy you. The game's input asset contains a complete, bound, working
**Keyboard&Mouse** control scheme: WASD and the arrow keys for movement, Shift to dash, Ctrl to sneak, Z for the
camera, middle click to reset it, arrows for menu navigation, and a mouse-look action scaled to a sensible
sensitivity. Somebody built all of that. Then somebody wrote a list of devices the game is allowed to see, put six
kinds of controller on it, and left the keyboard off. Every one of those bindings has been sitting in the shipped
build, unreachable, since day one. This mod's core fix is clearing that list.

The port carries the same lack of care elsewhere: no resolution or UI-scale settings, a UI sized for a handheld
screen, and glyph assets lifted straight from the previous game. That last one did at least mean the key prompts
from the previous mod carried over unchanged.

For the record: this mod fixed the game's input before Natsume's first patch did anything at all. One person, one
evening, a decompiler and a text editor. When the patch notes do arrive, they will say "bug fixes".

The developers get the credit for the game. The keyboard gets its dignity back.

Download: [Nexus Mods](https://www.nexusmods.com/harvestmoonechoesofteradea) or the
[GitHub releases](https://github.com/wisdomcostseyes/teradea-kbm/releases).

## Install

The game is Unity 6 with IL2CPP, so it needs **BepInEx 6**, and the current BepInEx 6 builds crash this Unity
version on startup (an [open bug](https://github.com/BepInEx/Il2CppInterop/issues/283) in Il2CppInterop). The
release zip therefore includes a BepInEx 6 bleeding-edge build with a patched Il2CppInterop; see
[Modder notes](#modder-notes-for-modders) for exactly what was changed.

1. Extract the mod's zip into the game folder
   (`...\steamapps\common\Harvest Moon Echoes of Teradea\`, next to the game's exe). You should end up with
   `winhttp.dll`, a `BepInEx` folder and a `dotnet` folder beside the exe, and
   `BepInEx\plugins\TeradeaKbm\TeradeaKbm.dll`.
2. Launch the game. The first start takes a minute or two longer while BepInEx generates its interop assemblies;
   a console window opens alongside the game. Later starts are normal.
3. Play. A controller can stay plugged in; both work at once and the prompts follow whichever you touched last.

To uninstall, delete `BepInEx\plugins\TeradeaKbm`. To remove BepInEx too, delete `winhttp.dll`,
`doorstop_config.ini`, `.doorstop_version`, `changelog.txt`, and the `BepInEx` and `dotnet` folders.

## Default keys

| Action | Keys |
|---|---|
| Move | W A S D |
| Camera | Mouse |
| Confirm / interact / use tool | **F**, Enter, left click |
| Cancel / back | **Esc**, right click |
| Item bag | **Tab** (or I) |
| Y prompts (sort, skip, ...) | **R** |
| Previous / next tab | **Q** / **E** |
| Dash | **Shift** (hold) |
| Camera zoom in / out | **Z** / mouse wheel |
| Camera reset / top-down view | **C** (or middle click) / **V** |
| Menu / options | **Esc** |
| Quick actions (d-pad), menu selectors, page switching | Arrow keys |
| Right stick in menus (lists, sub-tabs, pages) | Arrow keys |
| Walk instead of run | hold **Ctrl** |
| Release the mouse cursor | hold **Alt** |

The buttons are remappable in `BepInEx\config\EchoesOfTeradea.KeyboardMouse.cfg` (created on first launch). Each
entry lists keys separated by `;`; a bare name is a keyboard key (`e`, `leftShift`, `upArrow`...), anything in
angle brackets is a raw Unity Input System path such as `<Mouse>/leftButton`. Prompts follow your bindings
automatically. Movement (WASD) and the directional keys (arrows) are fixed, because their prompts are drawn as a
WASD cluster and an arrow cross. Mouse sensitivity and Y inversion are in the `[Mouse]` section.

## Known limits

- Tutorial pictures inside the Tips menu are pre-rendered screenshots and still show controller buttons.
- Menus are navigated with keys, not the mouse pointer. The game has no pointer UI to hook.
- The mouse only steers the camera during gameplay. In menus it's ignored, because the game routes its mouse-look
  action into tab strips and would page through them as you move the mouse.

## Modder notes (for modders)

- **Why there's no keyboard support.** `InputSystem.settings.supportedDevices` is a whitelist of six gamepad
  layouts. Unity never creates keyboard or mouse devices. The mod clears it at startup, then disables the game's
  own keyboard bindings (via empty binding overrides) and adds its own from the config so remapping has one source
  of truth. The mouse delta is fed into the right-stick `Look` action (scaled, gated to gameplay action sets) so the
  camera keeps its follow behaviour; the game's own `LookMouse` action is a touch-style drag camera that never
  follows the player, which 0.0.1 used by mistake.
- **Prompts.** Every glyph is a UI `Image` whose sprite is named `*icon_guide_*`. A hook on `Image.sprite` swaps in
  the matching blank shape from `keys\` (one PNG per original sprite name) and renders the key names into the boxes
  listed in the PNG's `.json` sidecar, using the game's own font. Text prompts use private-use font characters
  chosen per controller from the `MSGKEYACTIONPARAM` table; the mod reads `MessageManager.keyAction` back to map
  them to controls and rewrites them on `TMP_Text.text`.
- **Labels created while hidden.** TextMeshPro measures nonsense for a label whose prompt isn't active yet, which
  gave 10x font sizes. Measurements outside a plausible range are replaced by a geometric estimate and re-measured
  once the label is live.
- **BepInEx 6 on Unity 6000.3.** Build be.788 reads this game's IL2CPP metadata (v39), but its bundled
  Il2CppInterop hooks the wrong function in `Class::GetDefaultFieldValue` on 6000.3 and the CLR dies with
  `0x80131506` seconds after startup (Il2CppInterop issue #283). The bundled fix is
  [Il2CppInterop PR #291](https://github.com/BepInEx/Il2CppInterop/pull/291) (commit `637e115`), built from the
  author's branch and swapped into `BepInEx\core` as `Il2CppInterop.Runtime.dll`, `Il2CppInterop.Common.dll` and
  `Il2CppInterop.HarmonySupport.dll`, plus `TerraFX.Interop.Windows.dll` which that build needs and the stock
  bundle doesn't ship. Il2CppInterop is LGPL-3.0; the modified source is the linked branch. Once a BepInEx build
  ships with the fix merged, the stock package works and these four files can be dropped.
- **Startup rule.** Nothing touches `UnityEngine.InputSystem` or installs the prompt hooks from the chainloader;
  both happen from the `InputSystemManager.Awake` prefix.
- **Erasing bindings** through `BindingSyntax.Erase()` doesn't work through the interop (null reference inside
  IL2CPP). `ApplyBindingOverride(index, "")` does.
- **Don't Harmony-patch methods that take structs by value** (e.g. `OperateGuideButton.Initialize` with its
  `CancellationToken`): the detour's struct marshalling crashes intermittently with an access violation in
  `il2cpp_runtime_invoke`. 0.0.1 did; 0.0.2 relies on the `Image.sprite` hook alone.

## Building

- .NET SDK 8 or newer. References resolve from `BepInEx\core` and the `BepInEx\interop` folder the loader generates
  on first launch (set `GameDir` in the csproj or pass `-p:GameDir=...`).
- `tools\deploy.ps1` builds and copies the DLL plus `assets\keys` into the game (game must be closed).
- `tools\package.ps1` builds `dist\TeradeaKbm-<version>.zip` for release, including the patched BepInEx.
- `tools\make_keycaps.ps1` regenerates the blank shapes and their `.json` label sidecars from the originals in
  `reference\glyphs` (dump them with the `DumpGlyphs` config option; the mod pulls all 117 from the asset table).
  `tools\overlay_check.ps1` renders every shape over its original and reports the offset of each.

## Layout

| Path | What |
|---|---|
| `src/TeradeaKbm/` | Plugin source (C#, .NET 6, BepInEx 6 IL2CPP + HarmonyX + Il2CppInterop). |
| `assets/keys/` | Blank key shapes, one per original glyph sprite name, each with a `.json` label layout. |
| `tools/` | Build, deploy, package and glyph-generation scripts. |

`reference/` (decompiled interop, dumped glyphs, Il2CppInterop source) is local only and git-ignored.

## License

MIT. The key images are original drawings made for this mod; no game assets are redistributed. BepInEx and
Il2CppInterop are LGPL-3.0 and are redistributed unmodified except for the Il2CppInterop patch described above.
