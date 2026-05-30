param(
    [string]$ModulePath = (Join-Path $PSScriptRoot ".."),
    [string]$BannerlordPath = "C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord"
)

$ErrorActionPreference = "Stop"
$ModulePath = (Resolve-Path $ModulePath).Path

$sourceBackground = Join-Path $ModulePath "Assets\CareerSystem\engineer_career_background.png"
$sourceIcon = Join-Path $ModulePath "Assets\CareerSystem\engineer_open_fire_icon.png"
if (-not (Test-Path $sourceBackground)) {
    $sourceBackground = "C:\Users\jaros\Downloads\TOR_EngineerCareer_Build\Assets\CareerSystem\engineer_career_background.png"
}
if (-not (Test-Path $sourceIcon)) {
    $sourceIcon = "C:\Users\jaros\Downloads\TOR_EngineerCareer_Build\Assets\CareerSystem\engineer_open_fire_icon.png"
}

$illustrationDir = Join-Path $ModulePath "GUI\SpriteParts\ui_careersystem\CareerSystem\Illustrations"
$iconDir = Join-Path $ModulePath "GUI\SpriteParts\ui_abilityicons"
New-Item -ItemType Directory -Force -Path $illustrationDir, $iconDir | Out-Null

Add-Type -AssemblyName System.Drawing
function Resize-Image {
    param([string]$InputPath, [string]$OutputPath, [int]$Width, [int]$Height)
    $src = [System.Drawing.Image]::FromFile($InputPath)
    $bmp = New-Object System.Drawing.Bitmap $Width, $Height
    $graphics = [System.Drawing.Graphics]::FromImage($bmp)
    $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $graphics.DrawImage($src, 0, 0, $Width, $Height)
    $bmp.Save($OutputPath, [System.Drawing.Imaging.ImageFormat]::Png)
    $graphics.Dispose()
    $bmp.Dispose()
    $src.Dispose()
}

Resize-Image $sourceBackground (Join-Path $illustrationDir "Engineer.png") 500 280
Resize-Image $sourceIcon (Join-Path $iconDir "engineer_open_fire_icon.png") 256 256
Write-Host "Prepared GUI/SpriteParts PNG files."

$generator = Join-Path $BannerlordPath "bin\Win64_Shipping_wEditor\TaleWorlds.TwoDimension.SpriteSheetGenerator.exe"
if (-not (Test-Path $generator)) {
    Write-Warning "SpriteSheetGenerator not found. Install Bannerlord Modding Kit, then:"
    Write-Warning "1. Run TaleWorlds.TwoDimension.SpriteSheetGenerator.exe"
    Write-Warning "2. Import ui_careersystem and ui_abilityicons in resource.show_resource_browser"
    Write-Warning "3. Commit GUI/TOR_EngineerCareerSpriteData.xml and Assets/GauntletUI/*.tpac"
    exit 0
}

Start-Process -FilePath $generator -WorkingDirectory $ModulePath -Wait
Write-Host "SpriteSheetGenerator finished. Import categories in Modding Kit resource browser."
