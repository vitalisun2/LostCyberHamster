# Unity UI Toolkit integration

Read this reference when final illustrated glyphs must render as localized Unity UI Toolkit text.

## Import

Each glyph PNG:

- Texture Type: `Sprite (2D and UI)`
- Sprite Mode: `Single`
- Mesh Type: `Full Rect`
- Mip Maps: off
- Wrap Mode: `Clamp`
- Filter Mode: `Bilinear`
- Alpha Is Transparency: on
- AssetBundle: project UI bundle when used

Preserve generated width and height. Runtime width equals `glyphAspect * commonHeight`.

## Runtime layout

Use one host and one inner horizontal word container. Create one non-growing `VisualElement` per glyph. Assign:

- common glyph height;
- width from `glyphMetrics`;
- sprite through a Unicode-based USS class such as `--u0041`;
- `margin-left` from pair matrix for every glyph after the first;
- explicit space width; reset pair state after whitespace.

Keep whole-word scale and angle on the inner word container. This rotates and scales spacing together with letters.

Small labels may use extra negative tracking after contour kerning when the approved reference intentionally merges black outlines. Apply this at surface level, not by changing shared glyph crops.

## Data

`illustrated_alphabet.json` contains:

- aspect ratio for each Unicode glyph;
- alphabet order and dimensions;
- signed pair margins as readable rows;
- the same matrix as signed-byte Base64 for compact runtime embedding.

Decode each byte as signed 8-bit. Matrix index is `leftIndex * alphabetSize + rightIndex`.

## Validation

After import:

1. Refresh Unity AssetDatabase and regenerate project files.
2. Compile.
3. Open the actual screen in Play Mode.
4. Switch every supported localization through the real localization service.
5. Check computed word scale, bounds, and per-glyph margins when the screenshot differs from source files.
6. Capture full-resolution screenshots and inspect tight pairs, long strings, edges, baseline, and colour fringe.
