﻿using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using Verse.AI;

namespace IndustrialMelee
{
    public class CompIndustrialArmor : CompReloadable
    {
        public Material GetMaterial(Rot4 bodyFacing)
        {
            var apparel = this.parent as Apparel;
            ApparelGraphicRecordGetter.TryGetGraphicApparel(apparel, apparel.Wearer.story.bodyType, out var rec);
            return rec.graphic.MatAt(bodyFacing);
        }
        public override void CompTick()
        {
            base.CompTick();
            if (this.parent.IsHashIntervalTick(100))
            {
                Log.Message(this.RemainingCharges + " - " + this.MaxCharges);
                this.UsedOnce();
            }
        }
    }
}
