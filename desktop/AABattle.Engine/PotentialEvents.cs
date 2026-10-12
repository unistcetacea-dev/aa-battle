using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace AABattle {
 // 포텐셜 공통 효과 큐 (EFFECT_QUESTIONS_30 확정 01~09, TRIGGER_QUESTIONS_50 T-05·T-06).
 //
 // 흐름: 트리거 성립(기존 매칭 함수) → PotentialQueue.Raise(사건)
 //   → 후보 정렬(속도 내림차순, 동속 rng) → 포텐셜마다
 //     a. 중복 차단(같은 사건 1회, 같은 연쇄 1회) b. 사용 제한(1/시·1/T·최초) c. 확률(소유자·사건 묶음 1회)
 //     d. 「할 수 있다」 확인 e. 사용 횟수 소비 f. 효과 실행(독립 / 대가·「이때」 종속)
 // 효과 실행은 PotentialExecutors에 등록된 실행기가 맡는다. 실행기가 없는 효과는 예전처럼 "효과 판정 대기"로 남긴다.

 public enum EffectOutcome {
  Success,   // 효과가 실제로 적용됨
  Failed,    // 면역·중복 등으로 실패(사용 횟수는 돌려주지 않는다)
  Pending,   // 엔진이 처리하지 않음(사람이 판정). 실패가 아니다
  Deferred   // 다른 단계가 처리(교대 요청 등). 사용 횟수도 그 단계가 소비한다
 }

 // 포텐셜을 발동시킬 수 있는 사건 하나
 public sealed class PotentialEvent {
  public string Label = "";                 // 정규화 트리거 라벨(라벨 사건). 조건식 사건이면 설명용
  public Func<string, bool> Clause;         // 조건식 사건: 정규화 전 절 문자열(공백 제거)을 받아 성립 여부
  public Func<string, bool> Spec;           // 트리거 모듈 사건: 절 문자열을 그대로 받아 성립 여부(TriggerModules)
  public Fighter Owner;                     // 발동 관점의 포켓몬(「자신」). 트레이너 사건이면 장소의 자기 포켓몬
  public TrainerRecord Trainer;             // 트레이너 포텐셜 사건이면 트레이너
  public Fighter Other;                     // 상대(없으면 장소의 상대)
  public string Message = "";               // 사건 문맥(로그용)
  public Move Move;
  public int Damage, Hit;
  public Action<string> Log;                // 호출한 쪽의 로그
  public bool Announce;                     // 처리 직전에 Message를 로그에 남긴다(TriggerStructures.Fire 형식)
  public Func<PotentialInstance, bool, string> PendingText;  // 실행하지 않을 때의 로그(호출 경로별 예전 문구). bool = 추가 조건 있음
  internal int Id, ChainId, Depth;
  public bool Matches(string clause) {
   if (Spec != null) return Spec(clause ?? "");
   if (Clause != null) return Clause(Regex.Replace(PotentialParts.Clean(clause ?? ""), @"\s+", ""));
   return TriggerStructures.Normalize(clause) == Label;
  }
 }

 // 사건에 걸린 포텐셜 하나(포켓몬 또는 트레이너). Key = 소유자 데이터 ID + 슬롯 (+ 같은 슬롯 안의 순번)
 public sealed class PotentialInstance {
  public Fighter Owner;
  public TrainerRecord Trainer;
  public Potential Pokemon;
  public PotentialRecord Record;
  public string Key = "", Name = "", Slot = "", Uses = "", Activation = "", Effect = "", Description = "";
  public string[] Clauses = new string[0];
  public bool IsTrainer { get { return Trainer != null; } }
  public string Label { get { return IsTrainer ? "TRAINER 『" + Name + "』" : "『" + Name + "』"; } }
  // OptionalEffectPrompt는 Potential을 받으므로 트레이너 포텐셜은 임시 Potential로 넘긴다
  public Potential AsPotential() {
   return Pokemon ?? new Potential { id = Slot, name = Name, description = Effect, activation = Activation, uses = Uses, triggers = Clauses };
  }
 }

 // 실행기 한 번의 호출
 public sealed class EffectCall {
  public PotentialInstance Source;
  public PotentialEvent Event;
  public string Line = "";
  public Rules Rules;
  public Func<double> Rng;
  public List<string> Log;                  // 실행기가 남긴 줄. 큐가 사건의 로그로 옮긴다
  public Fighter Owner { get { return Source.Owner; } }
  public Fighter Opponent { get { return Event.Other ?? PotentialQueue.OpponentOf(Source.Owner); } }
  public void Say(string text) { if (Log != null) Log.Add(text); }
 }

 // 실행기: 효과 문장 한 줄을 처리할 수 있는지(CanRun)와 결과(Run)
 public sealed class PotentialExecutor {
  public string Name = "";
  public Func<EffectCall, bool> CanRun;
  public Func<EffectCall, EffectOutcome> Run;
 }

 // 실행기 등록부. F2 이후 작업은 Register로 실행기를 붙인다(먼저 등록한 실행기가 우선)
 public static class PotentialExecutors {
  static readonly List<PotentialExecutor> all = new List<PotentialExecutor>();
  public static IEnumerable<PotentialExecutor> All { get { return all; } }
  static PotentialExecutors() { PotentialQueue.BuiltIn.Register(); MoveModules.RegisterExecutors(); }
  public static PotentialExecutor Register(string name, Func<EffectCall, bool> canRun, Func<EffectCall, EffectOutcome> run) {
   var executor = new PotentialExecutor { Name = name, CanRun = canRun, Run = run };
   all.Add(executor);
   return executor;
  }
  public static void Unregister(PotentialExecutor executor) { all.Remove(executor); }
  public static PotentialExecutor Find(EffectCall call) { return all.FirstOrDefault(x => x.CanRun(call)); }
 }

 // 포켓몬·트레이너 포텐셜 사용 이력(인스턴스 키 기준). TrainerBattleState가 진영별로 들고 배틀 내내 복사된다
 public sealed class PotentialUsage {
  public Dictionary<string, int> Battle = new Dictionary<string, int>();   // 1/시·최초 1회
  public Dictionary<string, int> Turn = new Dictionary<string, int>();     // 1/T (턴 종료에 초기화)
  public HashSet<string> Declared = new HashSet<string>();                 // 이번 T 선행입력 선언(선언 때 이미 소비)
  public PotentialUsage Copy() {
   return new PotentialUsage { Battle = new Dictionary<string, int>(Battle), Turn = new Dictionary<string, int>(Turn), Declared = new HashSet<string>(Declared) };
  }
  // "1/시"·"2/시"·"최초" → (횟수, 배틀 단위) / "1/T" → (횟수, 턴 단위) / 그 밖 → 제한 없음
  public static bool TryLimit(string uses, out int count, out bool perTurn) {
   count = 0; perTurn = false;
   string value = PotentialParts.Clean(uses ?? "").Replace(" ", "");
   if (value.Length == 0) return false;
   if (value.Contains("최초")) { count = 1; return true; }
   var match = Regex.Match(value, @"(?<n>\d+)/(?<unit>시|T|턴)");
   if (!match.Success) return false;
   count = int.Parse(match.Groups["n"].Value);
   perTurn = match.Groups["unit"].Value != "시";
   return count > 0;
  }
  public bool CanUse(string key, string uses) {
   int count; bool perTurn;
   if (!TryLimit(uses, out count, out perTurn)) return true;
   var table = perTurn ? Turn : Battle;
   int used; table.TryGetValue(key, out used);
   return used < count;
  }
  public void Use(string key, string uses) {
   int count; bool perTurn;
   if (!TryLimit(uses, out count, out perTurn)) return;
   var table = perTurn ? Turn : Battle;
   int used; table.TryGetValue(key, out used);
   table[key] = used + 1;
  }
  public bool Used(string key) { return Battle.ContainsKey(key) || Turn.ContainsKey(key); }
  public void EndTurn() { Turn.Clear(); Declared.Clear(); }
 }

 public static class PotentialKeys {
  static string Owner(Pokemon p) { return p == null ? "" : !string.IsNullOrEmpty(p.id) ? p.id : p.name; }
  // 같은 슬롯에 포텐셜이 여럿이면 순번으로 구분한다
  public static string For(Pokemon owner, Potential potential) {
   var list = (owner == null ? null : owner.potentials) ?? new Potential[0];
   string slot = potential == null ? "" : potential.id ?? "";
   int ordinal = list.Where(x => x != null && (x.id ?? "") == slot).ToList().IndexOf(potential);
   return Owner(owner) + "|" + slot + "|" + Math.Max(0, ordinal);
  }
  public static string For(TrainerRecord trainer, PotentialRecord record) {
   var list = (trainer == null ? null : trainer.potentials) ?? new List<PotentialRecord>();
   string slot = record == null ? "" : record.slot ?? "";
   int ordinal = list.Where(x => x != null && (x.slot ?? "") == slot).ToList().IndexOf(record);
   string owner = trainer == null ? "" : !string.IsNullOrEmpty(trainer.id) ? trainer.id : trainer.name;
   return "T:" + owner + "|" + slot + "|" + Math.Max(0, ordinal);
  }
  // 선행입력 선언처럼 이름만 아는 경우
  public static string ForName(Pokemon owner, string name) {
   var potential = (owner == null ? null : owner.potentials ?? new Potential[0]).FirstOrDefault(x => x != null && x.name == name);
   return potential == null ? Owner(owner) + "|?|" + name : For(owner, potential);
  }
 }

 public static class PotentialQueue {
  public const int MaxDepth = 8;
  // 효과 사이 종속: 앞 효과가 대가인 연결(대가 실패 → 뒤 효과 실패)과, 앞 효과 성공이 필요한 「이때」·「이 효과로」
  public static readonly string[] CostConnectors = { "소비해서", "소비해", "소비하여", "소비하고", "파괴해서", "파괴해", "파괴하여", "파괴하고", "버리고", "버려서", "잃고", "지불하고", "지불해" };
  public static readonly string[] FollowUpStarts = { "이때", "이 때", "이 효과로", "이효과로" };
  // 「임의의」 대상 선택(사용자 선택). 테스트에서 바꿔 끼울 수 있다. 기본은 첫 후보
  public static Func<PotentialInstance, List<Fighter>, Fighter> ChooseTarget = (source, candidates) => candidates.FirstOrDefault();

  [ThreadStatic] static int nextEvent;
  [ThreadStatic] static Frame current;
  sealed class Frame { public int ChainId, Depth; public HashSet<string> ChainFired; public Frame Parent; }

  public static Fighter OpponentOf(Fighter owner) { var scope = EffectScope.Current; return scope == null ? null : scope.Opponent(owner); }

  // 사건 묶음을 발동 순서(소유 포켓몬의 현재 속도 내림차순, 동속이면 rng)로 처리한다
  public static bool Active { get { return EffectScope.Current != null; } }
  public static void Raise(params PotentialEvent[] events) {
   var scope = EffectScope.Current;
   var list = (events ?? new PotentialEvent[0]).Where(x => x != null).ToList();
   if (scope != null) foreach (var x in list) if (x.Owner == null && x.Trainer != null && scope.States != null)
     for (int i = 0; i < scope.States.Length && i < scope.Fighters.Length; i++) if (scope.States[i] != null && ReferenceEquals(scope.States[i].Trainer, x.Trainer)) x.Owner = scope.Fighters[i];
   list = list.Where(x => x.Owner != null).ToList();
   if (scope == null || list.Count == 0) return;
   var rng = scope.Rng ?? (() => .5);
   var keyed = list.Select(e => new { e, speed = Speed(scope, e.Owner), tie = list.Count > 1 ? rng() : 0 }).ToList();
   foreach (var item in keyed.OrderByDescending(x => x.speed).ThenBy(x => x.tie)) Process(scope, item.e);
  }

  static int Speed(EffectScope scope, Fighter owner) {
   var other = scope.Opponent(owner) ?? owner;
   try { return Engine.Effective(owner, other, 5, scope.Rules); } catch { return 0; }
  }

  // 사건에 맞는 포텐셜을 찾는다(기존 매칭: 절 하나라도 성립). 추가 절이 있으면 Extra=true
  public static List<PotentialInstance> Candidates(PotentialEvent e, out List<bool> extra) {
   var result = new List<PotentialInstance>(); extra = new List<bool>();
   if (e.Trainer != null) {
    foreach (var record in (e.Trainer.potentials ?? new List<PotentialRecord>()).Where(p => p != null && p.enabled && p.slot == "고유" && p.name.Length > 0)) {
     var clauses = TrainerTriggers.ClausesOf(record).ToArray();
     if (!clauses.Any(e.Matches)) continue;
     result.Add(new PotentialInstance { Owner = e.Owner, Trainer = e.Trainer, Record = record, Key = PotentialKeys.For(e.Trainer, record), Name = record.name, Slot = record.slot, Uses = record.uses, Activation = record.activation, Effect = record.effect ?? "", Description = record.effect ?? "", Clauses = clauses });
     extra.Add(false);
    }
    return result;
   }
   foreach (var p in (e.Owner.Data.potentials ?? new Potential[0]).Where(x => x != null)) {
    var clauses = TriggerStructures.Clauses(p).ToArray();
    if (!clauses.Any(e.Matches)) continue;
    string condition, effect;
    PotentialLibrary.Split(p.description ?? "", out condition, out effect);
    if (effect.Length == 0) effect = p.description ?? "";
    result.Add(new PotentialInstance { Owner = e.Owner, Pokemon = p, Key = PotentialKeys.For(e.Owner.Data, p), Name = p.name, Slot = p.id ?? "", Uses = p.uses, Activation = p.activation, Effect = effect, Description = p.description ?? "", Clauses = clauses });
    extra.Add(Extra(e, clauses));
   }
   return result;
  }

  // 확정 48: 여러 절은 AND. 사건과 맞지 않는 절은 지금 성립하는 상태 조건이어야 한다(등장 계열 두 사건은 OR)
  static bool Extra(PotentialEvent e, string[] clauses) {
   var scope = EffectScope.Current;
   var matched = clauses.FirstOrDefault(e.Matches);
   foreach (var clause in clauses.Where(x => !e.Matches(x))) {
    if (matched != null && TriggerModules.Alternative(matched, clause)) continue;
    if (!TriggerModules.Holds(clause, e.Owner, e.Other ?? (scope == null ? null : scope.Opponent(e.Owner)), scope == null ? null : scope.Rules)) return true;
   }
   return false;
  }

  static void Process(EffectScope scope, PotentialEvent e) {
   e.Id = ++nextEvent;
   var parent = current;
   e.ChainId = parent == null ? e.Id : parent.ChainId;
   e.Depth = parent == null ? 0 : parent.Depth + 1;
   var chainFired = parent == null ? new HashSet<string>() : parent.ChainFired;
   var log = e.Log;
   if (e.Announce && e.Message.Length > 0) Say(log, e.Message);
   if (e.Depth > MaxDepth) { Say(log, "포텐셜 연쇄가 " + MaxDepth + "단계를 넘어 중단했다 — " + e.Message); return; }
   List<bool> extra;
   var candidates = Candidates(e, out extra);
   var plans = new List<Plan>();
   var seen = new HashSet<string>();
   for (int i = 0; i < candidates.Count; i++) {
    var source = candidates[i];
    if (!seen.Add(source.Key)) continue;                          // a. 같은 사건에서 1회
    if (chainFired.Contains(source.Key)) { Say(log, source.Label + " — 같은 연쇄에서 이미 발동해 다시 발동하지 않는다."); continue; }
    plans.Add(BuildPlan(scope, e, source, extra[i]));
   }
   // c. 확률: 같은 소유자·같은 사건 묶음으로 1회 판정(중확률 보장 포함)
   var rolling = plans.Where(p => p.Executable).ToList();
   var rolled = PotentialProbability.Resolve(rolling.Select(p => p.Source.Description + " " + p.Source.Activation).ToArray(), scope.Rng);
   for (int i = 0; i < rolling.Count; i++) {
    double? rate = PotentialProbability.Rate(rolling[i].Source.Description + " " + rolling[i].Source.Activation);
    rolling[i].ProbabilityOk = !rate.HasValue || rolled[i];
   }
   foreach (var plan in plans) Fire(scope, e, plan, chainFired);
  }

  sealed class Plan {
   public PotentialInstance Source;
   public bool Extra, Executable, ProbabilityOk = true, Optional, Declared;
   public List<Step> Steps = new List<Step>();
  }
  sealed class Step { public string Line; public PotentialExecutor Executor; public bool AfterCost, NeedsSuccess; }

  static Plan BuildPlan(EffectScope scope, PotentialEvent e, PotentialInstance source, bool extra) {
   var plan = new Plan { Source = source, Extra = extra };
   if (extra) return plan;
   foreach (var step in Split(source.Effect)) {
    var call = new EffectCall { Source = source, Event = e, Line = step.Line, Rules = scope.Rules, Rng = scope.Rng };
    step.Executor = PotentialExecutors.Find(call);
    plan.Steps.Add(step);
   }
   // 실행할 수 있는 효과가 하나도 없거나 다른 단계가 맡는 효과뿐이면 예전처럼 로그만 남긴다
   plan.Executable = plan.Steps.Any(s => s.Executor != null && !IsDeferred(s.Executor));
   plan.Optional = Regex.IsMatch(PotentialParts.Clean(source.Effect), @"수\s*있다") || plan.Steps.Any(s => s.Executor != null && s.Executor.Name == BuiltIn.AnalysisName);
   return plan;
  }

  static bool IsDeferred(PotentialExecutor executor) { return executor.Name == BuiltIn.SwitchName; }

  // 효과 문장을 독립 효과 목록으로 나눈다: 줄바꿈 → 문장 끝 → 대가 연결어
  static List<Step> Split(string effect) {
   var steps = new List<Step>();
   effect = Regex.Replace(effect ?? "", @"[.．。]\s*(이 대미지로는 빈사로 할 수 없다)", " $1");
   foreach (string raw in (effect ?? "").Replace("\r", "").Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries)) {
    // 수집 원문 변형도 편집기 모듈 표준 문장으로 바꾼 뒤 실행기에 넘긴다
    string normalized = EffectNormalizer.Normalize(raw.Trim());
    if (normalized.Length == 0) normalized = raw.Trim();
    foreach (string sentence in Regex.Split(normalized, @"(?<=[.．。])\s+")) {
     string text = sentence.Trim();
     if (text.Length == 0) continue;
     // 확률은 큐가 포텐셜당 1회 판정하므로 실행기에는 확률 접두어를 뗀 문장을 넘긴다
     string probability, body;
     if (EffectNormalizer.TryProbabilityPrefix(text, out probability, out body)) text = body.Trim();
     string starter = FollowUpStarts.FirstOrDefault(x => text.StartsWith(x));
     bool follow = starter != null;
     if (follow) text = text.Substring(starter.Length).TrimStart(' ', ',', '，');
     var pieces = SplitCost(text);
     for (int i = 0; i < pieces.Count; i++)
      steps.Add(new Step { Line = pieces[i], AfterCost = i > 0, NeedsSuccess = i == 0 && follow && steps.Count > 0 });
    }
   }
   return steps;
  }

  static List<string> SplitCost(string text) {
   foreach (string connector in CostConnectors) {
    int index = text.IndexOf(connector, StringComparison.Ordinal);
    if (index <= 0) continue;
    string cost = text.Substring(0, index + connector.Length).Trim().TrimEnd(',', '，');
    string rest = text.Substring(index + connector.Length).Trim().TrimStart(',', '，').Trim();
    if (rest.Length == 0) break;
    return new List<string> { cost, rest };
   }
   return new List<string> { text };
  }

  static void Fire(EffectScope scope, PotentialEvent e, Plan plan, HashSet<string> chainFired) {
   var source = plan.Source;
   var log = e.Log;
   string head = source.Label + " " + (e.Label.Length > 0 ? e.Label + " " : "") + "트리거 충족";
   if (!plan.Executable) {
    // 실행기가 없거나 추가 조건이 있으면 예전과 같은 대기 로그(교대 런타임은 이 로그를 읽는다)
    Say(log, e.PendingText != null ? e.PendingText(source, plan.Extra) : head + (plan.Extra ? " · 추가 조건 확인 필요" : "") + " · 효과 판정 대기: " + source.Effect);
    return;
   }
   var usage = Usage(scope, source);
   bool preInput = (source.Activation ?? "").Trim() == "선행";
   bool declared = preInput && usage != null && usage.Declared.Contains(source.Key);
   if (preInput && !declared) { Say(log, head + " · 선행입력을 선언하지 않아 발동하지 않는다."); return; }
   // b. 사용 제한
   if (!declared && usage != null && !usage.CanUse(source.Key, source.Uses)) { Say(log, head + " · 사용 횟수(" + source.Uses + ")를 이미 썼다."); return; }
   if (!declared && usage == null && PotentialUsageLimited(source)) { Say(log, head + " · 효과 판정 대기(사용 이력을 알 수 없음): " + source.Effect); return; }
   // c. 확률
   if (!plan.ProbabilityOk) { Say(log, head + " · 확률 판정 실패"); return; }
   // d. 할 수 있다
   if (plan.Optional) {
    var prompt = scope.Rules == null ? null : scope.Rules.OptionalEffectPrompt;
    if (prompt == null) { Say(log, head + " · 효과 판정 대기: " + source.Effect); return; }
    if (!prompt(source.Owner, e.Other ?? OpponentOf(source.Owner), source.AsPotential())) { Say(log, head + " · 사용하지 않았다."); return; }
   }
   // e. 소비(이후 효과가 실패해도 돌려주지 않는다)
   if (!declared && usage != null) usage.Use(source.Key, source.Uses);
   Say(log, head + " · 효과 적용: " + source.Effect);
   chainFired.Add(source.Key);
   // f. 실행. 실행 중 생긴 사건은 같은 연쇄, 깊이+1로 즉시 처리된다
   var saved = current;
   current = new Frame { ChainId = e.ChainId, Depth = e.Depth, ChainFired = chainFired, Parent = saved };
   try {
    EffectOutcome previous = EffectOutcome.Success;
    foreach (var step in plan.Steps) {
     EffectOutcome outcome;
     if (step.AfterCost && previous != EffectOutcome.Success) outcome = Blocked(log, source, step, previous, "대가");
     else if (step.NeedsSuccess && previous != EffectOutcome.Success) outcome = Blocked(log, source, step, previous, "앞 효과");
     else if (step.Executor == null) { Say(log, source.Label + " 효과 판정 대기: " + step.Line); outcome = EffectOutcome.Pending; }
     else {
      var lines = new List<string>();
      var call = new EffectCall { Source = source, Event = e, Line = step.Line, Rules = scope.Rules, Rng = scope.Rng, Log = lines };
      outcome = step.Executor.Run(call);
      foreach (string line in lines) Say(log, line);
      if (outcome == EffectOutcome.Pending) Say(log, source.Label + " 효과 판정 대기: " + step.Line);
     }
     previous = outcome;
    }
   } finally { current = saved; }
   // 소지품의 「1번만」: 발동하면 소지품이 사라진다
   if (source.Pokemon != null && source.Pokemon.id == ItemRuntime.Slot && source.Uses == "1/시") ItemRuntime.Lose(source.Owner, "소비", x => Say(log, x));
  }

  static bool PotentialUsageLimited(PotentialInstance source) { int count; bool perTurn; return PotentialUsage.TryLimit(source.Uses, out count, out perTurn); }

  static EffectOutcome Blocked(Action<string> log, PotentialInstance source, Step step, EffectOutcome previous, string reason) {
   if (previous == EffectOutcome.Failed) { Say(log, source.Label + " " + reason + "가 실패해 실행하지 않는다: " + step.Line); return EffectOutcome.Failed; }
   Say(log, source.Label + " 효과 판정 대기(" + reason + " 결과 대기): " + step.Line);
   return EffectOutcome.Pending;
  }

  static PotentialUsage Usage(EffectScope scope, PotentialInstance source) {
   if (scope.States == null) return null;
   int side = SideOf(scope, source);
   return side < 0 || side >= scope.States.Length || scope.States[side] == null ? null : scope.States[side].Usage;
  }

  static int SideOf(EffectScope scope, PotentialInstance source) {
   if (source.IsTrainer && scope.States != null)
    for (int i = 0; i < scope.States.Length; i++) if (scope.States[i] != null && ReferenceEquals(scope.States[i].Trainer, source.Trainer)) return i;
   if (scope.Fighters != null) for (int i = 0; i < scope.Fighters.Length; i++) if (ReferenceEquals(scope.Fighters[i], source.Owner)) return i;
   return source.Owner == null ? -1 : Mechanics.PartySide(source.Owner, scope.Rules);
  }

  // 효과의 대상 문자열을 장소 기준으로 해석한다(03·04)
  public static List<Fighter> Targets(EffectCall call, string subject) {
   var scope = EffectScope.Current;
   string text = PotentialParts.Clean(subject ?? "").Replace(" ", "");
   var owner = call.Owner; var opponent = call.Opponent;
   bool bench = text.Contains("대기") || text.Contains("파티"), fainted = text.Contains("빈사");
   Func<Fighter, bool> alive = f => f != null && (fainted || f.HP > 0);
   var pool = new List<Fighter>();
   if (text.StartsWith("상대")) { if (alive(opponent) && (opponent.OnField || bench)) pool.Add(opponent); if (bench) pool.AddRange(Party(scope, opponent).Where(alive)); }
   else { if (alive(owner)) pool.Add(owner); if (bench) pool.AddRange(Party(scope, owner).Where(alive)); }
   pool = pool.Distinct().ToList();
   if (text.Contains("임의") && pool.Count > 1) { var chosen = ChooseTarget(call.Source, pool); return chosen == null ? new List<Fighter>() : new List<Fighter> { chosen }; }
   if ((text.Contains("랜덤") || text.Contains("무작위")) && pool.Count > 1) { var rng = call.Rng ?? (() => .5); return new List<Fighter> { pool[Math.Min(pool.Count - 1, (int)(rng() * pool.Count))] }; }
   if (text.Contains("1체") && pool.Count > 1) return new List<Fighter> { pool[0] };
   return pool;
  }

  static IEnumerable<Fighter> Party(EffectScope scope, Fighter member) {
   if (scope == null || scope.Rules == null || member == null) return Enumerable.Empty<Fighter>();
   int side = Mechanics.PartySide(member, scope.Rules);
   return side < 0 ? Enumerable.Empty<Fighter>() : (scope.Rules.Parties[side] ?? new Fighter[0]).Where(x => x != null && !Mechanics.SameMember(x, member));
  }

  static void Say(Action<string> log, string text) { if (log != null) log(text); }

  // 기존에 동작하던 실행기를 감싼 등록(F1에서는 동작을 바꾸지 않는다)
  internal static class BuiltIn {
   public const string AnalysisName = "해석 허가", SwitchName = "교대·유턴 부여";
   public static void Register() {
    // 우선도: 기술 선택 단계(PriorityEffectRuntime.PrimeTurn)가 따로 처리하는 기술 사용 라벨은 제외
    PotentialExecutors.Register("우선도",
     c => !c.Source.IsTrainer && !PriorityEffectRuntime.SelectionLabel(c.Event.Label) && !PriorityEffectRuntime.Continuous(c.Source.Pokemon) && PriorityEffectRuntime.Matches(c.Line),
     c => PriorityEffectRuntime.ApplyLine(c.Line, c.Owner, c.Opponent, c.Event.Label, c.Event.Message, c.Say) ? EffectOutcome.Success : EffectOutcome.Failed);
    // 해석 허가: 「할 수 있다」 확인은 큐가 한다
    PotentialExecutors.Register(AnalysisName,
     c => !c.Source.IsTrainer && EffectStructures.IsAnalysisPermission(c.Line),
     c => {
      var target = c.Opponent;
      if (target == null || target.HP <= 0) return EffectOutcome.Failed;
      if (PotentialProtection.AnalysisBlocked(target, c.Owner, c.Rules)) { c.Say(target.Data.name + ": 데이터 해석을 무효화했다"); return EffectOutcome.Failed; }
      var state = BattleRecords.StateOf(c.Owner);
      if (BattleRecords.IsAnalyzed(state, target)) { c.Say(target.Data.name + ": 이미 해석한 상대다"); return EffectOutcome.Failed; }
      c.Say("『" + c.Source.Name + "』 선택 발동 · " + target.Data.name + "의 데이터를 해석한다.");
      if (state != null) BattleRecords.RecordAnalysis(state, c.Owner, new[] { c.Owner }, target, c.Say); else RuntimeTriggers.Analyzed(c.Owner, new[] { c.Owner }, target, c.Say);
      return EffectOutcome.Success;
     });
    // 교대·유턴 부여: 실제 교대는 엔진의 교대 단계(ResolveTriggeredSwitches)가 로그를 읽어 처리한다
    PotentialExecutors.Register(SwitchName,
     c => SwitchEffectRuntime.IsSwitchEffect(c.Line) || SwitchEffectRuntime.IsPivotGrant(c.Line),
     c => EffectOutcome.Deferred);
    // 트레이너 고유: 아군 능력 상승(기존 TrainerTriggers 동작)
    PotentialExecutors.Register("트레이너 아군 능력 상승",
     c => c.Source.IsTrainer && TrainerTriggers.AllyRise(c.Line) != null,
     c => TrainerTriggers.ApplyAllyRise(c.Line, c.Owner, c.Opponent, c.Say) ? EffectOutcome.Success : EffectOutcome.Failed);
    // 상태 부여 문장(StateEffects 표). 확률은 큐가 포텐셜당 1회 판정했으므로 다시 굴리지 않는다
    PotentialExecutors.Register("상태 부여",
     c => StateEffects.Parse(c.Line) != null && StateEffects.Find(StateEffects.Parse(c.Line).State) != null,
     c => StateEffects.Apply(c.Line, c.Owner, c.Opponent, c.Rules, c.Rng, c.Log, c.Source.Label));
    // F2 능력 랭크 변화(기간이 붙은 랭크는 되돌리는 규칙이 없어 판정 대기)
    PotentialExecutors.Register("능력 랭크 변화",
     c => { RankEffect r; int turns; return Duration(c.Line, out turns) == c.Line && EffectModules.TryRank(c.Line, out r); },
     RunRank);
    // F2 수치 배율: 사건으로 걸리면 기간(NT동안·T종료시까지)·이번 T(기술 관련 사건)·필드를 떠날 때까지 유지
    PotentialExecutors.Register("수치 배율",
     c => { ScalarEffect s; int turns; return EffectModules.TryScalar(Duration(c.Line, out turns), out s); },
     RunScalar);
    // F2 종족치 변경(확정 13: 가장 최근 효과로 덮어쓰고 교대하면 원래 값)
    PotentialExecutors.Register("종족치 변경",
     c => { SpeciesEffect s; return EffectModules.TrySpecies(c.Line, out s); },
     RunSpecies);
    // F3 최대 HP의 1/N: 회복(빈사 제외) · 대미지(간접, 매직가드에 막힘) · 소비(대가, 매직가드 무시, 부족하면 실패)
    PotentialExecutors.Register("최대 HP의 1/N",
     c => { FractionEffect f; return EffectModules.TryFraction(c.Line, out f); },
     RunFraction);
    // F4a 장소 전개(확정 24: 기술과 같은 규칙, 기간이 없으면 5T)
    PotentialExecutors.Register("장소 전개",
     c => { int turns; string kind, value; return EffectModules.TryPlace(Duration(c.Line, out turns), out kind, out value); },
     c => {
      int turns; string kind, value; EffectModules.TryPlace(Duration(c.Line, out turns), out kind, out value);
      if (turns <= 0) turns = 5;
      var rules = c.Rules ?? new Rules(); var log = c.Log ?? new List<string>();
      if (kind == "날씨") return Mechanics.SetWeather(c.Owner, c.Opponent, value, turns, rules, log, c.Source.Label) ? EffectOutcome.Success : EffectOutcome.Failed;
      if (kind == "지형") return Mechanics.SetTerrain(c.Owner, c.Opponent, value, turns, rules, log, c.Source.Label) ? EffectOutcome.Success : EffectOutcome.Failed;
      if (kind == "트릭룸") { rules.TrickRoom = rules.TrickRoom > 0 ? 0 : turns; c.Say("트릭룸 " + (rules.TrickRoom > 0 ? turns + "턴" : "해제") + " (" + c.Source.Label + ")"); return EffectOutcome.Success; }
      rules.Gravity = turns; c.Say("중력 " + turns + "턴 (" + c.Source.Label + ")");
      return EffectOutcome.Success;
     });
    // F4a 설치물(확정 25: 층수 제한을 넘으면 실패, 묶인 뒤 효과도 실패)
    PotentialExecutors.Register("설치물",
     c => { bool install; string name; return EffectModules.TryHazard(c.Line, out install, out name); },
     c => {
      bool install; string name; EffectModules.TryHazard(c.Line, out install, out name);
      var rules = c.Rules ?? new Rules();
      int own = Mechanics.PartySide(c.Owner, rules); if (own < 0) own = 0;
      if (install) return Mechanics.InstallHazard(1 - own, name, rules, c.Log ?? new List<string>()) ? EffectOutcome.Success : EffectOutcome.Failed;
      if (!rules.Sides[own].Any()) { c.Say((own + 1) + "팀 진영에 제거할 설치물이 없다"); return EffectOutcome.Failed; }
      rules.Sides[own].ClearHazards(); c.Say((own + 1) + "팀 진영의 설치물을 제거했다 (" + c.Source.Label + ")");
      return EffectOutcome.Success;
     });
    // F4a 강제교대(확정 26): 행동 뒤 교대 단계에서 상대 멤버 중 무작위
    PotentialExecutors.Register("강제교대",
     c => EffectModules.IsForcedSwitch(c.Line),
     c => {
      var scope = EffectScope.Current; var target = c.Opponent;
      if (scope == null || target == null || target.HP <= 0) return EffectOutcome.Failed;
      int side = Mechanics.PartySide(target, c.Rules ?? new Rules());
      if (side < 0) side = Array.IndexOf(scope.Fighters, target);
      if (side < 0) return EffectOutcome.Failed;
      scope.ForcedSwitches.Add(new KeyValuePair<int, string>(side, c.Source.Label));
      c.Say(target.Data.name + ": " + c.Source.Label + "의 강제교대 요청");
      return EffectOutcome.Success;
     });
    // F4b 추가행동(확정 29): 사건으로 얻으면 행동 사이에 넣는다(가장 먼저 → 바로 다음, 가장 마지막에 → 턴 끝)
    PotentialExecutors.Register("추가행동",
     c => { ExtraActionGrant g; int every; return ExtraActionRuntime.TryParse(c.Line, out g, out every); },
     c => {
      ExtraActionGrant g; int every; ExtraActionRuntime.TryParse(c.Line, out g, out every);
      var scope = EffectScope.Current; var rules = c.Rules ?? new Rules();
      if (scope == null || c.Owner == null || c.Owner.HP <= 0) return EffectOutcome.Failed;
      if (every > 0 && (rules.Turn <= 0 || rules.Turn % every != 0)) { c.Say(c.Source.Label + ": 「" + every + "」배수 T가 아니다"); return EffectOutcome.Failed; }
      g.Side = Array.IndexOf(scope.Fighters, c.Owner); if (g.Side < 0) g.Side = Mechanics.PartySide(c.Owner, rules);
      if (g.Side < 0) return EffectOutcome.Failed;
      g.Owner = c.Owner; g.Source = c.Source.Label;
      scope.ExtraActions.Add(g);
      c.Owner.Conditions.PendingAction = true;
      c.Say(c.Owner.Data.name + ": 추가행동을 얻었다 (" + (g.First ? "바로 다음" : "턴 마지막") + ")");
      return EffectOutcome.Success;
     });
    // G 사건으로 걸린 방호·회피·관통·해석 무효: 기간(NT동안·T종료시까지)·이번 행동·필드를 떠날 때까지
    PotentialExecutors.Register("방호 상태",
     c => { int turns; string body = Duration(c.Line, out turns); string k; bool tr; MoveCondition m; double? rate; return PotentialProtection.TryGuard(body, out k, out m, out rate) || PotentialProtection.TryPierce(body, out m, out tr) || PotentialProtection.IsAnalysisImmunity(body); },
     c => {
      int turns; string body = Duration(c.Line, out turns);
      var owner = c.Owner; if (owner == null || owner.HP <= 0) return EffectOutcome.Failed;
      bool action = turns == 0 && ActionEvent(c.Event);
      PotentialModifiers.Add(owner, new ActiveModifier { Key = c.Source.Key, Text = body, Source = c.Source.Label, Turns = turns == 0 ? -1 : turns, Action = action, Trainer = c.Source.IsTrainer });
      c.Say(owner.Data.name + ": " + body + (action ? " (이번 행동)" : turns > 0 ? " (" + turns + "T)" : " (필드를 떠날 때까지)"));
      return EffectOutcome.Success;
     });
    // 먼저 행동(사용자 결정: 우선도와 관계없이 먼저). 사건으로 얻으면 다음 순서 결정에 한 번 쓴다
    PotentialExecutors.Register("먼저 행동",
     c => EffectModules.IsActFirst(c.Line),
     c => { if (c.Owner == null || c.Owner.HP <= 0) return EffectOutcome.Failed; c.Owner.Conditions.ActFirst = Math.Max(c.Owner.Conditions.ActFirst, EffectModules.ActFirstTier(c.Line)); c.Say(c.Owner.Data.name + ": 다음 행동 순서에서 상대보다 먼저 행동한다"); return EffectOutcome.Success; });
    // F4b 포켓몬 타입 변경(확정 30): 같은 타입이면 실패, 다르면 마지막 것. 교대하면 원래 타입
    PotentialExecutors.Register("타입 변경",
     c => { int turns; string subject; string[] types; return Duration(c.Line, out turns) == c.Line && TypeRules.TryTypeChange(c.Line, out subject, out types); },
     c => {
      string subject; string[] types; TypeRules.TryTypeChange(c.Line, out subject, out types);
      var target = Recipient(c, subject);
      if (target == null || target.HP <= 0) return EffectOutcome.Failed;
      var current = target.Data.types ?? new string[0];
      if (current.Length == types.Length && current.All(types.Contains)) { c.Say(target.Data.name + ": 이미 「" + string.Join("/", types) + "」 타입이다"); return EffectOutcome.Failed; }
      if (target.Conditions.OriginalTypes == null) target.Conditions.OriginalTypes = current.ToArray();
      target.Data.types = types.ToArray();
      c.Say(target.Data.name + ": 「" + string.Join("/", types) + "」 타입이 되었다 (" + c.Source.Label + ")");
      return EffectOutcome.Success;
     });
    // F4b 사건으로 걸린 상성 지정·타입 내성 관통·자속 배율·유리한 상성: 수치 배율과 같은 기간 규칙
    PotentialExecutors.Register("상성 상태",
     c => { int turns; string k = TypeRules.Classify(Duration(c.Line, out turns)); return k.Length > 0 && k != "타입 변경"; },
     c => {
      int turns; string body = Duration(c.Line, out turns);
      var owner = c.Owner; if (owner == null || owner.HP <= 0) return EffectOutcome.Failed;
      bool action = turns == 0 && ActionEvent(c.Event);
      PotentialModifiers.Add(owner, new ActiveModifier { Key = c.Source.Key, Text = body, Source = c.Source.Label, Turns = turns == 0 ? -1 : turns, Action = action });
      c.Say(owner.Data.name + ": " + body + (action ? " (이번 행동)" : turns > 0 ? " (" + turns + "T)" : " (필드를 떠날 때까지)"));
      return EffectOutcome.Success;
     });
    // F3 상태이상 치유
    PotentialExecutors.Register("상태 치유·해제",
     c => { string t; string[] n; bool r; return StateCure.TryParse(c.Line, out t, out n, out r); },
     c => { string t; string[] n; bool r; StateCure.TryParse(c.Line, out t, out n, out r); var lines = new List<string>(); bool done = StateCure.Cure(t == "상대" ? c.Opponent : c.Owner, n, lines); foreach (var x in lines) c.Say(x); return done ? EffectOutcome.Success : EffectOutcome.Failed; });
    // 특성 변경(기간이 있으면 끝날 때, 없으면 장소를 떠날 때 원래대로)
    PotentialExecutors.Register("특성 변경",
     c => { int turns; string sj, ab; return MiscModules.TryAbility(Duration(c.Line, out turns), out sj, out ab); },
     c => {
      int turns; string sj, ab; MiscModules.TryAbility(Duration(c.Line, out turns), out sj, out ab);
      var target = sj == "상대" ? c.Opponent : c.Owner;
      if (target == null || target.HP <= 0) return EffectOutcome.Failed;
      if (AbilityRuntime.Name(target) == ab) { c.Say(target.Data.name + ": 이미 「" + ab + "」 특성이다"); return EffectOutcome.Failed; }
      var lines = new List<string>(); MiscModules.ChangeAbility(target, ab, turns, lines, c.Source.Label); foreach (var x in lines) c.Say(x);
      return EffectOutcome.Success;
     });
    PotentialExecutors.Register("소지품 소실",
     c => MiscModules.IsBreak(c.Line),
     c => { if (c.Owner == null || string.IsNullOrEmpty(c.Owner.Data.item)) return EffectOutcome.Failed; ItemRuntime.Lose(c.Owner, "부서졌다", c.Say); return EffectOutcome.Success; });
    PotentialExecutors.Register("기능확장 부여",
     c => { string subject; string[] moves; return FunctionExtensions.TryGrant(c.Line, out subject, out moves); },
     c => {
      string subject; string[] moves; FunctionExtensions.TryGrant(c.Line, out subject, out moves);
      var owner = c.Owner; if (owner == null || owner.HP <= 0) return EffectOutcome.Failed;
      foreach (string move in moves) if (!owner.Conditions.ExtensionMoves.Contains(move)) owner.Conditions.ExtensionMoves.Add(move);
      c.Say(owner.Data.name + ": 기능확장 " + string.Join("·", moves) + "을 내보낼 수 있다 (장소를 떠날 때까지)");
      return EffectOutcome.Success;
     });
    PotentialExecutors.Register("상태이상 치유",
     c => { string t; return EffectModules.TryCure(c.Line, out t); },
     c => {
      string t; EffectModules.TryCure(c.Line, out t);
      var target = t == "상대" ? c.Opponent : c.Owner;
      if (target == null || target.HP <= 0 || target.Status == "정상") return EffectOutcome.Failed;
      c.Say(target.Data.name + ": " + target.Status + " 치유 (" + c.Source.Label + ")");
      target.Status = "정상"; target.Conditions.ToxicStage = 1; target.Conditions.SleepFromRest = false;
      return EffectOutcome.Success;
     });
   }

   // 「NT동안 / T종료시까지」 접두어를 떼고 기간을 돌려준다(없으면 0, T종료시까지는 1)
   static string Duration(string line, out int turns) {
    turns = 0;
    var m = Regex.Match(line ?? "", @"^(?:(?<n>\d+)\s*T\s*(?:동안|간)|(?<end>T\s*종료\s*시\s*까지))\s*,?\s*");
    if (!m.Success) return line;
    turns = m.Groups["end"].Success ? 1 : int.Parse(m.Groups["n"].Value);
    return line.Substring(m.Length);
   }

   static Fighter Recipient(EffectCall c, string subject) { return subject == "상대" ? c.Opponent : c.Owner; }

   // 기술·피격 관련 사건으로 걸린 배율은 이번 T만(확정: 조건이 사라질 때까지)
   static bool ActionEvent(PotentialEvent e) {
    string label = e.Label ?? "";
    return e.Move != null || PriorityEffectRuntime.SelectionLabel(label) || label == TriggerStructures.AttackSuccessLabel || label == TriggerStructures.ReceivedLabel || label == TriggerStructures.ReceivedOrNullifiedLabel || label == TriggerStructures.DamagedLabel;
   }

   static EffectOutcome RunRank(EffectCall c) {
    RankEffect r; EffectModules.TryRank(c.Line, out r);
    var target = Recipient(c, r.Subject);
    if (target == null || target.HP <= 0) return EffectOutcome.Failed;
    var other = ReferenceEquals(target, c.Owner) ? c.Opponent : c.Owner;
    var stats = r.Stats.ToList();
    if (r.Mode == "랜덤") stats = new List<int> { EffectModules.StatIndex(PotentialStatRules.ChooseRandom(c.Rng ?? (() => .5))) };
    else if (r.Mode == "최고" || r.Mode == "최저") stats = new List<int> { EffectModules.StatIndex(PotentialStatRules.ChooseExtreme(target, other ?? target, c.Rules, r.Mode == "최고", c.Rng ?? (() => .5))) };
    else if (r.Mode == "임의") {
     var choose = c.Rules == null ? null : c.Rules.ChooseStat;
     if (choose == null) { c.Say(c.Source.Label + ": 임의의 능력은 사용자가 고른다 — 판정 대기"); return EffectOutcome.Pending; }
     string chosen = choose(c.Source, target, PotentialStatRules.Arbitrary);
     if (string.IsNullOrEmpty(chosen)) return EffectOutcome.Pending;
     stats = new List<int> { EffectModules.StatIndex(chosen) };
    }
    bool changed = false;
    var log = c.Log ?? new List<string>();
    foreach (int index in stats) {
     if (index >= 1 && index <= 5) changed |= Mechanics.ChangeStage(target, other ?? target, index, r.Amount, c.Rules, c.Source.Label, log);
     else if (index == 6 || index == 7 || index == 8) {
      int before = index == 6 ? target.AccuracyStage : index == 7 ? target.EvasionStage : target.CriticalStage;
      int after = index == 8 ? Math.Max(0, Math.Min(3, before + r.Amount)) : Math.Max(-6, Math.Min(6, before + r.Amount));
      if (after == before) continue;
      if (index == 6) target.AccuracyStage = after; else if (index == 7) target.EvasionStage = after; else target.CriticalStage = after;
      log.Add(target.Data.name + ": " + (index == 6 ? "명중" : index == 7 ? "회피" : "급소") + " " + (r.Amount > 0 ? "+" : "") + (after - before) + " (" + c.Source.Label + ")");
      if (index != 8) AnsweredTriggers.StageChanged(target, other, index, before, after, log.Add);
      changed = true;
     }
    }
    return changed ? EffectOutcome.Success : EffectOutcome.Failed;
   }

   static EffectOutcome RunScalar(EffectCall c) {
    int turns; string body = Duration(c.Line, out turns);
    ScalarEffect s; EffectModules.TryScalar(body, out s);
    if (s.Condition.Placeholder) { c.Say(c.Source.Label + ": 기술 조건의 타입이 비어 있다 — 판정 대기"); return EffectOutcome.Pending; }
    bool action = turns == 0 && ActionEvent(c.Event);
    var owner = c.Owner;
    if (owner == null || owner.HP <= 0) return EffectOutcome.Failed;
    PotentialModifiers.Add(owner, new ActiveModifier { Key = c.Source.Key, Text = body, Source = c.Source.Label, Turns = turns == 0 ? -1 : turns, Action = action });
    c.Say(owner.Data.name + ": " + body + (action ? " (이번 행동)" : turns > 0 ? " (" + turns + "T)" : " (필드를 떠날 때까지)"));
    return EffectOutcome.Success;
   }

   static EffectOutcome RunSpecies(EffectCall c) {
    SpeciesEffect s; EffectModules.TrySpecies(c.Line, out s);
    var target = Recipient(c, s.Subject);
    if (target == null || target.HP <= 0) return EffectOutcome.Failed;
    var b = target.Data.@base;
    if (target.Conditions.OriginalBase == null) target.Conditions.OriginalBase = (int[])b.Clone();
    int trainerFloor = -1; string trainerGrade = "";
    if (s.Operation == "트레이너") {
     var scope = EffectScope.Current;
     int side = target.FieldSide;
     var trainer = scope != null && scope.States != null && side >= 0 && side < scope.States.Length && scope.States[side] != null ? scope.States[side].Trainer : null;
     var aptitude = trainer == null ? null : s.Aptitude == "지시" ? trainer.command : s.Aptitude == "육성" ? trainer.training : s.Aptitude == "통솔" ? trainer.leadership : trainer.ability;
     try { trainerGrade = aptitude == null ? "" : (aptitude.rank ?? "").Trim(); trainerFloor = trainerGrade.Length == 0 ? -1 : SpeciesStatRanks.LabelRange(trainerGrade)[0]; } catch { trainerFloor = -1; }
     if (trainerFloor < 0) { c.Say(c.Source.Label + ": 트레이너 「" + s.Aptitude + "」 등급을 알 수 없다 — 판정 대기"); return EffectOutcome.Pending; }
    }
    bool changed = false;
    foreach (int i in s.Stats) {
     int current = b[i], next = current;
     int floor = s.Grade.Length > 0 ? GradeValue(c, target, i, current, s.Grade, s.Value) : s.Value;
     switch (s.Operation) {
      case "변경": next = floor; break;
      case "상승": next = Math.Max(current, floor); break;
      case "하락": next = Math.Min(current, floor); break;
      case "랭크": next = SpeciesStatRanks.RaisedRange(current, s.Ranks)[0]; break;
      case "수치": next = s.Value; break;
      case "최고 복사": next = PotentialStatRules.CopyBase(target.Data, EffectModules.StatIndex(PotentialStatRules.ChooseExtremeBase(target.Data, true, c.Rng ?? (() => .5)))); break;
      case "최저 복사": next = PotentialStatRules.CopyBase(target.Data, EffectModules.StatIndex(PotentialStatRules.ChooseExtremeBase(target.Data, false, c.Rng ?? (() => .5)))); break;
      case "복사": next = b[s.Source]; break;
      case "트레이너": next = Math.Max(current, trainerFloor < 0 ? current : GradeValue(c, target, i, current, trainerGrade, -1)); break;
     }
     if (next == current) continue;
     b[i] = next; changed = true;
     c.Say(target.Data.name + ": " + Engine.Names[i] + " 종족치 " + current + "→" + next + " (" + c.Source.Label + ")");
    }
    if (!changed && target.Conditions.OriginalBase != null && target.Conditions.OriginalBase.SequenceEqual(b)) target.Conditions.OriginalBase = null;
    if (changed && s.Stats.Contains(0)) target.HP = Math.Min(target.HP, Engine.Stats(target, (c.Opponent ?? target).Data.level, c.Rules ?? new Rules())[0]);
    return changed ? EffectOutcome.Success : EffectOutcome.Failed;
   }

   // 등급 문장의 값: 현재 값의 -·+를 유지한 범위(B-→A-, B+→A+, 무표기→A 범위). 지정 값이 범위 안이면 그 값, 아니면 선택 또는 최솟값
   static int GradeValue(EffectCall c, Fighter target, int stat, int current, string grade, int value) {
    var range = SpeciesStatRanks.GradeRange(current, grade);
    if (value >= range[0] && value <= range[1]) return value;
    if (range[0] == range[1]) return range[0];
    var choose = c.Rules == null ? null : c.Rules.ChooseSpeciesValue;
    int chosen = choose == null ? -1 : choose(c.Source, target, stat, range[0], range[1]);
    if (chosen >= range[0] && chosen <= range[1]) return chosen;
    c.Say(c.Source.Label + ": 「" + grade + "」 범위 " + range[0] + "~" + range[1] + " 중 최솟값 " + range[0] + "을 썼다(편집기에서 값을 지정할 수 있다)");
    return range[0];
   }

   static EffectOutcome RunFraction(EffectCall c) {
    FractionEffect f; EffectModules.TryFraction(c.Line, out f);
    var target = f.Target == "상대" ? c.Opponent : c.Owner;
    if (target == null) return EffectOutcome.Failed;
    var other = ReferenceEquals(target, c.Owner) ? c.Opponent ?? target : c.Owner;
    var rules = c.Rules ?? new Rules();
    var log = c.Log ?? new List<string>();
    int max = Mechanics.MaxHP(target, other, rules);
    int amount = FractionalHpRule.Amount(max, f.Denominator);
    if (f.Action == "회복") {
     if (target.HP <= 0) { c.Say(target.Data.name + ": 빈사라 회복할 수 없다"); return EffectOutcome.Failed; }
     return Mechanics.Heal(target, other, rules, amount, c.Source.Label, log) > 0 ? EffectOutcome.Success : EffectOutcome.Failed;
    }
    if (f.Action == "소비") {
     if (target.HP <= amount) { c.Say(target.Data.name + ": 체력이 부족해 " + amount + "을 소비할 수 없다"); return EffectOutcome.Failed; }
     int before = target.HP;
     target.HP -= amount;
     c.Say(target.Data.name + " — " + c.Source.Label + " 대가: HP -" + amount);
     AnsweredTriggers.HpChanged(target, other, rules, before, target.HP, c.Source.Label, log.Add);
     return EffectOutcome.Success;
    }
    if (target.HP <= 0) return EffectOutcome.Failed;
    if (Mechanics.Ability(target, "매직가드")) { c.Say(target.Data.name + ": 매직가드로 대미지를 받지 않는다"); return EffectOutcome.Failed; }
    if (!f.CanFaint) amount = Math.Min(amount, target.HP - 1);
    if (amount <= 0) return EffectOutcome.Failed;
    Mechanics.Hurt(target, other, rules, amount, c.Source.Label, log);
    return EffectOutcome.Success;
   }
  }
 }
}
