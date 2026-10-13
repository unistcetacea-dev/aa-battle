using System;
using System.Collections.Generic;

namespace AABattle
{
  public static partial class ModuleCatalog
  {
    // 소지품 기술 묶음(MoveItemModules)의 효과 모듈. 기술 설명 문장이 표준 문장으로 바뀌어 실행된다.
    static void RegisterMoveItemEffects(List<ModuleDefinition> modules)
    {
      const string family = "기술·소지품";
      CoreAdd(modules, "effect.item.swap", "소지품 교환", "소지품 교환", family, MoveItemModules.Swap + ".");
      CoreAdd(modules, "effect.item.break.food", "나무열매·주얼 파괴", "소지품 제한 파괴", family, MoveItemModules.BreakFood + ".");
      CoreAdd(modules, "effect.item.recycle.any", "소모한 소지품 재획득", "소지품 재획득", family, MoveItemModules.RecycleAny + ".");
      CoreAdd(modules, "effect.berry.eat.all", "필드 전원 나무열매 먹기", "나무열매 모두 먹기", family, MoveItemModules.Teatime + ".");
      CoreAdd(modules, "effect.berry.pluck", "상대 나무열매 뺏어먹기", "나무열매 뺏어먹기", family, MoveItemModules.Pluck + ".");
      CoreAdd(modules, "effect.item.fling", "소지품 던지기", "소지품 던지기", family, MoveItemModules.Fling + ".");
      CoreAdd(modules, "effect.berry.consume", "나무열매 소모", "나무열매 소모", family, MoveItemModules.ConsumeBerry + ".");
    }
  }
}
