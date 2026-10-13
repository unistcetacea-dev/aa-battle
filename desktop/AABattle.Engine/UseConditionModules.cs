// 포텐셜 사용 조건 수식문(「이 포텐셜은 …할 수 있다」)과 본문 앞처리.
// 수식문은 트리거도 효과도 아니고 포텐셜 한 개의 속성이다. 본문에서 떼어 내어 플래그로 읽는다.
// 연결: PotentialLibrary.Split(본문 앞처리) · TriggerStructures.Clauses가 쓰는 같은 Split을 거친다
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace AABattle
{
  // 포텐셜 하나에 붙은 사용 조건 수식
  public sealed class UseConditions
  {
    public bool OffField;        // 자신이 필드에 없어도 발동할 수 있다
    public bool PartyWide;       // PT의 전원이 사용할 수 있다
    public bool SwitchTurnLast;  // 통상교대로 나온 T의 최후에도 발동할 수 있다
    public bool OnHit;           // 공격 명중시에 사용할 수 있다(트리거를 대신한다)
    // 「이 포텐셜은 「능력:A」 이하일 때는 발동하지 않는다」: 트레이너 자질 등급 제한(자질 이름이 비면 없음)
    public string BlockAptitude = "", BlockRank = "";
    public bool BlockAtMost, BlockOriginal;   // 이하/이상, 원래 등급(임시 변화를 무시) 기준

    public bool Any
    {
      get
      {
        return OffField || PartyWide || SwitchTurnLast || OnHit || BlockAptitude.Length > 0;
      }
    }
  }

  public static class UseConditionModules
  {
    public const string OffFieldSentence = "이 포텐셜은 자신이 필드에 없어도 발동할 수 있다";
    public const string PartyWideSentence = "이 포텐셜은 PT의 전원이 사용할 수 있다";
    public const string SwitchTurnLastSentence = "이 포텐셜은 통상교대로 나온 T의 최후에도 발동할 수 있다";
    public const string OnHitSentence = "이 포텐셜은 공격 명중시에 사용할 수 있다";
    // 공격 명중 확정 트리거의 표준 문장(명중 확정 모듈)
    public const string OnHitTrigger = "자신의 공격이 명중하면";

    static readonly Regex Lead = new Regex(@"^이\s*(?:포텐셜|퍼텐셜)은\s*(?<rest>.+)$");
    static readonly Regex OffField = new Regex(@"^자신이\s*(?:필드|장소)에\s*(?:없어도|있지\s*않아도)\s*(?:사용|발동)(?:할\s*수\s*있다|한다)$");
    static readonly Regex PartyWide = new Regex(@"^PT의\s*(?:전원|모두)(?:이|가)\s*(?:사용|발동)할\s*수\s*있다$");
    static readonly Regex SwitchLast = new Regex(@"^통상교대로\s*(?:장소에\s*|필드에\s*)?나온\s*T의\s*최후에도\s*(?:사용|발동)할\s*수\s*있다$");
    static readonly Regex BlockRule = new Regex(@"^(?:(?<o>원래)\s*)?「(?<a>지시|육성|통솔|능력)(?:\s*:\s*(?<r1>[A-Za-z]+)[-+]?」|」(?:이|가)\s*「(?<r2>[A-Za-z]+)[-+]?」)\s*(?<d>이하|이상)일 때는 발동하지 않는다$");
    static readonly Regex OnHitRule = new Regex(@"^공격\s*명중\s*시에?\s*(?:사용|발동)할\s*수\s*있다$");
    // 시트 장식: 상자선 줄, 색 태그
    static readonly Regex Art = new Regex(@"(?:\s*/\s*|\s*[\r\n]+\s*|^\s*)[─━═┄┈\-]{4,}[─━═┄┈\-\s]*");
    static readonly Regex ColorTag = new Regex(@"</?clr[^>]*>");

    // 수식문 한 문장을 읽는다. 맞지 않으면 false
    public static bool TryRead(string sentence, UseConditions into)
    {
      string text = Regex.Replace(PotentialParts.Clean(sentence ?? ""), @"\s+", " ").Trim().TrimEnd('.', '。', '．').Trim();
      var lead = Lead.Match(text);
      if (!lead.Success)
        return false;
      string rest = lead.Groups["rest"].Value;
      if (OffField.IsMatch(rest))
        into.OffField = true;
      else if (PartyWide.IsMatch(rest))
        into.PartyWide = true;
      else if (SwitchLast.IsMatch(rest))
        into.SwitchTurnLast = true;
      else if (OnHitRule.IsMatch(rest))
        into.OnHit = true;
      else if (BlockRule.IsMatch(rest))
      {
        var block = BlockRule.Match(rest);
        into.BlockAptitude = block.Groups["a"].Value;
        into.BlockRank = (block.Groups["r1"].Success ? block.Groups["r1"].Value : block.Groups["r2"].Value).ToUpperInvariant();
        into.BlockAtMost = block.Groups["d"].Value == "이하";
        into.BlockOriginal = block.Groups["o"].Success;
      }
      else
        return false;
      return true;
    }

    // 본문 앞처리: 장식 제거 → 수식문 분리 → 변형 문장을 표준형으로. 수식문은 conditions에 담긴다
    public static string Prepare(string body, out UseConditions conditions)
    {
      conditions = new UseConditions();
      string text = body ?? "";
      if (text.Length == 0)
        return text;
      string original = text;
      text = ColorTag.Replace(text, "");
      text = Art.Replace(text, " ");
      var kept = new List<string>();
      bool changed = !ReferenceEquals(text, original) && text != original;
      foreach (string sentence in Regex.Split(text.Trim(), @"(?<=[.!?。])\s+"))
      {
        if (sentence.Trim().Length == 0)
          continue;
        if (TryRead(sentence, conditions))
        {
          changed = true;
          continue;
        }

        string variant = Variant(sentence.Trim());
        changed = changed || variant != sentence.Trim();
        kept.Add(variant);
      }

      // 바꿀 것이 없으면 원문을 그대로 돌려준다(줄바꿈 보존)
      return changed ? string.Join(" ", kept).Trim() : original;
    }

    // 포텐셜 본문에 붙은 수식(없으면 빈 수식)
    public static UseConditions Of(string description)
    {
      UseConditions conditions = new UseConditions();
      string text = description ?? "";
      if (text.Contains("포텐셜은") || text.Contains("퍼텐셜은"))
        Prepare(text, out conditions);
      return conditions;
    }

    static readonly string[] Bands = { "E", "D", "C", "B", "A", "AA", "AAA", "S" };

    // 등급 제한이 있으면 지금 트레이너가 그 제한에 걸리는지 본다. 트레이너·등급을 알 수 없으면 막지 않는다
    public static bool Allows(string description, Fighter owner, EffectScope scope)
    {
      var conditions = Of(description);
      if (conditions.BlockAptitude.Length == 0 || owner == null || scope == null || scope.States == null)
        return true;
      int side = Array.IndexOf(scope.Fighters, owner);
      if (side < 0 || side >= scope.States.Length || scope.States[side] == null || scope.States[side].Trainer == null)
        return true;
      var state = conditions.BlockOriginal ? null : scope.States[side];
      var trainer = scope.States[side].Trainer;
      string name = conditions.BlockAptitude;
      if (!TrainerAccessRules.TrainerAtLeast(trainer, name, "E", state))
        return true;
      int index = Array.IndexOf(Bands, conditions.BlockRank);
      if (index < 0)
        return true;
      bool blocked;
      if (conditions.BlockAtMost)
        blocked = index + 1 >= Bands.Length || !TrainerAccessRules.TrainerAtLeast(trainer, name, Bands[index + 1], state);
      else
        blocked = TrainerAccessRules.TrainerAtLeast(trainer, name, conditions.BlockRank, state);
      return !blocked;
    }

    // 그 포텐셜이 트리거를 갖지 않을 때 수식문이 대신하는 트리거 문장
    public static string ImpliedTrigger(UseConditions conditions)
    {
      return conditions != null && conditions.OnHit ? OnHitTrigger : "";
    }

    // 효과 목록에서 뺄 문장: 머리말(「○○ 전용 포텐셜.」)·서포트 표시·(※ …) 주석
    public static bool IsNote(string sentence)
    {
      string s = Regex.Replace(PotentialParts.Clean(sentence ?? ""), @"\s+", " ").Trim();
      return Regex.IsMatch(s, @"^(?:《[^》]*》\s*)?[^.]{0,30}?(?:전용|고유)\s*(?:포텐셜|퍼텐셜)\.?$")
        || Regex.IsMatch(s, @"^[^.]{0,20}서포트 효과\.?$")
        || Regex.IsMatch(s, @"^[(（]\s*※.*[)）]\.?$")
        || Regex.IsMatch(s, @"^(?:「[^」]*」종의 정점에 있는 『고유종』|원종인 「[^」]*」종과는 다른 생태를 갖는다|.{0,20}혼혈인 『변종』)\.?$");
    }

    // 변형 문장을 문장 단위 표준형으로 바꾼다(트리거 분리 전에 필요한 것만)
    static string Variant(string sentence)
    {
      string s = sentence;
      // 짝이 없는 닫는 괄호는 시트 표기 실수다: 「…오른다.)」
      if (s.Count(c => c == ')' || c == '）') > s.Count(c => c == '(' || c == '（'))
        s = Regex.Replace(s, @"[)）]+$", "").TrimEnd();
      // 「빈사로 할 수 없다」 계열 → 앞 대미지 문장에 이어 붙는 표준 문장
      s = Regex.Replace(s, @"^(?:이\s*대미지로는\s*)?「?빈사」?(?:로는|로)\s*(?:할\s*수\s*없다)\.?$", "이 대미지로는 빈사로 할 수 없다.");
      // 「필드에 나와 2T간 …」 → 등장 사건 + 기간 효과
      var span = Regex.Match(s, @"^필드에 나와\s*(?<n>[0-9０-９]+)\s*T\s*간\s*(?<rest>.+)$");
      if (span.Success)
        s = "필드에 나올 때, " + span.Groups["n"].Value + "T간 " + span.Groups["rest"].Value;
      // 「데이터가 해석된 상대에 대해서, …」 → 해석 상태 조건 + 효과
      var analyzed = Regex.Match(s, @"^데이터가 해석된 상대에 대해서,?\s*(?<rest>.+)$");
      if (analyzed.Success)
        s = "상대의 데이터가 해석된 상태일 때, " + analyzed.Groups["rest"].Value;
      // 「죽어내밀기」로 나온 상대에게 … → 사건 + 효과
      var forced = Regex.Match(s, @"^「죽어내밀기」로 나온 상대에게\s*(?<rest>.+)$");
      if (forced.Success)
        s = "「죽어내밀기」로 상대가 나왔을 때, 상대에게 " + forced.Groups["rest"].Value;
      return s;
    }
  }
}
