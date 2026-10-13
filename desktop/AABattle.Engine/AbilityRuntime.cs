using System;
using System.Collections.Generic;
using System.Linq;

namespace AABattle {
 // 현재 특성과 슬롯 동기화·무효화 생명주기. 효과 실행은 문장 모듈이 맡는다.
 public static class AbilityRuntime {
  public static readonly string[] EntryAbilities={"가뭄","잔비","모래날림","눈퍼뜨리기","그래스메이커","일렉트릭메이커","사이코메이커","미스트메이커","위협","다운로드"};
  public static readonly string[] SupportedAbilities={"재생력","자연회복","가속","멀티스케일","심술꾸러기","단순","클리어바디","하얀연기","미러아머","오기","승기","적응력","테크니션","철주먹","메가런처","옹골찬턱","예리함","이판사판","화학변화가스","옹골참"};

  public static string Name(Fighter fighter){return fighter==null?"":fighter.AbilityChanged?fighter.CurrentAbility??"":fighter.Data==null?"":fighter.Data.ability??"";}
  public static bool Has(Fighter fighter,string name){return fighter!=null&&fighter.OnField&&Name(fighter)==name&&!fighter.AbilitySuppressed;}
  public static void Reset(Fighter fighter){if(fighter==null)return;fighter.OriginalAbility=fighter.Data==null?"":fighter.Data.ability??"";fighter.CurrentAbility=fighter.OriginalAbility;fighter.AbilityChanged=false;fighter.AbilitySuppressed=false;AbilityModules.Sync(fighter);}
  // 특성 교체·복사는 등장으로 보지 않으므로 여기서는 이름만 바꾼다.
  public static void SetCurrent(Fighter fighter,string ability){
   if(fighter==null||AbilityChangeModules.CannotChange(fighter))return;
   fighter.CurrentAbility=ability??"";
   fighter.AbilityChanged=true;
   AbilityModules.Sync(fighter);
   AbilityChangeModules.Refresh(null);
  }

  public static void RefreshSuppression(Fighter[] fighters,Action<string> log){
   if (fighters == null || fighters.Length != 2) return;
   bool[] suppresses = {
    AbilityPhrases.SuppressesAbilities(fighters[0], fighters[1]),
    AbilityPhrases.SuppressesAbilities(fighters[1], fighters[0])
   };
   bool[] immune = {
    AbilityPhrases.SuppressesAbilities(fighters[0], fighters[1], true),
    AbilityPhrases.SuppressesAbilities(fighters[1], fighters[0], true)
   };
   for (int i = 0; i < 2; i++) {
    var f = fighters[i];
    if (f == null) continue;
    bool next = suppresses[1 - i] && !immune[i] && !AbilityChangeModules.CannotSuppress(f, fighters[1 - i]);
    if (f.AbilitySuppressed == next) continue;
    f.AbilitySuppressed = next;
    AbilityModules.Sync(f);
    if (log != null) log(f.Data.name + ": 특성 「" + Name(f) + "」 " + (next ? "무효화" : "상시 효과 재개"));
   }
   var scope = EffectScope.Current;
   if (scope != null) {
    var lines = new List<string>();
    AbilityPhrases.RefreshWeatherLock(fighters, scope.Rules, lines);
    if (log != null) foreach (string line in lines) log(line);
   }
   foreach (var fighter in fighters) ItemRuntime.Sync(fighter);
  }

  public static void Enter(Fighter owner,Fighter opponent,Rules rules,Action<string> log){
   if(owner==null||opponent==null||rules==null||log==null||owner.HP<=0||owner.Conditions.AbilityEntryApplied)return;owner.Conditions.AbilityEntryApplied=true;
  }

  public static void Leave(Fighter owner,Fighter opponent,Rules rules,SwitchReason reason,List<string> log){}
  public static void EndTurn(Fighter owner,Fighter opponent,Rules rules,List<string> log){}

  public static double Stab(Fighter attacker,double normal){return normal;}
  public static double Power(Fighter attacker,Move move){return 1;}
  public static double Damage(Fighter attacker,Fighter defender,Rules rules){return 1;}
  public static bool BlocksCritical(Fighter attacker,Fighter defender){return defender!=null&&!Mechanics.BypassAbility(attacker)&&AbilityPhrases.NoCrit(defender,attacker);}
  public static bool Endures(Fighter attacker,Fighter defender,Rules rules){return false;}
  public static double Evasion(Fighter attacker,Fighter defender){return 1;}

  public static void Test(){
   var p=new Pokemon{name="가뭄 검사",ability="가뭄",level=50,@base=new[]{100,100,100,100,100,100},iv=new int[6],types=new[]{"불꽃"},moves=new string[0],potentials=new Potential[0]};var a=new Fighter(p);var b=new Fighter(new Pokemon{name="상대",level=50,@base=new[]{100,100,100,100,100,100},iv=new int[6],types=new[]{"노말"},moves=new string[0],potentials=new Potential[0]});if(!AbilityModules.Runnable(AbilityCatalog.Find("가뭄"))||!AbilityModules.Runnable(AbilityCatalog.Find("위협"))||AbilityModules.Dedicated.Contains("가뭄"))throw new Exception("Ability entry modules");
   var gas=new Fighter(new Pokemon{name="가스",ability="화학변화가스",level=50,@base=new[]{100,100,100,100,100,100},iv=new int[6],types=new[]{"독"},moves=new string[0],potentials=new Potential[0]});RefreshSuppression(new[]{a,gas},null);if(!a.AbilitySuppressed||a.Data.potentials.Any(x=>x!=null&&x.id==AbilityModules.Slot))throw new Exception("Ability suppression");gas.HP=0;RefreshSuppression(new[]{a,gas},null);if(a.AbilitySuppressed||!a.Data.potentials.Any(x=>x!=null&&x.id==AbilityModules.Slot))throw new Exception("Continuous ability restoration");
  }
 }
}
