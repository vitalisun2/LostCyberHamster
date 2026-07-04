# Publish Android Build To Telegram

Используй этот prompt, когда нужно вручную собрать Android APK LostCyberHamster из текущего или явно указанного source checkout и отправить билд в Telegram.

## Вход

Если пользователь явно указал `SourceWorktree`, путь к worktree, ветку или ref - собирай именно его.

Если источник не указан, source по умолчанию - текущий рабочий каталог агента и текущий checkout, в котором агент уже находится.

`BuildLabel` по умолчанию сформируй из текущей ветки или короткого имени задачи. Если checkout dirty, это допустимо для development APK, но обязательно отрази dirty state в результате.

## Обязательный маршрут

1. Прочитай `docs/rules/build_and_telegram_publishing.md`.
2. Прочитай локальный skill `publish-build-to-telegram-buffer`: `SKILL.md` и `references/lost-cyber-hamster-context.md`.
3. Определи `SourceWorktree`:
   - явный путь пользователя;
   - текущий workspace, если источник не указан;
   - отдельный worktree для явной ветки/ref, если переключение текущего checkout небезопасно из-за незакоммиченных изменений.
4. Собери Android development APK через текущий repo/skill workflow из `docs/rules/build_and_telegram_publishing.md`.
5. Опубликуй APK в Telegram через `publish-build-to-telegram-buffer`.
6. В финальном ответе кратко укажи:
   - результат сборки и публикации;
   - APK path;
   - `buildId`;
   - source branch;
   - source commit;
   - dirty state;
   - способ доставки в Telegram.

## Ограничения

- Не переносить изменения между ветками только ради сборки.
- Не коммитить APK, sandbox, `Library/`, локальные secrets или Telegram config.
- Не печатать и не искать Telegram secrets.
- Не удалять warm sandbox/cache без явной причины.
- Если Telegram-публикация недоступна, остановись после успешной сборки и сообщи локальный путь к APK.
