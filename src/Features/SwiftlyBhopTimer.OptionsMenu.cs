using System.Globalization;
using SwiftlyBhopTimer.Services;
using SwiftlyS2.Core.Menus.OptionsBase;
using SwiftlyS2.Shared.Commands;
using SwiftlyS2.Shared.Menus;
using SwiftlyS2.Shared.Players;

namespace SwiftlyBhopTimer;

public sealed partial class SwiftlyBhopTimer
{
    private static readonly int[] FovPresetValues = [85, 90, 100, 110, 120, 130];

    private void ShowOptionsMenu(ICommandContext context)
    {
        var player = context.Sender;
        if (player is null)
        {
            Reply(context, "[SwiftlyBhopTimer] This command can only be used by players.");
            return;
        }

        ShowOptionsMenu(player);
    }

    private void ShowOptionsMenu(IPlayer player)
    {
        var state = _timerStateStore.GetOrCreate(player.Slot, player.SteamID.ToString(), player.Name);

        try
        {
            var menu = CreateOptionsMenu("Options", maxVisibleItems: 7);

            AddToggleOption(menu, player, "Hide legs", toggledState => toggledState.HideLegs, enabledLabel: "ON", disabledLabel: "OFF", (clickedPlayer, toggledState) =>
            {
                toggledState.HideLegs = !toggledState.HideLegs;
                _playerVisualService?.ApplySelfVisuals(clickedPlayer, toggledState);
                SavePlayerSettings(toggledState);
                SendChat(clickedPlayer, $"{label("Hide legs")} {label("|")} {(toggledState.HideLegs ? green("ON") : red("OFF"))}");
            });

            AddToggleOption(menu, player, "Hide players", toggledState => toggledState.HidePlayers, enabledLabel: "ON", disabledLabel: "OFF", (clickedPlayer, toggledState) =>
            {
                toggledState.HidePlayers = !toggledState.HidePlayers;
                _playerVisualService?.ApplyPlayerHiding(clickedPlayer, toggledState, GetHideTargetPlayers());
                SavePlayerSettings(toggledState);
                SendChat(clickedPlayer, $"{label("Hide players")} {label("|")} {(toggledState.HidePlayers ? green("ON") : red("OFF"))}");
            });

            AddToggleOption(menu, player, "Hide viewmodel", toggledState => toggledState.HideFpsViewModel, enabledLabel: "ON", disabledLabel: "OFF", (clickedPlayer, toggledState) =>
            {
                toggledState.HideFpsViewModel = !toggledState.HideFpsViewModel;
                Core.Scheduler.NextTick(() => ApplyHideFpsWithRespawnRefresh(clickedPlayer, toggledState));
                SavePlayerSettings(toggledState);
                SendChat(clickedPlayer, $"{label("Hide FPS")} {label("|")} {(toggledState.HideFpsViewModel ? green("ON") : red("OFF"))}");
            });

            AddFovMenuOption(menu, player, state);
            AddModeMenuOption(menu, player);

            AddToggleOption(menu, player, "Sounds", toggledState => toggledState.SoundsEnabled, enabledLabel: "ON", disabledLabel: "OFF", (clickedPlayer, toggledState) =>
            {
                toggledState.SoundsEnabled = !toggledState.SoundsEnabled;
                SavePlayerSettings(toggledState);
                SendChat(clickedPlayer, $"{label("Sounds")} {label("|")} {(toggledState.SoundsEnabled ? green("ON") : red("OFF"))}");
            });

            Core.MenusAPI.OpenMenuForPlayer(player, menu);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SwiftlyBhopTimer] Failed to open options menu: {ex.Message}");
            SendChat(player, $"{label("Options")} {label("|")} {red("menu unavailable")} {gray("Check server console for SwiftlyS2 menu API errors.")}");
        }
    }

    private void ShowFovOptionsMenu(IPlayer player)
    {
        var state = _timerStateStore.GetOrCreate(player.Slot, player.SteamID.ToString(), player.Name);

        try
        {
            var menu = CreateOptionsMenu("FOV", maxVisibleItems: 8);
            AddFovStepOption(menu, player, state, -5);
            AddFovStepOption(menu, player, state, +5);

            foreach (var preset in FovPresetValues)
            {
                AddFovSetOption(menu, player, state, preset);
            }

            AddBackOption(menu, player);
            Core.MenusAPI.OpenMenuForPlayer(player, menu);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SwiftlyBhopTimer] Failed to open FOV options menu: {ex.Message}");
            SendChat(player, $"{label("FOV")} {label("|")} {red("menu unavailable")} {gray("Check server console for SwiftlyS2 menu API errors.")}");
        }
    }

    private IMenuAPI CreateOptionsMenu(string title, int maxVisibleItems)
    {
        var configuration = new MenuConfiguration
        {
            Title = title,
            MaxVisibleItems = maxVisibleItems,
            HideFooter = false,
            HideComment = false,
            FreezePlayer = false,
            AutoCloseAfter = 30
        };

        return Core.MenusAPI.CreateMenu(
            configuration,
            new MenuKeybindOverrides(),
            null!,
            MenuOptionScrollStyle.CenterFixed,
            MenuOptionTextStyle.TruncateEnd);
    }

    private void AddToggleOption(
        IMenuAPI menu,
        IPlayer player,
        string name,
        Func<PlayerTimerState, bool> isEnabled,
        string enabledLabel,
        string disabledLabel,
        Action<IPlayer, PlayerTimerState> toggle,
        bool closeAfterClick = false)
    {
        var option = new ButtonMenuOption(120, 1000)
        {
            Text = GetToggleOptionText(player, name, isEnabled, enabledLabel, disabledLabel),
            BindingText = () => GetToggleOptionText(player, name, isEnabled, enabledLabel, disabledLabel),
            Comment = "toggle",
            CloseAfterClick = closeAfterClick
        };

        option.Click += (_, args) =>
        {
            var clickedState = _timerStateStore.GetOrCreate(args.Player.Slot, args.Player.SteamID.ToString(), args.Player.Name);
            toggle(args.Player, clickedState);
            return ValueTask.CompletedTask;
        };

        menu.AddOption(option);
    }

    private string GetToggleOptionText(
        IPlayer player,
        string name,
        Func<PlayerTimerState, bool> isEnabled,
        string enabledLabel,
        string disabledLabel)
    {
        var state = _timerStateStore.GetOrCreate(player.Slot, player.SteamID.ToString(), player.Name);
        return $"{name}: {(isEnabled(state) ? enabledLabel : disabledLabel)}";
    }

    private void AddFovMenuOption(IMenuAPI menu, IPlayer player, PlayerTimerState state)
    {
        var option = new ButtonMenuOption(120, 1000)
        {
            Text = GetFovOptionText(player),
            BindingText = () => GetFovOptionText(player),
            Comment = $"{MinPlayerFov}-{MaxPlayerFov}",
            CloseAfterClick = true
        };

        option.Click += (_, args) =>
        {
            Core.Scheduler.NextTick(() => ShowFovOptionsMenu(args.Player));
            return ValueTask.CompletedTask;
        };

        menu.AddOption(option);
    }

    private void AddModeMenuOption(IMenuAPI menu, IPlayer player)
    {
        var option = new ButtonMenuOption(120, 1000)
        {
            Text = GetModeOptionText(player),
            BindingText = () => GetModeOptionText(player),
            Comment = "select",
            CloseAfterClick = true
        };

        option.Click += (_, args) =>
        {
            Core.Scheduler.NextTick(() => ShowModeOptionsMenu(args.Player));
            return ValueTask.CompletedTask;
        };

        menu.AddOption(option);
    }

    private void ShowModeOptionsMenu(IPlayer player)
    {
        try
        {
            var menu = CreateOptionsMenu("Mode", maxVisibleItems: 4);
            AddModeSetOption(menu, player, TimerRunMode.Standard);
            if (TimerRunModes.ClassicEnabled)
            {
                AddModeSetOption(menu, player, TimerRunMode.Classic);
            }

            AddBackOption(menu, player);
            Core.MenusAPI.OpenMenuForPlayer(player, menu);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SwiftlyBhopTimer] Failed to open Mode options menu: {ex.Message}");
            SendChat(player, $"{label("Mode")} {label("|")} {red("menu unavailable")} {gray("Check server console for SwiftlyS2 menu API errors.")}");
        }
    }

    private void AddModeSetOption(IMenuAPI menu, IPlayer player, TimerRunMode mode)
    {
        var option = new ButtonMenuOption(120, 1000)
        {
            Text = GetModeSetOptionText(player, mode),
            BindingText = () => GetModeSetOptionText(player, mode),
            Comment = mode == TimerRunMode.Classic ? "loose strafe" : "default",
            CloseAfterClick = false
        };

        option.Click += (_, args) =>
        {
            var clickedState = _timerStateStore.GetOrCreate(args.Player.Slot, args.Player.SteamID.ToString(), args.Player.Name);
            if (SetPlayerTimerMode(args.Player, clickedState, mode))
            {
                SendChat(args.Player, $"{label("Mode")} {label("|")} {green(TimerRunModes.ToDisplayName(clickedState.TimerMode))} {gray("timer reset")}");
            }
            else
            {
                ApplyPlayerModeIfOnTimerTeam(args.Player, clickedState);
                SendChat(args.Player, $"{label("Mode")} {label("|")} {gold(TimerRunModes.ToDisplayName(clickedState.TimerMode))}");
            }

            return ValueTask.CompletedTask;
        };

        menu.AddOption(option);
    }

    private void AddFovStepOption(IMenuAPI menu, IPlayer player, PlayerTimerState state, int step)
    {
        var sign = step > 0 ? "+" : "";
        var option = new ButtonMenuOption(120, 1000)
        {
            Text = GetFovStepOptionText(player, step, sign),
            BindingText = () => GetFovStepOptionText(player, step, sign),
            Comment = "adjust",
            CloseAfterClick = false
        };

        option.Click += (_, args) =>
        {
            var clickedState = _timerStateStore.GetOrCreate(args.Player.Slot, args.Player.SteamID.ToString(), args.Player.Name);
            var currentFov = clickedState.PlayerFov ?? MinPlayerFov;
            var nextFov = Math.Clamp(currentFov + step, MinPlayerFov, MaxPlayerFov);
            SetPlayerFovFromMenu(args.Player, nextFov);
            return ValueTask.CompletedTask;
        };

        menu.AddOption(option);
    }

    private void AddFovSetOption(IMenuAPI menu, IPlayer player, PlayerTimerState state, int fov)
    {
        var option = new ButtonMenuOption(120, 1000)
        {
            Text = GetFovSetOptionText(player, fov),
            BindingText = () => GetFovSetOptionText(player, fov),
            Comment = "set",
            CloseAfterClick = false
        };

        option.Click += (_, args) =>
        {
            SetPlayerFovFromMenu(args.Player, fov);
            return ValueTask.CompletedTask;
        };

        menu.AddOption(option);
    }

    private string GetFovOptionText(IPlayer player)
    {
        var state = _timerStateStore.GetOrCreate(player.Slot, player.SteamID.ToString(), player.Name);
        var currentFov = state.PlayerFov?.ToString(CultureInfo.InvariantCulture) ?? "default";
        return $"FOV: {currentFov}";
    }

    private string GetModeOptionText(IPlayer player)
    {
        var state = _timerStateStore.GetOrCreate(player.Slot, player.SteamID.ToString(), player.Name);
        state.TimerMode = TimerRunModes.Normalize(state.TimerMode);
        return $"Mode: {TimerRunModes.ToDisplayName(state.TimerMode)}";
    }

    private string GetModeSetOptionText(IPlayer player, TimerRunMode mode)
    {
        var state = _timerStateStore.GetOrCreate(player.Slot, player.SteamID.ToString(), player.Name);
        state.TimerMode = TimerRunModes.Normalize(state.TimerMode);
        mode = TimerRunModes.Normalize(mode);
        var selected = state.TimerMode == mode ? " *" : "";
        return $"{TimerRunModes.ToDisplayName(mode)}{selected}";
    }

    private string GetFovStepOptionText(IPlayer player, int step, string sign)
    {
        var state = _timerStateStore.GetOrCreate(player.Slot, player.SteamID.ToString(), player.Name);
        var currentFov = state.PlayerFov ?? MinPlayerFov;
        var nextFov = Math.Clamp(currentFov + step, MinPlayerFov, MaxPlayerFov);
        return $"FOV {sign}{step} -> {nextFov}";
    }

    private string GetFovSetOptionText(IPlayer player, int fov)
    {
        var state = _timerStateStore.GetOrCreate(player.Slot, player.SteamID.ToString(), player.Name);
        return $"FOV {fov}{(state.PlayerFov == fov ? " *" : "")}";
    }

    private void AddBackOption(IMenuAPI menu, IPlayer player)
    {
        var option = new ButtonMenuOption("Back", 120, 1000)
        {
            Comment = "options",
            CloseAfterClick = true
        };

        option.Click += (_, args) =>
        {
            Core.Scheduler.NextTick(() => ShowOptionsMenu(args.Player));
            return ValueTask.CompletedTask;
        };

        menu.AddOption(option);
    }

    private void SetPlayerFovFromMenu(IPlayer player, int fov)
    {
        var state = _timerStateStore.GetOrCreate(player.Slot, player.SteamID.ToString(), player.Name);
        state.PlayerFov = Math.Clamp(fov, MinPlayerFov, MaxPlayerFov);
        ApplyPlayerFov(player, state);
        SavePlayerSettings(state);
        SendChat(player, $"{label("FOV")} {label("|")} {green(state.PlayerFov.Value.ToString(CultureInfo.InvariantCulture))}");
    }
}
