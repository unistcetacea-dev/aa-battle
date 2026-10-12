using System;
using System.Linq;
using System.Collections.Generic;
using NUnit.Framework;

namespace AABattle {
 // 효과 문장 → 상태변화 자동 연결(StateEffects)
 [TestFixture] public class StateEffectTests {
  static Fighter F(string type = "노말", params Potential[] potentials) {
   return new Fighter(new Pokemon {
    name = "STATE " + type, level = 100, types = new[] { type }, ability = "", item = "",
    @base = new[] { 100, 100, 100, 100, 100, 100 }, iv = new[] { 31, 31, 31, 31, 31, 31 },
    moves = new[] { "몸통박치기" }, potentials = potentials
   });
  }
  static Potential P(string trigger, string effect) {
   return new Potential { id = "종족 ①", name = "상태 검사", description = effect, triggers = new[] { trigger } };
  }
  static Move Idle() {
   return new Move { name = "미등록", category = "변화", types = new[] { "노말" }, tags = new string[0], power = 0, accuracy = 100, target = "자신" };
  }

  [TestCase("중확률로 상대를 2T간 「동상」상태로 한다", "상대", "동상", 2, false, .5)]
  [TestCase("「가열」상태가 된다", "자신", "가열", 0, false, -1)]
  [TestCase("T종료시까지 「도발」상태로 한다", "", "도발", 0, true, -1)]
  [TestCase("상대를 「독」 상태로 만든다", "상대", "독", 0, false, -1)]
  [TestCase("드믈게 상대를 「악인」상태로 한다", "상대", "악인", 0, false, .1)]
  [TestCase("3T동안 상대를 「조이기」상태로 한다", "상대", "조이기", 3, false, -1)]
  [TestCase("중확률로 2T간 「앵콜」상태로 한다", "", "앙코르", 2, false, .5)]
  [TestCase("2T간 자진을 「순풍」상태로 한다", "자신", "순풍", 2, false, -1)]
  public void ParsesStateSentence(string text, string target, string state, int turns, bool untilTurnEnd, double rate) {
   var s = StateEffects.Parse(text);
   Assert.That(s, Is.Not.Null, text);
   Assert.That(s.Target, Is.EqualTo(target));
   Assert.That(s.State, Is.EqualTo(state));
   Assert.That(s.Turns, Is.EqualTo(turns));
   Assert.That(s.UntilTurnEnd, Is.EqualTo(untilTurnEnd));
   Assert.That(s.Rate.HasValue ? s.Rate.Value : -1, Is.EqualTo(rate));
  }

  [TestCase("자신의 「공격」이 오른다")]
  [TestCase("「방」의 종족치가 「AA+」가 된다")]
  [TestCase("자신의 「얼음」기술이「효과가 굉장」이 된다")]
  [TestCase("기술폭이 「4」가 된다")]
  public void IgnoresNonStateSentence(string text) {
   Assert.That(StateEffects.Parse(text), Is.Null);
  }

  static IEnumerable<string> TableStates() { return StateEffects.Table.Select(x => x.Name); }

  // 표의 모든 상태가 실제로 걸리는지 확인한다. 표에 상태를 추가하면 이 검사도 자동으로 늘어난다
  [TestCaseSource(nameof(TableStates))]
  public void EveryTableStateApplies(string state) {
   var self = F(); var opponent = F(); var rules = new Rules(); var log = new List<string>();
   Mechanics.SetParties(rules, new[] { self, opponent });
   foreach (var f in new[] { self, opponent }) f.Conditions.LastMove = "몸통박치기";
   var rule = StateEffects.Find(state);
   string sentence = rule.SelfByDefault ? "자신을 「" + state + "」상태로 한다" : "상대를 「" + state + "」상태로 한다";
   var target = rule.SelfByDefault ? self : opponent;
   var before = string.Join("|", target.Conditions.Labels(target)) + target.Status + target.Conditions.Hibernate;
   Assert.That(StateEffects.TryApply(sentence, self, opponent, rules, () => .5, log, "검사"), Is.True, state);
   var after = string.Join("|", target.Conditions.Labels(target)) + target.Status + target.Conditions.Hibernate;
   Assert.That(after, Is.Not.EqualTo(before), state + " / " + string.Join(" / ", log));
  }

  [Test] public void ProbabilityAndImmunity() {
   var self = F(); var opponent = F(); var log = new List<string>();
   StateEffects.TryApply("중확률로 상대를 「혼란」상태로 한다", self, opponent, new Rules(), () => .9, log, "검사");
   Assert.That(opponent.Conditions.Confusion, Is.EqualTo(0));
   Assert.That(log.Last(), Does.Contain("확률 판정 실패"));
   StateEffects.TryApply("중확률로 상대를 「혼란」상태로 한다", self, opponent, new Rules(), () => .1, log, "검사");
   Assert.That(opponent.Conditions.Confusion, Is.GreaterThan(0));
   var fire = F("불꽃");
   StateEffects.TryApply("상대를 「화상」상태로 한다", self, fire, new Rules(), () => .5, log, "검사");
   Assert.That(fire.Status, Is.EqualTo("정상"), "fire type is immune to burn");
  }

  [Test] public void UnknownStateIsReportedNotApplied() {
   var log = new List<string>();
   Assert.That(StateEffects.TryApply("상대를 「텔레키네시스」상태로 한다", F(), F(), new Rules(), () => .5, log, "검사"), Is.False);
   Assert.That(log.Last(), Does.Contain("아직 자동 적용되지 않는다"));
  }

  [Test] public void PokemonPotentialAppliesStateAtTurnEnd() {
   var db = TestData.Db;
   var owner = F("노말", P(TriggerStructures.TurnEndLabel, "상대를 「혼란」상태로 한다."));
   var turn = Engine.Resolve(db, new[] { owner, F() }, new[] { Idle(), Idle() }, new Rules(), () => .5);
   Assert.That(turn.Fighters[1].Conditions.Confusion, Is.GreaterThan(0), string.Join("\n", turn.Log));
   Assert.That(turn.Log.Any(x => x.Contains("효과 적용")), Is.True);
  }

  [Test] public void PotentialWithExtraConditionWaitsForManualJudgement() {
   var db = TestData.Db;
   var potential = new Potential { id = "종족 ①", name = "조건부", description = "상대를 「혼란」상태로 한다.", triggers = new[] { TriggerStructures.TurnEndLabel, "체력이 절반 이하일 때" } };
   var turn = Engine.Resolve(db, new[] { F("노말", potential), F() }, new[] { Idle(), Idle() }, new Rules(), () => .5);
   Assert.That(turn.Fighters[1].Conditions.Confusion, Is.EqualTo(0));
   Assert.That(turn.Log.Any(x => x.Contains("효과 판정 대기")), Is.True);
  }

  [Test] public void OutsideBattleScopeOnlyLogs() {
   var owner = F("노말", P(TriggerStructures.TurnEndLabel, "「가열」상태가 된다."));
   var log = new List<string>();
   TriggerStructures.Fire(owner, TriggerStructures.TurnEndLabel, "검사", log.Add);
   Assert.That(owner.Conditions.Heated, Is.False);
   Assert.That(log.Any(x => x.Contains("효과 판정 대기")), Is.True);
  }

  [Test] public void FieldEffectAppliesStateToEveryPokemon() {
   var a = F(); var b = F(); var log = new List<string>();
   var rules = new Rules { FieldEffects = new[] { new FieldEffectRecord { name = "열기", trigger = TriggerStructures.TurnEndLabel, effect = "「가열」상태가 된다." } } };
   FieldEffectRuntime.Fire(rules, TriggerStructures.TurnEndLabel, new[] { a, b }, "검사", () => .5, log.Add);
   Assert.That(a.Conditions.Heated && b.Conditions.Heated, Is.True, string.Join("\n", log));
  }
 }
}
