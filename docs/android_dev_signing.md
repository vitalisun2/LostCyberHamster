# Android dev signing

Тема: единая подпись Android development APK для локальных и Telegram-сборок LostCyberHamster.

## Зачем

Android обновляет установленное приложение только если новый APK имеет тот же `applicationId` и подписан тем же сертификатом. Если разные ноутбуки собирают `com.vues.LostCyberHamster` разными debug keystore, APK конфликтуют друг с другом и не ставятся поверх старой версии.

Решение для тестовых билдов: все сборочные стенды подписывают Android development APK одним общим dev/test keystore. Это не release-ключ.

## Где лежит локальная настройка

На каждом ноутбуке:

```text
%USERPROFILE%\.lostcyberhamster\android-dev-signing\
  LostCyberHamster-dev.keystore
  signing.local.json
```

`signing.local.json` содержит пароль keystore и пароль alias. Эти файлы нельзя коммитить, печатать в логи или пересказывать в публичной документации.

Схема `signing.local.json`:

```json
{
  "keystorePath": "LostCyberHamster-dev.keystore",
  "keystorePass": "<secret>",
  "keyaliasName": "lostcyberhamster-dev",
  "keyaliasPass": "<secret>",
  "certificateSha256": "<sha256-without-colons>"
}
```

`keystorePath` может быть относительным к `signing.local.json`; это делает transfer package переносимым между ноутбуками.

## Как использует build pipeline

`tools/build/build_android_telegram.ps1` по умолчанию читает:

```text
%USERPROFILE%\.lostcyberhamster\android-dev-signing\signing.local.json
```

При необходимости путь можно переопределить:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\build\build_android_telegram.ps1 `
  -AndroidSigningConfigPath "D:\secure\signing.local.json" `
  -Development
```

Если signing config или keystore отсутствуют, Android build должен падать до создания APK. Это намеренно: случайная подпись локальным Unity/Android debug keystore снова сломает обновления на телефонах.

Build summary и build manifest содержат только non-secret metadata: alias и SHA-256 сертификата.

## Проверка текущего ноутбука

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\build\install_android_dev_signing.ps1
```

Скрипт проверит наличие локального `signing.local.json`, keystore, alias и SHA-256 сертификата. Пароли не печатаются.

## Перенос на другой ноутбук

Передается архив вида:

```text
LostCyberHamster-dev-signing.zip
  LostCyberHamster-dev.keystore
  signing.local.json
  README-install-signing.md
```

На другом ноутбуке:

1. Распаковать архив в временную папку вне репозитория.
2. Из корня репозитория выполнить:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\build\install_android_dev_signing.ps1 `
  -PackageRoot "<path-to-unpacked-package>"
```

3. Убедиться, что скрипт вывел тот же `certificateSha256`.
4. После этого обычные Android dev builds через `tools/build/build_android_telegram.ps1` будут подписываться общим dev keystore.

Если на телефоне уже установлен билд, подписанный другим сертификатом, Android не позволит обновиться поверх него. Для перехода на общий dev keystore нужно один раз удалить старое приложение с телефона и установить новый APK. После этого следующие APK с любого настроенного ноутбука будут ставиться поверх.

## Текущий общий dev certificate

SHA-256 сертификата фиксируется в локальном `signing.local.json` и в transfer package. При публикации APK в Telegram fingerprint можно сверять по `build-summary.codex.json` или manifest metadata.
