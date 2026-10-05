$ErrorActionPreference='Stop'
Add-Type -AssemblyName PresentationCore,WindowsBase
$font=New-Object System.Windows.Media.GlyphTypeface([Uri](Join-Path $PSScriptRoot 'ZCOOLKuaiLe-Regular.ttf'))
$group=New-Object System.Windows.Media.GeometryGroup
$offset=0.0
foreach($letter in '语伴'.ToCharArray()) {
    $glyph=$font.GetGlyphOutline($font.CharacterToGlyphMap[[int]$letter],100,100)
    $glyph.Transform=New-Object System.Windows.Media.TranslateTransform($offset,0)
    $group.Children.Add($glyph)
    $offset+=104
}
[IO.File]::WriteAllText((Join-Path $PSScriptRoot 'yuban.path'),$group.GetFlattenedPathGeometry().ToString([Globalization.CultureInfo]::InvariantCulture))
