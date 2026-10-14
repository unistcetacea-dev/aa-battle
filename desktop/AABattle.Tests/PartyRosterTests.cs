using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace AABattle {
 [TestFixture] public class PartyRosterTests {
  [Test] public void MultipleMembershipsAndDefaultLineupRoundTripByReference() {
   var first=new TrainerRecord{name="첫 트레이너"};
   var second=new TrainerRecord{name="둘째 트레이너"};
   var shared=new PokemonRecord{name="공유 포켓몬"};
   var project=new EditorProject();
   project.trainers.AddRange(new[]{first,second});
   project.pokemon.Add(shared);
   project.teams.Add(new TeamRecord{name="A",trainer=first,trainers=new List<TrainerRecord>{first,second},members=new List<PokemonRecord>{shared},defaultTrainerId="",defaultMemberIds=new List<string>()});
   project.teams.Add(new TeamRecord{name="B",trainer=second,trainers=new List<TrainerRecord>{second},members=new List<PokemonRecord>{shared}});

   var saved=ProjectStore.Parse(ProjectStore.Serialize(project));
   Assert.That(saved.teams[0].trainers.Select(t=>t.name),Is.EqualTo(new[]{"첫 트레이너","둘째 트레이너"}));
   Assert.That(saved.teams[0].members[0],Is.SameAs(saved.teams[1].members[0]));
   Assert.That(saved.teams[0].defaultTrainerId,Is.EqualTo(first.id));
   Assert.That(saved.teams[0].defaultMemberIds,Is.EqualTo(new[]{shared.id}));
  }

  [Test,Category("UI"),Apartment(System.Threading.ApartmentState.STA)] public void BattleRosterUsesSelectedMembersAndHidesForms() {
   var trainer=new TrainerRecord{name="트레이너"};trainer.leadership.rank="B";
   var opponent=new TrainerRecord{name="상대"};
   var eligible=new PokemonRecord{id="pk-good",name="출전 가능"};
   var form=new PokemonRecord{id="pk-form",name="폼",formOf="pk-good"};
   var restricted=new PokemonRecord{id="pk-role",name="어벤저 보유"};
   restricted.potentials.Add(new PotentialRecord{enabled=true,slot="역할",name="어벤저"});
   var left=new TeamRecord{name="왼쪽",trainer=trainer,trainers=new List<TrainerRecord>{trainer},members=new List<PokemonRecord>{eligible,form,restricted},defaultTrainerId=trainer.id};
   var right=new TeamRecord{name="오른쪽",trainer=opponent,trainers=new List<TrainerRecord>{opponent},members=new List<PokemonRecord>{new PokemonRecord{id="pk-opponent",name="상대 포켓몬"}},defaultTrainerId=opponent.id};

   using(var picker=new PartyRosterForm(new[]{left,right})) {
    picker.SetSelectionForTest(0,0,new[]{0});
    picker.SetSelectionForTest(1,0,new[]{0});
    picker.ConfirmForTest();
    var chosen=picker.SelectedTeams;
    Assert.That(chosen[0].members.Select(p=>p.name),Is.EqualTo(new[]{"출전 가능"}));
    Assert.That(TeamRules.Check(chosen[0]),Is.Empty);
    Assert.That(chosen[0].members.Any(p=>p.formOf.Length>0),Is.False);
   }

   using(var picker=new PartyRosterForm(new[]{left,right})) {
    picker.SetSelectionForTest(0,0,new[]{1});
    picker.SetSelectionForTest(1,0,new[]{0});
    picker.ConfirmForTest();
    Assert.That(TeamRules.Check(picker.SelectedTeams[0]),Has.Some.Contains("어벤저"));
   }
  }
 }
}
