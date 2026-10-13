using System;
using System.Linq;
using System.Collections.Generic;

namespace AABattle {
 // 오레마스 4대자질에 따른 팀 편성 제한. 배틀을 막지 않고, 시작 전에 확인할 위반 목록만 만든다.
 // 자질 랭크가 비어 있는 항목은 판단할 수 없으므로 검사하지 않는다.
 public static class TeamRules {
  // 통솔 A가 있어야 임명할 수 있는 추가임명·통솔 조건 역할. 『엠퍼러』는 통솔 S.
  public static readonly string[] LeadershipRoles={"양대간판","어벤저","인챈터","스카우터","체이서","톱 배터","선구의 에이스"};
  // 전용 포텐셜: 포켓몬 편집에서 그 포켓몬에 달지만 트레이너 포텐셜이다(개수 제한은 파티 전체 합산).
  public const string ExclusiveSlot="전용";
  static string Rank(AptitudeRecord value){return value==null?"":(value.rank??"").Trim();}
  static bool Known(string rank){return TrainerAccessRules.RankAtLeast(rank,"E");}
  static bool At(string rank,string required){return TrainerAccessRules.RankAtLeast(rank,required);}
  static IEnumerable<PotentialRecord> Active(IEnumerable<PotentialRecord> list,Func<string,bool> slot){return (list??Enumerable.Empty<PotentialRecord>()).Where(x=>x!=null&&x.enabled&&!string.IsNullOrWhiteSpace(x.name)&&slot(x.slot??""));}
  // 통솔: 기본 1, A 2, AA 3, AAA 4, S 6.
  public static int BondLimit(string leadership){return At(leadership,"S")?6:At(leadership,"AAA")?4:At(leadership,"AA")?3:At(leadership,"A")?2:1;}
  // 능력: C 고유1/전용3, B 고유2/전용5, A 고유 제한 없음/전용6, AA 전용8, AAA 전용10, S 제한 없음.
  public static int UniqueLimit(string ability){return At(ability,"A")?int.MaxValue:At(ability,"B")?2:1;}
  public static int ExclusiveLimit(string ability){return At(ability,"S")?int.MaxValue:At(ability,"AAA")?10:At(ability,"AA")?8:At(ability,"A")?6:At(ability,"B")?5:3;}
  // 육성: 종족 ② C, ③ B, ④ A.
  public static string SpeciesTierRank(string slot){return slot.Contains("②")?"C":slot.Contains("③")?"B":slot.Contains("④")?"A":"";}
  public static List<string> Check(TeamRecord team){
   var problems=new List<string>();if(team==null||team.trainer==null)return problems;var t=team.trainer;var members=(team.members??new List<PokemonRecord>()).Where(x=>x!=null).ToList();
   string leadership=Rank(t.leadership),ability=Rank(t.ability),training=Rank(t.training);
   if(Known(leadership)){
    int bonds=members.Count(m=>Active(m.potentials,s=>s=="유대").Any()),limit=BondLimit(leadership);if(bonds>limit)problems.Add("『유대』 "+bonds+"체 — 통솔 "+leadership+"는 "+limit+"체까지");
    foreach(var m in members){
     foreach(var role in Active(m.potentials,s=>s=="역할")){if(role.name=="엠퍼러"&&!At(leadership,"S"))problems.Add(m.name+": 『엠퍼러』는 통솔 S 필요");else if(LeadershipRoles.Contains(role.name)&&!At(leadership,"A"))problems.Add(m.name+": 『"+role.name+"』는 통솔 A 필요");}
     foreach(var privilege in Active(m.potentials,s=>s=="특권"))if(privilege.name.Contains("익스펜션")&&!At(leadership,"A"))problems.Add(m.name+": 『"+privilege.name+"』는 통솔 A 필요");
    }
   }
   if(Known(ability)){
    int unique=Active(t.potentials,s=>s=="고유").Count(),exclusive=members.Sum(m=>Active(m.potentials,s=>s==ExclusiveSlot).Count());
    if(unique>UniqueLimit(ability))problems.Add("고유 포텐셜 "+unique+"개 — 능력 "+ability+"는 "+UniqueLimit(ability)+"개까지");
    if(exclusive>ExclusiveLimit(ability))problems.Add("전용 포텐셜 "+exclusive+"개 — 능력 "+ability+"는 "+ExclusiveLimit(ability)+"개까지");
   }
   if(Known(training))foreach(var m in members)foreach(var p in Active(m.potentials,s=>s.StartsWith("종족"))){string need=SpeciesTierRank(p.slot);if(need.Length>0&&!At(training,need))problems.Add(m.name+": 『"+p.slot+"』는 육성 "+need+" 필요");}
   return problems;
  }
  public static void Test(){
   var trainer=new TrainerRecord{name="T"};trainer.leadership.rank="B";trainer.ability.rank="C";trainer.training.rank="C";
   var a=new PokemonRecord{name="A"};a.potentials.Add(new PotentialRecord{slot="유대",name="영혼의 유대"});a.potentials.Add(new PotentialRecord{slot="역할",name="어벤저"});a.potentials.Add(new PotentialRecord{slot="종족 ③",name="시험"});
   var b=new PokemonRecord{name="B"};b.potentials.Add(new PotentialRecord{slot="유대",name="사랑의 유대"});b.potentials.Add(new PotentialRecord{slot="특권",name="익스펜션（물）"});
   for(int i=0;i<4;i++)a.potentials.Add(new PotentialRecord{slot="전용",name="전용"+i});
   var team=new TeamRecord{trainer=trainer};team.members.Add(a);team.members.Add(b);var found=Check(team);
   if(found.Count!=5||!found.Any(x=>x.Contains("유대"))||!found.Any(x=>x.Contains("어벤저"))||!found.Any(x=>x.Contains("익스펜션"))||!found.Any(x=>x.Contains("전용"))||!found.Any(x=>x.Contains("종족 ③")))throw new Exception("Team aptitude rules failed: "+string.Join(" / ",found));
   trainer.leadership.rank="A";trainer.ability.rank="B";trainer.training.rank="B";if(Check(team).Count!=0)throw new Exception("Team aptitude rules false positive: "+string.Join(" / ",Check(team)));
   trainer.leadership.rank="";trainer.ability.rank="";trainer.training.rank="";trainer.potentials.Add(new PotentialRecord{slot="고유",name="x"});if(Check(team).Count!=0)throw new Exception("Unranked trainer should not be checked");
  }
 }
}
