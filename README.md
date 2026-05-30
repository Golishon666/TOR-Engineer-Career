# TOR Engineer Career

Standalone career module for The Old Realms.

## Features

- Adds the Empire-only Engineer career.
- Adds the Imperial Engineer profession option during character creation.
- Starts the character with an Old Rifle and Musket Bullets.
- Adds the `Open Fire!` career ability tooltip and engineer-themed UI assets.
- Career talents support extra ammunition, explosive rounds, piercing shots and ricochets.

## UI assets

Source art lives in `Assets/CareerSystem/`. Game-ready sprites are generated into:

- `GUI/SpriteParts/ui_tor_engineer_career/CareerSystem/Illustrations/Engineer.png` (500×280)
- `GUI/SpriteParts/ui_tor_engineer_ability/engineer_open_fire_icon.png` (256×256)

**Important:** do not ship `GUI/SpriteParts/Config.xml` until both `GUI/TOR_EngineerCareerSpriteData.xml` and `Assets/GauntletUI/*.tpac` exist. Also do not reuse category names `ui_careersystem` / `ui_abilityicons` from TOR_Armory — that causes Mod Kit / game crashes.

### Modding Kit workflow

1. Prepare PNGs and sync the module into the game folder:

```powershell
.\tools\SetupModKitSprites.ps1
```

2. In **SpriteSheetGenerator**, compile `TOR_EngineerCareer` categories `ui_tor_engineer_career` and `ui_tor_engineer_ability`.

3. Launch **Mount & Blade II: Bannerlord - Modding Kit** from Steam, enable this mod, press **Play**.

4. Open the editor console with `` ` `` and run:

```text
resource.show_resource_browser
```

5. Import both sprite categories into `Assets/GauntletUI`.

6. Rename `GUI/SpriteParts/Config.xml.example` to `Config.xml`, then sync again:

```powershell
.\tools\Sync-ModuleToGame.ps1
```

Ship these generated files with the mod:

- `GUI/TOR_EngineerCareerSpriteData.xml`
- `GUI/SpriteParts/Config.xml`
- `Assets/GauntletUI/ui_tor_engineer_career_1_tex.tpac`
- `Assets/GauntletUI/ui_tor_engineer_ability_1_tex.tpac`

## Installation

Copy `TOR_EngineerCareer` into:

```text
Mount & Blade II Bannerlord/Modules/
```

Required modules:

- Native
- SandBoxCore
- Sandbox
- StoryMode
- TOR_Armory
- TOR_Environment
- TOR_Core

Built for The Old Realms `v1.3.15`.
