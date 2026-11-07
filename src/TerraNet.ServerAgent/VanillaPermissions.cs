using TShockAPI;

namespace TerraNet.ServerAgent;

public static class VanillaPermissions
{
    private static readonly string[] Gameplay =
    [
        "tshock.journey.biomespreadfreeze", "tshock.journey.godmode", "tshock.journey.placementrange",
        "tshock.journey.rain.freeze", "tshock.journey.rain.strength", "tshock.journey.research",
        "tshock.journey.setdifficulty", "tshock.journey.setspawnrate", "tshock.journey.time.freeze",
        "tshock.journey.time.set", "tshock.journey.time.setspeed", "tshock.journey.wind.freeze", "tshock.journey.wind.strength",
        "tshock.npc.summonboss", "tshock.npc.startinvasion", "tshock.npc.startdd2", "tshock.npc.hurttown", "tshock.npc.spawnpets",
        "tshock.tp.rod", "tshock.tp.wormhole", "tshock.tp.pylon", "tshock.tp.tppotion", "tshock.tp.magicconch", "tshock.tp.demonconch",
        "tshock.world.modify", "tshock.world.paint", "tshock.world.movenpc", "tshock.world.editspawn",
        "tshock.world.time.usesundial", "tshock.world.time.usemoondial", "tshock.world.worldupgrades", "tshock.world.toggleparty",
        "tshock.ignore.placetile", "tshock.ignore.removetile", "tshock.ignore.sendtilesquare", "tshock.ignore.liquid",
        "tshock.ignore.paint", "tshock.ignore.projectile", "tshock.ignore.damage", "tshock.ignore.hp", "tshock.ignore.mp",
        "tshock.ignore.itemstack", "tshock.ignore.npcbuff", "tshock.ignore.resizechests"
    ];

    public static void Apply()
    {
        foreach (var name in new[] { TShock.Config.Settings.DefaultGuestGroupName, TShock.Config.Settings.DefaultRegistrationGroupName })
        {
            var group = TShock.Groups.GetGroupByName(name);
            if (group is null)
                continue;
            var missing = Gameplay.Where(p => !group.HasPermission(p)).ToList();
            if (missing.Count > 0)
                TShock.Groups.AddPermissions(name, missing);
        }
    }
}
