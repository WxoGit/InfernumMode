using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Graphics.Shaders;
using Terraria.ModLoader;

namespace InfernumMode.BehaviorOverrides.BossAIs.Providence
{
	public class DyingSun : ModProjectile
    {
        public PrimitiveTrailCopy FireDrawer;
        public ref float Time => ref Projectile.ai[0];
        public ref float Radius => ref Projectile.ai[1];
        public override void SetStaticDefaults() => DisplayName.SetDefault("Dying Sun");

        public override void SetDefaults()
        {
            Projectile.width = 164;
            Projectile.height = 164;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 100;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
            Projectile.extraUpdates = 1;
            Projectile.scale = 1f;
        }

        public override void AI()
        {
            Projectile.scale += 0.08f;
            Radius = Projectile.scale * 42f;
            Projectile.Opacity = Utils.GetLerpValue(8f, 42f, Projectile.timeLeft, true);

            Time++;
        }

        public float SunWidthFunction(float completionRatio) => Radius * Projectile.scale * (float)Math.Sin(MathHelper.Pi * completionRatio);

        public Color SunColorFunction(float completionRatio)
        {
            Color sunColor = Main.dayTime ? Color.Yellow : Color.Cyan;
            return Color.Lerp(sunColor, Color.White, (float)Math.Sin(MathHelper.Pi * completionRatio) * 0.5f + 0.3f) * Projectile.Opacity;
        }

        public override bool PreDraw(ref Color lightColor)
        {
            if (FireDrawer is null)
                FireDrawer = new PrimitiveTrailCopy(SunWidthFunction, SunColorFunction, null, true, GameShaders.Misc["Infernum:Fire"]);

            GameShaders.Misc["Infernum:Fire"].UseSaturation(0.45f);
            GameShaders.Misc["Infernum:Fire"].UseImage1("Images/Misc/Perlin");

            List<float> rotationPoints = new();
            List<Vector2> drawPoints = new();

            for (float offsetAngle = -MathHelper.PiOver2; offsetAngle <= MathHelper.PiOver2; offsetAngle += MathHelper.Pi / 80f)
            {
                rotationPoints.Clear();
                drawPoints.Clear();

                float adjustedAngle = offsetAngle + MathHelper.Pi * -0.2f;
                Vector2 offsetDirection = adjustedAngle.ToRotationVector2();
                for (int i = 0; i < 16; i++)
                {
                    rotationPoints.Add(adjustedAngle);
                    drawPoints.Add(Vector2.Lerp(Projectile.Center - offsetDirection * Radius / 2f, Projectile.Center + offsetDirection * Radius / 2f, i / 16f));
                }

                FireDrawer.Draw(drawPoints, -Main.screenPosition, 24);
            }
            return false;
        }
    }
}
