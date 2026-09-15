using System;
using System.Linq;
using System.Text.RegularExpressions;
namespace AABattle {
 public enum EntryReason {Lead,Switch,Replacement,Forced}
 public static class EntryTriggers {
  public const string Id="field.enter";
  public const string Label="장소에 나왔을 때";
  public static string NormalizeLines(string text){return string.Join("\r\n",(text??"").Split(new[]{(char)13,(char)10},StringSplitOptions.RemoveEmptyEntries).Select(Normalize).Distinct());} public static string Normalize(string text){
   string value=PotentialParts.Clean(text);string k=Regex.Replace(value,@"\s+","");
   if(Regex.IsMatch(k,@"^(?:자신이?|자신의포켓몬이)?(?:필드|장소)에(?:나와|나오면|나왔을때|나왔을때에|나올때|나온때)$"))return Label;
   value=Regex.Replace(value,@"(?:필드|장소)에\s*(?:나왔을\s*때(?:에)?|나오면|나온\s*때|나올\s*때|나와)$",Label);value=Regex.Replace(value,@"교대해서\s*","교대해 ");return value;
  }
  public static void Enter(Fighter fighter,EntryReason reason,Action<string> log){
   log(fighter.Data.name+" · "+Label+" ["+reason+"]");
   foreach(var p in fighter.Data.potentials??new Potential[0]){
    var clauses=(p.triggers??new string[0]).SelectMany(x=>(x??"").Split(new[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries)).ToList();
    string condition,effect;PotentialLibrary.Split(p.description??"",out condition,out effect);clauses.AddRange(condition.Split(new[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries));
    if(clauses.Any(x=>Normalize(x)==Label))log("『"+p.name+"』 등장 트리거 충족"+(clauses.Any(x=>Normalize(x)!=Label)?" · 추가 조건 확인 필요":"")+" · 효과 판정 대기: "+effect);
   }
  }
  public static void Test(){
   foreach(var x in new[]{"필드에 나왔을때","장소에 나와","필드에 나오면","자신이 장소에 나왔을 때"})if(Normalize(x)!=Label)throw new Exception("Entry alias: "+x);
   if(Normalize("선발로 필드에 나오면")==Label||Normalize("상대가 필드에 나오면")==Label)throw new Exception("Entry specificity lost");
   foreach(EntryReason r in Enum.GetValues(typeof(EntryReason))){int n=0;var p=new Pokemon{name="검사",level=100,@base=new[]{100,100,100,100,100,100},iv=new int[6],types=new[]{"노말"},moves=new string[0],potentials=new[]{new Potential{name="등장",description="공격이 오른다.",triggers=new[]{"장소에 나와"}}}};Enter(new Fighter(p),r,x=>n++);if(n!=2)throw new Exception("Entry dispatch");}
  }
 }
}
