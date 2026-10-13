using System;
using System.Linq;
using System.Collections.Generic;
using NUnit.Framework;

namespace AABattle {
 // 소지품: 「아바돈_소지품」 기준(사용자 지정) + 오레마스 시스템
 [TestFixture] public class ItemTests {
  static int ids;
  static Fighter F(string item = "", string type = "노말", params string[] moves) {
   return new Fighter(new Pokemon {
    id = "pk-i" + (++ids), name = "I" + ids, level = 100, types = new[] { type }, ability = "", item = item,
    @base = new[] { 100, 100, 100, 100, 100, 100 }, iv = new[] { 31, 31, 31, 31, 31, 31 },
    moves = moves.Length > 0 ? moves : new[] { "몸통박치기" }, potentials = new Potential[0]
   });
  }
  static Move M(string name) { return TestData.Db.moves.First(x => x.name == name); }
  static Move Idle() { return new Move { name = "미등록", category = "변화", types = new[] { "노말" }, tags = new string[0], accuracy = 100, target = "자신" }; }
  static int Max(Fighter a, Fighter b, Move m) { return Engine.Calculate(TestData.Db, a, b, m, new Rules()).Max; }
  static TurnResult Turn(Fighter a, Fighter b, Move ma, Move mb, Rules r = null) { return Engine.Resolve(TestData.Db, new[] { a, b }, new[] { ma, mb }, r ?? new Rules(), () => .5); }

  [Test] public void CatalogReadsDocumentAndTypedNames() {
   Assert.That(ItemCatalog.All.Length, Is.GreaterThan(100));
   Assert.That(ItemCatalog.Find("불꽃플레이트").Effect, Does.Contain("「불꽃」"));
   Assert.That(ItemCatalog.Find("불구슬플레이트").Type, Is.EqualTo("불꽃"));
   Assert.That(ItemCatalog.Find("물주얼").Effect, Does.Contain("「물」"));
   Assert.That(ItemCatalog.Find("철구").Name, Is.EqualTo("검은철구"));
   Assert.That(ItemCatalog.Find("탈출팩"), Is.Not.Null, "(8세대) 표기는 이름에서 뗀다");
  }

  [Test] public void MoveListCanExceedFour() {
   var f = F("", "노말", "몸통박치기", "할퀴기", "전광석화", "방어", "칼춤", "지진");
   Assert.That(f.Moves.Length, Is.EqualTo(6));
   Assert.That(f.Copy().Moves.Length, Is.EqualTo(6));
  }

  [Test] public void TypeBoostItemRunsAsPotential() {
   Assert.That(Max(F("목탄"), F(), M("화염방사")), Is.GreaterThan(Max(F(), F(), M("화염방사")) * 1.15));
   Assert.That(Max(F("목탄"), F(), M("물대포")), Is.EqualTo(Max(F(), F(), M("물대포"))));
  }

  // 「1번만」: 발동하면 소지품이 사라진다
  [Test] public void SitrusBerryHealsOnceAndIsConsumed() {
   var holder = F("자뭉열매"); holder.HP = Mechanics.MaxHP(holder, F(), new Rules()) / 2 + 3;
   var turn = Turn(holder, F(), Idle(), M("몸통박치기"));
   Assert.That(turn.Fighters[0].Data.item, Is.Empty, string.Join("\n", turn.Log));
   Assert.That(turn.Fighters[0].LostItem, Is.EqualTo("자뭉열매"));
   Assert.That(turn.Fighters[0].HP, Is.GreaterThan(Mechanics.MaxHP(holder, F(), new Rules()) / 2));
  }

  [Test] public void ResistBerryHalvesOnceThenIsConsumed() {
   var attacker = F(); var plain = F("", "풀"); var berry = F("오카열매", "풀");
   int normal = Max(attacker, plain, M("화염방사"));
   Assert.That(Max(attacker, berry, M("화염방사")), Is.InRange(normal / 2 - 2, normal / 2 + 2));
   Assert.That(Max(attacker, F("오카열매"), M("화염방사")), Is.EqualTo(Max(attacker, F(), M("화염방사"))), "not super effective");
   var turn = Turn(attacker, berry, M("화염방사"), Idle());
   Assert.That(turn.Fighters[1].Data.item, Is.Empty);
   Assert.That(turn.Fighters[1].LostItem, Is.EqualTo("오카열매"));
  }

  [Test] public void LifeOrbBoostsAndRecoils() {
   Assert.That(Max(F("생명의구슬"), F(), M("몸통박치기")), Is.GreaterThan(Max(F(), F(), M("몸통박치기")) * 1.25));
   var orb = F("생명의구슬"); int max = Mechanics.MaxHP(orb, F(), new Rules());
   var turn = Turn(orb, F(), M("몸통박치기"), Idle());
   Assert.That(turn.Fighters[0].HP, Is.EqualTo(max - max / 10));
  }

  // 구애: 1.5배, 장소에 나와 처음 내보낸 기술만, 구애 중에는 기능확장 불가(오레마스 시스템)
  [Test] public void ChoiceLocksFirstMoveAndBlocksExtension() {
   var holder = F("구애머리띠", "노말", "몸통박치기", "할퀴기");
   Assert.That(Engine.Effective(holder, F(), 1, new Rules()), Is.EqualTo((int)Math.Floor(Engine.Effective(F(), F(), 1, new Rules()) * 1.5)));
   var turn = Turn(holder, F(), M("몸통박치기"), Idle());
   var after = turn.Fighters[0];
   Assert.That(Mechanics.MoveBlock(after, M("할퀴기")), Does.Contain("구애"));
   Assert.That(Mechanics.MoveBlock(after, M("몸통박치기")), Is.Empty);
   Assert.That(FunctionExtensions.Available(after, F(), new Rules(), null, null), Is.Empty);
   Mechanics.LeaveField(after);
   Assert.That(Mechanics.MoveBlock(after, M("할퀴기")), Is.Empty, "leaving the field clears the lock");
  }

  [Test] public void RockyHelmetDamagesContactAttacker() {
   var attacker = F(); int max = Mechanics.MaxHP(attacker, F(), new Rules());
   var turn = Turn(attacker, F("울퉁불퉁멧"), M("몸통박치기"), Idle());
   Assert.That(turn.Fighters[0].HP, Is.EqualTo(max - max / 8), string.Join("\n", turn.Log));
  }

  [Test] public void LumBerryCuresStatusOnce() {
   var holder = F("리샘열매");
   var turn = Turn(holder, F(), Idle(), M("도깨비불"));
   Assert.That(turn.Fighters[0].Status, Is.EqualTo("정상"), string.Join("\n", turn.Log));
   Assert.That(turn.Fighters[0].Data.item, Is.Empty);
  }

  [Test] public void SpongeNullifiesWaterAndBreaksOnOtherAttack() {
   var holder = F("스펀지"); int max = Mechanics.MaxHP(holder, F(), new Rules());
   var water = Turn(F(), holder, M("물대포"), Idle());
   Assert.That(water.Fighters[1].HP, Is.EqualTo(max), string.Join("\n", water.Log));
   Assert.That(water.Fighters[1].Data.item, Is.EqualTo("스펀지"), "nullified water is not 「받았다」");
   var turn = Turn(F(), F("스펀지"), M("몸통박치기"), Idle());
   Assert.That(turn.Fighters[1].Data.item, Is.Empty);
  }

  [Test] public void WeatherRockExtendsTurns() {
   var rules = new Rules();
   Mechanics.SetWeather(F("뜨거운바위"), F(), "쾌청", 5, rules, new List<string>(), "검사");
   Assert.That(rules.WeatherTurns, Is.EqualTo(8));
  }

  // 느림보꼬리 = 우선도 -0.5(사용자 결정): 같은 우선도에서만 나중
  [Test] public void LaggingTailIsHalfPriorityLower() {
   var slow = F(); slow.Data.@base[5] = 10; var tail = F("느림보꼬리");
   var same = Turn(tail, slow, M("몸통박치기"), M("몸통박치기"));
   Assert.That(same.Log.FindIndex(x => x.StartsWith(slow.Data.name + "의 몸통박치기")), Is.LessThan(same.Log.FindIndex(x => x.StartsWith(tail.Data.name + "의 몸통박치기"))), "same priority: tail goes last");
   var quick = Turn(F("느림보꼬리"), slow, M("전광석화"), M("몸통박치기"));
   Assert.That(quick.Log.FindIndex(x => x.Contains("의 전광석화")), Is.LessThan(quick.Log.FindIndex(x => x.Contains("의 몸통박치기"))), "higher priority still first");
  }

  [Test] public void UtilityUmbrellaIgnoresSunAndRainPower() {
   var sun = new Rules { Weather = "쾌청" };
   int plain = Engine.Calculate(TestData.Db, F(), F(), M("화염방사"), new Rules()).Max;
   Assert.That(Engine.Calculate(TestData.Db, F("만능우산"), F(), M("화염방사"), sun).Max, Is.EqualTo(plain));
   Assert.That(Engine.Calculate(TestData.Db, F(), F("만능우산"), M("화염방사"), sun).Max, Is.EqualTo(plain), "defending holder too");
  }

  // 주눅구슬: 상대에 의해 능력이 내려갔을 때만
  [Test] public void AdrenalineOrbNeedsOpponentCause() {
   var holder = F("주눅구슬"); var foe = F(); var rules = new Rules(); var log = new List<string>();
   using (EffectScope.Begin(new[] { holder, foe }, rules, () => .5, new[] { new TrainerBattleState(), new TrainerBattleState() })) {
    Mechanics.ChangeStage(holder, holder, 1, -1, rules, "자신", log);
    Assert.That(holder.Stages[5], Is.EqualTo(0), "self-inflicted");
    Mechanics.ChangeStage(holder, foe, 1, -1, rules, "상대", log);
   }
   Assert.That(holder.Stages[5], Is.EqualTo(1), string.Join("\n", log));
   Assert.That(holder.Data.item, Is.Empty);
  }

  // 특성 변경 모듈(미라인형): 교대하면 원래대로
  [Test] public void MummyDollChangesAttackerAbility() {
   var attacker = F(); attacker.Data.ability = "위협";
   var turn = Turn(attacker, F("미라인형"), M("몸통박치기"), Idle());
   Assert.That(AbilityRuntime.Name(turn.Fighters[0]), Is.EqualTo("미라"), string.Join("\n", turn.Log));
   Mechanics.LeaveField(turn.Fighters[0]);
   Assert.That(AbilityRuntime.Name(turn.Fighters[0]), Is.EqualTo("위협"));
  }

  [Test] public void RingTargetRemovesHolderImmunity() {
   Assert.That(Mechanics.TypeEffect(TestData.Db, F(), F("겨냥표적", "고스트"), M("몸통박치기"), new Rules()), Is.EqualTo(1));
   Assert.That(Mechanics.TypeEffect(TestData.Db, F(), F("", "고스트"), M("몸통박치기"), new Rules()), Is.EqualTo(0));
  }

  // 단타도 C 랭크 확률로 급소(1/16, C+1이면 1/8)
  [TestCase("", .1, false)]
  [TestCase("초점렌즈", .1, true)]
  [TestCase("", .05, true)]
  public void SingleHitRollsCriticalByStage(string item, double roll, bool critical) {
   var turn = Engine.Resolve(TestData.Db, new[] { F(item), F() }, new[] { M("몸통박치기"), Idle() }, new Rules(), () => roll);
   Assert.That(turn.Log.Any(x => x.Contains("급소!")), Is.EqualTo(critical), string.Join("\n", turn.Log));
  }


  // 추가효과 모듈
  [TestCase("자신의 기술에 「1할：풀죽음」의 추가효과를 부여한다", "자신의 기술에 「1할:풀죽음」의 추가효과를 부여한다.")]
  [TestCase("아군의 「기능확장」에 「2할:마비」를 부여한다", "자신의 「기능확장」 기술에 「2할:마비」의 추가효과를 부여한다.")]
  [TestCase("자신의 공격에 「3할:화상」의 추가효과를 부여한다", "자신의 공격에 「3할:화상」의 추가효과를 부여한다.")]
  public void NormalizesSecondaryEffect(string raw, string expected) {
   Assert.That(EffectNormalizer.Normalize(raw), Is.EqualTo(expected));
   Assert.That(EffectModules.Classify(expected), Is.EqualTo("추가효과 부여"));
  }

  // 왕의징표석: 먼저 맞힌 상대는 풀죽어 행동하지 못한다
  [Test] public void KingsRockFlinchesSlowerTarget() {
   var holder = F("왕의징표석"); holder.Data.@base[5] = 200; var target = F();
   var turn = Turn(holder, target, M("몸통박치기"), M("몸통박치기"), null);
   var rolled = Engine.Resolve(TestData.Db, new[] { holder, target }, new[] { M("몸통박치기"), M("몸통박치기") }, new Rules(), () => .05);
   Assert.That(rolled.Log.Any(x => x.Contains("풀죽어서 움직일 수 없다")), Is.True, string.Join("\n", rolled.Log));
   Assert.That(turn.Log.Any(x => x.Contains("풀죽어서")), Is.False, "roll .5 does not flinch");
  }

  // 행동 순서: 교대는 먼저 행동한 것으로 치지 않는다(사용자 결정)
  [Test] public void ActionOrderConditionsIgnoreSwitching() {
   Potential P(string trigger, string effect) { return new Potential { id = "종족 ①", name = "검사", description = effect, triggers = trigger.Split('\n') }; }
   var slow = new Fighter(new Pokemon { id = "slow", name = "느림", level = 100, types = new[] { "노말" }, item = "", @base = new[] { 100, 100, 100, 100, 100, 10 }, iv = new int[6], moves = new[] { "몸통박치기" }, potentials = new[] { P("T 종료 시\n상대가 자신보다 먼저 행동할 때", "자신의 「공격」이 오른다."), P("상대가 자신보다 먼저 행동했을 때", "자신의 「방어」가 오른다.") } });
   var turn = Turn(slow, F(), M("몸통박치기"), M("몸통박치기"));
   Assert.That(turn.Fighters[0].Stages[1], Is.EqualTo(1), "state at turn end");
   Assert.That(turn.Fighters[0].Stages[2], Is.EqualTo(1), "event before own action");
   var reserve = F();
   var switched = Engine.Resolve(TestData.Db, new[] { slow.Copy(), F() }, new[] { new BattleCommand { Move = M("몸통박치기") }, new BattleCommand { SwitchIn = reserve } }, new Rules(), null, () => .5);
   Assert.That(switched.Fighters[0].Stages[1], Is.EqualTo(0), "switching is not acting first");
  }

  // 기술머신(X): 배틀 중 한 번 X를 기능확장으로 내보낸다
  [Test] public void TechnicalMachineIsOneExtension() {
   var holder = F("기술머신(전광석화)");
   Assert.That(FunctionExtensions.Available(holder, F(), new Rules(), null, null), Is.EqualTo(new[] { "전광석화" }));
   var turn = Engine.Resolve(TestData.Db, new[] { holder, F() }, new[] { new BattleCommand { Move = M("전광석화"), Extension = true }, new BattleCommand { Move = Idle() } }, new Rules(), null, () => .5);
   Assert.That(turn.Fighters[0].Data.item, Is.Empty);
  }
 }
}
