using System;
using System.Linq;
using System.Collections.Generic;
using NUnit.Framework;

namespace AABattle {
 // 기능확장(기술 목록 밖의 기술)과 트레이너 고유의 「아군」 관점
 [TestFixture] public class FunctionExtensionTests {
  static int ids;
  static Fighter F(params Potential[] potentials) {
   return new Fighter(new Pokemon {
    id = "pk-x" + (++ids), name = "X" + ids, level = 100, types = new[] { "노말" }, ability = "", item = "",
    @base = new[] { 100, 100, 100, 100, 100, 100 }, iv = new[] { 31, 31, 31, 31, 31, 31 },
    moves = new[] { "몸통박치기" }, potentials = potentials
   });
  }
  static Potential P(string trigger, string effect, string slot = "종족 ①") { return new Potential { id = slot, name = "검사 " + slot, description = effect, triggers = trigger.Length == 0 ? new string[0] : new[] { trigger } }; }
  static Move Tackle() { return TestData.Db.moves.First(x => x.name == "몸통박치기"); }
  static Move Idle() { return new Move { name = "미등록", category = "변화", types = new[] { "노말" }, tags = new string[0], accuracy = 100, target = "자신" }; }

  [TestCase("「기능확장:속이다」를 내보낼 수 있다", "자신이 「기능확장:속이다」를 내보낼 수 있다.")]
  [TestCase("PT 전원이 「기능확장:자이로볼」를 내보낼 수 있다", "아군 전원이 「기능확장:자이로볼」을 내보낼 수 있다.")]
  [TestCase("아군 전원이 기능확장 「전자포」를 사용할 수 있다", "아군 전원이 「기능확장:전자포」를 내보낼 수 있다.")]
  [TestCase("「기능확장(달의불빛)」을 내보낼 수 있다", "자신이 「기능확장:달의불빛」을 내보낼 수 있다.")]
  [TestCase("PT의 아군 전원이 「기능확장:공격지령」, 「방어지령」, 「회복지령」을 내보낼 수 있다", "아군 전원이 「기능확장:공격지령」「기능확장:방어지령」「기능확장:회복지령」을 내보낼 수 있다.")]
  public void NormalizesGrantWording(string raw, string expected) {
   Assert.That(EffectNormalizer.Normalize(raw), Is.EqualTo(expected));
   Assert.That(EffectModules.Classify(expected), Is.EqualTo("기능확장 부여"));
  }

  [Test] public void AvailableComesFromOwnPartyAndTrainerUnique() {
   var own = F(P("", "「기능확장:대폭발」을 내보낼 수 있다"));
   var member = F(P("", "아군 전원이 「기능확장:전자포」를 내보낼 수 있다"), P("", "「기능확장:속이다」를 내보낼 수 있다", "종족 ②"));
   var trainer = new TrainerRecord { potentials = new List<PotentialRecord> { new PotentialRecord { slot = "고유", name = "검사 고유", trigger = "", effect = "PT의 아군 전원이 「기능확장:자이로볼」를 내보낼 수 있다" } } };
   var moves = FunctionExtensions.Available(own, F(), new Rules(), trainer, new[] { member });
   Assert.That(moves, Is.EquivalentTo(new[] { "대폭발", "전자포", "자이로볼" }), "member's self-only grant is not shared");
   Assert.That(FunctionExtensions.Available(F(P("", "「기능확장:몸통박치기」를 내보낼 수 있다")), F(), new Rules(), null, null), Is.Empty, "already in the move list");
  }

  // 기능확장으로 낸 기술만 「기능확장」 조건(트리거·효과)에 맞는다
  [TestCase(true, 1)]
  [TestCase(false, 0)]
  public void ExtensionFlagDrivesConditions(bool extension, int stage) {
   var user = F(P("자신이 「기능확장」을 내보낼 때", "자신의 「공격」이 오른다."));
   var turn = Engine.Resolve(TestData.Db, new[] { user, F() }, new[] { new BattleCommand { Move = Tackle(), Extension = extension }, new BattleCommand { Move = Idle() } }, new Rules(), null, () => .5);
   Assert.That(turn.Fighters[0].Stages[1], Is.EqualTo(stage), string.Join("\n", turn.Log));
  }

  // 트레이너 고유의 「아군」 = 장소의 자기 포켓몬(사용자 결정)
  [Test] public void TrainerAllyMeansOwnActivePokemon() {
   var trainer = new TrainerRecord { potentials = new List<PotentialRecord> { new PotentialRecord { slot = "고유", name = "검사 고유", trigger = "아군의 「노말」 기술 명중시", effect = "아군의 공격이 오른다." } } };
   var states = new[] { new TrainerBattleState { Trainer = trainer }, new TrainerBattleState() };
   var turn = Engine.Resolve(TestData.Db, new[] { F(), F() }, new[] { new BattleCommand { Move = Tackle() }, new BattleCommand { Move = Idle() } }, new Rules(), states, () => .5);
   Assert.That(turn.Fighters[0].Stages[1], Is.EqualTo(1), string.Join("\n", turn.Log));
   Assert.That(TrainerTriggers.TrainerClause("아군을 필드에 내보냈을 때"), Is.EqualTo(TriggerStructures.EntryLabel));
  }
 }
}
