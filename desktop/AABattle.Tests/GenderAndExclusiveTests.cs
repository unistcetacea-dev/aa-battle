using System;
using System.Linq;
using System.Collections.Generic;
using NUnit.Framework;

namespace AABattle {
 // 성별(수컷·암컷·없음)과 전용 포텐셜(포켓몬에 달지만 트레이너 포텐셜)의 엔진 규칙
 [TestFixture] public class GenderAndExclusiveTests {
  static int ids;
  static Fighter F(string gender = null, string type = "노말", params Potential[] potentials) {
   return new Fighter(new Pokemon {
    id = "pk-g" + (++ids), name = "G" + ids, level = 100, types = new[] { type }, ability = "", item = "", gender = gender,
    @base = new[] { 100, 100, 100, 100, 100, 100 }, iv = new[] { 31, 31, 31, 31, 31, 31 },
    moves = new[] { "몸통박치기" }, potentials = potentials
   });
  }
  static Potential P(string effect, string slot) {
   return new Potential { id = slot, name = "검사 " + slot, description = effect, triggers = new[] { TriggerStructures.FieldLabel } };
  }
  static Move Physical() {
   return new Move { name = "노말검사", category = "물리", types = new[] { "노말" }, tags = new string[0], power = 80, accuracy = 100, attack = "공격", defense = "방어" };
  }

  // ===== 성별 =====
  [TestCase("수컷", "암컷", false)]
  [TestCase("암컷", "수컷", false)]
  [TestCase("수컷", "수컷", true)]
  [TestCase("암컷", "암컷", true)]
  [TestCase("없음", "수컷", true)]
  [TestCase("암컷", "없음", true)]
  [TestCase("없음", "없음", true)]
  [TestCase(null, "수컷", true)]
  public void SameGenderOrMissingGenderFailsTheMove(string user, string target, bool fails) {
   var move = TestData.Db.moves.First(x => x.name == "헤롱헤롱");
   Assert.That(move.effect, Does.Contain("성별이 같으면 실패한다"));
   string reason = FailModules.Reason(F(user), F(target), move, new Rules());
   Assert.That(reason.Length > 0, Is.EqualTo(fails), reason);
   if (fails) Assert.That(reason, Does.Contain("성별"));
  }

  [Test] public void GenderSurvivesSaveLoadAndBattleConversion() {
   var project = new EditorProject();
   var mon = new PokemonRecord { name = "G", gender = "암컷", level = 50, types = new List<string> { "노말" }, moves = new List<string> { "몸통박치기" } };
   mon.stats = new StatsRecord { hp = 50, attack = 50, defense = 50, spAttack = 50, spDefense = 50, speed = 50 };
   project.pokemon.Add(mon);
   var loaded = ProjectStore.Parse(ProjectStore.Serialize(project));
   Assert.That(loaded.pokemon[0].gender, Is.EqualTo("암컷"));
   string warning;
   Assert.That(EditorBridge.ToPokemon(loaded.pokemon[0], TestData.Db, out warning).gender, Is.EqualTo("암컷"));
   Assert.That(new PokemonRecord().gender, Is.EqualTo("없음"), "기본값은 없음");
   // 성별 칸이 없던 옛 파일과 알 수 없는 값은 없음이다
   Assert.That(ProjectStore.Parse("{\"pokemon\":[{\"name\":\"옛\"}]}").pokemon[0].gender, Is.EqualTo("없음"));
   mon.gender = "모름";
   Assert.That(EditorBridge.ToPokemon(mon, TestData.Db, out warning).gender, Is.EqualTo("없음"));
  }

  // ===== 전용 포텐셜: 포켓몬에 달아도 트레이너 포텐셜 =====
  const string Guard = "상대의 「노말」 기술을 무효화한다.";

  static bool Blocked(Fighter attacker, Fighter defender) {
   return PotentialProtection.Check(TestData.Db, attacker, defender, Physical(), new Rules(), () => .5, false) != null;
  }

  [Test] public void ExclusiveGuardIsTrainerProtectionThatPokemonPierceCannotBreak() {
   var ordinary = F("수컷", "노말", P(Guard, "종족 ①"));
   var exclusive = F("수컷", "노말", P(Guard, TeamRules.ExclusiveSlot));
   var pokemonPierce = F("수컷", "노말", P("자신의 「노말」 기술이 상대의 방호 포텐셜을 무시한다.", "종족 ①"));
   var trainerPierce = F("수컷", "노말", P("자신의 「노말」 기술이 상대의 트레이너 방호 포텐셜을 무시한다.", "종족 ①"));
   var exclusivePierce = F("수컷", "노말", P("자신의 「노말」 기술이 상대의 방호 포텐셜을 무시한다.", TeamRules.ExclusiveSlot));
   Assert.That(Blocked(F(), ordinary), Is.True);
   Assert.That(Blocked(F(), exclusive), Is.True);
   Assert.That(Blocked(pokemonPierce, ordinary), Is.False, "포켓몬 관통은 포켓몬 방호를 뚫는다");
   Assert.That(Blocked(pokemonPierce, exclusive), Is.True, "포켓몬 관통은 트레이너 방호(전용)를 뚫지 못한다");
   Assert.That(Blocked(trainerPierce, exclusive), Is.False, "「트레이너 방호」를 명시한 관통은 뚫는다");
   Assert.That(Blocked(exclusivePierce, exclusive), Is.False, "트레이너 포텐셜(전용)에서 나온 관통은 뚫는다");
   var guard = PotentialProtection.Check(TestData.Db, F(), exclusive, Physical(), new Rules(), () => .5, false);
   Assert.That(guard.Value.Value, Does.StartWith("트레이너 "));
  }

  [Test] public void ExclusivePotentialIsFlaggedAsTrainerPotentialOnlyForTheExclusiveSlot() {
   Assert.That(new PotentialInstance { Pokemon = new Potential { id = TeamRules.ExclusiveSlot } }.IsTrainerPotential, Is.True);
   Assert.That(new PotentialInstance { Pokemon = new Potential { id = "종족 ①" } }.IsTrainerPotential, Is.False);
   Assert.That(new PotentialInstance { Trainer = new TrainerRecord() }.IsTrainerPotential, Is.True);
   Assert.That(new PotentialInstance { Pokemon = new Potential { id = TeamRules.ExclusiveSlot } }.IsTrainer, Is.False, "실행기 분담은 기록 출처만 본다");
  }

  [Test] public void ExclusiveLimitCountsPokemonExclusivesOfTheWholeParty() {
   var trainer = new TrainerRecord { name = "T" };
   trainer.ability.rank = "B"; // 전용 5개까지
   var team = new TeamRecord { trainer = trainer };
   for (int i = 0; i < 3; i++) {
    var mon = new PokemonRecord { name = "M" + i };
    mon.potentials.Add(new PotentialRecord { slot = TeamRules.ExclusiveSlot, name = "전용" + i + "a" });
    mon.potentials.Add(new PotentialRecord { slot = TeamRules.ExclusiveSlot, name = "전용" + i + "b" });
    team.members.Add(mon);
   }
   Assert.That(TeamRules.Check(team).Any(x => x.Contains("전용 포텐셜 6개")), Is.True, "포켓몬 3체 × 2개 = 6개");
   team.members[2].potentials.RemoveAt(1);
   Assert.That(TeamRules.Check(team).Any(x => x.Contains("전용")), Is.False, "5개는 B까지");
   trainer.potentials.Add(new PotentialRecord { slot = TeamRules.ExclusiveSlot, name = "옛 전용" });
   Assert.That(TeamRules.Check(team).Any(x => x.Contains("전용")), Is.False, "트레이너 쪽 전용은 세지 않는다");
   team.members[1].potentials[0].enabled = false;
   Assert.That(TeamRules.Check(team).Any(x => x.Contains("전용")), Is.False, "발동 금지는 세지 않는다");
  }

  [Test] public void BasicKingSetsPokemonExclusiveCapacityToSix() {
   var trainer = new TrainerRecord { name = "T" };
   trainer.ability.rank = "B";
   trainer.potentials.Add(new PotentialRecord { slot = "고유", name = "반상위 각개전투", effect = "자신의 『전용』 용량을 「6」으로 한다" });
   var team = new TeamRecord { trainer = trainer };
   for (int i = 0; i < 3; i++) {
    var mon = new PokemonRecord { name = "M" + i };
    mon.potentials.Add(new PotentialRecord { slot = TeamRules.ExclusiveSlot, name = "전용" + i + "a" });
    mon.potentials.Add(new PotentialRecord { slot = TeamRules.ExclusiveSlot, name = "전용" + i + "b" });
    team.members.Add(mon);
   }
   Assert.That(TeamRules.Check(team).Any(x => x.Contains("전용 포텐셜")), Is.False);
   team.members[0].potentials.Add(new PotentialRecord { slot = TeamRules.ExclusiveSlot, name = "일곱째 전용" });
   Assert.That(TeamRules.Check(team).Any(x => x.Contains("전용 포텐셜")), Is.True);
  }

  [Test] public void LegacyTrainerExclusiveRowsAreDroppedOnLoad() {
   var project = new EditorProject();
   var trainer = new TrainerRecord { name = "T" };
   trainer.potentials.Add(new PotentialRecord { slot = TeamRules.ExclusiveSlot, name = "옛 전용" });
   trainer.potentials.Add(new PotentialRecord { slot = "고유", name = "남는 고유" });
   project.trainers.Add(trainer);
   var mon = new PokemonRecord { name = "M" };
   mon.potentials.Add(new PotentialRecord { slot = TeamRules.ExclusiveSlot, name = "포켓몬 전용" });
   project.pokemon.Add(mon);
   var loaded = ProjectStore.Parse(ProjectStore.Serialize(project));
   Assert.That(loaded.trainers[0].potentials.Select(x => x.name), Is.EqualTo(new[] { "남는 고유" }));
   Assert.That(loaded.pokemon[0].potentials.Select(x => x.name), Is.EqualTo(new[] { "포켓몬 전용" }), "포켓몬 쪽 전용은 남는다");
  }

  [Test] public void AnalysisDoesNotRevealExclusivePotentials() {
   var target = F("수컷", "노말", P("자신의 「공격」을 강화(1.5배)한다.", "종족 ①"), P(Guard, TeamRules.ExclusiveSlot));
   var shown = AnalysisForm.PublicPotentials(target);
   Assert.That(shown.Length, Is.EqualTo(1));
   Assert.That(shown[0], Does.Contain("종족 ①"));
  }
 }
}
