using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Web.Script.Serialization;
using NUnit.Framework;

namespace AABattle {
 // 편집기 모듈 표준 문장과 수집 원문 변형의 정규화
 [TestFixture] public class EffectModuleTests {
  [TestCase("상대의 「불꽃」 기술의 데미지를 반감한다", "상대가 주는 「불꽃」 기술의 대미지를 약화(0.5배)한다.")]
  [TestCase("「독」기술로 받는 데미지를 반감한다", "자신이 받는 「독」 기술의 대미지를 약화(0.5배)한다.")]
  [TestCase("상대의 「땅」「바위」기술로 받는 데미지를 반감한다", "자신이 받는 「땅/바위」 기술의 대미지를 약화(0.5배)한다.")]
  [TestCase("자신의 기술의 데미지가 2배가 된다", "자신이 주는 대미지를 강화(2배)한다.")]
  [TestCase("타입 불일치인 「고스트」기술의 위력을 강화(1.5배)한다", "자신의 「고스트」「타입 불일치」 기술의 위력을 강화(1.5배)한다.")]
  [TestCase("상대의 「효과가 매우 굉장」한 기술의 위력을 반감한다", "상대의 「효과가 굉장」 기술의 위력을 약화(0.5배)한다.")]
  [TestCase("상대의 「효과가 매우 별로」인 기술의 데미지를 반감한다", "상대가 주는 「효과가 별로」 기술의 대미지를 약화(0.5배)한다.")]
  [TestCase("상대의 물리기술의 위력을 완화(0.67배)한다", "상대의 「물리」 기술의 위력을 약화(0.67배)한다.")]
  [TestCase("상대의 「위력:40」이하인 기술의 위력을 반감한다", "상대의 「위력 40 이하」 기술의 위력을 약화(0.5배)한다.")]
  [TestCase("상대의 「」 기술의 명중률을 감소(0.85배)시킨다", "상대의 「○」 기술의 명중을 약화(0.85배)한다.")]
  [TestCase("자신의 「명중」「회피」를 강화(1.1배)한다.", "자신의 「명중/회피」를 강화(1.1배)한다.")]
  [TestCase("T종료시까지 자신의 「방/특방」을 2배로 한다", "T종료시까지 자신의 「방/특방」을 강화(2배)한다.")]
  [TestCase("자신의 「공/방/특공」의 종족치를 「A」로 한다", "자신의 「공/방/특공」 종족치를 「A」로 한다.")]
  [TestCase("자신의 「공」을 가장 높은 종족치와 동랭크로 한다", "자신의 「공」 종족치를 가장 높은 종족치로 한다.")]
  [TestCase("자신의 「방어」의 종족치를 트레이너의 「육성」과 같은 랭크까지 올린다", "자신의 「방어」 종족치를 트레이너 「육성」 등급까지 올린다.")]
  [TestCase("최대 체력의 1/4를 회복한다", "자신의 체력을 최대 HP의 1/4 회복한다.")]
  [TestCase("상대에게 최대 체력의 1/8의 「고스트」 대미지를 준다", "상대에게 최대 HP의 1/8 대미지를 준다.")]
  [TestCase("가장 높은 능력이 크게 오른다.", "자신의 가장 높은 능력이 크게 오른다.")]
  [TestCase("상대의 명중을 내린다.", "상대의 「명중」을 내린다.")]
  [TestCase("랜덤으로 상대의 능력을 내린다.", "상대의 랜덤한 능력치를 내린다.")]
  public void NormalizesVariantsToModuleSentence(string raw, string expected) {
   Assert.That(EffectNormalizer.Normalize(raw), Is.EqualTo(expected));
   Assert.That(EffectModules.Classify(raw), Is.Not.Empty);
  }

  [TestCase("상대의 「위력:40」이하인 기술의 추가효과가 발생하지 않는다")]
  [TestCase("최초로 내보내는 기술의 위력을 배화한다")]
  [TestCase("자신과 같은 타입의 기술의 데미지를 반감한다")]
  public void LeavesUnknownWordingPending(string raw) {
   Assert.That(EffectModules.Classify(raw), Is.Empty);
  }

  [Test] public void EditorModuleSentencesAreRecognized() {
   Assert.That(EffectModules.Classify("자신의 체력을 최대 HP의 1/4 회복한다."), Is.EqualTo("최대 HP의 1/N"));
   FractionEffect f;
   Assert.That(EffectModules.TryFraction("자신에게 최대 HP의 1/4 대미지를 준다 이 대미지로는 빈사로 할 수 없다", out f) && !f.CanFaint && f.Action == "대미지", Is.True);
   Assert.That(EffectModules.Classify("자신에게 최대 HP의 1/4 대미지를 준다. 이 대미지로는 빈사로 할 수 없다."), Is.EqualTo("최대 HP의 1/N"));
   Assert.That(EffectModules.Classify(EffectNormalizer.CanonicalRank("자신의 「공격」", 2)), Is.EqualTo("능력 랭크 변화"));
   Assert.That(EffectModules.Classify(EffectNormalizer.Canonical("상대가 받는 대미지", "강화", 1.5)), Is.EqualTo("수치 배율"));
  }

  [Test] public void ConditionMatchesEffectivenessIncludingDoubleSuper() {
   MoveCondition c;
   Assert.That(MoveCondition.TryParse("「효과가 굉장」", out c), Is.True);
   var user = new Fighter(new Pokemon { name = "U", level = 50, types = new[] { "불꽃" }, @base = new[] { 1, 1, 1, 1, 1, 1 }, iv = new int[6], moves = new string[0] });
   var fire = new Move { name = "불꽃세례", category = "특수", types = new[] { "불꽃" }, tags = new string[0], power = 40 };
   Assert.That(c.Matches(fire, user, 4), Is.True, "x4 counts as super effective");
   Assert.That(c.Matches(fire, user, 1), Is.False);
   Assert.That(MoveCondition.TryParse("「효과가 별로」", out c) && c.Matches(fire, user, .25) && !c.Matches(fire, user, 0), Is.True, "x0.25 counts as not very effective, immunity does not");
   Assert.That(MoveCondition.TryParse("「효과 보통」", out c) && c.Matches(fire, user, 1), Is.True);
   Assert.That(MoveCondition.TryParse("「물리」「타입 불일치」「위력 40 이하」", out c) && !c.Matches(fire, user, 1), Is.True);
   Assert.That(MoveCondition.TryParse("「○」", out c) && c.Placeholder && !c.Matches(fire, user, 1), Is.True, "placeholder never matches");
  }

  // 수집 효과 문장 전체: 정규화는 두 번 해도 같다
  [Test] public void NormalizationIsIdempotentOverCollectedEffects() {
   string dir = TestContext.CurrentContext.TestDirectory;
   while (dir != null && !File.Exists(Path.Combine(dir, "research", "potentials", "potential-parts.json"))) dir = Path.GetDirectoryName(dir);
   Assert.That(dir, Is.Not.Null, "repository root");
   var data = new JavaScriptSerializer { MaxJsonLength = 100000000 }.Deserialize<Dictionary<string, object>>(File.ReadAllText(Path.Combine(dir, "research", "potentials", "potential-parts.json")));
   var broken = new List<string>();
   foreach (Dictionary<string, object> effect in (System.Collections.ArrayList)data["effects"]) {
    string once = EffectNormalizer.Normalize((string)effect["text"]);
    string twice = EffectNormalizer.Normalize(once);
    if (once != twice) broken.Add(once + "  ⇒  " + twice);
   }
   Assert.That(broken, Is.Empty, string.Join("\n", broken.Take(20)));
  }
 }
}
