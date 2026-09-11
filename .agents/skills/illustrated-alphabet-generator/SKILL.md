---
name: illustrated-alphabet-generator
description: Build reusable Latin and Cyrillic illustrated-letter sprite alphabets from painted word or alphabet references, including clean alpha, normalized glyph metrics, contour-based pair kerning, sprite sheets, previews, and Unity UI Toolkit integration data. Use for localized UI text that must preserve a hand-drawn reference style; do not use for ordinary font selection.
---

# Illustrated Alphabet Generator

Create reusable glyph assets, not flattened word images. Localized text must remain assembled from individual letters.

## Source decision

- If complete alphabet sheets exist, process them directly.
- If only painted words exist, extract their clean reusable letters first. Generate missing letters in the same visual language. This step needs agent visual judgment; geometry and kerning cannot invent unseen letterforms.
- Prefer references covering straight, round, diagonal, narrow, wide, and language-specific forms. For Cyrillic include forms such as `Ё`, `Ж`, `Й`, `Ф`, `Щ`, `Ъ`, `Ы`, `Ю`, `Я`.
- Keep source sheets and visual references outside runtime asset folders. Put only final glyphs and derived data into the product.

## Required visual invariants

- Transparent background is real alpha, never painted checkerboard.
- No source-background color, matte, halo, or neighbour pixels remain on a glyph.
- Every glyph uses one content height and baseline. Width preserves its aspect ratio.
- Side padding is equal and minimal.
- Pair kerning follows visible contours. Default target: outlines touch without fill overlap. A small configurable outline overlap is allowed when the reference uses it.
- Compare rendered words at final UI size and angle. A contact that looks correct at source size may open after scaling.

## Workflow

1. Collect reference word images and list exact supported alphabets.
2. Extract existing letters. Generate missing glyphs as fixed-grid transparent alphabet sheets.
3. Visually reject inconsistent letters before deterministic processing.
4. Run `scripts/build_illustrated_alphabet.py` for normalization, individual sprites, contact kerning, contact sheets, and manifest.
5. Run `scripts/render_word_preview.py` for every current localization string and target scale.
6. Adjust `--contact-overlap`, per-surface tracking, or whole-word scale in that order. Fix glyph shape or crop before adding arbitrary pair overrides.
7. For Unity UI Toolkit integration, read [references/unity-ui-toolkit.md](references/unity-ui-toolkit.md).
8. Validate light, dark, and checkerboard backgrounds plus final in-engine screenshots.

## Commands

```powershell
python scripts/build_illustrated_alphabet.py `
  --latin-sheet C:\path\alphabet_latin.png `
  --cyrillic-sheet C:\path\alphabet_cyrillic.png `
  --output C:\path\illustrated_alphabet `
  --contact-overlap 2

python scripts/render_word_preview.py `
  --alphabet-root C:\path\illustrated_alphabet `
  --text "ВЫБРАТЬ УРОВЕНЬ" `
  --output C:\path\preview.png `
  --scale 0.28 `
  --tracking -14 `
  --angle -1
```

`build_illustrated_alphabet.py` expects Latin rows `ABCDEFGHIJKLM` / `NOPQRSTUVWXYZ` and Cyrillic rows `АБВГДЕЁЖЗИЙ` / `КЛМНОПРСТУФ` / `ХЦЧШЩЪЫЬЭЮЯ`.

## Completion

- Latin: 26 sprites. Cyrillic: 33 sprites.
- Manifest reports both alphabets complete and alpha-valid.
- Contact sheets show one height and baseline.
- Current localized words have even optical spacing at final size.
- No clipped text, coloured fringe, or neighbour contamination.
- Product compile and real UI capture pass after integration.
