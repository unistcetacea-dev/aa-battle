using System;
using System.Linq;
using System.Collections.Generic;
using NUnit.Framework;

namespace AABattle {
 // 남은 특성 모듈화 4차: 사용자 답변(예약 공격 추가효과 · 업로드 · 수치 · 상태 · 뜻)과 추가 답변(독연기 · 플라즈마가드 · 멀티태스크 · 화이부실 · 은밀)
 [TestFixture] public class AbilityRemaining4Tests {
  static Fighter F(string ability = "", string type = "노말", int hp = 0, string item = "") {
   var f = AbilityRemaining3MoveTests.F(ability, type, hp);
   if (item.Length > 0) { f.Data.item = item; ItemRuntime.Sync(f); }
   return f;
  }
  static Move M(string name) { return AbilityRemaining3MoveTests.M(name); }
  static Move Idle() { return AbilityRemaining3MoveTests.Idle(); }
  static TurnResult Turn(Fighter a, Fighter b, Move ma, Move mb, Rules rules = null, double roll = .5) {
   return Engine.Resolve(TestData.Db, new[] { a, b }, new[] { ma, mb }, rules ?? new Rules(), () => roll);
  }
  static string Log(TurnResult t) { return string.Join("\n", t.Log); }
  static int Max(Fighter f) { return Engine.Stats(f, 100, new Rules())[0]; }
  static EffectScope Scope(Fighter[] fighters, Rules rules) {
   return EffectScope.Begin(fighters, rules, () => .5, new[] { new TrainerBattleState(), new TrainerBattleState() }, TestData.Db);
  }

  // ----- 모든 새 문장은 자동으로 분류되고 한 턴을 오류 없이 돈다 -----
  public static IEnumerable<string> TableNames { get { return AbilitySentenceTable.Answered.Keys; } }
  [TestCaseSource("TableNames")]
  public void EveryRewrittenAbilityIsAutomaticAndRunsATurn(string ability) {
   var definition = AbilityCatalog.Find(ability);
   Assert.That(definition.support, Is.EqualTo("자동"), ability + " / " + definition.blocker);
   foreach (bool first in new[] { true, false }) {
    var holder = F(ability); var other = F();
    var rules = new Rules(); rules.OptionalEffectPrompt = (a, b, p) => true;
    var t = first ? Turn(holder, other, M("몸통박치기"), M("전기쇼크"), rules) : Turn(other, holder, M("전기쇼크"), M("몸통박치기"), rules);
    Assert.That(t.Log.Where(x => x.Contains("〔" + ability + "〕") && x.Contains("판정 대기")), Is.Empty, ability + "\n" + Log(t));
   }
  }

  // ----- 답변 1: 번지는불꽃 · 선독 · 파멸의알림 = 기술 데이터의 예약 공격을 추가효과로 건다 -----
  [TestCase("번지는불꽃", "자연발화")]
  [TestCase("선독", "미래예지")]
  [TestCase("파멸의알림", "파멸의소원")]
  public void SecondaryEffectSchedulesTheNamedDelayedMove(string ability, string expected) {
   var t = Turn(F(ability), F(), M("몸통박치기"), Idle(), null, 0);
   var delayed = t.Fighters[1].Conditions.DelayedAttacks;
   Assert.That(delayed.Count, Is.EqualTo(1), Log(t));
   Assert.That(delayed[0].Move.name, Is.EqualTo(expected));
   var data = TestData.Db.moves.First(m => m.name == expected);
   Assert.That(delayed[0].Move.power, Is.EqualTo(data.power));
   Assert.That(delayed[0].Move.types, Is.EqualTo(data.types));
  }
  [Test] public void SecondaryDelayedMoveRollsItsChanceAndDoesNotStack() {
   var miss = Turn(F("선독"), F(), M("몸통박치기"), Idle(), null, .9);
   Assert.That(miss.Fighters[1].Conditions.DelayedAttacks, Is.Empty, "4할을 넘는 난수");
   var target = F();
   target.Conditions.DelayedAttacks.Add(new DelayedAttackState { Move = M("미래예지").Copy(), SourceName = "먼저", TurnsRemaining = 2 });
   var t = Turn(F("선독"), target, M("몸통박치기"), Idle(), null, 0);
   Assert.That(t.Fighters[1].Conditions.DelayedAttacks.Count, Is.EqualTo(1), "이미 예약 공격이 있으면 겹치지 않는다\n" + Log(t));
   Assert.That(t.Fighters[1].Conditions.DelayedAttacks[0].SourceName, Is.EqualTo("먼저"));
   // 선독은 공격에만, 번지는불꽃은 기술이지만 변화 기술에는 추가효과가 없다
   var status = Turn(F("번지는불꽃"), F(), Idle(), Idle(), null, 0);
   Assert.That(status.Fighters[1].Conditions.DelayedAttacks, Is.Empty);
  }
  [Test] public void ScheduledSecondaryMoveLandsAtTheEndOfTheNextTurn() {
   var rules = new Rules();
   var t1 = Turn(F("선독"), F(), M("몸통박치기"), Idle(), rules, 0);
   int hp = t1.Fighters[1].HP;
   var t2 = Turn(t1.Fighters[0], t1.Fighters[1], Idle(), Idle(), t1.Rules, 0);

   Assert.That(t2.Fighters[1].HP, Is.LessThan(hp), Log(t2));
   Assert.That(t2.Log.Any(x => x.Contains("미래예지")), Is.True, Log(t2));
  }

  // ----- 답변 2: 업로드 = 다운로드와 같은 트리거 + 능력치 비교 -----
  [TestCase(100, 130, 4)]   // 공격 < 특공 -> 특방
  [TestCase(130, 100, 2)]   // 공격 > 특공 -> 방어
  [TestCase(100, 100, 2)]   // 공격 = 특공 -> 방어(공 >= 특공)
  public void UploadComparesAttackAndSpecialAttackOnEntry(int attack, int specialAttack, int expectedStat) {
   var foe = F(); foe.Data.@base[1] = attack; foe.Data.@base[3] = specialAttack;
   var t = Turn(F("업로드"), foe, Idle(), Idle());
   Assert.That(t.Fighters[0].Stages[expectedStat], Is.EqualTo(1), Log(t));
   Assert.That(t.Fighters[0].Stages.Skip(1).Take(5).Sum(), Is.EqualTo(1));
  }

  // ----- 답변 3: 수치 -----
  [Test] public void SludgeRainHealsPoisonTypesAndHurtsTheRestByTheSameAmount() {
   var poison = F("오물의비", "독", 0); var normal = F("", "노말", 0);
   poison.HP = Max(poison) / 2;
   int before = poison.HP;
   var t = Turn(poison, normal, Idle(), Idle());
   Assert.That(t.Fighters[0].HP - before, Is.EqualTo(Max(poison) / 8), Log(t));
   Assert.That(Max(normal) - t.Fighters[1].HP, Is.EqualTo(Max(normal) / 8));
   // 보유자가 독 타입이 아니면 보유자가 감소하고 독 타입 상대가 회복한다
   var holder = F("오물의비", "노말"); var foe = F("", "독"); foe.HP = Max(foe) / 2; int foeBefore = foe.HP;
   var t2 = Turn(holder, foe, Idle(), Idle());
   Assert.That(Max(holder) - t2.Fighters[0].HP, Is.EqualTo(Max(holder) / 8), Log(t2));
   Assert.That(t2.Fighters[1].HP - foeBefore, Is.EqualTo(Max(foe) / 8));
  }

  [Test]
  public void GreedRaisesTheSameStatByTheSameAmountWithoutRetriggering() {
   var holder = F("욕심쟁이");
   var t = Turn(holder, F(), M("칼춤"), Idle());
   Assert.That(t.Fighters[0].Stages[1], Is.EqualTo(4), "칼춤 +2 에 같은 +2가 더해지고 다시 발동하지 않는다\n" + Log(t));
   Assert.That(Log(t), Does.Contain("같은 연쇄에서 이미 발동"));
  }
  [Test] public void GreedIgnoresAStatDropAndOtherStats() {
   var holder = F("욕심쟁이"); var foe = F(); foe.Data.ability = "위협"; AbilityModules.Sync(foe);
   var t = Turn(holder, foe, Idle(), Idle());
   Assert.That(t.Fighters[0].Stages[1], Is.EqualTo(-1), "내려간 것에는 발동하지 않는다\n" + Log(t));
  }

  [TestCase(0, 1.0)]
  [TestCase(1, 1.2)]
  [TestCase(2, 1.3)]
  [TestCase(3, 1.4)]
  [TestCase(4, 1.5)]
  [TestCase(5, 1.6)]
  [TestCase(6, 1.6)]
  public void FallenAllyRateIsOnePointOnePlusTenthsCappedAtFive(int fallen, double expected) {
   Assert.That(AbilityMiscModules.FallenAllyRate(fallen), Is.EqualTo(expected).Within(1e-9));
   var holder = F("총대장"); holder.OnField = true;
   var party = new List<Fighter> { holder };
   for (int i = 0; i < fallen; i++) { var down = F(); down.HP = 0; party.Add(down); }
   var rules = new Rules(); rules.Parties = new[] { party.ToArray(), new Fighter[0] };
   var foe = F();
   Assert.That(PotentialModifiers.Stat(holder, foe, 1, rules), Is.EqualTo(expected).Within(1e-9), "공격");
   Assert.That(PotentialModifiers.Stat(holder, foe, 3, rules), Is.EqualTo(expected).Within(1e-9), "특공");
   Assert.That(PotentialModifiers.Stat(holder, foe, 2, rules), Is.EqualTo(1).Within(1e-9), "방어는 그대로");
  }

  // 「작은」 이하 = 최대 HP의 1/6 이하(1/6 경계: 이하는 회복, 초과는 발동하지 않는다)
  [TestCase(600, 100, true)]
  [TestCase(600, 101, false)]
  [TestCase(600, 1, true)]
  [TestCase(600, 0, false)]
  [TestCase(600, -50, false)]
  public void SmallDamageTriggerUsesTheOneSixthBoundary(int max, int damage, bool expected) {
   var spec = TriggerModules.Parse("「작은」 이하의 대미지를 받았을 때");
   Assert.That(spec.Module, Is.EqualTo("작은 대미지"));
   var fact = new TriggerFact { Kind = "체력", Self = true, Before = max, After2 = max - damage, Max = max };
   Assert.That(TriggerModules.Matches(spec, fact, null), Is.EqualTo(expected));
   Assert.That(TriggerModules.Matches(spec, new TriggerFact { Kind = "체력", Self = false, Before = max, After2 = max - 1, Max = max }, null), Is.False, "상대의 체력은 해당 없음");
  }
  [Test] public void ProliferationHealsAQuarterAfterASmallIndirectDamage() {
   var holder = F("증식"); holder.Status = "독"; holder.HP = Max(holder) / 2;
   int before = holder.HP;
   var t = Turn(holder, F(), Idle(), Idle());
   // 독 1/8 대미지(작은 대미지) 뒤 1/4 회복
   Assert.That(t.Fighters[0].HP, Is.EqualTo(before - Max(holder) / 8 + Max(holder) / 4), Log(t));
   // 큰 대미지는 발동하지 않는다
   var hurt = F("증식"); hurt.Status = "맹독"; hurt.Conditions.ToxicStage = 6; hurt.HP = Max(hurt) / 2;
   var big = Turn(hurt, F(), Idle(), Idle());
   Assert.That(big.Fighters[0].HP, Is.LessThan(hurt.HP), Log(big));
  }

  // ----- 답변 4: 빙산 = 얼음의벽 진영 상태 -----
  [Test] public void IcebergDeploysTheIceWallForThreeTurns() {
   var t = Turn(F("빙산"), F(), Idle(), Idle());
   Assert.That(t.Rules.Sides[0].IceWall, Is.EqualTo(2), "3T간(등장한 T의 종료 시 1 줄어든다)" + Log(t));
   Assert.That(t.Rules.Sides[1].IceWall, Is.Zero);
  }
  [TestCase("물리", "몸통박치기")]
  [TestCase("특수", "전기쇼크")]
  public void IceWallHalvesDamageOfBothCategoriesAndDoesNotStackWithOtherWalls(string category, string move)
  {
   var rules = new Rules();
   var m = M(move);
   Assert.That(m.category, Is.EqualTo(category));
   Assert.That(Mechanics.DamageFactor(F(), F(), m, rules, 1), Is.EqualTo(1));
   rules.Sides[1].IceWall = 3;
   Assert.That(Mechanics.DamageFactor(F(), F(), m, rules, 1), Is.EqualTo(.5));
   rules.Sides[1].Reflect = rules.Sides[1].LightScreen = 3;
   Assert.That(Mechanics.DamageFactor(F(), F(), m, rules, 1), Is.EqualTo(.5), "벽끼리 겹쳐도 한 번만 반감");
  }
  [Test] public void IceWallIsReleasedByAnOpponentsFireMoveButNotByOthers() {
   var fire = M("화염방사"); var plain = M("몸통박치기");
   var rules = new Rules(); rules.Sides[0].IceWall = 3;
   var log = new List<string>();
   IceWallRules.BreakByFire(plain, rules, 0, log);
   Assert.That(rules.Sides[0].IceWall, Is.EqualTo(3));
   IceWallRules.BreakByFire(fire, rules, 0, log);
   Assert.That(rules.Sides[0].IceWall, Is.Zero, string.Join("\n", log));
   // 실제 턴: 상대가 불꽃 기술을 내보내면 그 대미지는 반감되지 않고 벽이 사라진다
   var holder = F("빙산"); var foe = F();
   var t = Turn(holder, foe, Idle(), fire);
   Assert.That(t.Rules.Sides[0].IceWall, Is.Zero, Log(t));
   var t2 = Turn(F("빙산"), F(), Idle(), plain);
   Assert.That(t2.Rules.Sides[0].IceWall, Is.GreaterThan(0), Log(t2));
   Assert.That(t2.Fighters[0].HP, Is.GreaterThan(Turn(F(), F(), Idle(), plain).Fighters[0].HP), "벽이 있으면 덜 맞는다");
  }
  [Test] public void IceWallIsClearedByScreenBreakersAndFieldClearers() {
   var rules = new Rules(); rules.Sides[1].IceWall = 3;
   MoveModules.BreakScreens(F(), F(), M("깨트리기"), rules, 0, new List<string>());
   Assert.That(rules.Sides[1].IceWall, Is.Zero, "깨트리기");
   var wall = new Rules(); wall.Sides[0].IceWall = 3;
   var t = Turn(F(), F("배리어프리"), Idle(), Idle(), wall);
   Assert.That(t.Rules.Sides[0].IceWall, Is.Zero, "벽 해제: " + Log(t));
   Assert.That(new SideFieldState { IceWall = 2 }.Any(), Is.True);
   var cleared = new SideFieldState { IceWall = 2 }; cleared.Clear();
   Assert.That(cleared.IceWall, Is.Zero);
  }
  [Test] public void IceWallMoveSetsTheSameSideState() {
   var t = Turn(F(), F(), M("얼음의벽"), Idle());
   Assert.That(t.Rules.Sides[0].IceWall, Is.GreaterThan(0), Log(t));
  }
  [Test] public void IceWallMoveAndStateSentenceShareTheSameState() {
   var rules = new Rules(); var a = F(); var b = F();
   Mechanics.SetParties(rules, new[] { a, b });
   Assert.That(StateEffects.TryApply("자신을 「얼음의벽」상태로 한다", a, b, rules, () => .5, new List<string>(), "검사"), Is.True);
   Assert.That(rules.Sides[0].IceWall, Is.EqualTo(IceWallRules.DefaultTurns));
   Assert.That(StateEffects.Apply("자신을 5T간 「얼음의벽」상태로 한다", a, b, rules, () => .5, new List<string>(), "검사"), Is.EqualTo(EffectOutcome.Failed), "이미 전개되어 있으면 실패");
   Assert.That(rules.Sides[0].IceWall, Is.EqualTo(IceWallRules.DefaultTurns), "남은 턴 유지");
  }

  // ----- 답변 4: 은밀 = 어둠안(땅속과 다른 숨는 곳) -----
  [Test] public void StealthHidesInTheDarkAndIsReleasedWhenTheHolderActs() {
   var holder = F("은밀"); holder.Data.@base[5] = 10;   // 느리게
   var foe = F();
   var t = Turn(holder, foe, Idle(), M("몸통박치기"));
   Assert.That(t.Fighters[0].HP, Is.EqualTo(Max(holder)), "어둠안에 있으면 맞지 않는다\n" + Log(t));
   Assert.That(Log(t), Does.Contain("어둠안"));
   Assert.That(t.Fighters[0].Conditions.Hidden, Is.Empty, "자신이 행동했으므로 풀렸다");
   Assert.That(Mechanics.HasCondition(new Fighter(holder.Data) { }, "땅속"), Is.False);
  }
  [Test] public void StealthIsReleasedByItsOwnActionBeforeTheOpponentMovesWhenFaster() {
   var holder = F("은밀"); holder.Data.@base[5] = 200;
   var t = Turn(holder, F(), Idle(), M("몸통박치기"));
   Assert.That(t.Fighters[0].HP, Is.LessThan(Max(holder)), Log(t));
  }
  [Test] public void NoGuardAndMindReaderReachTheDarkness() {
   var holder = F("은밀"); holder.Data.@base[5] = 10;
   var t = Turn(holder, F("노가드"), Idle(), M("몸통박치기"));
   Assert.That(t.Fighters[0].HP, Is.LessThan(Max(holder)), "노가드는 닿는다\n" + Log(t));
   var held = F(); held.Conditions.Hidden = "땅속";
   Assert.That(MoveModules.Reaches(F(), held, M("몸통박치기"), -1), Is.False, "땅속과 같은 숨기 규칙");
   var dark = F(); dark.Conditions.Hidden = "어둠안";
   Assert.That(MoveModules.Reaches(F(), dark, M("몸통박치기"), -1), Is.False);
   Assert.That(MoveModules.Reaches(F("노가드"), dark, M("몸통박치기"), -1), Is.True);
  }

  // ----- 답변 5: 더블 배틀 사양은 효과 없음 -----
  [TestCase("굳건한신념")]
  [TestCase("스크루지느러미")]
  [TestCase("텔레파시")]
  public void MultiBattleOnlyAbilitiesAreNoEffectAutomatic(string ability) {
   var definition = AbilityCatalog.Find(ability);
   Assert.That(definition.support, Is.EqualTo("자동"));
   Assert.That(definition.blocker, Is.EqualTo(AbilityNoEffectModules.Reason));
  }
  [Test] public void PickupIsNoEffectAutomatic() {
   var definition = AbilityCatalog.Find("픽업");
   Assert.That(definition.support, Is.EqualTo("자동"));
   Assert.That(definition.blocker, Is.EqualTo(AbilityNoEffectModules.Reason));
  }

  // ----- 답변 5: 트랜스 = 상대의 가장 높은 종족치를 자신의 같은 능력에 복사 -----
  [Test] public void TransCopiesTheOpponentsHighestBaseStatIntoTheSameStat() {
   var foe = F(); foe.Data.@base = new[] { 100, 60, 70, 140, 80, 90 };
   var holder = F("트랜스");
   var t = Turn(holder, foe, Idle(), Idle());
   Assert.That(t.Fighters[0].Data.@base[3], Is.EqualTo(140), Log(t));
   Assert.That(t.Fighters[0].Data.@base[1], Is.EqualTo(100), "다른 능력은 그대로");
   Assert.That(t.Fighters[0].Conditions.OriginalBase, Is.Not.Null, "퇴장하면 복원할 원래 값을 기억한다");
   Assert.That(t.Fighters[0].Conditions.OriginalBase[3], Is.EqualTo(100));
  }

  // ----- 답변 5: 파수견 = 위협의 하락은 막고 공격이 오른다 -----
  [Test] public void WatchdogTurnsIntimidateIntoAnAttackRise() {
   var t = Turn(F("파수견"), F("위협"), Idle(), Idle());
   Assert.That(t.Fighters[0].Stages[1], Is.EqualTo(1), Log(t));
   var control = Turn(F(), F("위협"), Idle(), Idle());
   Assert.That(control.Fighters[0].Stages[1], Is.EqualTo(-1));
   var other = Turn(F("파수견"), F("꿀모으기"), Idle(), Idle());
   Assert.That(other.Fighters[0].Stages[1], Is.Zero, "〔위협〕이 아닌 하락에는 반응하지 않는다");
  }

  // ----- 추가 답변 1: 독연기 -----
  [Test] public void PoisonSmokePoisonsTheSourceWhenItNullifiesADrop() {
   var t = Turn(F("독연기"), F("위협"), Idle(), Idle());
   Assert.That(t.Fighters[0].Stages[1], Is.Zero, "하락은 무효");
   Assert.That(t.Fighters[1].Status, Is.EqualTo("독"), Log(t));
   var plain = Turn(F("클리어바디"), F("위협"), Idle(), Idle());
   Assert.That(plain.Fighters[1].Status, Is.EqualTo("정상"), "독연기가 아니면 독이 되지 않는다");
   var none = Turn(F("독연기"), F(), Idle(), Idle());
   Assert.That(none.Fighters[1].Status, Is.EqualTo("정상"), "하락을 막은 적이 없으면 발동하지 않는다");
  }

  // ----- 추가 답변 2: 플라즈마가드 -----
  [Test] public void PlasmaGuardParalyzesAnAttackerOnlyWhenHitAtFullHp() {
   var full = Turn(F("플라즈마가드"), F(), Idle(), M("몸통박치기"));
   Assert.That(full.Fighters[1].Status, Is.EqualTo("마비"), Log(full));
   var holder = F("플라즈마가드"); holder.HP = Max(holder) - 1;
   var notFull = Turn(holder, F(), Idle(), M("몸통박치기"));
   Assert.That(notFull.Fighters[1].Status, Is.EqualTo("정상"), "피격 순간 체력이 가득이 아니면 발동하지 않는다");
   var status = Turn(F("플라즈마가드"), F(), Idle(), Idle());
   Assert.That(status.Fighters[1].Status, Is.EqualTo("정상"));
  }

  // ----- 추가 답변 3: 멀티태스크 -----
  [Test] public void MultitaskUsesTheHeldMachineAfterTheRegularActionOnlyAtFullHp() {
   var rules = new Rules(); rules.OptionalEffectPrompt = (a, b, p) => true;
   var holder = F("멀티태스크", "노말", 0, "기술머신(화염방사)"); var foe = F();
   var t = Turn(holder, foe, M("몸통박치기"), Idle(), rules);
   Assert.That(Log(t), Does.Contain("화염방사"), Log(t));
   Assert.That(t.Fighters[0].Data.item, Is.Empty, "기술머신은 기능확장으로 쓰면 소모된다\n" + Log(t));
   var ext = Log(t).Split('\n').Count(x => x.Contains("화염방사!"));
   Assert.That(ext, Is.EqualTo(1), "추가행동은 한 번(추가행동 중에는 다시 얻지 않는다)\n" + Log(t));
   var hurt = F("멀티태스크", "노말", 0, "기술머신(화염방사)"); hurt.HP = Max(hurt) - 1;
   var t2 = Turn(hurt, F(), M("몸통박치기"), Idle(), rules);
   Assert.That(Log(t2), Does.Not.Contain("화염방사"), "체력이 가득이 아니면 쓸 수 없다");
   var none = Turn(F("멀티태스크"), F(), M("몸통박치기"), Idle(), rules);
   Assert.That(Log(none), Does.Not.Contain("추가행동을 얻었다"), "기술머신이 없으면 해당 없음");
  }
  [Test] public void MultitaskIsOptionalSoItDoesNotRunWithoutAnAnswer() {
   var holder = F("멀티태스크", "노말", 0, "기술머신(화염방사)");
   var t = Turn(holder, F(), M("몸통박치기"), Idle(), new Rules());
   Assert.That(t.Fighters[0].Data.item, Is.EqualTo("기술머신(화염방사)"), "「할 수 있다」는 선택 창이 없으면 쓰지 않는다\n" + Log(t));
   var declined = new Rules(); declined.OptionalEffectPrompt = (a, b, p) => false;
   var t2 = Turn(F("멀티태스크", "노말", 0, "기술머신(화염방사)"), F(), M("몸통박치기"), Idle(), declined);
   Assert.That(t2.Fighters[0].Data.item, Is.EqualTo("기술머신(화염방사)"));
  }

  // ----- 추가 답변 4: 화이부실 -----
  [Test] public void SharingGivesTheHolderTheItemEffectTheOpponentUsed() {
   var rules = new Rules();
   var holder = F("화이부실", "노말", 0, "구애스카프"); var foe = F("", "노말", 0, "치리열매");
   using (Scope(new[] { holder, foe }, rules)) {
    var log = new List<string>();
    ItemRuntime.Lose(foe, "소비", log.Add);
    Assert.That(holder.Stages[1], Is.EqualTo(1), "치리열매(공격 상승)의 효과를 자신도 받는다\n" + string.Join("\n", log));
    Assert.That(holder.Data.item, Is.EqualTo("구애스카프"), "자신의 소지품은 그대로");
    Assert.That(holder.LostBerry, Is.Empty, "빌린 열매는 소모 기록을 남기지 않는다");
   }
   // 소비가 아닌 파괴는 해당 없음
   var holder2 = F("화이부실"); var foe2 = F("", "노말", 0, "치리열매");
   using (Scope(new[] { holder2, foe2 }, rules)) {
    ItemRuntime.Lose(foe2, "파괴", new List<string>().Add);
    Assert.That(holder2.Stages[1], Is.Zero);
   }
   // 자신이 소모한 소지품에는 반응하지 않는다
   var self = F("화이부실", "노말", 0, "치리열매");
   using (Scope(new[] { self, F() }, rules)) {
    ItemRuntime.Lose(self, "소비", new List<string>().Add);
    Assert.That(self.Stages[1], Is.Zero);
   }
   // 소지품이 없는 보유자도 받는다
   var empty = F("화이부실"); var foe3 = F("", "노말", 0, "치리열매");
   using (Scope(new[] { empty, foe3 }, rules)) {
    ItemRuntime.Lose(foe3, "소비", new List<string>().Add);
    Assert.That(empty.Stages[1], Is.EqualTo(1));
    Assert.That(empty.Data.item, Is.Empty);
   }
  }

  // ----- 새 트리거 모듈의 표준 문장 -----
  [TestCase("「작은」 이하의 대미지를 받았을 때", "작은 대미지")]
  [TestCase("「작은」이하의 대미지를 받았을 때", "작은 대미지")]
  [TestCase("상대에 의한 능력 하락을 무효화했을 때", "하락 무효")]
  [TestCase("상대의 〔위협〕을 받았을 때", "위협 받음")]
  [TestCase("체력이 가득인 상태로 공격을 받았을 때", "가득 피격")]
  public void NewTriggerModulesParseToStandardSentences(string text, string module) {
   Assert.That(TriggerModules.Parse(TriggerStructures.Normalize(text)).Module, Is.EqualTo(module));
   Assert.That(TriggerModules.IsModule(TriggerModules.Normalize(text)), Is.True);
   Assert.That(TriggerModules.Parse(TriggerModules.Normalize(text)).IsEvent, Is.True);
  }
 }
}
