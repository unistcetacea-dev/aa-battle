using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using NUnit.Framework;

namespace AABattle {
 // 샘플 파티(samples/abaddon-sylph.aabdata.json): 불러오기·편성 검사·배틀 진행
 [TestFixture] public class AbaddonSampleTests {
  static string SamplePath() {
   string dir = AppDomain.CurrentDomain.BaseDirectory;
   while (dir != null) {
    string path = Path.Combine(dir, "samples", "abaddon-sylph.aabdata.json");
    if (File.Exists(path)) return path;
    dir = Path.GetDirectoryName(dir);
   }
   throw new FileNotFoundException("samples/abaddon-sylph.aabdata.json");
  }
  static EditorProject Load() {
   return ProjectStore.Parse(File.ReadAllText(SamplePath(), Encoding.UTF8));
  }

  [Test] public void SampleLoadsConvertsAllMovesAndPassesTeamRules() {
   var project = Load();
   var team = project.teams.Single();
   Assert.That(team.members.Count, Is.EqualTo(6));
   Assert.That(team.trainer.name, Is.EqualTo("마히토"));
   Assert.That(TeamRules.Check(team), Is.Empty, string.Join(" / ", TeamRules.Check(team)));
   foreach (var member in team.members) {
    string warning;
    var pokemon = EditorBridge.ToPokemon(member, TestData.Db, out warning, team.trainer);
    Assert.That(warning, Is.Empty, member.name);
    Assert.That(pokemon.moves.Length, Is.EqualTo(member.moves.Count), member.name + ": 4개를 넘는 기술도 모두 변환된다");
    Assert.That(member.stats.hp, Is.EqualTo(100), member.name);
    Assert.That(member.asciiArt, Does.Not.Contain("</br>"));
   }
   Assert.That(team.members.SelectMany(m => m.potentials).Count(p => p.slot == TeamRules.ExclusiveSlot), Is.EqualTo(7));
  }

  // 회피·내성·격 템플릿은 샘플의 저장 값 그대로 편집 창에서 불러와진다
  [Test, Category("UI"), Apartment(System.Threading.ApartmentState.STA)] public void SampleCounterPotentialsLoadInTheEditor() {
   using (var form = new DataEditorForm()) {
    form.ImportProject(Load());
    form.Show();
    form.SelectTabForTest(1);
    System.Windows.Forms.Application.DoEvents();
    foreach (var record in form.ProjectForTest.pokemon.SelectMany(p => p.potentials).Where(p => EditorTemplate.IsCounterSlot(p.slot) && p.name.Length > 0)) {
     var window = form.OpenPotentialWindowForTest(new PotentialDraft { Slot = record.slot, TargetType = record.targetType, Name = record.name });
     Assert.That(window.NameChoices, Does.Contain(record.name), record.name);
     Assert.That(window.Collect().TargetType, Is.EqualTo(record.targetType), record.name);
     window.Cancel();
     window.Dispose();
    }
    form.CloseForTest();
   }
  }

  // 샘플 파티로 6명 각각이 선봉이 되어 1턴 진행한다
  [Test] public void SamplePartyRunsOneTurnWithEveryLead() {
   var project = Load();
   var team = project.teams.Single();
   var db = TestData.Db;
   for (int lead = 0; lead < 6; lead++) {
    var sides = new List<Fighter>[2];
    for (int side = 0; side < 2; side++) {
     sides[side] = team.members.Select(m => {
      string warning;
      return new Fighter(EditorBridge.ToPokemon(m, db, out warning, team.trainer)) { AA = m.asciiArt };
     }).ToList();
    }
    var fighters = new[] { sides[0][lead], sides[1][(lead + 1) % 6] };
    var commands = new BattleCommand[2];
    for (int side = 0; side < 2; side++) {
     var move = db.moves.First(x => x.name == fighters[side].Moves[0]);
     commands[side] = new BattleCommand { Move = move, Party = sides[side].ToArray() };
    }
    var states = new[] { new TrainerBattleState { Trainer = team.trainer, InitialSize = 6 }, new TrainerBattleState { Trainer = team.trainer, InitialSize = 6 } };
    foreach (var state in states) state.RefreshUnlocks();
    var turn = Engine.Resolve(db, fighters, commands, new Rules(), states, () => .5);
    Assert.That(turn.Log, Is.Not.Empty, "선봉 " + lead);
    if (turn.Fighters[0].Data.name == "로보코") Assert.That(turn.Fighters[0].Data.@base[0], Is.EqualTo(150), "배틀 시작 시 금관 사이즈 체력 종족치 적용");
   }
  }
 }
}
