﻿using InfernumMode.Assets.Effects;
using InfernumMode.Assets.ExtraTextures;
using InfernumMode.Common.Graphics.AttemptRecording;
using InfernumMode.Core.GlobalInstances.Players;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace InfernumMode.Content.Credits
{
    public class CreditManager : ModSystem
    {
        private enum CreditState
        {
            LoadingTextures,
            Playing,
            FinalizingDisposing
        }

        public static bool CreditsPlaying
        {
            get;
            private set;
        }

        private static int CreditsTimer = 0;

        private static int ActiveGifIndex = 0;

        private static CreditAnimationObject[] CreditGIFs;

        private static CreditState CurrentState = CreditState.LoadingTextures;

        private static readonly string[] Names = { Programmers, Musicians, Artists, Testers1, Testers2, Testers3, Testers4 };

        private static readonly string[] Headers = { "Programmers", "Musicians", "Artists", "Testers", "Testers", "Testers", "Testers"};

        private static readonly Color[] HeaderColors = 
        { 
            new(212, 56, 34), 
            new(143, 11, 139), 
            new(80, 105, 185), 
            new(0, 148, 75), 
            new(0, 148, 75), 
            new(0, 148, 75), 
            new(0, 148, 75)
        };

        private static readonly ScreenCapturer.RecordingBoss[] Bosses = 
        { 
            ScreenCapturer.RecordingBoss.KingSlime, 
            ScreenCapturer.RecordingBoss.WoF, 
            ScreenCapturer.RecordingBoss.Calamitas, 
            ScreenCapturer.RecordingBoss.Vassal,
            ScreenCapturer.RecordingBoss.Provi,
            ScreenCapturer.RecordingBoss.Draedon,
            ScreenCapturer.RecordingBoss.SCal
        };

        public const int TotalGIFs = 7;

        public const string Artists = "Arix\nFreeman\nIbanPlay\nPengolin\nReika\nSpicySpaceSnake";

        public const string Musicians = "Pinpin";

        public const string Programmers = "Dominic\nNycro\nToasty";

        public const string Testers1 = "Blast\nBronze\nCata\nEin\nGamerXD";

        public const string Testers2 = "Gonk\nIan\nJareto\nJoey\nLGL";

        public const string Testers3 = "Nutella\nMatthionine\nMyra\nPiky\nPurpleMattik";

        public const string Testers4 = "Smh\nShade\nShadine\nTeiull";

        public override void Load() => Main.OnPostDraw += DrawCredits;

        public override void Unload() => Main.OnPostDraw -= DrawCredits;

        public override void PostUpdateDusts() => UpdateCredits();

        internal static void StartRecordingFootageForCredits(ScreenCapturer.RecordingBoss boss)
        {
            if (Main.netMode == NetmodeID.Server || ScreenCapturer.RecordCountdown > 0)
                return;

            // Only start a recording if one does not exist for this player and boss, to avoid overriding them.
            if (File.Exists($"{ScreenCapturer.FolderPath}/{ScreenCapturer.GetStringFromBoss(boss)}{ScreenCapturer.FileExtension}"))
                return;

            ScreenCapturer.CurrentBoss = boss;
            ScreenCapturer.RecordCountdown = ScreenCapturer.BaseRecordCountdownLength;
        }

        internal static void BeginCredits()
        {
            // Return if the credits are already playing, or have completed for this player.
            if (CreditsPlaying) //|| Main.LocalPlayer.GetModPlayer<CreditsPlayer>().CreditsHavePlayed)
                return;

            // Else, mark them as playing.
            CurrentState = CreditState.LoadingTextures;
            CreditsTimer = 0;
            ActiveGifIndex = 0;
            CreditsPlaying = true;
        }

        private static void UpdateCredits()
        {
            if (!CreditsPlaying)
                return;

            // TODO -- Make this variable based on the amount of frames in the gif.
            float gifTime = ScreenCapturer.BaseRecordCountdownLength / ScreenCapturer.RecordingFrameSkip;
            float disposeTime = 60f;
            float fadeInTime = 60f;
            float fadeOutTime = gifTime - fadeInTime;

            switch (CurrentState)
            {
                case CreditState.LoadingTextures:
                    // The textures must be loaded for each gif, however doing them all at once causes major lag. So, it is split up throughout the credits.
                    if (CreditsTimer == 0)
                        Main.RunOnMainThread(() => SetupObjects(0));

                    if (CreditsTimer >= gifTime)
                    {
                        CurrentState = CreditState.Playing;
                        CreditsTimer = 0;
                    }
                    break;

                case CreditState.Playing:
                    if (CreditsTimer <= gifTime)
                    {
                        if (CreditGIFs.IndexInRange(ActiveGifIndex))
                            CreditGIFs[ActiveGifIndex]?.Update();

                        // Dispose of the textures partway into the next gif, to ensure that it does not try to do it while they are in use.
                        if (CreditsTimer == disposeTime)
                        {
                            Main.RunOnMainThread(() => SetupObjects(ActiveGifIndex + 1));
                            if (CreditGIFs.IndexInRange(ActiveGifIndex - 1))
                            {
                                // Dispose of all the textures.
                                CreditGIFs[ActiveGifIndex - 1]?.DisposeTextures();
                                CreditGIFs[ActiveGifIndex - 1] = null;
                            }
                        }

                        if (CreditsTimer >= gifTime)
                        {
                            if (ActiveGifIndex < TotalGIFs)
                            {
                                ActiveGifIndex++;
                                CreditsTimer = 0;
                                return;
                            }
                            else
                            {
                                CreditsTimer = 0;
                                CurrentState = CreditState.FinalizingDisposing;
                                CreditsPlaying = true;
                                return;
                            }
                        }
                    }
                    break;
                case CreditState.FinalizingDisposing:
                    if (CreditsTimer >= disposeTime)
                    {
                        // Dispose of all the final textures.
                        if (CreditGIFs.IndexInRange(ActiveGifIndex))
                        {
                            CreditGIFs[ActiveGifIndex]?.DisposeTextures();
                            CreditGIFs[ActiveGifIndex] = null;
                        }
                        // Mark the credits as completed.
                        Main.LocalPlayer.GetModPlayer<CreditsPlayer>().CreditsHavePlayed = true;
                        CreditsPlaying = false;
                    }
                    break;
            }

            CreditsTimer++;
        }

        private static void DrawCredits(GameTime gameTime)
        {
            // Only draw if the credits are playing.
            if (!CreditsPlaying || CurrentState != CreditState.Playing)
                return;

            // This is already ended.
            Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.Transform);

            // TODO -- Make this variable based on the amount of frames in the gif.
            float gifTime = ScreenCapturer.BaseRecordCountdownLength / ScreenCapturer.RecordingFrameSkip;
            float fadeInTime = 60f;
            float fadeOutTime = gifTime - fadeInTime;

            if (CreditsTimer <= gifTime)
            {
                float opacity = 1f;

                if (CreditsTimer <= fadeInTime)
                    opacity = Utils.GetLerpValue(0f, fadeInTime, CreditsTimer, true);
                else if (CreditsTimer >= fadeOutTime)
                    opacity = 1f - Utils.GetLerpValue(fadeOutTime, gifTime, CreditsTimer, true);

                if (CreditGIFs.IndexInRange(ActiveGifIndex))
                {
                    CreditGIFs[ActiveGifIndex]?.DrawGIF(CreditsTimer / ScreenCapturer.RecordingFrameSkip, opacity);
                    Main.pixelShader.CurrentTechnique.Passes[0].Apply();

                    CreditGIFs[ActiveGifIndex]?.DrawNames(opacity);
                }
            }

            Main.spriteBatch.End();
        }

        private static void SetupObjects(int index)
        {
            if (index is 0)
                CreditGIFs = new CreditAnimationObject[TotalGIFs];

            // Leave if the index is out of the range.
            if (!CreditGIFs.IndexInRange(index))
                return;
         
            Texture2D[] textures = ScreenCapturer.LoadGifAsTexture2Ds(Bosses[index], out bool baseCreditsUsed);
            CreditGIFs[index] = new CreditAnimationObject(new(Main.screenWidth * 0.5f, Main.screenHeight * 0.3f), -Vector2.UnitY * 0.05f, textures, Headers[index], Names[index], HeaderColors[index], baseCreditsUsed);            
        }
    }
}
