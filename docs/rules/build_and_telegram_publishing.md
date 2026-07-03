# Сборка игры и публикация в Telegram

Владелец темы: процесс сборки локального тестового билда LostCyberHamster и публикации APK в Telegram-канал `LostCyberHamster builds`.

## Источник правды

- Использовать локальный Codex skill `publish-build-to-telegram-buffer`: `%USERPROFILE%\.codex\skills\publish-build-to-telegram-buffer`.
- Перед запуском всегда читать `SKILL.md` и `references/lost-cyber-hamster-context.md` внутри skill: там актуальные пути, preflight, ограничения Telegram и детали инфраструктуры.
- Не искать и не печатать Telegram secrets; использовать только уже настроенную локальную конфигурацию или явно переданные пользователем данные.

## Где выполнять

- После feature-задачи или bug fix сборку и публикацию выполнять из task-worktree `.worktrees/<slug>`, где сделаны изменения.
- `integration/unity-live` использовать только как общий Unity-стенд для проверки под lock из Task Branch workflow. После успешной проверки стенд очищается, lock снимается, build запускается из task-worktree.
- Bug regression / analysis-only workflow сам по себе не запускает сборку и публикацию; это отдельный запрос после доказанного root cause или завершённого fix.

## Дефолт сборки

- Если пользователь просит "собери билд", "собери APK", "отправь APK" или "отправь в Telegram", по умолчанию собирать Android development APK (`-Development`).
- Windows build и non-development/release build делать только по явному запросу пользователя.
- Артефакты сохраняются под `Builds/telegram-buffer`; build summary, Telegram caption и финальный ответ должны явно указывать Git branch, short commit и dirty-tree state того worktree, из которого собран билд.

## Минимальный запуск

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$env:USERPROFILE\.codex\skills\publish-build-to-telegram-buffer\scripts\build_unity_player.ps1" -RepositoryRoot "<task-worktree>" -Platform Android -Development
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$env:USERPROFILE\.codex\skills\publish-build-to-telegram-buffer\scripts\publish_latest_apk_to_telegram_buffer.ps1" -RepositoryRoot "<task-worktree>"
```

## Инфраструктура публикации

- `build_unity_player.ps1` делает preflight, ставит Unity build helper, собирает Addressables и player build.
- Полностью автоматическая публикация идёт через локальный Telegram Bot API, если конфиг уже настроен.
- Telegram Desktop delivery требует подтверждения пользователя прямо перед отправкой файла.
- Если Telegram-публикация недоступна, остановиться после сборки и сообщить локальный путь к артефакту.
