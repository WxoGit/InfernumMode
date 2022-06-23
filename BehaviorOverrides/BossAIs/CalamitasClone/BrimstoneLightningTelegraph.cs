using CalamityMod;
using CalamityMod.Events;
using CalamityMod.Projectiles.Boss;
using Microsoft.Xna.Framework;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace InfernumMode.BehaviorOverrides.BossAIs.CalamitasClone
{
    public class BrimstoneLightningTelegraph : ModProjectile
    {
        public override string Texture => "CalamityMod/Projectiles/InvisibleProj";
        public override void SetStaticDefaults()
        {
            DisplayName.SetDefault("Telegraph");
        }

        public override void SetDefaults()
        {
            Projectile.width = Projectile.height = 64;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 45;
        }

        public override void AI()
        {
            Projectile.scale = (float)Math.Sin(MathHelper.Pi * Projectile.timeLeft / 45f);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Main.spriteBatch.DrawLineBetter(Projectile.Center - Vector2.UnitY * 4000f, Projectile.Center + Vector2.UnitY * 4000f, Color.Red, Projectile.scale * 4f);
            return false;
        }

        public override void Kill(int timeLeft)
        {
            SoundEngine.PlaySound(HolyBlast.ImpactSound, Projectile.Center);
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            for (int i = 0; i < 2; i++)
            {
                Vector2 spawnPosition = Projectile.Center + Vector2.UnitX * Main.rand.NextFloat(-10f, 10f);
                spawnPosition.Y -= 1600f;

                bool shouldBeBuffed = DownedBossSystem.downedProvidence && !BossRushEvent.BossRushActive && CalamitasCloneBehaviorOverride.ReadyToUseBuffedAI;
                int lightningDamage = shouldBeBuffed ? 375 : 160;
                int lightning = Utilities.NewProjectileBetter(spawnPosition, Vector2.UnitY * 7f, ModContent.ProjectileType<BrimstoneLightning>(), lightningDamage, 0f);
                if (Main.projectile.IndexInRange(lightning))
                {
                    Main.projectile[lightning].ai[0] = Main.projectile[lightning].velocity.ToRotation();
                    Main.projectile[lightning].ai[1] = Main.rand.Next(100);
                }
            }
        }
    }
}
