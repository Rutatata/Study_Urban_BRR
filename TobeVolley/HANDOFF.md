# Передача работы в локальную сессию Claude Code (на ПК пользователя)

Ты работаешь ЛОКАЛЬНО на Windows-ПК пользователя. В отличие от прежней облачной сессии, здесь можно запускать Unity,
читать её логи и смотреть настоящие скриншоты. Общайся с пользователем по-русски.

## Проект
- Ветка: `claude/anime-game-rematch-nauwh5` (сначала pull; коммить и пушь в неё же). Unity-проект: папка `TobeVolley/`.
- Unity **6.3 LTS (6000.3.20f1)** установлена; редактор обычно лежит в `C:\Program Files\Unity\Hub\Editor\6000.3.20f1\Editor\Unity.exe`.
  Если пользователь открывал проект из другой папки (раньше качал ZIP), спроси, какую папку он использует, или переведи его на этот git-клон.
- Игра «TOBE VOLLEY»: 3D аниме-волейбол от третьего лица в стиле **Rematch** (Sloclap) + аниме **Haikyuu!!**.
  Управляешь одним игроком, пустые места занимают боты. Онлайн через Unity Gaming Services (сессии + Relay), офлайн — «ТРЕНИРОВКА С БОТАМИ».
  Всё собирается кодом (`Assets/_Tobe/Scripts/Core/GameBootstrap.cs`), а `Assets/_Tobe/Editor/TobeSetup.cs` сам настраивает URP, сцену и обводку. Сначала прочитай `TobeVolley/README.md`.
- Код (`Assets/_Tobe/Scripts`):
  - `Shared/Contracts.cs` — общие типы, `GameHub`, `NetApi`;
  - `Sim/VolleySim.cs` — серверные правила, мяч, боты;
  - `Net/*` — NGO named messages; движение своего игрока считает клиент в `MatchClient`;
  - `View/*` — арена (`Look/*`), предзагрузка и клонирование VRM через UniVRM 0.131.3 (`Packages/com.vrmc.*`), `CharacterAppearance`, `PlayerView`;
  - `View/Anim/*` — мокап CMU BVH (`Assets/StreamingAssets/Motions`), запечённый в humanoid-мускулы; авторские волейбольные позы; процедурная походка и IK (`PlayerAnimator`, `GaitSolver`, `LocoBrain`, `VolleyKeys`);
  - `UI/*` — uGUI. Правило: один MonoBehaviour на файл, имя файла = имя класса.

## Что сейчас не устраивает пользователя (исправить и проверить глазами)
1. Анимация «не скелетная, а дёрганая»; при нажатии клавиш движения персонаж будто ТЕЛЕПОРТИРУЕТСЯ.
   Последний облачный коммит исправил возврат на место при подаче и добавил сглаживание чужих игроков и мяча между снапшотами
   (на хосте снапшоты 60 Гц) — проверь в Play, остались ли рывки.
   Где искать: сглаживание в `PlayerView`, условия «привязки» к серверной позиции в `MatchClient`, `PlayerAnimator` (foot plant, IK, gait),
   переключение поз, зависимость от FPS. Цель — плавная, весомая скелетная анимация, как в Rematch.
2. Волейбольные движения:
   - низкая стойка готовности, приставной шаг лицом к мячу и сетке;
   - разбег в 3 шага на атаку;
   - приём снизу, передача сверху, блок у сетки, празднования.
   Правила волейбола соблюдаются: через сетку не пройти, к ней не прикоснуться, сквозь игроков не пройти.
3. Читаемость картинки: раньше был сильный пересвет (ослабили bloom и свет в `View/Look/*`, `ArenaBuilder.cs`) — проверь реальный результат.
   Подстрой свет и постобработку, заметность мяча, плашки ников (`View/NameplateLayer.cs`), номера на форме, трибуны.
4. Звуки: синтезированный шум трибун убран, он звучал как прибой. Нужны настоящие звуки в `Assets/_Tobe/Resources/Audio/`
   с именами whistle, crowd_loop, cheer, spike, bump, set, net, floor, squeak.
   Их скачивает пользователь с Pixabay Sound Effects или Freesound (фильтр CC0). Скажи точно, что найти: с этого ПК сайты открываются.
5. Опционально — анимации Mixamo. Пользователь качает их со своим Adobe-логином: FBX for Unity, Without Skin, 30 fps, In Place.
   - Папка: `Assets/_Tobe/Resources/Anim/`.
   - Имена: idle, ready, run, sprint, shuffle_left, backpedal, jump_vertical, jump_approach, land, dive, celebrate, sad.
   - Плюс bump, set, spike, block, serve_float, serve_jump — если пользователь купит волейбольный пак, например Fab «Volley-ball animations, Motion Cast #14».
   - `Editor/AnimImportPostprocessor.cs` сам ставит им Humanoid, а `View/Anim/MotionLoader.cs` запекает их и использует в первую очередь.

## Как работать
- Лучше всего поставить бесплатный плагин **MCP for Unity** (GitHub `CoplayDev/unity-mcp`, сначала спроси пользователя).
  Тогда сможешь читать Console, входить в Play и выходить из него, снимать скриншоты окна Game.
  Без плагина: batchmode / `-executeMethod` для dev-билда или скрипта со скриншотом из Play, плюс логи
  `%LOCALAPPDATA%\Unity\Editor\Editor.log` и Player.log.
- Цикл работы: запуск → скриншот или лог → правка → повтор, пока не будет хорошо. Тестируй через «ТРЕНИРОВКА С БОТАМИ».
- Делай маленькие понятные коммиты в `claude/anime-game-rematch-nauwh5` и пушь их. Library/Temp не коммить.
