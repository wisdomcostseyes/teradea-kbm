# Builds a release zip for Nexus/GitHub: dist\TeradeaKbm-<version>.zip
# Layout inside the zip mirrors the game folder, so users extract it over the game root. The zip includes the
# patched BepInEx 6 (tools\bepinex_patched, see README "Modder notes") because stock BepInEx 6 crashes on this
# Unity version.
$ErrorActionPreference = "Stop"
$root = Resolve-Path "$PSScriptRoot\.."
$proj = "$root\src\TeradeaKbm\TeradeaKbm.csproj"
$version = ([xml](Get-Content $proj)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
$bepinex = "$root\tools\bepinex_patched"
if (-not (Test-Path "$bepinex\BepInEx\core\Il2CppInterop.Runtime.dll")) { throw "patched BepInEx missing at $bepinex" }

Set-Location "$root\src\TeradeaKbm"
dotnet build -c Release --nologo -v minimal -p:SkipDeploy=true | Out-Null
$dll = "$root\src\TeradeaKbm\bin\Release\net6.0\TeradeaKbm.dll"
if (-not (Test-Path $dll)) { throw "build failed: $dll missing" }

$stage = "$root\dist\stage"
Remove-Item $stage -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force $stage | Out-Null
Copy-Item "$bepinex\*" $stage -Recurse
# a fresh install has no config/cache/log state
foreach ($d in "config", "cache", "interop", "unity-libs", "plugins", "patchers") { Remove-Item "$stage\BepInEx\$d" -Recurse -Force -ErrorAction SilentlyContinue }
Remove-Item "$stage\BepInEx\LogOutput.log" -Force -ErrorAction SilentlyContinue
$plugin = "$stage\BepInEx\plugins\TeradeaKbm"
New-Item -ItemType Directory -Force "$plugin\keys" | Out-Null
Copy-Item $dll $plugin
Copy-Item "$root\assets\keys\*.png" "$plugin\keys"
Copy-Item "$root\assets\keys\*.json" "$plugin\keys"
Copy-Item "$root\README.md" "$stage\TeradeaKbm-README.md"
Copy-Item "$root\LICENSE" "$stage\TeradeaKbm-LICENSE.txt"

$zip = "$root\dist\TeradeaKbm-$version.zip"
New-Item -ItemType Directory -Force "$root\dist" | Out-Null
Remove-Item $zip -Force -ErrorAction SilentlyContinue
Compress-Archive -Path "$stage\*" -DestinationPath $zip
Remove-Item $stage -Recurse -Force
Write-Host "wrote $zip"
