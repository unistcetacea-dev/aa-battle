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
  public string id,key,text,activation,uses;
  public List<string> names=new List<string>(),sources=new List<string>(),raws=new List<string>();
  public string Display {get{var meta=new[]{uses,activation}.Where(x=>!string.IsNullOrEmpty(x));string prefix=string.Join(" · ",meta);return (prefix.Length>0?"["+prefix+"] ":"")+text;}}
 }
 public class PotentialParts {
  public List<PotentialPart> triggers=new List<PotentialPart>(),effects=new List<PotentialPart>();
  public static string Clean(string text){var result=Regex.Replace((text??"").Normalize(NormalizationForm.FormKC).Replace('，',',').Replace('、',','),@"\s+"," ").Trim().TrimEnd('.',',','。').Trim();if(Regex.Matches(result,@"\[\d+\]").Count==1)result=Regex.Replace(result,@"^\[\d+\]\s*","");return result;}

  public static string Key(string text){return Regex.Replace(Clean(text),@"\s+","");}
  static void Add(Dictionary<string,PotentialPart> map,string kind,string text,PotentialRecord p,string source){
   text=Clean(text);if(text.Length==0)return;
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
   return new PotentialParts{triggers=triggers.Values.OrderBy(x=>x.text).ToList(),effects=effects.Values.OrderBy(x=>x.text).ToList()};
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
   var all=Build(StandardEntries());if(all.triggers.Count<100||all.effects.Count<100||all.triggers.Select(x=>x.key).Distinct().Count()!=all.triggers.Count||all.effects.Select(x=>x.key).Distinct().Count()!=all.effects.Count)throw new Exception("Part catalog uniqueness failed");
  }
 }
}
