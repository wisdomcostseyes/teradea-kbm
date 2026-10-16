# Nexus header banner (1300x372) from an in-game screenshot: a band around the title logo, with the mod title and a
# row of key caps in the game's prompt style over a darkened lower half.
param(
    [string]$Shot = "$PSScriptRoot\..\dist\screenshots\20261016012135_1.jpg",
    [string]$Out = "$PSScriptRoot\..\dist\banner.png",
    [string]$FontName = "FOT-Seurat Pro B",
    [int]$CropTop = 70,    # band origin in screenshot pixels (2560x1440 source)
    [int]$CropLeft = 160,
    [int]$CropWidth = 2200, # narrower than the frame: zooms in a little and drops the build number top-right
    [switch]$WithText       # overlay the mod title and a row of key caps (default: clean crop)
)
Add-Type -AssemblyName System.Drawing
$W = 1300; $H = 372
$Cream = [System.Drawing.Color]::FromArgb(255, 239, 231, 181)
$Dark  = [System.Drawing.Color]::FromArgb(255, 41, 24, 8)
$src = [System.Drawing.Bitmap]::FromFile($Shot)
$scale = $W / $CropWidth
$bandH = [int]($H / $scale)
$bmp = New-Object System.Drawing.Bitmap -ArgumentList $W, $H
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = 'AntiAlias'; $g.TextRenderingHint = 'AntiAliasGridFit'; $g.PixelOffsetMode = 'HighQuality'; $g.InterpolationMode = 'HighQualityBicubic'
$g.DrawImage($src, (New-Object System.Drawing.Rectangle -ArgumentList 0, 0, $W, $H), (New-Object System.Drawing.Rectangle -ArgumentList $CropLeft, $CropTop, $CropWidth, $bandH), [System.Drawing.GraphicsUnit]::Pixel)
$src.Dispose()
if ($WithText) {
# darken the lower half so the text reads
$p1 = New-Object System.Drawing.Point -ArgumentList 0, ([int]($H * 0.5)); $p2 = New-Object System.Drawing.Point -ArgumentList 0, $H
$grad = New-Object System.Drawing.Drawing2D.LinearGradientBrush -ArgumentList $p1, $p2, ([System.Drawing.Color]::FromArgb(0, 20, 12, 6)), ([System.Drawing.Color]::FromArgb(215, 20, 12, 6))
$g.FillRectangle($grad, 0, [int]($H * 0.5), $W, [int]($H * 0.5) + 1)
}

function RoundRect([float]$x, [float]$y, [float]$w, [float]$h, [float]$r) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath; $d = $r * 2
    $p.AddArc($x, $y, $d, $d, 180, 90); $p.AddArc($x + $w - $d, $y, $d, $d, 270, 90); $p.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90); $p.AddArc($x, $y + $h - $d, $d, $d, 90, 90); $p.CloseFigure(); return $p
}
function Cap([float]$x, [float]$y, [float]$w, [float]$h, [string]$label, [bool]$round, [bool]$solid) {
    $r = if ($round) { [Math]::Min($w, $h) / 2 - 0.01 } else { [Math]::Min($w, $h) * 0.22 }
    $stroke = 4.0
    foreach ($pass in @(@(6, 14), @(3, 22))) {
        $hp = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb($pass[1], $Dark), ($stroke + $pass[0])); $hp.LineJoin = 'Round'
        $sp = RoundRect $x ($y + 2) $w $h $r; $g.DrawPath($hp, $sp); $sp.Dispose(); $hp.Dispose()
    }
    $path = RoundRect $x $y $w $h $r
    if ($solid) { $g.FillPath((New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(191, 41, 25, 11))), $path) }
    else { $pen = New-Object System.Drawing.Pen($Cream, $stroke); $pen.LineJoin = 'Round'; $g.DrawPath($pen, $path); $pen.Dispose() }
    $path.Dispose()
    $family = New-Object System.Drawing.FontFamily($FontName)
    $em = 100.0; $tp = $null
    while ($em -ge 6) {
        $p = New-Object System.Drawing.Drawing2D.GraphicsPath
        $p.AddString($label, $family, 0, [float]$em, (New-Object System.Drawing.PointF(0, 0)), [System.Drawing.StringFormat]::GenericTypographic)
        $b = $p.GetBounds()
        if ($b.Height -le $h * 0.55 -and $b.Width -le $w * 0.78) { $tp = $p; break }
        $p.Dispose(); $em -= 2
    }
    if ($tp) {
        $b = $tp.GetBounds(); $m = New-Object System.Drawing.Drawing2D.Matrix
        $m.Translate([float]($x + $w / 2 - ($b.X + $b.Width / 2)), [float]($y + $h / 2 - ($b.Y + $b.Height / 2))); $tp.Transform($m)
        $sb = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(50, $Dark))
        foreach ($o in @(@(0,2),@(2,0),@(2,2))) { $mm = New-Object System.Drawing.Drawing2D.Matrix; $mm.Translate([float]$o[0], [float]$o[1]); $sp = $tp.Clone(); $sp.Transform($mm); $g.FillPath($sb, $sp); $sp.Dispose() }
        $g.FillPath((New-Object System.Drawing.SolidBrush($Cream)), $tp); $tp.Dispose()
    }
}
function Title([string]$text, [float]$x, [float]$cy, [float]$em, [bool]$centerX) {
    $family = New-Object System.Drawing.FontFamily($FontName)
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $p.AddString($text, $family, 0, [float]$em, (New-Object System.Drawing.PointF(0, 0)), [System.Drawing.StringFormat]::GenericTypographic)
    $b = $p.GetBounds(); $m = New-Object System.Drawing.Drawing2D.Matrix
    $tx = if ($centerX) { $x - ($b.X + $b.Width / 2) } else { $x - $b.X }
    $m.Translate([float]$tx, [float]($cy - ($b.Y + $b.Height / 2))); $p.Transform($m)
    $sb = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(120, $Dark))
    foreach ($o in @(@(0,3),@(3,0),@(3,3),@(2,5),@(5,2))) { $mm = New-Object System.Drawing.Drawing2D.Matrix; $mm.Translate([float]$o[0], [float]$o[1]); $sp = $p.Clone(); $sp.Transform($mm); $g.FillPath($sb, $sp); $sp.Dispose() }
    $g.FillPath((New-Object System.Drawing.SolidBrush($Cream)), $p); $p.Dispose()
}

if ($WithText) {
# text block bottom-left
Title 'Keyboard & Mouse Support' 44 300 50 $false
Title ('with key prompts  ' + [char]0x00B7 + '  powered by BepInEx and spite') 46 344 21 $false
# key caps bottom-right
$capSize = 52; $gap = 10; $capY = 288
$caps = @(@('F', $true, $true), @('Esc', $true, $false), @('Tab', $false, $false), @('Q', $false, $false), @('E', $false, $false), @('Shift', $false, $false))
[float[]]$widths = @($capSize, $capSize, ($capSize * 1.5), $capSize, $capSize, ($capSize * 1.7))
$total = 0; for ($i = 0; $i -lt $caps.Count; $i++) { $total += $widths[$i] + $gap }
$x = $W - 44 - $total + $gap
for ($i = 0; $i -lt $caps.Count; $i++) { Cap $x $capY $widths[$i] $capSize $caps[$i][0] $caps[$i][1] $caps[$i][2]; $x += $widths[$i] + $gap }
}
$g.Dispose(); New-Item -ItemType Directory -Force (Split-Path $Out) | Out-Null; $bmp.Save($Out); $bmp.Dispose(); Write-Output $Out




