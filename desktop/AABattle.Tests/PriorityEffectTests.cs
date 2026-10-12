using System;
using System.Linq;

namespace AABattle {
 public static class PriorityEffectTests {
  static void Check(bool value,string name){if(!value)throw new Exception("Priority effect FAIL: "+name);}
  static Fighter F(string name,string effect="",string trigger="X"){
   var p=new Pokemon{name=name,level=100,types=new[]{"노말"},moves=new string[0],@base=new[]{100,100,100,100,100,100},iv=new[]{31,31,31,31,31,31},potentials=effect.Length==0?new Potential[0]:new[]{new Potential{name="우선도 효과",description=effect,triggers=new[]{trigger}}}};return new Fighter(p);
  }
  static Move M(string name){return new Move{name=name,category="물리",types=new[]{"노말"},tags=new string[0],power=80,accuracy=100,attack="공격",defense="방어",priority=0};}
  public static void Run(Database db){
   var up=F("상승자","자신의 기술 우선도를 2 올린다.");var plain=F("일반자");var move=M("우선도기술");
   Check(Mechanics.Priority(up,move,plain)==2,"self priority increase");
   var down=F("하락자","상대의 기술 우선도를 2 내린다.");Check(Mechanics.Priority(plain,move,down)==-2,"opponent priority decrease");
   var trainer=new TrainerRecord{potentials=new System.Collections.Generic.List<PotentialRecord>{new PotentialRecord{slot="고유",name="고유 우선도",trigger="X",effect="아군의 기술 우선도를 1 올린다."}}};
   Check(Mechanics.Priority(plain,move,F("상대"),trainer,new TrainerRecord())==1,"trainer priority effect");
   var result=Engine.Resolve(db,new[]{up,plain},new[]{move,M("일반기술")},new Rules(),()=>.5);
   int first=result.Log.FindIndex(x=>x.Contains("상승자의 우선도기술!")),second=result.Log.FindIndex(x=>x.Contains("일반자의 일반기술!"));
   Check(first>=0&&second>=0&&first<second,"battle action order uses effect");
   var evented=F("이벤트자","T 종료시까지 자신의 기술 우선도를 2 올린다.",TriggerStructures.AttackSuccessLabel);PriorityEffectRuntime.ActivateEvent(evented,plain,TriggerStructures.AttackSuccessLabel,"명중",null);Check(Mechanics.Priority(evented,move,plain)==2,"mid-turn event applies priority");PriorityEffectRuntime.EndTurn(evented,null);Check(Mechanics.Priority(evented,move,plain)==0,"turn-limited priority expires");
  }
 }
}
