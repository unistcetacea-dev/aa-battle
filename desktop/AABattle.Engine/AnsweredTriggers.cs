using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace AABattle {
 // 사용자와 확정한 공통 트리거 판정. 효과 실행은 아직 PotentialEffect 단계에서 맡는다.
 public static class AnsweredTriggers {
  static string Key(string value){return Regex.Replace(PotentialParts.Clean(value??""),@"\s+","");}
  static IEnumerable<Potential> Potentials(Fighter fighter){return fighter==null||fighter.Data==null?Enumerable.Empty<Potential>():(fighter.Data.potentials??new Potential[0]).Where(x=>x!=null);}
  static bool Matches(Potential potential,Func<string,bool> condition){return TriggerStructures.Clauses(potential).Any(x=>condition(Key(x)));}
  static void Fire(Fighter owner,Func<string,bool> condition,string message,Action<string> log){
   if(owner==null||owner.HP<=0)return;
   foreach(var potential in Potentials(owner).Where(x=>Matches(x,condition)))log("『"+potential.name+"』 트리거 충족 — "+message+" · 효과 판정 대기: "+potential.description);
  }
  static bool FractionAtMost(string text,int hp,int max){
   var match=Regex.Match(text,@"^(?:자신의?)?체력이(?<top>\d+)\/(?<bottom>\d+)이하일때$");
   if(!match.Success)return false;int top=int.Parse(match.Groups["top"].Value),bottom=int.Parse(match.Groups["bottom"].Value);return bottom>0&&hp*bottom<=max*top;
  }
  static bool FractionCrossed(string text,int before,int after,int max){
   var match=Regex.Match(text,@"^(?:자신의?)?체력이(?<top>\d+)\/(?<bottom>\d+)이하가?되었을때$");
   if(!match.Success)return false;int top=int.Parse(match.Groups["top"].Value),bottom=int.Parse(match.Groups["bottom"].Value);return bottom>0&&before*bottom>max*top&&after*bottom<=max*top;
  }
  static bool IsFull(string text){return text=="체력이가득일때"||text=="자신의체력이가득일때"||text=="체력이최대일때";}
  static bool IsRecover(string text){return text.Contains("체력을회복")&&(text.EndsWith("했을때")||text.EndsWith("할때"));}
  static bool IsDrain(string text){return text.Contains("드레인")&&text.Contains("체력")&&(text.EndsWith("했을때")||text.EndsWith("할때"));}
  static bool IsIndirectDamage(string text){return (text.Contains("공격이외")||text.Contains("간접"))&&text.Contains("대미지")&&(text.EndsWith("받았을때")||text.EndsWith("입었을때"));}
  static bool IsCritical(string text){return text.Contains("급소")&&(text.Contains("맞았")||text.Contains("명중"));}
  static bool IsContact(string text,bool received){return text.Contains("접촉")&&(received?(text.Contains("받았")||text.Contains("당했")):(text.Contains("명중")||text.Contains("성공")));}
  static bool IsDefendBlock(string text){return text.Contains("방어")&&(text.Contains("무효화")||text.Contains("막았"));}
  static bool IsNoAttack(string text,bool self){return (self?text.StartsWith("자신")||text.StartsWith("자기"):text.StartsWith("상대"))&&text.Contains("공격")&&text.Contains("않");}
  static bool IsBothAttack(string text){return text.Contains("서로")&&text.Contains("공격")&&(text.Contains("내보")||text.Contains("선택"));}
  static bool IsBothSureHit(string text){return text.Contains("서로")&&text.Contains("필중")&&(text.Contains("내보")||text.Contains("선택"));}
  static bool IsAbility(string text,string ability){return text.Contains("특성")&&text.Contains("발동")&&(text.Contains("「"+ability+"」")||text.Contains("『"+ability+"』")||text.Contains(ability));}
  static bool IsStatChange(string text,bool rise){return text.Contains(rise?"올랐":"내려")&&(text.Contains("능력")||text.Contains("랭크"));}
  static bool IsWeatherNow(string text,string weather){if(weather=="강한 비")return text.Contains("강한비")&&text.EndsWith("일때");if(weather=="비")return text.Contains("비")&&!text.Contains("강한비")&&text.EndsWith("일때");if(weather=="눈"||weather=="싸라기눈")return (text.Contains("눈")||text.Contains("싸라기눈"))&&text.EndsWith("일때");return text.Contains(weather)&&text.EndsWith("일때");}
  static bool IsWeatherChanged(string text,string weather){return text.Contains(weather.Replace(" ",""))&&(text.Contains("되었을때")||text.Contains("변화했을때"));}
  static bool IsTerrainNow(string text,string terrain){return terrain!="없음"&&text.Contains(terrain)&&text.EndsWith("일때");}
  static bool IsTerrainChanged(string text,string terrain){return terrain!="없음"&&text.Contains(terrain)&&(text.Contains("되었을때")||text.Contains("전개되었을때"));}
  static bool HasCurrentItem(string text,Fighter owner){var match=Regex.Match(text,@"「(?<name>[^」]+)」.*(?:소지품|도구).*(?:있을때|일때)$");return match.Success&&owner.Data.item==match.Groups["name"].Value;}
  static bool HasCurrentAbility(string text,Fighter owner){var match=Regex.Match(text,@"「(?<name>[^」]+)」.*특성.*(?:있을때|일때)$");return match.Success&&owner.Data.ability==match.Groups["name"].Value;}
  static bool HasNamedPotential(string text,Fighter owner){var match=Regex.Match(text,@"[『「](?<name>[^』」]+)[』」].*(?:있을때|일때)$");return match.Success&&Potentials(owner).Any(x=>x.name==match.Groups["name"].Value);}
  static bool IsMoveTag(string text,Move move){var match=Regex.Match(text,@"「(?<tag>[^」]+)」(?:계통|기술).*(?:명중|성공|내보)");return match.Success&&(move.tags??new string[0]).Contains(match.Groups["tag"].Value);}

  public static IEnumerable<Potential> Field(Fighter owner,Fighter opponent,Rules rules){
   if(owner==null||opponent==null||rules==null||owner.HP<=0)return Enumerable.Empty<Potential>();int max=Mechanics.MaxHP(owner,opponent,rules);
   string weather=Mechanics.Weather(owner,opponent,rules);
   return Potentials(owner).Where(p=>TriggerStructures.Clauses(p).Any(c=>{string key=Key(c);return IsFull(key)&&owner.HP>=max||FractionAtMost(key,owner.HP,max)||StageTotalCondition(key,owner)||IsWeatherNow(key,weather)||IsTerrainNow(key,rules.Terrain)||HasCurrentItem(key,owner)||HasCurrentAbility(key,owner)||HasNamedPotential(key,owner)||key.Contains("순풍")&&rules.Sides.Any(x=>x.Tailwind>0); }));
  }
  public static void HpChanged(Fighter target,Fighter other,Rules rules,int before,int after,string reason,Action<string> log){
   if(target==null||other==null||rules==null||after>=before)return;int max=Mechanics.MaxHP(target,other,rules);
   Fire(target,x=>FractionCrossed(x,before,after,max),target.Data.name+"의 HP가 "+before+"→"+after+" ("+reason+")",log);
  }
  public static void Healed(Fighter target,int amount,string reason,Action<string> log){if(amount>0)Fire(target,IsRecover,target.Data.name+"의 HP가 "+amount+" 회복 ("+reason+")",log);}
  public static void Drained(Fighter source,Fighter target,int damage,int heal,Action<string> log){if(damage>0&&heal>0)Fire(source,IsDrain,target.Data.name+"에게 "+damage+" 대미지 · "+heal+" 회복",log);}
  public static void IndirectDamage(Fighter target,Fighter other,int amount,string reason,Action<string> log){if(amount>0)Fire(other,IsIndirectDamage,target.Data.name+"이 공격 이외의 대미지 "+amount+" ("+reason+")",log);}
  public static void AttackHit(Fighter user,Fighter target,Move move,bool substitute,bool critical,Action<string> log){
   if(user==null||target==null||move==null)return;
   if(critical)Fire(user,IsCritical,user.Data.name+"의 "+move.name+"이 급소에 명중",log);
   Fire(user,x=>IsContact(x,false)&&Mechanics.Contact(move),user.Data.name+"의 접촉 기술 "+move.name+"이 명중",log);
   foreach(var potential in Potentials(user).Where(p=>Matches(p,x=>IsMoveTag(x,move))))log("『"+potential.name+"』 기술 태그 트리거 충족 — "+move.name+" · 효과 판정 대기: "+potential.description);
   if(!substitute)Fire(target,x=>IsContact(x,true)&&Mechanics.Contact(move),target.Data.name+"이 대타 없이 접촉을 받음: "+move.name,log);
  }
  public static void DefensiveBlocked(Fighter defender,Fighter attacker,Move move,string reason,Action<string> log){if(reason.StartsWith("방어"))Fire(defender,IsDefendBlock,defender.Data.name+"의 방어가 "+move.name+"을 막음",log);}
  public static void Selected(Fighter[] fighters,BattleCommand[] commands,Action<string> log){
   if(fighters==null||commands==null||fighters.Length!=2||commands.Length!=2)return;
   bool[] attack=new bool[2];bool[] sure=new bool[2];
   for(int i=0;i<2;i++){var command=commands[i]??new BattleCommand();attack[i]=!command.IsSwitch&&command.Move!=null&&command.Move.category!="변화";sure[i]=attack[i]&&(command.Form??"").Contains("맞춰라")||attack[i]&&Mechanics.SureHit(command.Move);}
   for(int i=0;i<2;i++){int other=1-i;Fire(fighters[i],x=>IsNoAttack(x,true)&&!attack[i],fighters[i].Data.name+"이 공격하지 않는 행동을 선택",log);Fire(fighters[i],x=>IsNoAttack(x,false)&&!attack[other],fighters[other].Data.name+"이 공격하지 않는 행동을 선택",log);Fire(fighters[i],x=>IsBothAttack(x)&&attack[0]&&attack[1],"서로 공격기를 선택",log);Fire(fighters[i],x=>IsBothSureHit(x)&&sure[0]&&sure[1],"서로 필중 공격기를 선택",log);}
  }
  public static void AbilityActivated(Fighter owner,string ability,Action<string> log){if(owner!=null&&ability==owner.Data.ability)Fire(owner,x=>IsAbility(x,ability),owner.Data.name+"의 특성 「"+ability+"」이 실제로 발동",log);}
  public static void WeatherChanged(Fighter source,Fighter opponent,string before,string after,Action<string> log){if(before==after||after=="없음")return;Fire(source,x=>IsWeatherChanged(x,after),"날씨가 「"+after+"」가 됨",log);Fire(opponent,x=>IsWeatherChanged(x,after),"날씨가 「"+after+"」가 됨",log);}
  public static void TerrainChanged(Fighter source,Fighter opponent,string before,string after,Action<string> log){if(before==after||after=="없음")return;Fire(source,x=>IsTerrainChanged(x,after),"필드가 「"+after+"」가 됨",log);Fire(opponent,x=>IsTerrainChanged(x,after),"필드가 「"+after+"」가 됨",log);}
  public static void StageChanged(Fighter target,Fighter other,int index,int before,int after,Action<string> log){
   if(before==after)return;bool rise=after>before;string stat=index>=0&&index<Engine.Names.Length?Engine.Names[index]:"명중/회피";
   Fire(target,x=>IsStatChange(x,rise),target.Data.name+"의 "+stat+" 랭크 "+before+"→"+after,log);
   Fire(other,x=>IsStatChange(x,!rise),target.Data.name+"의 "+stat+" 랭크 "+before+"→"+after,log);
  }
  static bool StageTotalCondition(string text,Fighter owner){
   var match=Regex.Match(text,@"^(?:자신의?)?능력(?:변화)?가?(?<sign>[+-])(?<value>\d+)(?:이상|이하)일때$");
   if(!match.Success)return false;int total=(owner.Stages??new int[0]).Sum()+owner.AccuracyStage+owner.EvasionStage;int value=int.Parse(match.Groups["value"].Value);return match.Groups["sign"].Value=="+"?total>=value:total<=-value;
  }
  public static void Test(){
   var p=new Pokemon{name="검증",level=50,@base=new[]{100,100,100,100,100,100},iv=new int[6],types=new[]{"노말"},moves=new string[0],potentials=new[]{new Potential{name="반피",description="",triggers=new[]{"자신의 체력이 1/2 이하가 되었을 때"}},new Potential{name="회복",description="",triggers=new[]{"체력을 회복했을 때"}},new Potential{name="상승",description="",triggers=new[]{"자신의 능력이 올랐을 때"}}}};var a=new Fighter(p);var b=new Fighter(p);int hits=0;HpChanged(a,b,new Rules(),100,70,"검증",x=>hits++);HpChanged(a,b,new Rules(),70,60,"검증",x=>hits++);if(hits!=1)throw new Exception("HP threshold crosses only once");Healed(a,0,"검증",x=>hits++);Healed(a,10,"검증",x=>hits++);if(hits!=2)throw new Exception("Actual heal only");StageChanged(a,b,1,6,6,x=>hits++);StageChanged(a,b,1,0,1,x=>hits++);if(hits!=3)throw new Exception("Actual stage rise only");
  }
 }
}
