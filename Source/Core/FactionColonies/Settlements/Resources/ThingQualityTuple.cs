using FactionColonies.util;
using RimWorld;
using System;
using Verse;

namespace FactionColonies
{
    public class ThingQualityTuple : IExposable, IEquatable<ThingQualityTuple>
    {
        public ThingDef thingDef;
        public QualityCategory quality;
        public ThingDef stuffDef;
        public ThingQualityTuple()
        {
        }
        public string ListRejectionMessage()
        {
            if (CraftUtil.ThingHasQuality(thingDef))
            {
                if (CraftUtil.ThingIsStuffable(thingDef))
                {
                    return "FCTitheListRejectionStuffQuality".Translate(thingDef.LabelCap, TextUtil.GetQualityLabelCap(quality), stuffDef.LabelCap);
                }
                else
                {
                    return "FCTitheListRejectionQuality".Translate(thingDef.LabelCap, TextUtil.GetQualityLabelCap(quality));
                }
            }
            else
            {
                if (CraftUtil.ThingIsStuffable(thingDef))
                {
                    return "FCTitheListRejectionStuff".Translate(thingDef.LabelCap, stuffDef.LabelCap);
                }
                else
                {
                    return "FCTitheListRejection".Translate(thingDef.LabelCap);
                }
            }
        }
        /* IExposable functions */
        public void ExposeData()
        {
            Scribe_Defs.Look(ref thingDef, "thingDef");
            Scribe_Values.Look(ref quality, "quality");
            Scribe_Defs.Look(ref stuffDef, "stuffDef");
        }

        /* IExposable functions end */

        /* IEquatable functions */
        public bool Equals(ThingQualityTuple other)
        {
            if (other is null)
            {
                return false;
            }
            return other.thingDef == this.thingDef && other.quality == this.quality && other.stuffDef == this.stuffDef;
        }
        public override bool Equals(object obj)
        {
            if (obj is ThingQualityTuple)
            {
                return Equals(obj as ThingQualityTuple);
            }
            return false;
        }
        public override int GetHashCode() => HashCode.Combine(thingDef, quality, stuffDef);
        public static bool operator ==(ThingQualityTuple t1, ThingQualityTuple t2)
        {
            if (t1 is null)
            {
                return t2 is null;
            }
            return t1.Equals(t2);
        }
        public static bool operator !=(ThingQualityTuple t1, ThingQualityTuple t2)
        {
            if (t1 is null)
            {
                return t2 is null;
            }
            return !t1.Equals(t2);
        }
        /* IEquatable functions end */
    }
}