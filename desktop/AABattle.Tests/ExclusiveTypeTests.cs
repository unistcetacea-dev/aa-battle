using System;
using System.Linq;
using System.Collections.Generic;
using NUnit.Framework;

namespace AABattle {
 // 오레마스 전용 포텐셜 유형 템플릿: 문장이 모두 읽히는지, 새 모듈과 유형이 실제로 동작하는지
 [TestFixture] public class ExclusiveTypeTests {
  static int ids;

  static Potential Exclusive(string typeName, string name = "검사전용") {
   var type = ExclusiveTypes.Find(typeName);
   return new Potential {
    id = "전용", name = name, description = ExclusiveTypes.Filled(type, type.Effect), activation = type.Activation, uses = type.Uses,
    triggers = ExclusiveTypes.Filled(type, type.Trigger).Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries)
   };
  }
  static Fighter F(int speed = 100, params Potential[] potentials) {
   return new Fighter(new Pokemon {
    id = "excl" + (++ids), name = "전" + ids, level = 100, types = new[] { "노말" }, ability = "", item = "",
    @base = new[] { 300, 100, 100, 100, 100, speed }, iv = new[] { 31, 31, 31, 31, 31, 31 },
    moves = new[] { "몸통박치기" }, potentials = potentials
   });
  }
  static Move Hit(int priority = 0, double accuracy = 100, string name = "검사공격") {
   return new Move { name = name, category = "물리", target = "상대", types = new[] { "노말" }, power = 60, accuracy = accuracy, attack = "공격", defense = "방어", priority = priority, tags = new string[0] };
  }
  static Move Idle() { return new Move { name = "대기", category = "변화", target = "자신", types = new[] { "노말" }, accuracy = 100, tags = new string[0] }; }
  static Move Guard() { return TestData.Db.moves.First(m => m.name == "방어"); }
  static TurnResult Run(Fighter a, Fighter b, BattleCommand ca, BattleCommand cb, Func<double> rng = null, Rules rules = null) {
   return Engine.Resolve(TestData.Db, new[] { a, b }, new[] { ca, cb }, rules ?? new Rules(), new[] { new TrainerBattleState(), new TrainerBattleState() }, rng ?? (() => .5), q => q.Candidates.First());
  }
  static BattleCommand Use(Move move, params string[] declared) { return new BattleCommand { Move = move, PreInputs = declared }; }

  // ===== 템플릿 문장은 모두 모듈로 읽힌다 =====
  [Test] public void EveryTemplateSentenceIsReadableOrPendingWithReason() {
   var problems = new List<string>();
   foreach (var type in ExclusiveTypes.All) {
    Assert.That(type.Name.Length, Is.GreaterThan(0));
    if (type.Pending.Length > 0) continue;
    string trigger = ExclusiveTypes.Filled(type, type.Trigger);
    foreach (string clause in trigger.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries))
     if (TriggerModules.Classify(clause).Length == 0) problems.Add(type.Name + " 트리거: " + clause);
    foreach (var step in PotentialQueue.Preview(ExclusiveTypes.Filled(type, type.Effect)))
     if (step.Value.Length == 0 && !PriorityEffectRuntime.Matches(step.Key)) problems.Add(type.Name + " 효과: " + step.Key);
   }
   Assert.That(problems, Is.Empty, string.Join("\n", problems));
  }

  [Test] public void PendingTypesNameTheirReason() {
   foreach (var type in ExclusiveTypes.All.Where(x => x.Pending.Length > 0)) Assert.That(type.Pending.Length, Is.GreaterThan(8), type.Name);
   Assert.That(ExclusiveTypes.Names, Is.Unique);
  }

  [Test] public void PlaceholdersAreFlagged() {
   Assert.That(ExclusiveTypes.Find("대삼각").HasPlaceholder, Is.True);
   Assert.That(ExclusiveTypes.Find("필중추미").HasPlaceholder, Is.False);
  }

  // ===== 정규화 · 분류 =====
  [TestCase("「추격」효과의 발동 시, 기술의 데미지를 2배로 한다.", "자신이 「추격」 효과로 주는 대미지를 강화(2배)한다.")]
  [TestCase("상대의 「상태변화」를 해제한다.", "상대의 모든 상태변화를 해제한다.")]
  [TestCase("자신의 기술이 상대에게 「필중」한다.", "자신의 기술은 필중이다.")]
  public void CollectedVariantsBecomeStandardSentences(string raw, string standard) {
   Assert.That(EffectNormalizer.Normalize(raw), Is.EqualTo(standard));
  }

  [TestCase("자신이 「추격」 효과로 주는 대미지를 강화(2배)한다.", "추격 발동 배율")]
  [TestCase("상대의 모든 상태변화를 해제한다.", "상태변화 전체 해제")]
  [TestCase("이때 가장 먼저 같은 기술에 의한 추가공격을 한다.", "버서크 추가공격")]
  public void NewModulesClassify(string sentence, string kind) {
   Assert.That(EffectModules.Classify(sentence), Is.EqualTo(kind));
   Assert.That(ModuleCatalog.All.Any(x => x.Kind == ModuleKind.Effect && x.Expected == kind), Is.True, "ModuleCatalog 등록");
  }

  [Test] public void FunctionExtensionNameMayContainParentheses() {
   Assert.That(EffectNormalizer.Normalize("자신이 「기능확장:아라가미탄(불꽃)」를 내보낼 수 있다."), Is.EqualTo("자신이 「기능확장:아라가미탄(불꽃)」를 내보낼 수 있다."));
  }

  [Test] public void MakenLastTurnTriggerRequiresARegularSwitchEntry() {
   string sentence = ExclusiveTriggerModules.NormalSwitchLastSentence;
   var spec = TriggerModules.Parse(sentence);
   var owner = F();
   owner.Conditions.TurnFacts = new List<TriggerFact> {
    new TriggerFact { Kind = "등장", Self = true, Method = "통상교대" }
   };
   Assert.That(TriggerModules.Classify(sentence), Is.EqualTo("타이밍"));
   Assert.That(ModuleCatalog.All.Any(x => x.Kind == ModuleKind.TriggerEvent && x.Template == sentence), Is.True);
   Assert.That(TriggerModules.Matches(spec,
    new TriggerFact { Kind = "타이밍", Method = TriggerStructures.TurnLastLabel }, owner), Is.True);
   owner.Conditions.TurnFacts[0].Method = "선발";
   Assert.That(TriggerModules.Matches(spec,
    new TriggerFact { Kind = "타이밍", Method = TriggerStructures.TurnLastLabel }, owner), Is.False);
  }

  // ===== 사건으로 얻는 필중 · 관통 =====
  [Test] public void SureHitTypeHitsOnlyWhenDeclared() {
   var move = Hit(0, 30);
   Func<double> miss = () => .99;
   var plain = F(100, Exclusive("필중추미")); var target = F();
   var t0 = Run(plain, target, Use(move), Use(Idle()), miss);
   Assert.That(t0.Fighters[1].HP, Is.EqualTo(target.HP), "선언하지 않으면 빗나간다\n" + string.Join("\n", t0.Log));
   var declared = F(100, Exclusive("필중추미")); var target2 = F();
   var t1 = Run(declared, target2, Use(move, "검사전용"), Use(Idle()), miss);
   Assert.That(t1.Fighters[1].HP, Is.LessThan(target2.HP), string.Join("\n", t1.Log));
  }

  [Test] public void TriangleEffectPiercesProtect() {
   var plain = F(100, Exclusive("삼각효과")); var guard = F(50);
   var t0 = Run(plain, guard, Use(Hit()), Use(Guard()));
   Assert.That(t0.Fighters[1].HP, Is.EqualTo(guard.HP), "선언하지 않으면 방어에 막힌다");
   var declared = F(100, Exclusive("삼각효과")); var guard2 = F(50);
   var t1 = Run(declared, guard2, Use(Hit(), "검사전용"), Use(Guard()));
   Assert.That(t1.Fighters[1].HP, Is.LessThan(guard2.HP), string.Join("\n", t1.Log));
  }

  [Test] public void GrantExpiresAfterTheAction() {
   var user = F(100, Exclusive("삼각효과")); var guard = F(50);
   Run(user, guard, Use(Hit(), "검사전용"), Use(Guard()));
   Assert.That(user.Conditions.Modifiers.Any(x => x.Text.Contains("관통")), Is.False);
  }

  // ===== 추격 =====
  [Test] public void EnhancedPursuitDoublesDamageOverPlainPursuit() {
   int plainDamage = PursuitDamage("필중추미"), enhanced = PursuitDamage("강화추미");
   Assert.That(plainDamage, Is.GreaterThan(0));
   Assert.That(enhanced, Is.InRange(plainDamage * 2 - 3, plainDamage * 2 + 3));
  }
  static int PursuitDamage(string type) {
   var a = F(100, Exclusive(type)); var b = F(); var bench = F(); int start = b.HP;

   var t = Run(a, b, Use(Hit(-7, 100), "검사전용"), new BattleCommand { SwitchIn = bench });
   Assert.That(t.SwitchedOut[1], Is.Not.Null, string.Join("\n", t.Log));
   return start - t.SwitchedOut[1].HP;
  }

  // ===== 우선도 =====
  [Test] public void PivotTypeRaisesPriorityOnlyWhenDeclared() {
   var slow = F(10, Exclusive("선행유턴")); var fast = F(200);
   var t0 = Run(slow, fast, Use(Hit()), Use(Hit()));
   Assert.That(t0.Log.First(x => x.Contains("의 검사공격!")), Does.Contain(fast.Data.name), "선언하지 않으면 빠른 쪽이 먼저");
   var slow2 = F(10, Exclusive("선행유턴")); var fast2 = F(200);
   var t1 = Run(slow2, fast2, Use(Hit(), "검사전용"), Use(Hit()));
   Assert.That(t1.Log.First(x => x.Contains("의 검사공격!")), Does.Contain(slow2.Data.name), string.Join("\n", t1.Log));
  }

  [Test] public void QuickDrawOnlyBoostsTheFirstMoveAfterEntering() {
   var slow = F(10, Exclusive("속사")); var fast = F(200);
   var t0 = Run(slow, fast, Use(Hit()), Use(Hit()));
   Assert.That(t0.Log.First(x => x.Contains("의 검사공격!")), Does.Contain(slow.Data.name), string.Join("\n", t0.Log));
   var t1 = Run(t0.Fighters[0], t0.Fighters[1], Use(Hit()), Use(Hit()));
   Assert.That(t1.Log.First(x => x.Contains("의 검사공격!")), Does.Contain(fast.Data.name), "두 번째 턴에는 우선도가 오르지 않는다\n" + string.Join("\n", t1.Log));
  }

  // ===== 상태변화 해제 =====
  [Test] public void ReleaseAllClearsProtectAndOtherConditionsButNotSubstitute() {
   var f = F();
   f.Conditions.Protect = true; f.Conditions.Confusion = 3; f.Conditions.Taunt = 2; f.Conditions.Substitute = 10;
   var log = new List<string>();
   Assert.That(ExclusiveModules.ReleaseAllConditions(f, log), Is.True);
   Assert.That(f.Conditions.Protect || f.Conditions.Confusion > 0 || f.Conditions.Taunt > 0, Is.False);
   Assert.That(f.Conditions.Substitute, Is.EqualTo(10));
   Assert.That(ExclusiveModules.ReleaseAllConditions(f, log), Is.False, "더 풀 것이 없으면 실패");
  }

  // ===== 그 밖의 유형 =====
  static int Hits(TurnResult t, Fighter who) { return t.Log.Count(x => x.StartsWith(who.Data.name + "의 검사공격!")); }

  [Test] public void BerserkHalvesHpAndGrantsAnImmediateFollowUp() {
   var user = F(100, Exclusive("버서크")); var target = F(50);
   user.HP = 77;
   int hp = user.HP;
   var t = Run(user, target, Use(Hit(), "검사전용"), Use(Idle()));
   Assert.That(Hits(t, user), Is.EqualTo(2), string.Join("\n", t.Log));
   Assert.That(t.Fighters[0].HP, Is.EqualTo(hp - hp / 2).Within(1));
  }

  [Test] public void BerserkLeavesHalfOfCurrentHpRoundedDown() {
   var user = F(100, Exclusive("버서크")); var target = F(50);
   user.HP = 81;
   var t = Run(user, target, Use(Hit(), "검사전용"), Use(Idle()));
   Assert.That(t.Fighters[0].HP, Is.EqualTo(40), string.Join("\n", t.Log));
  }

  [Test] public void BerserkCannotBeUsedAtOneHp() {
   var user = F(100, Exclusive("버서크")); var target = F(50);
   user.HP = 1;
   var t = Run(user, target, Use(Hit(), "검사전용"), Use(Idle()));
   Assert.That(Hits(t, user), Is.EqualTo(1), string.Join("\n", t.Log));
   Assert.That(t.Fighters[0].HP, Is.EqualTo(1));
  }

  [Test] public void CounterStrikeDodgesAnAttackOnlyWhenDeclared() {
   var dodger = F(50, Exclusive("영격")); var attacker = F(100);
   var t0 = Run(attacker, dodger, Use(Hit()), Use(Idle()));
   Assert.That(t0.Fighters[1].HP, Is.LessThan(dodger.HP), "선언하지 않으면 맞는다");
   var dodger2 = F(50, Exclusive("영격")); var attacker2 = F(100);
   var t1 = Run(attacker2, dodger2, Use(Hit()), Use(Idle(), "검사전용"));
   Assert.That(t1.Fighters[1].HP, Is.EqualTo(dodger2.HP), string.Join("\n", t1.Log));
  }

  [Test] public void ReverseTurnSwitchesOutAtTheEndOfTheSecondTurn() {
   var user = F(100, Exclusive("역선회")); var foe = F(50); var bench = F();
   var rules = new Rules { OptionalEffectPrompt = (f, o, p) => true };
   BattleCommand Stay() { return new BattleCommand { Move = Idle(), Party = new[] { bench } }; }
   var t1 = Run(user, foe, Stay(), Use(Idle()), null, rules);
   Assert.That(ReferenceEquals(t1.Fighters[0], bench), Is.False, "1턴째에는 교대하지 않는다\n" + string.Join("\n", t1.Log));
   var t2 = Run(t1.Fighters[0], t1.Fighters[1], Stay(), Use(Idle()), null, rules);
   Assert.That(t2.Fighters[0].Data.name, Is.EqualTo(bench.Data.name), string.Join("\n", t2.Log));
  }

  [Test] public void BumpUpRaisesEverySpeciesGrade() {
   var type = ExclusiveTypes.Find("범프업");
   var owner = F(100, new Potential { id = "전용", name = "범프", description = type.Effect, triggers = new[] { TriggerStructures.EntryLabel } });
   var other = F();
   var before = owner.Data.@base.ToArray();
   var log = new List<string>();
   var rules = new Rules();
   Mechanics.SetParties(rules, new[] { owner, other });
   using (EffectScope.Begin(new[] { owner, other }, rules, () => .5, new[] { new TrainerBattleState(), new TrainerBattleState() }))
    PotentialQueue.Raise(TriggerStructures.LabelEvent(owner, TriggerStructures.EntryLabel, "등장", log.Add));
   Assert.That(owner.Data.@base.Skip(1).Zip(before.Skip(1), (x, y) => x > y).All(x => x), Is.True, string.Join("\n", log));
  }

  [Test] public void RecoilExtraActionUsesTheNamedMoveAtTheEnd() {
   var user = F(100, Exclusive("추가반동")); var target = F(50);
   var t = Run(user, target, Use(Hit(), "검사전용"), Use(Idle()));
   Assert.That(t.Log.Any(x => x.Contains("추가행동") && x.Contains("파괴광선")), Is.True, string.Join("\n", t.Log));
  }

  [Test] public void HeavyBodyDoublesTheDamageOfThatHit() {
   var plain = F(100); var strong = F(100, Exclusive("강체술")); var target = F(50); var target2 = F(50);
   var t0 = Run(plain, target, Use(Hit()), Use(Idle()));
   var t1 = Run(strong, target2, Use(Hit()), Use(Idle()));
   int normal = target.HP - t0.Fighters[1].HP, doubled = target2.HP - t1.Fighters[1].HP;
   Assert.That(doubled, Is.InRange(normal * 2 - 4, normal * 2 + 4), string.Join("\n", t1.Log));
  }

  [Test] public void BreakthroughReleasesProtectBeforeTheHit() {
   var user = F(100, Exclusive("강행돌파")); var guard = F(50);
   var t = Run(user, guard, Use(Hit(), "검사전용"), Use(Guard()));
   Assert.That(t.Fighters[1].HP, Is.LessThan(guard.HP), string.Join("\n", t.Log));
  }
 }
}
