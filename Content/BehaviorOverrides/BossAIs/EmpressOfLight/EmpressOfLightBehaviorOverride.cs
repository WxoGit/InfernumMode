﻿using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.GameContent;
using Terraria.DataStructures;
using Terraria.Graphics.Shaders;
using CalamityMod;
using CalamityMod.Events;
using Terraria.Audio;
using System.Linq;
using System.Collections.Generic;
using Terraria.GameContent.Events;
using System.IO;
using InfernumMode.Core.OverridingSystem;
using InfernumMode.Core.GlobalInstances.Systems;
using InfernumMode.Assets.Effects;
using InfernumMode.Assets.ExtraTextures;

namespace InfernumMode.Content.BehaviorOverrides.BossAIs.EmpressOfLight
{
    public class EmpressOfLightBehaviorOverride : NPCBehaviorOverride
    {
        public enum EmpressOfLightAttackType
        {
            SpawnAnimation,
            PrismaticBoltCircle,
            BackstabbingLances,
            MesmerizingMagic,
            HorizontalCharge,
            EnterSecondPhase,
            RainbowWispForm,
            DanceOfSwords,
            MajesticPierce,
            LanceWallBarrage,

            DeathAnimation
        }

        public override int NPCOverrideType => NPCID.HallowBoss;

        #region Constants and Attack Patterns

        public static bool ShouldBeEnraged => Main.dayTime || BossRushEvent.BossRushActive;

        public const int SecondPhaseFadeoutTime = 90;

        public const int SecondPhaseFadeBackInTime = 90;

        public const int ScreenShaderIntensityIndex = 7;

        public const float Phase2LifeRatio = 0.8f;

        public const float Phase3LifeRatio = 0.5f;

        public const float Phase4LifeRatio = 0.2f;

        public const float BorderWidth = 6000f;

        public static int PrismaticBoltDamage => ShouldBeEnraged ? 350 : 175;

        public static int LanceDamage => ShouldBeEnraged ? 375 : 185;

        public static int SwordDamage => ShouldBeEnraged ? 400 : 200;

        public static int CloudDamage => ShouldBeEnraged ? 400 : 200;

        public static int LaserbeamDamage => ShouldBeEnraged ? 700 : 300;

        public static EmpressOfLightAttackType[] Phase1AttackCycle => new EmpressOfLightAttackType[]
        {
            EmpressOfLightAttackType.PrismaticBoltCircle,
            EmpressOfLightAttackType.MesmerizingMagic,
            EmpressOfLightAttackType.HorizontalCharge,
            EmpressOfLightAttackType.PrismaticBoltCircle,
            EmpressOfLightAttackType.HorizontalCharge,
            EmpressOfLightAttackType.MesmerizingMagic,
        };

        public static EmpressOfLightAttackType[] Phase2AttackCycle => new EmpressOfLightAttackType[]
        {
            EmpressOfLightAttackType.PrismaticBoltCircle,
            EmpressOfLightAttackType.MesmerizingMagic,
            EmpressOfLightAttackType.DanceOfSwords,
            EmpressOfLightAttackType.RainbowWispForm,
            EmpressOfLightAttackType.HorizontalCharge,
            EmpressOfLightAttackType.DanceOfSwords,
            EmpressOfLightAttackType.MesmerizingMagic,
            EmpressOfLightAttackType.PrismaticBoltCircle,
            EmpressOfLightAttackType.RainbowWispForm,
            EmpressOfLightAttackType.HorizontalCharge,
            EmpressOfLightAttackType.DanceOfSwords,
        };

        public static EmpressOfLightAttackType[] Phase3AttackCycle => new EmpressOfLightAttackType[]
        {
            EmpressOfLightAttackType.RainbowWispForm,
        };

        public static EmpressOfLightAttackType[] Phase4AttackCycle => new EmpressOfLightAttackType[]
        {
            EmpressOfLightAttackType.HorizontalCharge,
            EmpressOfLightAttackType.DanceOfSwords,
        };

        public override float[] PhaseLifeRatioThresholds => new float[]
        {
            Phase2LifeRatio,
            Phase3LifeRatio,
            Phase4LifeRatio
        };

        #endregion Constants and Attack Patterns

        #region Netcode Syncing

        public override void SendExtraData(NPC npc, ModPacket writer)
        {
            writer.Write(npc.Opacity);
        }

        public override void ReceiveExtraData(NPC npc, BinaryReader reader)
        {
            npc.Opacity = reader.ReadSingle();
        }

        #endregion Netcode Syncing

        #region AI and Behaviors
        public override bool PreAI(NPC npc)
        {
            npc.TargetClosestIfTargetIsInvalid();

            Player target = Main.player[npc.target];
            ref float attackType = ref npc.ai[0];
            ref float attackTimer = ref npc.ai[1];
            ref float currentPhase = ref npc.ai[2];
            ref float wingFrameCounter = ref npc.localAI[0];
            ref float leftArmFrame = ref npc.localAI[1];
            ref float rightArmFrame = ref npc.localAI[1];
            ref float screenShaderStrength = ref npc.localAI[3];
            ref float deathAnimationScreenShaderStrength = ref npc.Infernum().ExtraAI[ScreenShaderIntensityIndex];

            // Reset things every frame.
            npc.damage = 0;
            npc.spriteDirection = 1;
            npc.dontTakeDamage = false;
            leftArmFrame = 0f;
            rightArmFrame = 0f;
            deathAnimationScreenShaderStrength = 0f;

            // Disappear if the target is dead.
            if (!target.active || target.dead)
            {
                npc.active = false;
                return false;
            }

            // Use the bloom shader at night.
            if (!Main.dayTime && currentPhase >= 1f)
                npc.Infernum().ShouldUseSaturationBlur = true;

            // Enter new phases.
            float lifeRatio = npc.life / (float)npc.lifeMax;
            if (currentPhase == 0f && lifeRatio < Phase2LifeRatio)
            {
                SelectNextAttack(npc);
                ClearAwayEntities();
                npc.Infernum().ExtraAI[5] = 0f;
                attackType = (int)EmpressOfLightAttackType.EnterSecondPhase;
                currentPhase = 1f;
                npc.netUpdate = true;
            }

            if (currentPhase == 1f && lifeRatio < Phase3LifeRatio)
            {
                currentPhase = 2f;
                npc.Opacity = 1f;
                SelectNextAttack(npc);
                ClearAwayEntities();
                npc.Infernum().ExtraAI[5] = 0f;
                attackType = (int)EmpressOfLightAttackType.RainbowWispForm;
                npc.netUpdate = true;
            }

            if (currentPhase == 2f && lifeRatio < Phase4LifeRatio)
            {
                currentPhase = 3f;
                npc.Opacity = 1f;
                npc.Infernum().ExtraAI[5] = 0f;
                SelectNextAttack(npc);
                ClearAwayEntities();
                npc.netUpdate = true;
            }

            if (ShouldBeEnraged)
                npc.Calamity().CurrentlyEnraged = true;

            switch ((EmpressOfLightAttackType)attackType)
            {
                case EmpressOfLightAttackType.SpawnAnimation:
                    DoBehavior_SpawnAnimation(npc, target, ref attackTimer, ref leftArmFrame, ref rightArmFrame);
                    break;
                case EmpressOfLightAttackType.PrismaticBoltCircle:
                    DoBehavior_PrismaticBoltCircle(npc, target, ref attackTimer, ref leftArmFrame);
                    break;
                case EmpressOfLightAttackType.BackstabbingLances:
                    DoBehavior_BackstabbingLances(npc, target, ref attackTimer, ref leftArmFrame, ref rightArmFrame);
                    break;
                case EmpressOfLightAttackType.MesmerizingMagic:
                    DoBehavior_MesmerizingMagic(npc, target, ref attackTimer, ref leftArmFrame, ref rightArmFrame);
                    break;
                case EmpressOfLightAttackType.HorizontalCharge:
                    DoBehavior_HorizontalCharge(npc, target, ref attackTimer);
                    break;
                case EmpressOfLightAttackType.EnterSecondPhase:
                    DoBehavior_EnterSecondPhase(npc, target, ref attackTimer);
                    break;
                case EmpressOfLightAttackType.RainbowWispForm:
                    DoBehavior_RainbowWispForm(npc, target, ref attackTimer, ref rightArmFrame);
                    break;
                case EmpressOfLightAttackType.DanceOfSwords:
                    DoBehavior_DanceOfSwords(npc, target, ref attackTimer, ref leftArmFrame, ref rightArmFrame);
                    break;
                case EmpressOfLightAttackType.DeathAnimation:
                    DoBehavior_DeathAnimation(npc, target, ref attackTimer, ref deathAnimationScreenShaderStrength);
                    break;

                case EmpressOfLightAttackType.MajesticPierce:
                    DoBehavior_MajesticPierce(npc, target, ref attackTimer, ref leftArmFrame, ref rightArmFrame);
                    break;
                case EmpressOfLightAttackType.LanceWallBarrage:
                    DoBehavior_LanceWallBarrage(npc, target, ref attackTimer, ref leftArmFrame, ref rightArmFrame);
                    break;
            }

            // Manage the screen shader.
            if (Main.netMode != NetmodeID.Server)
            {
                screenShaderStrength = 1f;
                if (attackType == (int)EmpressOfLightAttackType.EnterSecondPhase)
                    screenShaderStrength = Utils.GetLerpValue(SecondPhaseFadeoutTime, SecondPhaseFadeoutTime + SecondPhaseFadeBackInTime, attackTimer, true);

                InfernumEffectsRegistry.EoLScreenShader.GetShader().UseImage("Images/Misc/noise");
                InfernumEffectsRegistry.EoLScreenShader.GetShader().UseImage(ModContent.Request<Texture2D>("InfernumMode/Content/BehaviorOverrides/BossAIs/EmpressOfLight/EmpressOfLightWingsTexture").Value, 1);
                InfernumEffectsRegistry.EoLScreenShader.GetShader().UseImage("Images/Misc/Perlin", 2);
                InfernumEffectsRegistry.EoLScreenShader.GetShader().UseColor(Color.Pink);
                InfernumEffectsRegistry.EoLScreenShader.GetShader().UseOpacity(deathAnimationScreenShaderStrength);
                InfernumEffectsRegistry.EoLScreenShader.GetShader().UseIntensity(screenShaderStrength);
            }

            wingFrameCounter++;
            attackTimer++;
            return false;
        }

        public static void TeleportTo(NPC npc, Vector2 destination)
        {
            npc.Center = destination;

            // Cease all movement.
            npc.velocity = Vector2.Zero;

            SoundEngine.PlaySound(SoundID.Item122, npc.Center);
            SoundEngine.PlaySound(SoundID.Item161, npc.Center);

            Utilities.CreateShockwave(npc.Center, 2, 8, 75, false);
            if (Main.netMode != NetmodeID.MultiplayerClient)
                Utilities.NewProjectileBetter(npc.Center + Vector2.UnitY * 8f, Vector2.Zero, ModContent.ProjectileType<ShimmeringLightWave>(), 0, 0f);

            npc.netUpdate = true;
        }

        public static void DoBehavior_SpawnAnimation(NPC npc, Player target, ref float attackTimer, ref float leftArmFrame, ref float rightArmFrame)
        {
            int spawnAnimationTime = EmpressAurora.Lifetime - 30;

            // Fade in.
            npc.Opacity = MathHelper.Clamp((float)Math.Sqrt(attackTimer / spawnAnimationTime), 0f, 1f);

            // Disable damage.
            npc.dontTakeDamage = true;

            // Summon auroras and descend.
            if (attackTimer == 1f)
            {
                npc.velocity = Vector2.UnitY * 5f;
                if (Main.netMode != NetmodeID.MultiplayerClient)
                {
                    Vector2 auroraSpawnPosition = npc.Center - Vector2.UnitY * 80f;
                    Utilities.NewProjectileBetter(auroraSpawnPosition, Vector2.Zero, ModContent.ProjectileType<EmpressAurora>(), 0, 0f);
                }

                npc.netUpdate = true;
            }

            npc.velocity *= 0.95f;

            // Scream shortly after being summoned.
            if (attackTimer == 10f)
                SoundEngine.PlaySound(SoundID.Item161, npc.Center);

            // Hold hands together for the first part of the animation.
            if (attackTimer < spawnAnimationTime * 0.67f)
            {
                leftArmFrame = 2f;
                rightArmFrame = 2f;
            }

            // Cast shimmers.
            if (attackTimer > 10f && attackTimer < spawnAnimationTime - 10f)
            {
                for (int i = 0; i < 6; i++)
                {
                    float dustPersistence = MathHelper.Lerp(1.3f, 0.7f, npc.Opacity) * Utils.GetLerpValue(0f, spawnAnimationTime - 60f, attackTimer, true);
                    Color newColor = Main.hslToRgb((attackTimer / spawnAnimationTime + Main.rand.NextFloat(0.1f)) % 1f, 1f, 0.5f);
                    Dust rainbowMagic = Dust.NewDustDirect(npc.position, npc.width, npc.height, 267, 0f, 0f, 0, newColor, 1f);
                    rainbowMagic.position = npc.Center + Main.rand.NextVector2Circular(npc.width * 12f, npc.height * 12f) + new Vector2(0f, -150f);
                    rainbowMagic.velocity *= Main.rand.NextFloat(0.8f);
                    rainbowMagic.noGravity = true;
                    rainbowMagic.fadeIn = 0.7f + Main.rand.NextFloat(dustPersistence * 0.7f);
                    rainbowMagic.velocity += Vector2.UnitY * 3f;
                    rainbowMagic.scale = 0.6f;

                    rainbowMagic = Dust.CloneDust(rainbowMagic);
                    rainbowMagic.scale /= 2f;
                    rainbowMagic.fadeIn *= 0.85f;
                }
            }

            if (attackTimer >= spawnAnimationTime)
                SelectNextAttack(npc);
        }

        public static void DoBehavior_PrismaticBoltCircle(NPC npc, Player target, ref float attackTimer, ref float leftArmFrame)
        {
            int boltReleaseDelay = 90;
            int boltReleaseTime = 74;
            int boltReleaseRate = 2;
            int attackSwitchDelay = 190;
            float boltSpeed = 10.5f;
            Vector2 handOffset = new(-55f, -30f);

            if (InPhase2(npc))
            {
                boltReleaseRate--;
                boltSpeed += 4f;
            }

            if (ShouldBeEnraged)
                boltSpeed += 8f;

            if (BossRushEvent.BossRushActive)
                boltSpeed *= 1.5f;

            // Hover to the top left/right of the target.
            Vector2 hoverDestination = target.Center + new Vector2((target.Center.X < npc.Center.X).ToDirectionInt() * 400f, -250f);
            Vector2 idealVelocity = npc.SafeDirectionTo(hoverDestination) * 13.5f;
            if (!npc.WithinRange(hoverDestination, 40f))
                npc.SimpleFlyMovement(idealVelocity, 0.75f);

            // Play a magic sound.
            if (attackTimer == boltReleaseDelay)
                SoundEngine.PlaySound(SoundID.Item164, npc.Center);

            // Fade out and teleport to the opposite side of the target halfway through the attack.
            if (attackTimer >= boltReleaseDelay + boltReleaseTime / 2 - 10 && attackTimer <= boltReleaseDelay + boltReleaseTime / 2)
            {
                npc.Opacity = Utils.GetLerpValue(0f, -10f, attackTimer - (boltReleaseDelay + boltReleaseTime / 2), true);
                if (npc.Opacity <= 0f)
                {
                    if (Main.netMode != NetmodeID.MultiplayerClient)
                        Utilities.NewProjectileBetter(npc.Center, Vector2.Zero, ModContent.ProjectileType<EmpressExplosion>(), 0, 0f);

                    float horizontalDistanceFromtarget = target.Center.X - npc.Center.X;
                    if (Math.Abs(horizontalDistanceFromtarget) < 600f)
                        horizontalDistanceFromtarget = Math.Sign(horizontalDistanceFromtarget) * 600f;
                    if (Math.Abs(horizontalDistanceFromtarget) > 1200f)
                        horizontalDistanceFromtarget = Math.Sign(horizontalDistanceFromtarget) * 1200f;

                    npc.Opacity = 1f;
                    npc.position.X += horizontalDistanceFromtarget * 2f;

                    if (Main.netMode != NetmodeID.MultiplayerClient)
                        Utilities.NewProjectileBetter(npc.Center, Vector2.Zero, ModContent.ProjectileType<EmpressExplosion>(), 0, 0f);

                    npc.netUpdate = true;
                }
            }

            // Release bolts.
            if (attackTimer >= boltReleaseDelay && attackTimer < boltReleaseDelay + boltReleaseTime)
            {
                leftArmFrame = 3f;
                if (Main.netMode != NetmodeID.MultiplayerClient && attackTimer % boltReleaseRate == 0f)
                {
                    float castCompletionInterpolant = Utils.GetLerpValue(boltReleaseDelay, boltReleaseDelay + boltReleaseTime, attackTimer, true);
                    Vector2 boltVelocity = -Vector2.UnitY.RotatedBy(MathHelper.TwoPi * castCompletionInterpolant) * boltSpeed;
                    Utilities.NewProjectileBetter(npc.Center + handOffset, boltVelocity, ModContent.ProjectileType<PrismaticBolt>(), PrismaticBoltDamage, 0f, -1, npc.target, castCompletionInterpolant);
                }
            }

            if (attackTimer >= boltReleaseDelay + boltReleaseTime + attackSwitchDelay)
                SelectNextAttack(npc);
        }

        public static void DoBehavior_MajesticPierce(NPC npc, Player target, ref float attackTimer, ref float leftArmFrame, ref float rightArmFrame)
        {
            int terraprismaCount = 2;
            int terraprismaAttackDelay = 32;
            int shootDelay = 156;
            int attackCycleCount = 3;

            ref float attackCycleCounter = ref npc.Infernum().ExtraAI[0];

            // Disable contact damage.
            npc.damage = 0;

            // Grant the target infinite flight time.
            target.wingTime = target.wingTimeMax;

            // Summon swords and clap on the first frame.
            if (attackTimer == 1f)
            {
                TeleportTo(npc, target.Center - Vector2.UnitY * 300f);

                if (Main.netMode != NetmodeID.MultiplayerClient)
                {
                    for (int i = 0; i < terraprismaCount; i++)
                    {
                        ProjectileSpawnManagementSystem.PrepareProjectileForSpawning(sword =>
                        {
                            sword.ModProjectile<LanceCreatingSword>().SwordIndex = i;
                            sword.ModProjectile<LanceCreatingSword>().SwordCount = terraprismaCount;
                            sword.ModProjectile<LanceCreatingSword>().AttackDelay = terraprismaAttackDelay;
                        });

                        Utilities.NewProjectileBetter(npc.Center, -Vector2.UnitY * 4f, ModContent.ProjectileType<LanceCreatingSword>(), SwordDamage, 0f, -1, npc.whoAmI, i / (float)terraprismaCount);
                    }

                    npc.netUpdate = true;
                }
            }

            if (attackTimer >= shootDelay)
            {
                attackTimer = 0f;

                Utilities.DeleteAllProjectiles(false, ModContent.ProjectileType<LanceCreatingSword>());
                attackCycleCounter++;
                if (attackCycleCounter >= attackCycleCount)
                    SelectNextAttack(npc);
                npc.netUpdate = true;
            }
        }

        public static void DoBehavior_LanceWallBarrage(NPC npc, Player target, ref float attackTimer, ref float leftArmFrame, ref float rightArmFrame)
        {
            int hoverRedirectTime = 40;
            int startingWallShootCountdown = 67;
            int endingWallShootCountdown = 27;
            int horizontalShootTime = 480;
            int horizontalLanceTransitionDelay = 50;
            int verticalLanceCount = 36;
            int verticalLanceTransitionDelay = 75;
            float horizontalLanceSpacing = 168f;
            float wallHorizontalOffset = 876f;
            float verticalLanceArea = 270f;
            ref float horizontalWallDirection = ref npc.Infernum().ExtraAI[0];
            ref float wallShootCountdown = ref npc.Infernum().ExtraAI[1];
            ref float wallShootDelay = ref npc.Infernum().ExtraAI[2];
            ref float wallShootCounter = ref npc.Infernum().ExtraAI[3];
            ref float attackSubstate = ref npc.Infernum().ExtraAI[4];

            bool goingAgainstWalls = Math.Abs(target.velocity.X) >= 6f && Math.Sign(target.velocity.X) != horizontalWallDirection;

            // Grant the target infinite flight time.
            target.wingTime = target.wingTimeMax;

            switch ((int)attackSubstate)
            {
                case 0:
                    // Decide the wall direction on the first frame.
                    // If the player has a lot of momentum in a certain direction, it will be chosen in such a way that the player can simply retain their current direction, so as to
                    // not require sudden, jarring turns.
                    // Otherwise, it'll simply be randomized.
                    if (attackTimer == 1f)
                    {
                        horizontalWallDirection = Main.rand.NextBool().ToDirectionInt();
                        if (Math.Abs(target.velocity.X) >= 10f)
                            horizontalWallDirection = Math.Sign(target.velocity.X);

                        wallShootDelay = startingWallShootCountdown;
                        wallShootCountdown = wallShootDelay;
                        npc.netUpdate = true;
                    }

                    // Redirect above the target.
                    Vector2 hoverDestination = target.Center + new Vector2(horizontalWallDirection * -270f, -196f);
                    npc.velocity *= 0.8f;
                    npc.Center = Vector2.SmoothStep(npc.Center, hoverDestination, 0.16f);
                    if (attackTimer < hoverRedirectTime)
                        break;

                    // Shoot lance walls.
                    wallShootCountdown--;

                    // Prepare for the next wall and fire.
                    if (wallShootCountdown <= 0f && attackTimer < hoverRedirectTime + horizontalShootTime)
                    {
                        if (Main.netMode == NetmodeID.MultiplayerClient)
                            break;

                        wallShootDelay = MathHelper.Clamp(wallShootDelay - 8f, endingWallShootCountdown, startingWallShootCountdown);
                        wallShootCountdown = wallShootDelay;
                        if (goingAgainstWalls)
                            wallShootCountdown *= 0.6f;

                        float lanceDirection = (Vector2.UnitX * horizontalWallDirection).ToRotation();
                        for (int i = -16; i < 16; i++)
                        {
                            float lanceHue = ((i + 16f) / 32f) * 4f % 1f;
                            Vector2 lanceSpawnPosition = target.Center + new Vector2(-horizontalWallDirection * wallHorizontalOffset, horizontalLanceSpacing * i);
                            if (wallShootCounter % 2f == 1f)
                            {
                                lanceSpawnPosition += Vector2.UnitY * horizontalLanceSpacing * 0.5f;
                                lanceHue = 1f - lanceHue;
                            }

                            ProjectileSpawnManagementSystem.PrepareProjectileForSpawning(lance =>
                            {
                                lance.MaxUpdates = 1;
                                lance.ModProjectile<EtherealLance>().Time = 20;
                                lance.ModProjectile<EtherealLance>().PlaySoundOnFiring = i == 0;
                                lance.ModProjectile<EtherealLance>().SoundPitch = (npc.ai[1] - hoverRedirectTime) / horizontalShootTime * 0.35f;
                            });
                            Utilities.NewProjectileBetter(lanceSpawnPosition, Vector2.Zero, ModContent.ProjectileType<EtherealLance>(), LanceDamage, 0f, -1, lanceDirection, lanceHue);
                        }

                        wallShootCounter++;
                        npc.netUpdate = true;
                    }

                    if (attackTimer >= hoverRedirectTime + horizontalShootTime + horizontalLanceTransitionDelay)
                    {
                        Utilities.DeleteAllProjectiles(false, ModContent.ProjectileType<EtherealLance>());

                        attackTimer = 0f;
                        attackSubstate = 1f;
                        npc.netUpdate = true;
                    }
                    break;

                // Suddenly summon a bunch of lances above the target.
                case 1:
                    if (attackTimer == 1f)
                    {
                        TeleportTo(npc, target.Center - Vector2.UnitY * 300f);

                        for (int i = 0; i < verticalLanceCount; i++)
                        {
                            float verticalLanceOffset = MathHelper.Lerp(-verticalLanceArea, verticalLanceArea, i / 35f);
                            float lanceHue = i / (float)verticalLanceCount * 2f % 1f;
                            Vector2 lanceSpawnPosition = target.Center + new Vector2(target.direction * 950f + target.velocity.X * 40f, verticalLanceOffset + target.velocity.Y * 18f);

                            ProjectileSpawnManagementSystem.PrepareProjectileForSpawning(lance =>
                            {
                                lance.ModProjectile<EtherealLance>().Time = -70;
                            });
                            Utilities.NewProjectileBetter(lanceSpawnPosition, Vector2.Zero, ModContent.ProjectileType<EtherealLance>(), LanceDamage, 0f, -1, (Vector2.UnitX * -target.direction).ToRotation(), lanceHue);
                        }
                    }

                    if (attackTimer >= verticalLanceTransitionDelay)
                        SelectNextAttack(npc);

                    break;
            }
        }

        public static void DoBehavior_BackstabbingLances(NPC npc, Player target, ref float attackTimer, ref float leftArmFrame, ref float rightArmFrame)
        {
            int backstabbingLanceTime = 104;
            int lanceReleaseRate = 3;
            int hoverRedirectTime = 30;
            int semicircleLanceShootTime = 30;
            float backstabbingLanceOffset = 700f;
            float semicircleLanceOffset = 950f;
            float idleHoverSpeed = 7f;
            float idleHoverAcceleration = 0.36f;
            float maxPerpendicularOffset = 450f;

            if (ShouldBeEnraged)
            {
                backstabbingLanceTime -= 12;
                lanceReleaseRate--;
                semicircleLanceShootTime -= 4;
                maxPerpendicularOffset += 72f;
            }

            ref float lanceSemicircleSpawnCenterX = ref npc.Infernum().ExtraAI[0];
            ref float lanceSemicircleSpawnCenterY = ref npc.Infernum().ExtraAI[1];
            ref float lanceSemicircleDirection = ref npc.Infernum().ExtraAI[2];

            // Disable contact damage.
            npc.damage = 0;

            // Release lances from behind the player.
            if (attackTimer < backstabbingLanceTime)
            {
                // Play the lance sound on the first frame.
                if (attackTimer == 1f)
                    SoundEngine.PlaySound(SoundID.Item162, npc.Center);

                // Slow down.
                npc.velocity *= 0.95f;

                if (Main.netMode != NetmodeID.MultiplayerClient && attackTimer % lanceReleaseRate == lanceReleaseRate - 1f)
                {
                    float lanceHue = attackTimer / backstabbingLanceTime;
                    Vector2 lanceSpawnPosition = target.Center - target.velocity.SafeNormalize(Vector2.UnitY) * backstabbingLanceOffset;
                    Utilities.NewProjectileBetter(lanceSpawnPosition, Vector2.Zero, ModContent.ProjectileType<EtherealLance>(), LanceDamage, 0f, -1, target.AngleFrom(lanceSpawnPosition), lanceHue);
                }

                return;
            }

            // Move towards the target.
            if (attackTimer <= backstabbingLanceTime + hoverRedirectTime)
            {
                // Clap hands on the first frame.
                leftArmFrame = rightArmFrame = 1f;
                if (attackTimer == backstabbingLanceTime + 1f)
                {
                    SoundEngine.PlaySound(SoundID.Item122, npc.Center);
                    SoundEngine.PlaySound(SoundID.Item161, npc.Center);
                    if (Main.netMode != NetmodeID.MultiplayerClient)
                    {
                        Utilities.NewProjectileBetter(npc.Center + Vector2.UnitY * 8f, Vector2.Zero, ModContent.ProjectileType<ShimmeringLightWave>(), 0, 0f);
                        npc.netUpdate = true;
                    }
                }

                Vector2 hoverDestination = target.Center + new Vector2((target.Center.X < npc.Center.X).ToDirectionInt() * 350f, -225f);
                npc.velocity *= 0.8f;
                npc.Center = Vector2.Lerp(npc.Center, hoverDestination, 0.12f);

                return;
            }

            // Hover near the target.
            if (npc.WithinRange(target.Center, 275f))
                npc.velocity *= 0.96f;
            else
                npc.SimpleFlyMovement(npc.SafeDirectionTo(target.Center) * idleHoverSpeed, idleHoverAcceleration);

            // Summon the lance semicircle.
            if (Main.netMode != NetmodeID.MultiplayerClient && attackTimer <= backstabbingLanceTime + hoverRedirectTime + semicircleLanceShootTime)
            {
                // Initialize the lance direction.
                if (lanceSemicircleDirection == 0f)
                {
                    lanceSemicircleDirection = target.velocity.ToRotation() + MathHelper.Pi;
                    lanceSemicircleSpawnCenterX = target.Center.X;
                    lanceSemicircleSpawnCenterY = target.Center.Y;
                    npc.netUpdate = true;
                }

                int delayUntilLanceFires = (int)(attackTimer - backstabbingLanceTime - hoverRedirectTime - semicircleLanceShootTime) * 2 - 24;
                if (ShouldBeEnraged)
                    delayUntilLanceFires += 10;

                float lanceHue = 1f - Utils.GetLerpValue(0f, semicircleLanceShootTime, attackTimer - backstabbingLanceTime - hoverRedirectTime, true);
                float semicirclePerpendicularOffset = Utils.Remap(attackTimer - backstabbingLanceTime - hoverRedirectTime, 0f, semicircleLanceShootTime, -maxPerpendicularOffset, maxPerpendicularOffset);
                Vector2 semicircleOffset = lanceSemicircleDirection.ToRotationVector2() * semicircleLanceOffset + (lanceSemicircleDirection + MathHelper.PiOver2).ToRotationVector2() * semicirclePerpendicularOffset;
                Vector2 lanceSpawnPosition = new Vector2(lanceSemicircleSpawnCenterX, lanceSemicircleSpawnCenterY) + semicircleOffset;

                ProjectileSpawnManagementSystem.PrepareProjectileForSpawning(lance =>
                {
                    lance.ModProjectile<EtherealLance>().Time = delayUntilLanceFires;
                });
                Utilities.NewProjectileBetter(lanceSpawnPosition, Vector2.Zero, ModContent.ProjectileType<EtherealLance>(), LanceDamage, 0f, -1, lanceSemicircleDirection + MathHelper.Pi, lanceHue);
            }

            if (attackTimer >= backstabbingLanceTime + hoverRedirectTime + semicircleLanceShootTime + 120f)
                SelectNextAttack(npc);
        }

        public static void DoBehavior_MesmerizingMagic(NPC npc, Player target, ref float attackTimer, ref float leftArmFrame, ref float rightArmFrame)
        {
            int shootRate = 75;
            int shootCount = 6;
            float wrappedattackTimer = attackTimer % shootRate;
            float slowdownFactor = Utils.GetLerpValue(shootRate - 8f, shootRate - 24f, wrappedattackTimer, true);
            float boltShootSpeed = 17f;
            ref float telegraphRotation = ref npc.Infernum().ExtraAI[0];
            ref float telegraphInterpolant = ref npc.Infernum().ExtraAI[1];
            ref float boltCount = ref npc.Infernum().ExtraAI[2];
            ref float totalHandsToShootFrom = ref npc.Infernum().ExtraAI[3];
            ref float shootCounter = ref npc.Infernum().ExtraAI[4];

            // Initialize things.
            if (totalHandsToShootFrom == 0f)
            {
                boltCount = 25f;
                totalHandsToShootFrom = 1f;
                if (InPhase2(npc))
                {
                    boltCount = 18f;
                    totalHandsToShootFrom = 2f;
                }

                if (ShouldBeEnraged)
                {
                    boltCount = 28f;
                    totalHandsToShootFrom = 2f;
                }

                npc.netUpdate = true;
            }

            // Calculate the telegraph interpolant.
            telegraphInterpolant = Utils.GetLerpValue(24f, shootRate - 18f, wrappedattackTimer);

            // Hover to the top left/right of the target.
            Vector2 hoverDestination = target.Center + new Vector2((target.Center.X < npc.Center.X).ToDirectionInt() * 120f, -300f);
            Vector2 idealVelocity = npc.SafeDirectionTo(hoverDestination) * 8f;
            if (!npc.WithinRange(hoverDestination, 40f))
                npc.SimpleFlyMovement(idealVelocity * slowdownFactor, slowdownFactor * 0.7f);
            else
                npc.velocity *= 0.93f;

            // Determinine the initial rotation of the telegraphs.
            if (wrappedattackTimer == 4f)
            {
                telegraphRotation = Main.rand.NextFloat(MathHelper.TwoPi);
                npc.netUpdate = true;
            }

            // Rotate the telegraphs.
            telegraphRotation += CalamityUtils.Convert01To010(telegraphInterpolant) * MathHelper.Pi / 75f;

            // Release magic on hands and eventually create bolts.
            int magicDustCount = (int)Math.Round(MathHelper.Lerp(1f, 5f, telegraphInterpolant));
            for (int i = 0; i < 2; i++)
            {
                if (i >= totalHandsToShootFrom)
                    break;

                int handDirection = (i == 0).ToDirectionInt();
                Vector2 handOffset = new(55f, -30f);
                Vector2 handPosition = npc.Center + handOffset * new Vector2(handDirection, 1f);

                // Create magic dust.
                for (int j = 0; j < magicDustCount; j++)
                {
                    float magicHue = (attackTimer / 45f + Main.rand.NextFloat(0.2f)) % 1f;
                    Dust rainbowMagic = Dust.NewDustPerfect(handPosition, 267);
                    rainbowMagic.color = Main.hslToRgb(magicHue, 1f, 0.5f);
                    rainbowMagic.velocity = -Vector2.UnitY.RotatedByRandom(0.6f) * Main.rand.NextFloat(1f, 4f);
                    rainbowMagic.scale *= 0.9f;
                    rainbowMagic.noGravity = true;
                }

                // Raise hands.
                if (i == 0)
                    rightArmFrame = 3;
                else
                    leftArmFrame = 3;

                // Release bolts outward and create hand explosions.
                if (wrappedattackTimer == shootRate - 1f)
                {
                    if (i == 0)
                        SoundEngine.PlaySound(SoundID.DD2_PhantomPhoenixShot with { Volume = 2.6f }, npc.Center);

                    if (Main.netMode != NetmodeID.MultiplayerClient)
                    {
                        for (int j = 0; j < boltCount; j++)
                        {
                            Vector2 boltShootVelocity = (MathHelper.TwoPi * j / boltCount + telegraphRotation).ToRotationVector2() * boltShootSpeed;
                            Utilities.NewProjectileBetter(handPosition, boltShootVelocity, ModContent.ProjectileType<AcceleratingPrismaticBolt>(), PrismaticBoltDamage, 0f, -1, 0f, j / boltCount);
                        }
                        Utilities.NewProjectileBetter(handPosition, Vector2.Zero, ModContent.ProjectileType<EmpressExplosion>(), 0, 0f);

                        if (i == 0)
                            shootCounter++;

                        npc.netUpdate = true;
                    }
                }
            }

            if (shootCounter >= shootCount)
                SelectNextAttack(npc);
        }

        public static void DoBehavior_HorizontalCharge(NPC npc, Player target, ref float attackTimer)
        {
            int chargeCount = 4;
            int redirectTime = 40;
            int chargeTime = 45;
            int attackTransitionDelay = 8;
            int boltReleaseRate = 0;
            float chargeSpeed = 56f;
            float hoverSpeed = 25f;
            ref float chargeDirection = ref npc.Infernum().ExtraAI[0];
            ref float chargeCounter = ref npc.Infernum().ExtraAI[1];

            if (chargeCounter == 0f)
                redirectTime += 16;

            if (InPhase2(npc))
            {
                boltReleaseRate = 15;
                chargeSpeed += 4f;
            }

            if (InPhase3(npc))
            {
                boltReleaseRate -= 5;
                chargeSpeed += 5f;
            }

            if (ShouldBeEnraged)
            {
                if (boltReleaseRate >= 5)
                    boltReleaseRate -= 4;
                chargeSpeed += 8f;
            }

            if (BossRushEvent.BossRushActive)
            {
                chargeSpeed *= 1.3f;
                hoverSpeed *= 1.5f;
            }

            // Initialize the charge direction.
            if (attackTimer == 1f)
            {
                chargeDirection = (target.Center.X > npc.Center.X).ToDirectionInt();
                npc.netUpdate = true;
            }

            // Hover into position before charging.
            if (attackTimer <= redirectTime)
            {
                // Scream prior to charging.
                if (attackTimer == redirectTime / 2)
                    SoundEngine.PlaySound(SoundID.Item160, npc.Center);

                Vector2 hoverDestination = target.Center + Vector2.UnitX * chargeDirection * -420f;
                npc.Center = npc.Center.MoveTowards(hoverDestination, 12.5f);
                npc.SimpleFlyMovement(npc.SafeDirectionTo(hoverDestination) * hoverSpeed, hoverSpeed * 0.16f);
                if (attackTimer == redirectTime)
                    npc.velocity *= 0.3f;
            }

            // Charge.
            // If applicable, release prismatic bolts.
            else if (attackTimer <= redirectTime + chargeTime)
            {
                npc.velocity = Vector2.Lerp(npc.velocity, Vector2.UnitX * chargeDirection * chargeSpeed, 0.15f);
                if (attackTimer == redirectTime + chargeTime)
                    npc.velocity *= 0.7f;

                // Do damage.
                npc.damage = npc.defDamage;
                if (ShouldBeEnraged)
                    npc.damage *= 2;

                if (Main.netMode != NetmodeID.MultiplayerClient && boltReleaseRate >= 1 && attackTimer % boltReleaseRate == boltReleaseRate - 1f)
                {
                    Vector2 boltVelocity = Main.rand.NextVector2Circular(8f, 8f);
                    Utilities.NewProjectileBetter(npc.Center, boltVelocity, ModContent.ProjectileType<PrismaticBolt>(), PrismaticBoltDamage, 0f, -1, npc.target, Main.rand.NextFloat());
                }
            }
            else
                npc.velocity *= 0.92f;

            if (attackTimer >= redirectTime + chargeTime + attackTransitionDelay)
            {
                attackTimer = 0f;
                chargeCounter++;
                if (chargeCounter >= chargeCount)
                    SelectNextAttack(npc);
                npc.netUpdate = true;
            }
        }

        public static void DoBehavior_EnterSecondPhase(NPC npc, Player target, ref float attackTimer)
        {
            int reappearTime = 35;

            // Don't take damage when transitioning.
            npc.dontTakeDamage = true;

            // Slow down.
            npc.velocity *= 0.8f;

            // Scream before fading out.
            if (attackTimer == 10f)
                SoundEngine.PlaySound(SoundID.Item161, npc.Center);

            // Fade out.
            if (attackTimer <= SecondPhaseFadeoutTime)
                npc.Opacity = MathHelper.Lerp(1f, 0f, attackTimer / SecondPhaseFadeoutTime);

            // Fade back in and teleport above the target.
            else if (attackTimer <= SecondPhaseFadeoutTime + reappearTime)
            {
                if (attackTimer == SecondPhaseFadeoutTime + 1f)
                {
                    npc.Center = target.Center - Vector2.UnitY * 300f;
                    npc.netUpdate = true;
                }

                npc.Opacity = Utils.GetLerpValue(0f, reappearTime, attackTimer - SecondPhaseFadeoutTime, true);
            }

            if (attackTimer >= SecondPhaseFadeoutTime + SecondPhaseFadeBackInTime)
                SelectNextAttack(npc);
        }
        
        public static void DoBehavior_RainbowWispForm(NPC npc, Player target, ref float attackTimer, ref float rightArmFrame)
        {
            int wispFormEnterTime = 60;
            int spinTime = 210;
            int prismCreationRate = 15;
            int laserReleaseDelay = 30;
            int boltReleaseRate = 43;
            int attackTransitionDelay = 360;
            float spinSpeed = 30f;
            float spinOffset = 540f;
            ref float wispColorInterpolant = ref npc.Infernum().ExtraAI[0];

            if (InPhase3(npc))
            {
                prismCreationRate -= 5;
                boltReleaseRate -= 9;
            }

            if (ShouldBeEnraged)
                boltReleaseRate -= 13;

            // Hover above the target and enter the wisp form.
            if (attackTimer <= wispFormEnterTime)
            {
                Vector2 hoverDestination = target.Center - Vector2.UnitY * 480f;
                npc.velocity = Vector2.Zero.MoveTowards(hoverDestination - npc.Center, 16f);
                wispColorInterpolant = attackTimer / wispFormEnterTime;
            }

            else
            {
                float spinSlowdownFactor = Utils.GetLerpValue(spinTime, spinTime - prismCreationRate, attackTimer - wispFormEnterTime, true);
                Vector2 hoverDestination = target.Center - Vector2.UnitY.RotatedBy(MathHelper.TwoPi / spinTime * 2f * attackTimer) * spinOffset;
                npc.velocity = Vector2.Zero.MoveTowards(hoverDestination - npc.Center, spinSlowdownFactor * spinSpeed);

                // Release prisms periodically.
                if (attackTimer % prismCreationRate == prismCreationRate - 1f && spinSlowdownFactor > 0f)
                {
                    SoundEngine.PlaySound(SoundID.DD2_KoboldExplosion, npc.Center);
                    if (Main.netMode != NetmodeID.MultiplayerClient)
                    {
                        Utilities.NewProjectileBetter(npc.Center, Vector2.Zero, ModContent.ProjectileType<EmpressExplosion>(), 0, 0f);

                        float shootDelay = attackTimer - (wispFormEnterTime + spinTime) - laserReleaseDelay;
                        Utilities.NewProjectileBetter(npc.Center, Vector2.Zero, ModContent.ProjectileType<EmpressPrism>(), 0, 0f, -1, shootDelay, npc.target);
                    }
                }

                // Raise the right hand upward.
                rightArmFrame = 3f;

                // Release prismatic bolts at the target occasionally.
                if (attackTimer % boltReleaseRate == boltReleaseRate - 1f)
                {
                    Vector2 handOffset = new(55f, -30f);
                    Vector2 handPosition = npc.Center + handOffset;
                    int bolt = Utilities.NewProjectileBetter(handPosition, Vector2.UnitY.RotatedByRandom(0.6f) * 10f, ModContent.ProjectileType<PrismaticBolt>(), PrismaticBoltDamage, 0f, -1, 0f, Main.rand.NextFloat());
                    if (Main.projectile.IndexInRange(bolt))
                        Main.projectile[bolt].localAI[0] = 0.75f;
                }
            }

            // Fade in and out.
            if (attackTimer >= wispFormEnterTime + spinTime)
            {
                Vector2 hoverDestination = target.Center - Vector2.UnitY * 425f;
                npc.velocity = Vector2.Zero.MoveTowards(hoverDestination - npc.Center, 30f);
                npc.Opacity = MathHelper.Lerp(0.3f, 1f, Utils.GetLerpValue(150f, 240f, npc.Distance(target.Center), true));

                wispColorInterpolant = Utils.GetLerpValue(wispFormEnterTime + spinTime + 60f, wispFormEnterTime + spinTime, attackTimer, true);
            }

            if (attackTimer >= wispFormEnterTime + spinTime + attackTransitionDelay)
            {
                npc.Opacity = 1f;
                SelectNextAttack(npc);
            }
        }

        public static void DoBehavior_DanceOfSwords(NPC npc, Player target, ref float attackTimer, ref float leftArmFrame, ref float rightArmFrame)
        {
            int swordCount = 4;
            int swordID = ModContent.ProjectileType<EmpressSword>();
            int attackDelay = 50;
            ref float swordIndexToUse = ref npc.Infernum().ExtraAI[0];
            ref float swordHoverOffsetAngle = ref npc.Infernum().ExtraAI[1];

            // Grant the player infinite flight time.
            target.wingTime = target.wingTimeMax;

            // Teleport above the target and create a bunch of swords on the first frame.
            if (attackTimer == 1f)
            {
                TeleportTo(npc, target.Center - Vector2.UnitY * 300f);

                if (Main.netMode != NetmodeID.MultiplayerClient)
                {
                    for (int i = 0; i < swordCount; i++)
                    {
                        float swordHue = i / (float)swordCount;                        
                        ProjectileSpawnManagementSystem.PrepareProjectileForSpawning(sword =>
                        {
                            sword.ModProjectile<EmpressSword>().SwordIndex = i;
                            sword.ModProjectile<EmpressSword>().SwordCount = swordCount;
                        });
                        Utilities.NewProjectileBetter(npc.Center - Vector2.UnitY * 50f, Vector2.UnitY * -4f, swordID, SwordDamage, 0f, -1, npc.whoAmI, swordHue);
                    }
                }
                return;
            }

            // Try to hover near the player so that they can use true melee against the empress.
            float hoverSpeed = 16f;
            Vector2 hoverDestination = target.Center + new Vector2((target.Center.X < npc.Center.X).ToDirectionInt() * 250f, -175f);
            npc.velocity = Vector2.Lerp(npc.velocity, Vector2.Zero.MoveTowards(hoverDestination - npc.Center, hoverSpeed), 0.25f);

            // Wait before attacking.
            if (attackTimer < attackDelay)
                return;

            // Choose a hover offset angle for the blade once done waiting.
            if (attackTimer == attackDelay)
            {
                // The sword should pick the side which is between the empress and the player, and then randomly pick a place on the semicircle that forms from it.
                swordHoverOffsetAngle = Main.rand.NextFloat(-MathHelper.PiOver2, MathHelper.PiOver2);
                if (target.Center.X < npc.Center.X)
                    swordHoverOffsetAngle += MathHelper.Pi;

                npc.netUpdate = true;
            }

            // Find the sword that the empress wishes to use.
            // Most of the behavior beyond this point is handled by attacking the sword itself, while the empress simply hovers.
            Projectile swordToUse = null;
            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                if (Main.projectile[i].type != swordID || !Main.projectile[i].active || Main.projectile[i].ModProjectile<EmpressSword>().SwordIndex != (int)swordIndexToUse)
                    continue;

                swordToUse = Main.projectile[i];
                swordToUse.ModProjectile<EmpressSword>().ShouldAttack = true;
                swordToUse.ModProjectile<EmpressSword>().Time = (int)(attackTimer - attackDelay);
                break;
            }

            // Check to see if the sword is done being used.
            if (npc.Infernum().ExtraAI[3] == 1f)
            {
                swordIndexToUse++;
                attackTimer = attackDelay - 1f;
                npc.Infernum().ExtraAI[3] = 0f;
                npc.netUpdate = true;

                if (swordIndexToUse >= swordCount)
                {
                    Utilities.DeleteAllProjectiles(false, ModContent.ProjectileType<EtherealLance>(), ModContent.ProjectileType<EmpressSword>());
                    SelectNextAttack(npc);
                }
            }
        }

        public static void DoBehavior_DeathAnimation(NPC npc, Player target, ref float attackTimer, ref float deathAnimationScreenShaderStrength)
        {
            int fadeInTime = 40;
            int fadeOutTime = 96;
            int deathAnimationTime = 480;
            ref float outwardExpandFactor = ref npc.Infernum().ExtraAI[1];

            // Disable damage.
            npc.damage = 0;
            npc.dontTakeDamage = true;
            npc.Calamity().ShouldCloseHPBar = true;

            // Teleport above the player and create a shockwave on the first frame.
            if (attackTimer == 1f)
            {
                npc.Center = target.Center - Vector2.UnitY * 350f;
                npc.velocity = Vector2.Zero;
                npc.netUpdate = true;

                SoundEngine.PlaySound(SoundID.Item161, target.Center);
                Utilities.CreateShockwave(npc.Center, 40, 4, 40f);
            }

            // Fade in after teleporting.
            if (attackTimer >= 1f)
            {
                deathAnimationScreenShaderStrength = Utils.GetLerpValue(1f, fadeInTime, attackTimer, true);
                outwardExpandFactor = Utils.GetLerpValue(-fadeOutTime, 0f, attackTimer - deathAnimationTime, true);
                npc.Opacity = (float)Math.Pow(deathAnimationScreenShaderStrength, 3D) * (1f - outwardExpandFactor);

                MoonlordDeathDrama.RequestLight(outwardExpandFactor * 1.2f, target.Center);
            }

            // Create sparkles everywhere.
            if (Main.netMode != NetmodeID.MultiplayerClient)
            {
                Vector2 sparkleSpawnPosition = target.Center + Main.rand.NextVector2Square(-900f, 900f);
                Utilities.NewProjectileBetter(sparkleSpawnPosition, Vector2.Zero, ModContent.ProjectileType<EmpressSparkle>(), 0, 0f);
            }

            // Play magic sounds.
            if (attackTimer % 24f == 23f && attackTimer >= fadeInTime * 2f)
                SoundEngine.PlaySound(SoundID.Item28, target.Center + Main.rand.NextVector2CircularEdge(400f, 400f));

            // Cast shimmers.
            for (int i = 0; i < 6; i++)
            {
                float dustPersistence = MathHelper.Lerp(1.3f, 0.7f, npc.Opacity);
                Color newColor = Main.hslToRgb(Main.rand.NextFloat(), 1f, 0.5f);
                Dust rainbowMagic = Dust.NewDustDirect(npc.position, npc.width, npc.height, 267, 0f, 0f, 0, newColor, 1f);
                rainbowMagic.position = npc.Center + Main.rand.NextVector2Circular(npc.width * 12f, npc.height * 12f) + new Vector2(0f, -150f);
                rainbowMagic.velocity *= Main.rand.NextFloat(0.8f);
                rainbowMagic.noGravity = true;
                rainbowMagic.fadeIn = 0.7f + Main.rand.NextFloat(dustPersistence * 0.7f);
                rainbowMagic.velocity += Vector2.UnitY * 3f;
                rainbowMagic.scale = 0.6f;

                rainbowMagic = Dust.CloneDust(rainbowMagic);
                rainbowMagic.scale /= 2f;
                rainbowMagic.fadeIn *= 0.85f;
            }

            // Drop loot once the animation is over.
            if (attackTimer >= deathAnimationTime)
            {
                npc.active = false;
                npc.NPCLoot();
            }
        }

        public static void ClearAwayEntities()
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            // Clear any clones or other things that might remain from other attacks.
            int[] projectilesToClearAway = new int[]
            {
                ModContent.ProjectileType<AcceleratingPrismaticBolt>(),
                ModContent.ProjectileType<EmpressPrism>(),
                ModContent.ProjectileType<EmpressSparkle>(),
                ModContent.ProjectileType<EmpressSword>(),
                ModContent.ProjectileType<EtherealLance>(),
                ModContent.ProjectileType<LightBolt>(),
                ModContent.ProjectileType<LightOrb>(),
                ModContent.ProjectileType<LightOverloadBeam>(),
                ModContent.ProjectileType<PrismaticBolt>(),
                ModContent.ProjectileType<PrismLaserbeam>(),
                ModContent.ProjectileType<SpinningPrismLaserbeam>(),
            };

            for (int j = 0; j < 2; j++)
            {
                for (int i = 0; i < Main.maxProjectiles; i++)
                {
                    if (projectilesToClearAway.Contains(Main.projectile[i].type) && Main.projectile[i].active)
                        Main.projectile[i].Kill();
                }
            }
        }

        public static void SelectNextAttack(NPC npc)
        {
            int phaseCycleIndex = (int)npc.Infernum().ExtraAI[5];

            npc.ai[0] = (int)Phase1AttackCycle[phaseCycleIndex % Phase1AttackCycle.Length];
            if (InPhase2(npc))
                npc.ai[0] = (int)Phase2AttackCycle[phaseCycleIndex % Phase2AttackCycle.Length];
            if (InPhase3(npc))
                npc.ai[0] = (int)Phase3AttackCycle[phaseCycleIndex % Phase3AttackCycle.Length];
            if (InPhase4(npc))
                npc.ai[0] = (int)Phase4AttackCycle[phaseCycleIndex % Phase4AttackCycle.Length];

            npc.ai[0] = (int)EmpressOfLightAttackType.DanceOfSwords;
            npc.Infernum().ExtraAI[5]++;

            for (int i = 0; i < 5; i++)
                npc.Infernum().ExtraAI[i] = 0f;
            npc.ai[1] = 0f;
            npc.netUpdate = true;
        }

        #endregion AI and Behaviors

        #region Drawing and Frames
        public static void PrepareShader()
        {
            Main.graphics.GraphicsDevice.Textures[1] = ModContent.Request<Texture2D>("InfernumMode/Content/BehaviorOverrides/BossAIs/EmpressOfLight/EmpressOfLightWingsTexture").Value;
        }

        public override bool PreDraw(NPC npc, SpriteBatch spriteBatch, Color drawColor)
        {
            EmpressOfLightAttackType attackType = (EmpressOfLightAttackType)npc.ai[0];
            float attackTimer = npc.ai[1];
            float WingFrameCounter = npc.localAI[0];

            Texture2D texture = TextureAssets.Npc[npc.type].Value;
            Vector2 baseDrawPosition = npc.Center - Main.screenPosition;
            Color baseColor = Color.White * npc.Opacity;
            Texture2D wingOutlineTexture = TextureAssets.Extra[ExtrasID.HallowBossWingsBack].Value;
            Texture2D leftArmTexture = TextureAssets.Extra[ExtrasID.HallowBossArmsLeft].Value;
            Texture2D rightArmTexture = TextureAssets.Extra[ExtrasID.HallowBossArmsRight].Value;
            Texture2D wingTexture = TextureAssets.Extra[ExtrasID.HallowBossWings].Value;
            Texture2D tentacleTexture = TextureAssets.Extra[ExtrasID.HallowBossTentacles].Value;
            Texture2D dressGlowmaskTexture = TextureAssets.Extra[ExtrasID.HallowBossSkirt].Value;

            Rectangle tentacleFrame = tentacleTexture.Frame(1, 8, 0, (int)(WingFrameCounter / 5f) % 8);
            Rectangle wingFrame = wingOutlineTexture.Frame(1, 11, 0, (int)(WingFrameCounter / 5f) % 11);
            Rectangle leftArmFrame = leftArmTexture.Frame(1, 7, 0, (int)npc.localAI[1]);
            Rectangle rightArmFrame = rightArmTexture.Frame(1, 7, 0, (int)npc.localAI[2]);
            Vector2 origin = leftArmFrame.Size() / 2f;
            Vector2 origin2 = rightArmFrame.Size() / 2f;
            SpriteEffects direction = npc.spriteDirection == 1 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
            int leftArmDrawOrder = 0;
            int rightArmDrawOrder = 0;
            if (npc.localAI[1] == 5f)
                leftArmDrawOrder = 1;

            if (npc.localAI[2] == 5f)
                rightArmDrawOrder = 1;

            float baseColorOpacity = 1f;
            int laggingAfterimageCount = 0;
            int baseDuplicateCount = 0;
            float afterimageOffsetFactor = 0f;
            float opacity = 0f;
            float duplicateFade = 0f;

            // Define variables for the horizontal charge state.
            if (attackType == EmpressOfLightAttackType.HorizontalCharge)
            {
                afterimageOffsetFactor = Utils.GetLerpValue(0f, 30f, attackTimer, true) * Utils.GetLerpValue(90f, 30f, attackTimer, true);
                opacity = Utils.GetLerpValue(0f, 30f, attackTimer, true) * Utils.GetLerpValue(90f, 70f, attackTimer, true);
                duplicateFade = Utils.GetLerpValue(0f, 15f, attackTimer, true) * Utils.GetLerpValue(45f, 30f, attackTimer, true);
                baseColor = Color.Lerp(baseColor, Color.White, afterimageOffsetFactor);
                baseColorOpacity *= 1f - duplicateFade;
                laggingAfterimageCount = 4;
                baseDuplicateCount = 3;
            }

            if (attackType == EmpressOfLightAttackType.EnterSecondPhase)
            {
                afterimageOffsetFactor = Utils.GetLerpValue(30f, SecondPhaseFadeoutTime, attackTimer, true) *
                    Utils.GetLerpValue(SecondPhaseFadeBackInTime, 0f, attackTimer - SecondPhaseFadeoutTime, true);
                opacity = Utils.GetLerpValue(0f, 60f, attackTimer, true) *
                    Utils.GetLerpValue(SecondPhaseFadeBackInTime, SecondPhaseFadeBackInTime - 60f, attackTimer - SecondPhaseFadeoutTime, true);
                duplicateFade = Utils.GetLerpValue(0f, 60f, attackTimer, true) *
                    Utils.GetLerpValue(SecondPhaseFadeBackInTime, SecondPhaseFadeBackInTime - 60f, attackTimer - SecondPhaseFadeoutTime, true);
                baseColor = Color.Lerp(baseColor, Color.White, afterimageOffsetFactor);
                baseColorOpacity *= 1f - duplicateFade;
                baseDuplicateCount = 4;
            }

            if (attackType is EmpressOfLightAttackType.RainbowWispForm or EmpressOfLightAttackType.DeathAnimation)
            {
                float brightness = npc.Infernum().ExtraAI[0] * (npc.Infernum().ExtraAI[ScreenShaderIntensityIndex] + 1f);

                afterimageOffsetFactor = opacity = brightness;

                if (opacity > 0f)
                    baseColorOpacity = opacity;

                baseColor = Color.Lerp(baseColor, new Color(1f, 1f, 1f, 0f), baseColorOpacity);
                baseDuplicateCount = 2;
                laggingAfterimageCount = 4;
            }

            if (baseDuplicateCount + laggingAfterimageCount > 0)
            {
                for (int i = -baseDuplicateCount; i <= baseDuplicateCount + laggingAfterimageCount; i++)
                {
                    if (i == 0)
                        continue;

                    Color duplicateColor = Color.White;
                    Vector2 drawPosition = baseDrawPosition;

                    // Create cool afterimages while charging at the target.
                    if (attackType == EmpressOfLightAttackType.HorizontalCharge)
                    {
                        float hue = (i + 5f) / 10f;
                        float drawOffsetFactor = 80f;
                        Vector3 offsetInformation = Vector3.Transform(Vector3.Forward,
                            Matrix.CreateRotationX((Main.GlobalTimeWrappedHourly - 0.3f + i * 0.1f) * 0.7f * MathHelper.TwoPi) *
                            Matrix.CreateRotationY((Main.GlobalTimeWrappedHourly - 0.8f + i * 0.3f) * 0.7f * MathHelper.TwoPi) *
                            Matrix.CreateRotationZ((Main.GlobalTimeWrappedHourly + i * 0.5f) * 0.1f * MathHelper.TwoPi));
                        drawOffsetFactor += Utils.GetLerpValue(-1f, 1f, offsetInformation.Z, true) * 150f;
                        Vector2 drawOffset = new Vector2(offsetInformation.X, offsetInformation.Y) * drawOffsetFactor * afterimageOffsetFactor;
                        drawOffset = drawOffset.RotatedBy(MathHelper.TwoPi * attackTimer / 180f);

                        float luminanceInterpolant = Utils.GetLerpValue(90f, 0f, attackTimer, true);
                        duplicateColor = Main.hslToRgb(hue, 1f, MathHelper.Lerp(0.5f, 1f, luminanceInterpolant)) * opacity * 0.8f;
                        duplicateColor.A /= 3;
                        drawPosition += drawOffset;
                    }

                    // Handle the wisp form afterimages.
                    if (attackType is EmpressOfLightAttackType.RainbowWispForm or EmpressOfLightAttackType.DeathAnimation)
                    {
                        float hue = (i + 5f) / 10f % 1f;
                        float drawOffsetFactor = 80f;
                        Vector3 offsetInformation = Vector3.Transform(Vector3.Forward,
                            Matrix.CreateRotationX((Main.GlobalTimeWrappedHourly * 1.3f - 0.4f + i * 0.16f) * 0.7f * MathHelper.TwoPi) *
                            Matrix.CreateRotationY((Main.GlobalTimeWrappedHourly * 1.3f - 0.7f + i * 0.32f) * 0.7f * MathHelper.TwoPi) *
                            Matrix.CreateRotationZ((Main.GlobalTimeWrappedHourly * 1.3f + 0.3f + i * 0.6f) * 0.1f * MathHelper.TwoPi));
                        drawOffsetFactor += Utils.GetLerpValue(-1f, 1f, offsetInformation.Z, true) * 30f;
                        drawOffsetFactor *= npc.Infernum().ExtraAI[ScreenShaderIntensityIndex] * 8f + 1f;

                        Vector2 drawOffset = new Vector2(offsetInformation.X, offsetInformation.Y) * drawOffsetFactor * afterimageOffsetFactor;
                        drawOffset = drawOffset.RotatedBy(MathHelper.TwoPi * attackTimer / 180f);

                        duplicateColor = Main.hslToRgb(hue, 1f, 0.6f) * opacity;
                        if (i > baseDuplicateCount)
                            duplicateColor = Main.hslToRgb((i - baseDuplicateCount - 1f) / laggingAfterimageCount % 1f, 1f, 0.5f) * opacity;

                        duplicateColor.A /= 12;
                        drawPosition += drawOffset;
                    }

                    // Do the transition visuals for phase 2.
                    if (attackType == EmpressOfLightAttackType.EnterSecondPhase)
                    {
                        // Fade in.
                        if (attackTimer >= SecondPhaseFadeoutTime)
                        {
                            int offsetIndex = i;
                            if (offsetIndex < 0)
                                offsetIndex++;

                            Vector2 circularOffset = ((offsetIndex + 0.5f) * MathHelper.PiOver4 + Main.GlobalTimeWrappedHourly * MathHelper.Pi * 1.333f).ToRotationVector2();
                            drawPosition += circularOffset * afterimageOffsetFactor * new Vector2(600f, 150f);
                        }

                        // Fade out and create afterimages that dissipate.
                        else
                            drawPosition += Vector2.UnitX * i * afterimageOffsetFactor * 200f;

                        duplicateColor = Color.White * opacity * baseColorOpacity * 0.8f;
                        duplicateColor.A /= 3;
                    }

                    // Create lagging afterimages.
                    if (i > baseDuplicateCount)
                    {
                        float lagBehindFactor = Utils.GetLerpValue(30f, 70f, attackTimer, true);
                        if (lagBehindFactor == 0f)
                            continue;

                        drawPosition = baseDrawPosition + npc.velocity * -3f * (i - baseDuplicateCount - 1f) * lagBehindFactor;
                        duplicateColor *= 1f - duplicateFade;
                    }

                    // Draw wings.
                    duplicateColor *= npc.Opacity;
                    spriteBatch.Draw(wingOutlineTexture, drawPosition, wingFrame, duplicateColor, npc.rotation, wingFrame.Size() / 2f, npc.scale * 2f, direction, 0f);
                    spriteBatch.Draw(wingTexture, drawPosition, wingFrame, duplicateColor, npc.rotation, wingFrame.Size() / 2f, npc.scale * 2f, direction, 0f);

                    // Draw tentacles in phase 2.
                    if (InPhase2(npc))
                        spriteBatch.Draw(tentacleTexture, drawPosition, tentacleFrame, duplicateColor, npc.rotation, npc.frame.Size() * 0.5f, npc.scale, direction, 0f);

                    // Draw the base texture.
                    spriteBatch.Draw(texture, drawPosition, npc.frame, duplicateColor, npc.rotation, npc.frame.Size() * 0.5f, npc.scale, direction, 0f);

                    // Draw hands.
                    for (int j = 0; j < 2; j++)
                    {
                        if (j == leftArmDrawOrder)
                            spriteBatch.Draw(leftArmTexture, drawPosition, leftArmFrame, duplicateColor, npc.rotation, origin, npc.scale, direction, 0f);

                        if (j == rightArmDrawOrder)
                            spriteBatch.Draw(rightArmTexture, drawPosition, rightArmFrame, duplicateColor, npc.rotation, origin2, npc.scale, direction, 0f);
                    }
                }
            }

            baseColor *= baseColorOpacity;
            void DrawInstance(Vector2 drawPosition, Color color, Color? tentacleDressColorOverride = null)
            {
                color *= npc.Opacity;

                // Draw wings. This involves usage of a shader to give the wing texture.
                spriteBatch.Draw(wingOutlineTexture, drawPosition, wingFrame, color, npc.rotation, wingFrame.Size() / 2f, npc.scale * 2f, direction, 0f);

                spriteBatch.EnterShaderRegion();

                DrawData wingData = new(wingTexture, drawPosition, wingFrame, color, npc.rotation, wingFrame.Size() / 2f, npc.scale * 2f, direction, 0);
                PrepareShader();
                GameShaders.Misc["HallowBoss"].Apply(wingData);
                wingData.Draw(spriteBatch);
                spriteBatch.ExitShaderRegion();

                float pulse = (float)Math.Sin(Main.GlobalTimeWrappedHourly * MathHelper.Pi) * 0.5f + 0.5f;
                Color tentacleDressColor = Main.hslToRgb((pulse * 0.08f + 0.6f) % 1f, 1f, 0.5f);
                tentacleDressColor.A = 0;
                tentacleDressColor *= 0.6f;
                if (ShouldBeEnraged)
                {
                    tentacleDressColor = GetDaytimeColor(Main.GlobalTimeWrappedHourly * 0.63f);
                    tentacleDressColor.A = 0;
                    tentacleDressColor *= 0.3f;
                }
                tentacleDressColor = tentacleDressColorOverride ?? tentacleDressColor;
                tentacleDressColor *= baseColorOpacity * npc.Opacity;

                // Draw tentacles.
                if (InPhase2(npc))
                {
                    spriteBatch.Draw(tentacleTexture, drawPosition, tentacleFrame, color, npc.rotation, npc.frame.Size() * 0.5f, npc.scale, direction, 0f);
                    for (int i = 0; i < 4; i++)
                    {
                        Vector2 drawOffset = npc.rotation.ToRotationVector2().RotatedBy(MathHelper.TwoPi * i / 4f + MathHelper.PiOver4) * MathHelper.Lerp(2f, 8f, pulse);
                        spriteBatch.Draw(tentacleTexture, drawPosition + drawOffset, tentacleFrame, tentacleDressColor, npc.rotation, npc.frame.Size() * 0.5f, npc.scale, direction, 0f);
                    }
                }

                // Draw the base texture.
                spriteBatch.Draw(texture, drawPosition, npc.frame, color, npc.rotation, npc.frame.Size() * 0.5f, npc.scale, direction, 0f);

                // Draw the dress.
                if (InPhase2(npc))
                {
                    for (int i = 0; i < 4; i++)
                    {
                        Vector2 drawOffset = npc.rotation.ToRotationVector2().RotatedBy(MathHelper.TwoPi * i / 4f + MathHelper.PiOver4) * MathHelper.Lerp(2f, 8f, pulse);
                        spriteBatch.Draw(dressGlowmaskTexture, drawPosition + drawOffset, null, tentacleDressColor, npc.rotation, npc.frame.Size() * 0.5f, npc.scale, direction, 0f);
                    }
                }

                // Draw arms.
                for (int k = 0; k < 2; k++)
                {
                    if (k == leftArmDrawOrder)
                        spriteBatch.Draw(leftArmTexture, drawPosition, leftArmFrame, color, npc.rotation, origin, npc.scale, direction, 0f);

                    if (k == rightArmDrawOrder)
                        spriteBatch.Draw(rightArmTexture, drawPosition, rightArmFrame, color, npc.rotation, origin2, npc.scale, direction, 0f);
                }
            }

            if (attackType == EmpressOfLightAttackType.RainbowWispForm && npc.Infernum().ExtraAI[0] > 0f || attackType == EmpressOfLightAttackType.DeathAnimation)
            {
                int instanceCount = 10;
                float wispInterpolant = npc.Infernum().ExtraAI[0];
                float wispOffset = 20f;
                List<float> wispOffsetFactors = new() { 1f };
                if (attackType == EmpressOfLightAttackType.DeathAnimation)
                {
                    wispInterpolant = npc.Infernum().ExtraAI[ScreenShaderIntensityIndex];
                    wispOffset += wispInterpolant * 350f + npc.Infernum().ExtraAI[1] * 900f;

                    int offsetCount = 30;
                    for (int i = 0; i < offsetCount; i++)
                        wispOffsetFactors.Insert(0, i / (float)(offsetCount - 1f));
                }

                foreach (float offsetFactor in wispOffsetFactors)
                {
                    for (int i = 0; i < instanceCount; i++)
                    {
                        float spinFactor = 1f;
                        Color wispColor = Main.hslToRgb((Main.GlobalTimeWrappedHourly * offsetFactor * 0.6f + 0.2f + i / (float)instanceCount) % 1f, 1f, 0.5f) * baseColorOpacity * 0.2f;
                        wispColor = Color.Lerp(baseColor, wispColor, wispInterpolant);
                        if (wispOffsetFactors.Count >= 2)
                        {
                            // Uses a factor of the golden ratio to ensure that angles are less likely to unwind into unnatural streaks.
                            spinFactor = (1f - offsetFactor + 0.25f) * 0.6180339f * 1.5f;
                            wispColor *= 1f - offsetFactor;
                        }

                        wispColor.A /= 6;

                        Vector2 drawOffset = (MathHelper.TwoPi * i / 10f + Main.GlobalTimeWrappedHourly * spinFactor * 3f).ToRotationVector2() * wispInterpolant * wispOffset * offsetFactor;
                        DrawInstance(baseDrawPosition + drawOffset, wispColor, wispColor);
                    }
                }

                Color baseInstanceColor = baseColor;
                baseInstanceColor.A /= 5;
                DrawInstance(baseDrawPosition, baseInstanceColor);
            }
            else
                DrawInstance(baseDrawPosition, baseColor);

            // Draw telegraphs.
            if (attackType == EmpressOfLightAttackType.MesmerizingMagic)
            {
                float telegraphRotation = npc.Infernum().ExtraAI[0];
                float telegraphInterpolant = npc.Infernum().ExtraAI[1];
                float boltCount = npc.Infernum().ExtraAI[2];
                float totalHandsToShootFrom = npc.Infernum().ExtraAI[3];

                // Stop early if the telegraphs are not able to be drawn.
                if (telegraphInterpolant <= 0f)
                    return false;

                for (int i = 0; i < 2; i++)
                {
                    if (i >= totalHandsToShootFrom)
                        break;

                    int handDirection = (i == 0).ToDirectionInt();
                    float telegraphWidth = MathHelper.Lerp(0.5f, 4f, telegraphInterpolant);
                    Vector2 handOffset = new(55f, -30f);
                    Vector2 handPosition = npc.Center + handOffset * new Vector2(handDirection, 1f);

                    for (int j = 0; j < boltCount; j++)
                    {
                        Color telegraphColor = Main.hslToRgb(j / (float)boltCount, 1f, 0.5f) * (float)Math.Sqrt(telegraphInterpolant);
                        if (ShouldBeEnraged)
                            telegraphColor = GetDaytimeColor(j / (float)boltCount);
                        telegraphColor *= 0.6f;

                        Vector2 telegraphDirection = (MathHelper.TwoPi * j / boltCount + telegraphRotation).ToRotationVector2();
                        Vector2 start = handPosition;
                        Vector2 end = start + telegraphDirection * 4500f;
                        spriteBatch.DrawLineBetter(start, end, telegraphColor, telegraphWidth);
                    }
                }
            }

            return false;
        }

        public override void FindFrame(NPC npc, int frameHeight)
        {
            npc.frame.Y = InPhase2(npc).ToInt() * frameHeight;
        }
        #endregion Drawing and Frames

        #region Misc Utilities
        public static bool InPhase2(NPC npc)
        {
            float attackTimer = npc.ai[1];
            float currentPhase = npc.ai[2];
            EmpressOfLightAttackType attackType = (EmpressOfLightAttackType)npc.ai[0];
            return currentPhase >= 1f && (attackType != EmpressOfLightAttackType.EnterSecondPhase || attackTimer >= SecondPhaseFadeoutTime);
        }

        public static bool InPhase3(NPC npc) => npc.ai[2] >= 2f;

        public static bool InPhase4(NPC npc) => npc.ai[2] >= 3f;

        public static Color GetDaytimeColor(float colorInterpolant)
        {
            Color pink = Color.HotPink;
            Color cyan = Color.Cyan;
            return Color.Lerp(pink, cyan, CalamityUtils.Convert01To010(colorInterpolant * 3f % 1f));
        }
        #endregion

        #region Death Effects
        public override bool CheckDead(NPC npc)
        {
            // Just die as usual if the Empress of Light is killed during the death animation. This is done so that Cheat Sheet and other butcher effects can kill her quickly.
            if (npc.ai[0] == (int)EmpressOfLightAttackType.DeathAnimation)
                return true;

            ClearAwayEntities();
            SelectNextAttack(npc);
            npc.ai[0] = (int)EmpressOfLightAttackType.DeathAnimation;
            npc.life = npc.lifeMax;
            npc.active = true;
            npc.netUpdate = true;
            return false;
        }
        #endregion Death Effects
    }
}
