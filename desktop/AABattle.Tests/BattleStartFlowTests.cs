using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Windows.Forms;
using NUnit.Framework;

namespace AABattle {
 // 시작 화면 이후 배틀 준비(StartForm.Begin과 같은 순서)를 실제 폼으로 재현한다
 [TestFixture] public class BattleStartFlowTests {
  static EditorProject LoadSample(string file) {
   string dir = AppDomain.CurrentDomain.BaseDirectory;
   while (dir != null) {
    string path = Path.Combine(dir, "samples", file);
    if (File.Exists(path)) return ProjectStore.Parse(File.ReadAllText(path, Encoding.UTF8));
    dir = Path.GetDirectoryName(dir);
   }
   throw new FileNotFoundException(file);
  }

  static void StartBattle(TeamRecord team, TrainerRecord trainer, List<PokemonRecord> members) {
   var db = TestData.Db;
   var chosen = new TeamRecord { name = team.name, trainer = trainer, trainers = team.trainers, members = members, fieldEffect = team.fieldEffect };
   var teams = new[] { chosen, chosen };
   var fighters = teams.Select(t => t.members.Select(m => {
    string warning;
    var f = new Fighter(EditorBridge.ToPokemon(m, db, out warning, t.trainer));
    f.AA = m.asciiArt;
    return f;
   }).ToList()).ToArray();
   using (var battle = new MainForm(db) { TestMode = true }) {
    // 「선택 발동」 확인 창이 뜨면 아니오로 닫는다
    var watch = new System.Windows.Forms.Timer { Interval = 100 };
    watch.Tick += (o, e) => {
     foreach (Form f in Application.OpenForms.Cast<Form>().Where(x => x.Text == "선택 발동").ToList()) f.DialogResult = DialogResult.No;
    };
    watch.Start();
    try {
     battle.LoadTeams(teams, fighters, new[] { 0, Math.Min(1, members.Count - 1) });
     battle.Show();
     Application.DoEvents();
     battle.RefreshView();
     Application.DoEvents();
     battle.Close();
    } finally {
     watch.Dispose();
    }
   }
  }

  [TestCase("finis-chaldea.aabdata.json")]
  [TestCase("abaddon-sylph.aabdata.json")]
  [Category("UI"), Apartment(System.Threading.ApartmentState.STA)]
  public void EveryTrainerAndMemberStartsBattle(string file) {
   Theme.Instant = true;
   var project = LoadSample(file);
   var team = project.teams.First();
   var trainers = team.trainers != null && team.trainers.Count > 0 ? team.trainers : new List<TrainerRecord> { team.trainer };
   var defaults = team.defaultMemberIds.Select(id => team.members.First(m => m.id == id)).ToList();
   foreach (var trainer in trainers) {
    try { StartBattle(team, trainer, defaults); }
    catch (Exception ex) { throw new Exception("트레이너 " + trainer.name + ": " + ex, ex); }
   }
   foreach (var member in team.members) {
    try { StartBattle(team, team.trainer, new List<PokemonRecord> { member }); }
    catch (Exception ex) { throw new Exception("포켓몬 " + member.name + ": " + ex, ex); }
   }
  }
 }
}
