# Skin Candidate Pipeline

Reusable pipeline для создания и безопасного переноса geometry-safe Unity
sprite sheets. Каждый инструмент получает explicit source, candidate и target
roots.

## Инструменты

- `generate_geometry_safe_skin.py` меняет RGB внутри source mask. Canvas, grid,
  frame order, empty cells и alpha bytes сохраняются.
- `unity/SkinCandidateImporterParityTool.cs` переносит texture importer,
  sprite rects/names/pivots, platform settings и custom physics shapes через
  Unity API. Flag `-skinPreserveCandidateIds` сохраняет tracked target GUID,
  sprite IDs, names и name/fileID mappings.
- `validate_skin_candidate.py` проверяет PNG, per-cell geometry, alpha/masks,
  importer settings, sprite metadata и physics-shape parity.
- `promote_skin_candidate.py` переносит validated top-level PNG + `.meta` без
  duplicate Unity GUID. Default mode — dry-run. Запись требует `--move`.

Python-инструментам нужны Python 3, Pillow и NumPy.

## Процесс

1. Использовать каждый default PNG как ImageGen edit reference. AI output
   остаётся design concept до geometry QA.
2. Создать source-mask-preserving sheets в
   `Assets/Content/skins/_generated_candidates/`.
3. Настроить candidate importer через Unity API.
4. Запустить validator. Import-ready candidate проходит все проверки.
5. Запустить promotion dry-run. После approval выполнить explicit `--move`.
6. Хранить failed concepts в Unity-ignored archive.

## Генерация Cyberpunk Pulse skateboard sheets

Запуск из repository root:

```powershell
python tools/skin_candidates/generate_geometry_safe_skin.py `
  --source-root LostCyberHamster/Assets/Content/skins/skateboard_mode/default `
  --output-root LostCyberHamster/Assets/Content/skins/_generated_candidates/neon-runner/variant_a/skateboard_mode `
  --profile cyberpunk-pulse
```

Subset: `--sheets Run_1.png Jump.png`. Skateboard defaults используют
`--cell-width 240 --cell-height 650`.

## Unity importer parity

Поместить `unity/SkinCandidateImporterParityTool.cs` под `Assets/Editor/` в
Unity project для импорта. Запуск:

```powershell
Unity.exe -batchmode -quit `
  -projectPath <unity-project> `
  -executeMethod SkinCandidateImporterParityTool.Run `
  -skinSourceRoot Assets/Content/skins/skateboard_mode/default `
  -skinCandidateRoot Assets/Content/skins/_generated_candidates/neon-runner/variant_a/skateboard_mode `
  -skinSheets Run_1.png,Run_2.png,Run_3.png,Jump.png,Double_Jump.png `
  -logFile <log-path>
```

`-skinSheets` опционален. Без него tool находит top-level candidate PNG с
matching source. Roots задаются как project-relative `Assets/...` paths.

Для нового candidate flag `-skinPreserveCandidateIds` не используется: tool
создаёт новые sprite IDs и source names. Для tracked target, уже referenced из
clips/prefabs, добавить flag:

```powershell
Unity.exe -batchmode -quit `
  -projectPath <unity-project> `
  -executeMethod SkinCandidateImporterParityTool.Run `
  -skinSourceRoot Assets/Content/skins/normal_mode/default `
  -skinCandidateRoot Assets/Content/skins/normal_mode/quantum-scout `
  -skinPreserveCandidateIds `
  -logFile <log-path>
```

Preserve mode требует одинаковый sprite count. Texture GUID, каждый existing
`SpriteRect.spriteID`, sprite name и name/fileID mapping сохраняются по index.
Source задаёт rect, border, alignment, pivot, outlines, custom physics,
importer contract и platform settings.

## Validation

```powershell
python tools/skin_candidates/validate_skin_candidate.py `
  --source-root LostCyberHamster/Assets/Content/skins/skateboard_mode/default `
  --candidate-root LostCyberHamster/Assets/Content/skins/_generated_candidates/neon-runner/variant_a/skateboard_mode `
  --output-json LostCyberHamster/Assets/Content/skins/_generated_candidates/neon-runner/variant_a/qa_full.json `
  --output-md LostCyberHamster/Assets/Content/skins/_generated_candidates/neon-runner/variant_a/QA_REPORT.md `
  --cell-width 240 `
  --cell-height 650
```

Validator находит matching PNG, пропускает dot/underscore concept directories
и возвращает nonzero exit code при failure. Проверки:

- exact RGBA8 dimensions и alpha bytes;
- per-sprite/per-cell alpha bbox и mask;
- отсутствие pixels в metadata-empty cells;
- sprite count/order, names, rects, pivots и alignment;
- texture importer и platform settings;
- custom physics shapes.

Metadata sprite count — источник правды. Текущий `Run_3` содержит 11 sprites на
canvas `6x2`; двенадцатая cell пустая.

## Promotion

Dry-run ничего не меняет:

```powershell
python tools/skin_candidates/promote_skin_candidate.py `
  --source-root LostCyberHamster/Assets/Content/skins/skateboard_mode/default `
  --candidate-root LostCyberHamster/Assets/Content/skins/_generated_candidates/neon-runner/variant_a/skateboard_mode `
  --target-root LostCyberHamster/Assets/Content/skins/skateboard_mode/neon-runner
```

После approval добавить `--move`. Existing target должен быть пустым и требует
`--allow-existing-empty-target`. Promotion переносит validated top-level PNG и
Unity `.meta` парой. Move-only contract сохраняет GUID без duplicate.

## Safety contract

- Production/default PNG и source `.meta` остаются неизменными до explicit
  promotion.
- Candidate importer настраивает Unity API.
- Tracked targets, referenced из clips/prefabs, настраиваются только с
  `-skinPreserveCandidateIds` и после backup.
- Canvas dimensions, grid, frame order, alpha, silhouette, position,
  proportions и physics geometry совпадают с source.
- Palette, clothing, materials, patterns и compact details меняются только
  внутри source opaque mask.
- Failed geometry/design проходит correction до трёх попыток.
- Stage, commit и promotion выполняются только по отдельной команде.
