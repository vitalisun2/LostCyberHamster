# Documentation Index

Quick reference guide to all project documentation.

---

## Project Overview

### README.md
**Location:** Root  
**Purpose:** Main repository documentation
- Git workflow: branch rules (main/develop/feature/bugfix), hooks setup
- Game manual: controls, mechanics, energy/lives/ulta system
- Obstacle types and bonus drop rates

### .github/copilot-instructions.md
**Location:** Root  
**Purpose:** AI assistant guidelines and project conventions
- Development workflow: thoroughness, no guessing, quality over speed
- Git workflow: auto-commit, push with confirmation
- Unity Editor specifics: AnimationMode API, SceneView, prefab structure
- Debugging rules: max 2 blind attempts, diagnostic logging workflow
- Editor tools: ObstacleAnimationImporter, ObstacleAnimationPreviewer

---

## Game Design

### docs/02_game_description.md
**Purpose:** Detailed gameplay description
- Controls: tap to switch lanes, jump button, super strike
- Mechanics: energy (10% per jump, 1%/sec recovery), lives (max 3), ulta (fills on attacks)
- Obstacles: big/small, alive/not alive (granny, dog, manhole, car)
- Bonuses: energy (pizza, energy drink), coins, crystals, lives, super strikes
- Drop rates: 30% chance, 85% energy / 10% crystals / 5% life

### docs/Planning/Milestone New York.md
**Purpose:** First location roadmap
- **Visual:** scrolling environment for all day parts, decor (bushes, trees, cars, mailbox), obstacle animations, intro
- **UI:** game UI (coins/crystals display, level progress), menu UI (day time cards, level numbers)
- **Economy:** analysis and adjustments (8 patterns per level)
- **Game Design:** 2+3+4+5 levels per day part (Morning/Afternoon/Evening/Night), tutorial

### docs/Planning/GameEconomy.md
**Purpose:** Economic model
- **Coins:** sources (bonuses, collectibles, ads 500→50), usage (skins, exchange to crystals)
- **Crystals:** rare drop (~3%), future usage (buy skins instead of coins)
- **Max per level:** ~176 coins (35 static + 141 bonus average)
- **Recommendations:** skins for crystals, lives for coins, exchange rate 1 crystal = 500 coins

---

## Technical Architecture

### docs/Planning/obstacle_animations_implementation_plan.md
**Purpose:** Obstacle animation system architecture
- **Tech stack:** ObstacleAnimatorController + AnimatorOverrideController
- **Addressables:** load by label `<Location> obstacle animations` (case-sensitive)
- **Naming:** `{spriteName}_{animType}.anim` where animType = `walk` (moving) or `idle` (static)
- **Artist workflow:** Procreate (5 FPS) → PNG sequence → Import → Slice → AnimationClip → Addressables
- **Frame sizes:** SmallAlive (152×108), BigAlive (100×212), pivot Bottom Center
- **Movement:** ScrollLeftMechanics (base) + ObstacleMoveMechanics (auto for `walk` animations)

### GameDesignDocWithGuidHistory/Addressables.md
**Purpose:** Addressables organization principles
- **Groups by location:** Loc01_NewYork, Loc02_Paris (level-specific assets)
- **Shared groups:** UI (UXML files), Meta (Skins, Quests, Shop)
- **Naming rules:** hierarchical (`Loc01_NewYork/Level01`), descriptive, CamelCase or underscores, no extensions

---

## Refactoring Plans

### LostCyberHamster/refactor_plan.md
**Purpose:** Global level system refactoring
- **Goal:** Migrate from flat `level_XX` list to hierarchical `Location/PartOfDay/Level` structure
- **Components:** LevelCatalogService, LegacyLevelCatalog / HierarchicalLevelCatalog
- **6 Steps:**
  1. ✅ Catalog & types (LocationId, PartOfDayId, LevelId)
  2. ✅ Resources (parallel hierarchy in Content/locations/)
  3. ✅ Loaders (LevelDataProvider, LevelController via catalog)
  4. UI flows (day time selection → level grid)
  5. ✅ Progress (PlayerData migration, LevelStars/OpenedLevels)
  6. ⏳ Feature flag (toggle between legacy/new mode)
- **Status:** Steps 1-5 complete, step 6 in progress

### docs/sprite_loader_refactor_plan.md
**Purpose:** Sprite loading system refactoring
- **Goal:** Unify sprite loading across runtime and editor tools
- **Solution:** AddressableLoader with lease pattern (RAII via IDisposable)
- **6 Milestones:**
  1. ✅ Requirements (inventory all sprite loaders)
  2. ✅ API design (SpriteLease, SpriteCacheManager, ISpriteProvider)
  3. ✅ Core implementation (AddressableLoader, sync editor wrappers)
  4. Runtime migration (LevelDataProvider, game systems)
  5. Editor migration (SpriteLoader, LevelTilemapEditor, EditorAddressablesService)
  6. Diagnostics (leak detection, documentation)
- **Status:** Milestones 1-3 complete, 4-6 in progress

### docs/ (Other Technical Plans)
- **sprite_loader_api_design.md** — API details for sprite loader refactoring (likely outdated)
- **sprite_loader_core_impl_plan.md** — Core implementation details (likely outdated)
- **sprite_loader_addressables_inventory.md** — Addressables inventory for refactoring
- **editor_addressables_service_plan.md** — Unified service for Editor tools
- **hamster_collision_test_scenarios.md** — Collision test scenarios

### LostCyberHamster/docs/progress_refactor_notes.md
**Purpose:** Working notes for level system refactoring

---

## Developer Tools

### EditorLogs/README.md
**Purpose:** Diagnostic logging system documentation
- **API:** `DebugManager.DiagLog()` writes to `EditorLogs/diagnostic_log.txt`
- **Unity Menu:** `Tools → Diagnostics` (View / Clear / Open Folder)
- **Workflow:** Add DiagLog calls → run code → read_file logs (no manual copy)
- **For AI:** Automates debugging without asking user for console logs

### docs/01_repo_settings.md
**Purpose:** Repository settings (duplicate of README rules section)

---

## Quick Navigation

**Game Design & Planning:**
- Game mechanics → `docs/02_game_description.md`
- NY milestone → `docs/Planning/Milestone New York.md`
- Economy balance → `docs/Planning/GameEconomy.md`

**Technical Implementation:**
- Animations → `docs/Planning/obstacle_animations_implementation_plan.md`
- Addressables → `GameDesignDocWithGuidHistory/Addressables.md`
- Level system → `LostCyberHamster/refactor_plan.md`
- Sprite loading → `docs/sprite_loader_refactor_plan.md`

**Workflows & Tools:**
- AI guidelines → `.github/copilot-instructions.md`
- Diagnostic logs → `EditorLogs/README.md`
- Git rules → `README.md` or `docs/01_repo_settings.md`
