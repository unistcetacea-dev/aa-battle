using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace AABattle {
 public enum EntryReason {Lead,Switch,Replacement,Forced}

 public static class TriggerStructures {
  public const string AlwaysLabel="X";
  public const string EntryLabel="장소에 나올 때";
  public const string DefeatLabel="상대를 쓰러뜨렸을 때";

  public static string NormalizeLines(string text){return string.Join("\r\n",(text??"").Split(new[]{(char)13,(char)10},StringSplitOptions.RemoveEmptyEntries).Select(Normalize).Distinct());}
  public static string Normalize(string text){
   string value=PotentialParts.Clean(text);string key=Regex.Replace(value,@"\s+","");
   if(key=="X"||key=="x"||key=="×")return AlwaysLabel;
   if(Regex.IsMatch(key,@"^(?:자신이?|자신의포켓몬이)?(?:필드|장소)에(?:나와|나오면|나왔을때|나왔을때에|나올때|나온때)$"))return EntryLabel;
   if(Regex.IsMatch(key,@"^(?:자동)?(?:자신이)?상대를쓰러(?:뜨|트)렸을때$"))return DefeatLabel;
   value=Regex.Replace(value,@"(?:필드|장소)에\s*(?:나왔을\s*때(?:에)?|나오면|나온\s*때|나올\s*때|나와)$",EntryLabel);
   value=value.Replace("쓰러트렸을 때","쓰러뜨렸을 때");
   return Regex.Replace(value,@"교대해서\s*","교대해 ");
  }
  public static bool IsAlways(string trigger){return Normalize(trigger)==AlwaysLabel;}
  public static void Fire(Fighter fighter,string label,string eventText,Action<string> log){
   log(eventText);
   foreach(var potential in fighter.Data.potentials??new Potential[0]){
    var clauses=(potential.triggers??new string[0]).SelectMany(x=>(x??"").Split(new[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries)).ToList();
    string condition,effect;PotentialLibrary.Split(potential.description??"",out condition,out effect);clauses.AddRange(condition.Split(new[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries));
    if(clauses.Any(x=>Normalize(x)==label))log("『"+potential.name+"』 "+label+" 트리거 충족"+(clauses.Any(x=>Normalize(x)!=label)?" · 추가 조건 확인 필요":"")+" · 효과 판정 대기: "+effect);
   }
  }
  public static void Test(){
   foreach(var text in new[]{"필드에 나왔을때","장소에 나와","필드에 나오면","자신이 장소에 나왔을 때"})if(Normalize(text)!=EntryLabel)throw new Exception("Entry alias: "+text);
   foreach(var text in new[]{"상대를 쓰러뜨렸을 때","상대를 쓰러트렸을 때","자신이 상대를 쓰러트렸을 때","자동 상대를 쓰러뜨렸을 때"})if(Normalize(text)!=DefeatLabel)throw new Exception("Defeat alias: "+text);
   if(Normalize("「강철」포켓몬을 쓰러트렸을 때")!="「강철」포켓몬을 쓰러뜨렸을 때")throw new Exception("Typed defeat spelling");
   if(Normalize("×")!=AlwaysLabel||!IsAlways("x"))throw new Exception("Always trigger alias");
   if(Normalize("선발로 필드에 나오면")==EntryLabel||Normalize("상대가 필드에 나오면")==EntryLabel)throw new Exception("Entry specificity lost");
  }
 }

 public static class EntryTriggers {
  public const string Id="field.enter";
  public const string Label=TriggerStructures.EntryLabel;
  public static bool IsEntry(EntryReason reason){return reason!=EntryReason.Forced;}
  public static void Enter(Fighter fighter,EntryReason reason,Action<string> log){
   if(!IsEntry(reason))return;
   TriggerStructures.Fire(fighter,Label,fighter.Data.name+" · "+Label+" ["+reason+"]",log);
  }
  public static void Test(){
   TriggerStructures.Test();
   foreach(EntryReason reason in Enum.GetValues(typeof(EntryReason))){int count=0;var pokemon=new Pokemon{name="검사",level=100,@base=new[]{100,100,100,100,100,100},iv=new int[6],types=new[]{"노말"},moves=new string[0],potentials=new[]{new Potential{name="등장",description="공격이 오른다.",triggers=new[]{"장소에 나와"}}}};Enter(new Fighter(pokemon),reason,x=>count++);if(count!=(reason==EntryReason.Forced?0:2))throw new Exception("Entry dispatch");}
   DefeatTriggers.Test();
  }
 }

 public static class DefeatTriggers {
  public const string Id="opponent.defeated.by_move";
  public const string Label=TriggerStructures.DefeatLabel;
  public static void ByMove(Fighter attacker,Fighter target,Move move,int hpBefore,int damage,Action<string> log){
   if(hpBefore<=0||damage<=0||target.HP>0)return;
   TriggerStructures.Fire(attacker,Label,attacker.Data.name+"의 "+move.name+" 직접 대미지로 "+target.Data.name+"을 쓰러뜨렸다.",log);
  }
  public static void Test(){
   var source=new Fighter(new Pokemon{name="사냥꾼",level=100,@base=new[]{100,100,100,100,100,100},iv=new int[6],types=new[]{"노말"},moves=new string[0],potentials=new[]{new Potential{name="승전",description="공격이 오른다.",triggers=new[]{"상대를 쓰러트렸을 때"}}}});var target=new Fighter(new Pokemon{name="표적",level=100,@base=new[]{100,100,100,100,100,100},iv=new int[6],types=new[]{"노말"},moves=new string[0]});target.HP=0;var move=new Move{name="몸통박치기"};int count=0;ByMove(source,target,move,1,1,x=>count++);if(count!=2)throw new Exception("Direct move defeat dispatch");count=0;ByMove(source,target,move,1,0,x=>count++);ByMove(source,target,move,0,1,x=>count++);if(count!=0)throw new Exception("Indirect defeat exclusion");
  }
 }
}
