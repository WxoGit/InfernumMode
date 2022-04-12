using CalamityMod;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.Graphics.Shaders;
using Terraria.ModLoader;

namespace InfernumMode.BehaviorOverrides.BossAIs.EmpressOfLight
{
    public class SpinningPrismLaserbeam2 : ModProjectile
    {
        public int LaserCount;

        public float LaserbeamIDRatio;

        public float AngularOffset;

        public float VerticalSpinDirection;

        public PrimitiveTrail RayDrawer = null;

        public ref float LaserLength => ref projectile.ai[0];

        public ref float Time => ref projectile.localAI[0];

        public const int Lifetime = 300;

        public const float MaxLaserLength = 4800f;

        public const float SpinRate = 5f;

        public override string Texture => "CalamityMod/Projectiles/InvisibleProj";

        public override void SetStaticDefaults() => DisplayName.SetDefault("Prismatic Ray");

        public override void SetDefaults()
        {
            projectile.width = projectile.height = 32;
            projectile.hostile = true;
            projectile.penetrate = -1;
            projectile.tileCollide = false;
            projectile.ignoreWater = true;
            projectile.timeLeft = Lifetime;
            projectile.hide = true;
            projectile.netImportant = true;
            projectile.Calamity().canBreakPlayerDefense = true;
        }

        public override void SendExtraAI(BinaryWriter writer)
        {
            writer.Write(LaserCount);
            writer.Write(LaserbeamIDRatio);
            writer.Write(AngularOffset);
            writer.Write(VerticalSpinDirection);
            writer.Write(Time);
        }

        public override void ReceiveExtraAI(BinaryReader reader)
        {
            LaserCount = reader.ReadInt32();
            LaserbeamIDRatio = reader.ReadSingle();
            AngularOffset = reader.ReadSingle();
            VerticalSpinDirection = reader.ReadSingle();
            Time = reader.ReadSingle();
        }

        public override void AI()
        {
            // Grow bigger up to a point.
            float maxScale = MathHelper.Lerp(0.051f, 1.5f, Utils.GetLerpValue(0f, 30f, projectile.timeLeft, true) * Utils.GetLerpValue(0f, 16f, Time, true));
            projectile.scale = MathHelper.Clamp(projectile.scale + 0.02f, 0.05f, maxScale);

            // Spin the laserbeam.
            float deviationAngle = (Time * MathHelper.TwoPi / 40f + LaserbeamIDRatio * SpinRate) / (LaserCount * SpinRate) * MathHelper.TwoPi;
            float sinusoidYOffset = (float)Math.Cos(deviationAngle) * AngularOffset;
            projectile.velocity = Vector2.UnitY.RotatedBy(sinusoidYOffset) * VerticalSpinDirection;

            // Update the laser length.
            LaserLength = MaxLaserLength;

            // Make the beam cast light along its length. The brightness of the light is reliant on the scale of the beam.
            DelegateMethods.v3_1 = Color.White.ToVector3() * projectile.scale * 0.6f;
            Utils.PlotTileLine(projectile.Center, projectile.Center + projectile.velocity * LaserLength, projectile.width * projectile.scale, DelegateMethods.CastLight);
            Time++;
        }

        internal float PrimitiveWidthFunction(float completionRatio) => projectile.scale * 12f;

        internal Color PrimitiveColorFunction(float completionRatio)
        {
            float opacity = projectile.Opacity * Utils.GetLerpValue(0.97f, 0.9f, completionRatio, true) * 
                Utils.GetLerpValue(0f, MathHelper.Clamp(15f / LaserLength, 0f, 0.5f), completionRatio, true) *
                (float)Math.Pow(Utils.GetLerpValue(60f, 270f, LaserLength, true), 3D);
            Color c = Main.hslToRgb((completionRatio * 8f + Main.GlobalTime * 0.5f + projectile.identity * 0.3156f) % 1f, 1f, 0.7f) * opacity;
            c.A = 0;

            return c;
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Color lightColor)
        {
            if (RayDrawer is null)
                RayDrawer = new PrimitiveTrail(PrimitiveWidthFunction, PrimitiveColorFunction, specialShader: GameShaders.Misc["Infernum:PrismaticRay"]);

            GameShaders.Misc["Infernum:PrismaticRay"].UseImage("Images/Misc/Perlin");
            Main.instance.GraphicsDevice.Textures[2] = ModContent.GetTexture("InfernumMode/ExtraTextures/PrismaticLaserbeamStreak");

            Vector2[] basePoints = new Vector2[24];
            for (int i = 0; i < basePoints.Length; i++)
                basePoints[i] = projectile.Center + projectile.velocity * i / (basePoints.Length - 1f) * LaserLength;

            Vector2 overallOffset = -Main.screenPosition;
            RayDrawer.Draw(basePoints, overallOffset, 32);
            return false;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
        {
            float _ = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), projectile.Center, projectile.Center + projectile.velocity * LaserLength, projectile.scale * 12f, ref _);
        }

        public override void DrawBehind(int index, List<int> drawCacheProjsBehindNPCsAndTiles, List<int> drawCacheProjsBehindNPCs, List<int> drawCacheProjsBehindProjectiles, List<int> drawCacheProjsOverWiresUI)
        {
            drawCacheProjsOverWiresUI.Add(index);
        }

        public override bool ShouldUpdatePosition() => false;
    }
}
