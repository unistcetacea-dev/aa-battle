using System.Collections.Generic;

namespace AABattle
{
  public static partial class ModuleCatalog
  {
    // 칼데아 3차에서 늘린 효과 모듈(타입 대미지 등)
    static void RegisterChaldeaEffects(List<ModuleDefinition> modules)
    {
      CoreAdd(modules, "effect.hp.damage.type", "타입 대미지", DamageVariantModules.Kind, "체력",
        "{주체}에게 최대 HP의 1/{분모} 「{타입}」 타입 대미지를 준다.",
        new ModuleSlot("주체", ModuleSlotKind.Subject, "상대", "상대", "자신"),
        new ModuleSlot("분모", ModuleSlotKind.Denominator, "8"),
        new ModuleSlot("타입", ModuleSlotKind.Type, "악"));
      CoreAdd(modules, "effect.potential.grant", "포텐셜 부여", PotentialGrantModules.Kind, "포텐셜",
        "자신에게 『{포텐셜}』를 부여한다.",
        new ModuleSlot("포텐셜", ModuleSlotKind.Text, "주인의 특권", PotentialGrantModules.GrantableNames()));
    }
  }
}
