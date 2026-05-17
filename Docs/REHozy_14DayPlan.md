# План разработки REHozy на 14 дней

## Вердикт: успеешь ли?

**Короткий ответ:** играбельный цикл «задание → действие → следующее задание → финиш» — **да, если держать MVP-упрощения**. Визуально «как в голове» по всем пунктам — **скорее нет** без переноса части фич на 3-ю неделю.

| Режим | Часы | Результат |
|-------|------|-----------|
| Твой бюджет | **~57 ч** | Все механики в упрощённом виде + UI + звук базово |
| Полный scope (без упрощений) | **~82–95 ч** | Провисающая гирлянда, vertex-грязь, цепочка факелов с балансом, полный SFX |

**Уже готово (не считаем в бюджет):** уборка из воды (гарпун + `HarpoonCargoTrashDrop`), ColorSpread, volumetric fog, сцена с огнём/водой как декор.

**Сильная база для переиспользования:**

- Перенос предмета: [`HarpoonController`](../Assets/REHozy/Scripts/Harpoon/HarpoonController.cs) + [`HarpoonCarryDriver`](../Assets/REHozy/Scripts/Harpoon/HarpoonCarryDriver.cs) + [`HarpoonMountableItem`](../Assets/REHozy/Scripts/Harpoon/HarpoonMountableItem.cs)
- Исчезновение через scale: [`HarpoonCargoTrashDrop`](../Assets/REHozy/Scripts/Harpoon/HarpoonCargoTrashDrop.cs) — шаблон для **луж**
- Милестоуны мира: [`ColorSpreadController.SetStep`](../Assets/REHozy/Scripts/Rendering/ColorSpread/ColorSpreadController.cs)

**Квесты:** заготовка в **другом проекте** — заложить **день 1 (2–3 ч)** на перенос/адаптацию, иначе всё остальное нечем вести по очереди.

---

## Бюджет времени

```mermaid
gantt
    title REHozy 14 дней (~57ч)
    dateFormat YYYY-MM-DD
    section Неделя1
    Квесты_UI_база     :2026-05-18, 3d
    Лужи_огонь         :2026-05-21, 2d
    Выходные_грязь_граффити_пропсы :2026-05-23, 2d
    section Неделя2
    Факелы_растения    :2026-05-26, 3d
    Гирлянда_звук_финиш :2026-05-29, 4d
```

- **Пн–Пт:** 2,5 ч/день × 10 = **25 ч**
- **Сб–Вс:** 8 ч/день × 4 = **32 ч**
- **Итого: 57 ч**

---

## Архитектура (сделать в первые 3–4 часа)

Единый контракт для всех «уборок»:

```csharp
// Assets/REHozy/Scripts/Gameplay/IWorldTask.cs (новое)
interface IWorldTask {
    string TaskId { get; }
    bool IsComplete { get; }
    void OnInteract(InteractionContext ctx); // ray, held tool, click count
}
```

- [ ] **`IWorldTask`** + `InteractionContext` в `Assets/REHozy/Scripts/Gameplay/`
- [ ] **`QuestManager`** — последовательная активация `ScriptableObject` заданий; при `IsComplete` → следующее + опционально `ColorSpreadController.SetStep`
- [ ] **`PlayerToolMode`** — enum: Harpoon / Brush / Water / FlameCarrier / PropPlacement / Garland — переключение по активному квесту
- [ ] **События:** `UnityEvent` на завершении (для SFX и UI)

Это сэкономит **8–12 ч** по сравнению с отдельной логикой на каждую механику.

---

## Поочерёдный список задач

### Фаза 0 — Уже сделано

- [x] Уборка предметов из воды (гарпун → мусорка → shrink)
- [x] ColorSpread (появление цветов)
- [x] Volumetric fog + постобработка сцены

### Фаза 1 — Скелет игры (дни 1–3, ~8 ч)

- [ ] **Перенос квестовой заготовки** из другого проекта → `QuestDefinition` SO + `QuestManager` + триггеры завершения
- [ ] **UI:** панель текущего задания, иконка, опциональный прогресс (3/5 кликов); Canvas + TextMeshPro
- [ ] **Кнопки** «Закончить» / «Начать заново» → `SceneManager.LoadScene` + сброс SO/PlayerPrefs
- [ ] Первые 2–3 квеста-заглушки в данных (без логики механик) для проверки цепочки

### Фаза 2 — Быстрые победы (дни 3–5, ~8 ч)

- [ ] **Лужи:** `PuddleTask` — N кликов ЛКМ → lerp scale к 0 → `Destroy` (копия логики shrink из `HarpoonCargoTrashDrop`, без физики)
- [ ] **Тушение огня:** raycast + «полив» (зажатие или клики) → уменьшение `ParticleSystem` / scale корня [`VFX_GroundFire_Circle`](../Assets/VFXPACK_FIRE_WALLCOEUR/Prefab/VFX_GroundFire_Circle.prefab) → stop emitters → collider off → квест complete
- [ ] Привязать лужи и тушение к квестам и SFX-заглушкам

### Фаза 3 — Выходные 1 (дни 6–7, ~16 ч)

- [ ] **Общий PaintSystem** (RenderTexture mask 512–1024, кисть в UV/world XZ)
- [ ] **Грязь (MVP):** mask + lerp albedo на материале земли
- [ ] **Графити:** та же RT на плоскости стены (отдельный UV-проектор); «стирание» = уменьшение `_Mask` / clip
- [ ] *(опционально, если останется 4+ ч)* Vertex-displacement грязи как в SnowVertex — отложить
- [ ] **Пропсы из коробки:** `PropSpawnBox` → клик по коробке спавнит prefab с `HarpoonMountableItem` → режим гарпуна → ЛКМ ставит на слой Ground (raycast) → можно снова поднять; отдельный layer `Placeable`

### Фаза 4 — Огонь и растения (дни 8–10, ~12 ч)

- [ ] **Костёр + факелы:** клики разжигают костёр → на кончик гарпуна/сокет крепится `FlameCarrier` (свет + VFX) → поднесение к факелу → передача огня; **скорость** `HarpoonCarryDriver` / delta position > порог → сброс огня
- [ ] **Саженцы:** коробка → в руку (как проп) → клик по `PlantSlot` → фиксация; полив M кликов → `Coroutine` scale + optional color lerp

### Фаза 5 — Гирлянда + полировка (дни 11–14, ~13 ч)

- [ ] **Гирлянда (MVP):** из коробки → режим расстановки → клики по `GarlandAnchor` → **LineRenderer / chain of prefab segments**; провисание — **катенария в C#** (sin-дуга между якорями), не физика верёвки
- [ ] **Звуки:** AudioMixer, 1 OneShot на тип действия; фоновый ambient loop на `AudioSource` 2D
- [ ] **Интеграция ColorSpread:** ключевые квесты вызывают `SetStep(step, worldOrigin)`
- [ ] **Playtest-день (воскресенье 2):** баги, порядок квестов, блокеры гарпуна

---

## Календарь по дням

| ✓ | День | Часы | Фокус |
|---|------|------|-------|
| [ ] | Пн 18.05 | 2,5 | Перенос квестов + `IWorldTask` + QuestManager |
| [ ] | Вт 19.05 | 2,5 | UI заданий + Finish/Restart |
| [ ] | Ср 20.05 | 2,5 | Лужи + квесты на лужи |
| [ ] | Чт 21.05 | 2,5 | Тушение огня (начало) |
| [ ] | Пт 22.05 | 2,5 | Тушение огня (конец) + SFX заглушки |
| [ ] | **Сб 23.05** | **8** | PaintSystem + грязь (mask MVP) |
| [ ] | **Вс 24.05** | **8** | Графити + пропсы из коробки |
| [ ] | Пн 25.05 | 2,5 | Факелы: разжигание костра |
| [ ] | Вт 26.05 | 2,5 | Факелы: перенос пламени + гашение от скорости |
| [ ] | Ср 27.05 | 2,5 | Саженцы: посадка + полив + рост |
| [ ] | Чт 28.05 | 2,5 | Гирлянда: якоря + LineRenderer |
| [ ] | Пт 29.05 | 2,5 | Звуки действий + ambient |
| [ ] | **Сб 30.05** | **8** | Все квесты в цепочке + ColorSpread hooks |
| [ ] | **Вс 31.05** | **8** | Playtest, фиксы, optional vertex-грязь |

---

## Подводные камни

### Технические

- **Конфликт гарпуна и других инструментов** — без `PlayerToolMode` клики уйдут в `HarpoonInputHandler` ([`HarpoonInputHandler.cs`](../Assets/REHozy/Scripts/Harpoon/HarpoonInputHandler.cs)); нужен ранний gate по активному квесту.
- **Грязь как SnowVertex** — deform map + temporal lerp + UV на больших мешах: легко **съесть 15–20 ч**; mask-first обязателен для срока.
- **Графити на стенах** — world-space paint на вертикальных поверхностях сложнее XZ-земли; проще отдельный mesh «плакат» с UV 0–1.
- **RenderTexture** — разрешение, фильтр, `Graphics.Blit` каждый кадр при зажатой кнопке → профилировать на целевой GPU.
- **Огонь VFX** — частицы не всегда гаснут от `scale`; надёжнее `StopEmitting()` + отключить lights/scripts.
- **Цепочка факелов** — edge cases: смена инструмента mid-carry, смерть пламени при `StartReturnHome` гарпуна.
- **Гирлянда с провисанием** — физика (Rope/Obi) **не влезает** в 57 ч; только процедурная дуга или префаб-сегменты.
- **Пропсы** — коллайдеры при переносе, Z-fighting на земле; snap по normal raycast.
- **Квесты из другого проекта** — несовпадение assembly/URP/Input System; заложить время на адаптацию namespaces.

### Процессные

- **Звуки «на всё»** — запись/поиск ассетов часто дольше кода; купить пак SFX в день 12, не в день 1.
- **Сцена SampleScene** — разрастается; дублировать сцену `SampleScene_Gameplay` для экспериментов.
- **Нет автотестов** — регрессии при правке гарпуна; держать чеклист из 10 пунктов на воскресенье.

### Что резать первым, если отстаёшь

1. Vertex-displacement грязи → оставить mask
2. Провисание гирлянды → прямая линия между точками
3. Сложная мини-игра скорости факела → фиксированный таймер «огонь живёт 8 сек»
4. Второй/третий графити → один объект
5. Полировка SFX → 3–4 общих звука вместо уникальных на действие

---

## Распределение часов (целевые 57 ч)

| Задача | Часы |
|--------|------|
| Квесты (перенос + адаптация) + UI + Finish/Restart | 11 |
| Лужи | 2 |
| Тушение огня | 6 |
| PaintSystem + грязь (mask) | 10 |
| Графити (reuse) | 4 |
| Пропсы (коробка + гарпун) | 5 |
| Факелы | 8 |
| Растения | 6 |
| Гирлянда MVP | 5 |
| Звуки + ambient | 5 |
| Интеграция ColorSpread + playtest buffer | 5 |

---

## Диаграмма потока игрока

```mermaid
flowchart LR
    QuestUI[QuestUI] --> QuestMgr[QuestManager]
    QuestMgr --> ToolMode[PlayerToolMode]
    ToolMode --> Harpoon[Harpoon]
    ToolMode --> Paint[PaintSystem]
    ToolMode --> Water[WaterExtinguish]
    ToolMode --> Flame[FlameCarrier]
    ToolMode --> Place[PropPlacement]
    ToolMode --> Garland[GarlandPlacer]
    Harpoon --> Complete[TaskComplete]
    Paint --> Complete
    Water --> Complete
    Flame --> Complete
    Place --> Complete
    Garland --> Complete
    Complete --> QuestMgr
    Complete --> ColorSpread[ColorSpreadController]
```

---

## Следующий шаг

Начать с **фазы 1** (квесты + UI), не с гирлянды.
