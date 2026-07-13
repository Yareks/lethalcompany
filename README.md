# Hunger Bar для Lethal Company

BepInEx-мод, добавляющий справа экрана вертикальную полосу голода. Полная зелёная полоса постепенно опустошается и становится красной. Рядом отображается процент оставшегося голода.

## Установка

1. Установите **BepInEx 5** в папку Lethal Company и один раз запустите игру.
2. Соберите проект или скачайте артефакт `HungerBar.dll` из GitHub Actions.
3. Скопируйте `HungerBar.dll` в `Lethal Company/BepInEx/plugins/HungerBar/`.
4. Запустите игру.

## Настройки

После первого запуска BepInEx создаст файл:

`BepInEx/config/ru.yareks.lethalcompany.hungerbar.cfg`

Параметры:

- `FullDurationMinutes` — за сколько минут полная полоса опустеет (по умолчанию 20);
- `RightOffset` — отступ полосы от правого края;
- `BarHeight` — максимальная высота полосы;
- `ShowPercentage` — показывать процент или нет.

Во время паузы голод не уменьшается. Значение `Current` и метод `Refill(float)` доступны другим модам для добавления еды.

## Сборка

Нужен .NET 8 SDK. Игровые DLL уже находятся в корне репозитория.

```bash
dotnet build HungerBar/HungerBar.csproj -c Release
```

Готовый файл: `HungerBar/bin/Release/HungerBar.dll`.
