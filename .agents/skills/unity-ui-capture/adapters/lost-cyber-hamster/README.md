# LostCyberHamster capture module

Полный план: 118 визуально различных состояний в 31 папке. Он собран и проверен по реальному UI 2026-09-11. Полный повторный захват не требует исследования проекта: runner сам выбирает этот модуль по маркерам.

Не включены состояния, где проверенный кадр менял только текст/число либо визуально дублировал соседний: `ability-upgrade--locked`, `ability-upgrade--level-gate`, `lose--live`, `select-level--next`, `shop--bottom`, `leaderboard--loading`, `notifications--game`, `development-complete--bottom`, `leaderboard-status--no-entry`, `leaderboard-status--sync`, `win-details--pending`.

При изменении UI добавлять состояние только если меняется компоновка, видимость, оформление, enabled/disabled вид, заполнение индикатора, выбранная вкладка, прокрутка к другому содержимому или отдельный overlay/modal. Разные значения при том же виде не добавлять.

Файлы:

- `adapter.json` — автодетект, состав модуля, output и lock.
- `plan.json` — порядок кадров и смысловые папки.
- `ProjectAdapter.cs` — изолированный профиль, UI lifecycle, восстановление.
- `Fixtures.cs`, `ExtraFixtures.cs` — подготовка реальных экранов и состояний.
