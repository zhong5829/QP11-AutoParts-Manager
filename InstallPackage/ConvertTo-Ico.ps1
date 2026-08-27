# JPG转ICO脚本 - 生成含多尺寸的ICO文件(16/32/48/256)
param(
    [Parameter(Mandatory=$true)]
    [string]$SourceFile,
    [string]$OutputFile = "app.ico",
    [int[]]$Sizes = @(16, 32, 48, 256)
)

Add-Type -AssemblyName System.Drawing

if (-not (Test-Path $SourceFile)) {
    Write-Error "Source file not found: $SourceFile"
    exit 1
}

$sourcePath = (Resolve-Path $SourceFile).Path
$source = [System.Drawing.Image]::FromFile($sourcePath)

$iconCount = $Sizes.Count
$iconDataList = [System.Collections.ArrayList]::new()

foreach ($sz in $Sizes) {
    $bmp = New-Object System.Drawing.Bitmap($sz, $sz)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $g.DrawImage($source, 0, 0, $sz, $sz)
    $g.Dispose()

    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $data = $ms.ToArray()
    $ms.Dispose()
    $bmp.Dispose()

    $iconDataList.Add($data) | Out-Null
}

$source.Dispose()

# Calculate offsets
$headerSize = 6 + ($iconCount * 16)
$offsets = [System.Collections.ArrayList]::new()
$currentOffset = $headerSize
foreach ($data in $iconDataList) {
    $offsets.Add($currentOffset) | Out-Null
    $currentOffset += $data.Length
}

# Write ICO file
$outPath = if ([System.IO.Path]::IsPathRooted($OutputFile)) { $OutputFile } else { Join-Path (Get-Location) $OutputFile }
$fs = New-Object System.IO.FileStream($outPath, [System.IO.FileMode]::Create)
$bw = New-Object System.IO.BinaryWriter($fs)

# ICONDIR header
$bw.Write([UInt16]0)                    # Reserved
$bw.Write([UInt16]1)                    # Type: 1 = ICO
$bw.Write([UInt16]$iconCount)           # Image count

# ICONDIRENTRY for each image
for ($i = 0; $i -lt $iconCount; $i++) {
    $sz = $Sizes[$i]
    $w = if ($sz -ge 256) { [byte]0 } else { [byte]$sz }
    $bw.Write($w)                        # Width
    $bw.Write($w)                        # Height
    $bw.Write([byte]0)                   # Color count
    $bw.Write([byte]0)                   # Reserved
    $bw.Write([UInt16]1)                 # Color planes
    $bw.Write([UInt16]32)                # Bits per pixel
    $bw.Write([UInt32]$iconDataList[$i].Length)  # Image data size
    $bw.Write([UInt32]$offsets[$i])      # Image data offset
}

# Image data (PNG format, ICO spec supports it)
for ($i = 0; $i -lt $iconCount; $i++) {
    $bw.Write($iconDataList[$i])
}

$bw.Dispose()
$fs.Dispose()

Write-Host "ICO created: $outPath"
Write-Host "Sizes: $($Sizes -join ', ') px"
