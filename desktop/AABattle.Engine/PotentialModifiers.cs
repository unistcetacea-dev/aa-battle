using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace AABattle {
 // 사건으로 걸린 수치 배율 하나. Turns: N(턴 종료마다 1 감소, 0이면 해제) / -1(필드를 떠날 때까지)
 public sealed class ActiveModifier {
  public string Key = "", Text = "", Source = "";
  public int Turns = -1;
  public bool Action;   // 기술·피격 사건으로 걸린 배율: 그 행동(기술 한 번)이 끝나면 해제
  public bool Trainer;  // 트레이너 포텐셜에서 걸린 것(트레이너 방호)
  public ActiveModifier Copy() { return (ActiveModifier)MemberwiseClone(); }
 }

 // 수치 배율 모듈(능력치·위력·주는/받는 대미지·명중)을 대미지 계산 단계에 곱한다(확정 14~18).
 // 출처: 조건이 계속 성립하는 포텐셜(상시·필드에 있는 한·「…일 때」 상태 조건) + 사건으로 걸린 배율(ConditionState.Modifiers).
 // 같은 포텐셜(키)은 한 계산에 한 번만 들어간다. 급소일 때 방어 측이 거는 약화(방호)는 무시한다.
 public static class PotentialModifiers {
  sealed class Entry { public string Key; public Fighter Owner; public ScalarEffect Effect; }
  static readonly Dictionary<string, List<ScalarEffect>> cache = new Dictionary<string, List<ScalarEffect>>();

  // 효과 문장에서 확률·기간이 없는 수치 배율만 뽑는다(계속 적용용)
  static List<ScalarEffect> Parse(string effect) {
   List<ScalarEffect> list;
   lock (cache) if (cache.TryGetValue(effect ?? "", out list)) return list;
   list = new List<ScalarEffect>();
   string normalized = EffectNormalizer.NormalizeLines(effect ?? "");
   foreach (string line in Regex.Split(normalized.Replace("\r", ""), @"\n|(?<=[.．。])\s+").Where(x => x.Trim().Length > 0)) {
    string probability, body;
    if (EffectNormalizer.TryProbabilityPrefix(line, out probability, out body)) continue;
    if (Regex.IsMatch(line, @"^(?:\d+T동안|T종료시까지)\s")) continue;
    ScalarEffect scalar;
    if (EffectModules.TryScalar(line.Trim(), out scalar)) list.Add(scalar);
   }
   lock (cache) cache[effect ?? ""] = list;
   return list;
  }

  // 트리거 절이 모두 지금 성립하는 포텐셜(상시·필드에 있는 한·상태 조건). 사용 제한·선행입력은 제외
  public static bool Continuous(Potential p, Fighter owner, Fighter opponent, Rules rules) {
   if (p == null || owner == null || owner.HP <= 0 || !owner.OnField) return false;
   // 사용 제한이 있는 포텐셜은 사건으로만 발동한다. 소지품의 「1번만」 계속 배율은 소지품이 남아 있는 동안 성립(맞은 뒤 소비)
   if (!string.IsNullOrWhiteSpace(p.uses) && p.id != ItemRuntime.Slot || (p.activation ?? "").Trim() == "선행") return false;
   var clauses = TriggerStructures.Clauses(p).ToList();
   if (clauses.Count == 0) return false;
   foreach (string clause in clauses) {
    string label = TriggerStructures.Normalize(clause);
    if (label == TriggerStructures.AlwaysLabel || label == TriggerStructures.FieldLabel) continue;
    if (AnsweredTriggers.StateHolds(clause, owner, opponent, rules)) continue;
    return false;
   }
   return true;
  }

  static IEnumerable<Entry> Entries(Fighter owner, Fighter opponent, Rules rules) {
   if (owner == null || owner.Data == null || owner.HP <= 0) yield break;
   foreach (var p in (owner.Data.potentials ?? new Potential[0]).Where(x => x != null)) {
    if (!Continuous(p, owner, opponent, rules)) continue;
    string condition, effect;
    PotentialLibrary.Split(p.description ?? "", out condition, out effect);
    var list = Parse(effect.Length > 0 ? effect : p.description);
    for (int i = 0; i < list.Count; i++) yield return new Entry { Key = PotentialKeys.For(owner.Data, p) + "#" + i, Owner = owner, Effect = list[i] };
   }
   foreach (var m in owner.Conditions.Modifiers ?? new List<ActiveModifier>()) {
    ScalarEffect scalar;
    if (m != null && EffectModules.TryScalar(m.Text, out scalar)) yield return new Entry { Key = m.Key, Owner = owner, Effect = scalar };
   }
  }

  static IEnumerable<Entry> Both(Fighter a, Fighter b, Rules rules) {
   var seen = new HashSet<string>();
   foreach (var e in Entries(a, b, rules).Concat(Entries(b, a, rules))) if (seen.Add(e.Key)) yield return e;
  }

  static bool SelfSide(string subject) { return subject == "자신" || subject.StartsWith("아군"); }

  // 능력치 배율(1~5 능력, 6 명중, 7 회피). holder의 능력치에 걸린 강화·약화
  public static double Stat(Fighter holder, Fighter opponent, int index, Rules rules) {
   if (holder == null) return 1;
   double factor = 1;
   foreach (var e in Both(holder, opponent, rules)) {
    var s = e.Effect;
    if (s.Stage != "능력치") continue;
    bool appliesToHolder = ReferenceEquals(e.Owner, holder) ? SelfSide(s.Subject) : s.Subject == "상대";
    if (!appliesToHolder) continue;
    if (s.Stats.Contains(index) || (s.HighestStat || s.LowestStat) && index >= 1 && index <= 5 && Extreme(holder, opponent, rules, s.HighestStat) == index) factor *= s.Rate;
   }
   return factor;
  }

  // 가장 높은/낮은 능력: 랭크·배율을 빼고 실능력치로 비교(동률이면 앞 능력)
  static int Extreme(Fighter f, Fighter opponent, Rules rules, bool highest) {
   var stats = Engine.Stats(f, opponent == null ? f.Data.level : opponent.Data.level, rules ?? new Rules());
   int best = 1;
   for (int i = 2; i <= 5; i++) if (highest ? stats[i] > stats[best] : stats[i] < stats[best]) best = i;
   return best;
  }

  // 위력 단계. a=공격 측, b=방어 측, type=실제 상성 배율
  public static double Power(Fighter a, Fighter b, Move move, Rules rules, double type) {
   double factor = 1;
   foreach (var e in Both(a, b, rules)) {
    var s = e.Effect;
    if (s.Stage != "위력" || !s.Condition.Matches(move, a, type)) continue;
    bool fromAttacker = ReferenceEquals(e.Owner, a);
    if (fromAttacker ? SelfSide(s.Subject) : s.Subject == "상대") {
     if (!fromAttacker && rules != null && rules.Critical && s.Rate < 1) continue;   // 급소: 방어 측 방호 무시
     factor *= s.Rate;
    }
   }
   return factor;
  }

  // 최종 대미지 단계
  public static double Damage(Fighter a, Fighter b, Move move, Rules rules, double type) {
   double factor = 1;
   foreach (var e in Both(a, b, rules)) {
    var s = e.Effect;
    if ((s.Stage != "주는 대미지" && s.Stage != "받는 대미지") || !s.Condition.Matches(move, a, type)) continue;
    bool fromAttacker = ReferenceEquals(e.Owner, a);
    bool self = SelfSide(s.Subject);
    // 공격 측이 거는 것: 자신이 주는 / 상대가 받는. 방어 측이 거는 것: 상대가 주는 / 자신이 받는
    bool applies = fromAttacker ? (s.Stage == "주는 대미지" ? self : !self) : (s.Stage == "주는 대미지" ? !self : self);
    if (!applies) continue;
    if (!fromAttacker && rules != null && rules.Critical && s.Rate < 1) continue;
    factor *= s.Rate;
   }
   return factor;
  }

  // 명중 단계(기술 조건이 붙은 명중 배율)
  public static double Accuracy(Fighter a, Fighter b, Move move, Rules rules) {
   double factor = 1;
   foreach (var e in Both(a, b, rules)) {
    var s = e.Effect;
    if (s.Stage != "명중" || !s.Condition.Matches(move, a, 1)) continue;
    bool fromAttacker = ReferenceEquals(e.Owner, a);
    if (fromAttacker ? SelfSide(s.Subject) : s.Subject == "상대") factor *= s.Rate;
   }
   return factor;
  }

  // 계속 성립하는 포텐셜의 표준 문장(확률·기간 없는 것)
  static readonly Dictionary<string, List<string>> sentenceCache = new Dictionary<string, List<string>>();
  static IEnumerable<string> ContinuousSentences(Fighter owner, Fighter opponent, Rules rules, string keyword = null, IEnumerable<Potential> potentials = null, bool ignoreAbilityBypass = false) {
   if (owner == null || owner.Data == null) yield break;
   foreach (var p in (potentials ?? owner.Data.potentials ?? new Potential[0]).Where(x => x != null)) {
    if (keyword != null && (p.description ?? "").IndexOf(keyword, StringComparison.Ordinal) < 0) continue;   // 관련 낱말이 없으면 조건 판정 생략
    if (!Continuous(p, owner, opponent, rules ?? new Rules())) continue;
    if (!ignoreAbilityBypass && AbilityModules.Ignored(p, owner, opponent ?? PotentialQueue.OpponentOf(owner))) continue;
    List<string> list;
    lock (sentenceCache) {
     if (!sentenceCache.TryGetValue(p.description ?? "", out list)) {
      string condition, effect;
      PotentialLibrary.Split(p.description ?? "", out condition, out effect);
      list = Regex.Split(EffectNormalizer.NormalizeLines(effect.Length > 0 ? effect : p.description ?? "").Replace("\r", ""), @"\n|(?<=[.．。])\s+").Select(x => x.Trim()).Where(x => x.Length > 0).Where(x => { string a, b; return !EffectNormalizer.TryProbabilityPrefix(x, out a, out b); }).ToList();
      sentenceCache[p.description ?? ""] = list;
     }
    }
    foreach (string s in list) yield return s;
   }
  }
  public static IEnumerable<string> Sentences(Fighter owner, Fighter opponent, Rules rules, string keyword = null, IEnumerable<Potential> potentials = null, bool ignoreAbilityBypass = false) {
   return ContinuousSentences(owner, opponent, rules, keyword, potentials, ignoreAbilityBypass);
  }
  // 「자신은 상대보다 먼저 행동한다」(계속) 또는 사건으로 얻은 이번 순서 우선(ConditionState.ActFirst)
  // 행동 순서 등급: 2 = 가장 먼저, 1 = 상대보다 먼저, 0 = 없음(사용자 타이밍 규칙)
  public static int FirstTier(Fighter f, Fighter opponent, Rules rules) {
   if (f == null || f.HP <= 0) return 0;
   int tier = f.Conditions.ActFirst;
   foreach (string s in ContinuousSentences(f, opponent, rules, "먼저")) tier = Math.Max(tier, EffectModules.ActFirstTier(s));
   return tier;
  }
  // 부유: 「자신은 공중에 떠오른다」(중력·철구·뿌리박기는 Grounded가 먼저 판정)
  public static bool Floating(Fighter f, Rules rules) { return f != null && f.HP > 0 && ContinuousSentences(f, null, rules, "떠오").Any(EffectModules.IsFloating); }
  // 상태이상 면역: 「자신은 「독」상태가 되지 않는다」
  public static bool StatusImmune(Fighter target, Fighter source, string status, Rules rules) {
   if (target == null) return false;
   foreach (string s in ContinuousSentences(target, source, rules, "않")) { string[] list; bool opponentOnly; if (EffectModules.TryStatusImmunity(s, out list, out opponentOnly) && list.Contains(status) && (!opponentOnly || source != null && !ReferenceEquals(source, target))) return true; }
   return false;
  }
  // 강제교대 무효: 「자신은 강제교대되지 않는다」
  public static bool ForcedSwitchImmune(Fighter f, Fighter opponent, Rules rules) { return f != null && ContinuousSentences(f, opponent, rules, "강제교대").Any(EffectModules.IsForcedSwitchImmunity); }

  // 사건으로 걸린 배율의 기간 감소(턴 종료)
  public static void Tick(Fighter f, List<string> log) {
   if (f == null || f.Conditions.Modifiers == null) return;
   ClearAction(f, log);
   MiscModules.TickAbility(f, log);
   foreach (var m in f.Conditions.Modifiers.Where(x => x.Turns > 0).ToList()) {
    if (--m.Turns > 0) continue;
    f.Conditions.Modifiers.Remove(m);
    if (log != null) log.Add(f.Data.name + ": " + m.Source + "의 배율 효과가 끝났다");
   }
  }

  // 행동 하나가 끝나면 그 행동 동안만 걸린 배율을 해제한다(공격 측·방어 측 모두)
  public static void ClearAction(Fighter f, List<string> log) {
   if (f == null || f.Conditions.Modifiers == null) return;
   foreach (var m in f.Conditions.Modifiers.Where(x => x.Action).ToList()) {
    f.Conditions.Modifiers.Remove(m);
    if (log != null) log.Add(f.Data.name + ": " + m.Source + "의 배율 효과가 끝났다(행동 종료)");
   }
  }

  public static void Add(Fighter f, ActiveModifier modifier) {
   f.Conditions.Modifiers.RemoveAll(x => x.Key == modifier.Key);   // 같은 포텐셜은 겹치지 않고 새로 건다
   f.Conditions.Modifiers.Add(modifier);
  }
 }
}
