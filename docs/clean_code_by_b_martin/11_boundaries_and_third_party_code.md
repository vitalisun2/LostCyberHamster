# Границы со сторонним и платформенным кодом

## Концепция

Внешний API не должен расползаться по доменной логике. Unity API, Addressables, PlayerPrefs, файловая система, плагины и сторонние библиотеки лучше держать за тонкими адаптерами, чтобы остальной код зависел от понятного контракта проекта.

## Когда читать

- Интеграция Unity API или сторонней библиотеки.
- Работа с Addressables, JSON, файловой системой, PlayerPrefs.
- Код проекта начинает зависеть от деталей внешнего API.

## Правила

- Изолируй внешний API в adapter/gateway/service.
- Доменный код должен видеть интерфейс проекта, а не детали библиотеки.
- Проверяй незнакомый API маленьким локальным экспериментом, прежде чем строить на нем систему.
- Не протаскивай платформенные типы глубже, чем нужно.
- На границе преобразуй ошибки и `null` во внутренний контракт проекта.

## Пример

Плохо: доменная логика напрямую знает про `PlayerPrefs`.

```csharp
public bool HasCompletedTutorial()
{
    return PlayerPrefs.GetInt("tutorial_completed", 0) == 1;
}
```

Лучше: внешний API спрятан за контрактом.

```csharp
public interface IPlayerProgressStore
{
    bool HasCompletedTutorial();
}

public sealed class PlayerPrefsProgressStore : IPlayerProgressStore
{
    private const string TutorialCompletedKey = "tutorial_completed";

    public bool HasCompletedTutorial()
    {
        return PlayerPrefs.GetInt(TutorialCompletedKey, 0) == 1;
    }
}
```

## Правило для агента

Если новый код импортирует внешний namespace в domain/use-case слой, остановись и проверь, не нужна ли граница-адаптер.
