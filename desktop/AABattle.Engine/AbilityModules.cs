using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace AABattle {
 // 특성 모듈(사용자 결정): 특성 도감 문장을 포텐셜과 같은 트리거·효과 모듈로 읽어 같은 실행기로 돌린다.
 // 슬롯 「특성」으로 따로 두고(로그는 〔특성명〕, 「특성이 발동했을 때」 사건), 포켓몬의 포텐셜 데이터로 저장하지 않는다(배틀용 복사본에만 붙인다).
 // 기준: 도감 문장 → 오레마스 문서 → 실기. 문장으로 표현할 수 없는 특성은 전용 코드(AbilityRuntime 등)가 맡는다(Dedicated).
 public static class AbilityModules {
  public const string Slot = "특성";
  // 아직 전용 코드가 처리하는 특성(문장 모듈로 옮기면 여기서 뺀다)
  public static readonly HashSet<string> Dedicated = new HashSet<string>(Mechanics.KnownAbilities.Concat(AbilityRuntime.EntryAbilities).Concat(AbilityRuntime.SupportedAbilities));
  // 도감 문장을 같은 뜻의 모듈 문장으로 옮긴 것
  public static readonly Dictionary<string, string> Rewrites = new Dictionary<string, string>();

  // 도감 문장 정리: 붙어 있는 문장 나누기, 변경 내역 괄호 메모 지우기
  public static string Clean(string effect) {
   string t = (effect ?? "").Replace('（', '(').Replace('）', ')').Replace('，', ',').Replace('．', '.').Replace('＋', '+').Replace('－', '-').Replace('：', ':');
   t = Regex.Replace(t, @"\s*\([^()]*(?:추가|변경|→|원작|실질적)[^()]*\)", "");
   t = Regex.Replace(t, @"\.(?=[^\s.\d)])", ". ");
   return Regex.Replace(t, @"\s+", " ").Trim();
  }
  static string Text(AbilityDefinition d) { string r; return Rewrites.TryGetValue(d.name, out r) ? r : AbilityPhrases.Fold(AbilityPhrases.Rewrite(Clean(d.effect))); }

  // 한 특성이 계속 효과와 사건 효과를 함께 가지면 「…때,」 절 단위로 포텐셜 하나씩
  public static Potential[] ToPotentials(AbilityDefinition d) {
   var parts = Split(Text(d));
   return parts.Select((x, i) => {
    string condition, effect; PotentialLibrary.Split(x, out condition, out effect);
    return new Potential { id = Slot, name = i == 0 ? d.name : d.name + " (" + (i + 1) + ")", description = x, triggers = condition.Length > 0 ? new string[0] : new[] { TriggerStructures.AlwaysLabel }, uses = "" };
   }).ToArray();
  }
  // 조건이 없는 문장(계속)과 「…때,」로 시작하는 문장(사건)을 나눈다
  static List<string> Split(string text) {
   var sentences = Regex.Split(text, @"(?<=[.。])\s+").Select(x => x.Trim()).Where(x => x.Length > 0).ToList();
   var result = new List<string>();
   string always = "";
   foreach (string s in sentences) {
    string condition, effect; PotentialLibrary.Split(s, out condition, out effect);
    if (condition.Length > 0) result.Add(s); else always += (always.Length > 0 ? " " : "") + s;
   }
   if (always.Length > 0) result.Insert(0, always);
   return result;
  }

  public static bool Runnable(AbilityDefinition d) {
   if (d == null || Dedicated.Contains(d.name)) return false;
   return ToPotentials(d).All(p => {
    string condition, effect; PotentialLibrary.Split(p.description, out condition, out effect);
    bool trigger = TriggerStructures.Clauses(p).All(c => c == TriggerStructures.AlwaysLabel || TriggerModules.IsModule(c));
    var sentences = Regex.Split(EffectNormalizer.NormalizeLines(effect.Length > 0 ? effect : p.description).Replace("\r", ""), @"\n|(?<=[.．。])\s+").Where(x => x.Trim().Length > 0).ToList();
    return trigger && sentences.Count > 0 && sentences.All(x => EffectModules.Classify(x).Length > 0);
   });
  }
  public static string Status(AbilityDefinition d) { return d == null ? "미구현" : Dedicated.Contains(d.name) || Runnable(d) ? "자동" : "수동"; }

  // 배틀 포켓몬의 포텐셜 목록에서 특성 슬롯을 지금 특성(무효화 포함)에 맞춘다
  public static void Sync(Fighter f) {
   if (f == null || f.Data == null) return;
   var list = (f.Data.potentials ?? new Potential[0]).Where(p => p == null || p.id != Slot).ToList();
   if (!f.AbilitySuppressed) {
    var d = AbilityCatalog.Find(AbilityRuntime.Name(f));
    if (Runnable(d)) list.AddRange(ToPotentials(d));
   }
   f.Data.potentials = list.ToArray();
   AbilityPhrases.SyncTypes(f);
  }
  // 틀깨기: 상대가 기술을 내는 동안 자신의 특성 계속 효과는 무시된다
  public static bool Ignored(Potential p, Fighter owner, Fighter opponent) {
   return p != null && p.id == Slot && opponent != null && opponent.Conditions.Acting && Mechanics.BypassAbility(opponent);
  }
 }
}
