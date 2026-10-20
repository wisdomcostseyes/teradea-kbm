# Measures every generated key cap against the main element of its original glyph. The main element is the
# largest connected solid blob that isn't red (so the press/direction arrows drawn above sticks, which don't
# touch the ring, are ignored, as are the mine arrows). Reports centre offset and size difference per glyph.
param(
    [string]$Glyphs = "$PSScriptRoot\..\reference\glyphs",
    [string]$Keys   = "$PSScriptRoot\..\assets\keys",
    [int]$Alpha = 120,
    [float]$Tolerance = 1.5
)
Add-Type -AssemblyName System.Drawing

function Get-MainBlob($path, [int]$alpha, [bool]$skipRed) {
    $b = [System.Drawing.Bitmap]::FromFile($path)
    $w = $b.Width; $h = $b.Height
    $solid = New-Object 'bool[,]' $w, $h
    for ($y = 0; $y -lt $h; $y++) { for ($x = 0; $x -lt $w; $x++) {
        $c = $b.GetPixel($x, $y)
        $red = ($c.R -gt $c.G + 40 -and $c.R -gt $c.B + 60)
        $solid[$x, $y] = ($c.A -gt $alpha) -and -not ($skipRed -and $red)
    } }
    $b.Dispose()
    $seen = New-Object 'bool[,]' $w, $h
    $best = $null
    for ($sy = 0; $sy -lt $h; $sy++) { for ($sx = 0; $sx -lt $w; $sx++) {
        if (-not $solid[$sx, $sy] -or $seen[$sx, $sy]) { continue }
        $stack = New-Object System.Collections.Generic.Stack[int[]]
        $stack.Push(@($sx, $sy)); $seen[$sx, $sy] = $true
        $n = 0; $minX = $sx; $maxX = $sx; $minY = $sy; $maxY = $sy
        while ($stack.Count -gt 0) {
            $p = $stack.Pop(); $x = $p[0]; $y = $p[1]; $n++
            if ($x -lt $minX) { $minX = $x }; if ($x -gt $maxX) { $maxX = $x }; if ($y -lt $minY) { $minY = $y }; if ($y -gt $maxY) { $maxY = $y }
            foreach ($d in @(@(1,0),@(-1,0),@(0,1),@(0,-1))) {
                $nx = $x + $d[0]; $ny = $y + $d[1]
                if ($nx -ge 0 -and $ny -ge 0 -and $nx -lt $w -and $ny -lt $h -and $solid[$nx, $ny] -and -not $seen[$nx, $ny]) { $seen[$nx, $ny] = $true; $stack.Push(@($nx, $ny)) }
            }
        }
        if ($best -eq $null -or $n -gt $best.N) { $best = @{ N = $n; X = $minX; Y = $minY; W = ($maxX - $minX + 1); H = ($maxY - $minY + 1); CX = (($minX + $maxX + 1) / 2); CY = (($minY + $maxY + 1) / 2) } }
    } }
    return $best
}

$rows = @(); $bad = 0
foreach ($f in Get-ChildItem $Keys -Filter "*.png" | Sort-Object Name) {
    $op = Join-Path $Glyphs $f.Name
    if (-not (Test-Path $op)) { continue }
    $o = Get-MainBlob $op $Alpha $true
    $s = Get-MainBlob $f.FullName $Alpha $false
    $dx = [Math]::Round($s.CX - $o.CX, 1); $dy = [Math]::Round($s.CY - $o.CY, 1)
    $dw = $s.W - $o.W; $dh = $s.H - $o.H
    $flag = if ([Math]::Abs($dx) -gt $Tolerance -or [Math]::Abs($dy) -gt $Tolerance -or [Math]::Abs($dw) -gt 6 -or [Math]::Abs($dh) -gt 6) { "  <-- check" } else { "" }
    if ($flag) { $bad++ }
    $rows += "{0,-42} orig {1,2}x{2,-2} @({3,5},{4,5})  cap {5,2}x{6,-2} @({7,5},{8,5})  d=({9,5},{10,5}) size=({11,3},{12,3}){13}" -f ($f.BaseName -replace 'ui_common_icon_guide_', ''), $o.W, $o.H, $o.CX, $o.CY, $s.W, $s.H, $s.CX, $s.CY, $dx, $dy, $dw, $dh, $flag
}
$rows
Write-Output ("{0} glyphs, {1} flagged (centre off > {2}px or size off > 6px)" -f $rows.Count, $bad, $Tolerance)
