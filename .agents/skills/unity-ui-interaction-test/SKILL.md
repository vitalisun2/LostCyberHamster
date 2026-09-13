---
name: unity-ui-interaction-test
description: Запускает реальный регрессионный тест интерактивности UI Toolkit в LostCyberHamster по подготовленным экранам и модальным состояниям. Использовать для прокликивания видимых кнопок и других интерактивных элементов, проверки переходов, скроллов, свайпов, пропавшего первого клика и зависшего блока ввода с машинно-читаемым отчётом. Это поведенческий тест без создания скриншотов.
---

# Unity UI interaction test

Use the existing `unity-ui-capture` LostCyberHamster adapter only to prepare the 118 deterministic UI states. Do not take screenshots. Dispatch pooled UI Toolkit pointer/wheel events through the live panel.

## Preconditions

- Work from the repository root; Unity project is `LostCyberHamster`.
- Unity Editor must be open, stopped, compiled, settled, and the active scene clean.
- Respect `.worktrees/.integration-lock`. Never overwrite another owner.
- Source changes must be recompiled before the behavioral run.

## Commands

```powershell
$env:PYTHONUTF8='1'
python .agents/skills/unity-ui-interaction-test/scripts/ui_interaction.py inspect --project LostCyberHamster
python .agents/skills/unity-ui-interaction-test/scripts/ui_interaction.py run --project LostCyberHamster --case home --progress
python .agents/skills/unity-ui-interaction-test/scripts/ui_interaction.py run --project LostCyberHamster --with-test-level --progress
```

Filters match the capture adapter: repeat `--case`, `--state case:state`, or `--only` wildcard filters. Use a fresh `--out` directory or omit it to write under Downloads.

## What counts as evidence

- Enabled buttons receive a real pointer down/up pair and emit their click.
- Custom tap/swipe surfaces receive real pointer events; scroll views receive a wheel event.
- Disabled controls do not activate.
- Input-triggered screen replacement must not happen synchronously inside the originating event dispatch.
- After every action, `UiInputBlock` must clear; after a transition, a harmless pointer-move probe must reach a control on the new UI tree on the first try.
- Unexpected Unity error/exception/assert logs fail the action.
- External IAP, ad, account, cloud-choice, and scene-changing controls are routed to the correct live target but stopped at the panel root. Their external side effects require their dedicated sandbox/device test.
- `interaction-result.json` is the detailed evidence; `run.json` records Editor/profile/lock cleanup; `summary.json` is the compact result.

The optional test level runs first at 3x simulation speed and must complete with `WIN`. Override it with `--test-level-time-scale` only when timing-sensitive gameplay requires real time. The UI pass prepares each state once, reuses it for guarded/disabled controls, waits for actual layout stability instead of screenshot delays, and checkpoints reports in batches. The UI run uses a progression-testing profile and restores the original profile before stopping Play Mode.
