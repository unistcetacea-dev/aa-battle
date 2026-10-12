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
  public static readonly HashSet<string> Dedicated = new HashSet<string> { "풍선", "방진고글", "먹다남은음식", "검은진흙", "통굽부츠", "기합의띠", "기합의머리띠" };
  // 문서에 「1번만」은 없지만 쓰면 사라지는 것(「사용하여」·실기 소비)
  static readonly HashSet<string> Consumed = new HashSet<string> { "룸서비스", "목스프레이", "허탕보험", "애슈열매" };
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
   { "일렉트릭시드", "「일렉트릭필드」가 되었을 때, 자신의 「방어」가 오른다." },
   // 전용 처리였던 것을 모듈 문장으로(사용자 지시): 배율·무효·지속·순서·특성
   { "생명의구슬", "자신의 「공격」 기술의 위력을 강화(1.3배)한다." },
   { "구애머리띠", "자신의 「공격」을 강화(1.5배)한다." },
   { "구애안경", "자신의 「특공」을 강화(1.5배)한다." },
   { "구애스카프", "자신의 「속도」를 강화(1.5배)한다." },
   { "검은철구", "자신의 「속도」를 약화(0.5배)한다." },
   { "보코열매", "자신의 체력이 가득일 때, 자신이 받는 「물리」 기술의 대미지를 약화(0.5배)한다." },
   { "마쥬열매", "자신의 체력이 가득일 때, 자신이 받는 「특수」 기술의 대미지를 약화(0.5배)한다." },
   { "스펀지", "상대의 「물」 공격을 무효화한다.\n상대의 공격을 받았을 때, 자신의 소지품이 부서진다." },
   { "횃불", "상대의 「불꽃」 공격을 무효화한다.\n상대의 공격을 받았을 때, 자신의 소지품이 부서진다." },
   { "뜨거운바위", "자신이 전개하는 「쾌청」의 지속 T를 +3한다." },
   { "보송보송바위", "자신이 전개하는 「모래바람」의 지속 T를 +3한다." },
   { "차가운바위", "자신이 전개하는 「싸라기눈」「눈」의 지속 T를 +3한다." },
   { "축축한바위", "자신이 전개하는 「비」의 지속 T를 +3한다." },
   { "빛의점토", "자신이 전개하는 「리플렉터」「빛의장막」의 지속 T를 +3한다." },
   { "그라운드코트", "자신이 전개하는 「그래스필드」「일렉트릭필드」「사이코필드」「미스트필드」의 지속 T를 +3한다." },
   { "초점렌즈", "자신의 기술의 급소 랭크를 +1한다." },
   { "겨냥표적", "상대의 기술이 타입 상성에 의한 무효를 관통한다." },
   { "느림보꼬리", "자신의 기술의 우선도를 -0.5한다." },
   { "선제공격손톱", "T 개시 시, 저확률로 상대보다 먼저 행동한다." },
   { "만능우산", "자신은 「쾌청」「비」의 영향을 받지 않는다." },
   { "주눅구슬", "상대에 의해 자신의 능력이 내려갔을 때, 자신의 「속도」가 오른다." },
   { "미라인형", "상대의 「접촉」 기술을 받았을 때, 상대의 특성을 「미라」로 한다." },
   { "룸서비스", "「트릭룸」이 전개되었을 때, 자신의 「속도」를 내린다." },
   { "목스프레이", "자신이 「소리」 기술을 내보냈을 때, 자신의 「특공」이 오른다." },
   { "허탕보험", "자신의 기술이 빗나갔을 때, 자신의 「속도」가 크게 오른다." },
   { "왕의징표석", "자신의 기술에 「1할:풀죽음」의 추가효과를 부여한다." },
   { "포커스렌즈", "상대가 자신보다 먼저 행동할 때, 자신의 「명중」을 강화(1.2배)한다." },
   // 새 모듈(MoveModules)
   { "큰뿌리", "자신의 「드레인」 기술의 회복량을 1.3배로 한다." },
   { "조임밴드", "자신이 거는 「조이기」의 대미지를 최대 HP의 1/6로 한다." },
   { "끈기갈고리손톱", "자신이 거는 「조이기」의 지속 T를 5T로 한다." },
   { "메트로놈", "자신이 같은 기술을 연속으로 내보낼 때마다 그 기술의 위력을 0.1배씩 강화한다(최대 2배)." },
   { "파워풀허브", "자신의 「모으기」 기술의 모으기를 생략한다." },
   { "방호패드", "자신의 「접촉」 기술에 반응하는 효과를 받지 않는다." },
   { "하양허브", "자신의 능력이 내려갔을 때, 자신의 내려간 능력을 원래대로 되돌린다." },
   { "애슈열매", "자신의 체력이 1/4 이하가 되었을 때, 다음 T 자신의 기술 우선도를 1 올린다." },
   { "끈적끈적바늘", "T 종료 시, 자신에게 최대 HP의 1/8 대미지를 준다.\n상대의 「접촉」 기술을 받았을 때, 상대가 소지품을 가지고 있지 않을 때, 자신의 소지품을 상대에게 옮긴다." }
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
  public static bool Once(ItemDefinition d) { return d != null && (Regex.IsMatch(d.Effect ?? "", @"1번만") || Consumed.Contains(d.Name)); }

  static string Text(ItemDefinition d) {
   string rewrite, type;
   if (Rewrites.TryGetValue(d.Name, out rewrite)) return rewrite;
   if (ResistBerries.TryGetValue(d.Name, out type)) return "자신이 받는 「" + type + "」" + (type == "노말" ? "" : "「효과가 굉장」") + " 기술의 대미지를 약화(0.5배)한다.";
   return Regex.Replace(Clean(d.Effect), @"(?:시합\s*중\s*)?1번만\s*", "");
  }
  // 한 소지품이 계속 효과와 사건 효과를 함께 가지면 줄마다 포텐셜 하나
  public static Potential[] ToPotentials(ItemDefinition d) {
   var lines = Text(d).Split('\n').Select(x => x.Trim()).Where(x => x.Length > 0).ToArray();
   return lines.Select((line, i) => ToPotential(d, line, i)).ToArray();
  }
  public static Potential ToPotential(ItemDefinition d) { return ToPotentials(d).First(); }
  static Potential ToPotential(ItemDefinition d, string text, int index) {
   string condition, effect;
   PotentialLibrary.Split(text, out condition, out effect);
   return new Potential { id = Slot, name = index == 0 ? d.Name : d.Name + " (" + (index + 1) + ")", description = text, triggers = condition.Length > 0 ? new string[0] : new[] { TriggerStructures.AlwaysLabel }, uses = Once(d) ? "1/시" : "" };
  }

  // 트리거와 효과가 모두 모듈로 읽히면 실행할 수 있다
  public static bool Runnable(ItemDefinition d) {
   if (d == null || Dedicated.Contains(d.Name)) return false;
   return ToPotentials(d).All(p => {
    string condition, effect;
    PotentialLibrary.Split(p.description, out condition, out effect);
    bool trigger = TriggerStructures.Clauses(p).All(c => c == TriggerStructures.AlwaysLabel || TriggerModules.IsModule(c));
    return trigger && EffectModules.Classify(effect.Length > 0 ? effect : p.description).Length > 0;
   });
  }

  // 포켓몬 포텐셜 목록의 소지품 항목을 지금 소지품에 맞춘다(Data는 포켓몬마다 따로 복사된다)
  public static void Sync(Fighter f) {
   if (f == null || f.Data == null) return;
   var list = (f.Data.potentials ?? new Potential[0]).Where(p => p == null || p.id != Slot).ToList();
   var d = ItemCatalog.Find(f.Data.item);
   if (Runnable(d) && f.Conditions.Embargo <= 0) list.AddRange(ToPotentials(d));
   f.Data.potentials = list.ToArray();
  }

  // 전용 코드로 처리하는 소지품(효과 안내 표시용)
  public static readonly HashSet<string> Coded = new HashSet<string> { "풍선", "방진고글", "먹다남은음식", "검은진흙", "통굽부츠", "기합의띠", "기합의머리띠" };
  public static string Status(ItemDefinition d) {
   if (d == null) return "미구현";
   if (d.Category == "중요한 물건") return "배틀 외";
   if (Coded.Contains(d.Name) || d.Name.StartsWith("기술머신")) return "자동 구현";
   return d.Name == "플레이트" || d.Name == "○주얼" ? "자동 구현" : Runnable(d) ? "자동 구현" : "미구현";
  }
  public static string Name(Fighter f) { if (f != null && f.Conditions.Embargo > 0) return ""; var d = f == null || f.Data == null ? null : ItemCatalog.Find(f.Data.item); return d != null ? d.Name : f == null || f.Data == null ? "" : f.Data.item ?? ""; }
  public static bool Holds(Fighter f, params string[] names) { string n = Name(f); return n.Length > 0 && names.Contains(n); }
  public static bool Choice(Fighter f) { return Holds(f, "구애머리띠", "구애안경", "구애스카프"); }

  // 「1번만」인 계속 배율(반감열매·보코/마쥬 등)이 이번 타격에 걸리는가. 맞기 전에 판정하고 맞은 뒤 소비한다
  public static List<Fighter> OnceScalars(Fighter a, Fighter b, Move m, double typeEffect, Rules r) {
   var used = new List<Fighter>();
   if (m == null || m.category == "변화") return used;
   foreach (var x in new[] { a, b }) {
    if (x == null || x.Data == null) continue;
    var other = ReferenceEquals(x, a) ? b : a;
    foreach (var p in (x.Data.potentials ?? new Potential[0]).Where(p => p != null && p.id == Slot && p.uses == "1/시")) {
     if (!TriggerStructures.Clauses(p).All(c => TriggerModules.Holds(c, x, other, r))) continue;
     string condition, effect; PotentialLibrary.Split(p.description, out condition, out effect);
     foreach (string line in Regex.Split(EffectNormalizer.NormalizeLines(effect.Length > 0 ? effect : p.description).Replace("\r", ""), @"\n|(?<=[.．。])\s+").Where(l => l.Trim().Length > 0)) {
      ScalarEffect se;
      if (!EffectModules.TryScalar(line, out se) || se.Subject != "자신") continue;
      bool relevant = ReferenceEquals(x, a) ? se.Stage == "위력" || se.Stage == "주는 대미지" : se.Stage == "받는 대미지";
      if (relevant && se.Condition.Matches(m, a, typeEffect) && !used.Contains(x)) used.Add(x);
     }
    }
   }
   return used;
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
