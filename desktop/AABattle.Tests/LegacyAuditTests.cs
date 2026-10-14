using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using NUnit.Framework;

namespace AABattle {
 // 지시서 S: 옛 구조 가정(기술 4개·대기석 전원 교대 가능·자질 글자 비교 등)을 점검해 고친 것의 재현 검사.
 // 점검표: research/LEGACY_AUDIT.md
 [TestFixture] public class LegacyAuditTests {
  static int ids;

  static Fighter F(params Potential[] potentials) {
   return new Fighter(new Pokemon {
    id = "legacy-" + (++ids), name = "옛가정" + ids, level = 100, types = new[] { "노말" },
    @base = new[] { 300, 100, 100, 100, 100, 100 }, iv = new[] { 31, 31, 31, 31, 31, 31 },
    moves = new[] { "몸통박치기" }, potentials = potentials
   });
  }

  static Move Idle() {
   return new Move { name = "미등록", category = "변화", types = new[] { "노말" }, target = "자신", accuracy = 100, tags = new string[0] };
  }

  // 항목 1: 대기석에는 쓰러진 포켓몬이 남지만 교대 대상이 될 수 없다
  [Test] public void FaintedReserveCannotBeSwitchedIn() {
   var lead = F(); var bench = F(); var foe = F();
   bench.HP = 0;
   var commands = new[] {
    new BattleCommand { Move = Idle(), SwitchIn = bench, Party = new[] { lead, bench } },
    new BattleCommand { Move = Idle() }
   };
   var ex = Assert.Throws<Exception>(() => Engine.Resolve(TestData.Db, new[] { lead, foe }, commands, new Rules(), new[] { new TrainerBattleState(), new TrainerBattleState() }, () => .5));
   Assert.That(ex.Message, Does.Contain("쓰러진"));
   Assert.That(Mechanics.CanSwitchIn(bench), Is.False);
   bench.HP = 10;
   Assert.That(Mechanics.CanSwitchIn(bench), Is.True);
  }

  // 항목 1: 행동 선택 창은 쓰러진 포켓몬을 목록에서 빼되 고른 번호는 대기석 원래 번호로 돌려준다
  [Test, Category("UI"), Apartment(System.Threading.ApartmentState.STA)]
  public void DirectiveFormListsOnlyLivingReservesWithOriginalIndex() {
   var fainted = F(); fainted.HP = 0;
   var alive = F();
   var reserves = new List<Fighter> { fainted, alive };
   using (var form = new DirectiveForm(null, new TrainerBattleState(), reserves, F(), new TurnSelection())) {
    var flags = BindingFlags.Instance | BindingFlags.NonPublic;
    var action = (PixelComboBox)typeof(DirectiveForm).GetField("action", flags).GetValue(form);
    var target = (PixelComboBox)typeof(DirectiveForm).GetField("target", flags).GetValue(form);
    Assert.That(target.Items.Count, Is.EqualTo(2), "선택하세요 + 살아 있는 대기 1체");
    action.SelectedItem = "교대";
    target.SelectedIndex = 1;
    typeof(DirectiveForm).GetMethod("Apply", flags).Invoke(form, null);
    Assert.That(form.Value.SwitchIndex, Is.EqualTo(1));
   }
  }

  // 항목 3: 초기화는 트레이너 상태를 LoadTeams처럼 다시 채운다(대기석 포켓몬이 주는 해금·봉인 기록 등)
  [Test, Category("UI"), Apartment(System.Threading.ApartmentState.STA)]
  public void ResetBattleRefillsTrainerUnlocksAndClearsFieldRecords() {
   Theme.Instant = true;
   var db = TestData.Db;
   var unlocker = F(new Potential { id = "종족 ①", name = "해금 검사", description = "자신의 「공격」이 오른다.", unlocks = new[] { TrainerAccessRules.Pt2 }, triggers = new[] { TriggerStructures.EntryLabel } });
   var fighters = new[] { new List<Fighter> { F(), unlocker }, new List<Fighter> { F() } };
   var team = new TeamRecord { name = "t", trainer = new TrainerRecord(), members = new List<PokemonRecord>() };
   using (var battle = new MainForm(db) { TestMode = true }) {
    battle.LoadTeams(new[] { team, team }, fighters, new[] { 0, 0 });
    Assert.That(battle.TrainerStatesForTest[0].UnlockedCapabilities, Does.Contain(TrainerAccessRules.Pt2));
    battle.Rules.SealedMoves["키"] = "몸통박치기";
    battle.Rules.WaterSport = 3;
    battle.ResetBattle();
    Assert.That(battle.TrainerStatesForTest[0].UnlockedCapabilities, Does.Contain(TrainerAccessRules.Pt2));
    Assert.That(battle.Rules.SealedMoves, Is.Empty);
    Assert.That(battle.Rules.WaterSport, Is.EqualTo(0));
    battle.Close();
   }
  }

  // 항목 4: 봉인 후보는 지금 가진 기술 목록(4개 초과 포함)이다
  [Test] public void SealChoicesComeFromCurrentMovesNotOriginalData() {
   var self = F(); var foe = F();
   foe.Moves = new[] { "몸통박치기", "방어", "10만볼트", "지진", "에어슬래시" };
   var rules = new Rules();
   Mechanics.SetParties(rules, new[] { self, foe });
   string[] offered = null;
   rules.ChooseSealMove = (f, list) => { offered = list; return list.Last(); };
   var outcome = StateEffects.Apply("상대를 「봉인」상태로 한다", self, foe, rules, () => .5, new List<string>(), "검사");
   Assert.That(outcome, Is.EqualTo(EffectOutcome.Success));
   Assert.That(offered, Is.EqualTo(foe.Moves));
   Assert.That(foe.Conditions.Sealed, Is.EqualTo("에어슬래시"));
  }

  // 항목 5·6: 육성 EX는 AA와 같다(트리거 「자질」 비교·자질 랭크 올림·내림 모두 같은 함수를 거친다)
  [TestCase("EX", "AA", true)]
  [TestCase("D-", "D", true)]
  [TestCase("A+", "A", true)]
  [TestCase("AA+", "A", false)]
  [TestCase("", "A", false)]
  public void TriggerRankBandUsesSharedGradeBand(string actual, string required, bool expected) {
   Assert.That(TriggerStructures.RankBand(actual, required), Is.EqualTo(expected));
  }

  [TestCase("EX", 1, "AAA")]
  [TestCase("EX", -1, "A")]
  [TestCase("D-", 1, "C")]
  [TestCase("S", 1, "S")]
  public void AptitudeRankShiftHandlesExAndSignedGrades(string rank, int delta, string expected) {
   var state = new TrainerBattleState();
   state.Usage.Battle["@aptitude:육성"] = delta;
   Assert.That(RoleModules.AdjustedRank(rank, "육성", state), Is.EqualTo(expected));
  }
 }
}
