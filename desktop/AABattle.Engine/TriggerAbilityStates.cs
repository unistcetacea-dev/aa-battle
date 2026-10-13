using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace AABattle {
 // 특성 모듈화 3차에서 만든 트리거 모듈(포텐셜에서도 쓴다)
 //  상태: 속도 비교 · 첫 행동 전 · 공중 · 같은 타입 · 최후의 1체 아님 · PT 특성 · 또는
 //   「자신의 「속도」가 상대보다 높을 때」 「자신의 「속도」가 상대보다 낮을 때」(같으면 둘 다 성립하지 않는다)
 //   「장소에 나와 아직 기술을 내보내지 않았을 때」(장소에 나와 첫 기술을 내보내는 행동이 끝날 때까지)
 //   「자신이 공중에 떠 있을 때」 「서로 공중에 떠 있을 때」
 //   「상대가 자신과 같은 타입일 때」(타입이 하나라도 겹친다)
 //   「자신이 「최후의 1체」가 아닐 때」
 //   「PT에 특성이 「마이너스/플러스」인 아군이 있을 때」(자신 외의 생존 멤버, 대기 포함)
 //   「A 또는 B」(상태 절 둘 이상 중 하나)
 //  사건: 급소 받음 · 필드 빈사 · 나무열매 사용
 //   「상대의 기술이 급소에 맞았을 때」 「필드의 누군가가 「빈사」 상태가 되었을 때」 「자신이 「나무열매」를 사용했을 때」
 public static partial class TriggerModules {
  static readonly Regex SpeedCompare = new Regex(@"^자신의 「속도」(?:이|가) 상대보다 (?<d>높|낮)을 때$");
  static readonly Regex FloatingState = new Regex(@"^(?<w>자신이|서로) 공중에 떠 있을 때$");
  static readonly Regex PartyAbility = new Regex(@"^PT에 특성이 (?<n>(?:「[^」]+」)+)인 아군이 있을 때$");
  static readonly Regex CriticalTaken = new Regex(@"^상대의 (?<c>(?:「[^」]+」)*)\s*기술이 급소에 맞았을 때$");
  const string FirstActionLabel = "장소에 나와 아직 기술을 내보내지 않았을 때";
  const string SameTypeLabel = "상대가 자신과 같은 타입일 때";
  const string NotLastLabel = "자신이 「최후의 1체」가 아닐 때";
  const string AnyFaintLabel = "필드의 누군가가 「빈사」 상태가 되었을 때";
  const string BerryUsedLabel = "자신이 「나무열매」를 사용했을 때";
  const string OrWord = "이거나 ";

  static bool TryParseAbilityState(string t, out TriggerSpec spec) {
   spec = null; Match m;
   // 「A이거나 B일 때」: A는 「…일 때」에서 「일 때」를 뗀 것. 두 절이 모두 지금 성립하는 상태 모듈이어야 한다
   int or = t.IndexOf(OrWord, StringComparison.Ordinal);
   if (or > 0 && t.EndsWith("일 때")) {
    var left = Parse(t.Substring(0, or).Trim() + "일 때");
    var right = Parse(t.Substring(or + OrWord.Length).Trim());
    if (left.Module.Length > 0 && right.Module.Length > 0 && !left.IsEvent && !right.IsEvent && left.Module != "또는" && right.Module != "또는") {
     string a = Canonical(left), b = Canonical(right);
     if (a.EndsWith("일 때") && b.EndsWith("일 때")) { spec = new TriggerSpec { Module = "또는", Names = new[] { a, b } }; return true; }
    }
   }
   m = SpeedCompare.Match(t);
   if (m.Success) { spec = new TriggerSpec { Module = "속도 비교", Op = m.Groups["d"].Value == "높" ? "높음" : "낮음" }; return true; }
   if (t == FirstActionLabel) { spec = new TriggerSpec { Module = "첫 행동 전" }; return true; }
   m = FloatingState.Match(t);
   if (m.Success) { spec = new TriggerSpec { Module = "공중", Subject = m.Groups["w"].Value == "서로" ? "자신과 상대" : "자신" }; return true; }
   if (t == SameTypeLabel) { spec = new TriggerSpec { Module = "같은 타입" }; return true; }
   if (t == NotLastLabel) { spec = new TriggerSpec { Module = "최후 아님" }; return true; }
   m = PartyAbility.Match(t);
   if (m.Success) {
    var names = Regex.Matches(m.Groups["n"].Value, "「([^」]+)」").Cast<Match>().SelectMany(x => x.Groups[1].Value.Split('/')).Select(x => x.Trim()).Where(x => x.Length > 0).ToArray();
    if (names.Length == 0) return false;
    spec = new TriggerSpec { Module = "PT 특성", Names = names };
    return true;
   }
   m = CriticalTaken.Match(t); MoveCondition c;
   if (m.Success && MoveCondition.TryParse(m.Groups["c"].Value, out c)) { spec = new TriggerSpec { Module = "급소 받음", Condition = c }; return true; }
   if (t == AnyFaintLabel) { spec = new TriggerSpec { Module = "필드 빈사" }; return true; }
   if (t == BerryUsedLabel) { spec = new TriggerSpec { Module = "나무열매 사용" }; return true; }
   return false;
  }

  static string CanonicalAbilityState(TriggerSpec s) {
   switch (s.Module) {
    case "또는": return s.Names[0].Substring(0, s.Names[0].Length - "일 때".Length) + OrWord + s.Names[1];
    case "속도 비교": return "자신의 「속도」가 상대보다 " + (s.Op == "높음" ? "높" : "낮") + "을 때";
    case "첫 행동 전": return FirstActionLabel;
    case "공중": return s.Subject == "자신과 상대" ? "서로 공중에 떠 있을 때" : "자신이 공중에 떠 있을 때";
    case "같은 타입": return SameTypeLabel;
    case "최후 아님": return NotLastLabel;
    case "PT 특성": return "PT에 특성이 " + string.Concat(s.Names.Select(x => "「" + x + "」")) + "인 아군이 있을 때";
    case "급소 받음": return "상대의 " + CondText(s.Condition) + "기술이 급소에 맞았을 때";
    case "필드 빈사": return AnyFaintLabel;
    case "나무열매 사용": return BerryUsedLabel;
   }
   return "";
  }

  // ===== 지금 성립하는가 =====
  [ThreadStatic] static HashSet<TriggerSpec> comparingSpeed;
  static bool HoldsAbilityState(TriggerSpec s, Fighter owner, Fighter opponent, Rules rules) {
   switch (s.Module) {
    case "또는": return s.Names.Any(x => Holds(x, owner, opponent, rules));
    case "속도 비교": {
     if (owner == null || opponent == null || owner.HP <= 0 || opponent.HP <= 0 || !owner.OnField || !opponent.OnField) return false;
     if (comparingSpeed == null) comparingSpeed = new HashSet<TriggerSpec>();
     if (!comparingSpeed.Add(s)) return false;
     try {
      int mine = Engine.Effective(owner, opponent, 5, rules), theirs = Engine.Effective(opponent, owner, 5, rules);
      return s.Op == "높음" ? mine > theirs : mine < theirs;
     } finally { comparingSpeed.Remove(s); }
    }
    case "첫 행동 전": return owner != null && owner.HP > 0 && owner.OnField && !owner.Conditions.MovedSinceEntry;
    case "공중": {
     bool mine = owner != null && owner.HP > 0 && owner.OnField && !Mechanics.Grounded(owner, rules);
     if (s.Subject != "자신과 상대") return mine;
     return mine && opponent != null && opponent.HP > 0 && opponent.OnField && !Mechanics.Grounded(opponent, rules);
    }
    case "같은 타입": return owner != null && opponent != null && owner.HP > 0 && opponent.HP > 0 && (owner.Data.types ?? new string[0]).Any(t => Mechanics.Has(opponent, t));
    case "최후 아님": {
     if (owner == null || owner.HP <= 0) return false;
     return Party(rules, SideOf(owner), owner).Count(x => x.HP > 0) > 1;
    }
    case "PT 특성": {
     if (owner == null || owner.HP <= 0) return false;
     return Party(rules, SideOf(owner), owner).Any(x => !ReferenceEquals(x, owner) && !Mechanics.SameMember(x, owner) && x.HP > 0 && s.Names.Contains(AbilityRuntime.Name(x)));
    }
   }
   return false;
  }

  // ===== 사건 대조 =====
  static bool MatchesAbilityEvent(TriggerSpec s, TriggerFact f) {
   switch (s.Module) {
    case "급소 받음": return f.Kind == "명중" && f.Received && f.Critical && f.Move != null && s.Condition.Matches(f.Move, f.User, f.TypeEffect);
    case "필드 빈사": return f.Kind == "상태 됨" && f.Method == "빈사";
    case "나무열매 사용": return f.Kind == "소지품 소실" && f.Self && f.Method == "소모" && ItemRuntime.IsBerry(f.Item);
   }
   return false;
  }
 }
}
