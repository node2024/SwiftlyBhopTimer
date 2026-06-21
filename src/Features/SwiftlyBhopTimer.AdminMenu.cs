using System.Globalization;
using SwiftlyBhopTimer.Services;
using SwiftlyS2.Core.Menus.OptionsBase;
using SwiftlyS2.Shared.Commands;
using SwiftlyS2.Shared.Menus;
using SwiftlyS2.Shared.Players;

namespace SwiftlyBhopTimer;

public sealed partial class SwiftlyBhopTimer
{
    private const string AdminStartZoneMenuColor = "#00ff00";
    private const string AdminEndZoneMenuColor = "#ff3333";

    private void ShowAdminMenu(ICommandContext context)
    {
        var player = context.Sender;
        if (player is null)
        {
            Reply(context, "[SwiftlyBhopTimer] This command can only be used by players.");
            return;
        }

        if (!IsAdmin(player))
        {
            Reply(context, "[SwiftlyBhopTimer] You do not have permission to open admin menu.");
            return;
        }

        ShowAdminMenu(player);
    }

    private void ShowAdminMenu(IPlayer player)
    {
        try
        {
            var menu = CreateOptionsMenu("Admin", maxVisibleItems: 8);
            AddAdminNavigationOption(menu, "Delete times", "current map", ShowAdminDeleteTimesMenu);
            AddAdminNavigationOption(menu, "Map zones", "set from your position", ShowAdminZonesMenu);
            AddAdminNavigationOption(menu, "Map chooser", "vote/change/extend", ShowAdminMapChooserMenu);
            AddAdminActionOption(menu, player, "Redraw beams", "start/end zones", clickedPlayer =>
            {
                ScheduleZoneRender("admin_menu", delayTicks: 1);
                SendChat(clickedPlayer, $"{label("Admin")} {label("|")} {green("zone beam redraw scheduled")}");
            });
            AddAdminActionOption(menu, player, "Apply cfg", "regenerate/apply", ApplyConfigFromAdminMenu);
            AddAdminToggleOption(menu, player, "Collision disable", _playerProtectionService?.DisableCollision ?? false, clickedPlayer =>
            {
                if (_playerProtectionService is null)
                {
                    SendChat(clickedPlayer, $"{label("Admin")} {label("|")} {red("protection service unavailable")}");
                    return;
                }

                _playerProtectionService.DisableCollision = !_playerProtectionService.DisableCollision;
                SendChat(clickedPlayer, $"{label("Collision")} {label("|")} {(_playerProtectionService.DisableCollision ? green("ON") : red("OFF"))}");
                Core.Scheduler.NextTick(() => ShowAdminMenu(clickedPlayer));
            });
            AddAdminToggleOption(menu, player, "Damage disable", _playerProtectionService?.DisableDamage ?? false, clickedPlayer =>
            {
                if (_playerProtectionService is null)
                {
                    SendChat(clickedPlayer, $"{label("Admin")} {label("|")} {red("protection service unavailable")}");
                    return;
                }

                _playerProtectionService.DisableDamage = !_playerProtectionService.DisableDamage;
                SendChat(clickedPlayer, $"{label("Damage")} {label("|")} {(_playerProtectionService.DisableDamage ? green("ON") : red("OFF"))}");
                Core.Scheduler.NextTick(() => ShowAdminMenu(clickedPlayer));
            });
            Core.MenusAPI.OpenMenuForPlayer(player, menu);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SwiftlyBhopTimer] Failed to open admin menu: {ex.Message}");
            SendChat(player, $"{label("Admin")} {label("|")} {red("menu unavailable")} {gray("Check server console for SwiftlyS2 menu API errors.")}");
        }
    }

    private void ShowAdminDeleteTimesMenu(IPlayer player)
    {
        if (_recordStore is null)
        {
            SendChat(player, $"{label("Delete time")} {label("|")} {red("record store unavailable")}");
            return;
        }

        try
        {
            var mode = GetPlayerTimerMode(player);
            var records = _recordStore.GetTopRecordsAsync(_currentMapName, 10, mode).GetAwaiter().GetResult();
            var menu = CreateOptionsMenu($"Delete {TimerRunModes.ToDisplayName(mode)}", maxVisibleItems: 8);
            if (records.Count == 0)
            {
                AddAdminNavigationOption(menu, "No records", _currentMapName, ShowAdminMenu);
            }
            else
            {
                for (var index = 0; index < records.Count; index++)
                {
                    var rank = index + 1;
                    AddDeleteTimeOption(menu, player, rank, records[index]);
                }
            }

            AddAdminNavigationOption(menu, "Back", "admin", ShowAdminMenu);
            Core.MenusAPI.OpenMenuForPlayer(player, menu);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SwiftlyBhopTimer] Failed to open delete times menu: {ex.Message}");
            SendChat(player, $"{label("Delete time")} {label("|")} {red("menu unavailable")}");
        }
    }

    private void ShowDeleteTimeConfirmMenu(IPlayer player, int rank, PlayerRecordEntry record)
    {
        try
        {
            var name = string.IsNullOrWhiteSpace(record.PlayerName) ? record.SteamId : record.PlayerName!;
            var menu = CreateOptionsMenu($"Delete #{rank}", maxVisibleItems: 4);
            AddAdminActionOption(menu, player, $"Delete #{rank}", TimeFormatter.FormatTicks(record.TimerTicks), clickedPlayer =>
            {
                DeleteRecordByRankFromAdminMenu(clickedPlayer, rank);
                Core.Scheduler.NextTick(() => ShowAdminDeleteTimesMenu(clickedPlayer));
            }, closeAfterClick: true);
            AddAdminNavigationOption(menu, $"Cancel {FitAdminMenuText(name)}", "back", ShowAdminDeleteTimesMenu);
            Core.MenusAPI.OpenMenuForPlayer(player, menu);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SwiftlyBhopTimer] Failed to open delete confirm menu: {ex.Message}");
            SendChat(player, $"{label("Delete time")} {label("|")} {red("confirm unavailable")}");
        }
    }

    private void ShowAdminZonesMenu(IPlayer player)
    {
        try
        {
            var menu = CreateOptionsMenu("Map Zones", maxVisibleItems: 8);
            AddMapPointOption(menu, "Set start corner 1", "MapStartC1", "start corner 1");
            AddMapPointOption(menu, "Set start corner 2", "MapStartC2", "start corner 2");
            AddMapPointOption(menu, "Set end corner 1", "MapEndC1", "end corner 1");
            AddMapPointOption(menu, "Set end corner 2", "MapEndC2", "end corner 2");
            AddMapPointOption(menu, "Set respawn", "RespawnPos", "respawn position");
            AddAdminNavigationOption(menu, "Bonus zones", "select bonus number", ShowAdminBonusNumberPrompt);
            AddAdminActionOption(menu, player, "Redraw beams", "start/end zones", clickedPlayer =>
            {
                ScheduleZoneRender("admin_zones_menu", delayTicks: 1);
                SendChat(clickedPlayer, $"{label("Zones")} {label("|")} {green("beam redraw scheduled")}");
            }, closeAfterClick: false);
            AddAdminNavigationOption(menu, "Back", "admin", ShowAdminMenu);
            Core.MenusAPI.OpenMenuForPlayer(player, menu);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SwiftlyBhopTimer] Failed to open admin zones menu: {ex.Message}");
            SendChat(player, $"{label("Zones")} {label("|")} {red("menu unavailable")}");
        }
    }

    private void ShowAdminBonusNumberPrompt(IPlayer player)
    {
        var state = _timerStateStore.GetOrCreate(player.Slot, player.SteamID.ToString(), player.Name);
        state.PendingAdminBonusSelectionTicks = AdminBonusSelectionTimeoutTicks;
        SendChat(player, $"{label("Bonus setup")} {label("|")} {green("type a bonus number from 1 to 99 in chat")} {gray("or cancel")}");
    }

    private void ShowAdminBonusZonesMenu(IPlayer player, int bonusNumber)
    {
        try
        {
            var menu = CreateOptionsMenu($"Bonus #{bonusNumber}", maxVisibleItems: 8);
            AddBonusMapPointOption(menu, bonusNumber, "Set start corner 1", "StartC1", "start corner 1");
            AddBonusMapPointOption(menu, bonusNumber, "Set start corner 2", "StartC2", "start corner 2");
            AddBonusMapPointOption(menu, bonusNumber, "Set end corner 1", "EndC1", "end corner 1");
            AddBonusMapPointOption(menu, bonusNumber, "Set end corner 2", "EndC2", "end corner 2");
            AddBonusMapPointOption(menu, bonusNumber, "Set respawn", "RespawnPos", "respawn position");
            AddAdminActionOption(menu, player, "Redraw beams", "all zones", clickedPlayer =>
            {
                ScheduleZoneRender("admin_bonus_zones_menu", delayTicks: 1);
                SendChat(clickedPlayer, $"{label("Bonus setup")} {gold($"#{bonusNumber}")} {label("|")} {green("beam redraw scheduled")}");
                Core.Scheduler.NextTick(() => ShowAdminBonusZonesMenu(clickedPlayer, bonusNumber));
            }, closeAfterClick: false);
            AddAdminNavigationOption(menu, "Select another", "1-99", ShowAdminBonusNumberPrompt);
            AddAdminNavigationOption(menu, "Back", "map zones", ShowAdminZonesMenu);
            Core.MenusAPI.OpenMenuForPlayer(player, menu);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SwiftlyBhopTimer] Failed to open admin bonus zones menu: {ex.Message}");
            SendChat(player, $"{label("Bonus setup")} {label("|")} {red("menu unavailable")}");
        }
    }

    private void AddDeleteTimeOption(IMenuAPI menu, IPlayer player, int rank, PlayerRecordEntry record)
    {
        var name = string.IsNullOrWhiteSpace(record.PlayerName) ? record.SteamId : record.PlayerName!;
        var option = new ButtonMenuOption($"#{rank} {TimeFormatter.FormatTicks(record.TimerTicks)}", 120, 1000)
        {
            Comment = FitAdminMenuText(name),
            CloseAfterClick = true
        };

        option.Click += (_, args) =>
        {
            ShowDeleteTimeConfirmMenu(args.Player, rank, record);
            return ValueTask.CompletedTask;
        };

        menu.AddOption(option);
    }

    private void AddMapPointOption(IMenuAPI menu, string text, string propertyName, string labelText)
    {
        var option = new ButtonMenuOption(GetAdminZoneOptionText(text, propertyName), 120, 1000)
        {
            Comment = "use current position",
            CloseAfterClick = false
        };

        option.Click += (_, args) =>
        {
            var state = _timerStateStore.GetOrCreate(args.Player.Slot, args.Player.SteamID.ToString(), args.Player.Name);
            if (state.AdminZoneSaveCooldownTicks > 0)
            {
                SendChat(args.Player, $"{label("Zones")} {label("|")} {gray("save already queued")}");
                return ValueTask.CompletedTask;
            }

            state.AdminZoneSaveCooldownTicks = AdminZoneSaveCooldownTicks;
            SaveMapPointFromPlayer(args.Player, propertyName, labelText, message => SendChat(args.Player, message));
            Core.Scheduler.NextTick(() => ShowAdminZonesMenu(args.Player));
            return ValueTask.CompletedTask;
        };

        menu.AddOption(option);
    }

    private void AddBonusMapPointOption(IMenuAPI menu, int bonusNumber, string text, string propertyName, string labelText)
    {
        var option = new ButtonMenuOption(GetAdminZoneOptionText(text, propertyName), 120, 1000)
        {
            Comment = "use current position",
            CloseAfterClick = false
        };

        option.Click += (_, args) =>
        {
            var state = _timerStateStore.GetOrCreate(args.Player.Slot, args.Player.SteamID.ToString(), args.Player.Name);
            if (state.AdminZoneSaveCooldownTicks > 0)
            {
                SendChat(args.Player, $"{label("Bonus setup")} {label("|")} {gray("save already queued")}");
                return ValueTask.CompletedTask;
            }

            state.AdminZoneSaveCooldownTicks = AdminZoneSaveCooldownTicks;
            SaveBonusMapPointFromPlayer(args.Player, bonusNumber, propertyName, labelText, message => SendChat(args.Player, message));
            Core.Scheduler.NextTick(() => ShowAdminBonusZonesMenu(args.Player, bonusNumber));
            return ValueTask.CompletedTask;
        };

        menu.AddOption(option);
    }


    private void AddAdminToggleOption(IMenuAPI menu, IPlayer player, string text, bool isEnabled, Action<IPlayer> action)
    {
        AddAdminActionOption(menu, player, $"{text}: {(isEnabled ? "ON" : "OFF")}", "toggle", action, closeAfterClick: false);
    }

    private void AddAdminNavigationOption(IMenuAPI menu, string text, string comment, Action<IPlayer> action)
    {
        var option = new ButtonMenuOption(text, 120, 1000)
        {
            Comment = comment,
            CloseAfterClick = true
        };

        option.Click += (_, args) =>
        {
            Core.Scheduler.NextTick(() => action(args.Player));
            return ValueTask.CompletedTask;
        };

        menu.AddOption(option);
    }

    private void AddAdminActionOption(
        IMenuAPI menu,
        IPlayer player,
        string text,
        string comment,
        Action<IPlayer> action,
        bool closeAfterClick = false)
    {
        var option = new ButtonMenuOption(text, 120, 1000)
        {
            Comment = comment,
            CloseAfterClick = closeAfterClick
        };

        option.Click += (_, args) =>
        {
            action(args.Player);
            return ValueTask.CompletedTask;
        };

        menu.AddOption(option);
    }

    private void ApplyConfigFromAdminMenu(IPlayer player)
    {
        if (_roundFlowConfigService is null)
        {
            SendChat(player, $"{label("Cfg")} {label("|")} {red("service unavailable")}");
            return;
        }

        try
        {
            _roundFlowConfigService.WriteConfig();
            _roundFlowConfigService.ApplyFull();
            SendChat(player, $"{label("Cfg")} {label("|")} {green("regenerated and applied")}");
        }
        catch (Exception ex)
        {
            SendChat(player, $"{label("Cfg")} {label("|")} {red("failed")} {gray(ex.Message)}");
        }
    }

    private void DeleteRecordByRankFromAdminMenu(IPlayer player, int rank)
    {
        if (_recordStore is null)
        {
            SendChat(player, $"{label("Delete time")} {label("|")} {red("record store unavailable")}");
            return;
        }

        var mode = GetPlayerTimerMode(player);
        var deleted = _recordStore.DeleteBestRecordByRankAsync(_currentMapName, rank, mode).GetAwaiter().GetResult();
        if (deleted is null)
        {
            SendChat(player, $"{label("Delete time")} {label("|")} {gold(TimerRunModes.ToDisplayName(mode))} {label("|")} {lightBlue(_currentMapName)} {red($"rank #{rank} not found")}");
            return;
        }

        var name = string.IsNullOrWhiteSpace(deleted.PlayerName) ? deleted.SteamId : deleted.PlayerName;
        var replayDeleted = deleted.Rank == 1 && (_replayService?.DeleteReplayIfMatches(deleted.MapName, deleted.Mode, deleted.SteamId, deleted.TimerTicks) ?? false);
        var replayPart = replayDeleted ? $" {label("|")} {gray("SR replay removed")}" : "";
        SendChat(player, $"{label("Delete time")} {label("|")} {gold(TimerRunModes.ToDisplayName(deleted.Mode))} {label("|")} {lightBlue(deleted.MapName)} {topPlacement(deleted.Rank)} {white(TimeFormatter.FormatTicks(deleted.TimerTicks))} {gray(name)} {green("deleted")}{replayPart}");
    }

    private static string FitAdminMenuText(string value)
    {
        const int maxLength = 48;
        if (string.IsNullOrWhiteSpace(value))
        {
            return "unknown";
        }

        return value.Length <= maxLength ? value : value[..maxLength].TrimEnd();
    }

    private static string GetAdminZoneOptionText(string text, string propertyName)
    {
        if (propertyName.Contains("Start", StringComparison.OrdinalIgnoreCase))
        {
            return $"<font color='{AdminStartZoneMenuColor}'>{text}</font>";
        }

        if (propertyName.Contains("End", StringComparison.OrdinalIgnoreCase))
        {
            return $"<font color='{AdminEndZoneMenuColor}'>{text}</font>";
        }

        return text;
    }
}
