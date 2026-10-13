// 수집 효과 문장의 표준화
using System;
using System.Linq;
using System.Globalization;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace AABattle
{
  public static partial class EffectModules
  {
    static string NormalizeBody(string text)
    {
      Match m;
      string action;
      double rate;
      MoveCondition c;
      string chaldea = ChaldeaModules.Normalize(text);
      if (chaldea != null)
        return chaldea;
      m = Regex.Match(text, @"^자신은\s*「(?<s>[^」]+)」\s*상태가\s*된다$");
      if (m.Success && StateEffects.Find(m.Groups["s"].Value) != null)
        return "자신을 「" + m.Groups["s"].Value + "」 상태로 한다";
      // 「상대 기술의」처럼 조사가 빠진 주어
      string moveModule = MoveModules.Normalize(text);
      string batonModule = BatonModules.Normalize(text) ?? PursuitModules.Normalize(text) ?? MoveMiscModules.Normalize(text) ?? BondModules.Normalize(text) ?? ExclusiveModules.Normalize(text) ?? PotentialNullify.Normalize(text);
      if (batonModule != null)
        return batonModule;
      if (moveModule != null)
        return moveModule;
      string criticalModule = AbilityCriticalFieldModules.Normalize(text);
      if (criticalModule != null)
        return criticalModule;
      string itemAbility = AbilityItemModules.Normalize(text);
      if (itemAbility != null)
        return itemAbility;
      string noEffect = AbilityNoEffectModules.Normalize(text);
      if (noEffect != null)
        return noEffect;
      string abilityMove = AbilityMoveModules.Normalize(text);
      if (abilityMove != null)
        return abilityMove;
      string abilityState = AbilityStateModules.Normalize(text);
      if (abilityState != null)
        return abilityState;
      string abilityEvent = AbilityEventModules.Normalize(text);
      if (abilityEvent != null)
        return abilityEvent;
      string changeModule = AbilityChangeModules.Normalize(text);
      if (changeModule != null)
        return changeModule;
      string abilityModule = AbilityPhrases.Normalize(text);
      if (abilityModule != null)
        return abilityModule;
      text = Regex.Replace(text, @"^(자신|상대)\s+(?=기술의|「)", "$1의 ");
      // 「기술의 우선도를 +N한다」 → 표준 문장 「기술 우선도를 N 올린다」 (대의 선 등)
      m = Regex.Match(text, @"^(?<t>자신|상대|아군)의\s*기술의?\s*우선도를\s*[+＋]\s*(?<n>[1-7])\s*(?:한다|올린다)$");
      if (m.Success)
        return EffectNormalizer.CanonicalPriority(m.Groups["t"].Value, int.Parse(m.Groups["n"].Value)).TrimEnd('.');
      // 「X」의 위력/대미지(괄호 하나가 기술 조건)
      m = Regex.Match(text, "^" + Subject + @"의\s*(?<c>「[^」]+」|공격|물리기술|특수기술)의\s*(?<k>위력|대미지|데미지)(?:을|를|이)\s*(?<v>.+)$");
      if (m.Success && TryVerb(m.Groups["v"].Value, out action, out rate) && MoveCondition.TryParse(m.Groups["c"].Value.StartsWith("「") ? m.Groups["c"].Value : "「" + m.Groups["c"].Value + "」", out c))
      {
        string s = Subj(m.Groups["s"].Value);
        return m.Groups["k"].Value == "위력" ? Scalar(s + "의 " + Cond(c) + "기술의 위력", action, rate) : Scalar(s + SubjectParticle(s) + " 주는 " + c.Text + " 기술의 대미지", action, rate);
      }

      // 상대의 (조건)기술로 받는 대미지 → 자신이 받는
      m = Regex.Match(text, @"^상대의\s*(?<c>.*?)\s*기술로\s*받는\s*(?:대미지|데미지)(?:을|를)\s*(?<v>.+)$");
      if (m.Success && TryVerb(m.Groups["v"].Value, out action, out rate) && MoveCondition.TryParse(m.Groups["c"].Value, out c))
        return Scalar("자신이 받는 " + (c.Empty ? "대미지" : c.Text + " 기술의 대미지"), action, rate);
      // 능력치 배율(괄호 없는 능력 이름·주어 생략·명중/회피 포함)
      m = Regex.Match(text, "^(?:" + Subject + @"의\s*)?(?<st>「[^」]+」|전능력치|가장 높은 능력|가장 낮은 능력|공격|방어|특공|특방|속도|명중|회피)(?:을|를)\s*(?<v>(?:강화|약화|저하)\([0-9.]+배\)(?:한다|시킨다))$");
      if (m.Success && TryVerb(m.Groups["v"].Value, out action, out rate))
      {
        string st = m.Groups["st"].Value;
        if (st.StartsWith("「") && !AllStats(st.Trim('「', '」')))
          st = null;
        else if (StatWords.Contains(st))
          st = "「" + st + "」";
        if (st != null)
          return Scalar(Subj(m.Groups["s"].Value) + "의 " + st, action, rate);
      }

      // 능력 랭크(주어 생략: 오르면 자신, 내리면 상대)
      m = Regex.Match(text, @"^랜덤(?:으로|하게)\s*(?<s>자신|상대)의\s*능력(?:치)?(?:을|를)\s*(?<d>매우 크게|크게)?\s*(?<dir>올린다|내린다)$");
      if (m.Success)
        return EffectNormalizer.CanonicalRank(m.Groups["s"].Value + "의 랜덤한 능력치", Amount(m.Groups["d"].Value, m.Groups["dir"].Value));
      m = Regex.Match(text, "^(?:" + Subject + @"의\s*)?(?<what>「[^」]+」|임의의 능력|랜덤한 능력치|가장 높은 능력|가장 낮은 능력|전능력치|공격|방어|특공|특방|속도|명중|회피)(?:이|가|을|를)\s*(?<d>매우 크게|크게)?\s*(?<dir>오른다|올린다|내린다)$");
      if (m.Success && (!m.Groups["what"].Value.StartsWith("「") || AllStats(m.Groups["what"].Value.Trim('「', '」'))))
      {
        string dir = m.Groups["dir"].Value;
        string s = m.Groups["s"].Success && m.Groups["s"].Value.Length > 0 ? Subj(m.Groups["s"].Value) : dir == "내린다" ? "상대" : "자신";
        string what = StatWords.Contains(m.Groups["what"].Value) ? "「" + m.Groups["what"].Value + "」" : m.Groups["what"].Value;
        return EffectNormalizer.CanonicalRank(s + "의 " + what, Amount(m.Groups["d"].Value, dir));
      }

      // 위력
      m = Regex.Match(text, "^(?:" + Subject + @"의\s*)?(?<c>.*?)\s*기술의\s*위력(?:을|이)\s*(?<v>.+)$");
      if (m.Success && TryVerb(m.Groups["v"].Value, out action, out rate) && MoveCondition.TryParse(m.Groups["c"].Value, out c))
        return Scalar(Subj(m.Groups["s"].Value) + "의 " + Cond(c) + "기술의 위력", action, rate);
      m = Regex.Match(text, "^" + Subject + @"의\s*(?<c>「[^」]+」)의\s*위력(?:을|이)\s*(?<v>.+)$");
      if (m.Success && TryVerb(m.Groups["v"].Value, out action, out rate) && MoveCondition.TryParse(m.Groups["c"].Value, out c))
        return Scalar(Subj(m.Groups["s"].Value) + "의 " + Cond(c) + "기술의 위력", action, rate);
      // 주는 대미지: 「자신/상대의 (조건) 기술의 대미지」
      m = Regex.Match(text, "^" + Subject + @"의\s*(?<c>.*?)\s*기술의\s*(?:대미지|데미지)(?:을|를|가|이)\s*(?<v>.+)$");
      if (m.Success && TryVerb(m.Groups["v"].Value, out action, out rate) && MoveCondition.TryParse(m.Groups["c"].Value, out c))
      {
        string s = Subj(m.Groups["s"].Value);
        return Scalar(s + SubjectParticle(s) + " 주는 " + (c.Empty ? "대미지" : c.Text + " 기술의 대미지"), action, rate);
      }

      // 받는 대미지: 「(조건) 기술로 받는 대미지」
      m = Regex.Match(text, "^(?:" + Subject + @"(?:이|가)\s*)?(?<c>.*?)\s*기술로\s*받는\s*(?:대미지|데미지)(?:을|를)\s*(?<v>.+)$");
      if (m.Success && TryVerb(m.Groups["v"].Value, out action, out rate) && MoveCondition.TryParse(m.Groups["c"].Value, out c))
      {
        string s = Subj(m.Groups["s"].Value);
        return Scalar(s + SubjectParticle(s) + " 받는 " + (c.Empty ? "대미지" : c.Text + " 기술의 대미지"), action, rate);
      }

      // 명중
      m = Regex.Match(text, "^" + Subject + @"의\s*(?<c>.*?)\s*기술의\s*명중률?(?:을|를|이)\s*(?<v>.+)$");
      if (m.Success && TryVerb(m.Groups["v"].Value, out action, out rate) && MoveCondition.TryParse(m.Groups["c"].Value, out c))
        return Scalar(Subj(m.Groups["s"].Value) + "의 " + Cond(c) + "기술의 명중", action, rate);
      // 능력치 배율의 「N배로 한다 / N배가 된다」
      m = Regex.Match(text, "^(?:" + Subject + @"의\s*)?(?<st>「[^」]+」|전능력치|공격|방어|특공|특방|속도)(?:을|를|이|가)\s*(?<v>" + Rate + @"배(?:로 한다|가 된다))$");
      if (m.Success && TryVerb(m.Groups["v"].Value, out action, out rate) && (m.Groups["st"].Value == "전능력치" || StatList(m.Groups["st"].Value.Trim('「', '」')) != null))
      {
        string st = m.Groups["st"].Value.StartsWith("「") || m.Groups["st"].Value == "전능력치" ? m.Groups["st"].Value : "「" + m.Groups["st"].Value + "」";
        return Scalar(Subj(m.Groups["s"].Value) + "의 " + st, action, rate);
      }

      // F4b 타입·상성
      string secondary = SecondaryEffects.Normalize(text);
      if (secondary != null)
        return secondary;
      string misc = MiscModules.Normalize(text);
      if (misc != null)
        return misc;
      string cure = StateCure.Normalize(text);
      if (cure != null)
        return cure;
      string extension = FunctionExtensions.Normalize(text);
      if (extension != null)
        return extension;
      string typed = TypeRules.Normalize(text);
      if (typed != null)
        return typed;
      // G 방호·회피·관통
      string guard = NormalizeGuard(text);
      if (guard != null)
        return guard;
      // F4b 추가행동
      string extra = NormalizeExtra(text);
      if (extra != null)
        return extra;
      // F4a 장소·교대 모듈
      string place = NormalizePlace(text);
      if (place != null)
        return place;
      // 종족치
      string species = NormalizeSpecies(text);
      if (species != null)
        return species;
      // 최대 HP의 1/N
      string fraction = NormalizeFraction(text);
      if (fraction != null)
        return fraction;
      // 주어 없는 능력 랭크: 「「공격」이 오른다」 → 자신
      m = Regex.Match(text, @"^(?<st>「[^」]+」)(?:이|가)\s*(?<d>(?:매우\s*)?크게\s*)?오른다$");
      if (m.Success && StatList(m.Groups["st"].Value.Trim('「', '」')) != null)
        return "자신의 " + m.Groups["st"].Value + "이 " + m.Groups["d"].Value.Trim() + (m.Groups["d"].Value.Trim().Length > 0 ? " " : "") + "오른다.";
      return null;
    }

    public static string NormalizeGuard(string clean)
    {
      var activation = Activation.Match((clean ?? "").Trim());
      if (activation.Success)
      {
        string inner = NormalizeGuard(clean.Trim().Substring(activation.Length));
        return inner == null ? null : activation.Groups[1].Value + " " + inner;
      }

      string text = Prepare(clean);
      string prefix = "";
      var duration = Regex.Match(text, @"^(?:(?<n>\d+)\s*T\s*(?:간|동안)|(?<end>T\s*종료\s*시\s*까지))\s*,?\s*");
      if (duration.Success)
      {
        prefix = duration.Groups["end"].Success ? "T종료시까지 " : duration.Groups["n"].Value + "T동안 ";
        text = text.Substring(duration.Length).Trim();
      }

      if (Regex.IsMatch(text, @"^(?:자신에\s*대한|상대로부터의|상대의)\s*(?:「데이터\s*해석」|데이터\s*해석)을\s*무효화한다$"))
        return prefix + "자신에 대한 데이터 해석을 무효화한다.";
      var pierce = Regex.Match(text, @"^(?:자신의\s*(?<c>(?:「[^」]+」\s*)*)\s*기술이\s*)?상대(?:의)?\s*(?<tr>트레이너의?\s*)?(?:『방호\s*포텐셜』|방호\s*포텐셜)을\s*(?:무시|무효화|관통)한다$");
      if (pierce.Success)
      {
        MoveCondition pc;
        if (!MoveCondition.TryParse(pierce.Groups["c"].Value, out pc))
          return null;
        return prefix + "자신의 " + (pc.Empty ? "" : pc.Text + " ") + "기술이 상대의 " + (pierce.Groups["tr"].Success ? "트레이너 " : "") + "방호 포텐셜을 무시한다.";
      }

      string probability = "";
      string body;
      string label;
      if (EffectNormalizer.TryProbabilityPrefix(text, out label, out body))
      {
        probability = label == "드물게" ? "드물게" : label + "로";
        text = body.Trim();
      }

      var m = Regex.Match(text, @"^(?:상대(?:의)?\s*)?(?<c>.*?)\s*(?<o>기술|공격)?\s*(?:을|를)\s*(?<p>드물게|저확률로|중확률로)?\s*(?<v>무효화|회피|반사)한다$");
      if (!m.Success)
        return null;
      string raw = m.Groups["c"].Value.Trim();
      if (!m.Groups["o"].Success && !raw.Contains("기술") && !Regex.IsMatch(raw, "^(?:「[^」]*」)+$"))
        return null;
      if (raw.Contains("「데이터") || raw.Contains("포텐셜") || raw.Contains("『"))
        return null;
      MoveCondition c;
      string tokens = Regex.Replace(raw, @"(?<=」)\s*기술$|(?<=」)(?:의)?$", "");
      if (!MoveCondition.TryParse(tokens, out c))
        return null;
      if (m.Groups["o"].Value == "공격" && c.Category.Length == 0)
        c.Category = "공격";
      if (c.Tags.Any(t => t.Contains("데이터") || t.Contains("해석") || t.Contains("추가행동") || t.Contains("포텐셜")))
        return null;
      if (m.Groups["p"].Success)
        probability = m.Groups["p"].Value;
      return prefix + PotentialProtection.Canonical(c, probability, m.Groups["v"].Value);
    }

    const string ExtraPosition = @"가장\s*먼저|가장\s*마지막에|제일\s*마지막에|T의\s*(?:최후|마지막)에|턴의\s*(?:최후|마지막)에|최후에";

    // 「X」(또는 「기능확장:X」)에 의한 추가행동 → 기술 이름 조건의 추가행동. 일반 단어 「X」는 기술 이름일 때만 이름으로 본다
    static string NormalizeExtraByName(string t)
    {
      var m = Regex.Match(t, @"^(?:「?(?<n>\d+)」?\s*(?:의\s*)?배수\s*(?:의\s*)?T\s*,?\s*)?(?:(?<pos>" + ExtraPosition + @")\s*,?\s*)?「(?<ext>기능\s*확장\s*[:：(（]\s*)?(?<name>[^」()（）:：]+?)\s*[)）]?」\s*(?:기술\s*)?(?:에\s*의한|로)\s*추가행동을\s*얻는다$");
      if (!m.Success)
        return null;
      string name = m.Groups["name"].Value.Trim();
      if (!m.Groups["ext"].Success && !MoveNames.Known(name))
        return null;
      var c = new MoveCondition();
      c.Names.Add(name);
      int every = m.Groups["n"].Success ? int.Parse(m.Groups["n"].Value) : 0;
      return ExtraActionRuntime.Canonical(every, m.Groups["pos"].Value.Contains("먼저"), c, false);
    }

    // 추가공격 = 방금 내보낸 공격을 한 번 더(사용자 결정). 「X」를 내보낸다 = 그 기술의 추가행동
    static string NormalizeExtraRepeat(string t)
    {
      var again = Regex.Match(t, @"^(?:공격\s*명중\s*후\s*,?\s*)?(?:(?<pos>" + ExtraPosition + @")\s*,?\s*)?추가공격을\s*(?:할\s*수\s*있다|내보낸다|한다)$");
      if (again.Success)
        return ExtraActionRuntime.Canonical(0, !LastPosition(again.Groups["pos"].Value), new MoveCondition(), true);
      var send = Regex.Match(t, @"^(?:(?<pos>" + ExtraPosition + @")\s*,?\s*)?「(?<name>[^」]+)」(?:을|를)\s*내보낸다$");
      if (send.Success && MoveNames.Known(send.Groups["name"].Value.Trim()))
      {
        var c = new MoveCondition();
        c.Names.Add(send.Groups["name"].Value.Trim());
        return ExtraActionRuntime.Canonical(0, !LastPosition(send.Groups["pos"].Value), c, false);
      }

      return null;
    }

    static bool LastPosition(string position)
    {
      return position.Contains("마지막") || position.Contains("최후");
    }

    // 추가행동 변형 → 「「N」배수 T, 가장 먼저/마지막에 「조건」 기술에 의한 추가행동을 얻는다」. 지령에 의한 추가행동권은 다른 규칙이라 바꾸지 않는다
    static string NormalizeExtra(string text)
    {
      string t = Regex.Replace(text, @"추가\s*행동권?", "추가행동");
      t = Regex.Replace(t, @"^(?<head>(?:「?\d+」?\s*(?:의\s*)?배수\s*(?:의\s*)?T\s*,?\s*)?)(?:아군|자신)이\s+", "${head}");
      string named = NormalizeExtraByName(t) ?? NormalizeExtraRepeat(t);
      if (named != null)
        return named;
      if (!t.Contains("추가행동") || t.Contains("지령"))
        return null;
      var m = Regex.Match(t, @"^(?:「?(?<n>\d+)」?\s*(?:의\s*)?배수\s*(?:의\s*)?T\s*,?\s*)?(?:(?<pos>가장\s*먼저|가장\s*마지막에|제일\s*마지막에|T의\s*(?:최후|마지막)에|턴의\s*(?:최후|마지막)에|최후에)\s*,?\s*)?(?:(?:기술\s*)?(?<c>(?:「[^」]+」\s*)+)(?:등의\s*)?(?:기술)?(?:에\s*의한|로)\s*)?추가행동을\s*얻는다$");
      if (!m.Success)
        return null;
      MoveCondition c = new MoveCondition();
      if (m.Groups["c"].Success && !MoveCondition.TryParse(m.Groups["c"].Value, out c))
        return null;
      if (m.Groups["c"].Success && c.Tags.Count > 0 && Regex.IsMatch(m.Groups["c"].Value, @"^「[^」/]+」$") && !text.Contains("기술"))
        return null;
      if (m.Groups["c"].Success && Regex.IsMatch(text, @"기술\s*「[^」]+」에\s*의한"))
      {
        c = new MoveCondition();
        c.Names.Add(Regex.Match(text, @"기술\s*「(?<v>[^」]+)」").Groups["v"].Value);
      }

      int every = m.Groups["n"].Success ? int.Parse(m.Groups["n"].Value) : 0;
      return ExtraActionRuntime.Canonical(every, m.Groups["pos"].Value.Contains("먼저"), c, false);
    }
  }
}
