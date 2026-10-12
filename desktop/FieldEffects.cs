using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Web.Script.Serialization;

namespace AABattle {
 public class FieldEffectRecord {
  public bool enabled=true;
  public string name="",trigger="",effect="",raw="";
  public int depth;
  public FieldEffectRecord Copy(){return new FieldEffectRecord{enabled=enabled,name=name??"",trigger=trigger??"",effect=effect??"",raw=raw??"",depth=Math.Max(0,Math.Min(5,depth))};}
 }

 public static class FieldEffectFile {
  public static FieldEffectRecord Parse(string json){
   var serializer=new JavaScriptSerializer{MaxJsonLength=50000000};object root=serializer.DeserializeObject(json??"");var map=root as Dictionary<string,object>;FieldEffectRecord value=null;
   if(map!=null&&map.ContainsKey("fieldEffect"))value=serializer.Deserialize<FieldEffectRecord>(serializer.Serialize(map["fieldEffect"]));
   else if(map!=null&&map.ContainsKey("teams")){var project=serializer.Deserialize<EditorProject>(serializer.Serialize(root));value=(project.teams??new List<TeamRecord>()).Where(x=>x!=null&&x.fieldEffect!=null&&!string.IsNullOrWhiteSpace(x.fieldEffect.name)).Select(x=>x.fieldEffect).FirstOrDefault();}
   else value=serializer.Deserialize<FieldEffectRecord>(serializer.Serialize(root));
   if(value==null||string.IsNullOrWhiteSpace(value.name))throw new Exception("필드효과 이름이 있는 데이터를 찾지 못했습니다.");return value.Copy();
  }
 }

 public static class FieldEffectRuntime {
  static readonly Regex Rank=new Regex(@"(?<stat>전능력치|공격|방어|특공|특방|속도|명중|회피|C)[^\r\n]*(?<degree>매우 크게|크게)?\s*(?<direction>오른다|올린다|내린다)",RegexOptions.Compiled);
  public static FieldEffectRecord[] FromTeams(IEnumerable<TeamRecord> teams){
   var result=new List<FieldEffectRecord>();var keys=new HashSet<string>();
   foreach(var team in teams??Enumerable.Empty<TeamRecord>()){
    var field=team==null?null:team.fieldEffect;if(field==null||!field.enabled||string.IsNullOrWhiteSpace(field.name))continue;
    var copy=field.Copy();copy.trigger=TriggerStructures.NormalizeLines(copy.trigger);copy.effect=EffectNormalizer.NormalizeLines(copy.effect);
    string key=copy.name+"\n"+copy.depth+"\n"+copy.trigger+"\n"+copy.effect;if(keys.Add(key))result.Add(copy);
   }
   return result.ToArray();
  }
  public static string Depth(FieldEffectRecord value){return value==null||value.depth<=0?"없음":value.depth.ToString();}
  public static string Summary(IEnumerable<FieldEffectRecord> values){var active=Active(values).ToArray();return active.Length==0?"없음":string.Join(" / ",active.Select(x=>x.name+" · 심도 "+Depth(x)));}
  public static string Detail(IEnumerable<FieldEffectRecord> values){var active=Active(values).ToArray();if(active.Length==0)return "《 필드효과 》 없음";return string.Join("\r\n\r\n",active.Select(x=>"《 "+x.name+"  ·  심도 "+Depth(x)+" 》\r\n트리거: "+(x.trigger.Length>0?x.trigger:"X")+"\r\n효과: "+(x.effect.Length>0?x.effect:x.raw)));
  }
  public static IEnumerable<FieldEffectRecord> Active(IEnumerable<FieldEffectRecord> values){return (values??Enumerable.Empty<FieldEffectRecord>()).Where(x=>x!=null&&x.enabled&&!string.IsNullOrWhiteSpace(x.name));}
  public static bool Matches(FieldEffectRecord value,string label){if(value==null)return false;var clauses=(value.trigger??"").Replace("\r","").Split(new[]{'\n'},StringSplitOptions.RemoveEmptyEntries);if(clauses.Length==0)clauses=new[]{TriggerStructures.AlwaysLabel};return clauses.Any(x=>TriggerStructures.Normalize(x)==label);}
  public static void Fire(Rules rules,string label,Fighter[] fighters,string context,Func<double> rng,Action<string> log){
   if(rules==null||fighters==null)return;
   foreach(var field in Active(rules.FieldEffects).Where(x=>Matches(x,label))){
    double? rate=PotentialProbability.Rate(field.effect);if(rate.HasValue&&(rng??(()=>.5))()>=rate.Value){if(log!=null)log("《"+field.name+"》 "+label+" · 확률 판정 실패");continue;}
    if(log!=null)log("《"+field.name+"》 "+label+" 트리거 충족 · "+context+" · 장소의 모든 포켓몬에게 발동");
    bool handled=false;foreach(string line in (field.effect??"").Replace("\r","").Split(new[]{'\n'},StringSplitOptions.RemoveEmptyEntries))handled|=ApplyLine(field,line,fighters,rules,log);
    if(!handled&&log!=null)log("《"+field.name+"》 효과 판정 대기: "+(field.effect.Length>0?field.effect:field.raw));
   }
  }
  public static void BeforeMove(Rules rules,Fighter[] fighters,Move move,string context,Func<double> rng,Action<string> log){
   if(move==null)return;Fire(rules,TriggerStructures.MoveUseLabel,fighters,context,rng,log);Fire(rules,move.category=="변화"?TriggerStructures.StatusMoveUseLabel:TriggerStructures.AttackMoveUseLabel,fighters,context,rng,log);Fire(rules,TriggerStructures.MoveName(move.name),fighters,context,rng,log);foreach(string type in move.types??new string[0])Fire(rules,TriggerStructures.MoveType(type),fighters,context,rng,log);
  }
  static bool ApplyLine(FieldEffectRecord field,string source,Fighter[] fighters,Rules rules,Action<string> log){
   string probability,body;if(EffectNormalizer.TryProbabilityPrefix(source,out probability,out body))source=body;string clean=PotentialParts.Clean(source);var rank=Rank.Match(clean);
   if(rank.Success){int amount=rank.Groups["degree"].Value=="매우 크게"?3:rank.Groups["degree"].Value=="크게"?2:1;if(rank.Groups["direction"].Value=="내린다")amount=-amount;string stat=rank.Groups["stat"].Value;foreach(var fighter in OnField(fighters)){if(stat=="전능력치"){for(int i=1;i<=5;i++)fighter.Stages[i]=Clamp(fighter.Stages[i]+amount);}else if(stat=="명중")fighter.AccuracyStage=Clamp(fighter.AccuracyStage+amount);else if(stat=="회피")fighter.EvasionStage=Clamp(fighter.EvasionStage+amount);else if(stat=="C")fighter.CriticalStage=Clamp(fighter.CriticalStage+amount);else{int index=Array.IndexOf(Engine.Names,stat);if(index>0)fighter.Stages[index]=Clamp(fighter.Stages[index]+amount);}if(log!=null)log("《"+field.name+"》 "+fighter.Data.name+" · "+stat+" "+(amount>0?"+":"")+amount);}return true;}
   string target,action;int denominator;if(EffectNormalizer.TryFraction(clean,out target,out action,out denominator)){foreach(var fighter in OnField(fighters)){var opponent=OnField(fighters).FirstOrDefault(x=>x!=fighter)??fighter;int maximum=Engine.Stats(fighter,opponent.Data.level,rules)[0],amount=Math.Max(1,maximum/denominator);if(action=="회복"){var healLog=new List<string>();amount=Mechanics.Heal(fighter,opponent,rules,amount,"《"+field.name+"》",healLog);if(log!=null)foreach(string line in healLog)log(line);}else{bool canFaint=!clean.Contains("빈사로 할 수 없다");int cap=canFaint?fighter.HP:Math.Max(0,fighter.HP-1);amount=Math.Min(cap,amount);fighter.HP-=amount;}if(log!=null)log("《"+field.name+"》 "+fighter.Data.name+" · HP "+(action=="회복"?"+":"-")+amount);}return true;}
   return false;
  }
  static IEnumerable<Fighter> OnField(IEnumerable<Fighter> fighters){return (fighters??Enumerable.Empty<Fighter>()).Where(x=>x!=null&&x.OnField&&x.HP>0);}
  static int Clamp(int value){return Math.Max(-6,Math.Min(6,value));}
  public static void Test(Database db){
   var teams=new[]{new TeamRecord{name="A",fieldEffect=new FieldEffectRecord{name="심층",depth=3,trigger=TriggerStructures.TurnEndLabel,effect="자신의 「공격」이 오른다."}},new TeamRecord{name="B",fieldEffect=new FieldEffectRecord{name="심층",depth=3,trigger=TriggerStructures.TurnEndLabel,effect="자신의 「공격」이 오른다."}}};var fields=FromTeams(teams);if(fields.Length!=1||Summary(fields)!="심층 · 심도 3")throw new Exception("Field effect normalization failed");var serializer=new JavaScriptSerializer();var parsed=FieldEffectFile.Parse(serializer.Serialize(fields[0]));var wrapped=FieldEffectFile.Parse(serializer.Serialize(new Dictionary<string,object>{{"fieldEffect",fields[0]}}));var project=FieldEffectFile.Parse(serializer.Serialize(new EditorProject{teams=new List<TeamRecord>{teams[0]}}));if(parsed.name!="심층"||wrapped.depth!=3||project.trigger!=TriggerStructures.TurnEndLabel)throw new Exception("Field effect file import failed");var fighters=new[]{new Fighter(db.pokemon.First()),new Fighter(db.pokemon.Skip(1).First())};var rules=new Rules{FieldEffects=fields};var logs=new List<string>();Fire(rules,TriggerStructures.TurnEndLabel,fighters,"검사",()=>0,logs.Add);if(fighters.Any(x=>x.Stages[1]!=1)||!logs.Any(x=>x.Contains("모든 포켓몬")))throw new Exception("Field effect must activate for every on-field Pokemon");fighters[1].OnField=false;Fire(rules,TriggerStructures.TurnEndLabel,fighters,"검사",()=>0,logs.Add);if(fighters[0].Stages[1]!=2||fighters[1].Stages[1]!=1)throw new Exception("Field effect must exclude benched Pokemon");
  }
 }
}
