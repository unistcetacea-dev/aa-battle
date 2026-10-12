using System;
using System.Linq;
using System.Collections.Generic;
using NUnit.Framework;

namespace AABattle {
 // 지시서 F1: 포텐셜 공통 효과 큐(발동 순서·중복·사용 횟수·확률·선택·연쇄·종속·연속 타격)
 [TestFixture] public class PotentialQueueTests {
  static int ids;
  static Fighter F(int speed, params Potential[] potentials) {
   return new Fighter(new Pokemon {
    id = "pk-q" + (++ids), name = "Q" + ids, level = 100, types = new[] { "노말" }, ability = "", item = "",
    @base = new[] { 100, 100, 100, 100, 100, speed }, iv = new[] { 31, 31, 31, 31, 31, 31 },
    moves = new[] { "몸통박치기" }, potentials = potentials
   });
  }
  static Potential P(string slot, string name, string trigger, string effect, string uses = "") {
   return new Potential { id = slot, name = name, description = effect, uses = uses, triggers = new[] { trigger } };
  }
  static string TurnEnd { get { return TriggerStructures.TurnEndLabel; } }

  // 검사용 실행기: "검사 성공"·"검사 실패"·"검사 연쇄"(능력 상승 사건을 낸다)·"체력을 소비해"(대가)
  List<PotentialExecutor> registered = new List<PotentialExecutor>();
  public static EffectOutcome CostOutcome = EffectOutcome.Success;
  public static int Runs;
  [SetUp] public void Register() {
   Runs = 0; CostOutcome = EffectOutcome.Success;
   registered.Add(PotentialExecutors.Register("검사 성공", c => c.Line.StartsWith("검사 성공"), c => { Runs++; return EffectOutcome.Success; }));
   registered.Add(PotentialExecutors.Register("검사 실패", c => c.Line.StartsWith("검사 실패"), c => { Runs++; return EffectOutcome.Failed; }));
   registered.Add(PotentialExecutors.Register("검사 연쇄", c => c.Line.StartsWith("검사 연쇄"), c => { Runs++; AnsweredTriggers.StageChanged(c.Owner, c.Opponent, 1, 0, 1, c.Say); return EffectOutcome.Success; }));
   registered.Add(PotentialExecutors.Register("검사 대가", c => c.Line.StartsWith("체력을 소비"), c => CostOutcome));
  }
  [TearDown] public void Unregister() { foreach (var x in registered) PotentialExecutors.Unregister(x); registered.Clear(); PotentialQueue.ChooseTarget = (s, list) => list.FirstOrDefault(); }

  static List<string> Run(Fighter[] fighters, Func<double> rng, TrainerBattleState[] states, params PotentialEvent[] events) {
   var log = new List<string>();
   foreach (var e in events) e.Log = log.Add;
   using (EffectScope.Begin(fighters, new Rules(), rng, states ?? new[] { new TrainerBattleState(), new TrainerBattleState() })) PotentialQueue.Raise(events);
   return log;
  }
  static PotentialEvent At(Fighter owner, string label) { return TriggerStructures.LabelEvent(owner, label, owner.Data.name + " · " + label, null); }
  static int Index(List<string> log, string name) { return log.FindIndex(x => x.Contains("『" + name + "』") && x.Contains("효과 적용")); }

  [Test] public void FasterOwnerFiresFirst() {
   var slow = F(50, P("종족 ①", "느림", TurnEnd, "검사 성공"));
   var fast = F(150, P("종족 ①", "빠름", TurnEnd, "검사 성공"));
   var log = Run(new[] { slow, fast }, () => .5, null, At(slow, TurnEnd), At(fast, TurnEnd));
   Assert.That(Index(log, "빠름"), Is.LessThan(Index(log, "느림")), string.Join("\n", log));
  }

  [TestCase(.1, .9, "A")]
  [TestCase(.9, .1, "B")]
  public void SpeedTieUsesRng(double first, double second, string winner) {
   var a = F(100, P("종족 ①", "A", TurnEnd, "검사 성공"));
   var b = F(100, P("종족 ①", "B", TurnEnd, "검사 성공"));
   var rolls = new Queue<double>(new[] { first, second });
   var log = Run(new[] { a, b }, () => rolls.Count > 0 ? rolls.Dequeue() : .5, null, At(a, TurnEnd), At(b, TurnEnd));
   string loser = winner == "A" ? "B" : "A";
   Assert.That(Index(log, winner), Is.LessThan(Index(log, loser)));
  }

  [Test] public void SameNameDifferentPotentialsBothFire() {
   var owner = F(100, P("종족 ①", "같은 이름", TurnEnd, "검사 성공"), P("종족 ②", "같은 이름", TurnEnd, "검사 성공"));
   Run(new[] { owner, F(100) }, () => .5, null, At(owner, TurnEnd));
   Assert.That(Runs, Is.EqualTo(2));
  }

  [Test] public void SameInstanceOncePerEvent() {
   var potential = new Potential { id = "종족 ①", name = "두 절", description = "검사 성공", triggers = new[] { "T종료시", "턴 종료시" } };
   var owner = F(100, potential);
   Run(new[] { owner, F(100) }, () => .5, null, At(owner, TurnEnd));
   Assert.That(Runs, Is.EqualTo(1));
  }

  [Test] public void ProbabilityFailureDoesNotConsume() {
   var owner = F(100, P("종족 ①", "확률", TurnEnd, "중확률로 검사 성공", "1/시"));
   var states = new[] { new TrainerBattleState(), new TrainerBattleState() };
   var log = Run(new[] { owner, F(100) }, () => .9, states, At(owner, TurnEnd));
   Assert.That(log.Any(x => x.Contains("확률 판정 실패")), Is.True);
   Assert.That(states[0].Usage.Battle.Count, Is.EqualTo(0));
   Run(new[] { owner, F(100) }, () => .1, states, At(owner, TurnEnd));
   Assert.That(Runs, Is.EqualTo(1));
   Assert.That(states[0].Usage.Battle.Count, Is.EqualTo(1));
  }

  [Test] public void RefusedOptionalEffectDoesNotConsume() {
   var owner = F(100, P("종족 ①", "선택", TurnEnd, "검사 성공할 수 있다", "1/시"));
   var states = new[] { new TrainerBattleState(), new TrainerBattleState() };
   var log = new List<string>();
   var rules = new Rules { OptionalEffectPrompt = (a, b, p) => false };
   var e = At(owner, TurnEnd); e.Log = log.Add;
   using (EffectScope.Begin(new[] { owner, F(100) }, rules, () => .5, states)) PotentialQueue.Raise(e);
   Assert.That(Runs, Is.EqualTo(0));
   Assert.That(states[0].Usage.Battle.Count, Is.EqualTo(0));
   Assert.That(log.Any(x => x.Contains("사용하지 않았다")), Is.True);
  }

  [Test] public void FailedEffectStillConsumes() {
   var owner = F(100, P("종족 ①", "실패", TurnEnd, "검사 실패", "1/시"));
   var states = new[] { new TrainerBattleState(), new TrainerBattleState() };
   Run(new[] { owner, F(100) }, () => .5, states, At(owner, TurnEnd));
   var log = Run(new[] { owner, F(100) }, () => .5, states, At(owner, TurnEnd));
   Assert.That(Runs, Is.EqualTo(1));
   Assert.That(log.Any(x => x.Contains("사용 횟수")), Is.True);
  }

  [TestCase("1/T", 2)]
  [TestCase("1/시", 1)]
  public void TurnLimitResetsBattleLimitDoesNot(string uses, int expected) {
   var owner = F(100, P("종족 ①", "제한", TurnEnd, "검사 성공", uses));
   var states = new[] { new TrainerBattleState(), new TrainerBattleState() };
   Run(new[] { owner, F(100) }, () => .5, states, At(owner, TurnEnd));
   Run(new[] { owner, F(100) }, () => .5, states, At(owner, TurnEnd));
   foreach (var s in states) s.Usage.EndTurn();
   Run(new[] { owner, F(100) }, () => .5, states, At(owner, TurnEnd));
   Assert.That(Runs, Is.EqualTo(expected));
  }

  [Test] public void ChainRaisesNewEventOncePerPotential() {
   var owner = F(100, P("종족 ①", "A", TurnEnd, "검사 연쇄"), P("종족 ②", "B", "자신의 능력이 올랐을 때", "검사 연쇄"));
   var log = Run(new[] { owner, F(100) }, () => .5, null, At(owner, TurnEnd));
   Assert.That(Runs, Is.EqualTo(2), string.Join("\n", log));
   Assert.That(log.Any(x => x.Contains("『B』") && x.Contains("같은 연쇄")), Is.True, "B does not refire on its own rise");
  }

  [Test] public void ChainStopsAfterDepthEight() {
   var potentials = Enumerable.Range(1, 12).Select(i => P("종족 " + i, "연쇄" + i, "자신의 능력이 올랐을 때", "검사 연쇄")).ToArray();
   var owner = F(100, potentials);
   var log = new List<string>();
   using (EffectScope.Begin(new[] { owner, F(100) }, new Rules(), () => .5, new[] { new TrainerBattleState(), new TrainerBattleState() }))
    AnsweredTriggers.StageChanged(owner, null, 1, 0, 1, log.Add);
   Assert.That(log.Any(x => x.Contains("8단계를 넘어 중단")), Is.True, string.Join("\n", log));
  }

  [TestCase(EffectOutcome.Failed, false)]
  [TestCase(EffectOutcome.Success, true)]
  public void CostFailureBlocksFollowingEffect(EffectOutcome cost, bool heated) {
   CostOutcome = cost;
   var owner = F(100, P("종족 ①", "대가", TurnEnd, "체력을 소비해 「가열」상태가 된다."));
   var log = Run(new[] { owner, F(100) }, () => .5, null, At(owner, TurnEnd));
   Assert.That(owner.Conditions.Heated, Is.EqualTo(heated), string.Join("\n", log));
  }

  [TestCase("검사 실패", false)]
  [TestCase("검사 성공", true)]
  public void FollowUpNeedsEarlierSuccess(string first, bool heated) {
   var owner = F(100, P("종족 ①", "이때", TurnEnd, first + ". 이때 「가열」상태가 된다."));
   var log = Run(new[] { owner, F(100) }, () => .5, null, At(owner, TurnEnd));
   Assert.That(owner.Conditions.Heated, Is.EqualTo(heated), string.Join("\n", log));
  }

  [Test] public void IndependentEffectsRunDespiteFailure() {
   var owner = F(100, P("종족 ①", "독립", TurnEnd, "검사 실패. 「가열」상태가 된다."));
   Run(new[] { owner, F(100) }, () => .5, null, At(owner, TurnEnd));
   Assert.That(owner.Conditions.Heated, Is.True);
  }

  [Test] public void MultiHitRaisesHitEventPerHit() {
   var attacker = F(100, P("종족 ①", "명중", TriggerStructures.AttackSuccessLabel, "검사 성공"));
   var hit = new Move { name = "연속검사", category = "물리", types = new[] { "노말" }, tags = new[] { "연속" }, power = 10, accuracy = 100, attack = "공격", defense = "방어", priority = 2, effect = "3회 「연속」 기술．" };
   var idle = new Move { name = "미등록", category = "변화", types = new[] { "노말" }, tags = new string[0], accuracy = 100, target = "자신" };
   Engine.Resolve(TestData.Db, new[] { attacker, F(100) }, new[] { hit, idle }, new Rules(), () => .5);
   Assert.That(Runs, Is.EqualTo(3));
  }

  [Test] public void LimitedStatePotentialFiresOncePerBattle() {
   var owner = F(100, P("종족 ①", "혼란", TurnEnd, "상대를 「혼란」상태로 한다.", "1/시"));
   var idle = new Move { name = "미등록", category = "변화", types = new[] { "노말" }, tags = new string[0], accuracy = 100, target = "자신" };
   var first = Engine.Resolve(TestData.Db, new[] { owner, F(100) }, new[] { idle, idle }, new Rules(), () => .5);
   Assert.That(first.Fighters[1].Conditions.Confusion, Is.GreaterThan(0));
   var second = Engine.Resolve(TestData.Db, first.Fighters, new[] { new BattleCommand { Move = idle }, new BattleCommand { Move = idle } }, first.Rules, first.Trainers, () => .5);
   Assert.That(second.Log.Any(x => x.Contains("『혼란』") && x.Contains("사용 횟수")), Is.True, string.Join("\n", second.Log));
  }

  [Test] public void UndeclaredPreInputDoesNotFire() {
   var potential = P("종족 ①", "선행", TurnEnd, "검사 성공"); potential.activation = "선행";
   var owner = F(100, potential);
   var log = Run(new[] { owner, F(100) }, () => .5, null, At(owner, TurnEnd));
   Assert.That(Runs, Is.EqualTo(0));
   Assert.That(log.Any(x => x.Contains("선행입력을 선언하지 않아")), Is.True);
  }

  [Test] public void TargetsUseFieldUnlessBenchNamed() {
   var owner = F(100); var opponent = F(100); var reserve = F(100); reserve.OnField = false;
   var rules = new Rules();
   Mechanics.SetParties(rules, new[] { owner, opponent }, new IEnumerable<Fighter>[] { new[] { owner, reserve }, new[] { opponent } });
   using (EffectScope.Begin(new[] { owner, opponent }, rules, () => .5)) {
    var call = new EffectCall { Source = new PotentialInstance { Owner = owner }, Event = new PotentialEvent { Owner = owner, Other = opponent }, Rules = rules, Rng = () => .5 };
    Assert.That(PotentialQueue.Targets(call, "아군"), Is.EqualTo(new[] { owner }));
    Assert.That(PotentialQueue.Targets(call, "상대"), Is.EqualTo(new[] { opponent }));
    Assert.That(PotentialQueue.Targets(call, "대기 중인 아군").Count, Is.EqualTo(2));
    PotentialQueue.ChooseTarget = (s, list) => list.Last();
    Assert.That(PotentialQueue.Targets(call, "임의의 대기 아군"), Is.EqualTo(new[] { reserve }));
   }
  }
 }
}
