using CalamityMod;
using CalamityMod.NPCs;
using CalamityMod.NPCs.ExoMechs.Apollo;
using CalamityMod.NPCs.ExoMechs.Ares;
using CalamityMod.NPCs.ExoMechs.Artemis;
using CalamityMod.NPCs.ExoMechs.Thanatos;
using CalamityMod.UI;
using CalamityMod.World;
using InfernumMode.BehaviorOverrides.BossAIs.Draedon.Athena;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent.Events;
using Terraria.Graphics.Shaders;
using Terraria.ID;
using Terraria.ModLoader;
using static InfernumMode.ILEditingStuff.HookManager;

namespace InfernumMode.ILEditingStuff
{
	public class FixExoMechActiveDefinitionRigidityHook : IHookEdit
	{
		public static void ChangeExoMechIsActiveDefinition(ILContext il)
		{
			ILCursor cursor = new ILCursor(il);

			cursor.EmitDelegate<Func<bool>>(() =>
			{
				if (NPC.AnyNPCs(ModContent.NPCType<ThanatosHead>()))
					return true;

				if (NPC.AnyNPCs(ModContent.NPCType<AresBody>()))
					return true;

				if (NPC.AnyNPCs(ModContent.NPCType<AthenaNPC>()))
					return true;

				if (NPC.AnyNPCs(ModContent.NPCType<Artemis>()) || NPC.AnyNPCs(ModContent.NPCType<Apollo>()))
					return true;

				return false;
			});
			cursor.Emit(OpCodes.Ret);
		}

		public void Load() => ExoMechIsPresent += ChangeExoMechIsActiveDefinition;

		public void Unload() => ExoMechIsPresent -= ChangeExoMechIsActiveDefinition;
	}

	public class DrawDraedonSelectionUIWithAthena : IHookEdit
	{
		public static float AthenaIconScale
		{
			get;
			set;
		} = 1f;

		internal static void DrawSelectionUI(ILContext context)
		{
			ILCursor cursor = new ILCursor(context);
			cursor.EmitDelegate<Action>(DrawWrapper);
			cursor.Emit(OpCodes.Ret);
		}

		public static void DrawWrapper()
		{
			Vector2 drawAreaVerticalOffset = Vector2.UnitY * 105f;
			Vector2 baseDrawPosition = Main.LocalPlayer.Top + drawAreaVerticalOffset - Main.screenPosition;
			Vector2 destroyerIconDrawOffset = new Vector2(-78f, -124f);
			Vector2 primeIconDrawOffset = new Vector2(0f, -140f);
			Vector2 twinsIconDrawOffset = new Vector2(78f, -124f);
			Vector2 athenaIconDrawOffset = new Vector2(78f, -130f);

			if (InfernumMode.CanUseCustomAIs)
			{
				destroyerIconDrawOffset = new Vector2(-78f, -130f);
				primeIconDrawOffset = new Vector2(-26f, -130f);
				twinsIconDrawOffset = new Vector2(26f, -130f);

				HandleInteractionWithButton(baseDrawPosition + destroyerIconDrawOffset, (int)ExoMech.Destroyer);
				HandleInteractionWithButton(baseDrawPosition + primeIconDrawOffset, (int)ExoMech.Prime);
				HandleInteractionWithButton(baseDrawPosition + twinsIconDrawOffset, (int)ExoMech.Twins);
				HandleInteractionWithButton(baseDrawPosition + athenaIconDrawOffset, 4);
				return;
			}

			ExoMechSelectionUI.HandleInteractionWithButton(baseDrawPosition + destroyerIconDrawOffset, ExoMech.Destroyer);
			ExoMechSelectionUI.HandleInteractionWithButton(baseDrawPosition + primeIconDrawOffset, ExoMech.Prime);
			ExoMechSelectionUI.HandleInteractionWithButton(baseDrawPosition + twinsIconDrawOffset, ExoMech.Twins);
		}

		public static void HandleInteractionWithButton(Vector2 drawPosition, int exoMech)
		{
			float iconScale;
			string description;
			Texture2D iconMechTexture;

			switch (exoMech)
			{
				case 1:
					iconScale = ExoMechSelectionUI.DestroyerIconScale;
					iconMechTexture = ModContent.GetTexture("CalamityMod/ExtraTextures/UI/HeadIcon_THanos");
					description = "Thanatos, a serpentine terror with impervious armor and innumerable laser turrets.";
					break;
				case 2:
					iconScale = ExoMechSelectionUI.PrimeIconScale;
					iconMechTexture = ModContent.GetTexture("CalamityMod/ExtraTextures/UI/HeadIcon_Ares");
					description = "Ares, a heavyweight, diabolical monstrosity with four Exo superweapons.";
					break;
				case 3:
					iconScale = ExoMechSelectionUI.TwinsIconScale;
					iconMechTexture = ModContent.GetTexture("CalamityMod/ExtraTextures/UI/HeadIcon_ArtemisApollo");
					description = "Artemis and Apollo, a pair of extremely agile destroyers with pulse cannons.";
					break;
				case 4:
				default:
					iconScale = AthenaIconScale;
					iconMechTexture = ModContent.GetTexture("InfernumMode/ExtraTextures/HeadIcon_Athena");
					description = "Athena, a giant supercomputer with multiple mounted pulse turrets.";
					drawPosition.Y += 2f;
					break;
			}

			// Check for mouse collision/clicks.
			Rectangle clickArea = Utils.CenteredRectangle(drawPosition, iconMechTexture.Size() * iconScale * 0.9f);

			// Check if the mouse is hovering over the contact button area.
			bool hoveringOverIcon = ExoMechSelectionUI.MouseScreenArea.Intersects(clickArea);
			if (hoveringOverIcon)
			{
				// If so, cause the button to inflate a little bit.
				iconScale = MathHelper.Clamp(iconScale + 0.0375f, 1f, 1.35f);

				// Make the selection known if a click is done.
				if (Main.mouseLeft && Main.mouseLeftRelease)
				{
					CalamityWorld.DraedonMechToSummon = (ExoMech)exoMech;

					if (Main.netMode != NetmodeID.SinglePlayer)
					{
						var netMessage = InfernumMode.CalamityMod.GetPacket();
						netMessage.Write((byte)CalamityModMessageType.ExoMechSelection);
						netMessage.Write((int)CalamityWorld.DraedonMechToSummon);
						netMessage.Send();
					}
				}
				Main.blockMouse = Main.LocalPlayer.mouseInterface = true;
			}

			// Otherwise, if not hovering, cause the button to deflate back to its normal size.
			else
				iconScale = MathHelper.Clamp(iconScale - 0.05f, 1f, 1.2f);

			// Draw the icon with the new scale.
			Main.spriteBatch.Draw(iconMechTexture, drawPosition, null, Color.White, 0f, iconMechTexture.Size() * 0.5f, iconScale, SpriteEffects.None, 0f);

			// Draw the descrption if hovering over the icon.
			if (hoveringOverIcon)
			{
				drawPosition.X -= Main.fontMouseText.MeasureString(description).X * 0.5f;
				drawPosition.Y += 36f;
				Utils.DrawBorderStringFourWay(Main.spriteBatch, Main.fontMouseText, description, drawPosition.X, drawPosition.Y, ExoMechSelectionUI.HoverTextColor, Color.Black, Vector2.Zero, 1f);
			}

			// And update to reflect the new scale.
			switch (exoMech)
			{
				case 1:
					ExoMechSelectionUI.DestroyerIconScale = iconScale;
					break;
				case 2:
					ExoMechSelectionUI.PrimeIconScale = iconScale;
					break;
				case 3:
					ExoMechSelectionUI.TwinsIconScale = iconScale;
					break;
				case 4:
					AthenaIconScale = iconScale;
					break;
			}
		}

		public void Load() => ExoMechSelectionUIDraw += DrawSelectionUI;

		public void Unload() => ExoMechSelectionUIDraw -= DrawSelectionUI;
	}

	public class DrawBlackEffectHook : IHookEdit
	{
		public static List<int> DrawCacheBeforeBlack = new List<int>(Main.maxProjectiles);
		public static List<int> DrawCacheProjsOverSignusBlackening = new List<int>(Main.maxProjectiles);
		public static List<int> DrawCacheAdditiveLighting = new List<int>(Main.maxProjectiles);
		internal static void DrawBlackout(ILContext il)
		{
			ILCursor cursor = new ILCursor(il);

			if (!cursor.TryGotoNext(MoveType.Before, i => i.MatchCall<Main>("DrawBackgroundBlackFill")))
				return;

			cursor.EmitDelegate<Action>(() =>
			{
				for (int i = 0; i < DrawCacheBeforeBlack.Count; i++)
				{
					try
					{
						Main.instance.DrawProj(DrawCacheBeforeBlack[i]);
					}
					catch (Exception e)
					{
						TimeLogger.DrawException(e);
						Main.projectile[DrawCacheBeforeBlack[i]].active = false;
					}
				}
				DrawCacheBeforeBlack.Clear();
			});

			if (!cursor.TryGotoNext(MoveType.After, i => i.MatchCall<MoonlordDeathDrama>("DrawWhite")))
				return;

			cursor.EmitDelegate<Action>(() =>
			{
				float fadeToBlack = 0f;
				if (CalamityGlobalNPC.signus != -1)
					fadeToBlack = Main.npc[CalamityGlobalNPC.signus].Infernum().ExtraAI[9];
				if (InfernumMode.BlackFade > 0f)
					fadeToBlack = InfernumMode.BlackFade;

				if (fadeToBlack > 0f)
				{
					Color color = Color.Black * fadeToBlack;
					Main.spriteBatch.Draw(Main.magicPixel, new Rectangle(-2, -2, Main.screenWidth + 4, Main.screenHeight + 4), new Rectangle(0, 0, 1, 1), color);
				}

				Main.spriteBatch.End();
				Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, Main.DefaultSamplerState, DepthStencilState.None, Main.instance.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
				for (int i = 0; i < DrawCacheProjsOverSignusBlackening.Count; i++)
				{
					try
					{
						Main.instance.DrawProj(DrawCacheProjsOverSignusBlackening[i]);
					}
					catch (Exception e)
					{
						TimeLogger.DrawException(e);
						Main.projectile[DrawCacheProjsOverSignusBlackening[i]].active = false;
					}
				}
				DrawCacheProjsOverSignusBlackening.Clear();

				Main.spriteBatch.SetBlendState(BlendState.Additive);
				for (int i = 0; i < DrawCacheAdditiveLighting.Count; i++)
				{
					try
					{
						Main.instance.DrawProj(DrawCacheAdditiveLighting[i]);
					}
					catch (Exception e)
					{
						TimeLogger.DrawException(e);
						Main.projectile[DrawCacheAdditiveLighting[i]].active = false;
					}
				}
				Main.spriteBatch.End();
				Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, Main.DefaultSamplerState, DepthStencilState.None, Main.instance.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
				DrawCacheAdditiveLighting.Clear();
			});
		}

		public void Load()
		{
			DrawCacheProjsOverSignusBlackening = new List<int>();
			DrawCacheAdditiveLighting = new List<int>();
			IL.Terraria.Main.DoDraw += DrawBlackout;
		}

		public void Unload()
		{
			DrawCacheProjsOverSignusBlackening = DrawCacheAdditiveLighting = null;
			IL.Terraria.Main.DoDraw -= DrawBlackout;
		}
	}

	public class DisableMoonLordBuildingHook : IHookEdit
	{
		internal static void DisableMoonLordBuilding(ILContext instructionContext)
		{
			var c = new ILCursor(instructionContext);

			if (!c.TryGotoNext(i => i.MatchLdcI4(ItemID.SuperAbsorbantSponge)))
				return;

			c.Index++;
			c.EmitDelegate<Action>(() =>
			{
				if (NPC.AnyNPCs(NPCID.MoonLordCore) && PoDWorld.InfernumMode)
					Main.LocalPlayer.noBuilding = true;
			});
		}

		public void Load() => IL.Terraria.Player.ItemCheck += DisableMoonLordBuilding;

		public void Unload() => IL.Terraria.Player.ItemCheck -= DisableMoonLordBuilding;
	}

	public class ChangeHowMinibossesSpawnInDD2EventHook : IHookEdit
	{
		internal static int GiveDD2MinibossesPointPriority(On.Terraria.GameContent.Events.DD2Event.orig_GetMonsterPointsWorth orig, int slainMonsterID)
		{
			if (OldOnesArmyMinibossChanges.GetMinibossToSummon(out int minibossID) && minibossID != NPCID.DD2Betsy && PoDWorld.InfernumMode)
				return slainMonsterID == minibossID ? 99999 : 0;

			return orig(slainMonsterID);
		}

		public void Load() => On.Terraria.GameContent.Events.DD2Event.GetMonsterPointsWorth += GiveDD2MinibossesPointPriority;

		public void Unload() => On.Terraria.GameContent.Events.DD2Event.GetMonsterPointsWorth -= GiveDD2MinibossesPointPriority;
	}

	public class DrawVoidBackgroundDuringMLFightHook : IHookEdit
	{
		public static void PrepareShaderForBG(On.Terraria.Main.orig_DrawSurfaceBG orig, Main self)
		{
			int moonLordIndex = NPC.FindFirstNPC(NPCID.MoonLordCore);
			bool useShader = InfernumMode.CanUseCustomAIs && moonLordIndex >= 0 && moonLordIndex < Main.maxNPCs && !Main.gameMenu;
			orig(self);

			if (useShader)
			{
				Main.spriteBatch.End();
				Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, Main.instance.Rasterizer, null, Matrix.Identity);

				Rectangle arena = Main.npc[moonLordIndex].Infernum().arenaRectangle;
				Vector2 topLeft = (arena.TopLeft() + Vector2.One * 8f - Main.screenPosition) / new Vector2(Main.screenWidth, Main.screenHeight) / Main.GameViewMatrix.Zoom;
				Vector2 bottomRight = (arena.BottomRight() + Vector2.One * 16f - Main.screenPosition) / new Vector2(Main.screenWidth, Main.screenHeight) / Main.GameViewMatrix.Zoom;
				Matrix zoomMatrix = Main.GameViewMatrix.TransformationMatrix;

				Vector2 scale = new Vector2(Main.screenWidth, Main.screenHeight) / Main.magicPixel.Size() * Main.GameViewMatrix.Zoom;
				GameShaders.Misc["Infernum:MoonLordBGDistortion"].Shader.Parameters["uTopLeftFreeArea"].SetValue(topLeft);
				GameShaders.Misc["Infernum:MoonLordBGDistortion"].Shader.Parameters["uBottomRightFreeArea"].SetValue(bottomRight);
				GameShaders.Misc["Infernum:MoonLordBGDistortion"].Shader.Parameters["uZoomMatrix"].SetValue(zoomMatrix);
				GameShaders.Misc["Infernum:MoonLordBGDistortion"].UseColor(Color.Gray);
				GameShaders.Misc["Infernum:MoonLordBGDistortion"].UseSecondaryColor(Color.Turquoise);
				GameShaders.Misc["Infernum:MoonLordBGDistortion"].SetShaderTexture(ModContent.GetTexture("InfernumMode/ExtraTextures/CultistRayMap"));
				GameShaders.Misc["Infernum:MoonLordBGDistortion"].Apply();
				Vector2 hell = new Vector2(Main.screenWidth * (Main.GameViewMatrix.Zoom.X - 1f), Main.screenHeight * (Main.GameViewMatrix.Zoom.Y - 1f));
				Main.spriteBatch.Draw(Main.magicPixel, hell * -0.5f, null, Color.White, 0f, Vector2.Zero, scale, 0, 0f);

				Main.spriteBatch.End();
				Main.spriteBatch.Begin();
			}
		}

		public void Load() => On.Terraria.Main.DrawSurfaceBG += PrepareShaderForBG;

		public void Unload() => On.Terraria.Main.DrawSurfaceBG -= PrepareShaderForBG;
	}
}