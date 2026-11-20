# LAST EXIT — Liminal Protocol

Головоломка об эвакуации пассажиров со станции. Кампания состоит из 15 уровней. Управление — мышью или касанием.

## Запуск в Unity

1. Скачайте эту ветку через **Code → Download ZIP** и распакуйте архив. Для клонирования выполните команду ниже.
2. В Unity Hub установите **Unity 2022.3.62f2**. В разделе **Projects → Add project from disk** выберите папку, в которой лежат `Assets`, `Packages` и `ProjectSettings`.
3. Дождитесь первого импорта и восстановления пакетов. Откройте `Assets/_LAST_EXIT/Scenes/StationL07.unity`, нажмите **Play** в Editor и **Продолжить** в меню игры.

```sh
git clone --single-branch --branch acceptance/unity-project https://github.com/zzMountain/LAST-EXIT-Liminal-Protocol.git
```

На первом уровне перетащите автобус по горизонтали и совместите его дверь с очередью нужного маршрута. Дождитесь посадки пассажиров. С четвёртого уровня появляются ECHO и ручной карантин.

## Где искать код

| Папка | Содержимое |
|---|---|
| `Assets/_LAST_EXIT/Runtime/Domain` | Правила игры, состояние поля и посадка |
| `Assets/_LAST_EXIT/Runtime/Content` | Чтение уровней и выбор вариантов |
| `Assets/_LAST_EXIT/Runtime/Presentation` | Управление автобусом, сцена, анимация, интерфейс и звук |
| `Assets/_LAST_EXIT/Runtime/Platform` | Прогресс кампании, язык и интеграция с Яндекс Играми |
| `Assets/_LAST_EXIT/Resources` | 15 уровней, 48 вариантов раскладки и шрифты интерфейса |
| `Assets/_LAST_EXIT/StationVisualsAndComponents` | Оформление станции (`Rendering`), настройки интерфейса (`UI`), компоненты игрового ввода и размещения интерфейса (`Preview`) |

Модели станции и транспорта созданы для проекта. В игре используются шрифты Inter и JetBrains Mono, 15 процедурных звуков и 5 обработанных клипов Stable Audio. Сведения об источниках звука и лицензии находятся в `Assets/_LAST_EXIT/Audio`, лицензии шрифтов — в `Assets/_LAST_EXIT/Art/Fonts` и `Assets/_LAST_EXIT/Resources/UI`.
