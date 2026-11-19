using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace LastExit.Platform
{
    // Presentation language only; level identifiers and saved campaign data stay locale independent.
    public static class GameLocalization
    {
        private const string LanguagePreference = "last_exit.language";
        private static readonly Dictionary<string, string[]> s_strings = new Dictionary<string, string[]>
        {
            { "terminal.counts", new[] { "В ОЧЕРЕДЯХ\n<size={2}>{0}</size>\nОСТАЛОСЬ АВТОБУСОВ / {1}",
                "IN QUEUES\n<size={2}>{0}</size>\nBUSES LEFT / {1}" } },
            { "hud.counts", new[] { "В очередях {0} · Осталось автобусов {1}", "In queues {0} · Buses left {1}" } },
            { "hud.compactCounts", new[] { "В очередях: {0}\nАвтобусов: {1}", "In queues: {0}\nBuses left: {1}" } },
            {
                "hud.reject", new[]
                {
                    "Автобус занят или путь перекрыт.\nПопробуйте другой автобус.",
                    "Bus is busy or the path is blocked.\nTry another bus."
                }
            },
            { "hud.nextBus", new[] { "Следующий · {0} · {2} мест", "Next bus · {0} · {2} seats" } },
            { "hud.paused", new[] { "Пауза", "Paused" } },
            { "hud.stability", new[] { "Стабильность {0} · Потеряно мест: {1}", "Stability {0} · Lost seats {1}" } },
            { "hud.compactStability", new[] { "Стаб. {0} · Мест −{1}", "Stab. {0} · Seats −{1}" } },
            { "result.victory", new[] { "Уровень пройден", "Level complete" } },
            { "result.defeat", new[] { "Эвакуация прервана", "Evacuation interrupted" } },
            { "result.lostSeats", new[] { "Потеряно мест: {0}", "Lost seats: {0}" } },
            { "result.evacuated", new[] { "Все пассажиры эвакуированы", "All passengers evacuated" } },
            { "result.retryReason", new[] { "Стабильность исчерпана. Повторите попытку.", "Stability depleted. Try again." } },
            { "result.campaignDone", new[] { "Уровни 01–15 пройдены\n{0}", "Levels 01–15 complete\n{0}" } },
            { "result.stabilityEmpty", new[] { "Стабильность исчерпана", "Stability depleted" } },
            { "result.echoRemoved", new[] { "ECHO устранён", "ECHO removed" } },
            {
                "result.humanLost", new[]
                {
                    "Ошибка · −1 стабильность · −1 место",
                    "Wrong passenger · −1 stability · −1 seat"
                }
            },
            { "result.compactHumanLost", new[] { "−1 стабильность\n−1 место", "−1 stability\n−1 seat" } },
            { "result.riskAccepted", new[] { "Аварийный сброс: −1 стабильность", "Emergency reset: −1 stability" } },
            { "result.eyebrow", new[] { "СТАНЦИЯ L-07 / ЭВАКУАЦИЯ", "STATION L-07 / EVACUATION" } },
            { "result.ratingBest", new[] { "Высшая оценка", "Perfect evacuation" } },
            { "result.ratingGood", new[] { "Хороший результат", "Well done" } },
            { "result.ratingComplete", new[] { "Эвакуация завершена", "Evacuation complete" } },
            { "action.restart", new[] { "Заново", "Restart" } },
            { "action.repeat", new[] { "Повторить", "Retry" } },
            { "action.next", new[] { "Следующий уровень", "Next level" } },
            { "action.continue", new[] { "Продолжить", "Continue" } },
            { "action.cancel", new[] { "Отмена", "Cancel" } },
            { "menu.title", new[] { "LAST EXIT", "LAST EXIT" } },
            { "menu.continue", new[] { "Продолжить", "Continue" } },
            { "menu.levels", new[] { "Выбор уровня", "Select level" } },
            { "menu.settings", new[] { "Настройки", "Settings" } },
            { "menu.back", new[] { "Назад", "Back" } },
            { "menu.resume", new[] { "Вернуться в игру", "Resume game" } },
            { "menu.level", new[] { "Уровень {0:00}", "Level {0:00}" } },
            { "menu.locked", new[] { "Закрыт", "Locked" } },
            {
                "menu.levelHint", new[]
                {
                    "Пройдите предыдущий уровень, чтобы открыть следующий.",
                    "Complete the previous level to unlock the next."
                }
            },
            { "menu.subtitle", new[] { "СТАНЦИЯ L-07 / ЛИМИНАЛЬНЫЙ ПРОТОКОЛ", "STATION L-07 / LIMINAL PROTOCOL" } },
            { "settings.language", new[] { "Язык", "Language" } },
            { "settings.summary", new[] { "Звук и язык", "Sound and language" } },
            { "settings.sound", new[] { "Звук", "Sound" } },
            { "settings.atmosphere", new[] { "Атмосфера", "Ambience" } },
            { "settings.effects", new[] { "Эффекты", "Effects" } },
            { "settings.on", new[] { "Вкл.", "On" } },
            { "settings.off", new[] { "Выкл.", "Off" } },
            { "settings.auto", new[] { "Авто", "Auto" } },
            { "settings.volume", new[] { "Громкость", "Volume" } },
            { "settings.soundEnabled", new[] { "Звук включён", "Sound enabled" } },
            { "settings.muted", new[] { "Тихо", "Muted" } },
            { "bus.class.MINI", new[] { "Мини", "Mini" } },
            { "bus.class.STANDARD", new[] { "Стандарт", "Standard" } },
            { "bus.class.LONG", new[] { "Длинный", "Long" } },
            { "board.quarantine", new[] { "КАРАНТИН", "QUARANTINE" } },
            { "board.blockedRoute", new[] { "! {0} · ДРУГОЙ\nМАРШРУТ", "! {0} · WRONG\nROUTE" } },
            { "board.blockedBody", new[] { "! {0}\nКОРПУС", "! {0}\nBLOCKED" } },
            { "stop.unavailable", new[] { "! Остановка недоступна · будет откат", "! Stop unavailable · will return" } },
            { "stop.aligned", new[] { "Отпустите: дверь и маршрут совпали", "Release: door and route aligned" } },
            { "stop.align", new[] { "Сместите дверь к линии очереди", "Align the door with a queue" } },
            { "stop.blocked", new[] { "! Проход перекрыт автобусом выше", "! A bus above blocks the path" } },
            { "stop.route", new[] { "! Маршрут не совпадает", "! Route does not match" } },
            { "stop.empty", new[] { "Эта очередь пуста", "This queue is empty" } },
            { "stop.full", new[] { "В автобусе нет свободных мест", "No seats available on this bus" } },
            { "security.timer", new[] { "CCTV · скан {0} с", "CCTV · scanning {0} s" } },
            { "security.queued", new[] { "CCTV · запрос принят\nПосле посадки", "CCTV · scan queued\nAfter boarding" } },
            { "security.charges", new[] { "Скан · CCTV\nЗарядов: {0}", "Scan · CCTV\nCharges: {0}" } },
            { "security.quarantine", new[] { "В карантин · {0}", "Quarantine · {0}" } },
            { "security.touch", new[] { "Коснитесь головы", "Tap a head" } },
            { "security.select", new[] { "Карантин: выбрать", "Quarantine: select" } },
            {
                "security.scanning", new[]
                {
                    "Следите за первыми пассажирами. Запомните искажение.",
                    "Watch the queue heads. Remember any distortion."
                }
            },
            {
                "security.choose", new[]
                {
                    "Выберите голову\nпассажира на поле.\nЗатем «В карантин».",
                    "Select a passenger\nhead on the board.\nThen tap Quarantine."
                }
            },
            { "security.confirm", new[] { "Цель: {0}.\nНажмите\n«В карантин».", "Target: {0}.\nTap\nQuarantine." } },
            {
                "security.tutorialAnomaly", new[]
                {
                    "Что-то не так.\nПросканируйте пассажиров.",
                    "Something is wrong.\nScan the passengers."
                }
            },
            {
                "security.tutorialQuarantine", new[]
                {
                    "Кто искажался? Выберите голову и нажмите «В карантин».",
                    "Who distorted? Select their head and tap Quarantine."
                }
            },
            {
                "tutorial.align", new[]
                {
                    "Красный закрывает путь бирюзовым.\nСдвиньте его влево к красной очереди.",
                    "Red blocks the turquoise queue.\nSlide it left to the red queue."
                }
            },
            {
                "tutorial.boarding", new[]
                {
                    "Очереди идут общим тактом.\nСледующая группа ждёт прибытия всех.",
                    "Queues board in shared beats.\nThe next group waits for everyone."
                }
            },
            { "tutorial.departing", new[] { "Полный автобус уезжает", "The full bus is departing" } },
            {
                "tutorial.arrival", new[]
                {
                    "Следующий красный уже на месте.\nСдвиньте его к красной очереди.",
                    "The next red bus has arrived.\nSlide it to the red queue."
                }
            },
            {
                "tutorial.objective05", new[]
                {
                    "CCTV проверяет только головы очередей.\nНовая голова требует нового наблюдения.",
                    "CCTV scans only queue heads.\nA new head needs another inspection."
                }
            },
            {
                "tutorial.objective06", new[]
                {
                    "Следующие автобусы ждут полного выезда.\nОсвободите ряд для общего потока.",
                    "Incoming buses wait for a full exit.\nClear a row for the next arrival."
                }
            },
            {
                "tutorial.objective07", new[]
                {
                    "Теперь четыре ряда. Первый корпус\nперекрывает путь к автобусам ниже.",
                    "There are now four rows. The first bus\nblocks the path to buses below."
                }
            },
            {
                "tutorial.objective08", new[]
                {
                    "Один CCTV проверяет все головы сразу.\nВыбирайте карантин по наблюдению на поле.",
                    "One CCTV scan checks all queue heads.\nChoose quarantine by what you observe."
                }
            },
            {
                "tutorial.objective10", new[]
                {
                    "Длинный: четыре клетки, шесть мест.\nДлинный корпус закрывает больше путей.",
                    "Long bus: four cells, six seats.\nA longer body blocks more paths."
                }
            },
            {
                "tutorial.objective13", new[]
                {
                    "Теперь пять рядов. Размер автобуса\nне закреплён за номером ряда.",
                    "There are now five rows. Bus size\nis independent of the row number."
                }
            },
        };

        public static event Action Changed;

        public static string LanguageCode { get; private set; } = "ru";

        public static void Initialize(string sdkLanguage)
        {
            string saved = PlayerPrefs.GetString(LanguagePreference, string.Empty);
            string language = saved == "ru" || saved == "en" ? saved : Normalize(sdkLanguage);
            ApplyLanguage(language);
        }

        public static void SelectLanguage(string code)
        {
            string language = Normalize(code);
            PlayerPrefs.SetString(LanguagePreference, language);
            PlayerPrefs.Save();
            ApplyLanguage(language);
        }

        public static string Get(string key, params object[] args)
        {
            if (string.IsNullOrEmpty(key))
            {
                return string.Empty;
            }

            if (s_strings.TryGetValue(key, out string[] translations) == false)
            {
                return key;
            }

            string text = translations[LanguageCode == "ru" ? 0 : 1];
            CultureInfo culture = CultureInfo.GetCultureInfo(LanguageCode);
            return args == null || args.Length == 0 ? text : string.Format(culture, text, args);
        }

        public static string FormatLevel(string id)
        {
            const string Prefix = "LEVEL-";

            if (id != null && id.StartsWith(Prefix, StringComparison.Ordinal) &&
                int.TryParse(id.Substring(Prefix.Length), NumberStyles.None, CultureInfo.InvariantCulture,
                    out int number))
            {
                return Get("menu.level", number);
            }

            return id ?? string.Empty;
        }

        private static string Normalize(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                return "ru";
            }

            string normalized = code.Trim().Replace('_', '-');
            return normalized.Equals("ru", StringComparison.OrdinalIgnoreCase) ||
                normalized.StartsWith("ru-", StringComparison.OrdinalIgnoreCase) ? "ru" : "en";
        }

        private static void ApplyLanguage(string language)
        {
            if (LanguageCode == language)
            {
                return;
            }

            LanguageCode = language;
            Changed?.Invoke();
        }
    }
}
