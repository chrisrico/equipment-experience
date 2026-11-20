using System;
using BepInEx.Configuration;
using HarmonyLib;

namespace EquipmentExperience
{
    /// <summary>
    /// Sets the adrenaline an item's attacks give per cast. Vanilla gives Lightning Strike none at all: its cast, its
    /// attack hits and its projectile are all set to 0, while the Staff of Embers gives 3 per cast. Humanoid clones
    /// the attack before Attack.Start, so changing the clone affects only that attack, and vanilla then grants it
    /// where it grants any per-cast adrenaline.
    /// </summary>
    public static class CastAdrenaline
    {
        private const string Name = "Adrenaline per cast";

        private static PrefabValues _values;

        // The project doesn't reference UnityEngine.PhysicsModule, so Attack.Start's Rigidbody parameter type is taken
        // from Attack's own Rigidbody field.
        private static readonly Type RigidbodyType = AccessTools.Field(typeof(Attack), "m_body")?.FieldType;

        public static void BindConfig(ConfigFile config)
        {
            _values = new PrefabValues(config.Bind("Adrenaline", "ItemAdrenalinePerCast", "StaffThunderBlood:3",
                "Comma-separated Prefab:adrenaline entries setting how much adrenaline each attack with the item " +
                "gives when it's used, replacing the vanilla amount (e.g. StaffThunderBlood:3 gives Lightning Strike " +
                "3 per cast, like the Staff of Embers; vanilla gives it none). Use the item's prefab name, as used " +
                "by the spawn command. Leave empty for vanilla."));
        }

        public static PatchGroup Group()
        {
            Type[] startArgs =
            {
                typeof(Humanoid), RigidbodyType, typeof(ZSyncAnimation), typeof(CharacterAnimEvent),
                typeof(VisEquipment), typeof(ItemDrop.ItemData), typeof(Attack), typeof(float), typeof(float),
            };
            var group = new PatchGroup(Name)
                .RequireField(typeof(Attack), "m_body")
                .RequireField(typeof(Attack), nameof(Attack.m_attackUseAdrenaline))
                .RequireField(typeof(ItemDrop.ItemData), nameof(ItemDrop.ItemData.m_dropPrefab));
            return RigidbodyType == null
                ? group
                : group.Patch(typeof(CastAdrenaline), typeof(Attack), nameof(Attack.Start), startArgs,
                    HarmonyPatchType.Prefix, nameof(Prefix));
        }

        private static void Prefix(Attack __instance, Humanoid character, ItemDrop.ItemData weapon)
        {
            try
            {
                if (character is Player && _values != null && _values.TryGet(weapon, out float adrenaline))
                {
                    __instance.m_attackUseAdrenaline = adrenaline;
                }
            }
            catch (Exception e)
            {
                Guard.Report(Name, e);
            }
        }
    }
}
