# Review sheet: for every generated key shape, draw the original glyph (dimmed, magenta tint) with the generated
# shape over it, plus the art-box centres of both, so positional drift is visible at a glance.
param(
    [string]$Glyphs = "$PSScriptRoot\..\reference\glyphs",
    [string]$Keys   = "$PSScriptRoot\..\assets\keys",
    [string]$Out    = "$PSScriptRoot\..\reference\overlay_check.png"
)
Add-Type -AssemblyName System.Drawing
function Get-Box($bmp, [int]$threshold) {
    $minX = $bmp.Width; $maxX = -1; $minY = $bmp.Height; $maxY = -1
    for ($y = 0; $y -lt $bmp.Height; $y++) { for ($x = 0; $x -lt $bmp.Width; $x++) {
        if ($bmp.GetPixel($x, $y).A -gt $threshold) { if ($x -lt $minX) { $minX = $x }; if ($x -gt $maxX) { $maxX = $x }; if ($y -lt $minY) { $minY = $y }; if ($y -gt $maxY) { $maxY = $y } }
    } }
    if ($maxX -lt 0) { return $null }
    return @{ X = $minX; Y = $minY; W = ($maxX - $minX + 1); H = ($maxY - $minY + 1); CX = (($minX + $maxX + 1) / 2); CY = (($minY + $maxY + 1) / 2) }
}
$files = Get-ChildItem $Keys -Filter "*.png" | Sort-Object Name
$scale = 4; $cellW = 64 * $scale; $cellH = 64 * $scale + 14; $perRow = 10
$rows = [Math]::Ceiling($files.Count / $perRow)
$sheet = New-Object System.Drawing.Bitmap -ArgumentList ($perRow * $cellW), ($rows * $cellH)
$g = [System.Drawing.Graphics]::FromImage($sheet)
$g.Clear([System.Drawing.Color]::FromArgb(255, 70, 46, 34)); $g.InterpolationMode = 'NearestNeighbor'; $g.PixelOffsetMode = 'Half'
$font = New-Object System.Drawing.Font("Consolas", 9)
$tintAttr = New-Object System.Drawing.Imaging.ImageAttributes
$cm = New-Object System.Drawing.Imaging.ColorMatrix
$cm.Matrix00 = 1; $cm.Matrix11 = 0.3; $cm.Matrix22 = 1; $cm.Matrix33 = 0.55; $cm.Matrix44 = 1
$tintAttr.SetColorMatrix($cm)
$report = @()
for ($i = 0; $i -lt $files.Count; $i++) {
    $f = $files[$i]; $op = Join-Path $Glyphs $f.Name
    $cx = ($i % $perRow) * $cellW; $cy = [Math]::Floor($i / $perRow) * $cellH
    $n = [System.Drawing.Bitmap]::FromFile($f.FullName)
    $line = $f.Name -replace 'ui_common_icon_guide_', '' -replace '_001_00', ''
    if (Test-Path $op) {
        $o = [System.Drawing.Bitmap]::FromFile($op)
        $dest = New-Object System.Drawing.Rectangle -ArgumentList ([int]$cx), ([int]$cy), ([int]($o.Width * $scale)), ([int]($o.Height * $scale))
        $g.DrawImage($o, $dest, 0, 0, $o.Width, $o.Height, [System.Drawing.GraphicsUnit]::Pixel, $tintAttr)
        $ob = Get-Box $o 100; $nb = Get-Box $n 100
        if ($ob -and $nb) {
            $dx = [Math]::Round($nb.CX - $ob.CX, 1); $dy = [Math]::Round($nb.CY - $ob.CY, 1)
            $report += "{0,-34} orig {1,2}x{2,-2} @({3,4},{4,4})  shape {5,2}x{6,-2} @({7,4},{8,4})  d=({9,5},{10,5})" -f $line, $ob.W, $ob.H, $ob.CX, $ob.CY, $nb.W, $nb.H, $nb.CX, $nb.CY, $dx, $dy
            $pen = New-Object System.Drawing.Pen([System.Drawing.Color]::Magenta, 1); $g.DrawRectangle($pen, $cx + $ob.X * $scale, $cy + $ob.Y * $scale, $ob.W * $scale, $ob.H * $scale)
            $pen2 = New-Object System.Drawing.Pen([System.Drawing.Color]::Cyan, 1); $g.DrawRectangle($pen2, $cx + $nb.X * $scale, $cy + $nb.Y * $scale, $nb.W * $scale, $nb.H * $scale)
        }
        $o.Dispose()
    }
    $g.DrawImage($n, $cx, $cy, $n.Width * $scale, $n.Height * $scale)
    $g.DrawString($line, $font, [System.Drawing.Brushes]::White, $cx, $cy + 64 * $scale)
    $n.Dispose()
}
$g.Dispose(); $sheet.Save($Out); $sheet.Dispose()
$report | Set-Content ($Out -replace '\.png$', '.txt') -Encoding UTF8
Write-Output $Out
