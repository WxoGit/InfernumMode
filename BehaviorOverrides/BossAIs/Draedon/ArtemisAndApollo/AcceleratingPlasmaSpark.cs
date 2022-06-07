using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace InfernumMode.BehaviorOverrides.BossAIs.Draedon.ArtemisAndApollo
{
	public class AcceleratingPlasmaSpark : ModProjectile
	{
		public override void SetStaticDefaults() => DisplayName.SetDefault("Plasma Spark");

		public override void SetDefaults()
		{
			projectile.width = projectile.height = 14;
			projectile.penetrate = -1;
			projectile.tileCollide = false;
			projectile.hostile = true;
			projectile.timeLeft = 360;
			projectile.Opacity = 0f;
			projectile.hide = true;
			cooldownSlot = 1;
		}

		public override void AI()
		{
			projectile.Opacity = MathHelper.Clamp(projectile.Opacity + 0.1f, 0f, 1f);
			projectile.rotation += projectile.velocity.X * 0.025f;
			if (projectile.velocity.Length() < 35f)
				projectile.velocity *= 1.024f;

			// Emit dust.
			for (int i = 0; i < 2; i++)
			{
				Dust plasma = Dust.NewDustDirect(projectile.position, projectile.width, projectile.height, 107);
				plasma.scale *= 0.7f;
				plasma.velocity = plasma.velocity * 0.4f + Main.rand.NextVector2Circular(0.4f, 0.4f);
				plasma.fadeIn = 0.4f;
				plasma.noGravity = true;
			}
		}

		public override void OnHitPlayer(Player target, int damage, bool crit)
		{
			if (projectile.Opacity != 1f)
				return;

			target.AddBuff(BuffID.CursedInferno, 120);
		}

		public override Color? GetAlpha(Color lightColor)
		{
			return new Color(255, 255, 255, 48) * projectile.Opacity;
		}

		public override bool PreDraw(SpriteBatch spriteBatch, Color lightColor)
		{
			Texture2D texture = Main.projectileTexture[projectile.type];
			Vector2 origin = texture.Size() * 0.5f;

			for (int i = 0; i < 4; i++)
			{
				Vector2 drawOffset = -projectile.velocity.SafeNormalize(Vector2.Zero) * i * 12f;
				Vector2 afterimageDrawPosition = projectile.Center + drawOffset - Main.screenPosition;
				Color backAfterimageColor = projectile.GetAlpha(lightColor) * ((4f - i) / 4f);
				Main.spriteBatch.Draw(texture, afterimageDrawPosition, null, backAfterimageColor, projectile.rotation, origin, projectile.scale, SpriteEffects.None, 0f);
			}

			Color frontAfterimageColor = projectile.GetAlpha(lightColor) * 0.2f;
			for (int i = 0; i < 9; i++)
			{
				Vector2 drawOffset = (MathHelper.TwoPi * i / 9f + projectile.rotation - MathHelper.PiOver2).ToRotationVector2() * 2f;
				Vector2 afterimageDrawPosition = projectile.Center + drawOffset - Main.screenPosition;
				Main.spriteBatch.Draw(texture, afterimageDrawPosition, null, frontAfterimageColor, projectile.rotation, origin, projectile.scale, SpriteEffects.None, 0f);
			}
			return false;
		}

		public override void DrawBehind(int index, List<int> behindNPCsAndTiles, List<int> behindNPCs, List<int> behindProjectiles, List<int> overPlayers)
		{
			behindProjectiles.Add(index);
		}
	}
}
