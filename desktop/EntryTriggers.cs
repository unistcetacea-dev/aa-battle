using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace AABattle {
 public enum EntryReason {Lead,Switch,Replacement,Forced}

 public static class TriggerStructures {
  public const string AlwaysLabel="X";
  public const string EntryLabel="장소에 나올 때";
  public const string DefeatLabel="상대를 쓰러뜨렸을 때";
  public const string ObserverLabel="PT에 참여하고 있을 때";
  public const string TurnEndLabel="T 종료 시";
  public const string FieldLabel="장소에 있는 한";
  public const string SwitchLabel="아군과 임의교대할 때";
  public const string ReceivedLabel="상대의 공격을 받았을 때";

  public static string NormalizeLines(string text){return string.Join("\r\n",(text??"").Split(new[]{(char)13,(char)10},StringSplitOptions.RemoveEmptyEntries).Select(Normalize).Distinct());}
  public static string Normalize(string text){
   string value=PotentialParts.Clean(text);string key=Regex.Replace(value,@"\s+","");
   if(key=="X"||key=="x"||key=="×")return AlwaysLabel;
   if(Regex.IsMatch(key,@"^(?:자신이?|자신의포켓몬이)?(?:필드|장소)에(?:나와|나오면|나왔을때|나왔을때에|나올때|나온때)$"))return EntryLabel;
   if(Regex.IsMatch(key,@"^(?:자동)?(?:자신이)?상대를쓰러(?:뜨|트)렸을때$"))return DefeatLabel;
   if(Regex.IsMatch(key,@"^(?:자동)?(?:자신이)?PT에(?:참여|참가)(?:하고있을때|하고있는한|중일때)$"))return ObserverLabel;
   if(Regex.IsMatch(key,@"^(?:매)?(?:T|턴)종료시(?:에)?$"))return TurnEndLabel;
   if(Regex.IsMatch(key,@"^(?:자신이)?(?:필드|장소)에(?:있는한|있을때)$"))return FieldLabel;
   if(Regex.IsMatch(key,@"^(?:자동)?아군과(?:임의)?교대할때$"))return SwitchLabel;
   if(Regex.IsMatch(key,@"^(?:자동)?상대의공격을(?:받았을때|받으면)$"))return ReceivedLabel;
   value=Regex.Replace(value,@"(?:필드|장소)에\s*(?:나왔을\s*때(?:에)?|나오면|나온\s*때|나올\s*때|나와)$",EntryLabel);
   value=value.Replace("쓰러트렸을 때","쓰러뜨렸을 때");
   return Regex.Replace(value,@"교대해서\s*","교대해 ");
  }
  public static bool IsAlways(string trigger){return Normalize(trigger)==AlwaysLabel;}
  public static IEnumerable<string> Clauses(Potential potential){
   var clauses=(potential.triggers??new string[0]).SelectMany(x=>(x??"").Split(new[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries)).ToList();string condition,effect;PotentialLibrary.Split(potential.description??"",out condition,out effect);clauses.AddRange(condition.Split(new[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries));return clauses;
  }
  public static IEnumerable<Potential> Matching(Fighter fighter,string label){return (fighter.Data.potentials??new Potential[0]).Where(p=>p!=null&&Clauses(p).Any(x=>Normalize(x)==label));}
  public static bool Has(Fighter fighter,string label){return fighter!=null&&Matching(fighter,label).Any();}
  public static void Fire(Fighter fighter,string label,string eventText,Action<string> log){
   log(eventText);
   foreach(var potential in Matching(fighter,label)){
    var clauses=Clauses(potential).ToList();string condition,effect;PotentialLibrary.Split(potential.description??"",out condition,out effect);
    if(clauses.Any(x=>Normalize(x)==label))log("『"+potential.name+"』 "+label+" 트리거 충족"+(clauses.Any(x=>Normalize(x)!=label)?" · 추가 조건 확인 필요":"")+" · 효과 판정 대기: "+effect);
   }
  }
  public static void Test(){
   foreach(var text in new[]{"필드에 나왔을때","장소에 나와","필드에 나오면","자신이 장소에 나왔을 때"})if(Normalize(text)!=EntryLabel)throw new Exception("Entry alias: "+text);
   foreach(var text in new[]{"상대를 쓰러뜨렸을 때","상대를 쓰러트렸을 때","자신이 상대를 쓰러트렸을 때","자동 상대를 쓰러뜨렸을 때"})if(Normalize(text)!=DefeatLabel)throw new Exception("Defeat alias: "+text);
   foreach(var text in new[]{"PT에 참가하고 있을 때","PT에 참가하고 있는 한","자신이 PT에 참여중일 때"})if(Normalize(text)!=ObserverLabel)throw new Exception("Observer alias: "+text);
   foreach(var text in new[]{"T종료시","T종료시에","매T종료 시에","턴 종료시"})if(Normalize(text)!=TurnEndLabel)throw new Exception("Turn end alias: "+text);
   foreach(var text in new[]{"필드에 있는 한","필드에 있을 때","장소에 있는 한"})if(Normalize(text)!=FieldLabel)throw new Exception("Field alias: "+text);
   foreach(var text in new[]{"아군과 교대할 때","아군과 임의 교대할 때","자동 아군과 교대할 때"})if(Normalize(text)!=SwitchLabel)throw new Exception("Switch alias: "+text);
   foreach(var text in new[]{"상대의 공격을 받았을 때","상대의 공격을 받으면","자동 상대의 공격을 받았을 때"})if(Normalize(text)!=ReceivedLabel)throw new Exception("Received alias: "+text);
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
   RuntimeTriggers.Test();
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

 public enum SwitchReason {Normal,Return,Pivot,Potential,Forced}
 public static class RuntimeTriggers {
  public static IEnumerable<Potential> Field(Fighter fighter){return fighter==null||fighter.HP<=0?Enumerable.Empty<Potential>():TriggerStructures.Matching(fighter,TriggerStructures.AlwaysLabel).Concat(TriggerStructures.Matching(fighter,TriggerStructures.FieldLabel)).Distinct();}
  public static IEnumerable<string> Observers(IEnumerable<Fighter> reserves){return (reserves??Enumerable.Empty<Fighter>()).Where(f=>f.HP>0).SelectMany(f=>TriggerStructures.Matching(f,TriggerStructures.ObserverLabel).Select(p=>f.Data.name+" 『"+p.name+"』"));}
  public static void TurnEnd(Fighter[] fighters,Rules rules,Func<double> rng,Action<string> log){
   var eligible=Enumerable.Range(0,2).Where(i=>fighters[i].HP>0&&TriggerStructures.Has(fighters[i],TriggerStructures.TurnEndLabel)).ToList();
   eligible.Sort((left,right)=>{int speedLeft=Engine.Effective(fighters[left],fighters[1-left],5,rules),speedRight=Engine.Effective(fighters[right],fighters[1-right],5,rules);if(speedLeft!=speedRight)return speedRight.CompareTo(speedLeft);return (rng??(()=>.5))()<.5?-1:1;});
   foreach(int side in eligible)TriggerStructures.Fire(fighters[side],TriggerStructures.TurnEndLabel,fighters[side].Data.name+" · "+TriggerStructures.TurnEndLabel,log);
  }
  public static bool Voluntary(SwitchReason reason){return reason!=SwitchReason.Forced;}
  public static bool Normal(SwitchReason reason){return reason==SwitchReason.Normal||reason==SwitchReason.Return;}
  public static void Switch(Fighter outgoing,SwitchReason reason,Action<string> log){if(outgoing.HP>0&&Voluntary(reason)&&TriggerStructures.Has(outgoing,TriggerStructures.SwitchLabel))TriggerStructures.Fire(outgoing,TriggerStructures.SwitchLabel,outgoing.Data.name+" · 임의교대 ["+reason+"]",log);}
  public static void Received(Fighter target,Fighter attacker,Move move,int bodyDamage,Action<string> log){if(target.HP>0&&bodyDamage>0&&TriggerStructures.Has(target,TriggerStructures.ReceivedLabel))TriggerStructures.Fire(target,TriggerStructures.ReceivedLabel,target.Data.name+"이 "+attacker.Data.name+"의 "+move.name+"으로 "+bodyDamage+" 대미지를 받았다.",log);}
  public static void Test(){
   var p=new Pokemon{name="관측자",level=100,@base=new[]{100,100,100,100,100,120},iv=new int[6],types=new[]{"노말"},moves=new string[0],potentials=new[]{new Potential{name="상시",description="공격이 오른다.",triggers=new[]{"필드에 있는 한"}},new Potential{name="관측",description="아군의 공격이 오른다.",triggers=new[]{"PT에 참가하고 있을 때"}},new Potential{name="종료",description="공격이 오른다.",triggers=new[]{"T종료시"}},new Potential{name="선회",description="공격이 오른다.",triggers=new[]{"아군과 교대할 때"}},new Potential{name="반격",description="공격이 오른다.",triggers=new[]{"상대의 공격을 받으면"}}}};var fighter=new Fighter(p);if(Field(fighter).Count()!=1||Observers(new[]{fighter}).Count()!=1)throw new Exception("Continuous and observer triggers");fighter.HP=0;if(Field(fighter).Any()||Observers(new[]{fighter}).Any())throw new Exception("Fainted continuous trigger");fighter=new Fighter(p);int count=0;Switch(fighter,SwitchReason.Normal,x=>count++);if(count!=2)throw new Exception("Voluntary switch trigger");count=0;Switch(fighter,SwitchReason.Forced,x=>count++);if(count!=0)throw new Exception("Forced switch exclusion");var target=new Fighter(p);count=0;Received(target,fighter,new Move{name="연속공격"},3,x=>count++);if(count!=2)throw new Exception("Received after move trigger");count=0;Received(target,fighter,new Move{name="연속공격"},0,x=>count++);target.HP=0;Received(target,fighter,new Move{name="연속공격"},3,x=>count++);if(count!=0)throw new Exception("No-damage or fainted received trigger");
  }
 }
}
