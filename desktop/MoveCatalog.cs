using System;
using System.Collections.Generic;
using System.Linq;

namespace AABattle {
 public class MoveClassification { public string name="",target="",contact="",attack="",defense=""; public string[] tags=new string[0]; }
 // 제공 시트의 기술 목록을 내장한 정적 분류 원본. 전투 데이터의 위력·타입은 기존 reference-data.json을 기준으로 유지한다.
 public static class MoveCatalog {
  static MoveClassification[] cached;
  static string[] Lines(string value){return (value??"").Replace("\r","").Split(new[]{'\n'},StringSplitOptions.RemoveEmptyEntries).Select(x=>x.Trim()).Where(x=>x.Length>0&&x!="－").ToArray();}
  public static MoveClassification[] All { get { if(cached==null)cached=Parse(Resource.Text("move-classification.tsv")).ToArray();return cached; } }
  static IEnumerable<MoveClassification> Parse(string text){
   var rows=(text??"").Replace("\r","").Split('\n');foreach(var row in rows.Skip(1)){var c=row.Split('\t');if(c.Length<18||string.IsNullOrWhiteSpace(c[0]))continue;yield return new MoveClassification{name=c[0].Trim(),target=c[7].Trim(),contact=c[9].Trim(),tags=c.Skip(11).Take(4).SelectMany(Lines).Distinct().ToArray(),attack=c[15].Trim(),defense=c[16].Trim()};}
  }
  public static void Apply(Database db){
   if(db==null||db.moves==null)return;var map=All.GroupBy(x=>x.name).ToDictionary(x=>x.Key,x=>x.First());foreach(var move in db.moves){MoveClassification c;if(move==null||!map.TryGetValue(move.name??"",out c))continue;move.target=c.target;move.attack=move.attack=="－"||string.IsNullOrWhiteSpace(move.attack)?c.attack:move.attack;move.defense=move.defense=="－"||string.IsNullOrWhiteSpace(move.defense)?c.defense:move.defense;move.tags=(move.tags??new string[0]).SelectMany(Lines).Concat(c.tags).Concat(c.contact=="○"?new[]{"접촉"}:new string[0]).Distinct().ToArray();}
  }
  public static void Test(Database db){var cannon=All.FirstOrDefault(x=>x.name=="가시대포");var scissors=All.FirstOrDefault(x=>x.name=="가위자르기");var future=All.FirstOrDefault(x=>x.name=="미래예지");var move=db.moves.FirstOrDefault(x=>x.name=="가시대포");var contact=db.moves.FirstOrDefault(x=>x.name=="가위자르기");if(All.Length!=1391||cannon==null||cannon.target!="단일"||!cannon.tags.Contains("연속")||scissors==null||future==null||move==null||move.target!="단일"||contact==null||!contact.tags.Contains("접촉"))throw new Exception("Move classification import");}
 }
}
