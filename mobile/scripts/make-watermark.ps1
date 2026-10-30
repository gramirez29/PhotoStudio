<#
.SYNOPSIS
  Makes assets/watermark.png, the logo as a white shape on a transparent background, from the avatar of the studio.

.DESCRIPTION
  The avatar is drawn on a flat navy square. Every pixel becomes white and keeps as much opacity as it differs from that navy,
  so the shape of the logo, with its shading, stays and the square disappears. The app tints the picture with a color of the
  theme (Image tintColor), so one file serves the light and the dark theme.

.PARAMETER Source
  The avatar picture (photosense_avatar_1080.png).

.PARAMETER Output
  Where to write the watermark. Defaults to ../assets/watermark.png.

.PARAMETER Size
  Width and height of the result, in pixels.
#>
param(
  [Parameter(Mandatory = $true)][string]$Source,
  [string]$Output = (Join-Path $PSScriptRoot '..\assets\watermark.png'),
  [int]$Size = 640
)

Add-Type -AssemblyName System.Drawing

# How far from the navy a pixel must be to be fully opaque (largest difference of any channel, 0-255).
$fullOpacityDistance = 110.0

$original = [System.Drawing.Image]::FromFile((Resolve-Path $Source))
$scaled = New-Object System.Drawing.Bitmap $Size, $Size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$graphics = [System.Drawing.Graphics]::FromImage($scaled)
$graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$graphics.DrawImage($original, 0, 0, $Size, $Size)
$graphics.Dispose()
$original.Dispose()

# The corner of the picture is the flat navy of the background.
$background = $scaled.GetPixel(2, 2)

$rect = New-Object System.Drawing.Rectangle 0, 0, $Size, $Size
$data = $scaled.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::ReadWrite, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$bytes = New-Object byte[] ($data.Stride * $Size)
[System.Runtime.InteropServices.Marshal]::Copy($data.Scan0, $bytes, 0, $bytes.Length)

for ($i = 0; $i -lt $bytes.Length; $i += 4) {
  $distance = [Math]::Max([Math]::Abs($bytes[$i] - $background.B), [Math]::Max([Math]::Abs($bytes[$i + 1] - $background.G), [Math]::Abs($bytes[$i + 2] - $background.R)))
  $alpha = [Math]::Min(255, [int](255 * $distance / $fullOpacityDistance))
  $bytes[$i] = 255
  $bytes[$i + 1] = 255
  $bytes[$i + 2] = 255
  $bytes[$i + 3] = $alpha
}

[System.Runtime.InteropServices.Marshal]::Copy($bytes, 0, $data.Scan0, $bytes.Length)
$scaled.UnlockBits($data)
$scaled.Save($Output, [System.Drawing.Imaging.ImageFormat]::Png)
$scaled.Dispose()
Write-Output "Wrote $Output"
