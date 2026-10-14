using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace AABattle {
 public sealed class BatonSnapshot {
  public bool Ranks, States;
  public int[] Stages;
  public int Accuracy, Evasion, Critical;
  public ConditionState Conditions;
  public List<string> FirstMoves = new List<string>();
  public Potential IncomingRankSource;
 }
 // 인계 허용 목록. 상태이상·특성·소지품·타입·종족치·배율은 넘기지 않는다.
 public static class BatonModules {
  public const string BilateralRanks = "자신이 나가거나 들어오는 임의교대의 「능력변화」를 인계할 수 있다";
  public static readonly string[] StateFields = {
   "Heated", "Chilled", "Charged", "Bomb", "BombSetTurn", "Intercept",
   "Confusion", "FocusEnergy", "Substitute", "Seed", "Curse", "CurseSource", "Ingrain", "AquaRing",
   "MagnetRise", "Telekinesis", "MindReaderSide", "MindReaderSetTurn", "Embargo",
   "HealBlock", "DeathCount", "Rage", "Flinch", "Bound", "BindDivisor"
  };
  static readonly FieldInfo[] fields = StateFields.Select(n=>typeof(ConditionState).GetField(n)).ToArray();
  static readonly Regex Transfer = new Regex(@"^(?<s>자신|상대)의 (?<n>(?:「능력변화」|「상태변화」)+)를 인계(?<opt>한다|할 수 있다)$");
  static readonly Regex FirstMove = new Regex(@"^교대해 나온 아군이 최초로 내보내는 (?<c>(?:「[^」]+」)+) 기술의 위력을 강화\((?<r>[0-9.]+)배\)한다$");
  sealed class Intent {
   public bool Ranks, States, Bomb;
   public readonly List<string> FirstMoves = new List<string>();
  }
  [ThreadStatic] static EffectScope scope;
  [ThreadStatic] static Dictionary<Fighter,Intent> pending;
  static Dictionary<Fighter,Intent> Pending {
   get {
    if(pending==null || scope!=EffectScope.Current) { scope=EffectScope.Current; pending=new Dictionary<Fighter,Intent>(); }
    return pending;
   }
  }
  static string Clean(string s) { return Regex.Replace(PotentialParts.Clean(s??""),@"\s+"," ").Trim().TrimEnd('.','。','．'); }
  public static string Normalize(string text) {
   string s=Clean(text);
   if(s==BilateralRanks)return s+".";
   if(s=="능력변화를 인계한다" || s=="능력변화를 인계해서 교대한다") return "자신의 「능력변화」를 인계한다.";
   if(s=="능력변화를 인계할 수 있다") return "자신의 「능력변화」를 인계할 수 있다.";
   var m=Transfer.Match(s);
   if(m.Success)return s+".";
   return FirstMove.IsMatch(s)?s+".":null;
  }
  public static string Classify(string text) {
   string s=Clean(text);
   return s==BilateralRanks?"양방향 임의교대 인계":Transfer.IsMatch(s)?"교대 인계":FirstMove.IsMatch(s)?"교대 아군 첫 기술 강화":"";
  }
  static bool Queue(EffectCall call) {
   string s=Clean(call.Line); var m=Transfer.Match(s);
   Fighter target=m.Success && m.Groups["s"].Value=="상대"?call.Opponent:call.Owner;
   if(target==null || target.HP<=0 || !target.OnField)return false;
   Intent intent;
   if(!Pending.TryGetValue(target,out intent))Pending[target]=intent=new Intent();
   if(m.Success) {
    intent.Ranks|=m.Groups["n"].Value.Contains("능력변화");
    intent.States|=m.Groups["n"].Value.Contains("상태변화");
    intent.Bomb|=target.Conditions.Bomb;
   } else intent.FirstMoves.Add(s);
   call.Say(target.Data.name+": 인계 준비 ("+call.Source.Label+")");
   return true;
  }
  public static void Register() {
   PotentialExecutors.Register("교대 인계",c=>Classify(c.Line).Length>0 && Clean(c.Line)!=BilateralRanks,c=>Queue(c)?EffectOutcome.Success:EffectOutcome.Failed);
  }
  // 들어오는 특성은 상대의 무효화를 반영한 복사본으로 판정한다.
  static Potential AcceptRanks(Fighter owner, Fighter other, Rules rules, bool entering) {
   if(owner==null || owner.HP<=0 || rules==null || rules.OptionalEffectPrompt==null)return null;
   var probe=owner;
   if(entering) {
    probe=owner.Copy();
    probe.OnField=true;
    probe.AbilitySuppressed=AbilityPhrases.SuppressesAbilities(other,probe)
     && !AbilityPhrases.SuppressesAbilities(probe,other,true) && !AbilityChangeModules.CannotSuppress(probe,other);
    AbilityModules.Sync(probe);
   }
   foreach(var potential in (probe.Data.potentials??new Potential[0]).Where(p=>p!=null)) {
    if(!PotentialModifiers.Sentences(probe,other,rules,"인계",new[]{potential},true).Any(s=>Clean(s)==BilateralRanks))continue;
    if(rules.OptionalEffectPrompt(owner,other,potential))return potential;
   }
   return null;
  }
  static void Activated(Potential source, Fighter owner, List<string> log) {
   if(source==null)return;
   if(log!=null)log.Add((source.id==AbilityModules.Slot?"〔"+source.name+"〕":"『"+source.name+"』")+": 능력변화 인계 선택 발동");
   if(source.id==AbilityModules.Slot)
    AnsweredTriggers.AbilityActivated(owner,AbilityRuntime.Name(owner),log==null?(Action<string>)null:log.Add);
  }
  // 퇴장 사건을 처리한 뒤, LeaveField 직전에 값을 잡는다.
  public static BatonSnapshot Capture(Fighter outgoing, Fighter other, Rules rules, SwitchReason reason, Fighter incoming=null, List<string> log=null) {
   if(outgoing==null || outgoing.HP<=0 || reason==SwitchReason.Fainted)return null;
   Intent intent;
   if(!Pending.TryGetValue(outgoing,out intent))intent=new Intent();
   bool voluntary=reason==SwitchReason.Normal || reason==SwitchReason.Return || reason==SwitchReason.Pivot || reason==SwitchReason.Potential;
   Potential enteringSource=null;
   if(voluntary && !intent.Ranks) {
    var leavingSource=AcceptRanks(outgoing,other,rules,false);
    if(leavingSource!=null)Activated(leavingSource,outgoing,log);
    else enteringSource=AcceptRanks(incoming,other,rules,true);
    intent.Ranks=leavingSource!=null || enteringSource!=null;
   }
   foreach(string line in PotentialModifiers.Sentences(outgoing,other,rules,"인계")) {
    var m=Transfer.Match(Clean(line));
    if(m.Success && m.Groups["s"].Value=="자신" && m.Groups["opt"].Value=="한다") {
     intent.Ranks|=m.Groups["n"].Value.Contains("능력변화");
     intent.States|=m.Groups["n"].Value.Contains("상태변화");
    }
   }
   Pending.Remove(outgoing);
   if(!intent.Ranks && !intent.States && intent.FirstMoves.Count==0)return null;
   var saved=outgoing.Conditions.Copy();
   // 배턴 기술 자체의 행동 후 폭탄 해제 전에 기록한 값도 보존한다.
   if(intent.States && intent.Bomb)saved.Bomb=true;
   return new BatonSnapshot { Ranks=intent.Ranks,States=intent.States,Stages=(int[])outgoing.Stages.Clone(),
    Accuracy=outgoing.AccuracyStage,Evasion=outgoing.EvasionStage,Critical=outgoing.CriticalStage,
    Conditions=saved,FirstMoves=new List<string>(intent.FirstMoves),IncomingRankSource=enteringSource };
  }
  public static void Apply(BatonSnapshot saved,Fighter incoming,List<string> log) {
   if(saved==null || incoming==null)return;
   if(saved.Ranks) {
    incoming.Stages=(int[])saved.Stages.Clone();
    incoming.AccuracyStage=saved.Accuracy; incoming.EvasionStage=saved.Evasion; incoming.CriticalStage=saved.Critical;
   }
   if(saved.States)foreach(var field in fields)field.SetValue(incoming.Conditions,field.GetValue(saved.Conditions));
   foreach(string line in saved.FirstMoves) {
    var m=FirstMove.Match(line);
    string power="자신의 "+m.Groups["c"].Value+" 기술의 위력을 강화("+m.Groups["r"].Value+"배)한다.";
    incoming.Conditions.Modifiers.Add(new ActiveModifier {Key="baton:first:"+power,Text=power,Source="교대 첫 기술",Turns=-1});
   }
   ItemRuntime.Sync(incoming);
   if(log!=null)log.Add(incoming.Data.name+": 교대 인계");
   // 등장 측의 발동 사건은 대기 복사본이 아니라 실제 장소의 포켓몬에게 낸다.
   Activated(saved.IncomingRankSource,incoming,log);
  }
  public static void AfterMove(Fighter owner,Move move,List<string> log) {
   if(owner==null || move==null)return;
   var used=(owner.Conditions.Modifiers??new List<ActiveModifier>()).Where(x=>x.Key.StartsWith("baton:first:"))
    .Where(x=>{ScalarEffect scalar;return EffectModules.TryScalar(x.Text,out scalar)&&scalar.Condition.Matches(move,owner,1);}).ToArray();
   foreach(var item in used)owner.Conditions.Modifiers.Remove(item);
   if(used.Length>0 && log!=null)log.Add(owner.Data.name+": 교대 첫 기술 강화가 소모됐다");
  }
 }
}
