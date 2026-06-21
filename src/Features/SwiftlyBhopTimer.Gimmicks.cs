using Microsoft.Extensions.Logging;

namespace SwiftlyBhopTimer;

public sealed partial class SwiftlyBhopTimer
{
    private static readonly string[] BhopHelperDefaultCommands =
    [
        "sv_enablebunnyhopping 1",
        "sv_autobunnyhopping 1",
        "sbt_movement_compat_apply",
        "sv_jump_precision_enable 0",
        "sv_legacy_jump 1",
        "sv_subtick_movement_view_angles 0",
        "sv_staminajumpcost 0",
        "sv_staminalandcost 0",
        "sv_staminarecoveryrate 60",
        "sv_staminamax 0",
        "sbt_autobhop_force 1",
        "sbt_surf_fix 1",
        "sharptimer_kill_pointservercommand_entities 0",
        "sharptimer_trigger_push_fix 1",
        "sharptimer_disable_telehop 0",
        "sharptimer_max_bhop_block_time 1",
        "rngfix_downhill 1",
        "rngfix_uphill 1",
        "rngfix_edge 1",
        "rngfix_triggerjump 1",
        "rngfix_telehop 1",
        "rngfix_stairs 1",
        "rngfix_useoldslopefixlogic 1",
        "rngfix_debug 0"
    ];

    private void ApplyBhopHelperDefaults(string reason)
    {
        foreach (var command in BhopHelperDefaultCommands)
        {
            ExecuteServerCommand(command);
        }

        Core.Logger.LogInformation(
            "Bhop helper defaults applied. Reason={Reason}; Map={Map}",
            reason,
            _currentMapName);
    }
}
