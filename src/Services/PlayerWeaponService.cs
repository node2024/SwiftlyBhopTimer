using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.SchemaDefinitions;

namespace SwiftlyBhopTimer.Services;

public sealed class PlayerWeaponService
{
    private static readonly HashSet<string> SecondaryWeaponDesignerNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "weapon_glock",
        "weapon_usp_silencer",
        "weapon_hkp2000",
        "weapon_p250",
        "weapon_deagle",
        "weapon_elite",
        "weapon_fiveseven",
        "weapon_tec9",
        "weapon_cz75a",
        "weapon_revolver"
    };

    private bool _giveWarningLogged;

    public bool RebuildDefaultWeapons(IPlayer player, string designerName)
    {
        if (player.PlayerPawn is not CCSPlayerPawn pawn)
        {
            return false;
        }

        try
        {
            pawn.ItemServices?.RemoveItems();
            pawn.ItemServices?.GiveItem(GetKnifeDesignerName(player));
            pawn.ItemServices?.GiveItem(designerName);
            pawn.WeaponServices?.SelectWeaponByDesignerName(designerName);
            return HasSecondaryWeapon(pawn);
        }
        catch (Exception ex) when (!_giveWarningLogged)
        {
            _giveWarningLogged = true;
            Console.WriteLine($"[SwiftlyBhopTimer] Failed to rebuild default weapons: {ex.Message}");
            return false;
        }
    }

    public bool EnsureSecondaryWeapon(IPlayer player, string designerName)
    {
        if (player.PlayerPawn is not CCSPlayerPawn pawn)
        {
            return false;
        }

        if (HasSecondaryWeapon(pawn))
        {
            return true;
        }

        try
        {
            pawn.ItemServices?.GiveItem(designerName);
            pawn.WeaponServices?.SelectWeaponByDesignerName(designerName);
            return HasSecondaryWeapon(pawn);
        }
        catch (Exception ex) when (!_giveWarningLogged)
        {
            _giveWarningLogged = true;
            Console.WriteLine($"[SwiftlyBhopTimer] Failed to give secondary weapon '{designerName}': {ex.Message}");
            return false;
        }
    }

    private static bool HasSecondaryWeapon(CCSPlayerPawn pawn)
    {
        var weaponServices = pawn.WeaponServices;
        if (weaponServices is null)
        {
            return false;
        }

        foreach (var weapon in weaponServices.MyValidWeapons)
        {
            if (IsSecondaryWeapon(weapon))
            {
                return true;
            }
        }

        foreach (var handle in weaponServices.MyWeapons)
        {
            if (handle.IsValid && IsSecondaryWeapon(handle.Value))
            {
                return true;
            }
        }

        return IsSecondaryWeapon(weaponServices.ActiveWeapon.Value) ||
               IsSecondaryWeapon(weaponServices.LastWeapon.Value);
    }

    private static bool IsSecondaryWeapon(CBasePlayerWeapon? weapon)
    {
        return weapon is not null &&
               !string.IsNullOrWhiteSpace(weapon.DesignerName) &&
               SecondaryWeaponDesignerNames.Contains(weapon.DesignerName);
    }

    private static string GetKnifeDesignerName(IPlayer player)
    {
        var team = player.Controller?.TeamNum ?? 0;
        return team == 2 ? "weapon_knife_t" : "weapon_knife";
    }
}
