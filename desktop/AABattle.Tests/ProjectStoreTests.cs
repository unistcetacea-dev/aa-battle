using System;
using System.Linq;
using System.Collections.Generic;
using System.Web.Script.Serialization;
using NUnit.Framework;

namespace AABattle {
 // 안정 데이터 ID: 팀은 id로 참조하고, 옛 파일의 사본은 원본에 연결한다
 [TestFixture] public class ProjectStoreTests {
  static PokemonRecord Mon(string name, int level) {
   var p = new PokemonRecord { name = name, level = level };
   p.types.Add("노말");
   p.moves.Add("몸통박치기");
   p.stats = new StatsRecord { hp = 100, attack = 100, defense = 100, spAttack = 100, spDefense = 100, speed = 100 };
   return p;
  }

  static EditorProject Sample() {
   var project = new EditorProject();
   var trainer = new TrainerRecord { name = "레드" };
   project.trainers.Add(trainer);
   project.pokemon.Add(Mon("피카", 50));
   project.pokemon.Add(Mon("피카", 60));
   var team = new TeamRecord { name = "A", trainer = trainer };
   team.members.AddRange(project.pokemon);
   project.teams.Add(team);
   return project;
  }

  [Test] public void SavesReferencesWithoutCopies() {
   string json = ProjectStore.Serialize(Sample());
   var raw = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(json);
   var team = (Dictionary<string, object>)((System.Collections.ArrayList)raw["teams"])[0];
   Assert.That(team["trainer"], Is.Null);
   Assert.That(((System.Collections.ArrayList)team["members"]).Count, Is.EqualTo(0));
   Assert.That(((System.Collections.ArrayList)team["memberIds"]).Count, Is.EqualTo(2));
   Assert.That(raw["format"], Is.EqualTo(ProjectStore.Format));
  }

  [Test] public void LoadedTeamFollowsOriginalEdits() {
   var project = ProjectStore.Parse(ProjectStore.Serialize(Sample()));
   var team = project.teams[0];
   Assert.That(team.members[0], Is.SameAs(project.pokemon[0]));
   Assert.That(team.trainer, Is.SameAs(project.trainers[0]));
   project.pokemon[1].level = 77;
   Assert.That(team.members[1].level, Is.EqualTo(77));
   Assert.That(project.pokemon.Select(x => x.id).Distinct().Count(), Is.EqualTo(2), "same species get different ids");
  }

  // v1 파일: 팀이 트레이너·포켓몬 사본을 들고 있고 id가 없다
  static string OldFile(bool includeOriginals) {
   var copyA = new { name = "피카", level = 60, types = new[] { "노말" }, moves = new[] { "몸통박치기" } };
   var copyB = new { name = "피카", level = 50, types = new[] { "노말" }, moves = new[] { "몸통박치기" } };
   var originals = includeOriginals ? new object[] { copyB, copyA } : new object[0];
   var file = new {
    format = "aa-battle-data-v1",
    trainers = new[] { new { name = "레드" } },
    pokemon = originals,
    teams = new[] { new { name = "A", trainer = new { name = "레드" }, members = new object[] { copyA, copyB } } }
   };
   return new JavaScriptSerializer().Serialize(file);
  }

  [Test] public void MigratesOldCopiesToOriginalsByContent() {
   var project = ProjectStore.Parse(OldFile(true));
   var team = project.teams[0];
   Assert.That(project.pokemon.Count, Is.EqualTo(2), "no duplicates added");
   Assert.That(team.members[0], Is.SameAs(project.pokemon[1]), "level 60 copy links to level 60 original");
   Assert.That(team.members[1], Is.SameAs(project.pokemon[0]), "level 50 copy links to level 50 original");
   Assert.That(team.trainer, Is.SameAs(project.trainers[0]));
   var again = ProjectStore.Parse(ProjectStore.Serialize(project));
   Assert.That(again.teams[0].members.Select(x => x.level), Is.EqualTo(new[] { 60, 50 }));
  }

  [Test] public void MigrationKeepsCopiesWithoutOriginal() {
   var project = ProjectStore.Parse(OldFile(false));
   Assert.That(project.pokemon.Count, Is.EqualTo(2));
   Assert.That(project.teams[0].members, Is.EqualTo(project.pokemon));
  }

  [Test] public void DuplicateIdsAreReassigned() {
   var project = Sample();
   project.pokemon[0].id = project.pokemon[1].id = "pk-same";
   ProjectStore.Normalize(project);
   Assert.That(project.pokemon[0].id, Is.Not.EqualTo(project.pokemon[1].id));
  }

  [Test] public void BattlePokemonCarriesRecordId() {
   var record = Mon("피카", 50);
   record.id = "pk-test";
   string warning;
   Assert.That(EditorBridge.ToPokemon(record, TestData.Db, out warning).id, Is.EqualTo("pk-test"));
  }

  [Test] public void SameSpeciesStayDistinctInParty() {
   string warning;
   var db = TestData.Db;
   var first = Mon("피카", 50); first.id = "pk-1";
   var second = Mon("피카", 60); second.id = "pk-2";
   var a = new Fighter(EditorBridge.ToPokemon(first, db, out warning));
   var b = new Fighter(EditorBridge.ToPokemon(second, db, out warning));
   var opponent = new Fighter(EditorBridge.ToPokemon(Mon("상대", 50), db, out warning));
   var rules = new Rules();
   Mechanics.SetParties(rules, new[] { a, opponent }, new IEnumerable<Fighter>[] { new[] { a, b }, new[] { opponent } });
   Assert.That(rules.Parties[0].Length, Is.EqualTo(2));
   Assert.That(Mechanics.SameMember(a, b), Is.False);
   Assert.That(Mechanics.SameMember(a, a.Copy()), Is.True);
  }
 }
}
