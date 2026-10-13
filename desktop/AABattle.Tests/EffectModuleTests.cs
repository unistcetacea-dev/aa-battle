using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Web.Script.Serialization;
using NUnit.Framework;

namespace AABattle {
 // 편집기 모듈 표준 문장과 수집 원문 변형의 정규화
 [TestFixture] public class EffectModuleTests {


  [TestCase("상대의 「위력:40」이하인 기술의 추가효과가 발생하지 않는다")]
  [TestCase("최초로 내보내는 기술의 위력을 배화한다")]
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

 // 「기술의 우선도를 +N한다」 변형은 표준 문장(기술 우선도를 N 올린다)이 된다
 }
}
