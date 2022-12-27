using InfernumMode.Systems;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Terraria;

namespace InfernumMode.OverridingSystem
{
    public abstract class NPCBehaviorOverride
    {
        internal static Dictionary<int, NPCBehaviorOverride> BehaviorOverrides = new();

        internal static void LoadAll()
        {
            BehaviorOverrides = new();
            
            foreach (Type type in Utilities.GetEveryMethodDerivedFrom(typeof(NPCBehaviorOverride), typeof(InfernumMode).Assembly))
            {
                NPCBehaviorOverride instance = (NPCBehaviorOverride)Activator.CreateInstance(type);

                // Cache the PreAI method if it exists.
                MethodInfo preAIMethod = type.GetMethod("PreAI", Utilities.UniversalBindingFlags);
                if (preAIMethod is not null)
                    OverridingListManager.InfernumNPCPreAIOverrideList[instance.NPCOverrideType] = new OverridingListManager.NPCPreAIDelegate(n => (bool)preAIMethod.Invoke(instance, new object[] { n }));

                // Cache the SetDefaults method if it exists.
                MethodInfo setDefaultsMethod = type.GetMethod("SetDefaults", Utilities.UniversalBindingFlags);
                if (setDefaultsMethod is not null)
                    OverridingListManager.InfernumSetDefaultsOverrideList[instance.NPCOverrideType] = setDefaultsMethod.ConvertToDelegate(instance);

                // Cache the PreDraw method if it exists.
                MethodInfo preDrawMethod = type.GetMethod("PreDraw", Utilities.UniversalBindingFlags);
                if (preDrawMethod is not null)
                    OverridingListManager.InfernumPreDrawOverrideList[instance.NPCOverrideType] = new OverridingListManager.NPCPreDrawDelegate((n, s, c) => (bool)preDrawMethod.Invoke(instance, new object[] { n, s, c }));

                // Cache the FindFrame method if it exists.
                MethodInfo findFrameMethod = type.GetMethod("FindFrame", Utilities.UniversalBindingFlags);
                if (findFrameMethod is not null)
                    OverridingListManager.InfernumFrameOverrideList[instance.NPCOverrideType] = new OverridingListManager.NPCFindFrameDelegate((n, h) => findFrameMethod.Invoke(instance, new object[] { n, h }));

                // Cache the CheckDead method if it exists.
                MethodInfo checkDeadMethod = type.GetMethod("CheckDead", Utilities.UniversalBindingFlags);
                if (checkDeadMethod is not null)
                    OverridingListManager.InfernumCheckDeadOverrideList[instance.NPCOverrideType] = new OverridingListManager.NPCCheckDeadDelegate(n => (bool)checkDeadMethod.Invoke(instance, new object[] { n }));

                BehaviorOverrides[instance.NPCOverrideType] = instance;
            }
        }

        internal static void LoadPhaseIndicaors()
        {
            foreach (int npcID in BehaviorOverrides.Keys)
            {
                NPCBehaviorOverride instance = BehaviorOverrides[npcID];
                float[] phaseThresholds = instance.PhaseLifeRatioThresholds;
                if (!Main.dedServ && InfernumMode.PhaseIndicator != null && phaseThresholds.Length >= 1)
                {
                    foreach (float lifeRatio in phaseThresholds)
                        InfernumMode.PhaseIndicator.Call(0, npcID, (NPC npc, float difficulty) => lifeRatio);
                }

                HatGirlTipsManager.TipsRegistry[instance.NPCOverrideType] = instance.GetTips().ToList();
            }
        }

        public virtual int? NPCIDToDeferToForTips => null;

        public virtual float[] PhaseLifeRatioThresholds => Array.Empty<float>();

        public virtual IEnumerable<Func<NPC, string>> GetTips() => Array.Empty<Func<NPC, string>>();

        public abstract int NPCOverrideType { get; }

        public virtual void SetDefaults(NPC npc) { }

        public virtual bool PreAI(NPC npc) => true;

        public virtual bool PreDraw(NPC npc, SpriteBatch spriteBatch, Color lightColor) => true;

        public virtual void FindFrame(NPC npc, int frameHeight) { }

        public virtual bool CheckDead(NPC npc) => true;
    }
}
