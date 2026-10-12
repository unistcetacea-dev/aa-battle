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
  static List<string> Fire(Fighter owner, Fighter other, string label, TrainerBattleState[] states = null, Rules rules = null) {
   var log = new List<string>();
   rules = rules ?? new Rules();
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
   Fire(owner, other, TriggerStructures.TurnEndLabel, null, new Rules { ChooseStat = (s, f, list) => "속도" });
   Assert.That(owner.Stages[5], Is.EqualTo(1));
  }

  // ===== F2 종족치 =====
  [Test] public void SpeciesGradeChangeRevertsOnLeave() {
   var owner = F("노말", P(TriggerStructures.TurnEndLabel, "자신의 「공격」의 종족치를 「AA」로 한다")); var other = F();
   Fire(owner, other, TriggerStructures.TurnEndLabel);
   Assert.That(owner.Data.@base[1], Is.EqualTo(SpeciesStatRanks.LabelRange("AA")[0]));
   Mechanics.LeaveField(owner, new List<string>());
   Assert.That(owner.Data.@base[1], Is.EqualTo(100));
  }

  // 사용자 결정: 등급 지정은 현재 값의 -·+를 이어받는다(B-→A-, B+→A+), 무표기는 등급 범위 안에서 고른다
  [TestCase(90, "A", 110, 110)]
  [TestCase(107, "A", 135, 139)]
  [TestCase(100, "A", 111, 134)]
  [TestCase(100, "A+", 135, 139)]
  public void GradeRangeKeepsCurrentModifier(int current, string grade, int min, int max) {
   Assert.That(SpeciesStatRanks.GradeRange(current, grade), Is.EqualTo(new[] { min, max }));
  }

  [Test] public void SpeciesGradeValueIsChosenWithinRange() {
   var owner = F("노말", P(TriggerStructures.TurnEndLabel, "자신의 「공격」의 종족치를 「A」로 한다")); var other = F();
   Fire(owner, other, TriggerStructures.TurnEndLabel, null, new Rules { ChooseSpeciesValue = (s, f, stat, min, max) => 120 });
   Assert.That(owner.Data.@base[1], Is.EqualTo(120));
   var minus = F("노말", P(TriggerStructures.TurnEndLabel, "자신의 「공격」의 종족치를 「A」로 한다")); minus.Data.@base[1] = 90;
   Fire(minus, other, TriggerStructures.TurnEndLabel, null, new Rules { ChooseSpeciesValue = (s, f, stat, min, max) => 120 });
   Assert.That(minus.Data.@base[1], Is.EqualTo(110), "B- becomes A- only");
  }

  // 기술 사건으로 걸린 배율은 그 기술 한 번에만
  [Test] public void MoveEventMultiplierLastsOnlyForThatAction() {
   var idle = new Move { name = "미등록", category = "변화", types = new[] { "노말" }, tags = new string[0], accuracy = 100, target = "자신" };
   var hit = Mv("노말"); hit.priority = 1;
   var boosted = F("노말", P(TriggerStructures.AttackMoveUseLabel, "자신이 주는 대미지를 강화(2배)한다."));
   var plainTurn = Engine.Resolve(TestData.Db, new[] { F(), F() }, new[] { hit, idle }, new Rules(), () => .5);
   var turn = Engine.Resolve(TestData.Db, new[] { boosted, F() }, new[] { hit, idle }, new Rules(), () => .5);
   int plainLoss = plainTurn.Fighters[1].HP, loss = turn.Fighters[1].HP;
   Assert.That(341 - loss, Is.InRange(2 * (341 - plainLoss) - 2, 2 * (341 - plainLoss) + 2), string.Join("\n", turn.Log));
   Assert.That(turn.Fighters[0].Conditions.Modifiers, Is.Empty, "cleared after the action");
   // 포텐셜은 남아 있어 다음 턴 공격에서 다시 발동한다
   turn.Fighters[1].HP = 341;
   var next = Engine.Resolve(TestData.Db, turn.Fighters, new[] { new BattleCommand { Move = hit }, new BattleCommand { Move = idle } }, turn.Rules, turn.Trainers, () => .5);
   Assert.That(341 - next.Fighters[1].HP, Is.InRange(2 * (341 - plainLoss) - 2, 2 * (341 - plainLoss) + 2), "fires again next turn");
   // 1/시면 다음 턴에는 발동하지 않는다
   var limited = F("노말", new Potential { id = "종족 ①", name = "한 번", description = "자신이 주는 대미지를 강화(2배)한다.", uses = "1/시", triggers = new[] { TriggerStructures.AttackMoveUseLabel } });
   var first = Engine.Resolve(TestData.Db, new[] { limited, F() }, new[] { hit, idle }, new Rules(), () => .5);
   var second = Engine.Resolve(TestData.Db, first.Fighters, new[] { new BattleCommand { Move = hit }, new BattleCommand { Move = idle } }, first.Rules, first.Trainers, () => .5);
   Assert.That(first.Fighters[1].HP - second.Fighters[1].HP, Is.InRange(341 - plainLoss - 2, 341 - plainLoss + 2), "1/시 used up");
  }

  // 등장 포텐셜은 실제로 나왔을 때 한 번만(매 턴 선발 등장을 다시 처리하지 않는다)
  [Test] public void EntryPotentialFiresOncePerEntry() {
   var idle = new Move { name = "미등록", category = "변화", types = new[] { "노말" }, tags = new string[0], accuracy = 100, target = "자신" };
   var lead = F("노말", P(TriggerStructures.EntryLabel, "자신의 「공격」이 오른다."));
   var t1 = Engine.Resolve(TestData.Db, new[] { lead, F() }, new[] { idle, idle }, new Rules(), () => .5);
   var t2 = Engine.Resolve(TestData.Db, t1.Fighters, new[] { new BattleCommand { Move = idle }, new BattleCommand { Move = idle } }, t1.Rules, t1.Trainers, () => .5);
   Assert.That(t2.Fighters[0].Stages[1], Is.EqualTo(1), string.Join("\n", t2.Log));
   var reserve = F("노말", P(TriggerStructures.EntryLabel, "자신의 「공격」이 오른다.")); reserve.OnField = false;
   var t3 = Engine.Resolve(TestData.Db, t2.Fighters, new[] { new BattleCommand { SwitchIn = reserve }, new BattleCommand { Move = idle } }, t2.Rules, t2.Trainers, () => .5);
   var t4 = Engine.Resolve(TestData.Db, t3.Fighters, new[] { new BattleCommand { Move = idle }, new BattleCommand { Move = idle } }, t3.Rules, t3.Trainers, () => .5);
   Assert.That(t3.Fighters[0].Stages[1], Is.EqualTo(1));
   Assert.That(t4.Fighters[0].Stages[1], Is.EqualTo(1), "switched-in pokemon does not refire next turn");
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
