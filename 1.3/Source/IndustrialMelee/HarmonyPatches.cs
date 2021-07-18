﻿using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace IndustrialMelee
{
    [StaticConstructorOnStartup]
    internal static class HarmonyInit
    {
        static HarmonyInit()
        {
            foreach (var human in DefDatabase<ThingDef>.AllDefs.Where(x => x.race != null && x.race.Humanlike))
            {
                human.comps.Add(new CompProperties_AttackCooldown());
            }
            new Harmony("IndustrialMelee.Mod").PatchAll();
        }

        public static Dictionary<Apparel, CompIndustrialArmor> cachedComps = new Dictionary<Apparel, CompIndustrialArmor>();
        public static bool TryGetCompIndustrialArmor(this Apparel apparel, out CompIndustrialArmor comp)
        {
            if (!cachedComps.TryGetValue(apparel, out comp))
            {
                cachedComps[apparel] = comp = apparel.TryGetComp<CompIndustrialArmor>();
            }
            return comp != null;
        }
    }

    [HarmonyPatch(typeof(HediffSet), nameof(HediffSet.BleedRateTotal), MethodType.Getter)]
    public static class BleedRateTotal_Patch
    {
        public static void Postfix(ref float __result, HediffSet __instance)
        {
            if (__result > 0)
            {
                if (__instance?.GetFirstHediffOfDef(IM_DefOf.IM_HighBleedrate) != null)
                {
                    __result *= 1.5f;
                }
                if (__instance?.GetFirstHediffOfDef(IM_DefOf.IM_10MoreBleedrate) != null)
                {
                    __result *= 1.1f;
                }
            }
        }
    }

    [HarmonyPatch(typeof(StatExtension), nameof(StatExtension.GetStatValue))]
    public static class GetStatValue_Patch
    {
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(Thing thing, StatDef stat, bool applyPostProcess, ref float __result)
        {
            if (stat == StatDefOf.MoveSpeed && thing is Pawn pawn)
            {
                if (pawn.CurJobDef == IM_DefOf.IM_Charge)
                {
                    __result = Mathf.Max(20, __result * 4.44f);
                }
                if (pawn.apparel?.WornApparel != null)
                {
                    foreach (var apparel in pawn.apparel.WornApparel)
                    {
                        if (apparel.TryGetCompIndustrialArmor(out var comp) && comp.RemainingCharges == 0)
                        {
                            __result = 0;
                            return;
                        }
                    }
                }
            }
        }
    }

    [HarmonyPatch(typeof(CompReloadable), nameof(CompReloadable.NeedsReload))]
    public static class NeedsReload_Patch
    {
        private static void Postfix(CompReloadable __instance, bool allowForcedReload, ref bool __result)
        {
            if (__instance is CompIndustrialArmor)
            {
                __result = __instance.RemainingCharges / __instance.MaxCharges < 0.3f;
            }
        }
    }

    [HarmonyPatch(typeof(Verb_MeleeAttackDamage), "ApplyMeleeDamageToTarget")]
    public static class Patch_ApplyMeleeDamageToTarget
    {
        public static void Postfix(Verb_MeleeAttackDamage __instance, LocalTargetInfo target)
        {
            if (target.Thing is Pawn victim)
            {
                if (__instance.EquipmentSource?.def == IM_DefOf.IM_MeleeWeapon_ChainSword && Rand.Chance(0.02f))
                {
                    var head = victim.health.hediffSet.GetNotMissingParts().FirstOrDefault((BodyPartRecord x) => x.def == BodyPartDefOf.Head);
                    if (head != null)
                    {
                        int num = Mathf.Max(GenMath.RoundRandom(victim.BodySize * 8f), 1);
                        for (int i = 0; i < num; i++)
                        {
                            victim.health.DropBloodFilth();
                        }

                        int num2 = Mathf.Clamp((int)victim.health.hediffSet.GetPartHealth(head) - 1, 1, 20);
                        DamageInfo damageInfo = new DamageInfo(DamageDefOf.Cut, num2, 999f, -1f, __instance.caster, head);
                        victim.TakeDamage(damageInfo);
                        if (!victim.Dead)
                        {
                            victim.Kill(damageInfo);
                        }
                    }
                }
                else if (__instance.EquipmentSource?.def == IM_DefOf.IM_MeleeWeapon_HeaterSaw && Rand.Chance(0.05f))
                {
                    List<BodyPartRecord> list = (from x in victim.RaceProps.body.AllParts where !victim.health.hediffSet.PartIsMissing(x) && x.depth == BodyPartDepth.Outside && x.coverage > 0.1f
                                                 select x).ToList<BodyPartRecord>();
                    if (list.Count == 0)
                    {
                        return;
                    }
                    BodyPartRecord bodyPartRecord;
                    if (GenCollection.TryRandomElement<BodyPartRecord>(list, out bodyPartRecord))
                    {
                        var missingBodyPart = HediffMaker.MakeHediff(HediffDefOf.MissingBodyPart, victim, bodyPartRecord);
                        victim.health.AddHediff(missingBodyPart);
                    }
                }
                else if (__instance.caster is Pawn instigator)
                {
                    var comp = instigator.TryGetComp<CompAttackCooldown>();
                    if (comp != null)
                    {
                        if (!comp.HasCooldownFor(AttackType.DrillSpearGore) && __instance.EquipmentSource?.def == IM_DefOf.IM_MeleeWeapon_DrillSpear && Rand.Chance(0.05f))
                        {
                            comp.SetCooldown(AttackType.DrillSpearGore, 7200);
                            List<BodyPartRecord> list = (from x in victim.RaceProps.body.AllParts
                                                         where !victim.health.hediffSet.PartIsMissing(x) && x.depth == BodyPartDepth.Inside
                                                         select x).ToList<BodyPartRecord>();
                            if (list.Count == 0)
                            {
                                return;
                            }
                            BodyPartRecord bodyPartRecord;
                            if (GenCollection.TryRandomElement<BodyPartRecord>(list, out bodyPartRecord))
                            {
                                var missingBodyPart = HediffMaker.MakeHediff(HediffDefOf.MissingBodyPart, victim, bodyPartRecord);
                                victim.health.AddHediff(missingBodyPart);
                            }
                        }
                        else if (!comp.HasCooldownFor(AttackType.RocketLanceGore) && __instance.EquipmentSource?.def == IM_DefOf.IM_MeleeWeapon_RocketLance && Rand.Chance(0.05f))
                        {
                            comp.SetCooldown(AttackType.RocketLanceGore, 7200);
                            List<BodyPartRecord> list = (from x in victim.RaceProps.body.AllParts
                                                         where !victim.health.hediffSet.PartIsMissing(x) && x.depth == BodyPartDepth.Inside
                                                         select x).ToList<BodyPartRecord>();
                            if (list.Count == 0)
                            {
                                return;
                            }
                            BodyPartRecord bodyPartRecord;
                            if (GenCollection.TryRandomElement<BodyPartRecord>(list, out bodyPartRecord))
                            {
                                var missingBodyPart = HediffMaker.MakeHediff(HediffDefOf.MissingBodyPart, victim, bodyPartRecord);
                                victim.health.AddHediff(missingBodyPart);
                            }
                        }
                    }

                }
            }
        }
    }

    [HarmonyPatch(typeof(PawnRenderer), "DrawBodyApparel")]
    public static class PawnRenderer_RenderPawnInternal_DrawExosuit_Transpiler
    {
        public static bool Prefix(Pawn ___pawn, Vector3 shellLoc, Vector3 utilityLoc, Mesh bodyMesh, float angle, Rot4 bodyFacing, PawnRenderFlags flags)
        {
            if (___pawn.apparel != null && ___pawn.apparel.WornApparelCount > 0)
            {
                foreach (var apparel in ___pawn.apparel.WornApparel)
                {
                    if (apparel.TryGetCompIndustrialArmor(out var comp))
                    {
                        var mat = comp.GetMaterial(bodyFacing);
                        Vector3 drawAt = shellLoc;
                        Matrix4x4 matrix = default(Matrix4x4);
                        Quaternion quaternion = Quaternion.AngleAxis(angle, Vector3.up);
                        matrix.SetTRS(drawAt, quaternion, new Vector3(1.3f, 0, 1.3f));
                        GenDraw.DrawMeshNowOrLater(bodyMesh, matrix, mat, true);
                        return false;
                    }
                }
            }
            return true;
        }
    }
    [HarmonyPatch(typeof(PawnRenderer), "DrawHeadHair")]
    public static class DrawHeadHair_Patch
    {
        public static bool Prefix(Pawn ___pawn, Vector3 rootLoc, Vector3 headOffset, float angle, Rot4 bodyFacing, Rot4 headFacing, RotDrawMode bodyDrawType, PawnRenderFlags flags)
        {
            Pawn pawn = ___pawn;
            if (pawn.apparel.AnyApparel)
            {
                if (pawn.apparel.WornApparel.Any(x => x.TryGetCompIndustrialArmor(out var comp)))
                {
                    return false;
                }
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(PawnGraphicSet), "HairMatAt")]
    public static class PawnGraphicSet_HairMatAt_Patch
    {
        public static void Postfix(PawnGraphicSet __instance, ref Material __result, Rot4 facing, bool portrait = false, bool cached = false)
        {
            Pawn pawn = __instance.pawn;
            if (pawn.apparel.AnyApparel && !portrait)
            {
                if (pawn.apparel.WornApparel.Any(x => x.TryGetCompIndustrialArmor(out var comp)))
                {
                    __result = BaseContent.ClearMat;
                }
            }
        }
    }

    [HarmonyPatch(typeof(PawnGraphicSet), "HeadMatAt")]
    public static class PawnGraphicSet_HeadMatAt_Patch
    {
        public static void Postfix(PawnGraphicSet __instance, ref Material __result, Rot4 facing, RotDrawMode bodyCondition = RotDrawMode.Fresh, bool stump = false, bool portrait = false, bool allowOverride = true)
        {
            Pawn pawn = __instance.pawn;
            if (pawn.apparel.AnyApparel && !portrait)
            {
                if (pawn.apparel.WornApparel.Any(x => x.TryGetCompIndustrialArmor(out var comp)))
                {
                    __result = BaseContent.ClearMat;
                }
            }
        }
    }

    [HarmonyPatch(typeof(PawnGraphicSet), "MatsBodyBaseAt")]
    public static class PawnGraphicSet_MatsBodyBaseAt_Test_Patch
    {
        [HarmonyPostfix, HarmonyPriority(Priority.Last)]
        public static void Postfix(PawnGraphicSet __instance, ref List<Material> __result)
        {
            Pawn pawn = __instance.pawn;
            if (!pawn.RaceProps.Humanlike)
            {
                return;
            }
            if (pawn.apparel.AnyApparel)
            {
                if (pawn.apparel.WornApparel.Any(x => x.TryGetCompIndustrialArmor(out var comp)))
                {
                    for (int i = 0; i < __result.Count; i++)
                    {
                        __result[i] = BaseContent.ClearMat;
                    }
                }
            }
        }
    }

    [HarmonyPatch(typeof(Pawn), "GetGizmos")]
	public class Pawn_GetGizmos_Patch
	{
        public static Dictionary<Pawn, CompAttackCooldown> cachedComps = new Dictionary<Pawn, CompAttackCooldown>();
        public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, Pawn __instance)
        {
            foreach (var g in __result)
            {
                yield return g;
            }
            if (__instance.MentalState is null && __instance.Faction == Faction.OfPlayer)
            {
                if (!cachedComps.TryGetValue(__instance, out var comp))
                {
                    comp = __instance.TryGetComp<CompAttackCooldown>();
                    cachedComps[__instance] = comp;
                }

                if (comp != null)
                {
                    if (__instance.equipment?.Primary?.def == IM_DefOf.IM_MeleeWeapon_RocketLance)
                    {
                        var charge = new Command_Action
                        {
                            // defaultLabel = "IM.Charge".Translate(),
                            // defaultDesc = "IM.ChargeDesc".Translate(),
                            icon = ContentFinder<Texture2D>.Get("UI/Buttons/Charge"),
                            action = delegate
                            {
                                Find.Targeter.BeginTargeting(ForPawn(__instance), delegate (LocalTargetInfo x)
                                {
                                    __instance.jobs.TryTakeOrderedJob(JobMaker.MakeJob(IM_DefOf.IM_Charge, x));
                                });
                            },
                        };
                        if (comp.HasCooldownFor(AttackType.Charge))
                        {
                            charge.Disable("IM.ChargeCooldown".Translate());
                        }
                        yield return charge;
                    }
                    else if (__instance.equipment?.Primary?.def == IM_DefOf.IM_ImpactBow)
                    {
                        var toggle = new Command_Toggle
                        {
                            defaultLabel = "IM.UseExplosiveArrows".Translate(),
                            icon = ContentFinder<Texture2D>.Get("UI/Buttons/EnableExplosiveArrows"),
                            isActive = () => comp.AttackIsEnabled(AttackType.ExplosiveArrows),
                            toggleAction = delegate ()
                            {
                                comp.EnableAttack(AttackType.ExplosiveArrows, !comp.AttackIsEnabled(AttackType.ExplosiveArrows));
                            }
                        };
                        yield return toggle;
                    }
                }
            }
        }
        public static TargetingParameters ForPawn(Pawn user)
        {
            TargetingParameters targetingParameters = new TargetingParameters();
            targetingParameters.canTargetPawns = true;
            targetingParameters.canTargetAnimals = true;
            targetingParameters.canTargetMechs = true;
            targetingParameters.validator = (TargetInfo x) => x.Thing is Pawn victim && !victim.Downed && !victim.Dead && user.Position.DistanceTo(x.Cell) <= 20;
            return targetingParameters;
        }
    }

	[DefOf]
    public static class IM_DefOf
    {
        public static HediffDef IM_HighBleedrate;
        public static HediffDef IM_10MoreBleedrate;
        public static ThingDef IM_MeleeWeapon_RocketLance;
        public static ThingDef IM_MeleeWeapon_ImpactHammer;
        public static ThingDef IM_ImpactBow;
        public static ThingDef IM_ArrowExplosive;
        public static JobDef IM_Charge;
        public static ThingDef IM_MeleeWeapon_ChainSword;
        public static ThingDef IM_MeleeWeapon_HeaterSaw;
        public static ThingDef IM_MeleeWeapon_DrillSpear;
    }

    public enum AttackType
    {
        HammerHead,
        Charge,
        DrillSpearGore,
        RocketLanceGore,
        ExplosiveArrows
    }
    public class CompProperties_AttackCooldown : CompProperties
    {
        public CompProperties_AttackCooldown()
        {
            this.compClass = typeof(CompAttackCooldown);
        }
    }

    public class CompAttackCooldown : ThingComp
    {
        private Dictionary<AttackType, int> cooldownTicks;

        private List<AttackType> enabledAttacks;
        public bool AttackIsEnabled(AttackType attackType)
        {
            if (enabledAttacks is null)
            {
                enabledAttacks = new List<AttackType>();
            }
            if (enabledAttacks.Contains(attackType))
            {
                return true;
            }
            return false;
        }
        public void EnableAttack(AttackType attackType, bool value = true)
        {
            if (enabledAttacks is null)
            {
                enabledAttacks = new List<AttackType>();
            }
            if (value)
            {
                enabledAttacks.Add(attackType);
            }
            else
            {
                enabledAttacks.Remove(attackType);
            }
        }
        public void SetCooldown(AttackType key, int toTickCooldown)
        {
            if (cooldownTicks is null)
            {
                cooldownTicks = new Dictionary<AttackType, int>();
            }
            cooldownTicks[key] = toTickCooldown;
        }
        public bool HasCooldownFor(AttackType key)
        {
            if (cooldownTicks is null)
            {
                cooldownTicks = new Dictionary<AttackType, int>();
            }
            if (cooldownTicks.TryGetValue(key, out var tick) && tick > Find.TickManager.TicksGame)
            {
                return true;
            }
            return false;
        }
        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Collections.Look(ref cooldownTicks, "cooldownTicks", LookMode.Value, LookMode.Value, ref attackTypeKeys, ref intValues);
            Scribe_Collections.Look(ref enabledAttacks, "enabledAttacks");
        }

        private List<AttackType> attackTypeKeys;
        private List<int> intValues;
    }
}
