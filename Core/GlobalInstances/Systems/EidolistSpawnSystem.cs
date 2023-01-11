using CalamityMod.NPCs.NormalNPCs;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace InfernumMode.Core.GlobalInstances.Systems
{
    public class EidolistSpawnSystem : ModSystem
    {
        public override void PreUpdateWorld()
        {
            // Don't mess with abyss spawns in worlds without a reworked abyss.
            if (!WorldSaveSystem.InPostAEWUpdateWorld || !InfernumMode.CanUseCustomAIs)
                return;

            // Don't respawn the eidolists if they have already been defeated.
            if (WorldSaveSystem.HasDefeatedEidolists)
                return;

            int eidolistID = ModContent.NPCType<Eidolist>();
            if (NPC.AnyNPCs(eidolistID))
                return;

            SpawnEidolists(WorldSaveSystem.EidolistWorshipPedestalCenter);
        }

        public static void SpawnEidolists(Point center)
        {
            int eidolistID = ModContent.NPCType<Eidolist>();

            // Don't spawn eidolists if a player is nearby.
            Vector2 worldCenter = center.ToWorldCoordinates();
            Player closestPlayer = Main.player[Player.FindClosest(worldCenter, 1, 1)];
            if (closestPlayer.WithinRange(worldCenter, 2400f))
                return;

            for (int i = 0; i < 4; i++)
            {
                float eidolistSpawnOffset = MathHelper.Lerp(-100f, 100f, i / 3f);
                Vector2 eidolistSpawnPosition = worldCenter + Vector2.UnitX * eidolistSpawnOffset;
                NPC.NewNPC(new EntitySource_WorldEvent(), (int)eidolistSpawnPosition.X, (int)eidolistSpawnPosition.Y, eidolistID, 1, 0f, 0f, i);
            }
        }
    }
}
