# Package the generated transparent artwork as a multi-resolution Windows icon.
param([string]$InputPng=(Join-Path $PSScriptRoot 'yuban-icon.png'),[string]$OutputIco=(Join-Path $PSScriptRoot 'companion.ico'))
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$source=[Drawing.Image]::FromFile($InputPng)
$sizes=@(16,20,24,32,40,48,64,128,256)
$frames=@()
try {
    foreach($size in $sizes) {
        $bitmap=[Drawing.Bitmap]::new($size,$size,[Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $g=[Drawing.Graphics]::FromImage($bitmap)
        $stream=[IO.MemoryStream]::new()
        try {
            $g.Clear([Drawing.Color]::Transparent)
            $g.CompositingMode=[Drawing.Drawing2D.CompositingMode]::SourceCopy
            $g.InterpolationMode=[Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $g.PixelOffsetMode=[Drawing.Drawing2D.PixelOffsetMode]::HighQuality
            $g.DrawImage($source,[Drawing.Rectangle]::new(0,0,$size,$size))
            $bitmap.Save($stream,[Drawing.Imaging.ImageFormat]::Png)
            $frames+=,@($stream.ToArray())
        } finally { $stream.Dispose(); $g.Dispose(); $bitmap.Dispose() }
    }
} finally { $source.Dispose() }
$writer=[IO.BinaryWriter]::new([IO.File]::Create($OutputIco))
try {
    $writer.Write([uint16]0);$writer.Write([uint16]1);$writer.Write([uint16]$sizes.Count)
    $offset=6+16*$sizes.Count
    for($i=0;$i -lt $sizes.Count;$i++) {
        $dim=if($sizes[$i] -eq 256){0}else{$sizes[$i]}
        $writer.Write([byte]$dim);$writer.Write([byte]$dim);$writer.Write([byte]0);$writer.Write([byte]0)
        $writer.Write([uint16]1);$writer.Write([uint16]32);$writer.Write([uint32]$frames[$i].Length);$writer.Write([uint32]$offset)
        $offset+=$frames[$i].Length
    }
    foreach($frame in $frames) {$writer.Write([byte[]]$frame)}
} finally {$writer.Dispose()}
