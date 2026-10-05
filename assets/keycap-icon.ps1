# Original, deterministic keycap logo. Geometry is shared by all raster sizes.
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
function Rounded([single]$x,[single]$y,[single]$w,[single]$h,[single]$r) {
    $path=[Drawing.Drawing2D.GraphicsPath]::new();$d=2*$r
    $path.AddArc($x,$y,$d,$d,180,90);$path.AddArc(($x+$w-$d),$y,$d,$d,270,90)
    $path.AddArc(($x+$w-$d),($y+$h-$d),$d,$d,0,90);$path.AddArc($x,($y+$h-$d),$d,$d,90,90);$path.CloseFigure()
    return ,$path
}
$bitmap=[Drawing.Bitmap]::new(512,512,[Drawing.Imaging.PixelFormat]::Format32bppArgb)
$g=[Drawing.Graphics]::FromImage($bitmap)
$g.SmoothingMode=[Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.Clear([Drawing.Color]::Transparent)
$base=Rounded 24 40 464 448 96
$face=Rounded 44 24 424 400 82
$baseFill=[Drawing.Drawing2D.LinearGradientBrush]::new([Drawing.Point]::new(0,40),[Drawing.Point]::new(0,488),[Drawing.ColorTranslator]::FromHtml('#A8BCD6'),[Drawing.ColorTranslator]::FromHtml('#5B759C'))
$faceFill=[Drawing.Drawing2D.LinearGradientBrush]::new([Drawing.Point]::new(0,24),[Drawing.Point]::new(0,424),[Drawing.ColorTranslator]::FromHtml('#FEFFFF'),[Drawing.ColorTranslator]::FromHtml('#DCE8F6'))
$edge=[Drawing.Pen]::new([Drawing.ColorTranslator]::FromHtml('#7891B2'),5)
$white=[Drawing.Pen]::new([Drawing.Color]::FromArgb(235,255,255,255),5)
$ink=[Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml('#203A60'))
try {
    $g.FillPath($baseFill,$base);$g.DrawPath($edge,$base)
    $g.FillPath($faceFill,$face);$g.DrawPath($white,$face)
    # E drawn as a single shape, independent of installed fonts; bold enough for a 16px tray icon.
    $letter=[Drawing.Drawing2D.GraphicsPath]::new()
    $coords=@(@(162,114),@(350,114),@(350,158),@(214,158),@(214,204),@(332,204),@(332,246),@(214,246),@(214,295),@(350,295),@(350,340),@(162,340))
    $points=[Drawing.PointF[]]@($coords|ForEach-Object{[Drawing.PointF]::new($_[0],$_[1])})
    $letter.AddPolygon($points);$g.FillPath($ink,$letter);$letter.Dispose()
    $bitmap.Save((Join-Path $PSScriptRoot 'yuban-icon.png'),[Drawing.Imaging.ImageFormat]::Png)
} finally {$base.Dispose();$face.Dispose();$baseFill.Dispose();$faceFill.Dispose();$edge.Dispose();$white.Dispose();$ink.Dispose();$g.Dispose();$bitmap.Dispose()}
& (Join-Path $PSScriptRoot 'pack-icon.ps1')
