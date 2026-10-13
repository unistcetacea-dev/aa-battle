using System;
using System.Collections.Generic;
using System.Linq;

namespace AABattle {
 public enum SwitchTiming { MidTurn, TurnEnd }

 public sealed class SwitchRequest {
  public int Side;
  public SwitchTiming Timing;
  public string Source="";
  public string PotentialName="";
  public string Description="";
  public Fighter Outgoing;
  public Fighter[] Candidates=new Fighter[0];
 }

 public sealed class SwitchRecord {
  public int Side;
  public SwitchTiming Timing;
  public SwitchReason Reason;
  public string Source="";
  public Fighter Outgoing;
  public Fighter Incoming;
 }

 // 교대 효과의 의미를 자연어 전체에 맡기지 않고, 이미 확정된 사건 로그와
 // 실제 처리 단계(행동 중간/턴 종료)를 연결하는 얇은 런타임이다.
 public static class SwitchEffectRuntime {
  static string Clean(string value){return PotentialParts.Clean(value??"").Replace(" ","").Replace("　","");}

  public static bool IsPivotMove(Move move){
   if(move==null||move.name=="체인가드")return false;
   return (move.tags??new string[0]).Contains("유턴")||Clean(move.effect).Contains("유턴기술");
  }

  public static bool IsPivotGrant(string description){
   string value=Clean(description);
   return value.Contains("유턴")&&value.Contains("기술")&&!value.Contains("상대의유턴기술을무효화");
  }
  public static bool IsPivotGrant(Potential potential){return potential!=null&&IsPivotGrant(potential.description);}

  public static bool IsSwitchEffect(string description){
   string value=Clean(description);
   if(!value.Contains("교대")||(!value.Contains("아군")&&!value.Contains("임의교대")))return false;
   return !value.Contains("교대할수없다")&&!value.Contains("교대를제한")&&!value.Contains("강제교대");
  }
   public static bool IsSwitchEffect(Potential potential){return potential!=null&&IsSwitchEffect(potential.description);}

   public static bool HasSwitchRestriction(Fighter holder){
    if(holder==null||holder.Data==null||holder.HP<=0||!holder.OnField)return false;
    return (holder.Data.potentials??new Potential[0]).Any(x=>x!=null&&EffectStructures.IsSwitchRestriction(x.description)&&TriggerStructures.Has(holder,TriggerStructures.FieldLabel));
   }

   public static bool CanSwitch(Fighter restrictionHolder,SwitchReason reason){
    string block;return CanSwitch(restrictionHolder,null,reason,out block);
   }
   public static bool CanSwitch(Fighter restrictionHolder,Fighter user,SwitchReason reason,out string block){
    block="";if((reason==SwitchReason.Normal||reason==SwitchReason.Return)&&user!=null&&!AbilityStateModules.AlwaysSwitchable(user)){block=AbilityStateModules.SwitchTrapReason(restrictionHolder,user);if(block.Length>0)return false;if(user.Conditions.MeanLook)block="「검은눈빛」: 통상교대 불가";else if(user.Conditions.Bound>0)block="「조이기」: 통상교대 불가";else if(user.Conditions.HeldInSky)block="「하늘 위」: 붙잡혀 교대 불가";if(block.Length>0)return false;}
    if(reason==SwitchReason.Normal||reason==SwitchReason.Return||reason==SwitchReason.Forced)return true;
    if(HasSwitchRestriction(restrictionHolder)){block="상대의 교대 제한 효과로 비통상 교대를 할 수 없다.";return false;}return true;
   }

   static bool Mentioned(IEnumerable<string> lines,string name){
   if(string.IsNullOrWhiteSpace(name))return false;string token="『"+name+"』";
   return (lines??Enumerable.Empty<string>()).Any(x=>(x??"").Contains(token)&&x.Contains("트리거"));
  }

  public static IEnumerable<Potential> TriggeredSwitches(Fighter owner,IEnumerable<string> lines){
   if(owner==null||owner.Data==null||owner.HP<=0||!owner.OnField)return Enumerable.Empty<Potential>();
   return (owner.Data.potentials??new Potential[0]).Where(x=>IsSwitchEffect(x)&&Mentioned(lines,x.name));
  }

  static bool TrainerMentioned(IEnumerable<string> lines,string name){if(string.IsNullOrWhiteSpace(name))return false;string token="TRAINER 『"+name+"』";return (lines??Enumerable.Empty<string>()).Any(x=>(x??"").Contains(token)&&x.Contains("트리거"));}
  public static IEnumerable<PotentialRecord> TriggeredTrainerSwitches(TrainerRecord trainer,IEnumerable<string> lines){
   if(trainer==null)return Enumerable.Empty<PotentialRecord>();return (trainer.potentials??new List<PotentialRecord>()).Where(x=>x!=null&&x.enabled&&x.name.Length>0&&IsSwitchEffect(x.effect)&&TrainerMentioned(lines,x.name));
  }
  public static bool TriggeredPivotGrant(TrainerRecord trainer,IEnumerable<string> lines){
   if(trainer==null)return false;return (trainer.potentials??new List<PotentialRecord>()).Any(x=>x!=null&&x.enabled&&IsPivotGrant(x.effect)&&TrainerMentioned(lines,x.name));
  }

  public static bool TriggeredPivotGrant(Fighter owner,IEnumerable<string> lines){
   if(owner==null||owner.Data==null||owner.HP<=0||!owner.OnField)return false;
   return (owner.Data.potentials??new Potential[0]).Any(x=>IsPivotGrant(x)&&Mentioned(lines,x.name));
  }

  public static bool Limited(Potential potential){return potential!=null&&(potential.uses??"").Contains("1/");}
 }

 public static class StatIgnoreRuntime {
  public static bool IsWildBoss(Fighter fighter){return fighter!=null&&fighter.Data!=null&&fighter.HP>0&&fighter.OnField&&(fighter.Data.potentials??new Potential[0]).Any(x=>x!=null&&x.id=="역할"&&x.name=="야생의 두목");}
  static bool Covers(string stats,int index){if(index<1||index>5)return false;string value=PotentialParts.Clean(stats??"").Replace(" ","").Replace("　","").Replace("「","").Replace("」","");if(value=="능력"||value=="전능력치")return true;if(value=="공/특공"||value=="공격/특공")return index==1||index==3;if(value=="방/특방"||value=="방어/특방")return index==2||index==4;if(value=="속도/회피")return index==5;return value==Engine.Names[index];}
  static bool HasRole(Fighter fighter,string role){return fighter!=null&&fighter.Data!=null&&(fighter.Data.potentials??new Potential[0]).Any(x=>x!=null&&x.id=="역할"&&x.name==role);}
  static bool TargetMatches(Fighter holder,Fighter source,string target){if(target=="상대")return !Object.ReferenceEquals(holder,source);if(target=="자신")return Object.ReferenceEquals(holder,source);if(target!=null&&target.Length>2&&target[0]=='『'&&target[target.Length-1]=='』')return HasRole(source,target.Substring(1,target.Length-2));return false;}
  public static bool TryIgnorePositive(Fighter holder,Fighter source,int statIndex,out bool ignoreRank,out bool ignoreBoost){ignoreRank=ignoreBoost=false;if(holder==null||source==null||holder.Data==null||source.Data==null||holder.HP<=0||source.HP<=0||!holder.OnField||!TriggerStructures.Has(holder,TriggerStructures.FieldLabel))return false;foreach(var potential in holder.Data.potentials??new Potential[0]){string target,stats,mode;if(potential==null||!EffectStructures.TryStatIgnore(potential.description,out target,out stats,out mode)||!TargetMatches(holder,source,target)||!Covers(stats,statIndex))continue;if(mode=="강화")ignoreBoost=true;else ignoreRank=ignoreBoost=true;}return ignoreRank||ignoreBoost;}
 }
}
