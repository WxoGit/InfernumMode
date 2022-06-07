﻿using CalamityMod.NPCs;
using CalamityMod.NPCs.Ravager;
using InfernumMode.OverridingSystem;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace InfernumMode.BehaviorOverrides.BossAIs.Ravager
{
	public class RavagerLegRightBehaviorOverride : NPCBehaviorOverride
	{
		public override int NPCOverrideType => ModContent.NPCType<RavagerLegRight>();

		public override NPCOverrideContext ContentToOverride => NPCOverrideContext.NPCAI;

		public override bool PreAI(NPC npc)
		{
			// Fuck off if the main boss is gone.
			if (CalamityGlobalNPC.scavenger < 0 || !Main.npc[CalamityGlobalNPC.scavenger].active)
			{
				npc.active = false;
				npc.netUpdate = true;
				return false;
			}

			NPC ravagerBody = Main.npc[CalamityGlobalNPC.scavenger];

			// Don't attack if the Ravager isn't ready to do so yet.
			npc.dontTakeDamage = false;
			npc.damage = npc.defDamage;
			if (ravagerBody.Infernum().ExtraAI[5] < RavagerBodyBehaviorOverride.AttackDelay)
			{
				npc.damage = 0;
				npc.dontTakeDamage = true;
			}

			if (npc.timeLeft < 1800)
				npc.timeLeft = 1800;

			npc.Center = ravagerBody.Center + new Vector2(70f, 88f);
			npc.Opacity = ravagerBody.Opacity;

			return false;
		}
	}
}
