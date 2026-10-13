using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace AABattle {
 // 효과 문장 한 줄을 읽은 결과. 예: "중확률로 상대를 2T간 「마비」상태로 한다"
 public class StateEffectSentence {
  public string Target = "";    // "자신" · "상대" · ""(문장에 대상 없음 → 상태별 기본 대상)
  public string State = "";     // 「」 안의 상태 이름(별칭은 표준 이름으로 바꾼다)
  public int Turns;             // "NT간" (0이면 상태의 기본 지속)
  public bool UntilTurnEnd;     // "T종료시까지"
  public double? Rate;          // 확률 접두어(드물게 10% · 저확률 20% · 중확률 50%)
  public bool IgnoreImmunity;   // 「…을 무시하고」(상태이상 면역 관통, 확정 22)
 }

 // 상태 하나를 거는 규칙. 표(StateEffects.Table)에 줄을 추가하면 포텐셜·필드효과 문장이 그 상태를 자동으로 건다
 public class StateEffectRule {
  public string Name;
  public string Category = "상태변화";   // "상태이상"(Fighter.Status 한 칸) / "상태변화"(ConditionState, 겹칠 수 있음)
  public string[] Aliases = new string[0];
  public bool SelfByDefault;    // 대상이 생략된 "「X」상태로 한다"를 자신에게 거는 상태
  public Func<StateEffectContext, bool> Apply;
 }

 // 상태를 걸 때 필요한 값
 public class StateEffectContext {
  public Fighter Target, Source;
  public Rules Rules;
  public Func<double> Rng;
  public int Turns;             // 0이면 기본 지속
  public bool IgnoreImmunity;
  public List<string> Log;
  public double Roll() { return Rng == null ? .5 : Rng(); }
  public void Say(string text) { if (Log != null) Log.Add(text); }
  public int SourceSide() { return Source == null ? -1 : Mechanics.PartySide(Source, Rules); }
 }

 // 엔진이 턴을 처리하는 동안의 규칙·난수·전투 중 포켓몬. 범위 밖(UI 미리보기 등)에서는 효과를 실행하지 않고 로그만 남긴다
 public sealed class EffectScope : IDisposable {
  [ThreadStatic] static EffectScope current;
  public static EffectScope Current { get { return current; } }
  public readonly Fighter[] Fighters;   // 교대하면 같은 배열의 칸이 바뀐다
  public readonly Rules Rules;
  public Fighter ExtraPowerOwner;
  public string ExtraPowerSentence = "";
  public readonly Func<double> Rng;
  public readonly TrainerBattleState[] States;   // 진영별 트레이너·포텐셜 사용 이력
  public readonly List<ExtraActionGrant> ExtraActions = new List<ExtraActionGrant>();   // 사건으로 얻은 추가행동(행동 사이에 넣는다)
  public readonly List<KeyValuePair<int, string>> ForcedSwitches = new List<KeyValuePair<int, string>>();  // 강제교대 요청(진영, 출처). 엔진의 교대 단계가 처리
  readonly EffectScope previous;
  EffectScope(Fighter[] fighters, Rules rules, Func<double> rng, TrainerBattleState[] states) { Fighters = fighters; Rules = rules; Rng = rng; States = states; previous = current; }
  public static EffectScope Begin(Fighter[] fighters, Rules rules, Func<double> rng, TrainerBattleState[] states = null) { current = new EffectScope(fighters, rules, rng, states); StateWatch.Prime(fighters, rules); return current; }
  public void Dispose() { if (current == this) current = previous; }
  public static void Restore(EffectScope saved) { current = saved; }
  public Fighter Opponent(Fighter fighter) {
   if (Fighters == null || Fighters.Length != 2) return null;
   if (ReferenceEquals(Fighters[0], fighter)) return Fighters[1];
   if (ReferenceEquals(Fighters[1], fighter)) return Fighters[0];
   return null;
  }
 }

 public static partial class StateEffects {
  static readonly Regex Sentence = new Regex(
   @"^(?:(?<ignore>[^「]*무시하고)\s*,?\s*)?" +
   @"(?<until>T\s*종료\s*시\s*까지\s*,?\s*)?" +
   @"(?:(?<n1>\d+)\s*T\s*(?:간|동안)\s*,?\s*)?" +
   @"(?:(?<target>자신|자진|상대|상대\s*포켓몬)\s*(?:을|를)\s*)?" +
   @"(?:(?<n2>\d+)\s*T\s*(?:간|동안)\s*)?" +
   @"「(?<state>[^」]+)」\s*(?:상태\s*)?" +
   @"(?:(?:로|으로)\s*(?<make>한다|만든다)|(?:가|이)\s*(?<become>된다))$");

  public static readonly StateEffectRule[] Table = BuildTable();

  // 효과 문장 한 줄을 상태 부여로 읽는다. 상태 부여 문장이 아니면 null
  public static StateEffectSentence Parse(string line) {
   string text = PotentialParts.Clean(line ?? "").Replace("드믈게", "드물게").Trim().TrimEnd('.', '。', '．').Trim();
   var sentence = new StateEffectSentence();
   string probability, body;
   if (EffectNormalizer.TryProbabilityPrefix(text, out probability, out body)) {
    sentence.Rate = PotentialProbability.Rate(probability);
    text = body.Trim().TrimEnd('.', '。', '．').Trim();
   }
   var match = Sentence.Match(text);
   if (!match.Success) return null;
   var rule = Find(match.Groups["state"].Value);
   sentence.State = rule == null ? match.Groups["state"].Value.Trim() : rule.Name;
   string target = match.Groups["target"].Value;
   sentence.Target = target.Length == 0 ? (match.Groups["become"].Success ? "자신" : "") : target == "자진" ? "자신" : target.StartsWith("상대") ? "상대" : target;
   string turns = match.Groups["n1"].Success ? match.Groups["n1"].Value : match.Groups["n2"].Value;
   if (turns.Length > 0) sentence.Turns = int.Parse(turns);
   sentence.UntilTurnEnd = match.Groups["until"].Success;
   sentence.IgnoreImmunity = match.Groups["ignore"].Success;
   return sentence;
  }

  public static StateEffectRule Find(string name) {
   string key = (name ?? "").Trim();
   return Table.FirstOrDefault(x => x.Name == key || x.Aliases.Contains(key));
  }

  // 문장을 읽어 상태를 건다. 상태 부여 문장이고 표에 있는 상태면 true(확률 실패·면역도 처리한 것으로 본다)
  public static bool TryApply(string line, Fighter self, Fighter opponent, Rules rules, Func<double> rng, List<string> log, string source, bool rollRate = true) {
   var sentence = Parse(line);
   if (sentence == null) return false;
   var rule = Find(sentence.State);
   if (rule == null) {
    if (log != null) log.Add(source + ": 「" + sentence.State + "」 상태는 아직 자동 적용되지 않는다.");
    return false;
   }
   bool toSelf = sentence.Target == "자신" || sentence.Target.Length == 0 && rule.SelfByDefault;
   var target = toSelf ? self : opponent;
   if (target == null || target.HP <= 0) {
    if (log != null) log.Add(source + ": 「" + rule.Name + "」 대상이 없다.");
    return true;
   }
   if (rollRate && sentence.Rate.HasValue && (rng == null ? .5 : rng()) >= sentence.Rate.Value) {
    if (log != null) log.Add(source + ": 「" + rule.Name + "」 확률 판정 실패");
    return true;
   }
   if (rule.Category != "상태이상" && !sentence.IgnoreImmunity && self != null && !Mechanics.BypassAbility(self) && PotentialModifiers.StatusImmune(target, self, rule.Name, rules ?? new Rules())) { if (log != null) log.Add(target.Data.name + ": 특성/포텐셜로 「" + rule.Name + "」 상태가 되지 않는다"); return true; }
   if (rule.Category == "상태변화" && Mechanics.AlreadyHas(target, rule.Name, log)) return true;
   var context = new StateEffectContext {
    Target = target, Source = self, Rules = rules ?? new Rules(), Rng = rng, Log = log,
    Turns = sentence.UntilTurnEnd ? 1 : sentence.Turns, IgnoreImmunity = sentence.IgnoreImmunity
   };
   if (rule.Apply(context) && log != null) log.Add(source + " → " + target.Data.name + ": 「" + rule.Name + "」");
   return true;
  }

  // 큐 실행기용: 확률은 큐가 이미 판정했다. 상태 부여 결과를 돌려준다
  public static EffectOutcome Apply(string line, Fighter self, Fighter opponent, Rules rules, Func<double> rng, List<string> log, string source) {
   var sentence = Parse(line);
   var rule = sentence == null ? null : Find(sentence.State);
   if (rule == null) return EffectOutcome.Pending;
   var target = sentence.Target == "자신" || sentence.Target.Length == 0 && rule.SelfByDefault ? self : opponent;
   if (target == null || target.HP <= 0) { if (log != null) log.Add(source + ": 「" + rule.Name + "」 대상이 없다."); return EffectOutcome.Failed; }
   if (rule.Category != "상태이상" && !sentence.IgnoreImmunity && self != null && !Mechanics.BypassAbility(self) && PotentialModifiers.StatusImmune(target, self, rule.Name, rules ?? new Rules())) { if (log != null) log.Add(target.Data.name + ": 특성/포텐셜로 「" + rule.Name + "」 상태가 되지 않는다"); return EffectOutcome.Failed; }
   if (rule.Category == "상태변화" && Mechanics.AlreadyHas(target, rule.Name, log)) return EffectOutcome.Failed;
   var context = new StateEffectContext { Target = target, Source = self, Rules = rules ?? new Rules(), Rng = rng, Log = log, Turns = sentence.UntilTurnEnd ? 1 : sentence.Turns, IgnoreImmunity = sentence.IgnoreImmunity };
   if (!rule.Apply(context)) return EffectOutcome.Failed;
   if (log != null) log.Add(source + " → " + target.Data.name + ": 「" + rule.Name + "」");
   return EffectOutcome.Success;
  }

  static StateEffectRule Rule(string name, Func<StateEffectContext, bool> apply, bool self = false, params string[] aliases) {
   return new StateEffectRule { Name = name, Apply = apply, SelfByDefault = self, Aliases = aliases };
  }

  static bool Flag(StateEffectContext c, string label, bool already, Action set) {
   if (already) { c.Say(c.Target.Data.name + ": " + label + " 실패: 이미 상태변화가 있다."); return false; }
   set();
   return true;
  }

  static StateEffectRule[] BuildTable() {
   var rules = new List<StateEffectRule>();
   // 주요 상태이상: 면역·중복·파티 수면 제한은 ApplyStatus가 판정한다
   foreach (string status in new[] { "독", "맹독", "마비", "화상", "얼음", "잠듦", "동상" }) {
    string name = status;
    var major = Rule(name, c => Mechanics.ApplyStatus(c.Target, c.Source, name, c.Rules, c.Rng, c.Log, false, c.IgnoreImmunity), false, name == "잠듦" ? new[] { "잠듦", "수면", "잠" } : name == "얼음" ? new[] { "얼음", "빙결" } : new string[0]);
    major.Category = "상태이상";
    rules.Add(major);
   }
   rules.Add(Rule("혼란", c => {
    var t = c.Target;
    if (c.Source != null && !Mechanics.BypassAbility(c.Source) && PotentialModifiers.StatusImmune(t, c.Source, "혼란", c.Rules) || c.Rules.Terrain == "미스트필드" && Mechanics.Grounded(t, c.Rules)) { c.Say("혼란 무효: 특성/포텐셜 / 미스트필드"); return false; }
    if (StateTicks.Guarded(t, c.Source, c.Rules)) { c.Say("신비의부적: 혼란 무효"); return false; }
    t.Conditions.Confusion = c.Turns > 0 ? c.Turns : 1 + (int)(c.Roll() * 4);
    return true;
   }));
   rules.Add(Rule("도발", c => { c.Target.Conditions.Taunt = c.Turns > 0 ? c.Turns : 3; return true; }));
   rules.Add(Rule("풀죽음", c => { c.Target.Conditions.Flinch = true; return true; }));
   rules.Add(Rule("씨뿌리기", c => {
    if (Mechanics.Has(c.Target, "풀")) { c.Say("풀 타입: 씨뿌리기 무효"); return false; }
    c.Target.Conditions.Seed = true;
    return true;
   }));
   // 행동 제한: 기본 지속·실패 조건은 ApplyActionState가 판정한다
   foreach (string action in new[] { "침묵", "망각", "트집", "회복봉인", "사슬묶기", "검은눈빛", "조이기", "앙코르" }) {
    string name = action;
    rules.Add(Rule(name, c => Mechanics.ApplyActionState(c.Target, name, c.Turns, c.Log), false, name == "앙코르" ? new[] { "앵콜" } : new string[0]));
   }
   // 다음 행동 1회의 타입 변경과 위력 강화
   foreach (string swap in new[] { "악인", "침수", "송전" }) {
    string name = swap;
    rules.Add(Rule(name, c => { Mechanics.SetTypeSwap(c.Target, name, c.Log); return true; }, true));
   }
   // 방어 기술(사용자 결정: 「방어」 상태가 된다, T 종료까지) · 버티기
   rules.Add(Rule("방어", c => { c.Target.Conditions.Protect = true; c.Say(c.Target.Data.name + ": 방어 태세!"); return true; }, true));
   rules.Add(Rule("버티기", c => { c.Target.Conditions.Enduring = true; c.Say(c.Target.Data.name + ": 버티기 태세!"); return true; }, true));
   // 행동불능(반동): 다음 행동을 쉰다(파괴광선 등 「다음 T 종료 후까지」)
   // 빈사(자폭 등): 자신이 쓰러진다
   rules.Add(Rule("빈사", c => { if (c.Target.HP <= 0) return false; c.Target.HP = 0; c.Say(c.Target.Data.name + "은 쓰러졌다!"); return true; }, true));
   rules.Add(Rule("행동불능", c => { c.Target.Conditions.Recharge = true; return true; }, true, "반동대기"));
   rules.Add(Rule("가열", c => Flag(c, "가열", c.Target.Conditions.Heated, () => c.Target.Conditions.Heated = true), true));
   rules.Add(Rule("냉각", c => Flag(c, "냉각", c.Target.Conditions.Chilled, () => c.Target.Conditions.Chilled = true), true));
   rules.Add(Rule("충전", c => Flag(c, "충전", c.Target.Conditions.Charged, () => c.Target.Conditions.Charged = true), true));
   // 반응 상태
   rules.Add(Rule("폭탄", c => { Mechanics.ApplyBomb(c.Target, c.Rules); return true; }));
   rules.Add(Rule("요격", c => { Mechanics.ApplyIntercept(c.Target, c.Turns > 0 ? c.Turns : 1); return true; }, true));
   rules.Add(Rule("분노", c => { c.Target.Conditions.Rage = c.Turns > 0 ? c.Turns : 4 + Math.Min(1, (int)(c.Roll() * 2)); return true; }, true));
   rules.Add(Rule("마음의눈", c => {
    int side = c.SourceSide();
    if (side < 0) { c.Say("마음의눈: 건 쪽 진영을 알 수 없다."); return false; }
    Mechanics.ApplyMindReader(c.Target, side, c.Rules);
    return true;
   }, false, "록온"));
   // 그 밖의 상태
   rules.Add(Rule("도우미", c => Flag(c, "도우미", c.Target.Conditions.HelpingHand, () => c.Target.Conditions.HelpingHand = true), true));
   rules.Add(Rule("웅크리기", c => Flag(c, "웅크리기", c.Target.Conditions.Curled, () => c.Target.Conditions.Curled = true), true));
   rules.Add(Rule("아쿠아링", c => Flag(c, "아쿠아링", c.Target.Conditions.AquaRing, () => c.Target.Conditions.AquaRing = true), true));
   rules.Add(Rule("뿌리박기", c => { c.Target.Conditions.Ingrain = true; return true; }, true));
   rules.Add(Rule("헤롱헤롱", c => {
    int side = c.SourceSide();
    if (side < 0) { c.Say("헤롱헤롱: 건 쪽 진영을 알 수 없다."); return false; }
    c.Target.Conditions.AttractSide = side;
    return true;
   }));
   rules.Add(Rule("동면", c => { Mechanics.ApplyHibernate(c.Target, c.Rules); return true; }));
   rules.Add(Rule("죽음의 카운트", c => {
    c.Target.Conditions.DeathCount = c.Turns > 0 ? c.Turns : 4;
    return true;
   }, false, "멸망의노래", "멸망의 노래"));
   AddMore(rules);
   return rules.ToArray();
  }
 }
}
