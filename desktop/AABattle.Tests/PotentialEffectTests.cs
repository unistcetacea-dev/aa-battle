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

  // ===== F4b 타입·상성(확정 30) =====
  [Test] public void MultiTypeMoveMultipliesEachTypeAndStabOnce() {
   var user = F("불꽃"); var grass = F("풀"); var water = F("물");
   var both = new Move { name = "복합검사", category = "물리", types = new[] { "불꽃", "물" }, tags = new string[0], power = 80, accuracy = 100, attack = "공격", defense = "방어" };
   Assert.That(Mechanics.TypeEffect(TestData.Db, user, grass, both, new Rules()), Is.EqualTo(1), "fire 2 x water 0.5");
   Assert.That(Mechanics.TypeEffect(TestData.Db, user, water, both, new Rules()), Is.EqualTo(.25), "fire 0.5 x water 0.5");
   Assert.That(Engine.Calculate(TestData.Db, user, grass, both, new Rules()).Stab, Is.EqualTo(1.5));
  }

  [Test] public void AddedTypeUsesMoreFavorableMatchupAndCountsForStab() {
   var adder = F("비행", P(Field, "자신의 「물」기술에 「불꽃」타입을 부여해 유리한 타입으로 계산한다"));
   var grass = F("풀"); var rock = F("바위");
   Assert.That(Mechanics.TypeEffect(TestData.Db, adder, grass, Mv("물"), new Rules()), Is.EqualTo(2), "fire beats grass");
   Assert.That(Mechanics.TypeEffect(TestData.Db, adder, rock, Mv("물"), new Rules()), Is.EqualTo(2), "water beats rock");
   Assert.That(Mechanics.TypeEffect(TestData.Db, adder, grass, Mv("전기"), new Rules()), Is.EqualTo(.5), "other moves unchanged");
   var fireUser = F("불꽃", P(Field, "자신의 「물」기술에 「불꽃」타입을 부여해 유리한 타입으로 계산한다"));
   Assert.That(Engine.Calculate(TestData.Db, fireUser, rock, Mv("물"), new Rules()).Stab, Is.EqualTo(1.5));
  }

  [Test] public void ResistPierceAndIgnoreAllCoverImmunity() {
   var water = F("물"); var ghost = F("고스트");
   var pierce = F("노말", P(Field, "자신의 「불꽃」기술이 상대의 타입내성을 관통한다"));
   Assert.That(Mechanics.TypeEffect(TestData.Db, pierce, water, Mv("불꽃"), new Rules()), Is.EqualTo(1));
   var normalPierce = F("노말", P(Field, "자신의 「노말」기술이 상대의 타입내성을 관통한다"));
   Assert.That(Mechanics.TypeEffect(TestData.Db, normalPierce, ghost, Mv("노말"), new Rules()), Is.EqualTo(1), "resist pierce also breaks immunity");
   var ignore = F("노말", P(Field, "자신의 「노말」기술이 타입상성을 무시한다"));
   Assert.That(Mechanics.TypeEffect(TestData.Db, ignore, ghost, Mv("노말"), new Rules()), Is.EqualTo(1));
   var holy = F("노말", P(Field, "자신의 「노말」기술이 타입상성에 의해 무시되지 않는다"));
   Assert.That(Mechanics.TypeEffect(TestData.Db, holy, ghost, Mv("노말"), new Rules()), Is.EqualTo(1));
  }

  [Test] public void StabMultiplierOverrideOnlyWhenTypeMatches() {
   var user = F("불꽃", P(Field, "자신의 불꽃기술의 타입일치배율을 강화(2배)한다."));
   var target = F();
   Assert.That(Engine.Calculate(TestData.Db, user, target, Mv("불꽃"), new Rules()).Stab, Is.EqualTo(2));
   var other = F("물", P(Field, "자신의 불꽃기술의 타입일치배율을 강화(2배)한다."));
   Assert.That(Engine.Calculate(TestData.Db, other, target, Mv("불꽃"), new Rules()).Stab, Is.EqualTo(1));
  }

  [Test] public void TypeChangeFailsWhenSameAndRevertsOnLeave() {
   var owner = F("노말", P(TriggerStructures.EntryLabel, "상대를 「고스트」 타입으로 한다")); var target = F("물");
   Fire(owner, target, TriggerStructures.EntryLabel);
   Assert.That(target.Data.types, Is.EqualTo(new[] { "고스트" }));
   var log = Fire(owner, target, TriggerStructures.EntryLabel);
   Assert.That(log.Any(x => x.Contains("이미")), Is.True, "same type change fails");
   Mechanics.LeaveField(target);
   Assert.That(target.Data.types, Is.EqualTo(new[] { "물" }));
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

  // ===== F4a 장소·설치물·강제교대·계속 효과 =====
  [Test] public void PlaceDeploymentFollowsMoveRules() {
   var rules = new Rules();
   var owner = F("노말", P(TriggerStructures.TurnEndLabel, "2T간 날씨를 「비」로 한다", "종족 ①"), P(TriggerStructures.TurnEndLabel, "필드에 「중력」을 전개한다", "종족 ②")); var other = F();
   Fire(owner, other, TriggerStructures.TurnEndLabel, null, rules);
   Assert.That(rules.Weather, Is.EqualTo("비")); Assert.That(rules.WeatherTurns, Is.EqualTo(2));
   Assert.That(rules.Gravity, Is.EqualTo(5), "no duration: 5 turns like the move");
   var again = F("노말", P(TriggerStructures.TurnEndLabel, "날씨를 「비」로 한다"));
   var log = Fire(again, other, TriggerStructures.TurnEndLabel, null, rules);
   Assert.That(log.Any(x => x.Contains("이미 비")), Is.True, "same weather fails like the move");
  }

  [Test] public void HazardLayersCapAndOwnSideClear() {
   var rules = new Rules();
   var owner = F("노말", P(TriggerStructures.TurnEndLabel, "적진에 「독압정」을 설치한다")); var other = F();
   for (int i = 0; i < 3; i++) Fire(owner, other, TriggerStructures.TurnEndLabel, null, rules);
   Assert.That(rules.Sides[1].ToxicSpikes, Is.EqualTo(2), "two layers max");
   rules.Sides[0].StealthRock = true;
   var cleaner = F("노말", P(TriggerStructures.TurnEndLabel, "자진의 설치물을 해제한다"));
   Fire(cleaner, other, TriggerStructures.TurnEndLabel, null, rules);
   Assert.That(rules.Sides[0].Any(), Is.False);
  }

  static Move Idle() { return new Move { name = "미등록", category = "변화", types = new[] { "노말" }, tags = new string[0], accuracy = 100, target = "자신" }; }

  [Test] public void ForcedSwitchPicksRandomReserveOrFails() {
   var hit = Mv("노말"); hit.priority = 1; hit.power = 10;
   var owner = F("노말", P(TriggerStructures.AttackSuccessLabel, "상대를 강제교대시킨다."));
   var target = F(); var reserve = F(); reserve.OnField = false;
   var turn = Engine.Resolve(TestData.Db, new[] { owner, target }, new[] { new BattleCommand { Move = hit }, new BattleCommand { Move = Idle(), Party = new[] { target, reserve } } }, new Rules(), new[] { new TrainerBattleState(), new TrainerBattleState() }, () => .5);
   Assert.That(Mechanics.SameMember(turn.Fighters[1], reserve), Is.True, string.Join("\n", turn.Log));
   var alone = Engine.Resolve(TestData.Db, new[] { owner, F() }, new[] { hit, Idle() }, new Rules(), () => .5);
   Assert.That(alone.Log.Any(x => x.Contains("교대할 수 있는 멤버가 없어")), Is.True);
  }

  [Test] public void ForcedSwitchImmunityBlocks() {
   var hit = Mv("노말"); hit.priority = 1; hit.power = 10;
   var owner = F("노말", P(TriggerStructures.AttackSuccessLabel, "상대를 강제교대시킨다."));
   var target = F("노말", P(Field, "상대로부터의 강제교대를 받지 않는다")); var reserve = F(); reserve.OnField = false;
   var turn = Engine.Resolve(TestData.Db, new[] { owner, target }, new[] { new BattleCommand { Move = hit }, new BattleCommand { Move = Idle(), Party = new[] { target, reserve } } }, new Rules(), new[] { new TrainerBattleState(), new TrainerBattleState() }, () => .5);
   Assert.That(Mechanics.SameMember(turn.Fighters[1], target), Is.True);
  }

  [Test] public void FloatingAndStatusImmunityAreContinuous() {
   var floater = F("노말", P(Field, "공중에 떠오른다"));
   Assert.That(Mechanics.Grounded(floater, new Rules()), Is.False);
   Assert.That(Mechanics.Grounded(floater, new Rules { Gravity = 3 }), Is.True, "gravity grounds");
   var guarded = F("노말", P(Field, "「독/마비」상태가 되지 않는다"));
   Assert.That(Mechanics.ApplyStatus(guarded, F(), "독", new Rules(), () => .5, new List<string>()), Is.False);
   Assert.That(Mechanics.ApplyStatus(guarded, F(), "화상", new Rules(), () => .5, new List<string>()), Is.True);
  }

  // ===== F4b 추가행동(확정 29) =====
  static Move Tackle() { return TestData.Db.moves.First(x => x.name == "몸통박치기"); }
  static int Uses(TurnResult t, Fighter f, string move) { return t.Log.Count(x => x.StartsWith(f.Data.name + "의 " + move + "!")); }

  [Test] public void EventExtraActionActsAgainAtTurnEndOnce() {
   var owner = F("노말", P(TriggerStructures.AttackSuccessLabel, "가장 마지막에 추가행동을 얻는다."));
   var other = F(); other.Data.name = "상대E";
   var turn = Engine.Resolve(TestData.Db, new[] { owner, other }, new[] { Tackle(), Idle() }, new Rules(), () => .5);
   Assert.That(Uses(turn, owner, "몸통박치기"), Is.EqualTo(2), string.Join("\n", turn.Log));
   Assert.That(turn.Log.Any(x => x.Contains("추가행동 중에는 추가행동을 얻을 수 없다")), Is.True, "no extra action during an extra action");
  }

  [Test] public void ContinuousExtraActionFirstRespectsTurnMultiple() {
   var owner = F("노말", P(Field, "가장 먼저 추가행동을 얻는다."));
   var other = F(); other.Data.name = "상대F";
   var turn = Engine.Resolve(TestData.Db, new[] { owner, other }, new[] { Tackle(), Idle() }, new Rules(), () => .5);
   Assert.That(Uses(turn, owner, "몸통박치기"), Is.EqualTo(2));
   var everyTwo = F("노말", P(Field, "「2」배수T, 가장 마지막에 추가행동을 얻는다"));
   var first = Engine.Resolve(TestData.Db, new[] { everyTwo, F() }, new[] { Tackle(), Idle() }, new Rules { Turn = 1 }, () => .5);
   var second = Engine.Resolve(TestData.Db, new[] { everyTwo, F() }, new[] { Tackle(), Idle() }, new Rules { Turn = 2 }, () => .5);
   Assert.That(Uses(first, everyTwo, "몸통박치기"), Is.EqualTo(1));
   Assert.That(Uses(second, everyTwo, "몸통박치기"), Is.EqualTo(2));
  }

  [Test] public void ExtraActionNeedsMatchingMoveAndLivingOwner() {
   var owner = F("노말", P(Field, "가장 마지막에 「불꽃」 기술에 의한 추가행동을 얻는다."));
   var turn = Engine.Resolve(TestData.Db, new[] { owner, F() }, new[] { Tackle(), Idle() }, new Rules(), () => .5);
   Assert.That(turn.Log.Any(x => x.Contains("추가행동에 쓸 수 있는 기술이 없다")), Is.True);
   var fragile = F("노말", P(Field, "가장 마지막에 추가행동을 얻는다.")); fragile.HP = 1;
   var hitter = F(); var strong = Tackle().Copy(); strong.power = 250; strong.priority = 5;
   var lost = Engine.Resolve(TestData.Db, new[] { fragile, hitter }, new[] { Tackle(), strong }, new Rules(), () => .5);
   Assert.That(lost.Log.Any(x => x.Contains("추가행동이 사라졌다")), Is.True, string.Join("\n", lost.Log));
  }

  // ===== G 방호·회피·관통(확정 28) =====
  static TurnResult Attack(Fighter attacker, Fighter defender, Move move, Func<double> rng) {
   return Engine.Resolve(TestData.Db, new[] { attacker, defender }, new[] { move, Idle() }, new Rules(), rng);
  }

  [Test] public void NullifyBlocksMatchingMovesAndPierceGoesThrough() {
   var hit = Mv("노말"); hit.priority = 1;
   var guard = F("노말", P(Field, "상대의 「물리」 기술을 무효화한다."));
   var blocked = Attack(F(), guard, hit, () => .5);
   Assert.That(blocked.Fighters[1].HP, Is.EqualTo(guard.HP));
   Assert.That(blocked.Log.Any(x => x.Contains("방호 포텐셜로 무효화")), Is.True);
   var special = Mv("노말", "특수"); special.priority = 1;
   Assert.That(Attack(F(), guard, special, () => .5).Fighters[1].HP, Is.LessThan(guard.HP), "special move not covered");
   var piercer = F("노말", P(Field, "자신의 「노말」기술이 상대의 방호 포텐셜을 무시한다"));
   Assert.That(Attack(piercer, guard, hit, () => .5).Fighters[1].HP, Is.LessThan(guard.HP), "pierce ignores pokemon protection");
  }

  [Test] public void EvasionRollsPerAttackAndIsNotPierced() {
   var hit = Mv("노말"); hit.priority = 1;
   var dodger = F("노말", P(Field, "상대의 「노말」 기술을 중확률로 회피한다"));
   var dodged = Attack(F(), dodger, hit, () => .1);
   Assert.That(dodged.Fighters[1].HP, Is.EqualTo(dodger.HP));
   Assert.That(dodged.Log.Any(x => x.Contains("회피했다")), Is.True);
   Assert.That(Attack(F(), dodger, hit, () => .9).Fighters[1].HP, Is.LessThan(dodger.HP));
   var piercer = F("노말", P(Field, "자신의 기술이 상대의 방호 포텐셜을 무시한다."));
   Assert.That(Attack(piercer, dodger, hit, () => .1).Fighters[1].HP, Is.EqualTo(dodger.HP), "evasion is not protection");
   var sure = hit.Copy(); sure.tags = new[] { "필중" };
   Assert.That(Attack(F(), dodger, sure, () => .1).Fighters[1].HP, Is.LessThan(dodger.HP), "sure-hit moves are not evaded");
  }

  [Test] public void AnalysisImmunityBlocksDataAnalysis() {
   var target = F("노말", P(Field, "상대의 「데이터해석」을 무효화한다"));
   Assert.That(PotentialProtection.AnalysisBlocked(target, F(), new Rules()), Is.True);
  }

  // ===== 타이밍 규칙(사용자) =====
  [TestCase("턴 개시시", TriggerStructures.TurnStartLabel)]
  [TestCase("T 개시 시", TriggerStructures.TurnStartLabel)]
  [TestCase("턴의 최후에", TriggerStructures.TurnLastLabel)]
  [TestCase("T의 최후에", TriggerStructures.TurnLastLabel)]
  public void TimingLabelsNormalize(string raw, string label) { Assert.That(TriggerStructures.Normalize(raw), Is.EqualTo(label)); }

  [Test] public void TurnStartBeforeActionsAndTurnLastAfter() {
   var owner = F("노말", P("턴 개시시", "검사 시작", "종족 ①"), P("턴의 최후에", "검사 최후", "종족 ②"));
   var turn = Engine.Resolve(TestData.Db, new[] { owner, F() }, new[] { Tackle(), Idle() }, new Rules(), () => .5);
   int start = turn.Log.FindIndex(x => x.Contains(TriggerStructures.TurnStartLabel + " 트리거"));
   int move = turn.Log.FindIndex(x => x.StartsWith(owner.Data.name + "의 몸통박치기!"));
   int last = turn.Log.FindIndex(x => x.Contains(TriggerStructures.TurnLastLabel + " 트리거"));
   Assert.That(start, Is.GreaterThanOrEqualTo(0).And.LessThan(move), string.Join("\n", turn.Log));
   Assert.That(last, Is.GreaterThan(move));
  }

  [Test] public void FirstTiersBeatPriorityAndUseSpeedWithinTier() {
   var quick = Tackle().Copy(); quick.priority = 3;
   var slowFirst = F("노말", P(Field, "가장 먼저 행동한다")); slowFirst.Data.@base[5] = 10;
   var fastOpp = F("노말", P(Field, "상대보다 먼저 행동한다"));
   var turn = Engine.Resolve(TestData.Db, new[] { slowFirst, fastOpp }, new[] { Tackle(), quick }, new Rules(), () => .5);
   Assert.That(turn.Log.FindIndex(x => x.StartsWith(slowFirst.Data.name + "의 몸통박치기!")), Is.LessThan(turn.Log.FindIndex(x => x.StartsWith(fastOpp.Data.name + "의 몸통박치기!"))), "가장 먼저 beats 상대보다 먼저 and priority");
   var a = F("노말", P(Field, "가장 먼저 행동한다")); a.Data.@base[5] = 10;
   var b = F("노말", P(Field, "가장 먼저 행동한다")); b.Data.@base[5] = 200;
   var both = Engine.Resolve(TestData.Db, new[] { a, b }, new[] { Tackle(), Tackle() }, new Rules(), () => .5);
   Assert.That(both.Log.FindIndex(x => x.StartsWith(b.Data.name + "의 몸통박치기!")), Is.LessThan(both.Log.FindIndex(x => x.StartsWith(a.Data.name + "의 몸통박치기!"))), "same tier: faster first");
  }

  // ===== 반사(방호 포텐셜 취급)·트레이너 방호 =====
  [Test] public void ReflectSendsMoveBackAndPierceBreaksIt() {
   var taunt = TestData.Db.moves.First(x => x.name == "도발");
   var mirror = F("노말", P(Field, "상대의 「변화」 기술을 반사한다"));
   var user = F();
   var turn = Engine.Resolve(TestData.Db, new[] { user, mirror }, new[] { taunt, Idle() }, new Rules(), () => .5);
   Assert.That(turn.Fighters[0].Conditions.Taunt, Is.GreaterThan(0), string.Join("\n", turn.Log));
   Assert.That(turn.Fighters[1].Conditions.Taunt, Is.EqualTo(0));
   var hit = Mv("노말"); hit.priority = 1;
   var wall = F("노말", P(Field, "상대의 「물리」 기술을 반사한다"));
   var back = Attack(F(), wall, hit, () => .5);
   Assert.That(back.Fighters[1].HP, Is.EqualTo(wall.HP));
   Assert.That(back.Fighters[0].HP, Is.LessThan(341));
   var piercer = F("노말", P(Field, "자신의 기술이 상대의 방호 포텐셜을 무시한다."));
   Assert.That(Attack(piercer, wall, hit, () => .5).Fighters[1].HP, Is.LessThan(wall.HP), "reflect is protection: pierced");
  }

  static TrainerBattleState TrainerWith(string effect) {
   var trainer = new TrainerRecord { name = "검사 트레이너" };
   trainer.potentials.Add(new PotentialRecord { enabled = true, slot = "고유", name = "철벽", trigger = TriggerStructures.AlwaysLabel, effect = effect });
   return new TrainerBattleState { Trainer = trainer };
  }

  [Test] public void TrainerProtectionNeedsTrainerPierce() {
   var hit = Mv("노말"); hit.priority = 1;
   Func<Fighter, TrainerBattleState, TurnResult> run = (attacker, attackerTrainer) => Engine.Resolve(TestData.Db, new[] { attacker, F() }, new[] { new BattleCommand { Move = hit }, new BattleCommand { Move = Idle() } }, new Rules(), new[] { attackerTrainer ?? new TrainerBattleState(), TrainerWith("상대의 「물리」 기술을 무효화한다.") }, () => .5);
   Assert.That(run(F(), null).Fighters[1].HP, Is.EqualTo(341), "trainer protection blocks");
   Assert.That(run(F("노말", P(Field, "자신의 기술이 상대의 방호 포텐셜을 무시한다.")), null).Fighters[1].HP, Is.EqualTo(341), "pokemon pierce does not break trainer protection");
   Assert.That(run(F("노말", P(Field, "자신의 기술이 상대의 트레이너 방호 포텐셜을 무시한다.")), null).Fighters[1].HP, Is.LessThan(341), "explicit trainer pierce");
   Assert.That(run(F(), TrainerWith("자신의 기술이 상대의 방호 포텐셜을 무시한다.")).Fighters[1].HP, Is.LessThan(341), "pierce from a trainer potential");
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
   var guarded = F(); guarded.Data.ability = "매직가드"; AbilityRuntime.Reset(guarded);
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
