using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace AABattle {
 // 교대 예약을 유지한 채 추격 행동 한 칸을 먼저 실행한다.
 public sealed class PursuitTurn {
  readonly Fighter[] original;
  readonly BattleCommand[] commands;
  readonly Rules rules;
  readonly bool[] chase=new bool[2], deferred=new bool[2], attempted=new bool[2], finished=new bool[2];
  Fighter boostedUser,boostedTarget;
  public PursuitTurn(Fighter[] fighters,BattleCommand[] orders,Rules r,Database db,TrainerBattleState[] states) {
   original=fighters.ToArray();commands=orders;rules=r;
   for(int side=0;side<2;side++) {
    int target=1-side;
    if(orders[side].IsSwitch || orders[side].Move==null || fighters[side].HP<=0 || fighters[target].HP<=0)continue;
    Move move=PursuitModules.Judge(fighters[side],fighters[target],Mechanics.ActionMove(db,fighters[side],orders[side].Move),r);
    if(!PursuitRuntime.WantsPursuit(fighters[side],fighters[target],orders[side],move,r,states[side]) || PursuitModules.ImmuneTo(fighters[target],fighters[side],r))continue;
    bool switching=orders[target].IsSwitch;
    if(switching) {
     string reason;
     if(!SwitchEffectRuntime.CanSwitch(fighters[side],fighters[target],(orders[target].Form??"").Contains("돌아와")?SwitchReason.Return:SwitchReason.Normal,out reason))continue;
    } else if(!PursuitRuntime.WantsPivot(db,fighters[target],fighters[side],orders[target],r,states[target]))continue;
    chase[side]=true;deferred[target]=switching;
   }
  }
  public bool Deferred(int side) {return deferred[side]&&!finished[1-side];}
  public void SuppressPreInput(List<string> log) {
   for(int side=0;side<2;side++)if(chase[1-side]&&!commands[side].IsSwitch) {
    var c=commands[side];
    if(string.IsNullOrEmpty(c.Form)&&string.IsNullOrEmpty(c.Order))continue;
    commands[side]=new BattleCommand {Move=c.Move,Extension=c.Extension,SwitchIn=c.SwitchIn,Trainer=c.Trainer,
     Party=c.Party,PreInputs=c.PreInputs};
    log.Add(original[side].Data.name+": 추격 끼어들기로 선행입력 트레이너 포텐셜을 발동하지 않는다");
   }
  }
  public List<ActionSlot> Order(List<ActionSlot> slots) {
   // 서로 추격하면 기존 속도·우선도 순서를 유지한다.
   if(chase[0]==chase[1])return slots;
   int side=chase[0]?0:1;
   var chosen=slots.FirstOrDefault(s=>s.Side==side&&s.Extra==null);
   if(chosen!=null){slots.Remove(chosen);slots.Insert(0,chosen);}
   return slots;
  }
  public void BeginSlot(int side,bool extra,Fighter[] fighters,List<string> log) {
   boostedUser=boostedTarget=null;
   if(extra || !chase[side] || attempted[side])return;
   attempted[side]=true;
   if(!ReferenceEquals(original[side],fighters[side]) || !ReferenceEquals(original[1-side],fighters[1-side]) ||
    fighters[side].HP<=0 || fighters[1-side].HP<=0 || PursuitModules.ImmuneTo(fighters[1-side],fighters[side],rules))return;
   boostedUser=fighters[side];boostedTarget=fighters[1-side];
   log.Add(boostedUser.Data.name+": 교대보다 먼저 추격이 끼어들었다 (위력 2배)");
  }
  public void Boost(Rules hitRules,Fighter user,Fighter target,Move move) {
   if(ReferenceEquals(user,boostedUser)&&ReferenceEquals(target,boostedTarget)&&PursuitModules.IsPursuit(move))hitRules.PowerMultiplier*=2;
  }
  // 이전 행동의 유턴까지 끝낸 뒤 원래 교대를 이어간다. 실패한 추격도 행동권은 소모된다.
  public void Complete(Fighter[] fighters,Action<int> normalSwitch,Action<int> returnSwitch,List<string> log) {
   for(int side=0;side<2;side++)if(attempted[side]&&!finished[side]) {
    finished[side]=true;int target=1-side;
    if(!deferred[target] || !ReferenceEquals(fighters[target],original[target]) || fighters[target].HP<=0 || !fighters[target].OnField)continue;
    string reason;
    var method=(commands[target].Form??"").Contains("돌아와")?SwitchReason.Return:SwitchReason.Normal;
    if(!SwitchEffectRuntime.CanSwitch(fighters[side],fighters[target],method,out reason)) {
     log.Add(fighters[target].Data.name+": 추격 후 교대 취소 — "+reason);continue;
    }
    if(method==SwitchReason.Return)returnSwitch(target);else normalSwitch(target);
   }
  }
 }
 public static class PursuitRuntime {
  public static PursuitTurn Begin(Database db,Fighter[] fighters,BattleCommand[] commands,Rules rules,TrainerBattleState[] states,List<string> log) {
   var turn=new PursuitTurn(fighters,commands,rules,db,states);turn.SuppressPreInput(log);return turn;
  }
  static bool GrantApplies(Potential potential,Fighter owner,Fighter other,Move move,Rules rules,BattleCommand command,bool pursuit=false) {
   if(potential==null || !(pursuit?PositivePursuitGrant(potential.description,owner,move):PositivePivotGrant(potential.description)))return false;
   if((potential.activation??"")=="선행" && !(command.PreInputs??new string[0]).Contains(potential.name))return false;
   foreach(string clause in TriggerStructures.Clauses(potential)) {
    var spec=TriggerModules.Parse(clause);
    if(!spec.IsEvent) {if(!TriggerModules.Holds(clause,owner,other,rules))return false;continue;}
    if(pursuit && (spec.Module!="기술 사용" || spec.After))return false;
    if(spec.Module!="기술 사용" && spec.Module!="명중")return false;
    var fact=new TriggerFact {Kind=spec.Module,Move=move,User=owner,Self=true,After=spec.After,Damaged=move.category!="변화"};
    if(!TriggerModules.Matches(spec,fact,owner))return false;
   }
   return true;
  }
  static bool PositivePursuitGrant(string description,Fighter owner,Move move) {
   string condition,effect;PotentialLibrary.Split(description??"",out condition,out effect);
   return Regex.Split(EffectNormalizer.NormalizeLines(effect.Length>0?effect:description??""),@"\r?\n|(?<=[.。])\s+")
    .Any(line=>PursuitModules.GrantsMove(line,owner,move));
  }
  public static bool WantsPursuit(Fighter owner,Fighter other,BattleCommand command,Move move,Rules rules,TrainerBattleState state) {
   if(PursuitModules.IsPursuit(move))return true;
   foreach(var potential in owner.Data.potentials??new Potential[0])if(GrantApplies(potential,owner,other,move,rules,command,true)&&
    (state==null || state.Usage.CanUse(PotentialKeys.For(owner.Data,potential),potential.uses)))return true;
   if(state==null || state.Trainer==null)return false;
   foreach(var record in state.Trainer.potentials??new List<PotentialRecord>()) {
    if(record==null || !record.enabled || record.slot!="고유" || !state.Usage.CanUse(PotentialKeys.For(state.Trainer,record),record.uses))continue;
    var potential=new Potential {description=record.effect,triggers=TrainerTriggers.ClausesOf(record).ToArray(),activation=record.activation,name=record.name};
    if(GrantApplies(potential,owner,other,move,rules,command,true))return true;
   }
   return false;
  }
  static bool PositivePivotGrant(string description) {
   string condition,effect;PotentialLibrary.Split(description??"",out condition,out effect);
   string body=effect.Length>0?effect:description??"";
   return Regex.Split(EffectNormalizer.NormalizeLines(body),@"\r?\n|(?<=[.。])\s+").Any(line=>
    Regex.IsMatch(line.Trim().TrimEnd('.','。','．'),@"^자신의 (?:(?:「[^」]+」)+\s*)?기술에 「?유턴」?\s*효과를 부여(?:한다|할 수 있다)$"));
  }
  public static bool WantsPivot(Database db,Fighter owner,Fighter other,BattleCommand command,Rules rules,TrainerBattleState state) {
   if(command.Move==null)return false;
   var move=Mechanics.ActionMove(db,owner,command.Move);
   if(SwitchEffectRuntime.IsPivotMove(move))return true;
   foreach(var potential in owner.Data.potentials??new Potential[0])if(GrantApplies(potential,owner,other,move,rules,command)&&
    (state==null || state.Usage.CanUse(PotentialKeys.For(owner.Data,potential),potential.uses)))return true;
   if(state==null || state.Trainer==null)return false;
   foreach(var record in state.Trainer.potentials??new List<PotentialRecord>()) {
    if(record==null || !record.enabled || record.slot!="고유" || !state.Usage.CanUse(PotentialKeys.For(state.Trainer,record),record.uses))continue;
    var potential=new Potential {description=record.effect,triggers=TrainerTriggers.ClausesOf(record).ToArray(),activation=record.activation,name=record.name};
    if(GrantApplies(potential,owner,other,move,rules,command))return true;
   }
   return false;
  }
 }
 // 기존 교대 실행기와 같은 경로를 추격 후에도 쓴다.
 public static partial class Engine {
  static void PursuitReturn(Fighter[] f,BattleCommand[] commands,Rules r,Func<double> rng,TurnResult result,bool[] away,BatonSnapshot[] transfers,int side) {
   RuntimeTriggers.Switch(f[side],commands[side].SwitchIn,SwitchReason.Return,result.Log.Add);
   transfers[side]=BatonModules.Capture(f[side],f[1-side],r,SwitchReason.Return,commands[side].SwitchIn,result.Log);
   AbilityRuntime.Leave(f[side],f[1-side],r,SwitchReason.Return,result.Log);
   Mechanics.LeaveField(f[side],result.Log,rng);
   result.SwitchedOut[side]=f[side].Copy();away[side]=true;
   result.Log.Add("『돌아와!』 — "+f[side].Data.name+"을 먼저 볼로 되돌렸다!");
  }
 }
}
