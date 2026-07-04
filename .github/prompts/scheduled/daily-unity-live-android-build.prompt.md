---
description: "Ежедневно обновить integration/unity-live, собрать Android APK и отправить билд в Telegram."
name: "Daily Unity Live Android Build"
agent: "agent"
argument-hint: "Без аргументов; используется scheduled automation"
---

# Daily Unity Live Android Build

Этот prompt используется Codex scheduled automation для ежедневной сборки актуальной `integration/unity-live` и публикации Android APK в Telegram.

## Цель

Раз в сутки собрать Android development APK из свежей `integration/unity-live` на локальном build-laptop и отправить его в Telegram-канал билдов.

## Маршрут

1. Работай в основном checkout проекта:

   ```text
   C:\Main\crystal_wave\LostCyberHamster_2025
   ```

2. Проверь git state. Если есть незакоммиченные изменения, не затирай их и не делай destructive checkout. Сообщи блокер, если они мешают безопасно обновить `integration/unity-live`.
3. Перейди на `integration/unity-live`.
4. Выполни `git fetch origin` и `git pull --ff-only origin integration/unity-live`.
5. После обновления выполни универсальный build prompt:

   ```text
   .github/prompts/builds/publish-android-build-to-telegram.prompt.md
   ```

   Явный source для него:

   ```text
   SourceWorktree = C:\Main\crystal_wave\LostCyberHamster_2025
   BuildLabel = unity-live-daily
   ```

6. В финальном отчете automation укажи:
   - успешно ли собран и отправлен билд;
   - APK path;
   - `buildId`;
   - commit `integration/unity-live`;
   - dirty state;
   - ошибки или блокеры, если они были.

## Ограничения

- Эта automation должна запускаться только на одном primary build-laptop, чтобы не дублировать ежедневные билды.
- Не использовать второй ноутбук как параллельный daily builder без отдельного явного решения.
- Не коммитить изменения, созданные build pipeline.
- Не менять расписание или настройки automation изнутри scheduled run.
