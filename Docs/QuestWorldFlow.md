# Появление предметов по квестам (Quest Model)

Всё настраивается в **Quest Model → Quests Events** — в каждом элементе списка, рядом с `On Start` / `On Finish`.

Анимация scale: [`QuestWorldScaleTransition`](../Assets/REHozy/Quests/QuestWorldScaleTransition.cs) (добавляется на объект автоматически).

## Поля в Quest State Info

| Блок | Поля |
|------|------|
| **World On Start** | `Show` — показать при старте квеста; `Hide` — скрыть; `Switch Tool Mode` + режим |
| **World On Finish** | то же при завершении |
| **On Start / On Finish** | UnityEvent — цепочка квестов, ColorSpread и т.д. (как сейчас) |

Порядок при игре: сначала **world** (с анимацией), потом **UnityEvent**.

Цепочка квестов — только через **On Finish** → `QuestPresenter.StartQuest` (отдельного «chain» поля нет).

## Пример: грязь → гарпун

**Элемент `quest_dirt`:**

| | Show | Hide | Tool mode |
|---|------|------|-----------|
| World On Start | Shovel | Harpoon, Torch | Shovel (опционально) |
| World On Finish | Harpoon | Shovel | Harpoon ✓ |

**On Finish** (UnityEvent): `StartQuest(quest_1)`, при необходимости ColorSpread.

Порядок элементов в **Quests Events** = порядок цепочки (сверху раньше по сюжету).

## Сцена

- Shovel active у Home; Harpoon/Torch inactive.
- `TestQuestActivator` → `quest_dirt`.
- `CarryableToolModeBootstrap` → Shovel.

## Сейв и отладка

После загрузки JSON: `QuestBus.OnRuntimeLoaded` → `QuestModel.RebuildWorldState()` (без анимации).

**Quest Debug** Reset/Interrupt — тот же пересчёт.

## Файлы

- [`QuestModel.cs`](../Assets/REHozy/Quests/QuestMVP/QuestModel.cs)
- [`QuestBus.cs`](../Assets/REHozy/Quests/QuestBus.cs) — `OnRuntimeLoaded`, `OnInterrupt`
