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
    if(reason==SwitchReason.Normal||reason==SwitchReason.Return||reason==SwitchReason.Forced)return true;
    return !HasSwitchRestriction(restrictionHolder);
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
}
