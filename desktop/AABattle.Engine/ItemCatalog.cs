using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace AABattle {
 public class ItemDefinition {
  public string Name = "", Category = "", Effect = "";
  public string Type = "";   // 플레이트·주얼처럼 타입이 붙는 도구의 타입
 }

 // 소지품 도감. 기준은 참치백과 「아바돈_소지품」(research/items/abaddon-items.txt, 사용자 지정) + 오레마스 시스템의 오리지널 소지품 규칙.
 // 「○」 자리는 플레이트·주얼의 타입이다. 「(8세대)」 같은 표기는 이름에서 뗀다
 public static class ItemCatalog {
  static ItemDefinition[] cached;
  static readonly Dictionary<string, string> Aliases = new Dictionary<string, string> { { "철구", "검은철구" }, { "달라붙는벌레", "달라붙는벌레" }, { "끈적끈적벌레", "달라붙는벌레" }, { "신비한 스푼", "신비한스푼" } };
  // 정식 플레이트 이름 → 타입(노말 플레이트는 없다)
  static readonly Dictionary<string, string> Plates = new Dictionary<string, string> {
   { "불구슬플레이트", "불꽃" }, { "물방울플레이트", "물" }, { "우뢰플레이트", "전기" }, { "초록플레이트", "풀" }, { "고드름플레이트", "얼음" }, { "주먹플레이트", "격투" },
   { "맹독플레이트", "독" }, { "대지플레이트", "땅" }, { "푸른하늘플레이트", "비행" }, { "이상한플레이트", "에스퍼" }, { "비단벌레플레이트", "벌레" }, { "암석플레이트", "바위" },
   { "원령플레이트", "고스트" }, { "용의플레이트", "드래곤" }, { "공포플레이트", "악" }, { "강철플레이트", "강철" }, { "정령플레이트", "페어리" }
  };
  // 문서에 없지만 엔진이 이미 처리하던 소지품
  static readonly ItemDefinition[] Builtin = {
   new ItemDefinition { Name = "풍선", Category = "기타 도구", Effect = "「땅」기술을 받지 않는다. 공격을 받으면 터진다." }
  };

  public static ItemDefinition[] All {
   get {
    if (cached == null) cached = Parse(Resource.Text("items.txt")).Concat(Builtin).GroupBy(x => x.Name).Select(x => x.First()).ToArray();
    return cached;
   }
  }

  public static ItemDefinition[] Parse(string text) {
   var list = new List<ItemDefinition>();
   string category = "";
   ItemDefinition last = null;
   foreach (string raw in (text ?? "").Replace("\r", "").Split('\n')) {
    string line = Regex.Replace(raw, @"\\\\\s*$", "").Trim().TrimStart('　', 'ㆍ', '・', ' ');
    if (line.Length == 0 || line.StartsWith("----") || line.StartsWith("^") || line.StartsWith("|") || line.StartsWith("=")) { last = null; continue; }
    var section = Regex.Match(line, @"^[《【]\s*(?<c>[^》】]+?)\s*[》】]$");
    if (section.Success) { category = section.Groups["c"].Value; last = null; continue; }
    var item = Regex.Match(line, @"^「(?<n>[^」]+)」\s*…\s*(?<e>.*)$");
    if (item.Success) {
     string name = Regex.Replace(item.Groups["n"].Value, @"\(\d+세대\)", "").Trim();
     last = new ItemDefinition { Name = name, Category = category, Effect = item.Groups["e"].Value.Trim() };
     list.Add(last);
     continue;
    }
    if (last != null && line.StartsWith("(")) last.Effect += " " + line;
   }
   return list.ToArray();
  }

  // 이름으로 찾는다. 「불꽃플레이트」「불꽃주얼」처럼 타입이 붙은 이름과 정식 플레이트 이름도 받는다
  public static ItemDefinition Find(string name) {
   string n = (name ?? "").Trim();
   if (n.Length == 0) return null;
   string alias; if (Aliases.TryGetValue(n, out alias)) n = alias;
   var exact = All.FirstOrDefault(x => x.Name == n);
   if (exact != null && !exact.Name.Contains("○") && exact.Name != "플레이트") return exact;
   string type;
   if (Plates.TryGetValue(n, out type)) return Typed("플레이트", n, type);
   var m = Regex.Match(n, @"^(?<t>[가-힣]+?)\s*(?<k>플레이트|주얼)$");
   if (m.Success && MoveCondition.TypeNames.Contains(m.Groups["t"].Value)) return Typed(m.Groups["k"].Value == "주얼" ? "○주얼" : "플레이트", n, m.Groups["t"].Value);
   return exact;
  }
  static ItemDefinition Typed(string template, string name, string type) {
   var source = All.FirstOrDefault(x => x.Name == template);
   if (source == null) return null;
   return new ItemDefinition { Name = name, Category = source.Category, Effect = source.Effect.Replace("○", type), Type = type };
  }
 }
}
