using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.IO;
using System.Web.Script.Serialization;

namespace AABattle {
 public class PotentialPart {
  public string id,key,text,activation,uses,exclusionReason;public bool protectedTemplate,excludedFromImplementation;
  public List<string> names=new List<string>(),sources=new List<string>(),raws=new List<string>();
  public string Display {get{var meta=new[]{protectedTemplate?"기본 보호":"",uses,activation}.Where(x=>!string.IsNullOrEmpty(x));string prefix=string.Join(" · ",meta);return (prefix.Length>0?"["+prefix+"] ":"")+text;}}
 }
 public class PotentialParts {
  public List<PotentialPart> triggers=new List<PotentialPart>(),effects=new List<PotentialPart>();
  public static string Clean(string text){var result=Regex.Replace((text??"").Normalize(NormalizationForm.FormKC).Replace('，',',').Replace('、',','),@"\s+"," ").Trim().TrimEnd('.',',','。').Trim();if(Regex.Matches(result,@"\[\d+\]").Count==1)result=Regex.Replace(result,@"^\[\d+\]\s*","");return result;}

  public static string Key(string text){return Regex.Replace(Clean(text),@"\s+","");}
  static void Add(Dictionary<string,PotentialPart> map,string kind,string text,PotentialRecord p,string source){
   text=kind=="T"?EntryTriggers.Normalize(text):EffectNormalizer.Normalize(text);if(text.Length==0)return;
   string mode=kind=="T"?Clean(p.activation):"",uses=kind=="T"?Clean(p.uses):"";
   string key=Key(text)+"\u001f"+Key(mode)+"\u001f"+Key(uses);PotentialPart part;
   if(!map.TryGetValue(key,out part)){string id;using(var sha=SHA256.Create())id=BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(kind+key))).Replace("-","").Substring(0,16).ToLowerInvariant();part=new PotentialPart{id=kind.ToLowerInvariant()+"-"+id,key=key,text=text,activation=mode,uses=uses};map.Add(key,part);}
   if(!part.names.Contains(p.name))part.names.Add(p.name);if(!part.sources.Contains(source))part.sources.Add(source);if(!part.raws.Contains(p.raw))part.raws.Add(p.raw);
  }
  static IEnumerable<string> Lines(string value){return (value??"").Replace("\r","").Split('\n').Select(x=>Regex.Replace(x.Trim(),@"^\[[0-9]+\]\s*","")).Where(x=>x.Length>0);}
  public static PotentialParts Build(IEnumerable<DexPotential> entries){
   var triggers=new Dictionary<string,PotentialPart>();var effects=new Dictionary<string,PotentialPart>();
   foreach(var item in entries){var p=item.record;string extraTrigger,cleanEffect;PotentialLibrary.Split(p.effect,out extraTrigger,out cleanEffect);
    foreach(var text in Lines(p.trigger).Concat(Lines(extraTrigger)))Add(triggers,"T",text,p,item.source);
    foreach(var text in Lines(cleanEffect))Add(effects,"E",text,p,item.source);
   }
   var result=new PotentialParts{triggers=triggers.Values.OrderBy(x=>x.text).ToList(),effects=effects.Values.OrderBy(x=>x.text).ToList()};var protectedKeys=new HashSet<string>(ProtectedEffectTexts().Select(Key));foreach(var effect in result.effects){effect.protectedTemplate=protectedKeys.Contains(Key(effect.text));if(!effect.protectedTemplate)effect.excludedFromImplementation=TryExclusionReason(effect.text,out effect.exclusionReason);}return result;
  }
  public static bool TryExclusionReason(string text,out string reason){
   string value=Clean(text);reason="";
   if(Regex.IsMatch(value,@"^[\(（].*[\)）]$")){reason="독립 주석";return true;}
   if(value=="PT포텐셜"){reason="분류 표기";return true;}
   if(Regex.IsMatch(value,@"(대회.*참가할 수 없다|대회.*밖에 참가할 수 없다|PT.*(참가|엔트리|소속).*수 (있|없)다|PT.*(참가|엔트리|소속).*수가 없다|함께 엔트리할 수 없다|배틀에 엔트리할 수 있다|트레이너.*PT.*(참가|소속)|트레이너에게 소속될 수 없다|필요통솔을)")||Regex.IsMatch(value,@"^이 (PT는|포텐셜은 PT의|퍼텐셜은 PT의).*(사용|발동)")){reason="편성·공유 규칙";return true;}
   if(Regex.IsMatch(value,@"(『유대』를 얻을 수 없다|경험치를 얻|획득상금)")){reason="배틀 외 획득 규칙";return true;}
   return false;
  }
  static IEnumerable<string> ProtectedEffectTexts(){
   var options=EditorTemplate.RolePotentials.Concat(EditorTemplate.CommonPotentials).Concat(EditorTemplate.InitiativePotentials).Concat(EditorTemplate.CounterTypeNames().SelectMany(type=>new[]{"회피","내성","격"}.SelectMany(slot=>EditorTemplate.CounterOptions(slot,type))));
   foreach(var option in options){string trigger,effect;PotentialLibrary.Split(option.effect,out trigger,out effect);foreach(string line in Lines(effect))yield return EffectNormalizer.Normalize(line);if(option.adjunctEffect.Length>0){PotentialLibrary.Split(option.adjunctEffect,out trigger,out effect);foreach(string line in Lines(effect))yield return EffectNormalizer.Normalize(line);}}
   foreach(var record in EditorTemplate.Orders(false).Concat(EditorTemplate.Orders(true)).Concat(EditorTemplate.ExtendedOrders(false)).Concat(EditorTemplate.ExtendedOrders(true))){string trigger,effect;PotentialLibrary.Split(record.effect,out trigger,out effect);foreach(string line in Lines(effect))yield return EffectNormalizer.Normalize(line);}
  }
  public static IEnumerable<DexPotential> StandardEntries(){
   var orders=EditorTemplate.Orders(false).Concat(EditorTemplate.Orders(true)).Concat(EditorTemplate.ExtendedOrders(false)).Concat(EditorTemplate.ExtendedOrders(true)).GroupBy(x=>x.name).Select(x=>new DexPotential{record=x.First(),source="사용자 제공 양식 / 지령",urls=new string[0]});
   var conditions=EditorTemplate.PotentialTriggerCatalog.Select(x=>new DexPotential{record=new PotentialRecord{trigger=x,name="공통 조건",raw=x},source="기본 트리거 목록",urls=new string[0]});
   return PotentialLibrary.Load().Concat(orders).Concat(conditions);
  }
  public static void Export(string path){var data=Build(StandardEntries());File.WriteAllText(path,new JavaScriptSerializer{MaxJsonLength=30000000}.Serialize(data),new UTF8Encoding(false));}
  public static void Test(){
   var data=Build(new[]{
    new DexPotential{record=new PotentialRecord{name="A",trigger="필드에 나왔을 때,",effect="위력을 강화(１.５배)한다.",uses="1/시"},source="A"},
    new DexPotential{record=new PotentialRecord{name="B",trigger="[1] 필드에 나왔을때",effect="위력을  강화(1.5배)한다",uses="1/시"},source="B"},
    new DexPotential{record=new PotentialRecord{name="C",trigger="필드에 나왔을 때",effect="위력을 강화(2배)한다",uses="2/시"},source="C"}});
   if(data.triggers.Count!=2||data.effects.Count!=2||!data.effects.Any(x=>x.sources.Count==2))throw new Exception("Parts deduplication/provenance failed");
   if(Key("저확률로 회피") == Key("중확률로 회피"))throw new Exception("Probability collapsed");
   var all=Build(StandardEntries());if(all.triggers.Count<100||all.effects.Count<100||all.triggers.Select(x=>x.key).Distinct().Count()!=all.triggers.Count||all.effects.Select(x=>x.key).Distinct().Count()!=all.effects.Count)throw new Exception("Part catalog uniqueness failed");if(all.effects.Count(x=>x.protectedTemplate)<20||!all.effects.Any(x=>x.protectedTemplate&&x.text.Contains("전능력치"))||all.effects.Any(x=>x.protectedTemplate&&x.excludedFromImplementation))throw new Exception("Template effect protection failed");if(all.effects.Count(x=>x.excludedFromImplementation)!=53)throw new Exception("Unexpected implementation exclusion count");
   string reason;if(!TryExclusionReason("(※1T이 아니라, 1번의 공격에 대해 반응)",out reason)||reason!="독립 주석"||!TryExclusionReason("PT에 참가할 수 없다",out reason)||reason!="편성·공유 규칙"||TryExclusionReason("PT 전원이 기술 「파도타기」를 내보낼 수 있다",out reason))throw new Exception("Implementation exclusion classification failed");
  }
 }
}
