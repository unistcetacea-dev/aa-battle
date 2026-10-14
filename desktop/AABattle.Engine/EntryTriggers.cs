using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace AABattle {
 public enum EntryReason {Lead,NormalSwitch,Return,Pivot,Potential,Replacement,Forced}


 public static class OptionalEffectRuntime {
  public static void Activate(Fighter owner,Fighter target,string label,Rules rules,Action<string> log){
   if(owner==null||target==null||owner.HP<=0||target.HP<=0||PotentialQueue.Active)return;
   foreach(var potential in (owner.Data.potentials??new Potential[0]).Where(x=>x!=null&&EffectStructures.IsAnalysisPermission(x.description)&&TriggerStructures.Clauses(x).Any(c=>TriggerStructures.Normalize(c)==label))){
    if(rules==null||rules.OptionalEffectPrompt==null)continue;
    if(rules.OptionalEffectPrompt(owner,target,potential)){if(log!=null){RuntimeTriggers.Analyzed(owner,new[]{owner},target,log);log("『"+potential.name+"』 선택 발동 · "+target.Data.name+"의 데이터를 해석한다.");}}
   }
  }
 }

 public static class EntryTriggers {
  public const string Id="field.enter";
  public const string Label=TriggerStructures.EntryLabel;
  public static bool IsEntry(EntryReason reason){return reason!=EntryReason.Forced;}
  public static string MethodLabel(EntryReason reason){if(reason==EntryReason.Lead)return TriggerStructures.LeadEntryLabel;if(reason==EntryReason.Replacement)return TriggerStructures.ReplacementEntryLabel;if(reason==EntryReason.NormalSwitch||reason==EntryReason.Return||reason==EntryReason.Pivot||reason==EntryReason.Potential)return TriggerStructures.VoluntaryEntryLabel;return "";}
  public static void Enter(Fighter fighter,EntryReason reason,Action<string> log){
   Enter(fighter,null,null,null,reason,log);
  }
  // partner: 교대해 들어간 아군(「「X」 타입 아군과 교대해 장소에 나올 때」)
  public static void Enter(Fighter fighter,Fighter opponent,Rules rules,Func<double> rng,EntryReason reason,Action<string> log,Fighter partner=null,bool immediate=false){
   if(fighter==null)return;
   StateWatch.Reset(fighter);
   // 등장한 T가 1T째(선발·죽어내밀기는 인터벌 등장이라 다음 T가 1T째)
   fighter.Conditions.FieldTurn=reason==EntryReason.Lead||reason==EntryReason.Replacement?0:1;
   // 효과 실행 범위 밖(죽어내밀기 화면)에서 나오면 다음 T 처음에 그 방식으로 처리한다(인터벌은 다음 T, 타이밍 규칙 5-1)
   // immediate: UI가 턴 밖(죽어내밀기 선택 직후)에서 효과 실행 범위를 열고 바로 처리할 때는 미루지 않는다
   if(!immediate&&!PotentialQueue.Active&&rules!=null&&reason==EntryReason.Replacement){fighter.Conditions.PendingEntry=reason;fighter.Conditions.EntryTriggered=false;return;}
   SpeciesValueModules.ApplyPassive(fighter, log);
   // 등장은 실제로 나왔을 때 한 번만(턴마다 선발 등장을 다시 처리하지 않도록 효과 실행 범위 안에서 표시)
   if(PotentialQueue.Active)fighter.Conditions.EntryTriggered=true;
   fighter.Conditions.PendingEntry=EntryReason.Lead;
   string method=TriggerModules.EntryMethodName(reason);
   TriggerModules.Raise(new TriggerFact{Kind="등장",Method=method,PartnerTypes=partner==null?new string[0]:(partner.Data.types??new string[0]).ToArray()},fighter,opponent,fighter.Data.name+" · "+Label+" ["+method+"]",log);
   if(!IsEntry(reason))return;
   PriorityEffectRuntime.ActivateEvent(fighter,null,Label,fighter.Data.name+" · "+Label+" ["+reason+"]",log);OptionalEffectRuntime.Activate(fighter,opponent,Label,rules,log);
   string label=MethodLabel(reason);if(label.Length>0)PriorityEffectRuntime.ActivateEvent(fighter,null,label,fighter.Data.name+" · "+label,log);if(reason==EntryReason.Lead||reason==EntryReason.Replacement)PriorityEffectRuntime.ActivateEvent(fighter,null,TriggerStructures.LeadOrReplacementEntryLabel,fighter.Data.name+" · "+TriggerStructures.LeadOrReplacementEntryLabel,log);
   RoleEntryRuntime.Enter(fighter,opponent,rules,rng,log);
  }
  public static void Test(){
   TriggerStructures.Test();
   foreach(EntryReason reason in Enum.GetValues(typeof(EntryReason))){int count=0;var pokemon=new Pokemon{name="검사",level=100,@base=new[]{100,100,100,100,100,100},iv=new int[6],types=new[]{"노말"},moves=new string[0],potentials=new[]{new Potential{name="등장",description="공격이 오른다.",triggers=new[]{"장소에 나와"}}}};Enter(new Fighter(pokemon),reason,x=>count++);if(count!=(reason==EntryReason.Forced?0:2))throw new Exception("Entry dispatch");}
   var methodPokemon=new Pokemon{name="선발",level=100,@base=new[]{100,100,100,100,100,100},iv=new int[6],types=new[]{"노말"},moves=new string[0],potentials=new[]{new Potential{name="선봉",description="공격이 오른다.",triggers=new[]{TriggerStructures.LeadEntryLabel}}}};var methodLog=new System.Collections.Generic.List<string>();Enter(new Fighter(methodPokemon),EntryReason.Lead,methodLog.Add);if(!methodLog.Any(x=>x.Contains("『선봉』")))throw new Exception("Entry method dispatch");
   DefeatTriggers.Test();
   RuntimeTriggers.Test();
   AnsweredTriggers.Test();

  }
 }

 public static class RoleEntryRuntime {
  static bool IsRole(Fighter fighter,string name){return fighter!=null&&fighter.Data!=null&&(fighter.Data.potentials??new Potential[0]).Any(p=>p!=null&&p.id=="역할"&&p.name==name&&TriggerStructures.Clauses(p).Any(x=>TriggerStructures.Normalize(x)==TriggerStructures.EntryLabel));}
  static void Raise(Fighter fighter,Fighter opponent,Rules rules,int stat,int amount,string role,List<string> log){if(stat<1||stat>5)return;if(Mechanics.ChangeStage(fighter,opponent,stat,amount,rules,role,log))log.Add("『"+role+"』: "+Engine.Names[stat]+" "+(amount>1?"크게 ":"")+"상승");}
  public static void Enter(Fighter fighter,Fighter opponent,Rules rules,Func<double> rng,Action<string> log){
   if(fighter==null||opponent==null||rules==null||log==null||fighter.HP<=0||fighter.Conditions.RoleEntryApplied)return;fighter.Conditions.RoleEntryApplied=true;var lines=new List<string>();
   // 탐사대원·야생 포켓몬의 「임의의 능력」은 역할 포텐셜 문장이 표준 경로(RunRank, 사용자 선택)로 처리한다
   if(IsRole(fighter,"야생의 두목"))foreach(int stat in Enumerable.Range(1,5))Raise(fighter,opponent,rules,stat,2,"야생의 두목",lines);
   foreach(string line in lines)log(line);
  }
 }

 public static class DefeatTriggers {
  public const string Id="opponent.defeated.by_move";
  public const string Label=TriggerStructures.DefeatLabel;
  public static void ByMove(Fighter attacker,Fighter target,Move move,int hpBefore,int damage,Action<string> log){
   if(hpBefore<=0||damage<=0||target.HP>0)return;
   TriggerModules.Raise(new TriggerFact{Kind="쓰러뜨림",Move=move,User=attacker,OtherTypes=(target.Data.types??new string[0]).ToArray(),OtherAin=TriggerModules.IsAin(target)},attacker,null,attacker.Data.name+"의 "+move.name+" 직접 대미지로 "+target.Data.name+"을 쓰러뜨렸다.",log);
   PriorityEffectRuntime.ActivateEvent(attacker,target,Label,attacker.Data.name+"의 "+move.name+" 직접 대미지로 "+target.Data.name+"을 쓰러뜨렸다.",log);
  }
  public static void Test(){
   var source=new Fighter(new Pokemon{name="사냥꾼",level=100,@base=new[]{100,100,100,100,100,100},iv=new int[6],types=new[]{"노말"},moves=new string[0],potentials=new[]{new Potential{name="승전",description="공격이 오른다.",triggers=new[]{"상대를 쓰러트렸을 때"}}}});var target=new Fighter(new Pokemon{name="표적",level=100,@base=new[]{100,100,100,100,100,100},iv=new int[6],types=new[]{"노말"},moves=new string[0]});target.HP=0;var move=new Move{name="몸통박치기"};int count=0;ByMove(source,target,move,1,1,x=>count++);if(count!=2)throw new Exception("Direct move defeat dispatch");count=0;ByMove(source,target,move,1,0,x=>count++);ByMove(source,target,move,0,1,x=>count++);if(count!=0)throw new Exception("Indirect defeat exclusion");
  }
 }

 public enum SwitchReason {Normal,Return,Pivot,Potential,Fainted,Forced}
 public static class TrainerTriggers {
  static IEnumerable<PotentialRecord> Unique(TrainerRecord trainer){return trainer==null?Enumerable.Empty<PotentialRecord>():(trainer.potentials??new List<PotentialRecord>()).Where(p=>p!=null&&p.enabled&&p.slot=="고유"&&p.name.Length>0);}
  // 트레이너 포텐셜의 「아군」은 장소에 있는 자기 포켓몬 = 포켓몬 포텐셜의 「자신」 관점(사용자 결정)
  public static string TrainerClause(string text){string t=PotentialParts.Clean(text??"").Trim();
   if(Regex.IsMatch(t,@"^(?:아군을\s*(?:필드|장소)에\s*내보냈을|아군이\s*(?:필드|장소)에\s*나(?:왔을|올))\s*때$"))return TriggerStructures.EntryLabel;
   var m=Regex.Match(t,@"^(?:자진의\s*포켓몬|아군)(?<p>의|이|가|을|를)\s*");if(!m.Success)return t;string p=m.Groups["p"].Value;return "자신"+(p=="의"?"의":p=="을"||p=="를"?"을":"이")+" "+t.Substring(m.Length);}
  public static IEnumerable<string> ClausesOf(PotentialRecord record){return (record==null?"":record.trigger??"").Replace("\r","").Split(new[]{'\n'},StringSplitOptions.RemoveEmptyEntries).SelectMany(x=>TriggerStructures.NormalizeLines(TrainerClause(x)).Replace("\r","").Split(new[]{'\n'},StringSplitOptions.RemoveEmptyEntries)).Where(x=>x.Length>0).Distinct();}
  public static IEnumerable<PotentialRecord> Matching(TrainerRecord trainer,string label){return Unique(trainer).Where(p=>ClausesOf(p).Contains(label));}
  public static void Fire(TrainerRecord trainer,string label,string context,Action<string> log,Fighter ally=null){
   if(trainer==null)return;
   if(PotentialQueue.Active){PotentialQueue.Raise(new PotentialEvent{Trainer=trainer,Owner=ally,Label=label,Message=context??"",Log=log,PendingText=(source,extra)=>"TRAINER 『"+source.Name+"』 "+label+" 트리거 충족 · "+context+" · 효과 판정: "+source.Effect});return;}
   foreach(var p in Matching(trainer,label)){log("TRAINER 『"+p.name+"』 "+label+" 트리거 충족 · "+context+" · 효과 판정: "+p.effect);foreach(string line in (p.effect??"").Replace("\r","").Split(new[]{'\n'},StringSplitOptions.RemoveEmptyEntries))ApplyAllyRise(line,ally,null,log);}
  }
  // 「자신/아군의 (능력)이 (크게) 오른다」. 맞지 않으면 null
  public static Match AllyRise(string line){var match=Regex.Match(PotentialParts.Clean(line??"").Trim().TrimEnd('.','．','。'),@"^(?:자신|아군)의\s*「?(?<stat>공격|방어|특공|특방|속도)」?(?:이|가|을|를)\s*(?<degree>매우 크게|크게)?\s*오른다$");return match.Success?match:null;}
  public static bool ApplyAllyRise(string line,Fighter ally,Fighter other,Action<string> log){var match=AllyRise(line);if(match==null||ally==null||ally.HP<=0)return false;int index=Array.IndexOf(Engine.Names,match.Groups["stat"].Value),amount=match.Groups["degree"].Value=="매우 크게"?3:match.Groups["degree"].Value=="크게"?2:1;if(index<=0)return false;int before=ally.Stages[index];ally.Stages[index]=Math.Min(6,ally.Stages[index]+amount);log(ally.Data.name+": "+Engine.Names[index]+" +"+amount+" (고유 포텐셜)");if(other!=null)AnsweredTriggers.StageChanged(ally,other,index,before,ally.Stages[index],log);return ally.Stages[index]!=before;}
  public static void BeforeMove(TrainerRecord trainer,Move move,Action<string> log,Fighter ally=null){Raise(trainer,new TriggerFact{Kind="기술 사용",Move=move,User=ally,Self=true},ally,move.name,log);}
  // 트레이너 포텐셜의 모듈 트리거: 장소의 자기 포켓몬 관점의 사건(자기 포켓몬이 주체면 Self)
  public static void Raise(TrainerRecord trainer,TriggerFact fact,Fighter ally,string context,Action<string> log){
   if(trainer==null||fact==null)return;
   var e=EventFor(trainer,fact,ally,context,log);if(e==null)return;
   if(PotentialQueue.Active){PotentialQueue.Raise(e);return;}
   foreach(var p in Unique(trainer).Where(p=>ClausesOf(p).Any(e.Matches)))log("TRAINER 『"+p.name+"』 트리거 충족 · "+context+" · 효과 판정 대기: "+p.effect);
  }
  public static PotentialEvent EventFor(TrainerRecord trainer,TriggerFact fact,Fighter ally,string context,Action<string> log){
   if(trainer==null||fact==null||log==null)return null;
   Func<string,bool> spec=clause=>TriggerModules.Matches(TriggerModules.Parse(TriggerStructures.Normalize(clause)),fact,ally);
   if(!Unique(trainer).Any(p=>ClausesOf(p).Any(spec)))return null;
   return new PotentialEvent{Trainer=trainer,Owner=ally,Spec=spec,Label=context??"",Move=fact.Move,Message=context??"",Log=log,PendingText=(source,extra)=>"TRAINER 『"+source.Name+"』 트리거 충족 · "+context+" · 효과 판정: "+source.Effect};
  }
  public static IEnumerable<string> Active(TrainerRecord trainer,bool here){var labels=new List<string>{TriggerStructures.AlwaysLabel,TriggerStructures.FieldLabel};if(here)labels.Add(TriggerStructures.HereLabel);return Unique(trainer).Where(p=>labels.Any(label=>ClausesOf(p).Contains(label))).Select(p=>"『"+p.name+"』");}
 }
}
