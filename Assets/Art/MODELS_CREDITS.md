# 3D-модели проекта EXFIL (все CC0, скачаны из открытых источников)

Все модели — **CC0 / Public Domain**: можно использовать коммерчески, без указания авторства
(указание приветствуется). Никаких лицензионных ограничений на проект не накладывается.

| Набор | Автор | Что взято | Лицензия | Источник |
|---|---|---|---|---|
| KayKit — Character Pack: Adventurers | Kay Lousberg | 5 риггованных персонажей + оружие (арбалет, дага, топор, меч) + 76 анимаций | CC0 | github.com/KayKit-Game-Assets/KayKit-Character-Pack-Adventures-1.0 |
| KayKit — Space Base Bits | Kay Lousberg | модульные базы, карго, контейнеры, туннели, техника (рейд-локация) | CC0 | github.com/KayKit-Game-Assets/KayKit-Space-Base-Bits-1.0 |
| KayKit — Prototype Bits | Kay Lousberg | модульный конструктор: стены, полы, двери, окна, лестницы, колонны (бункер) | CC0 | github.com/KayKit-Game-Assets/KayKit-Prototype-Bits-1.0 |
| KayKit — Dungeon Remastered | Kay Lousberg | сундуки, ящики, бочки, стеллажи, кровати, столы, факелы, монеты, ключи | CC0 | github.com/KayKit-Game-Assets/KayKit-Dungeon-Remastered-1.0 |
| Kenney — Mini Arena | Kenney | стены, полы, колонны, статуи, деревья, стойка с оружием | CC0 | github.com/KenneyNL/Starter-Kit-Basic-Scene |
| Kenney — City Builder / Starter Kit City | Kenney | дома, дороги, тротуары, деревья, трава (экстерьер рейда) | CC0 | github.com/KenneyNL/Starter-Kit-City-Builder |
| Kenney — Starter Kit FPS | Kenney | бластер и бластер-репитер (оружие), стены, платформы, дрон | CC0 | github.com/KenneyNL/Starter-Kit-FPS |
| Kenney — 3D Platformer | Kenney | кирпичи, платформы, монеты, флаги | CC0 | github.com/KenneyNL/Starter-Kit-3D-Platformer |
| Kenney — Prototype Textures | Kenney | текстуры-сетки для материалов уровня | CC0 | github.com/GeroVeni/kenney_prototype_tools |

## Формат `.expak`

Модели лежат не в `.fbx/.obj`, а в собственном формате **EXFIL pack** (бинарный, читается
`Assets/Scripts/EXFIL/Editor/AssetPipeline.cs`). Причина — Unity не импортирует glTF без
сторонних пакетов, а тянуть зависимости в проект не хочется. Пайплайн разворачивает паки в
настоящие Unity-ассеты:

* `Assets/Generated/Meshes/*.asset` — меши (включая разбитые по костям части персонажей);
* `Assets/Generated/Materials/*.mat` — материалы из атлас-текстур (URP или Standard);
* `Assets/Generated/Anims/*.anim` — **настоящие AnimationClip** с запечёнными кадрами
  (ходьба, бег, прицел, выстрел, перезарядка, смерть, взаимодействие и т.д.);
* `Assets/Generated/Prefabs/{Props,Weapons,Characters}/*.prefab` — готовые префабы
  со коллайдерами, точками крепления снаряжения и оружия (Muzzle / ShellEject / hand_r).

Пересобрать всё: меню Unity **EXFIL → Setup → Run full setup (one click)**.

## Инструменты (Python, лежат в `Tools/`)

* `gltf_lib.py` — минимальный читатель glTF/GLB (без зависимостей);
* `pack_io.py` — формат `.expak` + преобразование осей glTF → Unity;
* `bake_characters.py` — разбивка скелетных персонажей по костям + запекание анимаций;
* `bake_props.py` — OBJ/GLB → `.expak`;
* `build_all.py` — каталог ассетов и сборка всего в `Assets/Art/Models`.

Скрипты уже отработали: `Assets/Art/Models` содержит 156 моделей (139 пропсов, 11 стволов,
6 персонажей), ~5.5 МБ. Повторный запуск требует исходников наборов в `/tmp/dl` (см. ссылки выше).
