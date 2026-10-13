using System.Collections.Generic;

namespace AABattle
{
  public static partial class ModuleCatalog
  {
    static void RegisterRoleEffects(List<ModuleDefinition> modules)
    {
      CoreAdd(modules, "effect.bond.hot", "뜨거운 유대", BondModules.KindHot, "유대", BondModules.HotSentence);
      CoreAdd(modules, "effect.role.appoint", "역할 임명", RoleModules.EffectKind, "역할", "자신을 『{역할}』로 임명할 수 있다.", new ModuleSlot("역할", ModuleSlotKind.Text, "에이스", RoleModules.Choices));
      foreach (string aptitude in new[] { "지시", "육성", "통솔", "능력" })
        foreach (string direction in new[] { "올린다", "내린다" })
          CoreAdd(modules, "effect.aptitude." + aptitude + "." + direction, "트레이너 자질 " + direction, RoleModules.AptitudeKind, "트레이너", "트레이너의 「" + aptitude + "」을 {랭크}랭크 " + direction + ".", new ModuleSlot("랭크", ModuleSlotKind.Rank, "1", "1", "2", "3"));
    }
  }
}
