using System;
using System.Linq;
using System.Collections.Generic;
using NUnit.Framework;

namespace AABattle {
 // 유대 포텐셜: 지시 랭크 상승 · 트레이너 포텐셜 재사용 권한 · 자신 대상 재발동
 [TestFixture] public class BondPotentialTests {
  static int ids;
  const string Effect = "검사 유대 효과";
  const string Regain = "필드에 있는 한 트레이너의 지시를 「1」랭크 올린다.\n저확률로 포텐셜을 다시 받을 수 있다.";
  const string Replay = "필드에 있는 한 트레이너의 지시를 「1」랭크 올린다.\n저확률로 자신을 대상으로 포텐셜을 다시 발동한다.";
  static string TurnStart { get { return TriggerStructures.TurnStartLabel; } }
  static string TurnEnd { get { return TriggerStructures.TurnEndLabel; } }

  static Fighter F(params Potential[] potentials) {
   return new Fighter(new Pokemon {
    id = "pk-b" + (++ids), name = "B" + ids, level = 100, types = new[] { "노말" }, ability = "", item = "",
    @base = new[] { 100, 100, 100, 100, 100, 100 }, iv = new[] { 31, 31, 31, 31, 31, 31 },
    moves = new[] { "몸통박치기" }, potentials = potentials
   });
  }
  static Potential Bond(string name, string effect) {
   return new Potential { id = "유대", name = name, description = effect, activation = "자동", triggers = new[] { TurnStart } };
  }
  static TrainerRecord Trainer(params string[] uses) {
   var trainer = new TrainerRecord { id = "tr-bond", name = "트레이너" };
   for (int i = 0; i < uses.Length; i++)
    trainer.potentials.Add(new PotentialRecord { slot = "고유", name = "고유" + i, trigger = TurnEnd, effect = Effect, uses = uses[i] });
   return trainer;
  }
  static TrainerBattleState[] States(TrainerRecord trainer) {
   return new[] { new TrainerBattleState { Trainer = trainer }, new TrainerBattleState() };
  }

  List<PotentialExecutor> registered = new List<PotentialExecutor>();
  List<Fighter> targets = new List<Fighter>();
  [SetUp] public void Register() {
   targets.Clear();
   registered.Add(PotentialExecutors.Register("검사 유대", c => c.Line.StartsWith(Effect), c => { targets.Add(c.Owner); return EffectOutcome.Success; }));
  }
  [TearDown] public void Unregister() { foreach (var x in registered) PotentialExecutors.Unregister(x); registered.Clear(); }

  static List<string> Raise(Fighter[] fighters, TrainerBattleState[] states, Rules rules, Func<double> rng, params PotentialEvent[] events) {
   var log = new List<string>();
   foreach (var e in events) e.Log = log.Add;
   using (EffectScope.Begin(fighters, rules, rng, states)) PotentialQueue.Raise(events);
   return log;
  }
  static PotentialEvent Trainer(TrainerRecord trainer, Fighter owner) {
   return new PotentialEvent { Trainer = trainer, Owner = owner, Label = TurnEnd };
  }
  static PotentialEvent Start(Fighter owner) {
   return TriggerStructures.LabelEvent(owner, TurnStart, owner.Data.name + " · " + TurnStart, null);
  }
  static Rules Choosing(List<string[]> shown, int pick) {
   return new Rules { ChooseBondPotential = (f, names) => { shown.Add(names); return pick; } };
  }

  [TestCase("필드에 있을 때 트레이너의 「지시」를 1랭크 올린다", "필드에 있는 한 트레이너의 지시를 「1」랭크 올린다.", BondModules.KindRise)]
  [TestCase("필드에 있는 한, 트레이너의 지시를 「2」랭크 올린다.", "필드에 있는 한 트레이너의 지시를 「2」랭크 올린다.", BondModules.KindRise)]
  [TestCase("저확률로 포텐셜을 다시 받을 수 있다", "저확률로 포텐셜을 다시 받을 수 있다.", BondModules.KindRegain)]
  [TestCase("저확률로 자신을 대상으로 포텐셜을 다시 발동한다.", "저확률로 자신을 대상으로 포텐셜을 다시 발동한다.", BondModules.KindReplay)]
  public void SentencesNormalizeAndClassify(string raw, string expected, string kind) {
   Assert.That(EffectNormalizer.Normalize(raw), Is.EqualTo(expected));
   Assert.That(EffectModules.Classify(expected), Is.EqualTo(kind));
  }

  [Test] public void TrainerPotentialUseIsRecordedForTheReceiver() {
   var trainer = Trainer("1/시");
   var a = F(); var states = States(trainer);
   Raise(new[] { a, F() }, states, new Rules(), () => .5, Trainer(trainer, a));
   Assert.That(targets, Is.EqualTo(new[] { a }));
   Assert.That(BondModules.Own(states[0], a).Select(x => x.Name), Is.EqualTo(new[] { "고유0" }));
   Assert.That(BondModules.Own(states[0], F()).Count, Is.EqualTo(0));
   Assert.That(states[0].Copy().Usage.Bond.Count, Is.EqualTo(1), "이력은 상태 복사(턴·교대)를 넘어 유지된다");
  }

  [TestCase(.1, true)]
  [TestCase(.9, false)]
  public void RegainRestoresOneUseOnlyWhenTheLowProbabilityRollSucceeds(double roll, bool regained) {
   var trainer = Trainer("1/시");
   var bond = F(Bond("영혼의 유대", Regain)); var states = States(trainer); var shown = new List<string[]>();
   Raise(new[] { bond, F() }, states, new Rules(), () => .5, Trainer(trainer, bond));
   Raise(new[] { bond, F() }, states, new Rules(), () => .5, Trainer(trainer, bond));
   Assert.That(targets.Count, Is.EqualTo(1), "1/시는 한 번만");
   var log = Raise(new[] { bond, F() }, states, Choosing(shown, 0), () => roll, Start(bond));
   Assert.That(shown.Count, Is.EqualTo(regained ? 1 : 0), string.Join("\n", log));
   Assert.That(states[0].Usage.Battle.ContainsKey(PotentialKeys.For(trainer, trainer.potentials[0])), Is.Not.EqualTo(regained));
   Raise(new[] { bond, F() }, states, new Rules(), () => .5, Trainer(trainer, bond));
   Assert.That(targets.Count, Is.EqualTo(regained ? 2 : 1), "권한을 받으면 그 뒤 평소처럼 다시 쓸 수 있다");
  }

  [Test] public void RegainNeedsHistoryAndListsOnlyLimitedSpentPotentials() {
   var trainer = Trainer("1/시", "");
   var bond = F(Bond("영혼의 유대", Regain)); var states = States(trainer); var shown = new List<string[]>();
   var log = Raise(new[] { bond, F() }, states, Choosing(shown, 0), () => .1, Start(bond));
   Assert.That(shown, Is.Empty, "이력 없음 → 발동하지 않는다");
   Assert.That(log.Any(x => x.Contains("다시 받을 트레이너 포텐셜이 없다")), Is.True);
   Raise(new[] { bond, F() }, states, new Rules(), () => .5, Trainer(trainer, bond));
   Raise(new[] { bond, F() }, states, Choosing(shown, 0), () => .1, Start(bond));
   Assert.That(shown.Single().Length, Is.EqualTo(1), "횟수 제한이 없는 포텐셜은 목록에서 뺀다");
   Assert.That(shown.Single()[0], Does.Contain("고유0"));
  }

  [Test] public void CancelingTheListChangesNothing() {
   var trainer = Trainer("1/시");
   var bond = F(Bond("영혼의 유대", Regain)); var states = States(trainer);
   Raise(new[] { bond, F() }, states, new Rules(), () => .5, Trainer(trainer, bond));
   var before = states[0].Usage.Battle.Count;
   var log = Raise(new[] { bond, F() }, states, Choosing(new List<string[]>(), -1), () => .1, Start(bond));
   Assert.That(states[0].Usage.Battle.Count, Is.EqualTo(before));
   Assert.That(log.Any(x => x.Contains("사용하지 않았다")), Is.True);
  }

  [Test] public void ReplayRunsTheChosenPotentialOnTheBondPokemonWithoutSpendingUses() {
   var trainer = Trainer("1/시");
   var owner = F(); var bond = F(Bond("왕의 유대", Replay)); var states = States(trainer); var shown = new List<string[]>();
   Raise(new[] { owner, F() }, states, new Rules(), () => .5, Trainer(trainer, owner));
   int used = states[0].Usage.Battle.Values.Sum();
   var log = Raise(new[] { bond, F() }, states, Choosing(shown, 0), () => .1, Start(bond));
   Assert.That(targets, Is.EqualTo(new[] { owner, bond }), "재발동의 대상은 유대 포켓몬 자신\n" + string.Join("\n", log));
   Assert.That(states[0].Usage.Battle.Values.Sum(), Is.EqualTo(used), "횟수를 소모하지 않는다");
   Assert.That(shown.Count, Is.EqualTo(1));
   Assert.That(BondModules.Own(states[0], bond).Count, Is.EqualTo(1), "받은 포텐셜도 이력에 남는다");
  }

  [Test] public void PotentialReceivedOnlyByATeammateIsInTheKingListNotTheSoulList() {
   var trainer = Trainer("1/시");
   var mate = F(); var king = F(Bond("왕의 유대", Replay)); var soul = F(Bond("영혼의 유대", Regain));
   var states = States(trainer); var kingList = new List<string[]>(); var soulList = new List<string[]>();
   Raise(new[] { mate, F() }, states, new Rules(), () => .5, Trainer(trainer, mate));
   Raise(new[] { king, F() }, states, Choosing(kingList, -1), () => .1, Start(king));
   Raise(new[] { soul, F() }, states, Choosing(soulList, -1), () => .1, Start(soul));
   Assert.That(kingList.Single().Length, Is.EqualTo(1));
   Assert.That(soulList, Is.Empty);
  }

  [Test] public void HistorySurvivesSwitchingOut() {
   var trainer = Trainer("1/시");
   var bond = F(Bond("영혼의 유대", Regain)); var other = F(); var states = States(trainer); var shown = new List<string[]>();
   Raise(new[] { bond, F() }, states, new Rules(), () => .5, Trainer(trainer, bond));
   var next = new[] { states[0].Copy(), states[1].Copy() };
   Raise(new[] { other, F() }, next, new Rules(), () => .5);
   Raise(new[] { bond, F() }, next, Choosing(shown, 0), () => .1, Start(bond));
   Assert.That(shown.Count, Is.EqualTo(1));
  }

  [TestCase("A", "A", 1, 2)]
  [TestCase("A", "AA", 1, 3)]
  [TestCase("B", "B", 0, 1)]
  public void BondRaisesTheCommandRankWhileOnTheField(string rank, string _, int without, int with) {
   var trainer = new TrainerRecord(); trainer.command.rank = rank;
   var plain = F(); var bond = F(Bond("영혼의 유대", Regain));
   TrainerAccessRules.ApplyTrainerPassives(plain, trainer, null);
   TrainerAccessRules.ApplyTrainerPassives(bond, trainer, null);
   Assert.That(plain.CriticalStage, Is.EqualTo(rank == "B" ? 0 : 1));
   Assert.That(bond.CriticalStage, Is.EqualTo(rank == "B" ? 1 : 2));
  }

  [Test] public void BondOpensGatesOfTheNextCommandBand() {
   var trainer = new TrainerRecord(); trainer.command.rank = "A";
   var bond = F(Bond("왕의 유대", Replay));
   Assert.That(TrainerAccessRules.CanUseForm("맞춰라！", trainer, null, new Potential[0]), Is.False);
   Assert.That(TrainerAccessRules.CanUseForm("맞춰라！", trainer, null, bond.Data.potentials), Is.True);
  }

  [Test] public void BondIsJudgedRightAfterOrdersAndBeforeMoves() {
   var trainer = Trainer("1/시");
   var bond = F(Bond("영혼의 유대", Regain)); var foe = F();
   var states = States(trainer);
   var move = TestData.Db.moves.First(x => x.name == "몸통박치기");
   var turn = Engine.Resolve(TestData.Db, new[] { bond, foe }, new[] { new BattleCommand { Move = move, Order = "공격 지령" }, new BattleCommand { Move = move } }, new Rules(), states, () => .1);
   int order = turn.Log.FindIndex(x => x.Contains("지령!"));
   int start = turn.Log.FindIndex(x => x.Contains("영혼의 유대") && x.Contains(TurnStart));
   int hit = turn.Log.FindIndex(x => x.StartsWith(bond.Data.name + "의 몸통박치기!"));
   Assert.That(order, Is.GreaterThanOrEqualTo(0), string.Join("\n", turn.Log));
   Assert.That(start, Is.GreaterThan(order).And.LessThan(hit), string.Join("\n", turn.Log));
  }
 }
}
