using System;

namespace FactionColonies
{
    /*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*
     * Factory seam for military template types. Submods can swap the
     * delegates at mod init to have the base mod produce subclass instances
     * from every internal instantiation site. Defaults to plain base types,
     * so behavior is unchanged when no submod is active.
     *-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*/
    public static class MilTemplateFactory
    {
        public static Func<bool, MilUnitFC> UnitCreator = isBlank => new MilUnitFC(isBlank);
        public static Func<bool, MilSquadFC> SquadCreator = isBlank => new MilSquadFC(isBlank);
        public static Func<MercenarySquadFC> MercSquadCreator = () => new MercenarySquadFC();

        public static MilUnitFC CreateUnit(bool isBlank) => UnitCreator(isBlank);
        public static MilSquadFC CreateSquad(bool isBlank) => SquadCreator(isBlank);
        public static MercenarySquadFC CreateMercSquad() => MercSquadCreator();
    }
}
