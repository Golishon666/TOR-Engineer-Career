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

- `GUI/SpriteParts/ui_careersystem/CareerSystem/Illustrations/Engineer.png` (500×280)
- `GUI/SpriteParts/ui_abilityicons/engineer_open_fire_icon.png` (256×256)

After updating art, run:

```powershell
.\tools\PrepareSprites.ps1
```

Then use **Bannerlord Modding Kit** to run `TaleWorlds.TwoDimension.SpriteSheetGenerator.exe` and import the new categories in `resource.show_resource_browser`. Ship the generated `GUI/TOR_EngineerCareerSpriteData.xml` and `Assets/GauntletUI/*.tpac` with the mod.

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
