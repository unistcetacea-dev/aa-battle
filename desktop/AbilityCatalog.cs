using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace AABattle {
 public class AbilityDefinition { public string name,effect; }
 public static class AbilityCatalog {
  static AbilityDefinition[] cached;
  public static AbilityDefinition[] All {
   get { if(cached==null)cached=Parse(Decode()).GroupBy(x=>x.name).Select(x=>x.First()).OrderBy(x=>x.name).ToArray();return cached; }
  }
  static string Decode(){
   byte[] packed=Convert.FromBase64String(Resource.Text("abilities.b64").Trim());
   using(var source=new MemoryStream(packed))using(var gzip=new GZipStream(source,CompressionMode.Decompress))using(var reader=new StreamReader(gzip,Encoding.UTF8))return reader.ReadToEnd();
  }
  static IEnumerable<AbilityDefinition> Parse(string text){
   var rows=new List<string>();var current=new StringBuilder();bool quoted=false;
   foreach(char ch in text){if(ch=='\"')quoted=!quoted;if((ch=='\n'||ch=='\r')&&!quoted){if(current.Length>0){rows.Add(current.ToString());current.Clear();}}else current.Append(ch);}if(current.Length>0)rows.Add(current.ToString());
   foreach(string row in rows.Skip(1)){int tab=row.IndexOf('\t');if(tab<1)continue;string name=row.Substring(0,tab).Trim();string effect=row.Substring(tab+1).Trim().Trim('\"').Replace("，",",").Replace("．",".");if(name.Length>0)yield return new AbilityDefinition{name=name,effect=effect};}
  }
  public static AbilityDefinition Find(string name){return All.FirstOrDefault(x=>x.name==name);}
  public static void Test(){if(All.Length<580||Find("가뭄")==null||!Find("가뭄").effect.Contains("쾌청")||Find("가속")==null)throw new Exception("Ability catalog import");}
 }
}
