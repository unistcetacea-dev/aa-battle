using System;
using System.Linq;
using System.Collections.Generic;
using NUnit.Framework;

namespace AABattle {
 // F2 랭크·배율·종족치, F3 체력·상태이상 치유 실행기
 [TestFixture] public class PotentialEffectTests {
  static int ids;
  static Fighter F(string type = "노말", params Potential[] potentials) {
   return new Fighter(new Pokemon {
    id = "pk-e" + (++ids), name = "E" + ids, level = 100, types = new[] { type }, ability = "", item = "",
    @base = new[] { 100, 100, 100, 100, 100, 100 }, iv = new[] { 31, 31, 31, 31, 31, 31 },
    moves = new[] { "몸통박치기" }, potentials = potentials
   });
  }
  static Potential P(string trigger, string effect, string slot = "종족 ①") { return new Potential { id = slot, name = "검사 " + slot, description = effect, triggers = new[] { trigger } }; }
  static Move Mv(string type, string category = "물리", int power = 80) { return new Move { name = type + "검사", category = category, types = new[] { type }, tags = new string[0], power = power, accuracy = 100, attack = category == "물리" ? "공격" : "특공", defense = category == "물리" ? "방어" : "특방" }; }
  static int Max(Fighter a, Fighter b, Move m, Rules r = null) { return Engine.Calculate(TestData.Db, a, b, m, r ?? new Rules()).Max; }
  static string Field { get { return TriggerStructures.FieldLabel; } }
  static List<string> Fire(Fighter owner, Fighter other, string label, TrainerBattleState[] states = null) {
   var log = new List<string>();
   var rules = new Rules();
   Mechanics.SetParties(rules, new[] { owner, other });
   using (EffectScope.Begin(new[] { owner, other }, rules, () => .5, states ?? new[] { new TrainerBattleState(), new TrainerBattleState() }))
    PotentialQueue.Raise(TriggerStructures.LabelEvent(owner, label, owner.Data.name + " · " + label, log.Add));
   return log;
  }

  // ===== F2 계속 적용되는 배율 =====
  [Test] public void ContinuousPowerBoostAppliesOnlyToMatchingMoves() {
   var plain = F(); var boosted = F("노말", P(Field, "자신의 「불꽃」 기술의 위력을 강화(1.5배)한다.")); var target = F();
   Assert.That(Max(boosted, target, Mv("불꽃")), Is.GreaterThan(Max(plain, target, Mv("불꽃")) * 1.4));
   Assert.That(Max(boosted, target, Mv("물")), Is.EqualTo(Max(plain, target, Mv("물"))));
  }

  [Test] public void CollectedReceivedDamageHalvingWorksAndCriticalIgnoresIt() {
   var attacker = F(); var plain = F(); var guard = F("노말", P(Field, "「불꽃」기술로 받는 데미지를 반감한다"));
   int normal = Max(attacker, plain, Mv("불꽃")), halved = Max(attacker, guard, Mv("불꽃"));
   Assert.That(halved, Is.InRange(normal / 2 - 2, normal / 2 + 2));
   var crit = new Rules { Critical = true };
   Assert.That(Max(attacker, guard, Mv("불꽃"), crit), Is.EqualTo(Max(attacker, plain, Mv("불꽃"), crit)), "critical ignores defender protection");
  }

  [Test] public void OpponentTypeConditionGatesResistance() {
   var shield = new Potential { id = "내성", name = "대염내성", description = "상대의 「불꽃」 기술의 대미지를 완화(0.67배)한다.", triggers = new[] { "적진에 「불꽃」 포켓몬이 있을 때" } };
   var defender = F("노말", shield); var plain = F();
   var fireUser = F("불꽃"); var normalUser = F("노말");
   Assert.That(Max(fireUser, defender, Mv("불꽃")), Is.LessThan(Max(fireUser, plain, Mv("불꽃"))), "fire-type opponent present");
   Assert.That(Max(normalUser, defender, Mv("불꽃")), Is.EqualTo(Max(normalUser, plain, Mv("불꽃"))), "no fire-type opponent");
  }

  [Test] public void ContinuousStatBoostAndEffectivenessCondition() {
   var strong = F("노말", P(Field, "자신의 「공격」을 강화(1.5배)한다.")); var plain = F(); var target = F();
   Assert.That(Engine.Effective(strong, target, 1, new Rules()), Is.EqualTo((int)Math.Floor(Engine.Effective(plain, target, 1, new Rules()) * 1.5)));
   var hunter = F("노말", P(Field, "자신의 「효과가 굉장」 기술의 위력을 강화(2배)한다."));
   var grass = F("풀");
   Assert.That(Max(hunter, grass, Mv("불꽃")), Is.GreaterThan(Max(plain, grass, Mv("불꽃")) * 1.9));
   Assert.That(Max(hunter, target, Mv("불꽃")), Is.EqualTo(Max(plain, target, Mv("불꽃"))));
  }

  // ===== F2 사건 배율 =====
  [Test] public void EventMultiplierLastsUntilLeavingField() {
   var owner = F("노말", P(TriggerStructures.TurnEndLabel, "자신의 「공격」을 강화(2배)한다.")); var other = F();
   int before = Engine.Effective(owner, other, 1, new Rules());
   Fire(owner, other, TriggerStructures.TurnEndLabel);
   Assert.That(Engine.Effective(owner, other, 1, new Rules()), Is.EqualTo(before * 2));
   Mechanics.EndTurn(new[] { owner, other }, new Rules(), new List<string>());
   Assert.That(owner.Conditions.Modifiers.Count, Is.EqualTo(1), "no duration: stays");
   Mechanics.LeaveField(owner, new List<string>());
   Assert.That(owner.Conditions.Modifiers.Count, Is.EqualTo(0));
  }

  [Test] public void TimedEventMultiplierExpiresAtTurnEnd() {
   var owner = F("노말", P(TriggerStructures.TurnEndLabel, "T종료시까지 자신의 「방/특방」을 2배로 한다")); var other = F();
   Fire(owner, other, TriggerStructures.TurnEndLabel);
   Assert.That(owner.Conditions.Modifiers.Single().Turns, Is.EqualTo(1));
   Mechanics.EndTurn(new[] { owner, other }, new Rules(), new List<string>());
   Assert.That(owner.Conditions.Modifiers, Is.Empty);
  }

  // ===== F2 능력 랭크 =====
  [Test] public void RankEffectsFromCollectedWording() {
   var owner = F("노말", P(TriggerStructures.TurnEndLabel, "자신의 「공격」을 크게 올린다", "종족 ①"), P(TriggerStructures.TurnEndLabel, "상대의 명중을 내린다.", "종족 ②"));
   var other = F();
   Fire(owner, other, TriggerStructures.TurnEndLabel);
   Assert.That(owner.Stages[1], Is.EqualTo(2));
   Assert.That(other.AccuracyStage, Is.EqualTo(-1));
  }

  [Test] public void ArbitraryStatWaitsForChoice() {
   var owner = F("노말", P(TriggerStructures.TurnEndLabel, "자신의 임의의 능력이 오른다.")); var other = F();
   var log = Fire(owner, other, TriggerStructures.TurnEndLabel);
   Assert.That(owner.Stages.Sum(), Is.EqualTo(0));
   Assert.That(log.Any(x => x.Contains("판정 대기")), Is.True);
   try {
    PotentialQueue.ChooseStat = (s, f, list) => "속도";
    Fire(owner, other, TriggerStructures.TurnEndLabel);
    Assert.That(owner.Stages[5], Is.EqualTo(1));
   } finally { PotentialQueue.ChooseStat = null; }
  }

  // ===== F2 종족치 =====
  [Test] public void SpeciesGradeChangeRevertsOnLeave() {
   var owner = F("노말", P(TriggerStructures.TurnEndLabel, "자신의 「공격」의 종족치를 「AA」로 한다")); var other = F();
   Fire(owner, other, TriggerStructures.TurnEndLabel);
   Assert.That(owner.Data.@base[1], Is.EqualTo(SpeciesStatRanks.LabelRange("AA")[0]));
   Mechanics.LeaveField(owner, new List<string>());
   Assert.That(owner.Data.@base[1], Is.EqualTo(100));
  }

  [Test] public void SpeciesRaiseToTrainerAptitude() {
   var owner = F("노말", P(TriggerStructures.TurnEndLabel, "자신의 「방어」의 종족치를 트레이너의 「육성」과 같은 랭크까지 올린다")); var other = F();
   var trainer = new TrainerRecord(); trainer.training.rank = "A";
   Fire(owner, other, TriggerStructures.TurnEndLabel, new[] { new TrainerBattleState { Trainer = trainer }, new TrainerBattleState() });
   Assert.That(owner.Data.@base[2], Is.EqualTo(SpeciesStatRanks.LabelRange("A")[0]));
  }

  // ===== F3 체력·상태이상 =====
  [Test] public void HealDamageAndCost() {
   var healer = F("노말", P(TriggerStructures.TurnEndLabel, "최대 체력의 1/4를 회복한다")); var other = F();
   int max = Mechanics.MaxHP(healer, other, new Rules());
   healer.HP = 10;
   Fire(healer, other, TriggerStructures.TurnEndLabel);
   Assert.That(healer.HP, Is.EqualTo(10 + max / 4));
   var hitter = F("노말", P(TriggerStructures.TurnEndLabel, "상대의 체력을 1/4감소시킨다.*1")); var victim = F();
   Fire(hitter, victim, TriggerStructures.TurnEndLabel);
   Assert.That(victim.HP, Is.EqualTo(max - max / 4));
   var guarded = F(); guarded.Data.ability = "매직가드";
   Fire(hitter, guarded, TriggerStructures.TurnEndLabel);
   Assert.That(guarded.HP, Is.EqualTo(max), "magic guard blocks indirect damage");
  }

  [Test] public void FaintedPokemonIsNotHealed() {
   var healer = F("노말", P(TriggerStructures.TurnEndLabel, "아군의 체력을 1/4회복한다")); var other = F();
   healer.HP = 0;
   var log = new List<string>();
   using (EffectScope.Begin(new[] { healer, other }, new Rules(), () => .5, new[] { new TrainerBattleState(), new TrainerBattleState() })) {
    var call = new EffectCall { Source = new PotentialInstance { Owner = healer, Name = "검사" }, Event = new PotentialEvent { Owner = healer, Other = other }, Line = "자신의 체력을 최대 HP의 1/4 회복한다.", Rules = new Rules(), Log = log };
    Assert.That(PotentialExecutors.Find(call).Run(call), Is.EqualTo(EffectOutcome.Failed));
   }
   Assert.That(healer.HP, Is.EqualTo(0));
  }

  [Test] public void CostFailsWhenHpTooLowAndBlocksFollowingEffect() {
   var owner = F("노말", P(TriggerStructures.TurnEndLabel, "체력을 1/2 소비해 「가열」상태가 된다.")); var other = F();
   owner.HP = 5;
   Fire(owner, other, TriggerStructures.TurnEndLabel);
   Assert.That(owner.HP, Is.EqualTo(5));
   Assert.That(owner.Conditions.Heated, Is.False);
   owner.HP = Mechanics.MaxHP(owner, other, new Rules());
   Fire(owner, other, TriggerStructures.TurnEndLabel);
   Assert.That(owner.Conditions.Heated, Is.True);
  }

  [Test] public void HealAndCureStatus() {
   var owner = F("노말", P(TriggerStructures.TurnEndLabel, "자신의 체력을 1/4회복해 상태이상을 치유 한다")); var other = F();
   owner.Status = "마비"; owner.HP = 50;
   Fire(owner, other, TriggerStructures.TurnEndLabel);
   Assert.That(owner.Status, Is.EqualTo("정상"));
   Assert.That(owner.HP, Is.GreaterThan(50));
  }
 }
}
