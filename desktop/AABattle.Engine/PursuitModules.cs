using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace AABattle {
 public static class PursuitModules {
  public const string Immune="자신은 「추격」 효과를 받지 않는다";
  public const string Followup="「추격」 효과로 가장 먼저 같은 기술에 의한 추가공격을 한다";
  static readonly Regex Grant=new Regex(@"^자신의 (?<c>(?:「[^」]+」)*)\s*기술에 「추격」\s*효과를 부여한다$");
  static string Clean(string s) { return Regex.Replace(PotentialParts.Clean(s??""),@"\s+"," ").Trim().TrimEnd('.','。','．'); }
  public static string Normalize(string text) {
   string s=Clean(text);
   if(Regex.IsMatch(s,@"^(?:자신은 |상대로부터의 |상대의 (?:기술의 )?)?「?추격」?\s*효과를 받지 않는다$"))return Immune+".";
   var m=Grant.Match(s);
   if(m.Success) {MoveCondition c;return MoveCondition.TryParse(m.Groups["c"].Value,out c)?"자신의 "+(c.Empty?"":c.Text+" ")+"기술에 「추격」 효과를 부여한다.":null;}
   if(s==Followup)return s+".";
   return null;
  }
  public static string Classify(string text) {
   string s=Clean(text);
   if(s==Immune)return "추격 효과 면역";
   if(s==Followup)return "추격 추가공격";
   return Grant.IsMatch(s)?"추격 효과 부여":"";
  }
  public static bool IsPursuit(Move move) {
   return move!=null && ((move.tags??new string[0]).Contains("추격") || Regex.IsMatch(Clean(move.effect),@"「추격」\s*기술"));
  }
  public static bool ImmuneTo(Fighter target,Fighter attacker,Rules rules) {
   return target!=null && PotentialModifiers.Sentences(target,attacker,rules,"추격",null,true).Any(s=>Clean(s)==Immune);
  }
  public static Move Judge(Fighter owner,Fighter other,Move move,Rules rules) {
   if(move==null || IsPursuit(move))return move;
   foreach(string line in PotentialModifiers.Sentences(owner,other,rules,"추격")) {
    var m=Grant.Match(Clean(line));MoveCondition condition;
    if(!m.Success || !MoveCondition.TryParse(m.Groups["c"].Value,out condition) || !condition.Matches(move,owner,1))continue;
    var copy=move.Copy();copy.tags=(copy.tags??new string[0]).Concat(new[]{"추격"}).Distinct().ToArray();return copy;
   }
   return move;
  }
  public static bool GrantsMove(string line,Fighter owner,Move move) {
   var m=Grant.Match(Clean(line));MoveCondition condition;
   return m.Success && MoveCondition.TryParse(m.Groups["c"].Value,out condition) && condition.Matches(move,owner,1);
  }
  public static void Register() {
   PotentialExecutors.Register("추격 효과 부여",c=>Grant.IsMatch(Clean(c.Line)),c=>{
    if(c.Owner==null || c.Owner.HP<=0)return EffectOutcome.Failed;
    PotentialModifiers.Add(c.Owner,new ActiveModifier {Key=c.Source.Key,Text=Clean(c.Line)+".",Source=c.Source.Label,Turns=-1});
    return EffectOutcome.Success;
   });
   PotentialExecutors.Register("추격 추가공격",c=>Clean(c.Line)==Followup,c=>{
    if(ImmuneTo(c.Opponent,c.Owner,c.Rules)) {c.Say(c.Opponent.Data.name+": 추격 추가공격을 받지 않는다");return EffectOutcome.Failed;}
    var active=EffectScope.Current;
    if(active==null || c.Owner==null || c.Owner.HP<=0)return EffectOutcome.Failed;
    active.ExtraActions.Add(new ExtraActionGrant {Owner=c.Owner,Side=Array.IndexOf(active.Fighters,c.Owner),First=true,SameMove=true,Source=c.Source.Label});
    return EffectOutcome.Success;
   });
  }
  public static bool HandlesMoveSentence(string text) {
   return Regex.IsMatch(Clean(text),@"^상대가 「임의교대」 도중일 때,\s*위력이 강화\(2배\)된다$");
  }
 }
}
