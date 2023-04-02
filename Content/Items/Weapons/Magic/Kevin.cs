﻿using CalamityMod.Items;
using InfernumMode.Content.Projectiles.Magic;
using InfernumMode.Content.Rarities.InfernumRarities;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace InfernumMode.Content.Items.Weapons.Magic
{
    public class Kevin : ModItem
    {
        public const float TargetingDistance = 720f;

        public const int LightningArea = 1500;

        public override void SetStaticDefaults()
        {
            DisplayName.SetDefault("kevin");
            Tooltip.SetDefault("It stands for 'Kinetic Electroplasma Voltage Infuser'");
            SacrificeTotal = 1;
        }

        public override void SetDefaults()
        {
            Item.damage = 6400;
            Item.DamageType = DamageClass.Magic;
            Item.mana = 4;
            Item.width = 36;
            Item.height = 30;
            Item.useTime = Item.useAnimation = 20;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noMelee = true;
            Item.knockBack = 0f;

            Item.value = CalamityGlobalItem.RarityHotPinkBuyPrice;
            Item.rare = ModContent.RarityType<InfernumCyanSparkRarity>();

            Item.autoReuse = true;
            Item.noUseGraphic = true;
            Item.shoot = ModContent.ProjectileType<KevinProjectile>();
            Item.channel = true;
            Item.shootSpeed = 0f;
            Item.Infernum_Tooltips().DeveloperItem = true;
        }

        public override bool CanUseItem(Player player) => player.ownedProjectileCounts[Item.shoot] <= 0;
    }
}
