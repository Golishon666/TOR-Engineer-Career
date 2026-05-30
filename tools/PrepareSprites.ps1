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

$illustrationDir = Join-Path $ModulePath "GUI\SpriteParts\ui_tor_engineer_career\CareerSystem\Illustrations"
$iconDir = Join-Path $ModulePath "GUI\SpriteParts\ui_tor_engineer_ability"
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

$configPath = Join-Path $ModulePath "GUI\SpriteParts\Config.xml"
$configTemplate = Join-Path $ModulePath "GUI\SpriteParts\Config.xml.example"
$spriteDataPath = Join-Path $ModulePath "GUI\TOR_EngineerCareerSpriteData.xml"
$tpacDir = Join-Path $ModulePath "Assets\GauntletUI"
$hasPackagedSprites = (Test-Path $spriteDataPath) -and (Test-Path $tpacDir) -and ((Get-ChildItem $tpacDir -Filter *.tpac -ErrorAction SilentlyContinue).Count -gt 0)

if ($hasPackagedSprites) {
    if (-not (Test-Path $configPath) -and (Test-Path $configTemplate)) {
        Copy-Item $configTemplate $configPath -Force
        Write-Host "Enabled Config.xml because packaged sprites were found."
    }
}
elseif (Test-Path $configPath) {
    Remove-Item $configPath -Force
    Write-Warning "Removed Config.xml: packaged sprites are not ready yet (prevents Mod Kit / game crash)."
}

$generator = Join-Path $BannerlordPath "bin\Win64_Shipping_wEditor\TaleWorlds.TwoDimension.SpriteSheetGenerator.exe"
if (-not (Test-Path $generator)) {
    Write-Warning "SpriteSheetGenerator not found. Install Bannerlord Modding Kit first."
    exit 0
}

Write-Host "Launch SpriteSheetGenerator manually from Bannerlord root when ready."
Write-Host "Categories: ui_tor_engineer_career, ui_tor_engineer_ability"
