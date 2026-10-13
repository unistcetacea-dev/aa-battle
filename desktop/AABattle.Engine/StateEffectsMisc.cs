using System;
using System.Collections.Generic;

namespace AABattle {
 // 기술 설명에서 나온 상태(소란·참기). 이미 걸려 있으면 Apply 앞단(AlreadyHas)에서 실패한다
 public static partial class StateEffects {
  static void AddMisc(List<StateEffectRule> rules) {
   rules.Add(Rule("소란", c => MoveMiscModules.GrantUproar(c.Target, c.Turns, c.Roll(), c.Log), true));
   rules.Add(Rule("참기", c => MoveMiscModules.GrantBide(c.Target, c.Turns, c.Log), true));
  }
 }
}
