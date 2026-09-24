# Где брать 3D-модели (CC0 / бесплатные)

Игра уже играбельна на примитивах. Чтобы заменить заглушки — скачайте пакеты и перетащите префабы
в соответствующие поля. Никакой код править не нужно.

## Куда что вставлять

| Что | Поле | Куда в Unity |
|---|---|---|
| Оружие в руках (вид от первого лица) | `WeaponItemDefinition` → `ViewModel Prefab` | `Assets/Data/Items/Weapons/*.asset` |
| Оружие в мире / у ботов | `WeaponItemDefinition` → `World Model Prefab` | там же |
| Бронежилет, риг, шлем, рюкзак | `ArmorItemDefinition` → `Visual` → `Prefab3D` + `Attach Point` | `Assets/Data/Items/Armor/*.asset` |
| Одежда/костюм (заменяет тело) | `ClothingItemDefinition` → `Visual` → `Prefab3D` | `Assets/Data/Items/...` |
| Тело персонажа (в термобельё) | `CharacterDefinition` → `Body Prefab` | `Assets/Data/Characters/*.asset` |
| Растения по стадиям (5 шт.) | `PlantDefinition` → `Stage Prefabs` | `Assets/Data/Hideout/Plants/*.asset` |
| Модули оружия | `WeaponModuleItemDefinition` → иконка + префаб прицела | `Assets/Data/Items/Ammo/*.asset` |

## Проверенные источники (бесплатно, можно в коммерческих проектах)

- **Poly Pizza** — https://poly.pizza — тысячи low-poly моделей, CC0/CC-BY. Отлично подходит для пропсов бункера и техники.
- **Kenney.nl** — https://kenney.nl/assets — CC0: sci-fi/industrial kit, мебель, ящики, оружие, UI, звуки.
- **Quaternius** — https://quaternius.com — CC0: персонажи low-poly, оружие, природа, здания.
- **Mixamo** — https://www.mixamo.com — бесплатные анимации + базовые персонажи (нужен аккаунт Adobe).
- **ambientCG** — https://ambientcg.com — CC0 текстуры PBR (бетон, металл, ржавчина, дерево, грунт) — идеально для бункера.
- **OpenGameArt** — https://opengameart.org — моделей много, но проверяйте лицензию каждой.
- **Unity Asset Store (бесплатное)** — «Modular Sci-fi / Industrial Props», «Low-Poly Weapons», «Polygon Prototype».

## Быстрый рецепт

1. Скачайте пакет, распакуйте в `Assets/Models/<пакет>`.
2. Выделите импортированную модель → в инспекторе Materials выберите **Extract Materials** (иначе текстуры не появятся).
3. Соберите префаб: корень + дочерний `Muzzle` (пустой объект на конце ствола) — по нему считается точка выстрела,
   вспышка и гильза. Опционально `ShellEject`.
4. Перетащите префаб в поле `ViewModel Prefab` нужного оружия, выставьте `ViewModel Offset` / `Aim Offset` под кадр.
5. Для брони: префаб кидается в `Visual → Prefab3D`, а `Attach Point` выбирает кость/якорь
   (`Head`, `Face`, `Spine`, `Chest`, `Hips`, `Back`, `RightHand`, ноги).
6. Текстуры окружения (бетон/металл) — в материалы `SceneBuilder` (создаются при сборке сцен) или просто
   перекрасьте существующие материалы в сцене.

## Звук

- **Freesound** (CC0-фильтр), **Kenney Impact Sounds**, **Soniss GDC bundles** (бесплатные).
- Шаги: `FpsController` → массив `Footsteps`; стрельба/перезарядка: поля на `WeaponItemDefinition`;
  боль/смерть: `CharacterDefinition` → `PainSounds` / `DeathSounds`; фон: `AudioManager` → `AmbienceClips`.
