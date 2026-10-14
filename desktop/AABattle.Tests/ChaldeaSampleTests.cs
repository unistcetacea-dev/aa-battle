using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using NUnit.Framework;

namespace AABattle {
 // 샘플 파티(samples/finis-chaldea.aabdata.json): 불러오기·편집기·출전 선택·배틀 진행
 [TestFixture] public class ChaldeaSampleTests {
  static string SamplePath() {
   string dir = AppDomain.CurrentDomain.BaseDirectory;
   while (dir != null) {
    string path = Path.Combine(dir, "samples", "finis-chaldea.aabdata.json");
    if (File.Exists(path)) return path;
    dir = Path.GetDirectoryName(dir);
   }
   throw new FileNotFoundException("samples/finis-chaldea.aabdata.json");
  }
  static EditorProject Load() {
   return ProjectStore.Parse(File.ReadAllText(SamplePath(), Encoding.UTF8));
  }

  // 한 파티에 트레이너 12명과 포켓몬 28명이 소속되고, 기본 출전은 6명
  [Test] public void SampleLoadsWithRosterAndEveryMemberConverts() {
   var project = Load();
   var team = project.teams.Single();
   Assert.That(team.members.Count, Is.EqualTo(28));
   Assert.That(team.trainers.Count, Is.EqualTo(12));
   Assert.That(team.trainer.name, Is.EqualTo("유키나리"));
   Assert.That(team.trainer.potentials.Any(p => p.slot == "고유"), Is.False, "유키나리의 고유 포텐셜은 넣지 않는다");
   Assert.That(team.defaultMemberIds.Count, Is.EqualTo(6));
   foreach (var member in team.members) {
    Assert.That(member.stats.hp, Is.EqualTo(100), member.name);
    Assert.That(member.asciiArt, Does.Not.Contain("</br>"), member.name);
    Assert.That(member.potentials.Any(p => p.slot == TeamRules.ExclusiveSlot && p.name.Length > 0), Is.True, member.name + ": 전용 포텐셜");
    string warning;
    var pokemon = EditorBridge.ToPokemon(member, TestData.Db, out warning, team.trainer);
    Assert.That(pokemon.moves, Is.Not.Empty, member.name);
   }
   var withUnique = team.trainers.Where(t => t.potentials.Any(p => p.slot == "고유")).Select(t => t.name).ToArray();
   Assert.That(withUnique, Does.Contain("유우키"));
   Assert.That(withUnique, Does.Not.Contain("유키나리"));
  }

  // 편집기에서 샘플이 열리고, 저장된 회피·내성·격 템플릿이 편집 창에서 불러와진다
  [Test, Category("UI"), Apartment(System.Threading.ApartmentState.STA)] public void SampleOpensInTheEditorAndCounterPotentialsLoad() {
   using (var form = new DataEditorForm()) {
    form.ImportProject(Load());
    form.Show();
    form.SelectTabForTest(1);
    System.Windows.Forms.Application.DoEvents();
    Assert.That(form.ProjectForTest.pokemon.Count, Is.EqualTo(28));
    Assert.That(form.ProjectForTest.trainers.Count, Is.EqualTo(12));
    foreach (var record in form.ProjectForTest.pokemon.Take(8).SelectMany(p => p.potentials).Where(p => EditorTemplate.IsCounterSlot(p.slot) && p.name.Length > 0)) {
     var window = form.OpenPotentialWindowForTest(new PotentialDraft { Slot = record.slot, TargetType = record.targetType, Name = record.name });
     Assert.That(window.NameChoices, Does.Contain(record.name), record.name);
     Assert.That(window.Collect().TargetType, Is.EqualTo(record.targetType), record.name);
     window.Cancel();
     window.Dispose();
    }
    form.CloseForTest();
   }
  }

  // 출전 선택에서 기본 6명을 고르고, 6명 각각이 선봉이 되어 1턴 진행한다
  [Test, Category("UI"), Apartment(System.Threading.ApartmentState.STA)] public void RosterPicksSixAndEveryLeadRunsOneTurn() {
   var project = Load();
   var team = project.teams.Single();
   var picks = team.defaultMemberIds.Select(id => team.members.FindIndex(m => m.id == id)).ToArray();
   Assert.That(picks.All(i => i >= 0), Is.True);
   List<PokemonRecord> chosen;
   TrainerRecord chosenTrainer;
   using (var picker = new PartyRosterForm(new[] { team, team })) {
    picker.SetSelectionForTest(0, 0, picks);
    picker.SetSelectionForTest(1, 0, picks);
    picker.ConfirmForTest();
    chosen = picker.SelectedTeams[0].members;
    chosenTrainer = picker.SelectedTeams[0].trainer;
    Assert.That(chosen.Count, Is.EqualTo(6));
    var problems = TeamRules.Check(picker.SelectedTeams[0]);
    TestContext.Out.WriteLine("TeamRules(기본 출전 6명): " + (problems.Count == 0 ? "위반 없음" : string.Join(" / ", problems)));
   }
   var all = TeamRules.Check(team);
   TestContext.Out.WriteLine("TeamRules(후보 28명 전원): " + (all.Count == 0 ? "위반 없음" : string.Join(" / ", all)));

   var db = TestData.Db;
   for (int lead = 0; lead < chosen.Count; lead++) {
    var sides = new List<Fighter>[2];
    for (int side = 0; side < 2; side++) {
     sides[side] = chosen.Select(m => {
      string warning;
      return new Fighter(EditorBridge.ToPokemon(m, db, out warning, chosenTrainer)) { AA = m.asciiArt };
     }).ToList();
    }
    var fighters = new[] { sides[0][lead], sides[1][(lead + 1) % chosen.Count] };
    var commands = new BattleCommand[2];
    for (int side = 0; side < 2; side++) {
     var move = db.moves.First(x => x.name == fighters[side].Moves[0]);
     commands[side] = new BattleCommand { Move = move, Party = sides[side].ToArray() };
    }
    var states = new[] { new TrainerBattleState { Trainer = chosenTrainer, InitialSize = chosen.Count }, new TrainerBattleState { Trainer = chosenTrainer, InitialSize = chosen.Count } };
    foreach (var state in states) state.RefreshUnlocks();
    var turn = Engine.Resolve(db, fighters, commands, new Rules(), states, () => .5);
    Assert.That(turn.Log, Is.Not.Empty, "선봉 " + chosen[lead].name);
   }
  }
 }
}
