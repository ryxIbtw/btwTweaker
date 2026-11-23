using System;
using System.Collections.Generic;

namespace BtwTweaker
{
    // Вариант значения: набор «настройка драйвера → значение»
    public class NvOption
    {
        public string Label;
        public Dictionary<uint, uint> Set = new Dictionary<uint, uint>();
        public NvOption(string label, params uint[] pairs)
        {
            Label = label;
            for (int i = 0; i + 1 < pairs.Length; i += 2) Set[pairs[i]] = pairs[i + 1];
        }
    }

    // Одна настройка раздела «Панель NVIDIA»
    public class NvItem
    {
        public string Title;
        public string Description;
        public List<NvOption> Options = new List<NvOption>();   // выбор из списка
        public uint NumberId;                                   // либо число (ограничение FPS)
        public string Unit;
        public int Optimal = -1;                                // индекс варианта для «Оптимальное» (-1 — не трогать)
        public bool IsNumber { get { return NumberId != 0; } }

        public IEnumerable<uint> Ids()
        {
            if (IsNumber) { yield return NumberId; yield break; }
            var seen = new HashSet<uint>();
            foreach (var o in Options) foreach (var id in o.Set.Keys) if (seen.Add(id)) yield return id;
        }

        public NvItem Opt(string label, params uint[] pairs) { Options.Add(new NvOption(label, pairs)); return this; }
    }

    public class NvGroup
    {
        public string Title;
        public List<NvItem> Items = new List<NvItem>();
        public NvGroup(string t, params NvItem[] items) { Title = t; Items.AddRange(items); }
    }

    public static class NvCatalog
    {
        // ID настроек драйвера (NvApiDriverSettings.h / NVIDIA Profile Inspector)
        // Номера сверены с драйвером (NvAPI_DRS_GetSettingNameFromId): «Antialiasing - Mode» = 0x107EFC5B,
        // «Frame Rate Limiter» = 0x10835002. Скрытые (без имени в драйвере): FPS_BG, ULL_CPL, ULL, CUDA_P2.
        public const uint FXAA = 0x1074C972, GAMMA = 0x107D639D, MFAA = 0x0098C1AC, REPLAY = 0x10D48A85, AA_SEL = 0x107EFC5B,
            FPS_BG = 0x10835005, TRIPLE = 0x20FDD1F9, REFRESH = 0x0064B541, FPS = 0x10835002, VSYNC = 0x00A879CF,
            ULL_CPL = 0x0005F543, ULL = 0x10835000, PRERENDER = 0x007BA09E,
            CUDA_P2 = 0x50166C5E, PSTATE = 0x1057EB71, THREADED = 0x20C1221E,
            ANISO_SEL = 0x10D2BB16, ANISO_LVL = 0x101E61A9, QUALITY = 0x00CE2691, TRILIN = 0x002ECAF2, ANISO_OPT = 0x00E73211,
            AO = 0x00667329;

        // Значения драйвера, если настройка не задана в профиле
        public static readonly Dictionary<uint, uint> Defaults = new Dictionary<uint, uint>
        {
            { FXAA, 0 }, { GAMMA, 2 }, { MFAA, 0 }, { REPLAY, 0 }, { AA_SEL, 0 },
            { FPS_BG, 0 }, { TRIPLE, 0 }, { REFRESH, 0 }, { FPS, 0 }, { VSYNC, 0x60925292 },
            { ULL_CPL, 0 }, { ULL, 0 }, { PRERENDER, 0 },
            { CUDA_P2, 1 }, { PSTATE, 5 }, { THREADED, 0 },
            { ANISO_SEL, 0 }, { ANISO_LVL, 1 }, { QUALITY, 0 }, { TRILIN, 0 }, { ANISO_OPT, 0 }, { AO, 0 },
        };

        public static List<NvGroup> Build()
        {
            return new List<NvGroup>
            {
                new NvGroup(L.T("Сглаживание и резкость"),
                    new NvItem { Title = L.T("Сглаживание — FXAA"), Description = L.T("Быстрое сглаживание лесенок постобработкой во всех играх. Почти не нагружает видеокарту, но слегка размывает картинку и текст. Лучше включать сглаживание в самой игре."), Optimal = 0 }
                        .Opt(L.T("Откл"), FXAA, 0).Opt(L.T("Вкл"), FXAA, 1),
                    new NvItem { Title = L.T("Сглаживание — гамма-коррекция"), Description = L.T("Учитывает гамму при сглаживании (MSAA): края выглядят ровнее. Влияет только на игры с MSAA.") }
                        .Opt(L.T("Откл"), GAMMA, 0).Opt(L.T("Вкл"), GAMMA, 2),
                    new NvItem { Title = L.T("Сглаживание — MFAA"), Description = L.T("Multi-Frame Sampled AA: чередует шаблоны сэмплов между кадрами, чтобы получить качество 4x MSAA по цене 2x. Работает только вместе с MSAA в игре."), Optimal = 0 }
                        .Opt(L.T("Откл"), MFAA, 0).Opt(L.T("Вкл"), MFAA, 1),
                    new NvItem { Title = L.T("Сглаживание прозрачных текстур"), Description = L.T("Сглаживание листвы, решёток и других прозрачных текстур. Суперсэмплинг заметно нагружает видеокарту.") }
                        .Opt(L.T("Выключено / мультисэмплинг"), REPLAY, 0).Opt(L.T("Суперсэмплинг 2x"), REPLAY, 0x18).Opt(L.T("Суперсэмплинг 4x"), REPLAY, 0x28).Opt(L.T("Суперсэмплинг 8x"), REPLAY, 0x38),
                    new NvItem { Title = L.T("Сглаживание (MSAA) — режим"), Description = L.T("Кто управляет MSAA: игра или драйвер. «Переопределить» навязывает сглаживание драйвера, «Улучшить» дополняет сглаживание игры.") }
                        .Opt(L.T("Управляется приложением"), AA_SEL, 0).Opt(L.T("Переопределить"), AA_SEL, 1).Opt(L.T("Улучшить"), AA_SEL, 2)),

                new NvGroup(L.T("Синхронизация и частота кадров"),
                    new NvItem { Title = L.T("Ограничение FPS для фоновых приложений"), Description = L.T("Максимум кадров в секунду для игр и программ, когда их окно не активно (например, свернули игру). 0 — без ограничения."), NumberId = FPS_BG, Unit = "FPS" },
                    new NvItem { Title = L.T("Тройная буферизация"), Description = L.T("Третий буфер кадров для OpenGL при включённой вертикальной синхронизации: меньше просадок FPS, но больше задержка ввода."), Optimal = 0 }
                        .Opt(L.T("Откл"), TRIPLE, 0).Opt(L.T("Вкл"), TRIPLE, 1),
                    new NvItem { Title = L.T("Предпочтительная частота обновления"), Description = L.T("Какую частоту монитора использовать в полноэкранных играх. «Наивысшая доступная» не даёт игре выбрать 60 Гц на мониторе 144/240 Гц."), Optimal = 1 }
                        .Opt(L.T("Управляется приложением"), REFRESH, 0).Opt(L.T("Наивысшая доступная"), REFRESH, 1),
                    new NvItem { Title = L.T("Ограничение FPS"), Description = L.T("Максимум кадров в секунду во всех играх. Ставьте на 2–3 кадра ниже частоты монитора, если используете G-SYNC. 0 — без ограничения."), NumberId = FPS, Unit = "FPS" },
                    new NvItem { Title = L.T("Вертикальная синхронизация"), Description = L.T("Синхронизирует кадры с частотой монитора: нет разрывов, но выше задержка. «Быстрая» убирает разрывы почти без задержки, если FPS сильно выше частоты монитора.") }
                        .Opt(L.T("Как в приложении"), VSYNC, 0x60925292).Opt(L.T("Вкл"), VSYNC, 0x47814940).Opt(L.T("Откл"), VSYNC, 0x08416747).Opt(L.T("Быстрая"), VSYNC, 0x18888888),
                    new NvItem { Title = L.T("Режим низкой задержки"), Description = L.T("Сколько кадров процессор готовит заранее. «Вкл» — один кадр, «Ультра» — кадр отдаётся видеокарте точно к моменту отрисовки. Снижает задержку ввода, когда видеокарта загружена на 100%."), Optimal = 1 }
                        .Opt(L.T("Откл"), ULL_CPL, 0, ULL, 0, PRERENDER, 0).Opt(L.T("Вкл"), ULL_CPL, 1, ULL, 0, PRERENDER, 1).Opt(L.T("Ультра"), ULL_CPL, 2, ULL, 1, PRERENDER, 1)),

                new NvGroup(L.T("Система, память и вычисления"),
                    new NvItem { Title = L.T("CUDA — принудительное состояние P2"), Description = L.T("В CUDA-нагрузках (рендер, нейросети, некоторые игры и программы) драйвер снижает частоту памяти до состояния P2. После отключения память работает на полной частоте."), Optimal = 0 }
                        .Opt(L.T("Откл"), CUDA_P2, 0).Opt(L.T("Вкл"), CUDA_P2, 1),
                    new NvItem { Title = L.T("Управление питанием — режим"), Description = L.T("«Максимальная производительность» не даёт видеокарте сбрасывать частоты между кадрами — меньше микрофризов, но выше энергопотребление в простое."), Optimal = 2 }
                        .Opt(L.T("Оптимальное энергопотребление"), PSTATE, 5).Opt(L.T("Адаптивный"), PSTATE, 0).Opt(L.T("Максимальная производительность"), PSTATE, 1),
                    new NvItem { Title = L.T("Многопоточная оптимизация"), Description = L.T("Распределяет работу драйвера OpenGL по нескольким ядрам процессора. «Авто» — драйвер решает сам.") }
                        .Opt(L.T("Авто"), THREADED, 0).Opt(L.T("Вкл"), THREADED, 1).Opt(L.T("Откл"), THREADED, 2)),

                new NvGroup(L.T("Текстурная фильтрация"),
                    new NvItem { Title = L.T("Анизотропная фильтрация"), Description = L.T("Чёткость текстур под углом (дорога, пол вдаль). Почти не влияет на FPS на современных видеокартах. «Управляется приложением» — уровень задаёт игра.") }
                        .Opt(L.T("Управляется приложением"), ANISO_SEL, 0).Opt(L.T("Откл"), ANISO_SEL, 1, ANISO_LVL, 1).Opt("2x", ANISO_SEL, 1, ANISO_LVL, 2)
                        .Opt("4x", ANISO_SEL, 1, ANISO_LVL, 4).Opt("8x", ANISO_SEL, 1, ANISO_LVL, 8).Opt("16x", ANISO_SEL, 1, ANISO_LVL, 16),
                    new NvItem { Title = L.T("Фильтрация текстур — качество"), Description = L.T("Баланс качества и скорости фильтрации текстур. «Производительность» включает оптимизации драйвера — прирост FPS небольшой, картинка почти не меняется."), Optimal = 2 }
                        .Opt(L.T("Высокое качество"), QUALITY, 0xFFFFFFF6).Opt(L.T("Качество"), QUALITY, 0).Opt(L.T("Производительность"), QUALITY, 10).Opt(L.T("Высокая производительность"), QUALITY, 20),
                    new NvItem { Title = L.T("Фильтрация текстур — трилинейная оптимизация"), Description = L.T("Упрощённая трилинейная фильтрация там, где разница незаметна. Игнорируется при «Высоком качестве»."), Optimal = 0 }
                        .Opt(L.T("Вкл"), TRILIN, 0).Opt(L.T("Откл"), TRILIN, 1),
                    new NvItem { Title = L.T("Анизотропная фильтрация — оптимизация выборки"), Description = L.T("Меньше выборок при анизотропной фильтрации там, где это не видно. Немного быстрее."), Optimal = 1 }
                        .Opt(L.T("Откл"), ANISO_OPT, 0).Opt(L.T("Вкл"), ANISO_OPT, 1)),

                new NvGroup(L.T("Функции рендеринга"),
                    new NvItem { Title = L.T("Затенение фонового освещения (AO)"), Description = L.T("Драйвер добавляет мягкие тени в углах и щелях в поддерживаемых играх. Заметно снижает FPS; большинство современных игр делает это сами."), Optimal = 0 }
                        .Opt(L.T("Откл"), AO, 0).Opt(L.T("Производительность"), AO, 1).Opt(L.T("Качество"), AO, 2).Opt(L.T("Высокое качество"), AO, 3)),
            };
        }

        // Значение с учётом драйвера: заданное в профиле или значение по умолчанию
        public static uint Effective(Dictionary<uint, NvValue> v, uint id)
        {
            NvValue x;
            if (v.TryGetValue(id, out x) && x.Found) return x.Value;
            uint d;
            return Defaults.TryGetValue(id, out d) ? d : 0;
        }

        public static int CurrentIndex(NvItem it, Dictionary<uint, NvValue> v)
        {
            for (int i = 0; i < it.Options.Count; i++)
            {
                bool all = true;
                foreach (var kv in it.Options[i].Set) if (Effective(v, kv.Key) != kv.Value) { all = false; break; }
                if (all) return i;
            }
            return -1;
        }

        // Изменено пользователем (задано в профиле и не совпадает со значением NVIDIA)
        public static bool Edited(NvItem it, Dictionary<uint, NvValue> v)
        {
            foreach (var id in it.Ids())
            {
                NvValue x;
                if (v.TryGetValue(id, out x) && x.Found && !x.Predefined) return true;
            }
            return false;
        }

        public static IEnumerable<uint> AllIds(List<NvGroup> groups)
        {
            foreach (var g in groups) foreach (var it in g.Items) foreach (var id in it.Ids()) yield return id;
        }
    }
}
