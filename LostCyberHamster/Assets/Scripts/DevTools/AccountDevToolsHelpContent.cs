#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;

namespace Assets.Scripts.DevTools
{
    /// <summary>
    /// Хранит обучающие материалы для ручной проверки account flow в dev-меню.
    /// </summary>
    internal static class AccountDevToolsHelpContent
    {
        private static readonly string[] _titles =
        {
            "Цель проверки",
            "Пошаговый сценарий",
            "Что делают кнопки",
            "Подготовка Unity Editor",
            "Подготовка Android",
            "Ограничения и риски"
        };

        private static readonly string[] _texts =
        {
            "Account flow связывает временного UGS-гостя с долговечной внешней учётной записью. " +
            "Текущий MVP сохраняет identity после перезапуска на этом устройстве и закладывает основу для будущего восстановления после переустановки или на другом устройстве.\n\n" +
            "Ручная проверка должна доказать именно жизненный цикл identity: кто сейчас авторизован, сохраняется ли Player ID и корректно ли обрабатываются ошибки. " +
            "Identity сама по себе ещё не гарантирует перенос игровых данных.\n\n" +
            "Что проверяем:\n" +
            "1. Тихий guest: игра запускается без обязательного окна входа и создаёт либо восстанавливает UGS-сессию.\n\n" +
            "2. Link: новый Unity Player Account привязывается к тому же Player ID.\n\n" +
            "3. Restart: после повторного запуска восстанавливаются тот же Player ID и linked-state.\n\n" +
            "4. Ошибки и отмена: UI не падает, не зависает навсегда и позволяет повторить действие.\n\n" +
            "5. Конфликт: аккаунт, уже связанный с другим Player ID, не должен молча переключать текущего игрока.",

            "Шаг 1 — открой «Управление сессиями» и нажми «Создать / восстановить гостевую сессию».\n" +
            "Зачем: проверить cached identity или создать гостя.\n" +
            "Ожидаем: SignedIn = Да, Player ID заполнен, State = Guest или Linked.\n\n" +
            "Шаг 2 — нажать «Обновить статус».\n" +
            "Зачем: запросить у backend актуальные external identities.\n" +
            "Ожидаем: State и Linked обновились, ошибка пуста.\n\n" +
            "Шаг 3 — нажать «Привязать Unity Player Account» и пройти браузерный вход.\n" +
            "Зачем: сделать текущий Player ID восстанавливаемым.\n" +
            "Ожидаем: при новом аккаунте Player ID не меняется, Linked = Да. При конфликте операция останавливается с AlreadyLinked.\n\n" +
            "Шаг 4 — остановить и снова запустить Play Mode.\n" +
            "Зачем: проверить cached UGS session.\n" +
            "Ожидаем: восстановлен тот же Player ID.\n\n" +
            "Шаг 5 — отдельно проверить offline/закрытие браузера.\n" +
            "Ожидаем: понятная ошибка и возможность повторить действие; вечное ожидание считается багом.",

            "«Создать / восстановить гостевую сессию» — восстанавливает cached UGS player; если кэша нет, создаёт нового гостя.\n\n" +
            "«Обновить статус» — перечитывает linked-state без изменения identity.\n\n" +
            "«Привязать Unity Player Account» — link: добавляет внешний способ входа к текущему Player ID. При AlreadyLinked не переключает игрока.\n\n" +
            "«Отвязать Unity Player Account» — unlink: удаляет внешний способ восстановления у текущего Player ID, но не удаляет сам UGS player.\n\n" +
            "«Выйти из UGS» — завершает активную UGS-сессию, сохраняя cached identity для следующего входа.\n\n" +
            "«Выйти из UPA» — очищает локальную OAuth-сессию Player Accounts; UGS Player ID и link не меняются.\n\n" +
            "«Очистить данные входа на устройстве» — удаляет локальные UGS credentials. Следующий guest-вход создаст новый Player ID. Игровые данные этим не очищаются.",

            "1. Открой проект с правильным cloudProjectId. Локальная готовность показана вверху account-экрана.\n\n" +
            "2. В Unity Dashboard включи Authentication.\n\n" +
            "3. Добавь identity provider «Unity Player Accounts» и включи для него платформу PC — без неё браузерный callback в Unity Editor не работает.\n\n" +
            "4. Проверь локальный UnityPlayerAccountSettings: clientId должен быть заполнен.\n\n" +
            "5. Открой Assets/Scenes/Bootstrap.unity и нажми Play. В меню открой DEV → Аккаунт.\n\n" +
            "Runtime не может безопасно изменить Dashboard: это административная настройка. Кнопка Dashboard открывает страницу Unity Cloud, дальше выбери текущий проект → Authentication → Identity providers.",

            "1. В Unity Player Accounts provider отдельно включи платформу Android. PC-настройка не заменяет Android-настройку.\n\n" +
            "2. Проверь, что application identifier билда соответствует проекту.\n\n" +
            "3. При стандартной конфигурации custom redirect обычно не нужен: Unity использует unitydl://com.unityplayeraccounts.{projectId}.\n\n" +
            "4. Это не отменяет Dashboard/platform config: deep link только возвращает OAuth callback в приложение.\n\n" +
            "5. Проверяй на development build: открытие браузера, возврат в приложение, pause/resume, offline и повторный запуск.",

            "Текущий MVP управляет identity, а не полным жизненным циклом PlayerData. Надпись Linked не доказывает, что свежий прогресс уже выгружен в Cloud Save.\n\n" +
            "Конфликт AlreadyLinked теперь безопасно останавливает link и не меняет Player ID. Вход в существующий аккаунт появится отдельным flow только вместе с reload/merge PlayerData.\n\n" +
            "Unlink может удалить единственный способ восстановить игрока. После очистки cached identity или переустановки доступ к нему может быть потерян.\n\n" +
            "Очистка cached identity не очищает локальный PlayerData. Для чистого сценария сбрасывай прогресс отдельной кнопкой осознанно. Не используй ценные реальные аккаунты для destructive-проверок.\n\n" +
            "Если браузер закрыт без callback, текущий SDK-flow может зависнуть: это известный открытый риск, а не успешный результат теста."
        };

        public static int SectionCount => _titles.Length;

        /// <summary>
        /// Возвращает заголовок раздела справки.
        /// </summary>
        public static string GetTitle(int index)
        {
            ValidateIndex(index);
            return _titles[index];
        }

        /// <summary>
        /// Возвращает обучающий текст раздела справки.
        /// </summary>
        public static string GetText(int index)
        {
            ValidateIndex(index);
            return _texts[index];
        }

        private static void ValidateIndex(int index)
        {
            if (index < 0 || index >= _titles.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }
        }
    }
}
#endif
