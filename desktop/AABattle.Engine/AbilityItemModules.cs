using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace AABattle {
 // 소지품의 보유와 사용 가능 여부는 별개다.
 public static class AbilityItemModules {
  public const string Take = "상대의 소지품을 빼앗는다";
  public const string NoItems = "자신은 소지품을 사용할 수 없다";
  public const string NoConsumedItems = "상대는 소비하는 소지품을 사용할 수 없다";
  public const string ProtectItem = "자신의 소지품은 파괴되거나 약탈되지 않는다";
  static string Clean(string text) {
   return Regex.Replace(PotentialParts.Clean(text ?? ""), @"\s+", " ").Trim().TrimEnd('.', '。', '．');
  }
  public static string Normalize(string text) {
   string s = Clean(text);
   if (s == Take || s == "상대의 소지품을 파괴(약탈)한다") return Take + ".";
   if (s == NoItems || s == "자신의 소지품을 사용할 수 없다") return NoItems + ".";
   if (s == NoConsumedItems || s == "전투 중 상대는 소비하는 소지품을 사용할 수 없다") return NoConsumedItems + ".";
   if (s == ProtectItem || s == "자신의 소지품을 파괴(약탈)할 수 없다") return ProtectItem + ".";
   return null;
  }
  public static string Classify(string text) {
   string s = Clean(text);
   if (s == Take) return "소지품 약탈";
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
  public static EffectOutcome Steal(Fighter owner, Fighter other, Action<string> log) {
   if (owner == null || other == null || owner.HP <= 0 || other.HP <= 0 || string.IsNullOrEmpty(other.Data.item)) return EffectOutcome.Failed;
   if (ItemProtected(other, owner)) {
    if (log != null) log(other.Data.name + ": 소지품 파괴·약탈을 무효화했다");
    return EffectOutcome.Failed;
   }
   if (string.IsNullOrEmpty(owner.Data.item)) return ItemRuntime.Transfer(other, owner, log, true) ? EffectOutcome.Success : EffectOutcome.Failed;
   ItemRuntime.Lose(other, "파괴", log);
   return EffectOutcome.Success;
  }
  public static void Register() {
   PotentialExecutors.Register("소지품 약탈", c => Clean(c.Line) == Take, c => Steal(c.Owner, c.Opponent, c.Say));
  }
 }
}
