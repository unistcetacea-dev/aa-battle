using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace AABattle {
 // 소지품 효과: 도감 문장을 포텐셜과 같은 트리거·효과 모듈로 돌린다(슬롯 「소지품」). 「1번만」은 사용 1/시 + 쓰면 소지품이 사라진다.
 // 모듈로 표현할 수 없는 것은 전용 처리(구애·생명의구슬·반감열매 등)가 맡고 여기서는 붙이지 않는다
 public static class ItemRuntime {
  public const string Slot = "소지품";
  // 엔진이 전용 코드로 처리하는 소지품(여기서 포텐셜로 붙이지 않는다)
  public static readonly HashSet<string> Dedicated = new HashSet<string> { "풍선", "검은철구", "철구", "방진고글", "먹다남은음식", "검은진흙", "통굽부츠", "기합의띠" };
  // 문서 문장을 같은 뜻의 모듈 문장으로 옮긴 것(「1번만」 여부는 원문을 따른다)
  public static readonly Dictionary<string, string> Rewrites = new Dictionary<string, string> {
   { "리샘열매", "자신이 「상태이상」「혼란」 상태가 되었을 때, 자신의 「상태이상」「혼란」 상태를 치유한다." },
   { "멘탈허브", "자신이 「헤롱헤롱」「앙코르」「트집」「사슬묶기」「도발」「회복봉인」 상태가 되었을 때, 자신의 「헤롱헤롱」「앙코르」「트집」「사슬묶기」「도발」「회복봉인」 상태를 치유한다." },
   { "울퉁불퉁멧", "상대의 「접촉」 기술을 받았을 때, 상대에게 최대 HP의 1/8 대미지를 준다." },
   { "자보열매", "상대의 「물리」 기술을 받았을 때, 상대에게 최대 HP의 1/8 대미지를 준다." },
   { "애터열매", "상대의 「특수」 기술을 받았을 때, 상대에게 최대 HP의 1/8 대미지를 준다." },
   { "레드카드", "상대의 공격을 받았을 때, 상대를 강제교대시킨다." },
   { "스타열매", "자신의 체력이 1/4 이하가 되었을 때, 자신의 랜덤한 능력치가 크게 오른다." },
   { "그래스시드", "「그래스필드」가 되었을 때, 자신의 「방어」가 오른다." },
   { "미스트시드", "「미스트필드」가 되었을 때, 자신의 「특방」이 오른다." },
   { "사이코시드", "「사이코필드」가 되었을 때, 자신의 「특방」이 오른다." },
   { "일렉트릭시드", "「일렉트릭필드」가 되었을 때, 자신의 「방어」가 오른다." }
  };
  // 반감열매: 효과가 굉장한 그 타입 기술(카리열매는 노말)의 대미지를 1번만 반감
  public static readonly Dictionary<string, string> ResistBerries = new Dictionary<string, string> {
   { "오카열매", "불꽃" }, { "꼬시개열매", "물" }, { "초나열매", "전기" }, { "린드열매", "풀" }, { "플카열매", "얼음" }, { "로플열매", "격투" }, { "으름열매", "독" }, { "슈캐열매", "땅" }, { "바코열매", "비행" },
   { "야파열매", "에스퍼" }, { "리체열매", "벌레" }, { "루미열매", "바위" }, { "수불열매", "고스트" }, { "하반열매", "드래곤" }, { "마코열매", "악" }, { "바리비열매", "강철" }, { "로셀열매", "페어리" }, { "카리열매", "노말" }
  };

  static string Clean(string effect) {
   string t = effect ?? "";
   t = Regex.Replace(t, @"\s*<=.*$", "");
   t = Regex.Replace(t, @"\s*[(（]※[^)）]*[)）]", "");
   t = Regex.Replace(t, @"\s*\((?:플레이트가|메리트|독압정|내보내는)[^)]*\)", "");
   return t.Trim();
  }
  public static bool Once(ItemDefinition d) { return d != null && Regex.IsMatch(d.Effect ?? "", @"1번만"); }

  public static Potential ToPotential(ItemDefinition d) {
   string rewrite;
   string text = Rewrites.TryGetValue(d.Name, out rewrite) ? rewrite : Regex.Replace(Clean(d.Effect), @"(?:시합\s*중\s*)?1번만\s*", "");
   string condition, effect;
   PotentialLibrary.Split(text, out condition, out effect);
   return new Potential { id = Slot, name = d.Name, description = text, triggers = condition.Length > 0 ? new string[0] : new[] { TriggerStructures.AlwaysLabel }, uses = Once(d) ? "1/시" : "" };
  }

  // 트리거와 효과가 모두 모듈로 읽히면 실행할 수 있다
  public static bool Runnable(ItemDefinition d) {
   if (d == null || Dedicated.Contains(d.Name)) return false;
   var p = ToPotential(d);
   string condition, effect;
   PotentialLibrary.Split(p.description, out condition, out effect);
   bool trigger = TriggerStructures.Clauses(p).All(c => c == TriggerStructures.AlwaysLabel || TriggerModules.IsModule(c));
   return trigger && EffectModules.Classify(effect.Length > 0 ? effect : p.description).Length > 0;
  }

  // 포켓몬 포텐셜 목록의 소지품 항목을 지금 소지품에 맞춘다(Data는 포켓몬마다 따로 복사된다)
  public static void Sync(Fighter f) {
   if (f == null || f.Data == null) return;
   var list = (f.Data.potentials ?? new Potential[0]).Where(p => p == null || p.id != Slot).ToList();
   var d = ItemCatalog.Find(f.Data.item);
   if (Runnable(d)) list.Add(ToPotential(d));
   f.Data.potentials = list.ToArray();
  }

  // 전용 코드로 처리하는 소지품(효과 안내 표시용)
  public static readonly HashSet<string> Coded = new HashSet<string> { "풍선", "검은철구", "방진고글", "먹다남은음식", "검은진흙", "통굽부츠", "기합의띠", "기합의머리띠", "구애머리띠", "구애안경", "구애스카프", "생명의구슬", "보코열매", "마쥬열매", "초점렌즈", "겨냥표적", "스펀지", "횃불", "뜨거운바위", "보송보송바위", "차가운바위", "축축한바위", "빛의점토", "그라운드코트", "선제공격손톱", "느림보꼬리" };
  public static string Status(ItemDefinition d) {
   if (d == null) return "미구현";
   if (d.Category == "중요한 물건") return "배틀 외";
   if (Coded.Contains(d.Name) || ResistBerries.ContainsKey(d.Name) || d.Name.StartsWith("기술머신")) return "자동 구현";
   return d.Name == "플레이트" || d.Name == "○주얼" ? "자동 구현" : Runnable(d) ? "자동 구현" : "미구현";
  }
  public static string Name(Fighter f) { var d = f == null || f.Data == null ? null : ItemCatalog.Find(f.Data.item); return d != null ? d.Name : f == null || f.Data == null ? "" : f.Data.item ?? ""; }
  public static bool Holds(Fighter f, params string[] names) { string n = Name(f); return n.Length > 0 && names.Contains(n); }
  public static bool Choice(Fighter f) { return Holds(f, "구애머리띠", "구애안경", "구애스카프"); }

  // 능력치 배율(구애: 공격·특공·속도 1.5배, 오레마스 시스템)
  public static double StatFactor(Fighter f, int index) {
   if (index == 1 && Holds(f, "구애머리띠") || index == 3 && Holds(f, "구애안경") || index == 5 && Holds(f, "구애스카프")) return 1.5;
   if (index == 5 && Holds(f, "검은철구", "철구")) return .5;
   return 1;
  }
  // 위력 배율(생명의구슬 1.3배)
  public static double PowerFactor(Fighter a, Move m) { return m != null && m.category != "변화" && Holds(a, "생명의구슬") ? 1.3 : 1; }
  // 받는 대미지 배율: 반감열매·보코/마쥬열매(체력이 가득일 때). 소비는 실제로 맞은 뒤(Consumed)
  public static bool HalvesDamage(Fighter a, Fighter b, Move m, double typeEffect, Rules r) {
   if (b == null || m == null || m.category == "변화") return false;
   string n = Name(b), type;
   if (ResistBerries.TryGetValue(n, out type)) return (m.types ?? new string[0]).Contains(type) && (type == "노말" || typeEffect > 1);
   if (n == "보코열매" || n == "마쥬열매") return (n == "보코열매" ? m.category == "물리" : m.category == "특수") && b.HP >= Mechanics.MaxHP(b, a ?? b, r ?? new Rules());
   return false;
  }
  public static void AfterHit(Fighter a, Fighter b, Move m, double typeEffect, bool halved, bool damaged, List<string> log) {
   if (b == null || m == null) return;
   if (halved) Lose(b, "소비", log.Add);
   // 스펀지·횃불: 공격을 받으면 부서진다
   if (damaged && m.category != "변화" && Holds(b, "스펀지", "횃불")) Lose(b, "부서졌다", log.Add);
  }
  // 날씨·지형·벽 지속 T 연장(+3)
  public static int Turns(Fighter source, string what, int turns) {
   if (source == null || turns <= 0) return turns;
   string n = Name(source);
   bool extend = what == "쾌청" && n == "뜨거운바위" || what == "모래바람" && n == "보송보송바위" || (what == "싸라기눈" || what == "눈") && n == "차가운바위" || what == "비" && n == "축축한바위"
    || EffectModules.TerrainNames.Contains(what) && n == "그라운드코트" || (what == "리플렉터" || what == "빛의장막") && n == "빛의점토";
   return extend ? turns + 3 : turns;
  }

  // 소지품을 잃는다(소비·파괴·빼앗김). 사건 기록 후 포텐셜 목록도 맞춘다
  public static void Lose(Fighter f, string reason, Action<string> log) {
   if (f == null || f.Data == null || string.IsNullOrEmpty(f.Data.item)) return;
   string item = f.Data.item;
   f.LostItem = item;
   f.Data.item = "";
   Sync(f);
   if (log != null) log(f.Data.name + ": 「" + item + "」" + (reason == "소비" ? "을 사용했다" : " — " + reason));
  }
 }
}
