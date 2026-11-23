# Сборка btwTweaker встроенным в Windows компилятором C# (.NET Framework 4.8, C# 5).
# Запуск:  powershell -ExecutionPolicy Bypass -File build.ps1
#   -NoAdmin  тестовая сборка: без запроса прав администратора, с тестовыми режимами (снимки экрана)
param([string]$Out = "$PSScriptRoot\btwTweaker.exe", [switch]$NoAdmin)

$ErrorActionPreference = 'Stop'
$csc = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path $csc)) { $csc = "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\csc.exe" }
$src = "$PSScriptRoot\src"

# Иконка: скруглённый квадрат с сине-фиолетовым градиентом и белой молнией (PNG внутри ICO)
Add-Type -AssemblyName System.Drawing
$ico = "$src\app.ico"
$sizes = 16, 24, 32, 48, 64, 256
$pngs = foreach ($s in $sizes) {
    $bmp = New-Object System.Drawing.Bitmap $s, $s
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'; $g.PixelOffsetMode = 'HighQuality'
    $pad = if ($s -le 16) { 0.5 } else { $s * 0.04 }
    $rw = $s - $pad * 2; $d = $rw * 0.5
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddArc($pad, $pad, $d, $d, 180, 90); $path.AddArc($pad + $rw - $d, $pad, $d, $d, 270, 90)
    $path.AddArc($pad + $rw - $d, $pad + $rw - $d, $d, $d, 0, 90); $path.AddArc($pad, $pad + $rw - $d, $d, $d, 90, 90); $path.CloseFigure()
    $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush ((New-Object System.Drawing.PointF 0, $pad), (New-Object System.Drawing.PointF $s, ($pad + $rw)), ([System.Drawing.Color]::FromArgb(84, 112, 255)), ([System.Drawing.Color]::FromArgb(124, 58, 237)))
    $g.FillPath($brush, $path)
    # молния
    $k = $s / 100.0
    $pts = @((56, 14), (26, 56), (47, 56), (40, 86), (74, 42), (53, 42), (60, 14)) | ForEach-Object { New-Object System.Drawing.PointF ($_[0] * $k), ($_[1] * $k) }
    $g.FillPolygon([System.Drawing.Brushes]::White, [System.Drawing.PointF[]]$pts)
    $g.Dispose()
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
    , $ms.ToArray()
}
$fs = [System.IO.File]::Create($ico); $w = New-Object System.IO.BinaryWriter $fs
$w.Write([uint16]0); $w.Write([uint16]1); $w.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $s = $sizes[$i]; $b = if ($s -ge 256) { 0 } else { $s }
    $w.Write([byte]$b); $w.Write([byte]$b); $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([uint16]1); $w.Write([uint16]32); $w.Write([uint32]$pngs[$i].Length); $w.Write([uint32]$offset)
    $offset += $pngs[$i].Length
}
foreach ($p in $pngs) { $w.Write($p) }
$w.Close()

$cscArgs = @('/nologo', '/target:winexe', '/optimize+', '/codepage:65001', '/platform:anycpu', "/out:$Out", "/win32icon:$ico",
    '/r:System.dll', '/r:System.Core.dll', '/r:Microsoft.CSharp.dll', '/r:System.Management.dll', '/r:System.Web.Extensions.dll', '/r:System.Drawing.dll', '/r:Microsoft.VisualBasic.dll', '/r:System.ServiceProcess.dll', '/r:System.Xml.dll')
if ($NoAdmin) { $cscArgs += '/define:TESTHOOKS' } else { $cscArgs += "/win32manifest:$src\app.manifest" }
$fw = Split-Path $csc
$cscArgs += "/r:$fw\WPF\PresentationFramework.dll", "/r:$fw\WPF\PresentationCore.dll", "/r:$fw\WPF\WindowsBase.dll", "/r:$fw\System.Xaml.dll"
$cscArgs += "/resource:$src\lang\en.tsv,en.tsv"
$cscArgs += (Get-ChildItem "$src\core\*.cs").FullName
$cscArgs += (Get-ChildItem "$src\wpf\*.cs").FullName

& $csc @cscArgs
if ($LASTEXITCODE -ne 0) { throw "Сборка завершилась с ошибкой" }

$hash = (Get-FileHash -Algorithm SHA256 $Out).Hash.ToLowerInvariant()
[System.IO.File]::WriteAllText("$Out.sha256", "$hash  $([System.IO.Path]::GetFileName($Out))`n")
Write-Host "Готово: $Out"
