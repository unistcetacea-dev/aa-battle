// 기술 타입·분류·범위·태그 조건
using System;
using System.Linq;
using System.Globalization;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace AABattle
{
  // 편집기 구조화 효과 모듈의 표준 문장과 엔진 해석을 한곳에 둔다.
  // 수집 원문의 같은 뜻 변형은 EffectModules.Normalize가 표준 문장으로 바꾼다(EffectNormalizer.Normalize가 호출).
  // 실행기와 포텐셜 도감은 표준 문장만 해석한다. 해석하지 못한 문장은 "효과 판정 대기"로 남는다.
  // 기술 조건: 「불꽃/얼음」「물리」「타입 불일치」「효과가 굉장」「단일」「위력 40 이하」「접촉」「기술:베놈쇼크」
  public sealed class MoveCondition
  {
    public static readonly string[] TypeNames =
    {
      "노말",
      "불꽃",
      "물",
      "풀",
      "전기",
      "얼음",
      "격투",
      "독",
      "땅",
      "비행",
      "에스퍼",
      "벌레",
      "바위",
      "고스트",
      "드래곤",
      "악",
      "강철",
      "페어리"
    };
    public List<string> Types = new List<string>();
    public string Category = ""; // 물리 · 특수 · 변화 · 공격(물리+특수)
    public string Stab = ""; // 타입 일치 · 타입 불일치
    public string Effectiveness = ""; // 효과가 굉장(매우 포함) · 효과가 별로(매우 포함) · 효과 보통 · 효과가 굉장 이외
    public string Range = ""; // 단일 · 전체
    public int PowerMax = -1, PowerMin = -1;
    public bool PriorityPositive; // 「우선도 +」 선공기
    public bool PriorityZero; // 「우선도 ±0」 (기술의 원래 우선도가 0)
    public List<string> Tags = new List<string>();
    public List<string> Names = new List<string>();
    [ThreadStatic]
    public static Fighter MatchTarget; // 「자신과 같은 타입」 조건을 판정할 때 기술을 받는 쪽(방호 판정이 지정한다)
    public bool Placeholder; // 빈 괄호 「」·「○」: 타입 자리 표시(실행하지 않음)
    public bool Empty
    {
      get
      {
        return Types.Count == 0 && Category == "" && Stab == "" && Effectiveness == "" && Range == "" && PowerMax < 0 && PowerMin < 0 && !PriorityPositive && !PriorityZero && Tags.Count == 0 && Names.Count == 0 && !Placeholder;
      }
    }

    public string Text
    {
      get
      {
        var parts = new List<string>();
        if (Placeholder)
          parts.Add("「○」");
        if (Types.Count > 0)
          parts.Add("「" + string.Join("/", Types) + "」");
        if (Category.Length > 0)
          parts.Add("「" + Category + "」");
        if (Stab.Length > 0)
          parts.Add("「" + Stab + "」");
        if (Effectiveness.Length > 0)
          parts.Add("「" + Effectiveness + "」");
        if (Range.Length > 0)
          parts.Add("「" + Range + "」");
        if (PowerMax >= 0)
          parts.Add("「위력 " + PowerMax + " 이하」");
        if (PowerMin >= 0)
          parts.Add("「위력 " + PowerMin + " 이상」");
        if (PriorityPositive)
          parts.Add("「우선도 +」");
        if (PriorityZero)
          parts.Add("「우선도 ±0」");
        parts.AddRange(Tags.Select(x => "「" + x + "」"));
        parts.AddRange(Names.Select(x => "「기술:" + x + "」"));
        return string.Concat(parts);
      }
    }

    static string TypeName(string value)
    {
      value = value.Trim();
      if (value == "버레")
        return "벌레";
      if (value == "에스파")
        return "에스퍼";
      return value;
    }

    // 조건 문자열(괄호 토큰 + 허용된 수식어)을 읽는다. 모르는 말이 섞이면 false
    public static bool TryParse(string raw, out MoveCondition condition)
    {
      condition = new MoveCondition();
      string text = (raw ?? "").Trim();
      var tokens = Regex.Matches(text, "「([^」]*)」").Cast<Match>().Select(m => m.Groups[1].Value).ToList();
      string outside = Regex.Replace(text, "「[^」]*」", " ");
      outside = Regex.Replace(outside, @"\s+", " ").Trim();
      foreach (string word in outside.Split(' ').Where(x => x.Length > 0))
      {
        string w = word.Replace(" ", "");
        if (w == "타입불일치인" || w == "타입불일치")
          condition.Stab = "타입 불일치";
        else if (w == "타입일치인" || w == "타입일치")
          condition.Stab = "타입 일치";
        else if (w == "타입" || w == "불일치인" || w == "불일치")
        {
          if (w.StartsWith("불일치"))
            condition.Stab = "타입 불일치";
        }
        else if (new[]
        {
          "한",
          "인",
          "의",
          "등의",
          "효과가",
          "부여된",
          "있는",
          "효과를",
          "가진",
          "효과",
          "계통의",
          "계통"
        }.Contains(w))
        {
        }
        else if (!condition.Apply(w.TrimEnd('의'), false))
          return false;
      }

      if (Regex.IsMatch(outside.Replace(" ", ""), "타입불일치"))
        condition.Stab = "타입 불일치";
      foreach (string token in tokens)
        condition.Apply(token, true);
      return true;
    }

    // 조건 토큰 하나를 반영한다. bracketed=false(괄호 밖 단어)면 태그로 받지 않고 모르는 말이면 false
    public bool Apply(string token, bool bracketed)
    {
      string t = (token ?? "").Trim(), compact = t.Replace(" ", "");
      if (bracketed && (compact.Length == 0 || compact == "○" || compact.Trim('?').Length == 0))
      {
        Placeholder = true;
        return true;
      }

      if (compact.Length == 0)
        return !bracketed;
      if (compact.StartsWith("기술:") && bracketed)
      {
        Names.Add(t.Substring(t.IndexOf(':') + 1).Trim());
        return true;
      }

      string core = compact.EndsWith("기술") && compact.Length > 2 ? compact.Substring(0, compact.Length - 2) : compact;
      var parts = core.Split('/').Select(TypeName).ToArray();
      if (parts.All(x => TypeNames.Contains(x)))
      {
        foreach (var x in parts)
          if (!Types.Contains(x))
            Types.Add(x);
        return true;
      }

      if (core == "물리")
      {
        Category = "물리";
        return true;
      }

      if (core == "특수")
      {
        Category = "특수";
        return true;
      }

      if (core == "변화")
      {
        Category = "변화";
        return true;
      }

      if (core == "공격")
      {
        Category = "공격";
        return true;
      }

      if (core == "타입일치")
      {
        Stab = "타입 일치";
        return true;
      }

      if (core == "타입불일치")
      {
        Stab = "타입 불일치";
        return true;
      }

      if (core.StartsWith("효과"))
      {
        if (core.Contains("굉장") && core.EndsWith("이외"))
        {
          Effectiveness = "효과가 굉장 이외";
          return true;
        }

        if (core.Contains("굉장"))
        {
          Effectiveness = "효과가 굉장";
          return true;
        }

        if (core.Contains("별로"))
        {
          Effectiveness = "효과가 별로";
          return true;
        }

        if (core.Contains("보통"))
        {
          Effectiveness = "효과 보통";
          return true;
        }
      }

      if (core == "단일" || core == "단일공격")
      {
        Range = "단일";
        if (core == "단일공격")
          Category = "공격";
        return true;
      }

      if (core == "전체" || core == "전체공격")
      {
        Range = "전체";
        if (core == "전체공격")
          Category = "공격";
        return true;
      }

      var power = Regex.Match(core, @"^위력:?(?<n>\d+)(?<dir>이하|이상)$");
      if (power.Success)
      {
        int n = int.Parse(power.Groups["n"].Value);
        if (power.Groups["dir"].Value == "이하")
          PowerMax = n;
        else
          PowerMin = n;
        return true;
      }

      if (core == "우선도+" || core == "우선도가+" || core == "선공")
      {
        PriorityPositive = true;
        return true;
      }

      if (core == "우선도±0" || core == "우선도0" || core == "우선도+-0")
      {
        PriorityZero = true;
        return true;
      }

      if (!bracketed)
        return false;
      if (core == "자신과같은타입")
        core = "자신과 같은 타입"; // 방호 판정이 기술을 받는 쪽의 타입으로 판정한다
      Tags.Add(core);
      return true;
    }

    // 기술·사용자·실제 상성 배율로 조건을 판정한다
    public bool Matches(Move move, Fighter user, double typeEffect)
    {
      if (move == null || Placeholder)
        return false;
      var types = move.types ?? new string[0];
      if (Types.Count > 0 && !types.Any(Types.Contains))
        return false;
      if (Category == "공격" && move.category == "변화")
        return false;
      if (Category.Length > 0 && Category != "공격" && move.category != Category)
        return false;
      bool stab = user != null && types.Any(t => (user.Data.types ?? new string[0]).Contains(t));
      if (Stab == "타입 일치" && !stab)
        return false;
      if (Stab == "타입 불일치" && stab)
        return false;
      if (Effectiveness == "효과가 굉장" && !(typeEffect > 1))
        return false;
      if (Effectiveness == "효과가 별로" && !(typeEffect > 0 && typeEffect < 1))
        return false;
      if (Effectiveness == "효과가 굉장 이외" && typeEffect > 1)
        return false;
      if (Effectiveness == "효과 보통" && typeEffect != 1)
        return false;
      bool spread = (move.target ?? "").Contains("전체") || (move.tags ?? new string[0]).Contains("전체");
      if (Range == "전체" && !spread)
        return false;
      if (Range == "단일" && spread)
        return false;
      if (PowerMax >= 0 && !(move.power > 0 && move.power <= PowerMax))
        return false;
      if (PowerMin >= 0 && move.power < PowerMin)
        return false;
      if (PriorityPositive && move.priority <= 0)
        return false;
      if (PriorityZero && move.priority != 0)
        return false;
      // 「물기」「엄니」처럼 계통·이름 괄호가 여럿이면 하나라도 맞으면 된다. 「접촉」은 최종 접촉 판정
      if (Tags.Count > 0 && !Tags.Any(tag => tag == "접촉" ? Mechanics.Contact(move) : tag == "비접촉" ? !Mechanics.Contact(move) : tag == "자신과 같은 타입" ? MatchTarget != null && (move.types ?? new string[0]).Any(t => Mechanics.Has(MatchTarget, t)) : tag == "기능확장" ? move.extension : (move.tags ?? new string[0]).Contains(tag) || (move.effect ?? "").Contains(tag) || move.name == tag))
        return false;
      // 「기술:테크노버스터」는 이름 뒤에 （1P）처럼 구분이 붙은 변형도 같은 기술로 본다
      if (Names.Count > 0 && !Names.Any(name => name == move.name || (move.name ?? "").StartsWith(name + "（")))
        return false;
      return true;
    }
  }
}
