using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace AABattle {
 // 기술 판정에 관한 계속 효과 모듈(특성·소지품·포텐셜 공용 표준 문장)
 //  필중            자신의 「조건」 기술은 (「X」 타입에게) 필중이다. / 필드에 있는 전원의 기술은 필중이다.
 //  반드시 급소     자신의 「조건」 기술은 반드시 급소에 맞는다.
 //  급소 배율       자신의 급소 배율을 N배로 한다.
 //  분류 변경       자신의 공격기술의 분류를 「물리/특수」로 한다.
 //  위력 지정       자신의 「조건」 기술의 위력을 「N」으로 한다.
 //  명중 지정       필드에 있는 전원의 기술의 명중을 「N」으로 한다.
 //  접촉 취급 해제  자신의 기술은 「접촉」 기술로 취급하지 않는다.
 //  추가효과        자신의(상대의) 기술의 추가효과를 무효화한다. / 자신의 기술의 추가효과 발동 확률을 2배로 한다.
 //  연속 최대       자신의 「연속」 기술은 반드시 최대 횟수만큼 명중한다.
 //  방어 관통       자신의 「조건」 기술이 상대의 「방어」 기술을 관통한다.
 //  조건 반동 무효  자신의 「조건」 기술의 반동 대미지를 받지 않는다.
 // 기술 자체를 바꾸는 것(분류·위력·명중·접촉)은 Mechanics.JudgedMove가 복사본을 만들어 판정한다.
 public static class AbilityMoveModules {
  public const string FieldSureHit = "필드에 있는 전원의 기술은 필중이다";
  public const string NotContact = "자신의 기술은 「접촉」 기술로 취급하지 않는다";
  public const string NoOwnSecondary = "자신의 기술의 추가효과를 무효화한다";
  public const string NoTakenSecondary = "상대의 기술의 추가효과를 무효화한다";
  public const string DoubleSecondary = "자신의 기술의 추가효과 발동 확률을 2배로 한다";
  public const string FullMultiHit = "자신의 「연속」 기술은 반드시 최대 횟수만큼 명중한다";

  static readonly Regex SureHit = new Regex(@"^자신의 (?<c>(?:「[^」]+」)*)\s*기술은 (?:「(?<vs>[^」]+)」 타입에게 )?필중이다$");
  static readonly Regex AlwaysCrit = new Regex(@"^자신의 (?<c>(?:「[^」]+」)*)\s*기술은 반드시 급소에 맞는다$");
  static readonly Regex CritRate = new Regex(@"^자신의 급소 배율을 (?<n>[0-9.]+)배로 한다$");
  static readonly Regex Category = new Regex(@"^자신의 공격기술의 분류를 「(?<k>물리|특수)」(?:으로|로) 한다$");
  static readonly Regex SetPower = new Regex(@"^자신의 (?<c>(?:「[^」]+」)*)\s*기술의 위력을 「(?<n>\d+)」(?:으로|로) 한다$");
  static readonly Regex SetAccuracy = new Regex(@"^필드에 있는 전원의 기술의 명중을 「(?<n>\d+)」(?:으로|로) 한다$");
  static readonly Regex PierceProtect = new Regex(@"^자신의 (?<c>(?:「[^」]+」)*)\s*기술이 상대의 「방어」 기술을 관통한다$");
  static readonly Regex FailTaken = new Regex(@"^상대의 (?<c>(?:「[^」]+」)+)\s*기술이 실패한다$");
  static readonly Regex FailField = new Regex(@"^필드에 있는 전원의 (?<c>(?:「[^」]+」)+)\s*기술이 실패한다$");
  static readonly Regex ReplaceMove = new Regex(@"^상대의 (?<c>(?:「[^」]+」)*)\s*기술은 (?<n>\d+)할로 「기술:(?<m>[^」]+)」(?:이|가) 된다$");
  static readonly Regex FloatingHit = new Regex(@"^자신의 (?<c>(?:「[^」]+」)+)\s*기술은 공중에 떠 있는 상대에게도 명중한다$");
  public const string Truant = "자신이 행동한 다음 T에는 자신의 기술이 실패한다";
  static readonly Regex LagMove = new Regex(@"^자신의 (?<c>(?:「[^」]+」)+)\s*기술의 우선도를 -0\.5한다$");
  static readonly Regex PierceAbilityMove = new Regex(@"^자신의 (?<c>(?:「[^」]+」)+)\s*기술은 상대의 〔방호 특성〕을 관통한다$");
  static readonly Regex Recoil = new Regex(@"^자신의 (?<c>(?:「[^」]+」)+)\s*기술의 반동 대미지를 받지 않는다$");

  static string Clean(string text) {
   return Regex.Replace(PotentialParts.Clean(text ?? ""), @"\s+", " ").Trim().TrimEnd('.', '。', '．').Trim();
  }
  static bool Cond(string raw, out MoveCondition c) {
   return MoveCondition.TryParse(raw ?? "", out c);
  }

  // ===== 정규화 · 분류 =====
  public static string Normalize(string text) {
   string s = Clean(text);
   if (s.Length == 0) return null;
   Match m; MoveCondition c;
   // 수집 원문 변형 → 표준 문장
   m = Regex.Match(s, @"^자신의 (?<c>.*?)\s*기술에 「필중」 효과를 부여한다$");
   if (m.Success && TryCondition(m.Groups["c"].Value, out c)) return "자신의 " + (c.Empty ? "" : c.Text + " ") + "기술은 필중이다.";
   if (s == "필드의 전원의 기술에 「필중」 효과를 부여한다") return FieldSureHit + ".";
   if (Regex.IsMatch(s, @"^자신의 기술이 상대에게 「필중」한다$")) return "자신의 기술은 필중이다.";
   m = Regex.Match(s, @"^(?<c>.*?)\s*(?:인|한)?\s*기술이 반드시 급소에 맞는다$");
   if (m.Success && m.Groups["c"].Length > 0 && TryCondition(m.Groups["c"].Value, out c)) return "자신의 " + (c.Empty ? "" : c.Text + " ") + "기술은 반드시 급소에 맞는다.";
   if (Classify(s).Length > 0) return s + ".";
   return null;
  }
  // 「위력 40이하인」 같은 수집 표기를 MoveCondition으로 읽는다
  static bool TryCondition(string raw, out MoveCondition c) {
   string t = Regex.Replace(raw ?? "", @"위력\s*:?\s*「?(\d+)」?\s*(이하|이상)\s*(?:의|인)?", "「위력 $1 $2」");
   t = Regex.Replace(t, @"(?<=^|\s)(?:이|가|의|등의)(?=\s|$)", " ");
   return MoveCondition.TryParse(Regex.Replace(t, @"\s+", " ").Trim(), out c);
  }

  public static string Classify(string sentence) {
   string s = Clean(sentence);
   if (s == FieldSureHit || SureHit.IsMatch(s)) return "필중 부여";
   if (AlwaysCrit.IsMatch(s)) return "반드시 급소";
   if (CritRate.IsMatch(s)) return "급소 배율";
   if (Category.IsMatch(s)) return "기술 분류 변경";
   if (SetPower.IsMatch(s)) return "기술 위력 지정";
   if (SetAccuracy.IsMatch(s)) return "기술 명중 지정";
   if (s == NotContact) return "접촉 취급 해제";
   if (s == NoOwnSecondary || s == NoTakenSecondary) return "추가효과 무효";
   if (s == DoubleSecondary) return "추가효과 확률 배율";
   if (s == FullMultiHit) return "연속기 최대 횟수";
   if (PierceProtect.IsMatch(s) && !s.StartsWith("자신의 「변화」 기술이")) return "방어 관통";
   if (Recoil.IsMatch(s)) return "조건 반동 무효";
   if (LagMove.IsMatch(s)) return "조건 우선도 -0.5";
   if (ReplaceMove.IsMatch(s)) return "상대 기술 변경";
   if (FloatingHit.IsMatch(s)) return "공중 상대 명중";
   if (s == Truant) return "행동 후 쉼";
   if (PierceAbilityMove.IsMatch(s)) return "조건 특성 관통";
   if (FailTaken.IsMatch(s) || FailField.IsMatch(s)) return "기술 실패";
   return "";
  }

  // ===== 판정 =====
  static IEnumerable<string> Own(Fighter a, Fighter b, Rules rules, string keyword) {
   return a == null ? new string[0] : PotentialModifiers.Sentences(a, b, rules ?? new Rules(), keyword).Concat(PotentialModifiers.EventSentences(a, keyword)).Select(Clean);
  }
  static IEnumerable<string> Field(Fighter a, Fighter b, Rules rules, string keyword) {
   return Own(a, b, rules, keyword).Concat(Own(b, a, rules, keyword));
  }

  public static bool GrantsSureHit(Fighter a, Fighter b, Move move, Rules rules) {
   if (move == null) return false;
   if (Field(a, b, rules, "필중").Any(x => x == FieldSureHit)) return true;
   foreach (string line in Own(a, b, rules, "필중")) {
    var m = SureHit.Match(line); MoveCondition c;
    if (!m.Success || !Cond(m.Groups["c"].Value, out c) || !c.Matches(move, a, 1)) continue;
    if (m.Groups["vs"].Success && (b == null || !m.Groups["vs"].Value.Split('/').Any(t => (b.Data.types ?? new string[0]).Contains(t)))) continue;
    return true;
   }
   return false;
  }

  public static bool AlwaysCritical(Fighter a, Fighter b, Move move, Rules rules) {
   if (move == null) return false;
   foreach (string line in Own(a, b, rules, "급소")) {
    var m = AlwaysCrit.Match(line); MoveCondition c;
    if (m.Success && Cond(m.Groups["c"].Value, out c) && c.Matches(move, a, 1)) return true;
   }
   return false;
  }

  // 급소 대미지 배율(기본 2배)
  public static double CriticalRate(Fighter a, Fighter b, Rules rules) {
   double rate = 2;
   foreach (string line in Own(a, b, rules, "급소 배율")) {
    var m = CritRate.Match(line);
    if (m.Success) rate = Math.Max(rate, double.Parse(m.Groups["n"].Value, System.Globalization.CultureInfo.InvariantCulture));
   }
   return rate;
  }

  // 기술 자체를 바꾸는 계속 효과(분류·위력·명중·접촉). 원본은 건드리지 않고 복사본을 돌려준다
  public static Move Judge(Fighter a, Fighter b, Move move, Rules rules) {
   if (a == null || move == null) return move;
   Move copy = null;
   if (Mechanics.Contact(move) && Own(a, b, rules, "취급하지").Any(x => x == NotContact)) {
    copy = move.Copy();
    copy.tags = (copy.tags ?? new string[0]).Where(t => !t.Contains("접촉")).ToArray();
   }
   if (move.category != "변화") {
    foreach (string line in Own(a, b, rules, "분류를")) {
     var m = Category.Match(line);
     if (m.Success) { copy = copy ?? move.Copy(); copy.category = m.Groups["k"].Value; }
    }
    foreach (string line in Own(a, b, rules, "위력을 「")) {
     var m = SetPower.Match(line); MoveCondition c;
     if (m.Success && Cond(m.Groups["c"].Value, out c) && c.Matches(copy ?? move, a, 1)) { copy = copy ?? move.Copy(); copy.power = int.Parse(m.Groups["n"].Value); }
    }
   }
   foreach (string line in Field(a, b, rules, "명중을 「")) {
    var m = SetAccuracy.Match(line);
    if (m.Success && (copy ?? move).accuracy > 0) { copy = copy ?? move.Copy(); copy.accuracy = int.Parse(m.Groups["n"].Value); }
   }
   return copy ?? move;
  }

  // 사건으로 얻을 수 있는 기술 판정 문장(필중·방어 관통·방호 특성 관통)
  public static bool IsEventGrant(string sentence) {
   string s = Clean(sentence);
   return SureHit.IsMatch(s) || PierceProtect.IsMatch(s) || PierceAbilityMove.IsMatch(s) || s == AbilityPhrases.Pierce;
  }

  public static bool PiercesProtect(Fighter a, Fighter b, Move move, Rules rules) {
   if (move == null) return false;
   foreach (string line in Own(a, b, rules, "관통")) {
    var m = PierceProtect.Match(line); MoveCondition c;
    if (m.Success && Cond(m.Groups["c"].Value, out c) && c.Matches(move, a, 1)) return true;
   }
   return false;
  }

  // 조건부 우선도 -0.5(같은 우선도끼리에서만 나중)와 조건부 〔방호 특성〕 관통(지금 내는 기술로 판정)
  public static bool LagsMove(string line, Fighter f, Move move) {
   var m = LagMove.Match(line); MoveCondition c;
   return m.Success && Cond(m.Groups["c"].Value, out c) && c.Matches(move, f, 1);
  }
  public static bool PiercesAbilityFor(string line, Fighter f) {
   var m = PierceAbilityMove.Match(line); MoveCondition c;
   return m.Success && f != null && f.Conditions.ActingMove != null && Cond(m.Groups["c"].Value, out c) && c.Matches(f.Conditions.ActingMove, f, 1);
  }

  // 조건 반동 무효(조건이 없는 「자신의 기술의 반동 대미지를 받지 않는다」는 AbilityPhrases가 맡는다)
  public static bool IgnoresRecoil(Fighter a, Move move, Rules rules) {
   if (move == null) return false;
   foreach (string line in Own(a, null, rules, "반동")) {
    var m = Recoil.Match(line); MoveCondition c;
    if (m.Success && Cond(m.Groups["c"].Value, out c) && c.Matches(move, a, 1)) return true;
   }
   return false;
  }

  // 상대(b)의 계속 효과로 a의 공격기술이 확률로 다른 기술이 된다(동결·증발). 행동을 정할 때 한 번 굴린다
  public static Move Replace(Database db, Fighter a, Fighter b, Move move, Rules rules, Func<double> rng, List<string> log) {
   if (db == null || a == null || b == null || move == null || b.HP <= 0 || move.category == "변화") return move;
   foreach (string line in Own(b, a, rules, "된다")) {
    var m = ReplaceMove.Match(line); MoveCondition c;
    if (!m.Success || !Cond(m.Groups["c"].Value, out c) || !c.Matches(move, a, 1)) continue;
    var target = db.moves.FirstOrDefault(x => x.name == m.Groups["m"].Value);
    if (target == null || (rng ?? (() => 1))() >= int.Parse(m.Groups["n"].Value) / 10.0) continue;
    if (log != null) log.Add(b.Data.name + "의 〔" + AbilityRuntime.Name(b) + "〕: " + a.Data.name + "의 " + move.name + "이(가) " + target.name + "(으)로 바뀌었다");
    AnsweredTriggers.AbilityActivated(b, AbilityRuntime.Name(b), log == null ? (Action<string>)null : log.Add);
    return target;
   }
   return move;
  }

  // 「땅」 기술이 공중에 떠 있는 상대에게도 닿는가(천원돌파)
  public static bool HitsFloating(Fighter a, Fighter b, Move move, Rules rules) {
   foreach (string line in Own(a, b, rules, "공중에")) {
    var m = FloatingHit.Match(line); MoveCondition c;
    if (m.Success && Cond(m.Groups["c"].Value, out c) && c.Matches(move, a, 1)) return true;
   }
   return false;
  }

  // 기술 실패(무효화가 아니므로 방호 관통과 무관): a가 내는 기술이 b의 계속 효과로 실패하는가
  public static string FailReason(Fighter a, Fighter b, Move move, Rules rules) {
   if (a == null || b == null || move == null) return "";
   // 행동한 다음 T에는 쉰다(게으름): 기술을 내려는 행동마다 번갈아 간다
   if (Own(a, b, rules, "다음 T").Any(x => x == Truant)) {
    if (a.Conditions.TruantRest) { a.Conditions.TruantRest = false; return "행동한 다음 T라 기술이 실패한다."; }
    a.Conditions.TruantRest = true;
   }
   foreach (string line in Own(b, a, rules, "실패한다")) {
    var m = FailTaken.Match(line); MoveCondition c;
    if (m.Success && Cond(m.Groups["c"].Value, out c) && c.Matches(move, a, 1)) return line + ".";
   }
   foreach (string line in Field(a, b, rules, "실패한다")) {
    var m = FailField.Match(line); MoveCondition c;
    if (m.Success && Cond(m.Groups["c"].Value, out c) && c.Matches(move, a, 1)) return line + ".";
   }
   return "";
  }

  // 추가효과: 사용자(a)·대상(b)
  public static bool SuppressesSecondary(Fighter a, Fighter b, Rules rules) {
   return Own(a, b, rules, "추가효과").Any(x => x == NoOwnSecondary) || Own(b, a, rules, "추가효과").Any(x => x == NoTakenSecondary);
  }
  public static bool DoublesSecondary(Fighter a, Fighter b, Rules rules) {
   return Own(a, b, rules, "추가효과").Any(x => x == DoubleSecondary);
  }

  // 연속기 타격 수: 최대 횟수 모듈이 있으면 최대, 없으면 기존 규칙
  public static int HitCount(Fighter a, Fighter b, Move move, Rules rules, Func<double> rng, List<string> log) {
   if (Mechanics.MultiHit(move) && Own(a, b, rules, "최대 횟수").Any(x => x == FullMultiHit)) return Mechanics.HitCount(move, () => .999, log);
   return Mechanics.HitCount(move, rng, log);
  }
 }
}
