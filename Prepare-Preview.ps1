param(
    [Parameter(Mandatory = $true)][string]$InputImage,
    [Parameter(Mandatory = $true)][string]$OutputImage
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$source = [System.Drawing.Image]::FromFile($InputImage)
try {
    $width = 768
    $height = [int][Math]::Round($source.Height * $width / $source.Width)
    $bitmap = [System.Drawing.Bitmap]::new($width, $height)
    try {
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
        try {
            $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $graphics.DrawImage($source, 0, 0, $width, $height)
        } finally { $graphics.Dispose() }
        $bitmap.Save($OutputImage, [System.Drawing.Imaging.ImageFormat]::Png)
    } finally { $bitmap.Dispose() }
} finally { $source.Dispose() }
if ((Get-Item -LiteralPath $OutputImage).Length -ge 1MB) {
    throw 'Preview exceeds the 1 MB upload budget.'
}
