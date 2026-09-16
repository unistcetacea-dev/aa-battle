using System;
using System.Collections.Generic;
using System.Linq;

namespace AABattle {
 // 특성 전용 런타임. 특성을 포텐셜 슬롯으로 변환하지 않고 전투 사건에 직접 연결한다.
 public static class AbilityRuntime {
  public static readonly string[] EntryAbilities={"가뭄","잔비","모래날림","눈퍼뜨리기","그래스메이커","일렉트릭메이커","사이코메이커","미스트메이커","위협","다운로드"};
  public static readonly string[] SupportedAbilities={"재생력","자연회복","가속","멀티스케일","심술꾸러기","단순","클리어바디","하얀연기","미러아머","오기","승기","적응력","테크니션","철주먹","메가런처","옹골찬턱","예리함","이판사판","화학변화가스","옹골참"};
  static readonly Dictionary<string,string> Weather=new Dictionary<string,string>{{"가뭄","쾌청"},{"잔비","비"},{"모래날림","모래바람"},{"눈퍼뜨리기","싸라기눈"}};
  static readonly Dictionary<string,string> Terrain=new Dictionary<string,string>{{"그래스메이커","그래스필드"},{"일렉트릭메이커","일렉트릭필드"},{"사이코메이커","사이코필드"},{"미스트메이커","미스트필드"}};

  public static string Name(Fighter fighter){return fighter==null?"":fighter.AbilityChanged?fighter.CurrentAbility??"":fighter.Data==null?"":fighter.Data.ability??"";}
  public static bool Has(Fighter fighter,string name){return fighter!=null&&fighter.OnField&&Name(fighter)==name&&(!fighter.AbilitySuppressed||name=="화학변화가스");}
  public static void Reset(Fighter fighter){if(fighter==null)return;fighter.OriginalAbility=fighter.Data==null?"":fighter.Data.ability??"";fighter.CurrentAbility=fighter.OriginalAbility;fighter.AbilityChanged=false;fighter.AbilitySuppressed=false;}
  // 특성 교체·복사는 등장으로 보지 않으므로 여기서는 이름만 바꾼다.
  public static void SetCurrent(Fighter fighter,string ability){if(fighter!=null){fighter.CurrentAbility=ability??"";fighter.AbilityChanged=true;}}

  public static void RefreshSuppression(Fighter[] fighters,Action<string> log){
   if(fighters==null||fighters.Length!=2)return;bool[] gas={Gas(fighters[0]),Gas(fighters[1])};
   for(int i=0;i<2;i++){var f=fighters[i];if(f==null)continue;bool next=gas[1-i]&&Name(f)!="화학변화가스";if(f.AbilitySuppressed==next)continue;f.AbilitySuppressed=next;if(log!=null)log(f.Data.name+": 특성 「"+Name(f)+"」 "+(next?"무효화":"상시 효과 재개"));}
  }
  static bool Gas(Fighter fighter){return fighter!=null&&fighter.OnField&&fighter.HP>0&&Name(fighter)=="화학변화가스";}
  static bool ChangeStage(Fighter target,Fighter other,int index,int amount,Rules rules,string reason,Action<string> log){var lines=new List<string>();bool changed=Mechanics.ChangeStage(target,other,index,amount,rules,reason,lines);foreach(string line in lines)log(line);return changed;}

  public static void Enter(Fighter owner,Fighter opponent,Rules rules,Action<string> log){
   if(owner==null||opponent==null||rules==null||log==null||owner.HP<=0||owner.Conditions.AbilityEntryApplied)return;owner.Conditions.AbilityEntryApplied=true;if(owner.AbilitySuppressed)return;string ability=Name(owner),next;
   if(Weather.TryGetValue(ability,out next)){if(rules.Weather=="큰가뭄"||rules.Weather=="강한 비"||rules.Weather=="난기류"){log(owner.Data.name+"의 「"+ability+"」 — 특수 날씨를 덮어쓸 수 없다.");return;}if(rules.Weather==next)return;string before=rules.Weather;rules.Weather=next;rules.WeatherTurns=5;log(owner.Data.name+"의 「"+ability+"」: 날씨를 "+next+"으로 했다 (5T).");AnsweredTriggers.WeatherChanged(owner,opponent,before,next,log);AnsweredTriggers.AbilityActivated(owner,ability,log);return;}
   if(Terrain.TryGetValue(ability,out next)){if(rules.Terrain==next)return;string before=rules.Terrain;rules.Terrain=next;rules.TerrainTurns=5;log(owner.Data.name+"의 「"+ability+"」: 필드를 "+next+"로 했다 (5T).");AnsweredTriggers.TerrainChanged(owner,opponent,before,next,log);AnsweredTriggers.AbilityActivated(owner,ability,log);return;}
   if(ability=="위협"){if(opponent.OnField&&ChangeStage(opponent,owner,1,-1,rules,ability,log)){log(owner.Data.name+"의 「위협」!");AnsweredTriggers.AbilityActivated(owner,ability,log);}return;}
   if(ability=="다운로드"&&opponent.OnField){int stat=Engine.Effective(opponent,owner,2,rules)>Engine.Effective(opponent,owner,4,rules)?3:1;if(ChangeStage(owner,opponent,stat,1,rules,ability,log))AnsweredTriggers.AbilityActivated(owner,ability,log);}
  }

  public static void Leave(Fighter owner,Fighter opponent,Rules rules,SwitchReason reason,List<string> log){
   if(owner==null||opponent==null||rules==null||log==null||owner.HP<=0||reason==SwitchReason.Fainted||owner.AbilitySuppressed)return;string ability=Name(owner);
   if(ability=="재생력"){int max=Engine.Stats(owner,opponent.Data.level,rules)[0],gain=Math.Min(max-owner.HP,Math.Max(1,max/3));if(gain>0){owner.HP+=gain;log.Add(owner.Data.name+"의 「재생력」: HP +"+gain);AnsweredTriggers.Healed(owner,gain,ability,log.Add);AnsweredTriggers.AbilityActivated(owner,ability,log.Add);}}
   if(ability=="자연회복"&&owner.Status!="정상"){string old=owner.Status;owner.Status="정상";log.Add(owner.Data.name+"의 「자연회복」: "+old+" 치유");AnsweredTriggers.AbilityActivated(owner,ability,log.Add);}
  }
  public static void EndTurn(Fighter owner,Fighter opponent,Rules rules,List<string> log){if(owner==null||owner.HP<=0||owner.AbilitySuppressed||Name(owner)!="가속")return;if(Mechanics.ChangeStage(owner,opponent,5,1,rules,"가속",log))AnsweredTriggers.AbilityActivated(owner,"가속",log.Add);}

  public static double Stab(Fighter attacker,double normal){return Has(attacker,"적응력")&&normal>1?2:normal;}
  public static double Power(Fighter attacker,Move move){
   if(attacker==null||move==null)return 1;double value=1;string[] tags=move.tags??new string[0];
   if(Has(attacker,"테크니션")&&move.power<=60)value*=1.5;
   if(Has(attacker,"철주먹")&&tags.Any(x=>x.Contains("펀치")))value*=1.5;
   if(Has(attacker,"메가런처")&&tags.Any(x=>x.Contains("파동")||x.Contains("탄/볼")))value*=1.5;
   if(Has(attacker,"옹골찬턱")&&tags.Any(x=>x.Contains("물기/엄니")))value*=1.5;
   if(Has(attacker,"예리함")&&tags.Any(x=>x.Contains("칼/검")))value*=1.5;
   if(Has(attacker,"이판사판")&&tags.Any(x=>x.Contains("반동")))value*=1.2;return value;
  }
  public static double Damage(Fighter attacker,Fighter defender,Rules rules){if(defender!=null&&attacker!=null&&!Mechanics.BypassAbility(attacker)&&Has(defender,"멀티스케일")&&defender.HP>=Engine.Stats(defender,attacker.Data.level,rules)[0])return .5;return 1;}
  public static bool BlocksCritical(Fighter attacker,Fighter defender){return defender!=null&&!Mechanics.BypassAbility(attacker)&&Has(defender,"멀티스케일");}
  public static bool Endures(Fighter attacker,Fighter defender,Rules rules){return defender!=null&&!Mechanics.BypassAbility(attacker)&&(Has(defender,"옹골참")||Has(defender,"옹골찬턱"))&&defender.HP>=Engine.Stats(defender,attacker.Data.level,rules)[0];}
  public static double Evasion(Fighter attacker,Fighter defender){return defender!=null&&!Mechanics.BypassAbility(attacker)&&Has(defender,"하얀연기")?1.1:1;}

  public static void Test(){
   var p=new Pokemon{name="가뭄 검사",ability="가뭄",level=50,@base=new[]{100,100,100,100,100,100},iv=new int[6],types=new[]{"불꽃"},moves=new string[0],potentials=new Potential[0]};var a=new Fighter(p);var b=new Fighter(new Pokemon{name="상대",level=50,@base=new[]{100,100,100,100,100,100},iv=new int[6],types=new[]{"노말"},moves=new string[0],potentials=new Potential[0]});var r=new Rules();var log=new List<string>();Enter(a,b,r,log.Add);int count=log.Count;SetCurrent(a,"잔비");Enter(a,b,r,log.Add);if(r.Weather!="쾌청"||r.WeatherTurns!=5||!a.Conditions.AbilityEntryApplied||count==0||log.Count!=count)throw new Exception("Ability entry/change runtime");
   var gas=new Fighter(new Pokemon{name="가스",ability="화학변화가스",level=50,@base=new[]{100,100,100,100,100,100},iv=new int[6],types=new[]{"독"},moves=new string[0],potentials=new Potential[0]});RefreshSuppression(new[]{a,gas},null);if(!a.AbilitySuppressed||Has(a,"잔비"))throw new Exception("Ability suppression");gas.HP=0;RefreshSuppression(new[]{a,gas},null);if(a.AbilitySuppressed||!Has(a,"잔비"))throw new Exception("Continuous ability restoration");
  }
 }
}
