using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Globalization;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace AABattle {
 // 내장 포텐셜 연구 자료 읽기와 트리거/효과 분리
 public static class PotentialLibrary {
  static List<DexPotential> cache;
  static bool SplitClause(string value,out string condition,out string remainder){
   condition=remainder="";value=value.Trim();
   if(Regex.IsMatch(value,@"^[0-9０-９]+\s*T\s*동안")||Regex.IsMatch(value,@"^T\s*종료\s*시\s*까지"))return false;
   var enteredTurn=Regex.Match(value,@"^(.+?교대해\s*필드에\s*나온)\s*T\s*종료\s*시\s*까지\s+(.+)$");
   if(enteredTurn.Success){condition=enteredTurn.Groups[1].Value.Trim()+" 때";remainder="T 종료시까지 "+enteredTurn.Groups[2].Value.Trim();return true;}
   var enteredSecond=Regex.Match(value,@"^(.+?교대해\s*필드에\s*나와\s*[0-9０-９]+T째)\s*[,，、]\s*(.+)$");
   if(enteredSecond.Success){condition=enteredSecond.Groups[1].Value.Trim();remainder=enteredSecond.Groups[2].Value.Trim();return true;}
   var switchedTurn=Regex.Match(value,@"^(.+?교대로?\s*(?:필드에\s*)?나온\s*T)\s*[,，、]\s*(.+)$");
   if(switchedTurn.Success){condition=switchedTurn.Groups[1].Value.Trim();remainder=switchedTurn.Groups[2].Value.Trim();return true;}
   var ending=@"(?:때(?:에)?도|때|일때|경우|한해서|라면|있으면|없으면|(?:으)?면(?:\([^)]*(?:으)?면\))?|나오면|있는\s*동안|있는\s*한|참가\s*시에?|발동\s*시에?|사용\s*시에?|발생\s*시에?|개시\s*시에?|종료\s*시에?|교대\s*시에?|명중\s*시에?|피격\s*시에?)";
   var match=Regex.Match(value,@"^(.+?"+ending+@")\s*(?:[,，、.]\s*|\s+)(.+)$");
   if(!match.Success)return false;condition=match.Groups[1].Value.Trim();remainder=match.Groups[2].Value.Trim();return remainder.Length>0;
  }
  static bool ConditionOnly(string value){return Regex.IsMatch(value.Trim().TrimEnd('.','。'),@"(?:때(?:에)?도|때|일때|경우|한해서|라면|있으면|없으면|(?:으)?면(?:\([^)]*(?:으)?면\))?|나오면|있는\s*동안|있는\s*한|참가\s*시에?|발동\s*시에?|사용\s*시에?|발생\s*시에?|개시\s*시에?|종료\s*시에?|교대\s*시에?|명중\s*시에?|피격\s*시에?)$");}
  // Extract chained conditions while preserving duration phrases such as 1T 동안 and T종료시까지.
  public static void Split(string body,out string trigger,out string effect) {
   UseConditions use;body=UseConditionModules.Prepare(body,out use);
   var rules=RuleText.Split(body);trigger=string.Join("\r\n",rules.triggers);effect=string.Join("\r\n",rules.effects);
   if(trigger.Length==0&&use.OnHit)trigger=UseConditionModules.OnHitTrigger;
  }
  public static List<DexPotential> Load(){
   if(cache!=null)return cache;
   var catalog=new JavaScriptSerializer{MaxJsonLength=20000000}.Deserialize<ResearchCatalog>(Resource.Text("potentials.json"));
   cache=new List<DexPotential>();
   foreach(var x in catalog.items){
    var parsed=EditorTemplate.Parse("『"+x.name+"』… "+x.raw,x.category??"미분류");
    // Derive parts from preserved raw text, not legacy metadata that matched 임의 inside 임의교대.
    parsed.raw=x.raw;
    cache.Add(new DexPotential{record=parsed,source=string.Join(" / ",(x.sources??new ResearchSource[0]).Select(s=>s.source_page+" : "+s.line).Distinct()),urls=(x.sources??new ResearchSource[0]).Select(s=>s.source_url).Distinct().ToArray(),review=(x.needs_review!=null&&x.needs_review.Length>0)||(x.activation!=null&&x.activation.confidence=="review")?"검토 필요 · 자동 분리 초안":"자동 분리 · 원문 대조 가능"});
   }
   return cache;
  }
 }
}
