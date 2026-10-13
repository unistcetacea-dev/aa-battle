using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace AABattle {
 // 소지품의 보유와 사용 가능 여부는 별개다.
 // 소지품 모듈(특성·포텐셜 공용 표준 문장)
 //  약탈          상대의 소지품을 빼앗는다.            (자신이 빈손이면 약탈, 소지품이 있으면 상대 소지품만 파괴)
 //  파괴          상대의 소지품을 파괴한다.
 //  해석          상대의 소지품을 해석한다.            (소지품 이름을 로그에 공개)
 //  열매 사용     자신의 「나무열매」를 사용한다.       (효과 조건과 관계없이 지금 쓴다)
 //  되돌려받기    소모한 「나무열매」를 되돌려받는다.   (자신이 빈손일 때, 사용 이력도 지운다)
 //  다시 받기     사용한 나무열매의 효과를 다시 받는다. (지난 T에 사용한 나무열매. 빈손이면 한 번 더 먹는다)
 //  숙성          자신이 소모하는 「나무열매」의 효과가 2배가 된다.   (회복·대미지 분수 ×2, 랭크 한 단계 크게, 반감 열매는 1/4)
 //  플레이트 타입 자신의 타입을 소지품 「플레이트」의 타입으로 한다.
 //  소지품 사용 금지 · 보호 · 사건(소지품 소실 · 나무열매 사용)
 public static class AbilityItemModules {
  public const string Take = "상대의 소지품을 빼앗는다";
  public const string Break = "상대의 소지품을 파괴한다";
  public const string Frisk = "상대의 소지품을 해석한다";
  public const string UseBerry = "자신의 「나무열매」를 사용한다";
  public const string Recycle = "소모한 「나무열매」를 되돌려받는다";
  public const string Replay = "사용한 나무열매의 효과를 다시 받는다";
  public const string RipenRule = "자신이 소모하는 「나무열매」의 효과가 2배가 된다";
  public const string PlateType = "자신의 타입을 소지품 「플레이트」의 타입으로 한다";
  public const string NoItems = "자신은 소지품을 사용할 수 없다";
  public const string NoConsumedItems = "상대는 소비하는 소지품을 사용할 수 없다";
  public const string ProtectItem = "자신의 소지품은 파괴되거나 약탈되지 않는다";
  static string Clean(string text) {
   return Regex.Replace(PotentialParts.Clean(text ?? ""), @"\s+", " ").Trim().TrimEnd('.', '。', '．');
  }
  public static string Normalize(string text) {
   string s = Clean(text);
   if (s == Take || s == "상대의 소지품을 파괴(약탈)한다" || s == "상대가 가지고 있는 소지품을 약탈한다") return Take + ".";
   if (s == "상대의 「소지품」을 파괴한다") return Break + ".";
   if (s == "상대의 「소지품」을 해석한다") return Frisk + ".";
   if (s == "「나무열매」를 사용한다") return UseBerry + ".";
   if (s == "자신이 소모하는 「나무열매」의 효과가 2배가 된다") return RipenRule + ".";
   if (s == NoItems || s == "자신의 소지품을 사용할 수 없다") return NoItems + ".";
   if (s == NoConsumedItems || s == "전투 중 상대는 소비하는 소지품을 사용할 수 없다") return NoConsumedItems + ".";
   if (s == ProtectItem || s == "자신의 소지품을 파괴(약탈)할 수 없다") return ProtectItem + ".";
   if (Classify(s).Length > 0) return s + ".";
   return null;
  }
  public static string Classify(string text) {
   string s = Clean(text);
   if (s == Take) return "소지품 약탈";
   if (s == Break) return "소지품 파괴";
   if (s == Frisk) return "소지품 해석";
   if (s == UseBerry) return "나무열매 사용";
   if (s == Recycle) return "나무열매 되돌려받기";
   if (s == Replay) return "나무열매 효과 다시 받기";
   if (s == RipenRule) return "나무열매 효과 2배";
   if (s == PlateType) return "소지품 타입 변화";
   if (s == NoItems || s == NoConsumedItems) return "소지품 사용 금지";
   if (s == ProtectItem) return "소지품 보호";
   return "";
  }
  static IEnumerable<string> Lines(Fighter holder, Fighter other, bool bypass = false) {
   var scope = EffectScope.Current;
   return PotentialModifiers.Sentences(holder, other, scope == null ? new Rules() : scope.Rules, "소지품",
    holder == null ? null : (holder.Data.potentials ?? new Potential[0]).Where(p => p == null || p.id != ItemRuntime.Slot), bypass);
  }
  public static bool CanUseItem(Fighter holder) {
   if (holder == null || holder.Data == null || holder.Conditions.Embargo > 0) return false;
   var other = holder.FieldOpponent ?? PotentialQueue.OpponentOf(holder);
   if (Lines(holder, other, true).Any(s => Clean(s) == NoItems)) return false;
   return !ItemRuntime.Once(ItemCatalog.Find(holder.Data.item)) || !Lines(other, holder, true).Any(s => Clean(s) == NoConsumedItems);
  }
  public static bool ItemProtected(Fighter holder, Fighter other) {
   return Lines(holder, other).Any(s => Clean(s) == ProtectItem);
  }

  // ===== 숙성 =====
  // 소지품 슬롯을 뺀 나머지 포텐셜(특성 포함)에서 찾는다
  public static bool Ripens(Fighter holder) {
   if (holder == null || holder.Data == null || holder.AbilitySuppressed) return false;
   var definition = AbilityCatalog.Find(AbilityRuntime.Name(holder));
   return definition != null && AbilityModules.Sentences(definition).Any(s => Clean(s) == RipenRule)
    || (holder.Data.potentials ?? new Potential[0]).Any(p => p != null && p.id != ItemRuntime.Slot && (p.description ?? "").Contains(RipenRule));
  }
  // 나무열매 효과 문장을 2배로: 회복·대미지 분수는 분모를 반으로, 랭크는 한 단계 크게, 감소 배율은 반으로, 우선도는 2배
  // 도감 원문 변형(「공」을 올린다)도 같게 읽도록 효과를 표준 문장으로 바꾼 뒤 옮긴다
  public static string Ripen(string description) {
   string condition, effect;
   PotentialLibrary.Split(description ?? "", out condition, out effect);
   if (effect.Length == 0) return description;
   var lines = EffectNormalizer.NormalizeLines(effect).Replace("\r", "").Split('\n').Where(x => x.Trim().Length > 0).Select(RipenLine);
   string triggers = string.Join(", ", condition.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()));
   return (triggers.Length > 0 ? triggers + ", " : "") + string.Join(" ", lines);
  }
  static string RipenLine(string line) {
   string text = line.Trim();
   text = Regex.Replace(text, @"(최대 HP의 )1/(\d+)", m => m.Groups[1].Value + "1/" + Math.Max(1, int.Parse(m.Groups[2].Value) / 2));
   text = Regex.Replace(text, @"(「[^」]+」(?:이|가) |랜덤한 능력치(?:가|이) )(크게 )?(오른다|내린다)", m =>
    m.Groups[1].Value + (m.Groups[2].Success ? "매우 크게 " : "크게 ") + m.Groups[3].Value);
   text = Regex.Replace(text, @"약화\(([0-9.]+)배\)", m => "약화(" + (double.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) / 2).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + "배)");
   text = Regex.Replace(text, @"우선도를 (\d) 올린다", m => "우선도를 " + (int.Parse(m.Groups[1].Value) * 2) + " 올린다");
   return text;
  }

  // ===== 플레이트 타입 =====
  // 소지품이 바뀌거나 특성이 바뀔 때 맞춘다. 원래 타입은 따로 기억해 되돌린다
  public static void SyncItemType(Fighter f) {
   if (f == null || f.Data == null) return;
   var c = f.Conditions;
   var definition = AbilityCatalog.Find(AbilityRuntime.Name(f));
   bool rule = !f.AbilitySuppressed && definition != null && AbilityModules.Sentences(definition).Any(s => Clean(s) == PlateType);
   var item = rule && CanUseItem(f) ? ItemCatalog.Find(f.Data.item) : null;
   bool plate = item != null && item.Type.Length > 0 && item.Name.EndsWith("플레이트");
   if (plate) {
    if (c.ItemTypeBackup == null) c.ItemTypeBackup = f.Data.types.ToArray();
    f.Data.types = new[] { item.Type };
   } else if (c.ItemTypeBackup != null) {
    f.Data.types = c.ItemTypeBackup.ToArray();
    c.ItemTypeBackup = null;
   }
  }

  // ===== 약탈 · 파괴 · 해석 =====
  public static EffectOutcome Steal(Fighter owner, Fighter other, Action<string> log) {
   // 방금 쓰러뜨린 상대(빈사)의 소지품도 빼앗을 수 있다
   if (owner == null || other == null || owner.HP <= 0 || string.IsNullOrEmpty(other.Data.item)) return EffectOutcome.Failed;
   if (ItemProtected(other, owner)) {
    if (log != null) log(other.Data.name + ": 소지품 파괴·약탈을 무효화했다");
    return EffectOutcome.Failed;
   }
   if (string.IsNullOrEmpty(owner.Data.item)) return ItemRuntime.Transfer(other, owner, log, true) ? EffectOutcome.Success : EffectOutcome.Failed;
   ItemRuntime.Lose(other, "파괴", log);
   return EffectOutcome.Success;
  }
  static EffectOutcome BreakItem(Fighter owner, Fighter other, Action<string> log) {
   if (owner == null || other == null || owner.HP <= 0 || other.HP <= 0 || string.IsNullOrEmpty(other.Data.item)) return EffectOutcome.Failed;
   if (ItemProtected(other, owner)) {
    if (log != null) log(other.Data.name + ": 소지품 파괴·약탈을 무효화했다");
    return EffectOutcome.Failed;
   }
   ItemRuntime.Lose(other, "파괴", log);
   return EffectOutcome.Success;
  }

  // ===== 나무열매 =====
  // 소지품 슬롯의 효과를 조건과 관계없이 지금 실행한다(1번만인 열매는 쓰면 사라진다)
  static EffectOutcome UseNow(EffectCall c) {
   var f = c.Owner;
   if (f == null || f.HP <= 0 || !ItemRuntime.IsBerry(f.Data.item) || !CanUseItem(f)) return EffectOutcome.Failed;
   string item = f.Data.item;
   if (!PotentialQueue.Active) return EffectOutcome.Pending;
   c.Say(f.Data.name + ": 「" + item + "」을 지금 사용한다 (" + c.Source.Label + ")");
   PotentialQueue.Raise(new PotentialEvent {
    Owner = f, Other = c.Opponent, Spec = clause => true, Label = "나무열매 사용", Message = "", Log = c.Say, SlotOnly = ItemRuntime.Slot
   });
   return f.Data.item == item ? EffectOutcome.Failed : EffectOutcome.Success;
  }
  static void ForgetItemUsage(Fighter f) {
   var scope = EffectScope.Current;
   if (scope == null || scope.States == null || f == null) return;
   int side = Mechanics.PartySide(f, scope.Rules);
   if (side < 0 || side >= scope.States.Length || scope.States[side] == null) return;
   scope.States[side].Usage.Forget(PotentialKeys.SlotPrefix(f.Data, ItemRuntime.Slot));
  }
  static EffectOutcome RecycleBerry(EffectCall c) {
   var f = c.Owner;
   if (f == null || f.HP <= 0 || !string.IsNullOrEmpty(f.Data.item) || !ItemRuntime.IsBerry(f.LostBerry)) return EffectOutcome.Failed;
   f.Data.item = f.LostBerry;
   c.Say(f.Data.name + ": 「" + f.LostBerry + "」을 되돌려받았다 (" + c.Source.Label + ")");
   f.LostBerry = "";
   ItemRuntime.Sync(f);
   ForgetItemUsage(f);
   return EffectOutcome.Success;
  }
  // 지난 T에 사용한 나무열매
  static string LastBerry(Fighter f) {
   var fact = (f.Conditions.PrevTurnFacts ?? new List<TriggerFact>()).LastOrDefault(x => x.Kind == "소지품 소실" && x.Self && x.Method == "소모" && ItemRuntime.IsBerry(x.Item));
   return fact == null ? "" : fact.Item;
  }
  static EffectOutcome ReplayBerry(EffectCall c) {
   var f = c.Owner;
   string berry = f == null ? "" : LastBerry(f);
   if (berry.Length == 0 || f.HP <= 0 || !string.IsNullOrEmpty(f.Data.item)) return EffectOutcome.Failed;
   f.Data.item = berry;
   ItemRuntime.Sync(f);
   ForgetItemUsage(f);
   var result = UseNow(c);
   if (result != EffectOutcome.Success && f.Data.item == berry) { f.Data.item = ""; ItemRuntime.Sync(f); }
   return result;
  }

  public static void Register() {
   PotentialExecutors.Register("소지품 약탈", c => Clean(c.Line) == Take, c => Steal(c.Owner, c.Opponent, c.Say));
   PotentialExecutors.Register("소지품 파괴", c => Clean(c.Line) == Break, c => BreakItem(c.Owner, c.Opponent, c.Say));
   PotentialExecutors.Register("소지품 해석", c => Clean(c.Line) == Frisk, c => {
    var other = c.Opponent;
    if (c.Owner == null || other == null || other.HP <= 0) return EffectOutcome.Failed;
    c.Say(c.Owner.Data.name + ": 상대의 소지품은 " + (string.IsNullOrEmpty(other.Data.item) ? "없다" : "「" + other.Data.item + "」이다"));
    return EffectOutcome.Success;
   });
   PotentialExecutors.Register("나무열매 사용", c => Clean(c.Line) == UseBerry, UseNow);
   PotentialExecutors.Register("나무열매 되돌려받기", c => Clean(c.Line) == Recycle, RecycleBerry);
   PotentialExecutors.Register("나무열매 효과 다시 받기", c => Clean(c.Line) == Replay, ReplayBerry);
  }
 }
}
