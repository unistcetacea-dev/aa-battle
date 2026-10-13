using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace AABattle {
 // 남은 특성 모듈화 4차의 트리거 모듈(사건). 포텐셜에서도 쓴다.
 //  「작은」 이하의 대미지를 받았을 때      받은 대미지가 최대 HP의 1/6 이하(오레마스 시스템 「데미지 표기」: 작은 = 1/8~1/6, 미미한 포함). 1/6 초과는 성립하지 않는다.
 //  상대에 의한 능력 하락을 무효화했을 때   능력 하락 무효 판정에서 낸다(독연기).
 //  상대의 〔위협〕을 받았을 때            〔위협〕에 의한 하락을 무효화하는 문장이 있어 하락이 막힌 경우에 낸다(파수견).
 //  체력이 가득인 상태로 공격을 받았을 때   피격 순간의 체력이 가득(멀티스케일과 같은 시점)이고 대미지를 받았을 때(플라즈마가드).
 public static partial class TriggerModules {
  const string SmallDamageLabel = "「작은」 이하의 대미지를 받았을 때";
  const string DropNullifiedLabel = "상대에 의한 능력 하락을 무효화했을 때";
  const string IntimidateLabel = "상대의 〔위협〕을 받았을 때";
  const string FullHitLabel = "체력이 가득인 상태로 공격을 받았을 때";

  // 「작은」 이하 = 최대 HP의 1/6 이하(사용자 결정)
  public const int SmallDamageDenominator = 6;

  static bool TryParseRemaining4(string t, out TriggerSpec spec) {
   spec = null;
   string s = Regex.Replace(t ?? "", @"\s+", " ").Trim();
   if (Regex.IsMatch(s, @"^「작은」 ?이하의 (?:대미지|데미지)를 받았을 때$")) spec = new TriggerSpec { Module = "작은 대미지" };
   else if (s == DropNullifiedLabel) spec = new TriggerSpec { Module = "하락 무효" };
   else if (Regex.IsMatch(s, @"^상대의 [〔「]위협[〕」]을 받았을 때$")) spec = new TriggerSpec { Module = "위협 받음" };
   else if (s == FullHitLabel) spec = new TriggerSpec { Module = "가득 피격" };
   return spec != null;
  }

  static string CanonicalRemaining4(TriggerSpec s) {
   switch (s.Module) {
    case "작은 대미지": return SmallDamageLabel;
    case "하락 무효": return DropNullifiedLabel;
    case "위협 받음": return IntimidateLabel;
    case "가득 피격": return FullHitLabel;
   }
   return "";
  }

  static bool MatchesRemaining4(TriggerSpec s, TriggerFact f) {
   switch (s.Module) {
    case "작은 대미지":
     return f.Kind == "체력" && f.Self && f.Max > 0 && f.After2 < f.Before && (long)(f.Before - f.After2) * SmallDamageDenominator <= f.Max;
    case "하락 무효": return f.Kind == "하락 무효" && f.Self;
    case "위협 받음": return f.Kind == "위협 받음" && f.Self;
    case "가득 피격": return f.Kind == "명중" && f.Received && f.Damaged && f.WasFull && f.Move != null && f.Move.category != "변화";
   }
   return false;
  }
 }
}
