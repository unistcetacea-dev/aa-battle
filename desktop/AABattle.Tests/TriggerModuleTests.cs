using System;
using System.Linq;
using System.Collections.Generic;
using NUnit.Framework;

namespace AABattle {
 // 트리거 모듈 1단계: 수집 원문 정규화와 배틀 사건 대조
 [TestFixture] public class TriggerModuleTests {
  static int ids;
  static Fighter F(string type = "노말", params Potential[] potentials) {
   return new Fighter(new Pokemon {
    id = "pk-t" + (++ids), name = "T" + ids, level = 100, types = new[] { type }, ability = "", item = "",
    @base = new[] { 100, 100, 100, 100, 100, 100 }, iv = new[] { 31, 31, 31, 31, 31, 31 },
    moves = new[] { "몸통박치기" }, potentials = potentials
   });
  }
  static Potential P(string trigger, string slot = "종족 ①", string effect = "자신의 「공격」이 오른다.") { return new Potential { id = slot, name = "검사 " + slot, description = effect, triggers = trigger.Split('\n') }; }
  static Move Tackle() { return TestData.Db.moves.First(x => x.name == "몸통박치기"); }
  static Move Idle() { return new Move { name = "미등록", category = "변화", types = new[] { "노말" }, tags = new string[0], accuracy = 100, target = "자신" }; }
  static TurnResult Turn(Fighter a, Fighter b, Move ma, Move mb, Rules rules = null) { return Engine.Resolve(TestData.Db, new[] { a, b }, new[] { ma, mb }, rules ?? new Rules(), () => .5); }

  [TestCase("필드에 나와", "장소에 나올 때")]
  [TestCase("「죽어내밀기」로 장소에 나올 때", "죽어내밀기로 장소에 나올 때")]
  [TestCase("상대가 「강제교대」로 나왔을 때", "상대가 강제교대로 장소에 나왔을 때")]
  [TestCase("「전기/페어리」인 아군과 교대해 장소에 나올 때", "「전기/페어리」 타입 아군과 교대해 장소에 나올 때")]
  [TestCase("「불꽃」타입과 교대할 때", "「불꽃」 타입 아군과 교대할 때")]
  [TestCase("임의교대시", "아군과 임의교대할 때")]
  [TestCase("상대가 필드를 떠날 때", "상대가 장소를 떠날 때")]
  [TestCase("「3」의 배수의 턴 종료시", "「3」배수 T 종료 시")]
  [TestCase("필드에 나와 2T째 종료시", "장소에 나와 「2」T째 종료 시")]
  [TestCase("필드에 나온 T 종료시에", "장소에 나와 「1」T째 종료 시")]
  [TestCase("공격을 받은 T종료시", "「상대의 공격을 받았을 때」가 성립한 T 종료 시")]
  [TestCase("「죽어내밀기」로 나온 T종료시", "「죽어내밀기로 장소에 나올 때」가 성립한 T 종료 시")]
  [TestCase("「접촉기술」을 받았을 때", "상대의 「접촉」 기술을 받았을 때")]
  [TestCase("상대의 접촉공격을 받았을 때", "상대의 「접촉」 공격을 받았을 때")]
  [TestCase("상대의 공격이 명중했을 때", "상대의 공격을 받았을 때")]
  [TestCase("아인의 공격을 받았을 때", "아인종 상대의 공격을 받았을 때")]
  [TestCase("자신의 공격 명중 시", "자신의 공격이 명중했을 때")]
  [TestCase("공격이 성공했을 때", "자신의 공격이 명중했을 때")]
  [TestCase("자신의 「물기」「엄니」 등의 기술이 명중했을 때", "자신의 「물기」「엄니」 기술이 명중했을 때")]
  [TestCase("자신의 우선도 +1이상의 공격이 명중했을 때", "자신의 「우선도 +」 공격이 명중했을 때")]
  [TestCase("자신의 공격이 급소에 맞았을 때", "자신의 기술이 급소에 맞았을 때")]
  [TestCase("「강철」포켓몬을 쓰러뜨렸을 때", "「강철」 타입 포켓몬을 쓰러뜨렸을 때")]
  [TestCase("「아인」을 쓰러뜨렸을 때", "아인종 포켓몬을 쓰러뜨렸을 때")]
  [TestCase("자신이 「인파이트」으로 상대를 쓰러뜨렸을 때", "자신의 「인파이트」 기술로 상대를 쓰러뜨렸을 때")]
  [TestCase("공격기술을 내보낼 때", "「공격」 기술을 내보낼 때")]
  [TestCase("「소리」 타입 기술을 내보낼 때", "「소리」 기술을 내보낼 때")]
  [TestCase("자신이 「얼음」기술을 내보냈을 때", "「얼음」 기술을 내보냈을 때")]
  [TestCase("기술 「칼춤」을 내보냈을 때", "「칼춤」 기술을 내보냈을 때")]
  [TestCase("상대가 위력 「40」이하의 기술을 내보낼 때", "상대가 「위력 40 이하」 기술을 내보낼 때")]
  [TestCase("서로 공격을 내보낼 때", "서로 공격기를 선택했을 때")]
  public void NormalizesCollectedWording(string raw, string expected) {
   Assert.That(TriggerStructures.Normalize(raw), Is.EqualTo(expected));
   Assert.That(TriggerStructures.Normalize(expected), Is.EqualTo(expected), "canonical is stable");
   if (!expected.StartsWith("서로")) Assert.That(TriggerModules.Classify(expected), Is.Not.Empty, "canonical is a module");
  }

  // 2단계: 상태 · 체력 · 날씨 · 장소 · 랭크
  [TestCase("「빈사」상태가 됐을 때", "자신이 「빈사」 상태가 되었을 때")]
  [TestCase("쓰러졌을 때", "자신이 「빈사」 상태가 되었을 때")]
  [TestCase("상대가 쓰러졌을 때", "상대가 「빈사」 상태가 되었을 때")]
  [TestCase("상대가 「독」상태일 때", "상대가 「독」 상태이상일 때")]
  [TestCase("자신이 「아쿠아링」상태일 때", "자신이 「아쿠아링」 상태변화일 때")]
  [TestCase("상대가 「독(맹독)」상태가 됐을 때", "상대가 「독」「맹독」 상태이상이 되었을 때")]
  [TestCase("「충전」상태가 해제되었을 때", "자신의 「충전」 상태가 해제되었을 때")]
  [TestCase("상대가 「상태이상」에 걸렸을 때", "상대가 「상태이상」이 되었을 때")]
  [TestCase("체력이 가득일 때", "자신의 체력이 가득일 때")]
  [TestCase("자신의 체력이 1/2이하가 됐을 때", "자신의 체력이 1/2 이하가 되었을 때")]
  [TestCase("체력이 절반 이하가 됐을 때", "자신의 체력이 1/2 이하가 되었을 때")]
  [TestCase("체력이 「1/2」이하일 때", "자신의 체력이 1/2 이하일 때")]
  [TestCase("체력이 가득하지 않아졌을 때", "자신의 체력이 가득하지 않게 되었을 때")]
  [TestCase("상대의 체력이 「1」이 됐을 때", "상대의 체력이 「1」이 되었을 때")]
  [TestCase("자신의 체력이 회복했을 때", "자신이 체력을 회복했을 때")]
  [TestCase("날씨가 「(강한) 비」일 때", "날씨가 「비」일 때")]
  [TestCase("날씨가 「모래날림」이 되었을 때", "날씨가 「모래바람」이 되었을 때")]
  [TestCase("필드가 「일렉트릭필드」일때", "「일렉트릭필드」일 때")]
  [TestCase("필드에 「중력」이 전개되어 있을 때", "「중력」이 전개되어 있을 때")]
  [TestCase("필드가 「트릭룸」일 때", "「트릭룸」이 전개되어 있을 때")]
  [TestCase("장소가 「그래스필드」로 변했을 때", "「그래스필드」가 되었을 때")]
  [TestCase("상대의 속도가 내려갔을 때", "상대의 「속도」가 내려갔을 때")]
  [TestCase("상대의 능력이 떨어졌을 때", "상대의 능력이 내려갔을 때")]
  [TestCase("「공격」이 크게 올랐을 때", "자신의 「공격」이 크게 올랐을 때")]
  [TestCase("자신의 「공/특공」이 올랐을 때", "자신의 「공격/특공」이 올랐을 때")]
  [TestCase("상대가 「마비」상태가 된 T 종료시", "「상대가 「마비」 상태이상이 되었을 때」가 성립한 T 종료 시")]
  public void NormalizesStageTwoWording(string raw, string expected) { NormalizesCollectedWording(raw, expected); }

  [Test] public void FaintedOwnerTriggersOnBecomingFainted() {
   var owner = F("노말", P("쓰러졌을 때", "종족 ①", "상대에게 최대 HP의 1/4 대미지를 준다.")); owner.HP = 1;
   var attacker = F();
   var turn = Turn(attacker, owner, Tackle(), Idle());
   Assert.That(turn.Fighters[1].HP, Is.EqualTo(0));
   Assert.That(turn.Fighters[0].HP, Is.LessThan(Mechanics.MaxHP(turn.Fighters[0], turn.Fighters[1], new Rules())), string.Join("\n", turn.Log));
  }

  [Test] public void HpCrossingFiresOnceWhenGoingBelow() {
   var owner = F("노말", P("자신의 체력이 1/2 이하가 됐을 때"));
   var first = Turn(owner, F(), Idle(), Tackle());
   Assert.That(first.Fighters[0].Stages[1], Is.EqualTo(0), "still above half");
   first.Fighters[0].HP = Mechanics.MaxHP(first.Fighters[0], first.Fighters[1], first.Rules) / 2 + 5;
   var second = Engine.Resolve(TestData.Db, first.Fighters, new[] { Idle(), Tackle() }, first.Rules, () => .5);
   Assert.That(second.Fighters[0].Stages[1], Is.EqualTo(1), "crossed below half");
   var third = Engine.Resolve(TestData.Db, second.Fighters, new[] { Idle(), Tackle() }, second.Rules, () => .5);
   Assert.That(third.Fighters[0].Stages[1], Is.EqualTo(1), "already below: no refire");
  }

  [Test] public void EventWithOpponentStatusCondition() {
   var owner = F("노말", P("T 종료 시\n상대가 「화상」 상태이상일 때"));
   var burned = F(); burned.Status = "화상";
   Assert.That(Turn(owner, burned, Idle(), Idle()).Fighters[0].Stages[1], Is.EqualTo(1));
   Assert.That(Turn(F("노말", P("T 종료 시\n상대가 「화상」 상태이상일 때")), F(), Idle(), Idle()).Fighters[0].Stages[1], Is.EqualTo(0));
  }

  [Test] public void RankAndWeatherChangeEvents() {
   var owner = F("노말", P("상대의 「공격」이 내려갔을 때"), P("날씨가 「비」가 되었을 때", "종족 ②", "자신의 「방어」가 오른다."));
   var other = F(); var rules = new Rules(); var log = new List<string>();
   using (EffectScope.Begin(new[] { owner, other }, rules, () => .5, new[] { new TrainerBattleState(), new TrainerBattleState() })) {
    Mechanics.ChangeStage(other, owner, 1, -1, rules, "검사", log);
    Mechanics.SetWeather(other, owner, "비", 5, rules, log, "검사");
   }
   Assert.That(owner.Stages[1], Is.EqualTo(1), string.Join("\n", log));
   Assert.That(owner.Stages[2], Is.EqualTo(1), string.Join("\n", log));
  }

  // 확정 21: 강한 비는 「비」 조건에 포함, 날씨부정이면 불성립
  [Test] public void StrongRainCountsAsRain() {
   var spec = TriggerModules.Parse(TriggerStructures.Normalize("날씨가 「비」일 때"));
   var a = F(); var b = F();
   Assert.That(TriggerModules.Holds(spec, a, b, new Rules { Weather = "강한 비" }), Is.True);
   Assert.That(TriggerModules.Holds(TriggerModules.Parse(TriggerStructures.Normalize("날씨가 「강한 비」일 때")), a, b, new Rules { Weather = "비" }), Is.False);
  }

  [TestCase("상대를 해석한 T 종료시")]
  [TestCase("「나무열매」를 사용했을 때")]
  [TestCase("우선도 「-3」의 기술을 내보낼 때")]
  [TestCase("「효과가굉장」인 공격을 받을 때")]
  [TestCase("자신이 지령을 받았을 때")]
  public void LeavesOtherWordingOutOfModules(string raw) {
   Assert.That(TriggerModules.Classify(raw), Is.Empty);
  }

  [Test] public void MoveUseBeforeAndAfterAreSeparateTimings() {
   var user = F("노말", P("「노말」 기술을 내보낼 때", "종족 ①", "검사 직전"), P("「노말」 기술을 내보냈을 때", "종족 ②", "검사 직후"));
   var log = Turn(user, F(), Tackle(), Idle()).Log;
   int before = log.FindIndex(x => x.Contains("『검사 종족 ①』")), move = log.FindIndex(x => x.Contains("의 몸통박치기!")), after = log.FindIndex(x => x.Contains("『검사 종족 ②』"));
   Assert.That(before, Is.GreaterThanOrEqualTo(0).And.LessThan(move), string.Join("\n", log));
   Assert.That(after, Is.GreaterThan(move), string.Join("\n", log));
  }

  [Test] public void ReceivedWithMoveConditionAndHitMeansSuccess() {
   Assert.That(Mechanics.Contact(Tackle()), Is.True);
   var guard = F("노말", P("「접촉기술」을 받았을 때"));
   var other = F("노말", P("상대의 「특수」 기술을 받았을 때"));
   var attacker = F("노말", P("공격이 성공했을 때"));
   var turn = Turn(attacker, guard, Tackle(), Idle());
   Assert.That(turn.Fighters[1].Stages[1], Is.EqualTo(1), "contact received");
   Assert.That(turn.Fighters[0].Stages[1], Is.EqualTo(1), "hit = attack success");
   Assert.That(Turn(F(), other, Tackle(), Idle()).Fighters[1].Stages[1], Is.EqualTo(0), "physical is not special");
  }

  [Test] public void DefeatByTypeAndAin() {
   var hunter = F("노말", P("「노말」포켓몬을 쓰러뜨렸을 때"), P("「아인」을 쓰러뜨렸을 때", "종족 ②", "자신의 「방어」가 오른다."));
   var prey = F(); prey.HP = 1; prey.Data.ain = true;
   var turn = Turn(hunter, prey, Tackle(), Idle());
   Assert.That(turn.Fighters[1].HP, Is.EqualTo(0));
   Assert.That(turn.Fighters[0].Stages[1], Is.EqualTo(1), "typed defeat");
   Assert.That(turn.Fighters[0].Stages[2], Is.EqualTo(1), "ain defeat");
  }

  // 확정 48: 사건 + 상태 조건은 AND. 상태가 성립하지 않으면 실행하지 않는다
  [Test] public void EventWithStateConditionNeedsBoth() {
   var full = F("노말", P("T 종료 시\n체력이 가득일 때"));
   Assert.That(Turn(full, F(), Idle(), Idle()).Fighters[0].Stages[1], Is.EqualTo(1));
   var hurt = F("노말", P("T 종료 시\n체력이 가득일 때")); hurt.HP -= 10;
   Assert.That(Turn(hurt, F(), Idle(), Idle()).Fighters[0].Stages[1], Is.EqualTo(0));
  }

  [Test] public void TurnEndAfterEventInThatTurnOnly() {
   var owner = F("노말", P("공격을 받은 T종료시"));
   var first = Turn(owner, F(), Idle(), Tackle());
   Assert.That(first.Fighters[0].Stages[1], Is.EqualTo(1), "hit this turn");
   var second = Engine.Resolve(TestData.Db, first.Fighters, new[] { Idle(), Idle() }, first.Rules, () => .5);
   Assert.That(second.Fighters[0].Stages[1], Is.EqualTo(1), "no hit next turn");
  }

  // 등장한 T가 1T째(선발은 첫 T), 「N」배수 T는 배틀 T 번호
  [Test] public void FieldTurnAndTurnMultiple() {
   var owner = F("노말", P("필드에 나와 2T째 종료시"), P("「3」배수 T 종료 시", "종족 ②", "자신의 「방어」가 오른다."));
   var fighters = new[] { owner, F() }; var rules = new Rules();
   var stages = new List<int[]>();
   for (int t = 0; t < 3; t++) { var r = Engine.Resolve(TestData.Db, fighters, new[] { Idle(), Idle() }, rules, () => .5); fighters = r.Fighters; rules = r.Rules; stages.Add(new[] { fighters[0].Stages[1], fighters[0].Stages[2] }); }
   Assert.That(stages.Select(x => x[0]), Is.EqualTo(new[] { 0, 1, 1 }), "2T째 only");
   Assert.That(stages.Select(x => x[1]), Is.EqualTo(new[] { 0, 0, 1 }), "3배수 T");
  }

  [Test] public void OpponentForcedEntryIsSeparateMethod() {
   var watcher = F("노말", P("상대가 「강제교대」로 나왔을 때"), P("상대가 장소에 나왔을 때", "종족 ②", "자신의 「방어」가 오른다."));
   var entering = F();
   using (EffectScope.Begin(new[] { watcher, entering }, new Rules(), () => .5, new[] { new TrainerBattleState(), new TrainerBattleState() }))
    EntryTriggers.Enter(entering, watcher, new Rules(), () => .5, EntryReason.Forced, new List<string>().Add);
   Assert.That(watcher.Stages[1], Is.EqualTo(1), "forced entry");
   Assert.That(watcher.Stages[2], Is.EqualTo(0), "plain entry excludes forced");
  }
 }
}
