using CalamityMod;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Graphics.Shaders;
using Terraria.ID;
using Terraria.ModLoader;

namespace InfernumMode.BehaviorOverrides.BossAIs.DukeFishron
{
    public class TidalWave : ModProjectile
    {
        internal PrimitiveTrailCopy TornadoDrawer;
        public override string Texture => "CalamityMod/Projectiles/InvisibleProj";
        public ref float Time => ref projectile.ai[0];
        public ref float WaveHeight => ref projectile.ai[1];
        public override void SetStaticDefaults()
        {
            DisplayName.SetDefault("Wave");
            ProjectileID.Sets.TrailingMode[projectile.type] = 2;
            ProjectileID.Sets.TrailCacheLength[projectile.type] = 150;
        }

        public override void SetDefaults()
        {
            projectile.width = 40;
            projectile.height = 1020;
            projectile.hostile = true;
            projectile.tileCollide = false;
            projectile.ignoreWater = true;
            projectile.penetrate = -1;
            projectile.alpha = 255;
            projectile.timeLeft = 360;
            projectile.Calamity().canBreakPlayerDefense = true;
        }

        public override void AI()
        {
            Time++;

            if (WaveHeight < 60f)
                WaveHeight = 60f;
            WaveHeight = MathHelper.Lerp(WaveHeight, 720f, 0.04f);
            projectile.Opacity = (float)Math.Sin(projectile.timeLeft / 360f * MathHelper.Pi) * 3f;
            if (projectile.Opacity > 1f)
                projectile.Opacity = 1f;
        }

        internal Color ColorFunction(float completionRatio)
        {
            return Color.Lerp(Color.DeepSkyBlue, Color.Turquoise, (float)Math.Abs(Math.Sin(completionRatio * MathHelper.Pi + Main.GlobalTime)) * 0.5f) * projectile.Opacity;
        }

        internal float WidthFunction(float completionRatio) => WaveHeight;

        internal Vector2 OffsetFunction(float completionRatio) => Vector2.UnitY * (float)Math.Sin(completionRatio * MathHelper.Pi + Time / 11f) * 60f;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
        {
            for (int i = 0; i < projectile.oldPos.Length; i++)
            {
                float _ = 0f;
                float completionRatio = i / (float)projectile.oldPos.Length;
                Vector2 top = projectile.oldPos[i] + OffsetFunction(completionRatio);
                Vector2 bottom = projectile.oldPos[i] + Vector2.UnitY * WaveHeight + OffsetFunction(completionRatio);
                if (Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), top, bottom, (int)projectile.velocity.X, ref _))
                    return true;
            }
            return false;
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Color lightColor)
        {
            if (TornadoDrawer is null)
                TornadoDrawer = new PrimitiveTrailCopy(WidthFunction, ColorFunction, OffsetFunction, false, GameShaders.Misc["Infernum:DukeTornado"]);

            GameShaders.Misc["Infernum:DukeTornado"].SetShaderTexture(ModContent.GetTexture("Terraria/Misc/Perlin"));

            for (int i = 0; i < 3; i++)
                TornadoDrawer.Draw(projectile.oldPos, Vector2.UnitY * WaveHeight * 0.5f - Main.screenPosition, 35, 0f);
            return false;
        }

        public override void ModifyHitPlayer(Player target, ref int damage, ref bool crit)
        {
            target.Calamity().lastProjectileHit = projectile;
        }
    }
}
