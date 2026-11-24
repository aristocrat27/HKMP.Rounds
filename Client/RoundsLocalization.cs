using System;
using System.Collections.Generic;
using System.Linq;

namespace HKMP.Rounds.Client
{
    internal static class RoundsLocalization
    {
        private static readonly Dictionary<string, string> Russian =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["SETTINGS"] = "НАСТРОЙКИ",
                ["MAPS"] = "КАРТЫ",
                ["BENCHES"] = "СКАМЕЙКИ",
                ["PvP mode, round rules and shared bench"] = "Режим PvP, правила раунда и общая скамейка",
                ["Round options and shared bench"] = "Настройки раунда и общая скамейка",
                ["CLOSE"] = "ЗАКРЫТЬ",
                ["PVP MODE"] = "РЕЖИМ PVP",
                ["CLIENT CONTROLS & HUD"] = "УПРАВЛЕНИЕ И ИНДИКАТОРЫ",
                ["LIVE STATISTICS"] = "СТАТИСТИКА В РЕАЛЬНОМ ВРЕМЕНИ",
                ["SHOW STATISTICS OVERLAY"] = "ПОКАЗАТЬ ОКНО СТАТИСТИКИ",
                ["HIDE STATISTICS OVERLAY"] = "СКРЫТЬ ОКНО СТАТИСТИКИ",
                ["The overlay stays on screen while you play. Tap the statistics hotkey to show or hide it; hold the hotkey to move and resize it."] = "Окно статистики остаётся на экране во время игры. Нажмите назначенную клавишу, чтобы показать или скрыть окно, либо удерживайте её, чтобы перемещать окно и менять его размер.",
                ["UPDATE STATISTICS FOR EVERYONE"] = "ОБНОВИТЬ СТАТИСТИКУ ДЛЯ ВСЕХ",
                ["ROUND STATISTICS"] = "СТАТИСТИКА РАУНДА",
                ["EDIT MODE"] = "РЕЖИМ РЕДАКТИРОВАНИЯ",
                ["Menu hotkey"] = "Клавиша меню",
                ["Statistics overlay hotkey"] = "Клавиша окна статистики",
                ["Press a key for the statistics overlay. Escape cancels."] = "Нажмите клавишу окна статистики. Escape отменяет действие.",
                ["Ready hotkey (/rd)"] = "Готовность (/rd)",
                ["Game frozen."] = "Игра заморожена.",
                ["Game unfrozen."] = "Игра разморожена.",
                ["Show personal ready status"] = "Показывать свой статус готовности",
                ["Show ready count"] = "Показывать количество готовых",
                ["Show round status"] = "Показывать статус раунда",
                ["Press a key"] = "НАЖМИТЕ КЛАВИШУ",
                ["Close with Esc"] = "Закрыть — Esc",
                ["Scroll for more options"] = "Прокрутите для остальных настроек",
                ["Press a key for the menu hotkey. Escape cancels."] = "Нажмите клавишу меню. Escape отменяет действие.",
                ["Press a key for the ready hotkey. Escape cancels."] = "Нажмите клавишу готовности. Escape отменяет действие.",
                ["Freeze / unfreeze game"] = "Заморозить / разморозить игру",
                ["Press a key for game freeze. Escape cancels."] = "Нажмите клавишу заморозки игры. Escape отменяет действие.",
                ["Key binding cancelled."] = "Назначение клавиши отменено.",
                ["Hotkey saved: "] = "Клавиша назначена: ",
                ["That key is already used by the other hotkey or cannot be bound."] = "Эта клавиша уже занята другой командой или не поддерживается.",
                ["ROUND SETTINGS"] = "НАСТРОЙКИ РАУНДА",
                ["Auto Start"] = "Автостарт",
                ["Round SOUL"] = "Душа на раунд",
                ["Ready player count (0 = all)"] = "Количество готовых игроков (0 = все)",
                ["Timer Integration"] = "Интеграция с таймером",
                ["Disable Mobs"] = "Отключить врагов",
                ["Disable Shade"] = "Отключить тень",
                ["Individual wins for each team member"] = "Отдельные победы каждому участнику команды",
                ["Disable PvP before round"] = "Отключить PvP до начала раунда",
                ["Give 2 Lifeblood masks"] = "Выдать 2 маски живокрови",
                ["Hide phantom Mantis background"] = "Скрывать фон в фантомных богомолках",
                ["Hide phantom Mantis backgrounds for all players"] = "Скрывать фон в фантомных богомолках у всех игроков",
                ["Hide phantom Mantis backgrounds: ON"] = "Фон в фантомных богомолках: ВКЛ",
                ["Hide phantom Mantis backgrounds: OFF"] = "Фон в фантомных богомолках: ВЫКЛ",
                ["MATCH CONTROL"] = "УПРАВЛЕНИЕ МАТЧЕМ",
                ["Win target (0 = off)"] = "Цель побед (0 = выкл.)",
                ["END ROUND"] = "ЗАВЕРШИТЬ РАУНД",
                ["RESET STATISTICS"] = "СБРОСИТЬ СТАТИСТИКУ",
                ["Round preparation is in progress."] = "Подготовка раунда выполняется.",
                ["A round is currently active."] = "Сейчас идёт раунд.",
                ["No active round."] = "Нет активного раунда.",
                ["Match target: "] = "Цель матча: ",
                [" wins."] = " побед.",
                ["Match win target: off."] = "Цель побед в матче отключена.",
                ["EMERGENCY WARP"] = "АВАРИЙНАЯ ТЕЛЕПОРТАЦИЯ",
                ["WARP TO SELECTED BENCH"] = "ВАРП К ВЫБРАННОЙ СКАМЕЙКЕ",
                ["RESCUE PLAYER"] = "СПАСТИ ИГРОКА",
                ["Warp to bench: "] = "Телепортировать к скамейке: ",
                ["Select a bench before warping."] = "Выберите скамейку перед телепортацией.",
                ["Select a valid bench before warping."] = "Выберите корректную скамейку перед телепортацией.",
                ["The selected bench is not available in the current game scene."] = "Выбранная скамейка недоступна в текущем игровом мире.",
                ["Connect to an HKMP server to request a player warp."] = "Подключитесь к серверу HKMP, чтобы запросить телепортацию игрока.",
                ["Emergency warp requested for "] = "Запрошена аварийная телепортация игрока ",
                ["The game world is not ready for an emergency warp."] = "Игровой мир ещё не готов к аварийной телепортации.",
                ["A gameplay transition is already in progress."] = "Переход между локациями уже выполняется.",
                ["The emergency warp controller could not be initialized."] = "Не удалось инициализировать аварийную телепортацию.",
                ["An emergency warp is already running or has just completed."] = "Аварийная телепортация уже выполняется или только что завершилась.",
                ["No usable bench was found in this room, saved respawn, shared bench, or built-in catalog."] = "Не удалось найти доступную скамейку в комнате, сохранённой точке возрождения, общей скамейке или каталоге.",
                ["Warped to the nearest bench in "] = "Телепортация к ближайшей скамейке в локации ",
                ["Emergency warp started to "] = "Начата аварийная телепортация к точке ",
                ["Emergency warp request sent to "] = "Запрос аварийной телепортации отправлен игроку ",
                ["The selected player ID is invalid."] = "Идентификатор выбранного игрока недействителен.",
                ["The selected player is not connected."] = "Выбранный игрок не подключён.",
                ["STATISTICS"] = "СТАТИСТИКА",
                ["REFRESH STATISTICS"] = "ОБНОВИТЬ СТАТИСТИКУ",
                ["Statistics have not been loaded."] = "Статистика ещё не загружена.",
                ["Statistics are unavailable."] = "Статистика недоступна.",
                ["SHARED BENCH"] = "ОБЩАЯ СКАМЕЙКА",
                ["Select a discovered bench"] = "Выберите найденную скамейку",
                ["Shared bench: not set"] = "Общая скамейка: не выбрана",
                [" (LOCKED)"] = " (ЗАБЛОКИРОВАНА)",
                [" (UNLOCKED)"] = " (РАЗБЛОКИРОВАНА)",
                [" (LOCKED, but unavailable on this client)"] = " (ЗАБЛОКИРОВАНА, но недоступна на этом клиенте)",
                ["Quick: no healing; Quick Focus, Kingsoul, Spore Shroom, Hiveblood, Lifeblood charms, Deep Focus and Weaversong are removed. All other charms are granted."] = "Быстрый: лечение запрещено; Быстрый фокус, Душа короля, Споровый гриб, Кровь улья, амулеты на живокровь, Глубокий фокус и Песнь ткачей отключены. Все остальные амулеты выдаются.",
                ["OneShot: 1 HP, server-managed SOUL, no healing, and no Quick Focus, Spell Twister, Kingsoul, Spore Shroom, Hiveblood, Lifeblood charms, Deep Focus or Weaversong. Carefree Melody is granted."] = "Ваншот: 1 HP, душа контролируется сервером, лечение запрещено; Быстрый фокус, Искажатель заклинаний, Душа короля, Споровый гриб, Кровь улья, амулеты на живокровь, Глубокий фокус и Песнь ткачей отключены. Выдаётся Беспечная песнь.",
                ["Select a shared bench before locking it."] = "Выберите общую скамейку перед блокировкой.",
                ["Requesting lock of the shared bench..."] = "Запрашиваем блокировку общей скамейки…",
                ["Requesting unlock of the shared bench..."] = "Запрашиваем разблокировку общей скамейки…",
                ["The shared bench is locked. Round-end warps will use the host-selected bench."] = "Общая скамейка заблокирована. После раунда игроки будут перемещаться к выбранной хостом скамейке.",
                ["The shared bench is unlocked. Players without a personal selection use the host-selected bench; personal selections are respected."] = "Общая скамейка разблокирована. Игроки без личного выбора используют скамейку хоста, а личный выбор каждого игрока сохраняется.",
                ["Waiting for authoritative bench settings from the host."] = "Ожидается получение подтверждённых настроек скамеек от хоста.",
                ["The host-locked shared bench is not available on this client."] = "Заблокированная хостом общая скамейка недоступна на этом клиенте.",
                ["The selected bench is invalid."] = "Выбранная скамейка недействительна.",
                ["The selected bench is not available on this client."] = "Выбранная скамейка недоступна на этом клиенте.",
                ["No personal, shared, or saved bench has been selected."] = "Не выбрана личная, общая или сохранённая скамейка.",
                ["Only the host or an authorized admin can lock the shared bench."] = "Блокировать общую скамейку может только хост или администратор.",
                ["Map travel is disabled. Only the host can move the group."] = "Перемещение между картами отключено. Перемещать всех игроков может только хост.",
                ["SET HERE"] = "ВЫБРАТЬ ЗДЕСЬ",
                ["Only the host or an authorized admin can change the shared bench and its lock."] = "Изменять общую скамейку и её блокировку может только хост или администратор.",
                ["No benches have been discovered yet. Visit rooms to populate the list."] = "Скамейки ещё не найдены. Посетите комнаты, чтобы заполнить список.",
                ["Search benches..."] = "Поиск скамеек...",
                ["No matching benches."] = "Скамейки не найдены.",
                ["Your Deploy  ·  "] = "Ваша скамейка  ·  ",
                ["Host Deploy  ·  "] = "Скамейка хоста  ·  ",
                ["Only the host or an authorized admin can change round rules and PvP mode."] = "Изменять правила раундов и режим PvP может только хост или администратор.",
                ["Only the host or an authorized admin can set the shared bench and its lock."] = "Изменять общую скамейку и её блокировку может только хост или администратор.",
                ["Stand on or near a bench before using SET HERE."] = "Встаньте на скамейку или рядом с ней перед выбором.",
                ["Connect to an HKMP server to configure round settings."] = "Подключитесь к серверу HKMP, чтобы настраивать раунды.",
                ["Disconnected from the HKMP server."] = "Соединение с сервером HKMP потеряно.",
                ["You are no longer ready."] = "Вы больше не готовы.",
                ["Autostart is disabled."] = "Автостарт отключён.",
                ["You are ready. Automatic round start is disabled."] = "Вы готовы. Автоматический старт раунда отключён.",
                ["You are ready. At least two players must be connected to start a round."] = "Вы готовы. Для начала раунда должны быть подключены минимум два игрока.",
                ["The ready requirement was reached, but the round could not start. Your ready status was kept."] = "Порог готовности достигнут, но начать раунд не удалось. Ваш статус готовности сохранён.",
                ["Readiness can only be changed between rounds."] = "Готовность можно менять только между раундами.",
                ["Could not change your ready status."] = "Не удалось изменить статус готовности.",
                ["Use /rd to toggle your ready status for the next round."] = "Используйте /rd, чтобы переключать готовность к следующему раунду.",
                ["Only a connected player can change ready status."] = "Изменить готовность может только подключённый игрок.",
                ["A round is already active or preparing."] = "Раунд уже идёт или подготавливается.",
                ["You are ready ("] = "Вы готовы (",
                ["The ready threshold was reached, but the round could not start. Ready votes were reset; press the ready key again after checking the host status."] = "Порог готовности достигнут, но начать раунд не удалось. Голоса сброшены; после проверки состояния хоста нажмите клавишу готовности ещё раз.",
                ["The ready requirement was reached. The round is preparing."] = "Нужное количество игроков готово. Раунд подготавливается.",
                ["Could not find your connected player session."] = "Не удалось найти вашу игровую сессию.",
                ["F6  ·  Close with Esc  ·  Scroll for more options"] = "F6 · Закрыть — Esc · Прокрутите для остальных настроек",
                ["Quick: no healing or Focus; Quick Focus, Kingsoul, Spore Shroom, Hiveblood, Lifeblood charms, Deep Focus and Weaversong are removed. Other charms are granted for the round."] = "Быстрый: лечение и Фокус запрещены; удаляются Быстрый фокус, Душа короля, Споровый гриб, Кровь улья, амулеты на живокровь, Глубокий фокус и Песнь ткачей. Остальные амулеты выдаются на раунд.",
                ["Long: healing is allowed; only Spore Shroom is disabled."] = "Долгий: лечение разрешено; отключён только Споровый гриб.",
                ["OneShot: 1 HP, server-managed SOUL, no healing, and no Quick Focus, Spell Twister, Kingsoul, Spore Shroom, Hiveblood, Lifeblood charms, Deep Focus or Weaversong. Other charms are granted, including Carefree Melody."] = "Ваншот: 1 маска, Душа управляется сервером, лечение запрещено; недоступны Быстрый фокус, Заклинатель заклинаний, Душа короля, Споровый гриб, Кровь улья, амулеты на живокровь, Глубокий фокус и Песнь ткачей. Остальные амулеты выдаются, включая Беспечную песнь.",
                ["Kingsoul"] = "Душа короля",
                ["Spore Shroom"] = "Споровый гриб",
                ["Quick Focus"] = "Быстрый фокус",
                ["Carefree Melody"] = "Беспечная песнь",
                ["Deep Focus"] = "Глубокий фокус",
                ["Hiveblood"] = "Кровь улья",
                ["Weaversong"] = "Песнь ткачей",
                ["Lifeblood charms"] = "амулеты на живокровь",
                ["Lifeblood Heart"] = "Сердце живокрови",
                ["Lifeblood Core"] = "Ядро живокрови",
                ["QUICK"] = "БЫСТРОЕ",
                ["LONG"] = "ДОЛГОЕ",
                ["ONESHOT"] = "ВАНШОТ",
                ["Help"] = "Справка",
                ["Round settings help"] = "Справка по настройкам раундов",
                ["Help content is unavailable."] = "Содержимое справки недоступно.",
                ["Image unavailable."] = "Изображение недоступно.",
                ["No shared bench"] = "Общая скамейка не выбрана",
                ["Shared bench: "] = "Общая скамейка: ",
                ["Changing PvP mode..."] = "Меняем режим PvP…",
                ["Updating win target..."] = "Обновляем цель побед…",
                ["Resetting statistics..."] = "Сбрасываем статистику…",
                ["Refreshing statistics..."] = "Обновляем статистику…",
                ["Setting the bench you are sitting on..."] = "Выбираем скамейку, на которой вы сидите…",
                ["Setting the nearby bench..."] = "Выбираем ближайшую скамейку…",
                ["Shared bench set to "] = "Общая скамейка выбрана: ",
                ["Other benches are now locked."] = "Остальные скамейки заблокированы.",
                ["Other benches are now unlocked."] = "Остальные скамейки разблокированы.",
                ["Live match statistics"] = "Статистика матча в реальном времени",
                ["Ready HUD (tap / hold to edit and resize)"] = "Индикатор готовности (нажмите / удерживайте для редактирования и изменения размера)",
                ["Press a key for the ready command. Escape cancels."] = "Нажмите клавишу готовности. Escape отменяет действие.",
                ["Press a key for the ready HUD visibility/edit hotkey. Escape cancels."] = "Нажмите клавишу показа/редактирования индикатора готовности. Escape отменяет действие.",
                ["Requesting a server-side statistics refresh..."] = "Запрашиваем обновление статистики на сервере…",
                ["Connect to the host to refresh statistics."] = "Подключитесь к хосту, чтобы обновить статистику.",
                ["Resetting statistics for everyone..."] = "Сбрасываем статистику для всех…",
                ["Connect to the host to reset statistics."] = "Подключитесь к хосту, чтобы сбросить статистику.",
                ["Enforce vignette for all players"] = "Принудительно включать виньетку для всех игроков",
                ["Ending round..."] = "Завершаем раунд…",
                ["Bench warp requested for "] = "Запрошена телепортация к скамейке для ",
                ["Requesting a warp to the nearest real bench..."] = "Запрашиваем телепортацию к ближайшей настоящей скамейке…",
                ["Local emergency warp failed: "] = "Локальная аварийная телепортация не удалась: ",
                ["Select a bench first."] = "Сначала выберите скамейку.",
                ["Updating shared bench..."] = "Обновляем общую скамейку…",
                ["Updating bench lock..."] = "Обновляем блокировку скамеек…",
                ["Only the host or an authorized admin can change this setting."] = "Изменять эту настройку может только хост или администратор.",
                ["Statistics update automatically after each round. Only the host or an authorized admin can refresh or reset them."] = "Статистика обновляется автоматически после каждого раунда. Обновлять или сбрасывать её может только хост или администратор.",
                ["BACK TO SETTINGS"] = "ВЕРНУТЬСЯ К НАСТРОЙКАМ",
                ["APPLY"] = "ПРИМЕНИТЬ",
                ["ON"] = "ВКЛ",
                ["OFF"] = "ВЫКЛ",
                ["SET"] = "ВЫБРАТЬ",
                ["LOCK"] = "ЗАБЛОКИРОВАТЬ",
                ["UNLOCK"] = "РАЗБЛОКИРОВАТЬ",
                ["TEAM STATISTICS"] = "СТАТИСТИКА КОМАНД",
                ["PLAYER STATISTICS"] = "СТАТИСТИКА ИГРОКОВ",
                ["Statistics are empty."] = "Статистика пуста.",
                ["EDIT MODE • HOLD {KEY} TO MOVE / RESIZE"] = "РЕЖИМ РЕДАКТИРОВАНИЯ • УДЕРЖИВАЙТЕ {KEY} ДЛЯ ПЕРЕМЕЩЕНИЯ / ИЗМЕНЕНИЯ РАЗМЕРА",
                ["READY STATUS"] = "СТАТУС ГОТОВНОСТИ",
                ["READY COUNT"] = "КОЛИЧЕСТВО ГОТОВЫХ",
                ["READY HUD  •  DRAG AN ITEM TO MOVE  •  DRAG ITS CORNER TO RESIZE"] = "ИНДИКАТОР ГОТОВНОСТИ • ПЕРЕТАЩИТЕ ЭЛЕМЕНТ ДЛЯ ПЕРЕМЕЩЕНИЯ • ПОТЯНИТЕ ЗА УГОЛ ДЛЯ ИЗМЕНЕНИЯ РАЗМЕРА",
                [" wins"] = " побед",
                [" at "] = " в ",
                [" must be a whole number."] = " должно быть целым числом.",
                [" must be between "] = " должно быть в диапазоне от ",
                [" and "] = " до ",
                ["You must finish returning to the bench and get up before marking ready."] = "Дождитесь возвращения к скамейке и встаньте, прежде чем отмечать готовность.",
                ["Updating round settings..."] = "Обновляем настройки раунда…",
                ["No built-in map presets are available."] = "Нет доступных встроенных карт.",
                ["PvP mode, round rules, maps and benches"] = "Режим PvP, правила раундов, карты и скамейки",
                ["Only the host or an authorized admin can select a preset PvP map."] = "Выбрать карту PvP может только хост или администратор.",
                ["That built-in map preset is unavailable."] = "Эта встроенная карта недоступна.",
                ["A room with an active round cannot be entered or left."] = "Нельзя войти в комнату с активным раундом или покинуть её.",
                ["Moving all connected players to "] = "Перемещаем всех подключённых игроков на карту ",
                ["Only the host or an authorized admin can change map travel."] = "Изменять доступность перемещения между картами может только хост или администратор.",
                ["Map travel has been enabled."] = "Перемещение между картами включено.",
                ["Map travel has been disabled."] = "Перемещение между картами отключено.",
                ["Only the host or an authorized admin can create a phantom Mantis map."] = "Создавать фантомную карту богомолов может только хост или администратор.",
                ["A new phantom Mantis ID could not be allocated."] = "Не удалось выделить идентификатор для новой фантомной карты богомолов.",
                ["Created "] = "Создана карта ",
                ["Map travel is disabled by the host."] = "Перемещение между картами отключено хостом.",
                ["That destination is not in the server map catalog. Refresh the map list and try again."] = "Этого направления нет в каталоге сервера. Обновите список карт и повторите попытку.",
                ["Travel requested: "] = "Запрошен переход: ",
                ["That phantom Mantis map no longer exists."] = "Эта фантомная карта богомолов больше не существует.",
                ["Travel requested to phantom Mantis #"] = "Запрошен переход на фантомную карту богомолов №",
                ["Unsupported map travel action."] = "Неподдерживаемое действие перемещения между картами.",
                ["The destination room loaded, but no usable bench marker could be found."] = "Комната загружена, но подходящая скамейка не найдена.",
                ["No bench exists in '"] = "В комнате нет скамейки: ",
                ["The game world is not ready for preset map travel."] = "Игровой мир ещё не готов к переходу на выбранную карту.",
                ["Timed out while travelling to preset map '"] = "Истекло время перехода на карту ",
                ["Could not finish travel to the room's bench: "] = "Не удалось завершить переход к скамейке: ",
                ["Ignored a preset map command because that map is not active on this client."] = "Команда выбора карты проигнорирована: эта карта не активна на клиенте.",
                ["PVP & ROUND SETTINGS"] = "НАСТРОЙКИ PVP И РАУНДОВ",
                ["CLIENT CONTROLS & STATISTICS"] = "УПРАВЛЕНИЕ И СТАТИСТИКА",
                ["MAPS & BENCHES"] = "КАРТЫ И СКАМЕЙКИ",
                ["SELECT PVP MAP"] = "ВЫБРАТЬ КАРТУ PVP",
                ["Select a preset map. All connected players will be moved together."] = "Выберите карту. Все подключённые игроки будут перемещены одновременно.",
                ["Mantis Village"] = "Деревня Богомолов",
                ["Phantom Mantis Village"] = "Фантомная Деревня Богомолов",
                ["CREATE PHANTOM MANTIS VILLAGE"] = "СОЗДАТЬ ФАНТОМНУЮ ДЕРЕВНЮ БОГОМОЛОВ",
                ["Phantom Mantis Village #"] = "Фантомная Деревня Богомолов #",
                ["Separate rounds for each room"] = "Отдельные раунды для каждой комнаты",
                ["Each room has its own /rd readiness and round state. Players in other rooms will not see it."] = "В каждой комнате свои готовность /rd и состояние раунда. Игроки в других комнатах их не видят.",
                ["Separate rounds per room can only be changed when no room has an active or preparing round and all post-round bench returns are complete."] = "Раздельные раунды по комнатам можно менять, только когда во всех комнатах завершены раунды и все игроки вернулись к скамейкам.",
                ["Separate rounds per room can only be changed when no room has an active or preparing round."] = "Раздельные раунды по комнатам можно менять, только когда ни в одной комнате нет активного раунда или подготовки.",
                ["Separate rounds per room enabled. Readiness and round state are now isolated by scene."] = "Раздельные раунды включены. Готовность и состояние раунда теперь независимы для каждой комнаты.",
                ["Separate rounds per room disabled. Readiness and round state are shared again."] = "Раздельные раунды отключены. Готовность и состояние раунда снова общие.",
                ["A second room cannot start while another round uses the single HKMP.Timer integration. Disable timer integration to run simultaneous room rounds."] = "Нельзя запустить раунд во второй комнате, пока другой раунд использует единственную интеграцию HKMP.Timer. Отключите интеграцию таймера для одновременных раундов в комнатах.",
                ["Your room is not synchronized yet. Try /rd again after the room finishes loading."] = "Комната ещё не синхронизирована. Повторите /rd после завершения загрузки комнаты.",
                ["Choose this option, then press the key you want to assign. Escape cancels."] = "Выберите этот пункт, затем нажмите назначаемую клавишу. Escape отменяет действие.",
                ["Set Keybind"] = "Назначить кейбинд",
                ["Open Rounds menu hotkey"] = "Клавиша открытия меню Rounds",
                ["Crystallized Mound"] = "Кристаллический курган",
                ["Queen's Station"] = "Вокзал королевы",
                ["Howling Cliffs"] = "Воющие утёсы",
                ["Kingdom's Edge"] = "Край королевства",
                ["Dirtmouth"] = "Грязьмут",
                ["White Palace"] = "Белый дворец",
                ["City of Tears"] = "Город слёз",
                ["Distant Village"] = "Далёкая деревня",
                ["Open Rounds window"] = "Открыть меню Rounds",
                ["MAP TRAVEL & PHANTOM MANTIS"] = "КАРТЫ И ФАНТОМНЫЕ БОГОМОЛЫ",
                ["Allow players to travel between maps"] = "Разрешить игрокам перемещаться между картами",
                ["Connect to the HKMP host to change map travel."] = "Подключитесь к хосту HKMP, чтобы изменить доступность перемещения между картами.",
                ["Updating map travel setting..."] = "Обновляем настройку перемещения между картами…",
                ["CREATE PHANTOM MANTIS MAP"] = "СОЗДАТЬ ФАНТОМНУЮ ДЕРЕВНЮ БОГОМОЛОВ",
                ["Connect to the HKMP host to create a phantom map."] = "Подключитесь к хосту HKMP, чтобы создать фантомную карту.",
                ["Creating a new phantom Mantis map..."] = "Создаём новую фантомную карту богомолов…",
                ["Map travel is enabled. Each player can travel independently; moving the host does not move other players."] = "Перемещение между картами включено. Каждый игрок перемещается самостоятельно; перемещение хоста не перемещает остальных.",
                ["Map travel is disabled. Non-host players cannot travel; host/admin travel moves everyone together."] = "Перемещение между картами отключено. Игроки не могут перемещаться самостоятельно; переход хоста перемещает всех.",
                ["When enabled, each player travels independently. When disabled, only the host can move everyone to a destination."] = "При включении каждый игрок перемещается самостоятельно. При отключении только хост может переместить всех к выбранной точке.",
                ["No phantom Mantis maps have been created."] = "Фантомные карты богомолов ещё не созданы.",
                ["TELEPORT TO "] = "ТЕЛЕПОРТИРОВАТЬСЯ: ",
                ["Connect to the HKMP host to travel to a phantom map."] = "Подключитесь к хосту HKMP, чтобы переместиться на фантомную карту.",
                ["PHANTOM MAP PAGE "] = "СТРАНИЦА ФАНТОМНЫХ КАРТ ",
                ["PREVIOUS PHANTOM MAP PAGE"] = "ПРЕДЫДУЩАЯ СТРАНИЦА ФАНТОМНЫХ КАРТ",
                ["Only the host or an authorized admin can change the shared bench."] = "Менять общую скамейку может только хост или администратор.",
                ["Emergency warp failed: "] = "Ошибка аварийной телепортации: ",
                ["Requesting travel to "] = "Запрашиваем переход: ",
                ["Connect to an HKMP server to load map destinations."] = "Подключитесь к серверу HKMP, чтобы загрузить список направлений.",
                ["Player "] = "Игрок ",
                ["Press a key to open the Rounds window. Escape cancels."] = "Нажмите клавишу открытия окна Rounds. Escape отменяет действие.",
                ["Local mode: preset maps and selected-bench warp are available without a host."] = "Локальный режим: выбор карт и телепортация к выбранной скамейке доступны без хоста.",
                ["Connected to HKMP. Loading server settings..."] = "Подключено к HKMP. Загружаем настройки сервера…",
                ["Offline mode: preset maps are loaded locally. Phantom instances and shared destinations require an HKMP host."] = "Автономный режим: карты загружаются локально. Для фантомных копий и общих направлений нужен хост HKMP.",
                ["HOST & CONTROLS"] = "ХОСТ И УПРАВЛЕНИЕ",
                ["HIDE STATISTICS WINDOW"] = "СКРЫТЬ ОКНО СТАТИСТИКИ",
                ["SHOW STATISTICS WINDOW"] = "ПОКАЗАТЬ ОКНО СТАТИСТИКИ",
                ["Travelling locally to "] = "Локальный переход на карту: ",
                ["Local travel failed: "] = "Не удалось выполнить локальный переход: ",
                ["Phantom Mantis #"] = "Фантомная Деревня Богомолов #",
                ["Connect to the HKMP host to delete a phantom map."] = "Подключитесь к хосту HKMP, чтобы удалить фантомную карту.",
                ["Deleting "] = "Удаляем ",
                ["Connect to an HKMP server to load phantom map state."] = "Подключитесь к серверу HKMP, чтобы загрузить состояние фантомных карт.",
                ["Disconnected from the HKMP server. Local map travel and selected-bench warp are still available."] = "Соединение с сервером HKMP потеряно. Локальные переходы между картами и телепортация к выбранной скамейке по-прежнему доступны.",
                ["No usable built-in bench or configured return bench was found."] = "Не найдена подходящая встроенная скамейка или настроенная скамейка.",
                ["The game world is not ready for a bench warp."] = "Игровой мир ещё не готов к телепортации к скамейке.",
                ["A bench warp is already running."] = "Телепортация к скамейке уже выполняется.",
                ["A map or bench travel is already in progress."] = "Переход на карту или к скамейке уже выполняется.",
                ["The local map travel controller is not ready."] = "Контроллер локального перемещения по картам ещё не готов.",
                ["The game world is not ready for local travel."] = "Игровой мир ещё не готов к локальному переходу.",
                ["This built-in map preset is unavailable."] = "Эта встроенная карта недоступна.",
                ["Local travel started."] = "Локальный переход начат.",
                ["Warp started to "] = "Начата телепортация к ",
                ["USE HOST"] = "ХОСТ",
                ["Using the host-selected bench by default."] = "Используется скамейка, выбранная хостом.",
                ["Shared bench updated to the last bench used by the host."] = "Общая скамейка обновлена на последнюю скамейку хоста.",
                ["Only the host or an authorized admin can set the shared bench."] = "Выбирать общую скамейку может только хост или администратор.",
                ["Stand on or near a bench before using Set Here."] = "Встаньте на скамейку или рядом с ней перед выбором.",
                ["The selected bench is not in your current room."] = "Выбранная скамейка находится не в текущей комнате.",
                ["The nearby bench has not been verified in the discovered bench catalog."] = "Ближайшая скамейка не подтверждена каталогом обнаруженных скамеек.",
                ["Could not save the shared bench."] = "Не удалось сохранить общую скамейку.",
                ["That bench is not in the discovered bench catalog."] = "Этой скамейки нет в каталоге обнаруженных скамеек.",
                ["The server does not have a current position for your player."] = "Сервер не получил текущую позицию вашего игрока.",
                ["Unsupported bench settings request."] = "Неподдерживаемый запрос настроек скамейки.",
                ["Round settings updated."] = "Настройки раунда обновлены.",
                ["The round setting was not changed."] = "Настройка раунда не была изменена.",
                ["There is no active round to end."] = "Нет активного раунда для завершения.",
                ["Unsupported round setting."] = "Неподдерживаемая настройка раунда.",
                ["Invalid PvP mode."] = "Недопустимый режим PvP.",
                ["PvP mode can only be changed when no room has a round active or preparing."] = "Режим PvP можно изменить, только когда ни в одной комнате нет активного или подготавливаемого раунда.",
                ["PvP mode is already "] = "Режим PvP уже установлен: ",
                ["Could not persist the selected PvP mode."] = "Не удалось сохранить выбранный режим PvP.",
                ["PvP mode changed to "] = "Режим PvP изменён на ",
                ["PvP protection can only be changed when no room has a round active or preparing."] = "Защиту PvP можно изменить, только когда ни в одной комнате нет активного или подготавливаемого раунда.",
                ["The bench warp target is invalid."] = "Цель телепортации к скамейке недействительна.",
                ["Local travel started"] = "Локальный переход начат",
                ["RestBench Return"] = "Скамейка возврата",
                ["Configure PvP mode, round rules, statistics and the shared bench from the settings window."] = "Настройте режим PvP, правила раундов, статистику и общую скамейку в окне настроек.",
                ["PvP modes"] = "Режимы PvP",
                ["Quick disables healing and Focus."] = "Быстрый режим запрещает лечение и Фокус.",
                ["Long allows healing."] = "Долгий режим разрешает лечение.",
                ["OneShot uses one mask and server-managed SOUL."] = "OneShot оставляет одну маску, а Душой управляет сервер.",
                ["Quick mode"] = "Быстрый режим",
                ["Long mode"] = "Долгий режим",
                ["Live statistics"] = "Статистика в игре",
                ["Press the configured statistics hotkey (default: {STATS_KEY}) to show or hide the small statistics overlay while playing. Hold the same hotkey to enter edit mode, then drag the overlay to move it or drag any edge or corner to resize it. The overlay position and size are saved locally."] = "Нажмите назначенную клавишу статистики (по умолчанию: {STATS_KEY}), чтобы показать или скрыть небольшое окно статистики во время игры. Удерживайте эту клавишу, чтобы включить режим редактирования, затем перетащите окно для перемещения или тяните за край либо угол для изменения размера. Положение и размер окна сохраняются локально.",
                ["Benches"] = "Скамейки",
                ["Controls"] = "Управление",
                ["Press the configured menu hotkey (default: {MENU_KEY}) to open or close the settings window. Press **Esc** to close it. Drag the top area of the window to move it and drag the resize grip in the lower-left corner to resize it."] = "Нажмите назначенную клавишу меню (по умолчанию: {MENU_KEY}), чтобы открыть или закрыть окно настроек. Нажмите **Esc**, чтобы закрыть его. Перетаскивайте верхнюю часть окна для перемещения, а маркер в нижнем левом углу — для изменения размера.",
                ["OneShot mode"] = "OneShot",
                ["Press the configured menu hotkey (default: {MENU_KEY}) to open or close the settings window. Press Esc to close it. Drag the top area of the window to move it and drag the resize grip in the lower-left corner to resize it."] = "Нажмите назначенную клавишу меню (по умолчанию: {MENU_KEY}), чтобы открыть или закрыть окно настроек. Нажмите Esc, чтобы закрыть его. Перетаскивайте верхнюю часть окна для перемещения, а маркер в нижнем левом углу — для изменения размера.",
            };

        private static readonly Dictionary<string, string> English = BuildEnglish();

        private static Dictionary<string, string> BuildEnglish()
        {
            Dictionary<string, string> result = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, string> pair in Russian)
            {
                if (!result.ContainsKey(pair.Value))
                {
                    result.Add(pair.Value, pair.Key);
                }
            }
            return result;
        }

        private static string _language = "en";

        public static string Language
        {
            get { return _language; }
        }

        public static void SetLanguage(string language)
        {
            _language = string.Equals(language, "ru", StringComparison.OrdinalIgnoreCase) ? "ru" : "en";
        }

        public static string Translate(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            string translated;
            if (string.Equals(_language, "ru", StringComparison.Ordinal))
            {
                if (Russian.TryGetValue(value, out translated))
                {
                    return translated;
                }

                translated = TranslateFragments(value, true);
                if (!string.Equals(translated, value, StringComparison.Ordinal))
                {
                    return translated;
                }

                return value;
            }

            if (English.TryGetValue(value, out translated))
            {
                return translated;
            }

            if (Russian.ContainsKey(value))
            {
                return value;
            }

            translated = TranslateFragments(value, false);
            if (!string.Equals(translated, value, StringComparison.Ordinal))
            {
                return translated;
            }

            return value;
        }

        private static string TranslateFragments(string value, bool russian)
        {
            string result = value;
            KeyValuePair<string, string>[] pairs = (russian ? Russian : English)
                .Where(pair => pair.Key.Length >= 8 || IsShortFragment(pair.Key))
                .OrderByDescending(pair => pair.Key.Length)
                .ToArray();

            for (int i = 0; i < pairs.Length; i++)
            {
                string from = pairs[i].Key;
                string to = pairs[i].Value;
                if (!string.IsNullOrEmpty(from) && result.IndexOf(from, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    result = ReplaceIgnoreCase(result, from, to);
                }
            }

            return result;
        }

        private static bool IsShortFragment(string value)
        {
            return string.Equals(value, " wins", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, " побед", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, " at ", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, " в ", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, " must be a whole number.", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, " должно быть целым числом.", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, " must be between ", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, " должно быть в диапазоне от ", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, " and ", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, " до ", StringComparison.OrdinalIgnoreCase);
        }

        private static string ReplaceIgnoreCase(string source, string oldValue, string newValue)
        {
            int index = source.IndexOf(oldValue, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
            {
                return source;
            }

            return source.Substring(0, index) + newValue +
                   ReplaceIgnoreCase(source.Substring(index + oldValue.Length), oldValue, newValue);
        }
    }
}
