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

  // 선발 등장의 「장소에 나올 때」 역할 효과는 배틀 준비(LoadTeams) 직후 선택 창과 함께 처리되고, 첫 턴에 다시 일어나지 않는다
  [Test, Category("UI"), Apartment(System.Threading.ApartmentState.STA)]
  public void ExplorerStatChosenAtLoadTeamsAndNotRepeated() {
   Theme.Instant = true;
   var db = TestData.Db;
   Func<string, Fighter> make = name => new Fighter(new Pokemon {
    id = "lt-" + name, name = name, level = 100, types = new[] { "노말" },
    @base = new[] { 100, 100, 100, 100, 100, 100 }, iv = new[] { 31, 31, 31, 31, 31, 31 }, moves = new[] { "몸통박치기" },
    potentials = name != "탐사대원" ? new Potential[0] : new[] { new Potential {
     id = "역할", name = "탐사대원", description = EffectNormalizer.CanonicalRank("자신의 임의의 능력", 1), triggers = new[] { TriggerStructures.EntryLabel } } }
   });
   var explorer = make("탐사대원");
   var fighters = new[] { new List<Fighter> { explorer }, new List<Fighter> { make("상대") } };
   var team = new TeamRecord { name = "t", trainer = new TrainerRecord(), members = new List<PokemonRecord>() };
   int asked = 0;
   using (var battle = new MainForm(db) { TestMode = true }) {
    battle.Rules.ChooseStat = (s, f, list) => { asked++; return "속도"; };
    battle.LoadTeams(new[] { team, team }, fighters, new[] { 0, 0 });
    Assert.That(asked, Is.EqualTo(1));
    Assert.That(battle.Fighters[0].Stages[5], Is.EqualTo(1));
    var idle = db.moves.First(m => m.name == "몸통박치기");
    var turn = Engine.Resolve(db, battle.Fighters, new[] { idle, idle }, battle.Rules, () => .5);
    Assert.That(asked, Is.EqualTo(1));
    Assert.That(turn.Fighters[0].Stages[5], Is.EqualTo(1));
    battle.Close();
   }
  }

  // 「필드에 나온 후 최초」 조건(속이기): 선발 첫 턴 성공, 두 번째 턴 실패
  [Test, Category("UI"), Apartment(System.Threading.ApartmentState.STA)]
  public void FakeOutWorksOnlyOnFirstTurnAfterLoadTeams() {
   Theme.Instant = true;
   var db = TestData.Db;
   Func<string, Fighter> make = name => new Fighter(new Pokemon {
    id = "fo-" + name, name = name, level = 100, types = new[] { "노말" },
    @base = new[] { 300, 100, 100, 100, 100, 100 }, iv = new[] { 31, 31, 31, 31, 31, 31 }, moves = new[] { "속이기", "몸통박치기" }, potentials = new Potential[0]
   });
   var users = make("사용자"); var foe = make("상대");
   var fighters = new[] { new List<Fighter> { users }, new List<Fighter> { foe } };
   var team = new TeamRecord { name = "t", trainer = new TrainerRecord(), members = new List<PokemonRecord>() };
   using (var battle = new MainForm(db) { TestMode = true }) {
    battle.LoadTeams(new[] { team, team }, fighters, new[] { 0, 0 });
    var fake = db.moves.First(m => m.name == "속이기");
    var idle = db.moves.First(m => m.name == "몸통박치기").Copy();
    idle.power = 1;
    var current = battle.Fighters;
    var turn1 = Engine.Resolve(db, current, new[] { fake, idle }, battle.Rules, () => .5);
    Assert.That(turn1.Fighters[1].HP, Is.LessThan(current[1].HP), string.Join(" | ", turn1.Log));
    var hp = turn1.Fighters[1].HP;
    var turn2 = Engine.Resolve(db, turn1.Fighters, new[] { fake, idle }, turn1.Rules, () => .5);
    Assert.That(turn2.Fighters[1].HP, Is.EqualTo(hp), string.Join(" | ", turn2.Log));
    battle.Close();
   }
  }

  // 교대로 나온 포켓몬은 나온 T의 다음 T에 속이기가 성공한다
  [Test, Category("UI"), Apartment(System.Threading.ApartmentState.STA)]
  public void FakeOutWorksTurnAfterSwitchIn() {
   Theme.Instant = true;
   var db = TestData.Db;
   Func<string, Fighter> make = name => new Fighter(new Pokemon {
    id = "fo2-" + name, name = name, level = 100, types = new[] { "노말" },
    @base = new[] { 300, 100, 100, 100, 100, 100 }, iv = new[] { 31, 31, 31, 31, 31, 31 }, moves = new[] { "속이기", "몸통박치기" }, potentials = new Potential[0]
   });
   var lead = make("선발"); var bench = make("교대"); var foe = make("상대");
   var fighters = new[] { new List<Fighter> { lead, bench }, new List<Fighter> { foe } };
   var team = new TeamRecord { name = "t", trainer = new TrainerRecord(), members = new List<PokemonRecord>() };
   using (var battle = new MainForm(db) { TestMode = true }) {
    battle.LoadTeams(new[] { team, team }, fighters, new[] { 0, 0 });
    var fake = db.moves.First(m => m.name == "속이기");
    var idle = db.moves.First(m => m.name == "몸통박치기").Copy();
    idle.power = 1;
    var party = new[] { lead, bench };
    var turn1 = Engine.Resolve(db, battle.Fighters, new[] { new BattleCommand { Move = idle, SwitchIn = bench, Party = party }, new BattleCommand { Move = idle } }, battle.Rules, new[] { new TrainerBattleState(), new TrainerBattleState() }, () => .5);
    Assert.That(turn1.Fighters[0].Data.name, Is.EqualTo("교대"), string.Join(" | ", turn1.Log));
    int hp = turn1.Fighters[1].HP;
    var turn2 = Engine.Resolve(db, turn1.Fighters, new[] { fake, idle }, turn1.Rules, () => .5);
    Assert.That(turn2.Fighters[1].HP, Is.LessThan(hp), string.Join(" | ", turn2.Log));
    battle.Close();
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
