# Коробка с декором (Home Zone)

Отдельная подсистема в [`Assets/REHozy/Scripts/Decoration/`](../Assets/REHozy/Scripts/Decoration/). Не использует `CarryableToolCore` и hold-return.

## Поведение

| Действие | Результат |
|----------|-----------|
| Короткий ЛКМ по коробке | Случайный prefab из пула (count уменьшается) → в переноске |
| Короткий ЛКМ по установленному предмету | Снова в руке (пул не тратится) |
| Короткий ЛКМ в переноске | Поставить под курсор на землю |
| Колёсико в переноске | Медленный поворот **только пока крутите** колесо (`scrollYawDegreesPerNotch` на prefab) |
| Удержание ЛКМ | Ничего |
| Тень под предметом | Зелёная — можно поставить, красная — вода или нельзя |
| Вода | Поставить нельзя (проверка через `WaterVolumeHelper`) |

Пока предмет в руке, колёсико **не** зумит орбитальную камеру — только вращает декор.

Пока инструмент в `Carried` / `Busy` / `Returning` — коробка не реагирует. Пока в руке декор — инструмент с земли не поднимается.

## Сцена

1. `HomePoint` + `HomeZoneRegistry` (как для гарпуна).
2. GO **DecorationBox** в Home: `PropSpawnBox` + collider (не trigger).
3. На **HarpoonGameplay** (или аналог): `DecorationInputHandler` + те же Attack / camera, что у `CarryableToolInputHandler`.

Быстрая расстановка: меню **REHozy → Setup Decoration Box Test** ([`DecorationBoxSceneSetup`](../Assets/REHozy/Editor/DecorationBoxSceneSetup.cs)).

## Prefab предмета

| Компонент | Назначение |
|-----------|------------|
| `PlaceableDecoration` | Состояния Placed / Carried |
| `CarryableCarryDriver` | Следование за курсором (camera, heightOffset, groundMask) |
| Collider | Подбор raycast’ом |
| Layer **Placeable** | Маска в `DecorationInputHandler` |
| `Rigidbody` (kinematic) | Опционально |

## Prefab коробки

| Поле | Назначение |
|------|------------|
| `entries[]` | `{ prefab, count }` — пул |
| `spawnAnchor` | Точка появления (опционально) |
| `interactCollider` | Collider для клика |

## Связанные файлы

- [`PropSpawnBox`](../Assets/REHozy/Scripts/Decoration/PropSpawnBox.cs)
- [`PlaceableDecoration`](../Assets/REHozy/Scripts/Decoration/PlaceableDecoration.cs)
- [`DecorationInputHandler`](../Assets/REHozy/Scripts/Decoration/DecorationInputHandler.cs)
