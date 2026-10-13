using System;
using System.Linq;
using System.Collections.Generic;

namespace AABattle {
 // 우선도 변경 포텐셜 실행
 public static class PriorityEffectRuntime {
  static readonly System.Text.RegularExpressions.Regex Pattern=new System.Text.RegularExpressions.Regex(@"^(?<target>자신|상대|아군(?:\s+1체|\s+전체)?)의\s*기술\s*우선도를\s*(?<amount>[1-7])\s*(?<direction>올린다|내린다)\.?$",System.Text.RegularExpressions.RegexOptions.Compiled);
  public static int ForMove(Fighter user,Fighter opponent,TrainerRecord ownTrainer,TrainerRecord opponentTrainer,Action<string> log){
   if(user==null||user.HP<=0)return 0;int total=user.Conditions.PriorityModifier;
   total+=FromPokemon(user,opponent,user,ownTrainer,log);total+=FromPokemon(opponent,user,user,opponentTrainer,log);
   total+=FromTrainer(ownTrainer,user,opponent,user,log);total+=FromTrainer(opponentTrainer,opponent,user,user,log);return total;
  }
  static int FromPokemon(Fighter source,Fighter other,Fighter user,TrainerRecord trainer,Action<string> log){
   if(source==null||source.HP<=0)return 0;int total=0;
   var active=(source.Data.potentials??new Potential[0]).Where(p=>p!=null&&p.description!=null&&p.description.Length>0&&IsContinuous(p,source,other,trainer)&&!PotentialNullify.IsNullified(source,p,other,null));
   foreach(var p in active)total+=Read(p.description,source,other,user,p.name,log);return total;
  }
  static int FromTrainer(TrainerRecord trainer,Fighter owner,Fighter other,Fighter user,Action<string> log){
   if(trainer==null||owner==null||owner.HP<=0)return 0;int total=0;
   foreach(var p in TrainerTriggers.Matching(trainer,TriggerStructures.AlwaysLabel).Concat(TrainerTriggers.Matching(trainer,TriggerStructures.FieldLabel)).Distinct())total+=Read(p.effect,owner,other,user,p.name,log);
   return total;
  }
  public static void PrimeTurn(Fighter[] fighters,BattleCommand[] commands,TrainerBattleState[] states,Action<string> log){
   if(fighters==null||commands==null)return;
   for(int i=0;i<Math.Min(2,commands.Length);i++){var command=commands[i];var move=command==null?null:command.Move;if(command==null||command.IsSwitch||move==null||fighters[i]==null||fighters[i].HP<=0)continue;var other=fighters[1-i];string context=fighters[i].Data.name+"이 "+move.name+"을 선택";primeUsage=states!=null&&i<states.Length&&states[i]!=null?states[i].Usage:null;ActivateDirect(fighters[i],other,TriggerStructures.MoveUseLabel,context,log);ActivateDirect(fighters[i],other,move.category=="변화"?TriggerStructures.StatusMoveUseLabel:TriggerStructures.AttackMoveUseLabel,context,log);ActivateDirect(fighters[i],other,TriggerStructures.MoveName(move.name),context,log);foreach(string type in move.types??new string[0])ActivateDirect(fighters[i],other,TriggerStructures.MoveType(type),context,log);var trainer=states!=null&&i<states.Length&&states[i]!=null?states[i].Trainer:null;ActivateTrainerEvent(trainer,fighters[i],other,TriggerStructures.MoveUseLabel,context,log);ActivateTrainerEvent(trainer,fighters[i],other,move.category=="변화"?TriggerStructures.StatusMoveUseLabel:TriggerStructures.AttackMoveUseLabel,context,log);}
   primeUsage=null;
  }
  // 효과 실행 범위 안에서는 공통 효과 큐의 「우선도」 실행기가 처리한다. 기술 선택 단계(PrimeTurn)는 ActivateDirect를 쓴다
  public static void ActivateEvent(Fighter source,Fighter other,string label,string context,Action<string> log){
   if(PotentialQueue.Active)return;
   ActivateDirect(source,other,label,context,log);
  }
  // 기술 선택 단계에서만 쓴다: 선행입력은 선언한 것만, 사건 절과 함께 쓴 상태 절은 모두 성립해야 한다
  [ThreadStatic] static PotentialUsage primeUsage;
  static bool Allowed(Potential p,Fighter source,Fighter other,string label){
   if((p.activation??"").Trim()=="선행"&&primeUsage!=null&&!primeUsage.Declared.Contains(PotentialKeys.For(source.Data,p)))return false;
   foreach(string clause in TriggerStructures.Clauses(p)){
    if(TriggerStructures.Normalize(clause)==label)continue;
    var spec=TriggerModules.Parse(clause);
    if(spec.IsEvent)continue;
    if(spec.Module.Length>0&&!TriggerModules.Holds(clause,source,other,new Rules()))return false;
   }
   return true;
  }
  static void ActivateDirect(Fighter source,Fighter other,string label,string context,Action<string> log){
   if(source==null||source.HP<=0)return;
   foreach(var p in (source.Data.potentials??new Potential[0]).Where(x=>x!=null&&MatchesEvent(x,label)&&Allowed(x,source,other,label)))Apply(p.description,source,other,label,context,log);
  }
  public static void ActivateTrainerEvent(TrainerRecord trainer,Fighter owner,Fighter other,string label,string context,Action<string> log){
   if(trainer==null||owner==null||owner.HP<=0)return;
   foreach(var p in TrainerTriggers.Matching(trainer,label).Distinct())Apply(p.effect,owner,other,label,context,log);
  }
  static bool MatchesEvent(Potential p,string label){return TriggerStructures.Clauses(p).Any(x=>TriggerStructures.Normalize(x)==label)&&!IsContinuous(p,null,null,null);}
  static void Apply(string effect,Fighter source,Fighter other,string label,string context,Action<string> log){
   string condition,body;PotentialLibrary.Split(effect??"",out condition,out body);if(body.Length==0)body=effect??"";
   foreach(string line in body.Replace("\r","").Split(new[]{'\n'},StringSplitOptions.RemoveEmptyEntries)){string clean=PotentialParts.Clean(line);clean=System.Text.RegularExpressions.Regex.Replace(clean,@"^T\s*종료시까지\s*","");var match=Pattern.Match(clean);if(!match.Success)continue;string target=match.Groups["target"].Value;Fighter recipient=target=="상대"?other:source;if(recipient==null||recipient.HP<=0)continue;int amount=int.Parse(match.Groups["amount"].Value);if(match.Groups["direction"].Value=="내린다")amount=-amount;recipient.Conditions.PriorityModifier+=amount;recipient.Conditions.PriorityModifierTurns=1;if(log!=null)log("『우선도 효과』 "+label+" · "+context+" · "+recipient.Data.name+" 우선도 "+(amount>=0?"+":"")+amount+" (이번 T)");}
  }
  // 큐 실행기용: 기술 선택 단계가 처리하는 라벨인가, 상시 포텐셜인가, 우선도 문장인가, 한 줄 적용
  public static bool SelectionLabel(string label){string value;var spec=TriggerModules.Parse(label??"");if(spec.Module=="기술 사용"&&!spec.After)return true;return label==TriggerStructures.MoveUseLabel||label==TriggerStructures.StatusMoveUseLabel||label==TriggerStructures.AttackMoveUseLabel||TriggerStructures.TryMoveName(label??"",out value)||TriggerStructures.TryMoveType(label??"",out value);}
  public static bool Continuous(Potential p){return p!=null&&IsContinuous(p,null,null,null);}
  static string Prepare(string line){return System.Text.RegularExpressions.Regex.Replace(PotentialParts.Clean(line??"").Trim(),@"^T\s*종료시까지\s*","");}
  public static bool Matches(string line){return Pattern.IsMatch(Prepare(line));}
  public static bool ApplyLine(string line,Fighter source,Fighter other,string label,string context,Action<string> log){var match=Pattern.Match(Prepare(line));if(!match.Success)return false;Fighter recipient=match.Groups["target"].Value=="상대"?other:source;if(recipient==null||recipient.HP<=0)return false;int amount=int.Parse(match.Groups["amount"].Value);if(match.Groups["direction"].Value=="내린다")amount=-amount;recipient.Conditions.PriorityModifier+=amount;recipient.Conditions.PriorityModifierTurns=1;if(log!=null)log("『우선도 효과』 "+label+" · "+context+" · "+recipient.Data.name+" 우선도 "+(amount>=0?"+":"")+amount+" (이번 T)");return true;}
  public static void EndTurn(Fighter fighter,Action<string> log){if(fighter==null)return;if(fighter.Conditions.PriorityModifierTurns>0&&--fighter.Conditions.PriorityModifierTurns==0){if(log!=null&&fighter.Conditions.PriorityModifier!=0)log(fighter.Data.name+": 이번 T 우선도 보정 종료");fighter.Conditions.PriorityModifier=0;}}
  static bool IsContinuous(Potential p,Fighter source,Fighter other,TrainerRecord trainer){
   var clauses=TriggerStructures.Clauses(p).Select(TriggerStructures.Normalize).ToArray();
   if(clauses.Contains(TriggerStructures.AlwaysLabel)||clauses.Contains(TriggerStructures.FieldLabel))return true;
   if(clauses.Contains(TriggerStructures.HereLabel))return false;
   if(source==null)return false;
   foreach(string clause in clauses){string granted;if(TriggerStructures.TryGranted(clause,out granted)&&(source.Data.potentials??new Potential[0]).Any(x=>x!=null&&(x.name==granted||x.name.StartsWith(granted+" (")||x.name.StartsWith(granted+"（"))))return true;}
   return false;
  }
  static int Read(string effect,Fighter source,Fighter other,Fighter user,string name,Action<string> log){
   string condition,body;PotentialLibrary.Split(effect??"",out condition,out body);if(body.Length==0)body=effect??"";int total=0;
   foreach(string line in body.Replace("\r","").Split(new[]{'\n'},StringSplitOptions.RemoveEmptyEntries)){var match=Pattern.Match(PotentialParts.Clean(line));if(!match.Success)continue;string target=match.Groups["target"].Value;Fighter recipient=target=="상대"?other:source;if(recipient==null||recipient!=user)continue;int amount=int.Parse(match.Groups["amount"].Value);if(match.Groups["direction"].Value=="내린다")amount=-amount;total+=amount;if(log!=null)log("『"+name+"』 "+line+" · 행동 우선도 보정 "+(amount>=0?"+":"")+amount);}
   return total;
  }
 }
}
