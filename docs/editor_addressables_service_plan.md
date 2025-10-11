# Editor Addressables Service Mini-Plan

## Goal
Introduce a dedicated service responsible for editor-side Addressables access (sprites and JSON data), reducing responsibilities from `LevelTilemapUi`, `LevelDataManager`, and legacy loaders, while providing a single place to manage leases and location normalization.

## Tasks
1. **Service Skeleton**
   - Create `EditorAddressablesService` (namespace `Assets.Editor.LevelEditor.AddressablesSupport`).
   - Inject or expose dependencies via static entry point for now (future refactor can move to DI).

2. **Sprite Loading API**
   - Method `LoadObstacleSprites(string location)` returning `AddressableSetLease<Sprite>` (internally handles template fallback and label composition).
   - Optional overloads for other label-based sprite categories (collectables, decor) to prepare for future migrations.

3. **JSON (Mappings) API**
   - Methods `LoadObstacleMappings(string location, Action<Dictionary<string, ObstacleTypeEnum>> onLoaded)` and `SaveObstacleMappings(string location, Dictionary<string, ObstacleTypeEnum> bindings)` wrapping existing logic from `LevelDataManager`.
   - Centralize label/key building; ensure asset registration is encapsulated.

4. **Location Helpers**
   - Provide helper `ResolveLocation(string location, AddressableAssetType type)` to encapsulate template → New York fallback and future overrides.
   - Keep reusable for runtime if needed later.

5. **Migration Steps**
   - Update `LevelTilemapUi` to consume `EditorAddressablesService.LoadObstacleSprites` instead of inline logic.
   - Refactor `ObstacleSpriteTypeMappingsManager` and `LevelDataManager` to call the service for Addressables interactions, leaving them focused on UI/state logic.
   - Evaluate remaining references to legacy `SpriteLoader`; plan deprecation once editor consumers migrate.

6. **Testing & Verification**
   - Add editor-only tests or test harness to cover sprite lease lifecycle and JSON round-trips.
   - Manual smoke test in Level Tilemap Editor for location switching and mapping saves.

## Risks / Considerations
- Ensure service handles disposal correctly (optionally provide helper `Release` methods for consumers that cannot keep track).
- Account for existing caching behavior (if needed, consider thin caching inside service).
- Keep method naming aligned with existing `AddressableLoader` semantics to avoid confusion.
