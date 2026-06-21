using System.Globalization;
using SwiftlyBhopTimer.Services;
using SwiftlyS2.Core.Menus.OptionsBase;
using SwiftlyS2.Shared.Commands;
using SwiftlyS2.Shared.Menus;
using SwiftlyS2.Shared.Players;

namespace SwiftlyBhopTimer;

public sealed partial class SwiftlyBhopTimer
{
    private void ProcessMapChooser(IReadOnlyList<IPlayer> players)
    {
        if (_mapChooserService is null || !_mapChooserService.Enabled)
        {
            return;
        }

        var action = _mapChooserService.Tick(DateTime.UtcNow, players.Count);
        HandleMapChooserAction(action, players);
    }

    private void HandleMapChooserAction(MapChooserAction action, IReadOnlyList<IPlayer>? players = null)
    {
        if (action.Kind is MapChooserActionKind.None || _mapChooserService is null)
        {
            return;
        }

        switch (action.Kind)
        {
            case MapChooserActionKind.Reminder:
                SendChatAll($"{mcLabel("Map")} {mcSeparator()} {mcValue(_mapChooserService.FormatTimeLeft(DateTime.UtcNow))} {mcMuted("remaining")}");
                break;

            case MapChooserActionKind.VoteStarted:
                if (action.Vote is null)
                {
                    return;
                }

                SendChatAll($"{mcLabel("Map vote")} {mcSeparator()} {mcValue("started")} {mcMuted($"reason: {action.Vote.Reason}")} {mcSeparator()} {mcExtend("extend available")}");
                OpenMapVoteForAll(players);
                break;

            case MapChooserActionKind.Extended:
                ApplyMapChooserTimeLimitToHelper();
                SendChatAll($"{mcLabel("Map")} {mcSeparator()} {mcExtend("extended")} {mcMuted($"+{action.Minutes:0.#}m")} {mcSeparator()} {mcMuted($"total {_mapChooserService.FormatTotalTimeLimit()}")}");
                break;

            case MapChooserActionKind.MapSelected:
                if (action.Map is null)
                {
                    return;
                }

                SendChatAll($"{mcLabel("Next map")} {mcSeparator()} {mcValue(GetMapDisplayName(action.Map))} {mcMuted($"in {action.Seconds}s")}");
                break;

            case MapChooserActionKind.ChangeMap:
                if (action.Map is null)
                {
                    return;
                }

                SendChatAll($"{mcLabel("Changing map")} {mcSeparator()} {mcValue(GetMapDisplayName(action.Map))}");
                ExecuteMapChangeLikeMapChooser(action.Map, action.Command);
                break;
        }
    }

    private string mcLabel(string value) => MapChooserColor(_mapChooserService?.Config.ChatColors.Label, value, "{lightblue}");

    private string mcValue(string value) => MapChooserColor(_mapChooserService?.Config.ChatColors.Value, value, "{green}");

    private string mcAccent(string value) => MapChooserColor(_mapChooserService?.Config.ChatColors.Accent, value, "{gold}");

    private string mcExtend(string value) => MapChooserColor(_mapChooserService?.Config.ChatColors.Extend, value, "{gold}");

    private string mcMuted(string value) => MapChooserColor(_mapChooserService?.Config.ChatColors.Muted, value, "{gray}");

    private string mcError(string value) => MapChooserColor(_mapChooserService?.Config.ChatColors.Error, value, "{red}");

    private string mcSeparator() => mcLabel("|");

    private static string MapChooserColor(string? color, string value, string fallback)
    {
        var tag = NormalizeMapChooserColor(color, fallback);
        return $"{tag}{value}{{default}}";
    }

    private static string NormalizeMapChooserColor(string? color, string fallback)
    {
        if (string.IsNullOrWhiteSpace(color))
        {
            return fallback;
        }

        var trimmed = color.Trim();
        return trimmed.StartsWith('{') && trimmed.EndsWith('}')
            ? trimmed
            : $"{{{trimmed}}}";
    }

    private void ApplyMapChooserTimeLimitToHelper()
    {
        if (_mapChooserService is null)
        {
            return;
        }

        var minutes = _mapChooserService.TotalTimeLimitMinutes.ToString("0.###", CultureInfo.InvariantCulture);
        ExecuteServerCommand($"sbt_timelimit_set {minutes}");
    }

    private void ExecuteMapChangeLikeMapChooser(MapChooserMapEntry map, string? changeCommand)
    {
        Core.Scheduler.NextTick(() =>
        {
            ExecuteServerCommandWithBuffer($"nextlevel {map.Name}");

            if (!string.IsNullOrWhiteSpace(changeCommand))
            {
                ExecuteServerCommandWithBuffer(changeCommand.Trim());
                return;
            }

            if (!string.IsNullOrWhiteSpace(map.WorkshopId))
            {
                ExecuteServerCommandWithBuffer($"host_workshop_map {NormalizeWorkshopId(map.WorkshopId)}");
                return;
            }

            ExecuteServerCommandWithBuffer($"changelevel {map.Name}");
        });
    }

    private static string NormalizeWorkshopId(string workshopId)
    {
        var trimmed = workshopId.Trim();
        return trimmed.StartsWith("ws:", StringComparison.OrdinalIgnoreCase) ? trimmed[3..].Trim() : trimmed;
    }

    private void OnRtvCommand(ICommandContext context)
    {
        if (_mapChooserService is null || !_mapChooserService.Enabled)
        {
            Reply(context, $"{mcLabel("RTV")} {mcSeparator()} {mcError("map chooser disabled")}");
            return;
        }

        var player = context.Sender;
        if (player is null)
        {
            Reply(context, "[SwiftlyBhopTimer] This command can only be used by players.");
            return;
        }

        var players = Core.PlayerManager.GetAllValidPlayers().Where(IsPlayablePlayer).ToList();
        var result = _mapChooserService.AddRtv(player.SteamID.ToString(), DateTime.UtcNow, players.Count);
        if (result.Reason == "vote-active")
        {
            SendChat(player, $"{mcLabel("RTV")} {mcSeparator()} {mcMuted("vote already running")}");
            ShowMapVoteMenu(player);
            return;
        }

        if (result.Reason == "already")
        {
            SendChat(player, $"{mcLabel("RTV")} {mcSeparator()} {mcMuted($"{result.Current}/{result.Required} already counted")}");
            return;
        }

        if (result.StartedVote)
        {
            SendChatAll($"{mcLabel("RTV")} {mcSeparator()} {mcValue("vote threshold reached")}");
            HandleMapChooserAction(result.Action, players);
            return;
        }

        SendChatAll($"{mcLabel("RTV")} {mcSeparator()} {mcValue(player.Name)} {mcMuted($"{result.Current}/{result.Required}")}");
    }

    private void OnNominateCommand(ICommandContext context)
    {
        var player = context.Sender;
        if (player is null)
        {
            Reply(context, "[SwiftlyBhopTimer] This command can only be used by players.");
            return;
        }

        if (_mapChooserService is null || !_mapChooserService.Enabled)
        {
            SendChat(player, $"{mcLabel("Nominate")} {mcSeparator()} {mcError("map chooser disabled")}");
            return;
        }

        if (context.Args.Length == 0)
        {
            ShowMapListMenu(player, adminChange: false);
            return;
        }

        var query = string.Join(' ', context.Args);
        NominateMap(player, query);
    }

    private void OnMapsCommand(ICommandContext context)
    {
        var player = context.Sender;
        if (player is null)
        {
            Reply(context, "[SwiftlyBhopTimer] This command can only be used by players.");
            return;
        }

        if (_mapChooserService is null || !_mapChooserService.Enabled)
        {
            SendChat(player, $"{mcLabel("Maps")} {mcSeparator()} {mcError("map chooser disabled")}");
            return;
        }

        ShowMapListMenu(player, adminChange: IsAdmin(player));
    }

    private void OnTierCommand(ICommandContext context)
    {
        if (_mapChooserService is null || !_mapChooserService.Enabled)
        {
            Reply(context, $"{label("Tier")} {label("|")} {red("map chooser disabled")}");
            return;
        }

        var query = context.Args.Length > 0
            ? string.Join(' ', context.Args)
            : _currentMapName;
        var map = _mapChooserService.FindMap(query);
        if (map is null)
        {
            Reply(context, $"{label("Tier")} {label("|")} {red("map not found")} {gray(query)}");
            return;
        }

        Reply(context, $"{label("Tier")} {label("|")} {green(GetMapDisplayName(map))} {gold(GetMapTierText(map))}");
    }

    private void OnTimeleftCommand(ICommandContext context)
    {
        if (_mapChooserService is null || !_mapChooserService.Enabled)
        {
            Reply(context, $"{mcLabel("Timeleft")} {mcSeparator()} {mcError("map chooser disabled")}");
            return;
        }

        Reply(context, $"{mcLabel("Timeleft")} {mcSeparator()} {mcValue(_mapChooserService.FormatTimeLeft(DateTime.UtcNow))} {mcMuted($"of {_mapChooserService.FormatTotalTimeLimit()}")}");
    }

    private void OnNextMapCommand(ICommandContext context)
    {
        if (_mapChooserService?.NextMap is not { } nextMap)
        {
            Reply(context, $"{mcLabel("Next map")} {mcSeparator()} {mcMuted("not selected")}");
            return;
        }

        Reply(context, $"{mcLabel("Next map")} {mcSeparator()} {mcValue(GetMapDisplayName(nextMap))}");
    }

    private void OnMapVoteCommand(ICommandContext context)
    {
        if (!TryGetAdminSender(context, "start a map vote", out var player))
        {
            return;
        }

        if (_mapChooserService is null || !_mapChooserService.Enabled)
        {
            SendChat(player, $"{mcLabel("Map vote")} {mcSeparator()} {mcError("map chooser disabled")}");
            return;
        }

        var players = Core.PlayerManager.GetAllValidPlayers().Where(IsPlayablePlayer).ToList();
        HandleMapChooserAction(_mapChooserService.StartVote("admin", DateTime.UtcNow), players);
    }

    private void OnChangeMapCommand(ICommandContext context)
    {
        if (!TryGetAdminSender(context, "change map", out var player))
        {
            return;
        }

        var query = context.Args.Length == 0 ? "" : string.Join(' ', context.Args);
        ChangeMapFromAdmin(player, query);
    }

    private void ChangeMapFromAdmin(IPlayer player, string query)
    {
        if (_mapChooserService is null || !_mapChooserService.Enabled)
        {
            SendChat(player, $"{mcLabel("Change map")} {mcSeparator()} {mcError("map chooser disabled")}");
            return;
        }

        query = TrimQuotedMapQuery(query);
        if (string.IsNullOrWhiteSpace(query))
        {
            ShowMapListMenu(player, adminChange: true);
            return;
        }

        var map = _mapChooserService.FindMap(query);
        if (map is null)
        {
            SendChat(player, $"{mcLabel("Change map")} {mcSeparator()} {mcError("map not found")} {mcMuted(query)}");
            return;
        }

        HandleMapChooserAction(_mapChooserService.ChangeMapNow(map));
    }

    private static string TrimQuotedMapQuery(string value)
    {
        var trimmed = value.Trim();
        return trimmed.Length >= 2 && trimmed[0] == '"' && trimmed[^1] == '"'
            ? trimmed[1..^1].Trim()
            : trimmed;
    }

    private void OnExtendMapCommand(ICommandContext context)
    {
        if (!TryGetAdminSender(context, "extend map", out var player))
        {
            return;
        }

        if (_mapChooserService is null || !_mapChooserService.Enabled)
        {
            SendChat(player, $"{mcLabel("Extend")} {mcSeparator()} {mcError("map chooser disabled")}");
            return;
        }

        var action = _mapChooserService.Extend(DateTime.UtcNow, "admin");
        if (action.Kind == MapChooserActionKind.None)
        {
            SendChat(player, $"{mcLabel("Extend")} {mcSeparator()} {mcError("no extends remaining")}");
            return;
        }

        HandleMapChooserAction(action);
    }

    private void OnMapTierCommand(ICommandContext context)
    {
        if (!TryGetAdminSender(context, "set map tier", out var player))
        {
            return;
        }

        if (_mapChooserService is null || !_mapChooserService.Enabled)
        {
            SendChat(player, $"{label("Map tier")} {label("|")} {red("map chooser disabled")}");
            return;
        }

        if (!TryParseMapTierArgs(context.Args, out var mapQuery, out var tier))
        {
            SendChat(player, $"{label("Map tier")} {label("|")} {gray($"usage: !maptier <map> <0-{MapChooserService.MaxTier}> or !maptier <0-{MapChooserService.MaxTier}> for current map")}");
            return;
        }

        SetMapTierFromAdmin(player, mapQuery, tier);
    }

    private void OnAddMapCommand(ICommandContext context)
    {
        if (!TryGetAdminSender(context, "add map", out var player))
        {
            return;
        }

        if (_mapChooserService is null || !_mapChooserService.Enabled)
        {
            SendChat(player, $"{label("Add map")} {label("|")} {red("map chooser disabled")}");
            return;
        }

        if (!TryParseAddMapArgs(context.Args, out var mapName, out var workshopId, out var tier))
        {
            SendChat(player, $"{label("Add map")} {label("|")} {gray($"usage: !addmap <map> <workshopId> [0-{MapChooserService.MaxTier}]")}");
            return;
        }

        var map = _mapChooserService.AddOrUpdateMap(mapName, workshopId, tier, out var added);
        var result = added ? green("added") : gold("updated");
        SendChat(player, $"{label("Add map")} {label("|")} {green(GetMapDisplayName(map))} {result} {label("|")} {gray($"workshop {map.WorkshopId}")} {label("|")} {gold(GetMapTierText(map))}");
    }

    private void ShowAdminMapChooserMenu(IPlayer player)
    {
        if (_mapChooserService is null || !_mapChooserService.Enabled)
        {
            SendChat(player, $"{mcLabel("Map chooser")} {mcSeparator()} {mcError("disabled")}");
            return;
        }

        try
        {
            var menu = CreateOptionsMenu("Map Chooser", maxVisibleItems: 7);
            AddAdminActionOption(menu, player, "Start vote", "players choose", clickedPlayer =>
            {
                var players = Core.PlayerManager.GetAllValidPlayers().Where(IsPlayablePlayer).ToList();
                HandleMapChooserAction(_mapChooserService.StartVote("admin", DateTime.UtcNow), players);
            }, closeAfterClick: true);
            AddAdminActionOption(menu, player, "Extend map", _mapChooserService.CanExtend ? "add time" : "no extends left", clickedPlayer =>
            {
                var action = _mapChooserService.Extend(DateTime.UtcNow, "admin");
                if (action.Kind == MapChooserActionKind.None)
                {
                    SendChat(clickedPlayer, $"{mcLabel("Extend")} {mcSeparator()} {mcError("no extends remaining")}");
                    return;
                }

                HandleMapChooserAction(action);
            });
            AddAdminNavigationOption(menu, "Change map", "select map", clickedPlayer => ShowMapListMenu(clickedPlayer, adminChange: true));
            AddAdminNavigationOption(menu, "Set map tier", _currentMapName, clickedPlayer => ShowMapTierValueMenu(clickedPlayer, _currentMapName));
            AddAdminNavigationOption(menu, "Back", "admin", ShowAdminMenu);
            Core.MenusAPI.OpenMenuForPlayer(player, menu);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SwiftlyBhopTimer] Failed to open map chooser admin menu: {ex.Message}");
            SendChat(player, $"{mcLabel("Map chooser")} {mcSeparator()} {mcError("menu unavailable")}");
        }
    }

    private void ShowMapTierValueMenu(IPlayer player, string mapName)
    {
        if (_mapChooserService?.FindMap(mapName) is not { } map)
        {
            SendChat(player, $"{label("Map tier")} {label("|")} {red("map not found")} {gray(mapName)}");
            return;
        }

        try
        {
            var menu = CreateOptionsMenu($"Tier {FitAdminMenuText(GetMapDisplayName(map))}", maxVisibleItems: 8);
            for (var tier = 0; tier <= MapChooserService.MaxTier; tier++)
            {
                AddMapTierValueOption(menu, map.Name, tier, tier == map.Tier);
            }

            AddAdminNavigationOption(menu, "Back", "map chooser", ShowAdminMapChooserMenu);
            Core.MenusAPI.OpenMenuForPlayer(player, menu);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SwiftlyBhopTimer] Failed to open map tier value menu: {ex.Message}");
            SendChat(player, $"{label("Map tier")} {label("|")} {red("menu unavailable")}");
        }
    }

    private void AddMapTierValueOption(IMenuAPI menu, string mapName, int tier, bool selected)
    {
        var text = MapChooserService.FormatTier(tier);
        var option = new ButtonMenuOption(text, 120, 1000)
        {
            Comment = selected ? "current" : "set",
            CloseAfterClick = false
        };

        option.Click += (_, args) =>
        {
            SetMapTierFromAdmin(args.Player, mapName, tier);
            Core.Scheduler.NextTick(() => ShowMapTierValueMenu(args.Player, mapName));
            return ValueTask.CompletedTask;
        };

        menu.AddOption(option);
    }

    private void OpenMapVoteForAll(IReadOnlyList<IPlayer>? players)
    {
        foreach (var player in players ?? Core.PlayerManager.GetAllValidPlayers().Where(IsPlayablePlayer))
        {
            ShowMapVoteMenu(player);
        }
    }

    private void ShowMapVoteMenu(IPlayer player)
    {
        var vote = _mapChooserService?.ActiveVote;
        if (vote is null)
        {
            SendChat(player, $"{mcLabel("Map vote")} {mcSeparator()} {mcMuted("no active vote")}");
            return;
        }

        try
        {
            var menu = CreateOptionsMenu("Map Vote", maxVisibleItems: 8);
            foreach (var voteOption in vote.Options)
            {
                AddMapVoteOption(menu, player, voteOption);
            }

            Core.MenusAPI.OpenMenuForPlayer(player, menu);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SwiftlyBhopTimer] Failed to open map vote menu: {ex.Message}");
            SendChat(player, $"{mcLabel("Map vote")} {mcSeparator()} {mcError("menu unavailable")}");
        }
    }

    private void AddMapVoteOption(IMenuAPI menu, IPlayer player, MapVoteOption voteOption)
    {
        var option = new ButtonMenuOption(120, 1000)
        {
            Text = GetMapVoteOptionText(voteOption),
            BindingText = () => GetMapVoteOptionText(voteOption),
            Comment = voteOption.IsExtend ? "extend" : voteOption.MapName,
            CloseAfterClick = true
        };

        option.Click += (_, args) =>
        {
            if (_mapChooserService?.CastVote(args.Player.SteamID.ToString(), voteOption.Id, out var votedOption) == true && votedOption is not null)
            {
                var votedText = votedOption.IsExtend ? mcExtend(votedOption.Text) : mcValue(votedOption.Text);
                SendChat(args.Player, $"{mcLabel("Vote")} {mcSeparator()} {votedText}");
            }

            return ValueTask.CompletedTask;
        };

        menu.AddOption(option);
    }

    private static string GetMapVoteOptionText(MapVoteOption option)
    {
        return $"{option.Text} [{option.Votes}]";
    }

    private void ShowMapListMenu(IPlayer player, bool adminChange)
    {
        if (_mapChooserService is null)
        {
            return;
        }

        try
        {
            var menu = CreateOptionsMenu(adminChange ? "Change Map" : "Nominate", maxVisibleItems: 8);
            foreach (var map in _mapChooserService.Maps.OrderBy(map => map.DisplayName, StringComparer.OrdinalIgnoreCase))
            {
                AddMapListOption(menu, player, map, adminChange);
            }

            Core.MenusAPI.OpenMenuForPlayer(player, menu);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SwiftlyBhopTimer] Failed to open map list menu: {ex.Message}");
            SendChat(player, $"{label("Maps")} {label("|")} {red("menu unavailable")}");
        }
    }

    private void AddMapListOption(IMenuAPI menu, IPlayer player, MapChooserMapEntry map, bool adminChange)
    {
        var option = new ButtonMenuOption(GetMapDisplayName(map), 120, 1000)
        {
            Comment = adminChange ? $"{GetMapTierText(map)} | change now" : $"{GetMapTierText(map)} | nominate",
            CloseAfterClick = false
        };

        option.Click += (_, args) =>
        {
            if (_mapChooserService is null)
            {
                return ValueTask.CompletedTask;
            }

            var selectedMap = map;
            Core.Scheduler.NextTick(() =>
            {
                Console.WriteLine($"[SwiftlyBhopTimer] Map list menu clicked. Player={args.Player.Name}; Map={selectedMap.Name}; AdminChange={adminChange}");
                if (adminChange)
                {
                    ChangeMapFromAdminMenuSelection(args.Player, selectedMap);
                }
                else
                {
                    NominateMap(args.Player, selectedMap.Name);
                }

                var currentMenu = Core.MenusAPI.GetCurrentMenu(args.Player);
                if (currentMenu is not null)
                {
                    Core.MenusAPI.CloseMenuForPlayer(args.Player, currentMenu);
                }
            });

            return ValueTask.CompletedTask;
        };

        menu.AddOption(option);
    }

    private void ChangeMapFromAdminMenuSelection(IPlayer player, MapChooserMapEntry map)
    {
        if (_mapChooserService is null)
        {
            return;
        }

        SendChat(player, $"{mcLabel("Change map")} {mcSeparator()} {mcValue(GetMapDisplayName(map))} {mcMuted("selected")}");
        Core.Scheduler.NextTick(() =>
        {
            if (_mapChooserService is null)
            {
                return;
            }

            var selectedMap = _mapChooserService.FindMap(map.Name) ?? map;
            Console.WriteLine($"[SwiftlyBhopTimer] Admin map menu selected {selectedMap.Name}; executing immediate map change.");
            HandleMapChooserAction(_mapChooserService.ChangeMapNow(selectedMap));
        });
    }

    private void NominateMap(IPlayer player, string query)
    {
        if (_mapChooserService is null)
        {
            return;
        }

        if (!_mapChooserService.TryNominate(player.SteamID.ToString(), query, out var map) || map is null)
        {
            SendChat(player, $"{label("Nominate")} {label("|")} {red("map not found")} {gray(query)}");
            return;
        }

        SendChatAll($"{label("Nominate")} {label("|")} {green(player.Name)} {gray("nominated")} {green(GetMapDisplayName(map))}");
    }

    private bool TryParseMapTierArgs(string[] args, out string mapQuery, out int tier)
    {
        mapQuery = "";
        tier = 0;

        if (args.Length == 0)
        {
            return false;
        }

        if (args.Length == 1 && int.TryParse(args[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out tier))
        {
            mapQuery = _currentMapName;
            return tier is >= 0 && tier <= MapChooserService.MaxTier;
        }

        if (!int.TryParse(args[^1], NumberStyles.Integer, CultureInfo.InvariantCulture, out tier) || tier is < 0 || tier > MapChooserService.MaxTier)
        {
            return false;
        }

        mapQuery = string.Join(' ', args.Take(args.Length - 1)).Trim();
        return !string.IsNullOrWhiteSpace(mapQuery);
    }

    private static bool TryParseAddMapArgs(string[] args, out string mapName, out string workshopId, out int tier)
    {
        mapName = "";
        workshopId = "";
        tier = 0;

        if (args.Length is < 2 or > 3)
        {
            return false;
        }

        mapName = args[0].Trim();
        workshopId = args[1].Trim();
        if (string.IsNullOrWhiteSpace(mapName) || string.IsNullOrWhiteSpace(workshopId))
        {
            return false;
        }

        if (args.Length == 2)
        {
            return true;
        }

        return int.TryParse(args[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out tier) &&
               tier >= 0 &&
               tier <= MapChooserService.MaxTier;
    }

    private void SetMapTierFromAdmin(IPlayer player, string mapQuery, int tier)
    {
        if (_mapChooserService is null)
        {
            return;
        }

        if (!_mapChooserService.TrySetMapTier(mapQuery, tier, out var map) || map is null)
        {
            SendChat(player, $"{label("Map tier")} {label("|")} {red("map not found")} {gray(mapQuery)}");
            return;
        }

        SendChat(player, $"{label("Map tier")} {label("|")} {green(GetMapDisplayName(map))} {gray("set to")} {gold(GetMapTierText(map))}");
    }

    private bool TryGetAdminSender(ICommandContext context, string action, out IPlayer player)
    {
        player = null!;
        if (context.Sender is null)
        {
            Reply(context, "[SwiftlyBhopTimer] This command can only be used by players.");
            return false;
        }

        player = context.Sender;
        if (!IsAdmin(player))
        {
            Reply(context, $"[SwiftlyBhopTimer] You do not have permission to {action}.");
            return false;
        }

        return true;
    }

    private static string GetMapDisplayName(MapChooserMapEntry map)
    {
        return string.IsNullOrWhiteSpace(map.DisplayName) ? map.Name : map.DisplayName;
    }

    private static string GetMapTierText(MapChooserMapEntry map)
    {
        return MapChooserService.FormatTier(map.Tier);
    }
}
