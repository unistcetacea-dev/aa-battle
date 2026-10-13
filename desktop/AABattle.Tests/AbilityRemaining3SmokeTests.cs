using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using NUnit.Framework;

namespace AABattle {
 // 3차에서 문장 모듈로 옮긴 특성이 한 턴을 오류 없이 돌고, 자기 문장이 「효과 판정 대기」로 남지 않는다.
 [TestFixture] public class AbilityRemaining3SmokeTests {
  static int ids;
  static Fighter F(string ability, string type = "노말") {
   return new Fighter(new Pokemon { id = "r3smoke" + ++ids, name = "검사" + ids, ability = ability, item = "", level = 100,
    types = new[] { type }, @base = new[] { 100, 100, 100, 100, 100, 100 }, iv = new[] { 31, 31, 31, 31, 31, 31 },
    moves = new[] { "몸통박치기", "전기쇼크" }, potentials = new Potential[0] });
  }
  static Move M(string name) { return TestData.Db.moves.First(m => m.name == name); }

  public static IEnumerable<string> TableNames { get { return AbilityRewrites3.Table.Keys.Where(k => AbilityCatalog.Find(k) != null); } }

  [TestCaseSource("TableNames")]
  public void RewrittenAbilityRunsATurnWithoutPendingOwnSentence(string ability) {
   var definition = AbilityCatalog.Find(ability);
   Assert.That(definition.support, Is.EqualTo(AbilityRewrites3.PendingReasons.ContainsKey(ability) ? "부분 자동" : "자동"), ability);
   foreach (bool first in new[] { true, false }) {
    var holder = F(ability); var other = F("");
    var fighters = first ? new[] { holder, other } : new[] { other, holder };
    var commands = new[] { M("몸통박치기"), M("전기쇼크") };
    var t = Engine.Resolve(TestData.Db, fighters, commands, new Rules(), () => .5);
    string log = string.Join("\n", t.Log);
    // 특성 이름이 붙은 대기 로그가 없어야 한다
    var waiting = t.Log.Where(x => x.Contains("〔" + ability + "〕") && x.Contains("판정 대기")).ToList();
    Assert.That(waiting, Is.Empty, ability + "\n" + log);
   }
  }
 }
}
