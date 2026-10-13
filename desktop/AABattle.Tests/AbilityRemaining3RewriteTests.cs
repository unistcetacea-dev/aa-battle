using System;
using System.Linq;
using System.Collections.Generic;
using NUnit.Framework;

namespace AABattle {
 // 남은 특성 모듈화 3차: 기존 모듈로 옮긴 도감 문장이 실제로 그 효과를 내는지 모듈 단위로 확인한다
 [TestFixture] public class AbilityRemaining3RewriteTests {
  static Fighter F(string ability = "", string type = "노말", int hp = 0) { return AbilityRemaining3MoveTests.F(ability, type, hp); }
  static Move M(string name) { return AbilityRemaining3MoveTests.M(name); }
  static Move Idle() { return AbilityRemaining3MoveTests.Idle(); }
  static Move Custom(string name, string category, int power, string type, params string[] tags) {
   return new Move { name = name, category = category, types = new[] { type }, power = power, accuracy = 100, target = "상대", tags = tags, effect = "" };
  }
  static BattleCommand Cmd(Move m) { return new BattleCommand { Move = m }; }
  static TurnResult Turn(Fighter a, Fighter b, Move ma, Move mb, Rules r = null, Func<double> rng = null) {
   return Engine.Resolve(TestData.Db, new[] { a, b }, new[] { ma, mb }, r ?? new Rules(), rng ?? (() => .5));
  }
  static TrainerBattleState[] States() { return new[] { new TrainerBattleState(), new TrainerBattleState() }; }
  static int Max(Fighter f) { return Mechanics.MaxHP(f, F(), new Rules()); }

  // ----- 수치 배율 -----
  [Test] public void ScalarRewritesApplyTheirRates() {
   var rules = new Rules();
   var foe = F();
   Assert.That(PotentialModifiers.Damage(foe, F("건조피부"), Custom("불", "특수", 40, "불꽃"), rules, 1), Is.EqualTo(1.25));
   Assert.That(PotentialModifiers.Damage(foe, F("다한증"), Custom("물", "특수", 40, "물"), rules, 1), Is.EqualTo(1.25));
   Assert.That(PotentialModifiers.Damage(foe, F("대요새"), Custom("전체", "특수", 40, "노말"), rules, 1), Is.EqualTo(1), "전체 기술이 아니면 그대로");
   var spread = Custom("전체기술", "특수", 40, "노말"); spread.target = "전체";
   Assert.That(PotentialModifiers.Damage(foe, F("대요새"), spread, rules, 1), Is.EqualTo(.5));
   Assert.That(PotentialModifiers.Damage(foe, F("딱딱한등딱지"), Custom("탄", "특수", 40, "노말", "탄/볼"), rules, 1), Is.EqualTo(.5));
   Assert.That(PotentialModifiers.Damage(foe, F("정화의소금"), Custom("고스트", "특수", 40, "고스트"), rules, 1), Is.EqualTo(.5));
   Assert.That(PotentialModifiers.Damage(foe, F("얼어붙는불꽃"), Custom("불", "특수", 40, "불꽃"), rules, 1), Is.EqualTo(.5));
   Assert.That(PotentialModifiers.Power(foe, F("불타는피"), Custom("물", "특수", 40, "물"), rules, 1), Is.EqualTo(.5));
   Assert.That(PotentialModifiers.Power(foe, F("특화점"), Custom("탄", "특수", 40, "노말", "탄/볼"), rules, 1), Is.EqualTo(.5));
   Assert.That(PotentialModifiers.Power(F("특화점"), foe, Custom("탄", "특수", 40, "노말", "탄/볼"), rules, 1), Is.EqualTo(1.5));
   Assert.That(PotentialModifiers.Power(F("악마의주먹"), foe, Custom("펀치", "물리", 40, "노말", "펀치"), rules, 1), Is.EqualTo(1.3));
   Assert.That(PotentialModifiers.Power(F("단단한발톱"), foe, Custom("접촉", "물리", 40, "노말", "접촉"), rules, 1), Is.EqualTo(1.3));
   Assert.That(PotentialModifiers.Accuracy(F("킥의귀신"), foe, Custom("킥", "물리", 40, "격투", "킥"), rules), Is.EqualTo(1.33));
   Assert.That(PotentialModifiers.Accuracy(F("악마의꼬리"), foe, Custom("꼬리", "물리", 40, "노말", "꼬리"), rules), Is.EqualTo(1.3));
   Assert.That(MiscModules.CritBonus(F("단단한발톱"), foe, M("몸통박치기"), rules), Is.EqualTo(1));
   Assert.That(MiscModules.CritBonus(F("행운"), foe, M("몸통박치기"), rules), Is.EqualTo(1));
  }
  [Test] public void TypeEffectivenessBasedPowerRewrites() {
   var rules = new Rules(); var move = M("몸통박치기");
   Assert.That(PotentialModifiers.Power(F("브레인포스"), F(), move, rules, 2), Is.EqualTo(1.2));
   Assert.That(PotentialModifiers.Power(F("브레인포스"), F(), move, rules, 1), Is.EqualTo(1));
   Assert.That(PotentialModifiers.Power(F("색안경"), F(), move, rules, .5), Is.EqualTo(2));
   Assert.That(PotentialModifiers.Power(F(), F("필터"), move, rules, 2), Is.EqualTo(.67));
   Assert.That(PotentialModifiers.Power(F(), F("하드록"), move, rules, 2), Is.EqualTo(.67));
   Assert.That(PotentialModifiers.Power(F(), F("순백"), move, rules, 2), Is.EqualTo(.67));
   Assert.That(PotentialModifiers.Power(F(), F("하드프로텍트"), move, rules, 1), Is.EqualTo(1));
   Assert.That(PotentialModifiers.Damage(F(), F("신비의스푼"), move, rules, 1), Is.EqualTo(.5));
   Assert.That(PotentialModifiers.Damage(F(), F("신비의스푼"), move, rules, 2), Is.EqualTo(1), "효과가 굉장하면 그대로");
   var water = F("", "물"); var steel = F("", "강철");
   Assert.That(PotentialModifiers.Power(F("수은"), water, move.Copy(), rules, 1), Is.EqualTo(1), "독/강철 기술이 아니면 그대로");
   var poison = Custom("독", "물리", 40, "독");
   Assert.That(PotentialModifiers.Power(F("수은"), water, poison, rules, 1), Is.EqualTo(2));
   Assert.That(PotentialModifiers.Power(F("수은"), F(), poison, rules, 1), Is.EqualTo(1), "물 타입이 아니면 그대로");
   Assert.That(PotentialModifiers.Power(F("대영웅"), F(), M("전기쇼크"), rules, 1), Is.EqualTo(1.5), "타입 불일치");
   Assert.That(PotentialModifiers.Power(F("대영웅"), F(), M("몸통박치기"), rules, 1), Is.EqualTo(1), "타입 일치");
   Assert.That(PotentialModifiers.Power(F(), F("대영웅"), M("전기쇼크"), rules, 1), Is.EqualTo(.67).Within(.001), "상대의 타입 불일치 기술은 약화");
   Assert.That(PotentialModifiers.Power(F(), F("대영웅"), M("몸통박치기"), rules, 1), Is.EqualTo(1), "상대의 타입 일치 기술은 그대로");
   Assert.That(PotentialModifiers.Power(F("대영웅"), F("대영웅"), M("전기쇼크"), rules, 1), Is.EqualTo(1.5 * .67).Within(.01));
  }
  [Test] public void StateConditionedScalarRewrites() {
   var rules = new Rules();
   var dizzy = F("갈지자걸음"); dizzy.Conditions.Confusion = 3;
   Assert.That(PotentialModifiers.Stat(dizzy, F(), 7, rules), Is.EqualTo(2));
   Assert.That(PotentialModifiers.Stat(F("갈지자걸음"), F(), 7, rules), Is.EqualTo(1));
   var half = F("달마모드", "노말", 40);
   Assert.That(PotentialModifiers.Stat(half, F(), 2, rules), Is.EqualTo(2));
   Assert.That(PotentialModifiers.Stat(F("달마모드"), F(), 2, rules), Is.EqualTo(1), "체력이 가득이면 그대로");
   var weak = F("의욕");
   Assert.That(PotentialModifiers.Stat(weak, F(), 1, rules), Is.EqualTo(1.5));
   Assert.That(PotentialModifiers.Stat(weak, F(), 6, rules), Is.EqualTo(.8));
   rules.Weather = "모래바람";
   Assert.That(PotentialModifiers.Stat(F("모래의힘"), F(), 1, rules), Is.EqualTo(1.5));
   rules.Weather = "싸라기눈";
   Assert.That(PotentialModifiers.Stat(F("얼음귀신"), F(), 1, rules), Is.EqualTo(1.5));
   rules.Weather = "큰가뭄";
   var gift = F("플라워기프트");
   Assert.That(PotentialModifiers.Stat(gift, F(), 1, rules), Is.EqualTo(1.5));
   Assert.That(PotentialModifiers.Stat(gift, F(), 4, rules), Is.EqualTo(1.5));
   Assert.That(PotentialModifiers.Stat(gift, F(), 2, rules), Is.EqualTo(1));
   var burning = F("열폭주"); burning.Status = "화상";
   Assert.That(PotentialModifiers.Power(burning, F(), M("몸통박치기"), rules, 1), Is.EqualTo(1.5));
   Assert.That(AbilityPhrases.IgnoresStatusPowerPenalty(burning, "화상"), Is.True);
   Assert.That(PotentialModifiers.Power(F("열폭주"), F(), M("몸통박치기"), rules, 1), Is.EqualTo(1), "화상이 아니면 그대로");
   var late = F("애널라이즈"); var early = F(); early.Conditions.ActionOrder = 1; late.Conditions.ActionOrder = 2;
   Assert.That(PotentialModifiers.Damage(late, early, M("몸통박치기"), rules, 1), Is.EqualTo(1.3));
   late.Conditions.ActionOrder = 1; early.Conditions.ActionOrder = 2;
   Assert.That(PotentialModifiers.Damage(late, early, M("몸통박치기"), rules, 1), Is.EqualTo(1), "먼저 행동했으면 그대로");
  }

  // ----- 방호 · 상태 면역 · 순서 -----
  [Test] public void ProtectionRewrites() {
   var rules = new Rules();
   Assert.That(PotentialProtection.Check(TestData.Db, F(), F("공주님"), Custom("전체", "특수", 40, "노말"), rules, () => .5, false), Is.Null, "전체기술이 아니면 막지 않는다");
   var spread = Custom("전체기술", "특수", 40, "노말"); spread.target = "전체";
   Assert.That(PotentialProtection.Check(TestData.Db, F(), F("공주님"), spread, rules, () => .5, false), Is.Not.Null);
   var status = Custom("변화검사", "변화", 0, "노말");
   Assert.That(PotentialProtection.Check(TestData.Db, F(), F("황금몸"), status, rules, () => .5, false), Is.Not.Null);
   Assert.That(PotentialProtection.Check(TestData.Db, F(), F("황금몸"), M("몸통박치기"), rules, () => .5, false), Is.Null);
   Assert.That(PotentialProtection.Check(TestData.Db, F(), F("하드프로텍트"), Custom("탄", "특수", 40, "노말", "탄/볼"), rules, () => .5, false), Is.Not.Null);
   Assert.That(PotentialProtection.Check(TestData.Db, F(), F("영역"), Custom("설치", "변화", 0, "바위", "설치"), rules, () => .5, false), Is.Not.Null);
   foreach (string ability in new[] { "검의달인", "칼솜씨", "행운", "이도류", "대난투" })
    Assert.That(PotentialProtection.Check(TestData.Db, F(), F(ability), M("몸통박치기"), rules, () => .1, false), Is.Not.Null.And.Property("Key").EqualTo("회피"), ability);
   Assert.That(PotentialProtection.Check(TestData.Db, F(), F("칼솜씨"), M("몸통박치기"), rules, () => .5, false), Is.Null, "2할 실패");
   // 불가사의부적: 등장 후 3T 동안 효과가 굉장한 공격만 통한다
   var t = Turn(F("불가사의부적"), F(), Idle(), Idle());
   var guard = PotentialProtection.Check(TestData.Db, F(), t.Fighters[0], M("몸통박치기"), t.Rules, () => .5, false);
   Assert.That(guard, Is.Not.Null, string.Join("\n", t.Log));
  }
  [Test] public void StatusAndDropImmunityRewrites() {
   var rules = new Rules(); var foe = F();
   Assert.That(StateEffects.BlockedCondition(F("정신력"), foe, "풀죽음", rules, false, new List<string>()), Is.True);
   Assert.That(StateEffects.BlockedCondition(F("암살술"), foe, "풀죽음", rules, false, new List<string>()), Is.True);
   foreach (string state in new[] { "헤롱헤롱", "앙코르", "트집", "사슬묶기", "도발", "회복봉인" })
    Assert.That(StateEffects.BlockedCondition(F("아로마베일"), foe, state, rules, false, new List<string>()), Is.True, state);
   Assert.That(AbilityPhrases.BlocksDrop(F("플라워베일"), 1), Is.True);
   Assert.That(AbilityPhrases.BlocksDrop(F("일기당천"), 3), Is.True);
   Assert.That(AbilityPhrases.BlocksDrop(F("독연기"), 5), Is.True);
  }
  [Test] public void PriorityAndSpeedRewrites() {
   var fly = Custom("비행검사", "물리", 40, "비행"); var poison = Custom("독검사", "물리", 40, "독"); var poisonQuick = poison.Copy(); poisonQuick.priority = 1;
   Assert.That(AbilityPhrases.PriorityBonus(F("질풍날개"), fly), Is.EqualTo(1));
   Assert.That(AbilityPhrases.PriorityBonus(F("독단전횡"), poison), Is.EqualTo(1));
   Assert.That(AbilityPhrases.PriorityBonus(F("독단전횡"), poisonQuick), Is.EqualTo(0), "우선도 ±0일 때만");
   Assert.That(MiscModules.Lags(F("시간벌기"), F(), new Rules()), Is.True);
  }

  // ----- 사건: 등장 · 피격 · 종료 -----
  [TestCase("깨어진갑옷", "물리", 2, -1, 5, 2)]
  [TestCase("벗겨진도금", "특수", 4, -1, 5, 1)]
  public void StatSwapOnBeingHitRaisesSpeedAndLowersDefence(string ability, string category, int lowered, int lowDelta, int speed, int speedDelta) {
   var holder = F(ability);
   var move = Custom("속성검사", category, 40, "노말");
   var t = Turn(F(), holder, move, Idle());
   Assert.That(t.Fighters[1].Stages[lowered], Is.EqualTo(lowDelta), string.Join("\n", t.Log));
   Assert.That(t.Fighters[1].Stages[speed], Is.EqualTo(speedDelta));
   var other = Custom("다른분류", category == "물리" ? "특수" : "물리", 40, "노말");
   var t2 = Turn(F(), F(ability), other, Idle());
   Assert.That(t2.Fighters[1].Stages[speed], Is.EqualTo(0), "분류가 다르면 발동하지 않는다");
  }
  [TestCase("깨어진칼날", "물리", 1, -1, 5, 1)]
  [TestCase("힘밀기", "물리", 1, -2, 5, 0)]
  public void StatSwapOnHittingAppliesToTheAttacker(string ability, string category, int lowered, int lowDelta, int speed, int speedDelta) {
   var holder = F(ability);
   var t = Turn(holder, F(), Custom("분류검사", category, 40, "노말"), Idle());
   Assert.That(t.Fighters[0].Stages[lowered], Is.EqualTo(lowDelta), string.Join("\n", t.Log));
   Assert.That(t.Fighters[0].Stages[speed], Is.EqualTo(speedDelta));
  }
  [Test] public void BerserkShellAndCourageReactToHealthAndDrops() {
   var holder = F("분노의껍질"); holder.HP = Max(holder) / 2 + 20;
   var t = Turn(F(), holder, Custom("강타", "물리", 60, "노말"), Idle());
   Assert.That(t.Fighters[1].HP, Is.LessThanOrEqualTo(Max(holder) / 2));
   Assert.That(t.Fighters[1].Stages[1], Is.EqualTo(1), string.Join("\n", t.Log));
   Assert.That(t.Fighters[1].Stages[3], Is.EqualTo(1));
   Assert.That(t.Fighters[1].Stages[5], Is.EqualTo(1));
   Assert.That(t.Fighters[1].Stages[2], Is.EqualTo(-1));
   Assert.That(t.Fighters[1].Stages[4], Is.EqualTo(-1));
   var brave = F("넘치는용기"); var foe = F();
   using (EffectScope.Begin(new[] { brave, foe }, new Rules(), () => .5, new[] { new TrainerBattleState(), new TrainerBattleState() })) {
    var log = new List<string>();
    Mechanics.ChangeStage(brave, foe, 2, -1, new Rules(), "검사", log);
    Assert.That(brave.Stages[1], Is.EqualTo(2), string.Join("\n", log));
    Assert.That(brave.Stages[3], Is.EqualTo(2));
   }
  }
  [Test] public void EntryEffectRewrites() {
   var t = Turn(F("군신"), F(), Idle(), Idle());
   Assert.That(PotentialModifiers.Stat(t.Fighters[0], t.Fighters[1], 1, t.Rules), Is.EqualTo(1.5), string.Join("\n", t.Log));
   Assert.That(PotentialModifiers.Stat(t.Fighters[0], t.Fighters[1], 5, t.Rules), Is.EqualTo(1.5));
   Assert.That(PotentialModifiers.Stat(t.Fighters[0], t.Fighters[1], 2, t.Rules), Is.EqualTo(1));
   var guard = Turn(F("문지기"), F(), Idle(), Idle());
   Assert.That(PotentialModifiers.Stat(guard.Fighters[0], guard.Fighters[1], 2, guard.Rules), Is.EqualTo(1.5));
   Assert.That(PotentialModifiers.Stat(guard.Fighters[0], guard.Fighters[1], 4, guard.Rules), Is.EqualTo(1.5));
   var oni = Turn(F("호국의귀신"), F(), Idle(), Idle());
   Assert.That(PotentialModifiers.Stat(oni.Fighters[0], oni.Fighters[1], 4, oni.Rules), Is.EqualTo(1.5));
   // 꿀모으기·프레셔·기묘한약·아쿠아미라쥬
   var honey = Turn(F("꿀모으기"), F(), Idle(), Idle());
   Assert.That(honey.Fighters[1].EvasionStage, Is.EqualTo(-2), string.Join("\n", honey.Log));
   var pressure = Turn(F("프레셔"), F(), Idle(), Idle());
   Assert.That(pressure.Fighters[1].Stages[5], Is.EqualTo(-1));
   var aqua = Turn(F("아쿠아미라쥬"), F(), Idle(), Idle());
   Assert.That(aqua.Fighters[0].Stages[2], Is.EqualTo(1));
   Assert.That(aqua.Fighters[0].Conditions.AquaRing, Is.True);
   var shot = Turn(F("위협사격"), F(), Idle(), Idle());
   Assert.That(shot.Fighters[1].Stages[1], Is.EqualTo(-1));
   Assert.That(shot.Fighters[1].HP, Is.EqualTo(Max(shot.Fighters[1]) - Max(shot.Fighters[1]) / 8), string.Join("\n", shot.Log));
   var low = F("", "노말", 3);
   var finish = Turn(F("위협사격"), low, Idle(), Idle());
   Assert.That(finish.Fighters[1].HP, Is.EqualTo(1), "이 대미지로는 빈사로 할 수 없다");
   var mud = Turn(F("진흙투성이"), F(), Idle(), Idle());
   Assert.That(mud.Rules.MudSport, Is.GreaterThan(0), string.Join("\n", mud.Log));
  }
  [Test] public void TurnEndRewrites() {
   var rules = new Rules { Weather = "큰가뭄" };
   var dry = F("건조피부");
   var t = Turn(dry, F(), Idle(), Idle(), rules);
   Assert.That(t.Fighters[0].HP, Is.LessThan(dry.HP), "쾌청 계열에서는 1/8 대미지: " + string.Join("\n", t.Log));
   var rainy = F("건조피부", "노말", Max(dry) / 2);
   var t2 = Turn(rainy, F(), Idle(), Idle(), new Rules { Weather = "강한 비" });
   Assert.That(t2.Fighters[0].HP, Is.EqualTo(rainy.HP + Max(dry) / 8), string.Join("\n", t2.Log));
   var sweat = F("다한증", "노말", Max(dry) / 2);
   var t3 = Turn(sweat, F(), Idle(), Idle(), new Rules { Weather = "쾌청" });
   Assert.That(t3.Fighters[0].HP, Is.EqualTo(sweat.HP + Max(dry) / 8));
   var t4 = Turn(F("다한증"), F(), Idle(), Idle(), new Rules { Weather = "비" });
   Assert.That(t4.Fighters[0].HP, Is.EqualTo(Max(dry) - Max(dry) / 8));
   var ice = F("아이스바디", "얼음", Max(dry) / 2);
   var t5 = Turn(ice, F(), Idle(), Idle(), new Rules { Weather = "싸라기눈" });
   Assert.That(t5.Fighters[0].HP, Is.EqualTo(ice.HP + Max(dry) / 16));
   var dish = F("젖은접시", "노말", Max(dry) / 2);
   var t6 = Turn(dish, F(), Idle(), Idle(), new Rules { Weather = "비" });
   Assert.That(t6.Fighters[0].HP, Is.EqualTo(dish.HP + Max(dry) / 16));
   var t7 = Turn(F("젖은접시", "노말", Max(dry) / 2), F(), Idle(), Idle());
   Assert.That(t7.Fighters[0].HP, Is.EqualTo(Max(dry) / 2), "날씨가 없으면 회복하지 않는다");
   var ultimate = F("궁극", "노말", Max(dry) / 2);
   var t8 = Turn(ultimate, F(), Idle(), Idle());
   Assert.That(t8.Fighters[0].HP, Is.EqualTo(ultimate.HP + Max(dry) / 8));
   var nightmare = F(); nightmare.Status = "잠듦"; nightmare.Conditions.SleepTurns = 3; nightmare.Conditions.SleepStart = 3;
   var t9 = Turn(F("나이트메어"), nightmare, Idle(), Idle());
   Assert.That(t9.Fighters[1].HP, Is.LessThan(nightmare.HP), string.Join("\n", t9.Log));
   var awake = F();
   var t10 = Turn(F("나이트메어"), awake, Idle(), Idle());
   Assert.That(t10.Fighters[1].HP, Is.EqualTo(awake.HP), "잠들어 있지 않으면 해당 없음");
  }
  [TestCase("치유의마음")] [TestCase("탈피")]
  public void LowChanceCureAtTurnEnd(string ability) {
   var holder = F(ability); holder.Status = "독";
   var t = Turn(holder, F(), Idle(), Idle(), null, () => .1);
   Assert.That(t.Fighters[0].Status, Is.EqualTo("정상"), string.Join("\n", t.Log));
   var holder2 = F(ability); holder2.Status = "독";
   var t2 = Turn(holder2, F(), Idle(), Idle(), null, () => .9);
   Assert.That(t2.Fighters[0].Status, Is.EqualTo("독"));
  }
  [Test] public void KnockoutTriggersAndMissAndFaintRewrites() {
   var t = Turn(F("자기과신"), F("", "노말", 1), M("몸통박치기"), Idle());
   Assert.That(t.Fighters[0].Stages[1], Is.EqualTo(2), string.Join("\n", t.Log));
   var t2 = Turn(F("일기당천"), F("", "노말", 1), M("몸통박치기"), Idle());
   Assert.That(Enumerable.Range(1, 5).All(i => t2.Fighters[0].Stages[i] == 1), Is.True, string.Join("\n", t2.Log));
   var boom = F("유폭", "노말", 1);
   var t3 = Turn(F(), boom, M("몸통박치기"), Idle());
   Assert.That(t3.Fighters[0].HP, Is.EqualTo(Max(boom) - Max(boom) / 4), string.Join("\n", t3.Log));
   var missing = Custom("빗나감검사", "물리", 40, "노말"); missing.accuracy = 1;
   var hurt = F("잔심", "노말", 20);
   var t4 = Turn(hurt, F(), missing, Idle(), null, () => .99);
   Assert.That(t4.Fighters[0].HP, Is.EqualTo(20 + Max(hurt) / 2), string.Join("\n", t4.Log));
  }
  [Test] public void HitReactionRewrites() {
   var contact = Custom("접촉검사", "물리", 40, "노말", "접촉"); var ranged = Custom("원거리검사", "특수", 40, "노말");
   var bad = F("나쁜속셈");
   var t = Turn(F(), bad, ranged, Idle());
   Assert.That(t.Fighters[0].HP, Is.EqualTo(Max(bad) - Max(bad) / 8), string.Join("\n", t.Log));
   var t2 = Turn(F(), F("나쁜속셈"), contact, Idle());
   Assert.That(t2.Fighters[0].HP, Is.EqualTo(Max(bad)), "접촉 기술에는 반응하지 않는다");
   var fluff = Turn(F(), F("솜털"), contact, Idle());
   Assert.That(fluff.Fighters[0].Stages[5], Is.EqualTo(-1), string.Join("\n", fluff.Log));
   var poisoner = Turn(F(), F("독수"), Idle(), contact, null, () => 0);
   Assert.That(poisoner.Fighters[0].Status, Is.EqualTo("독").Or.EqualTo("정상"));
   var cracker = F("크래커");
   var t3 = Turn(cracker, F(), contact, Idle(), null, () => 0);
   Assert.That(t3.Fighters[1].Status, Is.EqualTo("마비"), string.Join("\n", t3.Log));
   var t4 = Turn(F("독수"), F(), contact, Idle(), null, () => 0);
   Assert.That(t4.Fighters[1].Status, Is.EqualTo("독"), string.Join("\n", t4.Log));
   var t5 = Turn(F("독수"), F(), ranged, Idle(), null, () => 0);
   Assert.That(t5.Fighters[1].Status, Is.EqualTo("정상"), "접촉 기술에만 붙는다");
  }
  [TestCase("악식", "악")] [TestCase("천성", "노말")] [TestCase("건조피부", "물")]
  public void AbsorbRewritesNullifyAndHeal(string ability, string type) {
   var holder = F(ability); holder.HP = Max(holder) / 2;
   var t = Turn(F(), holder, Custom("속성검사", "특수", 40, type), Idle(), new Rules(), () => .5);
   Assert.That(t.Fighters[1].HP, Is.EqualTo(holder.HP + Max(holder) / 4), string.Join("\n", t.Log));
  }
  [Test] public void FairyAbsorbRaisesSpecialAttack() {
   var holder = F("환상");
   var t = Turn(F(), holder, Custom("페어리검사", "특수", 40, "페어리"), Idle());
   Assert.That(t.Fighters[1].HP, Is.EqualTo(holder.HP));
   Assert.That(t.Fighters[1].Stages[3], Is.EqualTo(1), string.Join("\n", t.Log));
  }
  [Test] public void TrapSpeciesAndMiscRewrites() {
   Assert.That(AbilityPhrases.IgnoresWeatherDamage(F("모래헤치기"), "모래바람"), Is.True);
   var hail = Turn(F("슬로스타트"), F(), Idle(), Idle());
   Assert.That(hail.Fighters[0].Stages[1], Is.EqualTo(0));
   Assert.That(AbilityPhrases.NoCrit(F("대요새"), F()), Is.True);
   Assert.That(AbilityPhrases.NoCrit(F("딱딱한등딱지"), F()), Is.True);
  }
  [Test] public void SlowStartRaisesEveryStatAtTheFifthTurnEnd() {
   var holder = F("슬로스타트"); var foe = F();
   TurnResult t = null; var fighters = new[] { holder, foe }; var rules = new Rules();
   for (int turn = 1; turn <= 5; turn++) {
    t = Engine.Resolve(TestData.Db, fighters, new[] { Cmd(Idle()), Cmd(Idle()) }, rules, t == null ? null : t.Trainers, () => .5);
    fighters = t.Fighters; rules = t.Rules;
    int sum = Enumerable.Range(1, 5).Sum(i => t.Fighters[0].Stages[i]);
    Assert.That(sum, turn < 5 ? Is.EqualTo(0) : Is.GreaterThan(0), "T" + turn + "\n" + string.Join("\n", t.Log));
   }
  }
  [Test] public void SwitchTriggersAndEntryByVoluntarySwitch() {
   var rules = new Rules();
   var out1 = F("작약"); var foe = F();
   var t = Engine.Resolve(TestData.Db, new[] { out1, foe }, new[] { new BattleCommand { SwitchIn = F() }, Cmd(Idle()) }, rules, States(), () => .5);
   Assert.That(t.Fighters[1].HP, Is.EqualTo(Max(foe) - Max(foe) / 4), "교대하는 쪽의 특성: " + string.Join("\n", t.Log));
   var coverFire = F("원호사격");
   var t2 = Engine.Resolve(TestData.Db, new[] { F(), foe.Copy() }, new[] { new BattleCommand { SwitchIn = coverFire }, Cmd(Idle()) }, new Rules(), States(), () => .5);
   Assert.That(t2.Fighters[1].HP, Is.EqualTo(Max(foe) - Max(foe) / 4), string.Join("\n", t2.Log));
   var sneak = F("잠복"); var other = F();
   var t3 = Engine.Resolve(TestData.Db, new[] { sneak, other }, new[] { Cmd(M("몸통박치기")), new BattleCommand { SwitchIn = F() } }, new Rules(), States(), () => .5);
   Assert.That(PotentialModifiers.Power(t3.Fighters[0], t3.Fighters[1], M("몸통박치기"), t3.Rules, 1), Is.EqualTo(1), "T가 끝나면 사라진다: " + string.Join("\n", t3.Log));
  }
 }
}
