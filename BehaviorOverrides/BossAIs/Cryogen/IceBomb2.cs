using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace InfernumMode.BehaviorOverrides.BossAIs.Cryogen
{
    public class IceBomb2 : ModProjectile
    {
        public ref float Time => ref Projectile.localAI[0];
        public override void SetStaticDefaults() => DisplayName.SetDefault("Ice Bomb");

        public override void SetDefaults()
        {
            Projectile.width = Projectile.height = 34;
            Projectile.hostile = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 180;
        }

        public override void AI()
        {
            Projectile.Opacity = Utils.GetLerpValue(0f, 20f, Time, true) * Utils.GetLerpValue(0f, 20f, Projectile.timeLeft, true);
            Projectile.scale = MathHelper.Lerp(1f, 1.7f, 1f - Utils.GetLerpValue(0f, 20f, Projectile.timeLeft, true));
            Projectile.velocity *= 0.98f;
            Time++;
        }

        public override void Kill(int timeLeft)
        {
            SoundEngine.PlaySound(SoundID.Item27, Projectile.Center);
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            for (int i = 0; i < 5; i++)
            {
                Vector2 spikeVelocity = -Vector2.UnitY.RotatedBy(MathHelper.Lerp(-0.43f, 0.43f, i / 4f)) * 15f;
                Utilities.NewProjectileBetter(Projectile.Center, spikeVelocity, ModContent.ProjectileType<IceRain2>(), 120, 0f);
            }
        }

        public override bool? CanDamage()/* tModPorter Suggestion: Return null instead of false */ => Projectile.alpha < 20;
    }
}
