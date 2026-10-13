using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace AABattle {
 // 소지품 기술 모듈: 기술 설명 문장(문서 기준)을 효과 모듈 표준 문장으로 바꿔 포텐셜과 같은 실행기로 돌린다.
 // 표준 문장(새 모듈)
 //  교환       서로의 소지품을 뒤바꾼다.                        (트릭·바꿔치기. 한쪽이 빈손이어도 된다)
 //  제한 파괴  상대의 「나무열매」「주얼」을 파괴한다.            (불태우기)
 //  재획득     자신이 소모한 소지품을 다시 얻는다.               (리사이클. 자신이 빈손일 때)
 //  모두 먹기  필드의 전원이 지니고 있는 「나무열매」를 먹는다.   (다과회)
 //  열매 뺏어먹기 상대의 「나무열매」를 자신이 먹는다.           (쪼아대기·벌레먹기)
 //  던지기     자신의 소지품을 상대에게 던진다.                  (내던지기. 위력·효과는 FlingTable)
 //  열매 소모  자신의 「나무열매」를 소모한다.                   (자연의은혜·배의뭉치. 타입·위력은 NaturalGiftTable)
 // 기존 모듈 재사용: 상대의 소지품을 빼앗는다(탐내기·도둑질) · 상대의 소지품을 파괴한다(「소지품 파괴」 기술)
 //                  자신의 소지품을 상대에게 옮긴다(기프트패스) · 자신의 「나무열매」를 사용한다(볼가득넣기)
 // 기술 설명 쪽(효과 모듈이 아닌 위력·타입·실패 처리): 파괴할 때/빈손일 때 위력 강화 · 내던지기·자연의은혜 위력 · 지닌 소지품에 따른 타입 변화
 public static class MoveItemModules {
  public const string Swap = "서로의 소지품을 뒤바꾼다";
  public const string BreakFood = "상대의 「나무열매」「주얼」을 파괴한다";
  public const string RecycleAny = "자신이 소모한 소지품을 다시 얻는다";
  public const string Teatime = "필드의 전원이 지니고 있는 「나무열매」를 먹는다";
  public const string Pluck = "상대의 「나무열매」를 자신이 먹는다";
  public const string Fling = "자신의 소지품을 상대에게 던진다";
  public const string ConsumeBerry = "자신의 「나무열매」를 소모한다";

  static string Clean(string text) {
   return Regex.Replace(PotentialParts.Clean(text ?? ""), @"\s+", " ").Trim().TrimEnd('.', '。', '．').Trim();
  }
  static string Plain(Move m) {
   string t = m == null ? "" : m.effect ?? "";
   t = t.Replace('（', '(').Replace('）', ')').Replace('，', ',').Replace('．', '.').Replace('＋', '+').Replace('：', ':').Replace('～', '~');
   return Regex.Replace(t, @"\s+", " ");
  }
  static IEnumerable<string> Parts(Move m) {
   return Regex.Split(Plain(m), @"\.(?![^()]*\))").Select(x => x.Trim().TrimEnd(',').Trim()).Where(x => x.Length > 0);
  }
  static bool Says(Move m, string pattern) { return Parts(m).Any(p => Regex.IsMatch(p, pattern)); }

  // ===== 표준 문장 =====
  public static string Normalize(string text) {
   string t = Clean(text);
   if (t == Swap || t == BreakFood || t == RecycleAny || t == Teatime || t == Pluck || t == Fling || t == ConsumeBerry) return t + ".";
   return null;
  }
  public static string Classify(string sentence) {
   string s = Clean(sentence);
   if (s == Swap) return "소지품 교환";
   if (s == BreakFood) return "소지품 제한 파괴";
   if (s == RecycleAny) return "소지품 재획득";
   if (s == Teatime) return "나무열매 모두 먹기";
   if (s == Pluck) return "나무열매 뺏어먹기";
   if (s == Fling) return "소지품 던지기";
   if (s == ConsumeBerry) return "나무열매 소모";
   return "";
  }

  // ===== 기술 문장 → 표준 문장 (MoveRuntime.Read가 부른다) =====
  // 읽었으면 true. rest가 있으면 그 문장도 이어서 읽는다
  static readonly string[] DescriptionOnly = {
   @"^상대의 「소지품」을 파괴할 때\s*,\s*위력이 강화\(\d+(?:\.\d+)?배\)된다$",
   @"^자신에게 「소지품」이 없을 때\s*,\s*위력이 강화\(\d+(?:\.\d+)?배\)된다$",
   @"^사용한 「소지품」에 따라 위력과 효과가 변화한다$",
   @"^사용한 「나무열매」에 따라 타입과 위력이 변화한다$",
   @"^자신이 지니고 있는 (?:플레이트|메모리|카세트)에 따라 (?:기술의 )?타입이 변화한다$"
  };
  public static bool Handles(string s) { return DescriptionOnly.Any(p => Regex.IsMatch(s ?? "", p)); }

  public static bool Read(Move m, string s, List<string> into, out string rest) {
   rest = null;
   Match x;
   if (Handles(s)) return true;
   if (s == "「소지품 파괴」 기술") {
    // 같은 기술에 「나무열매」「주얼」 한정 문장이 따로 있으면(불태우기) 그쪽만 쓴다
    if (!Says(m, @"^상대의 「나무열매」「주얼」을 파괴한다$")) into.Add(AbilityItemModules.Break + ".");
    return true;
   }
   if (Regex.IsMatch(s, @"^상대의 「나무열매」「주얼」을 파괴한다$")) { into.Add(BreakFood + "."); return true; }
   if (Regex.IsMatch(s, @"^상대의 「소지품」을 약탈\(파괴\)한다$")) { into.Add(AbilityItemModules.Take + "."); return true; }
   if (Regex.IsMatch(s, @"^서로의 「소지품」을 뒤바꾼다$")) { into.Add(Swap + "."); return true; }
   if (Regex.IsMatch(s, @"^자신의 「소지품」을 상대에게 넘겨준다$")) { into.Add("자신의 소지품을 상대에게 옮긴다."); return true; }
   if (Regex.IsMatch(s, @"^자신이 소모한 「소지품」을 다시 얻는다$")) { into.Add(RecycleAny + "."); return true; }
   if (Regex.IsMatch(s, @"^필드의 전원이 지니고 있는 「나무열매」를 먹는다$")) { into.Add(Teatime + "."); return true; }
   if (Regex.IsMatch(s, @"^상대의 「나무열매」를 자신이 먹는다$")) { into.Add(Pluck + "."); return true; }
   if (Regex.IsMatch(s, @"^지니고 있는 「소지품」을 사용해 공격한다$")) { into.Add(Fling + "."); return true; }
   if (Regex.IsMatch(s, @"^지니고 있는 「나무열매」를 사용해 내보낸다$")) { into.Add(ConsumeBerry + "."); return true; }
   x = Regex.Match(s, @"^지니고 있는 「나무열매」를 먹고\s*,\s*(?<r>.+)$");
   if (x.Success) { into.Add(AbilityItemModules.UseBerry + "."); rest = x.Groups["r"].Value; return true; }
   return false;
  }

  // 먹지 못하면 뒤 효과도 일으키지 않는 문장(볼가득넣기)
  public static bool IsGate(string line) { return Clean(line) == AbilityItemModules.UseBerry; }

  // ===== 위력 · 타입 · 실패 (기술 설명) =====
  static bool Protected(Fighter holder, Fighter other) { return AbilityItemModules.ItemProtected(holder, other); }

  // 「상대의 소지품을 파괴할 때」 · 「자신에게 소지품이 없을 때」 위력 배율
  public static double PowerFactor(Fighter a, Fighter b, Move m) {
   double v = 1;
   if (a == null || m == null || string.IsNullOrEmpty(m.effect)) return v;
   foreach (string part in Parts(m)) {
    var x = Regex.Match(part, @"^(?<c>상대의 「소지품」을 파괴할 때|자신에게 「소지품」이 없을 때)\s*,\s*위력이 강화\((?<n>\d+(?:\.\d+)?)배\)된다$");
    if (!x.Success) continue;
    bool holds = x.Groups["c"].Value.StartsWith("상대")
     ? b != null && !string.IsNullOrEmpty(b.Data.item) && !Protected(b, a)
     : string.IsNullOrEmpty(a.Data.item);
    if (holds) v *= double.Parse(x.Groups["n"].Value, System.Globalization.CultureInfo.InvariantCulture);
   }
   return v;
  }

  // 지닌 소지품에 따라 타입과 위력이 바뀐 기술(원본은 건드리지 않는다)
  public static Move Transform(Fighter a, Move m) {
   if (a == null || a.Data == null || m == null || m.category == "변화" || string.IsNullOrEmpty(m.effect) || !m.effect.Contains("에 따라")) return m;
   string item = ItemRuntime.Name(a);
   Match kind = null;
   foreach (string part in Parts(m)) {
    kind = Regex.Match(part, @"^자신이 지니고 있는 (?<k>플레이트|메모리|카세트)에 따라 (?:기술의 )?타입이 변화한다$");
    if (kind.Success) break;
   }
   if (kind != null && kind.Success) {
    string type = HeldTypeTable.TypeOf(kind.Groups["k"].Value, item) ?? "노말";
    var typed = m.Copy();
    typed.types = new[] { type };
    return typed;
   }
   if (Says(m, @"^사용한 「나무열매」에 따라 타입과 위력이 변화한다$")) {
    if (!ItemRuntime.IsBerry(item)) return m;
    string type; int power;
    NaturalGiftTable.Find(item, out type, out power);
    var gift = m.Copy();
    gift.types = new[] { type };
    gift.power = power;
    return gift;
   }
   if (Says(m, @"^사용한 「소지품」에 따라 위력과 효과가 변화한다$")) {
    var entry = FlingTable.Find(item);
    if (entry == null) return m;
    var flung = m.Copy();
    flung.power = entry.Power;
    return flung;
   }
   return m;
  }

  // 던질·소모할 소지품이 없으면 실패(대타 등과 상관없다)
  public static string FailReason(Fighter a, Fighter b, Move m) {
   if (a == null || m == null || string.IsNullOrEmpty(m.effect)) return "";
   if (Says(m, @"^사용한 「소지품」에 따라 위력과 효과가 변화한다$") && ItemRuntime.Name(a).Length == 0) return "소지품이 없어 실패했다.";
   if (Says(m, @"^사용한 「나무열매」에 따라 타입과 위력이 변화한다$") && !ItemRuntime.IsBerry(ItemRuntime.Name(a))) return "「나무열매」를 사용할 수 없어 실패했다.";
   return "";
  }

  // ===== 실행기 =====
  static bool Shielded(Fighter target) {
   // 기술의 소지품 조작은 대타에 막힌다
   return target != null && target.Conditions.Substitute > 0;
  }

  static EffectOutcome Exchange(EffectCall c) {
   var a = c.Owner; var b = c.Opponent;
   if (a == null || b == null || a.HP <= 0 || b.HP <= 0) return EffectOutcome.Failed;
   if (string.IsNullOrEmpty(a.Data.item) && string.IsNullOrEmpty(b.Data.item)) return EffectOutcome.Failed;
   if (Shielded(b)) { c.Say(b.Data.name + ": 대타가 소지품 교환을 막았다"); return EffectOutcome.Failed; }
   if (Protected(b, a)) { c.Say(b.Data.name + ": 소지품 파괴·약탈을 무효화했다"); return EffectOutcome.Failed; }
   string mine = a.Data.item ?? "", theirs = b.Data.item ?? "";
   a.Data.item = theirs;
   b.Data.item = mine;
   ItemRuntime.Sync(a);
   ItemRuntime.Sync(b);
   c.Say(a.Data.name + "의 「" + (mine.Length > 0 ? mine : "없음") + "」과 " + b.Data.name + "의 「" + (theirs.Length > 0 ? theirs : "없음") + "」이 뒤바뀌었다");
   return EffectOutcome.Success;
  }

  static bool Burnable(string item) {
   return ItemRuntime.IsBerry(item) || (item ?? "").EndsWith("주얼");
  }
  static EffectOutcome BreakFoodItem(EffectCall c) {
   var a = c.Owner; var b = c.Opponent;
   if (a == null || b == null || a.HP <= 0 || b.HP <= 0 || !Burnable(b.Data.item)) return EffectOutcome.Failed;
   if (Shielded(b)) return EffectOutcome.Failed;
   if (Protected(b, a)) { c.Say(b.Data.name + ": 소지품 파괴·약탈을 무효화했다"); return EffectOutcome.Failed; }
   ItemRuntime.Lose(b, "파괴", c.Say);
   return EffectOutcome.Success;
  }

  static EffectOutcome RecycleItem(EffectCall c) {
   var f = c.Owner;
   if (f == null || f.HP <= 0 || !string.IsNullOrEmpty(f.Data.item) || string.IsNullOrEmpty(f.ConsumedItem)) return EffectOutcome.Failed;
   f.Data.item = f.ConsumedItem;
   c.Say(f.Data.name + ": 「" + f.ConsumedItem + "」을 다시 얻었다");
   if (ItemRuntime.IsBerry(f.ConsumedItem)) f.LostBerry = "";
   f.ConsumedItem = "";
   ItemRuntime.Sync(f);
   AbilityItemModules.ForgetItemUsage(f);
   return EffectOutcome.Success;
  }

  static EffectOutcome EatAll(EffectCall c) {
   bool any = false;
   foreach (var f in new[] { c.Owner, c.Opponent }.Where(x => x != null && x.HP > 0))
    any |= AbilityItemModules.EatBerry(f, f == c.Owner ? c.Opponent : c.Owner, c.Say, c.Source.Label) == EffectOutcome.Success;
   return any ? EffectOutcome.Success : EffectOutcome.Failed;
  }

  // 남의 나무열매를 eater가 먹는다. 자신의 소지품은 먹는 동안만 치워 두고 돌려놓는다
  static EffectOutcome EatAs(Fighter eater, Fighter other, string berry, Action<string> say, string label) {
   string own = eater.Data.item ?? "";
   eater.Data.item = berry;
   ItemRuntime.Sync(eater);
   var result = AbilityItemModules.EatBerry(eater, other, say, label);
   eater.Data.item = own;
   ItemRuntime.Sync(eater);
   return result;
  }

  static EffectOutcome PluckBerry(EffectCall c) {
   var a = c.Owner; var b = c.Opponent;
   if (a == null || b == null || a.HP <= 0 || !ItemRuntime.IsBerry(b.Data.item)) return EffectOutcome.Failed;
   if (Shielded(b)) return EffectOutcome.Failed;
   if (Protected(b, a)) { c.Say(b.Data.name + ": 소지품 파괴·약탈을 무효화했다"); return EffectOutcome.Failed; }
   string berry = b.Data.item;
   ItemRuntime.Lose(b, "먹혔다", c.Say);
   EatAs(a, b, berry, c.Say, c.Source.Label);
   return EffectOutcome.Success;
  }

  static EffectOutcome ConsumeOwnBerry(EffectCall c) {
   var a = c.Owner;
   if (a == null || !ItemRuntime.IsBerry(a.Data.item)) return EffectOutcome.Failed;
   ItemRuntime.Lose(a, "소비", c.Say);
   return EffectOutcome.Success;
  }

  static EffectOutcome ThrowItem(EffectCall c) {
   var a = c.Owner; var b = c.Opponent;
   string item = a == null ? "" : ItemRuntime.Name(a);
   if (a == null || item.Length == 0) return EffectOutcome.Failed;
   var entry = FlingTable.Find(item);
   c.Say(a.Data.name + ": 「" + item + "」을 던졌다");
   ItemRuntime.Lose(a, "소비", c.Say);
   if (b == null || b.HP <= 0 || Shielded(b)) return EffectOutcome.Success;
   if (entry.EatsBerry) { EatAs(b, a, item, c.Say, c.Source.Label); return EffectOutcome.Success; }
   var move = MoveRuntime.CurrentMove;
   foreach (string line in entry.Effects) {
    var lines = new List<string>();
    MoveRuntime.Run(a, b, move, line, c.Rules ?? new Rules(), c.Rng ?? (() => .5), lines);
    foreach (string text in lines) c.Say(text);
   }
   return EffectOutcome.Success;
  }

  public static void Register() {
   PotentialExecutors.Register("소지품 교환", c => Clean(c.Line) == Swap, Exchange);
   PotentialExecutors.Register("소지품 제한 파괴", c => Clean(c.Line) == BreakFood, BreakFoodItem);
   PotentialExecutors.Register("소지품 재획득", c => Clean(c.Line) == RecycleAny, RecycleItem);
   PotentialExecutors.Register("나무열매 모두 먹기", c => Clean(c.Line) == Teatime, EatAll);
   PotentialExecutors.Register("나무열매 뺏어먹기", c => Clean(c.Line) == Pluck, PluckBerry);
   PotentialExecutors.Register("소지품 던지기", c => Clean(c.Line) == Fling, ThrowItem);
   PotentialExecutors.Register("나무열매 소모", c => Clean(c.Line) == ConsumeBerry, ConsumeOwnBerry);
  }
 }
}
