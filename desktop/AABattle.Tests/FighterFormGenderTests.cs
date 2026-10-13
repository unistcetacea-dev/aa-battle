using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using NUnit.Framework;

namespace AABattle {
 // 배틀 화면 포켓몬 편집(FighterForm)의 성별 칸
 [TestFixture, Category("UI"), Apartment(System.Threading.ApartmentState.STA)] public class FighterFormGenderTests {
  static PokemonRecord Record(string gender) {
   var mon = new PokemonRecord { name = "성별 시험", gender = gender, types = new List<string> { "노말" }, moves = new List<string> { "몸통박치기" } };
   mon.stats = new StatsRecord { hp = 50, attack = 50, defense = 50, spAttack = 50, spDefense = 50, speed = 50 };
   return mon;
  }

  [Test] public void GenderChosenInBattleEditSurvivesTeamRoundTripAndDecidesInfatuation() {
   // 팀 저장 → 불러오기 → 배틀 포켓몬
   var project = new EditorProject();
   project.pokemon.Add(Record("암컷"));
   var loaded = ProjectStore.Parse(ProjectStore.Serialize(project));
   string warning;
   var fighter = new Fighter(EditorBridge.ToPokemon(loaded.pokemon[0], TestData.Db, out warning));
   var foe = new Fighter(EditorBridge.ToPokemon(Record("암컷"), TestData.Db, out warning));
   var move = TestData.Db.moves.First(x => x.name == "헤롱헤롱");
   Assert.That(FailModules.Reason(fighter, foe, move, new Rules()).Length > 0, Is.True, "암컷끼리는 실패");

   using (var form = new FighterForm(TestData.Db, fighter, 100, new Rules())) {
    var box = (PixelComboBox)typeof(FighterForm).GetField("gender", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(form);
    Assert.That(box.Text, Is.EqualTo("암컷"), "불러온 성별이 칸에 보인다");
    Assert.That(box.Items.Cast<object>().Select(x => x.ToString()), Is.EqualTo(new[] { "수컷", "암컷", "없음" }));
    box.SelectedItem = "수컷";
    typeof(FighterForm).GetMethod("ReadFields", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(form, new object[] { false });
    Assert.That(form.Value.Data.gender, Is.EqualTo("수컷"));
    Assert.That(FailModules.Reason(form.Value, foe, move, new Rules()), Is.EqualTo(""), "수컷과 암컷은 성공");
   }

   // 내장 도감에서 고른 포켓몬은 기본 없음
   var builtIn = new Fighter(TestData.Db.pokemon.First());
   using (var form = new FighterForm(TestData.Db, builtIn, 100, new Rules())) {
    var box = (PixelComboBox)typeof(FighterForm).GetField("gender", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(form);
    Assert.That(box.Text, Is.EqualTo("없음"));
   }
  }
 }
}
