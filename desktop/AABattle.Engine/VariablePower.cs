using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace AABattle {
 // 가변 위력 모듈: 기술 문장으로 기본 위력을 정한다(문서에 수치가 없으면 실기 식)
 //  자신의 현재 HP가 적을수록 강화      기사회생·바둥바둥: 48×HP/최대 → 200·150·100·80·40·20
 //  자신의 현재 HP가 적을수록 약화      분화·해수스파우팅·드래곤에너지: 150×HP/최대
 //  상대의 현재 HP가 많을수록 강화      묵사발·쥐어짜기: 120×상대 HP/최대(최소 1)
 //  자신/상대의 「능력변화:+」에 비례    어시스트파워·기어오르기 20+20×상승 합(최대 860) / 혼내기 60+20×상승 합(최대 200)
 //  비축하기 횟수만큼(+100, 최대 300)   토해내기·오토캐논(비축하기가 없으면 실패, 쓰면 비축 해제)
 //  연속해서 명중할 때마다 2배          연속자르기 40·80·160(빗나가면 초기화)
 //  「공격」 기술을 받은 횟수에 비례     분노의주먹 50+50×횟수(최대 350)
 //  「빈사」 상태인 아군의 수에 비례     성묘 50+50×수
 //  친밀도                             은혜갚기·화풀이: 친밀도 데이터가 없어 최대 102
 //  랜덤                               매그니튜드(진도 4~10), 프레젠트(40·80·120 또는 상대 회복 1/4)
 //  날씨/필드에 따라 타입과 위력        웨더볼·대지의파동(위력 100, 대지의파동은 접지했을 때만)
 public static class VariablePower {
  static string Plain(Move m) {
   string t = m == null ? "" : m.effect ?? "";
   t = t.Replace('（', '(').Replace('）', ')').Replace('，', ',').Replace('．', '.').Replace('＋', '+').Replace('：', ':');
   return Regex.Replace(t, @"\s+", " ");
  }
  static int Boosts(Fighter f) { return f.Stages.Skip(1).Where(x => x > 0).Sum() + Math.Max(0, f.AccuracyStage) + Math.Max(0, f.EvasionStage); }

  // 기본 위력(가변 위력 기술이 아니면 null)
  public static double? BasePower(Fighter a, Fighter b, Move m, Rules r) {
   if (a == null || m == null || m.category == "변화") return null;
   string t = Plain(m);
   r = r ?? new Rules();
   if (t.Contains("자신의 현재 HP가 적을수록 위력이 강화된다")) {
    int p = 48 * a.HP / Math.Max(1, Mechanics.MaxHP(a, b ?? a, r));
    return p <= 1 ? 200 : p <= 4 ? 150 : p <= 9 ? 100 : p <= 16 ? 80 : p <= 32 ? 40 : 20;
   }
   if (t.Contains("자신의 현재 HP가 적을수록 위력이 약화된다")) return Math.Max(1, Math.Floor(150.0 * a.HP / Math.Max(1, Mechanics.MaxHP(a, b ?? a, r))));
   if (t.Contains("상대의 현재 HP가 많을수록 위력이 강화된다") && b != null) return Math.Max(1, Math.Floor(120.0 * b.HP / Math.Max(1, Mechanics.MaxHP(b, a, r))));
   if (Regex.IsMatch(t, @"자신의 「능력변화:\+」에 비례해 위력이 강화")) return Math.Min(860, 20 + 20 * Boosts(a));
   if (Regex.IsMatch(t, @"상대의 「능력변화:\+」에 비례해 위력이 강화") && b != null) return Math.Min(200, 60 + 20 * Boosts(b));
   if (t.Contains("비축하기 횟수만큼 위력이 강화")) return Math.Min(300, 100 * a.Conditions.Stockpile);
   if (t.Contains("연속해서 명중할 때마다 위력이 강화(2배)된다")) return Math.Min(160, 40 * Math.Pow(2, a.Conditions.RepeatMove == m.name ? Math.Min(2, a.Conditions.RepeatCount) : 0));
   if (t.Contains("「공격」 기술을 받은 횟수에 비례해 위력이 강화된다")) return Math.Min(350, 50 + 50 * a.Conditions.TimesHit);
   if (t.Contains("「빈사」 상태인 아군의 수에 비례해 위력이 강화")) {
    int side = Mechanics.PartySide(a, r);
    int fainted = side < 0 ? 0 : (r.Parties[side] ?? new Fighter[0]).Count(x => x != null && x.HP <= 0 && !Mechanics.SameMember(x, a));
    return 50 + 50 * fainted;
   }
   if (Regex.IsMatch(t, @"친밀도가 (?:높을|낮을)수록 위력이 강화된다")) return 102;
   if (t.Contains("내보낼 때마다 위력이 랜덤하게 변화한다")) return a.Conditions.RolledPower > 0 ? a.Conditions.RolledPower : m.name.StartsWith("프레젠트") ? 80 : 70;
   return null;
  }

  // 날씨·필드에 따라 타입과 위력이 바뀌는 기술
  public static Move Transform(Fighter a, Fighter b, Move m, Rules r) {
   if (a == null || m == null || r == null) return m;
   string t = Plain(m), type = "";
   if (t.Contains("날씨에 따라 타입과 위력이 변화한다")) {
    string w = Mechanics.Weather(a, b ?? a, r);
    if (MiscModules.IgnoresWeather(a, w, r)) w = "없음";
    type = Mechanics.Sunny(w) ? "불꽃" : Mechanics.Rain(w) ? "물" : w == "모래바람" ? "바위" : w == "눈" || w == "싸라기눈" ? "얼음" : "";
   } else if (t.Contains("「필드」에 따라 타입과 위력이 변화한다") && Mechanics.Grounded(a, r)) {
    type = r.Terrain == "일렉트릭필드" ? "전기" : r.Terrain == "그래스필드" ? "풀" : r.Terrain == "미스트필드" ? "페어리" : r.Terrain == "사이코필드" ? "에스퍼" : "";
   }
   if (type.Length == 0) return m;
   var copy = m.Copy(); copy.types = new[] { type }; copy.power = 100;
   return copy;
  }

  // 기술을 내기 직전: 랜덤 위력을 정하고, 실패(비축하기 없음)·프레젠트 회복이면 이유
  public static string Roll(Fighter a, Fighter b, Move m, Rules r, Func<double> rng, List<string> log) {
   if (a == null || m == null) return "";
   a.Conditions.RolledPower = 0;
   string t = Plain(m);
   rng = rng ?? (() => .5);
   if (t.Contains("비축하기 횟수만큼 위력이 강화") && a.Conditions.Stockpile <= 0) return "비축하기가 없어 실패했다.";
   if (!t.Contains("내보낼 때마다 위력이 랜덤하게 변화한다")) return "";
   double x = rng();
   if (m.name.StartsWith("프레젠트")) {
    if (x >= .8) { if (b != null) Mechanics.Heal(b, a, r, Math.Max(1, Mechanics.MaxHP(b, a, r) / 4), "프레젠트", log); return "상대의 HP를 회복시켜 주었다."; }
    a.Conditions.RolledPower = x < .4 ? 40 : x < .7 ? 80 : 120;
    return "";
   }
   int[] powers = { 10, 30, 50, 70, 90, 110, 150 };
   double[] edges = { .05, .15, .35, .65, .85, .95, 1 };
   int i = Array.FindIndex(edges, e => x < e); if (i < 0) i = 6;
   a.Conditions.RolledPower = powers[i];
   log.Add(m.name + ": 진도 " + (i + 4));
   return "";
  }

  // 기술을 쓴 뒤: 비축하기를 쓰는 기술은 비축(과 비축한 방어·특방 상승)을 되돌린다(실기)
  public static void After(Fighter a, Move m) {
   if (a == null || m == null || !Plain(m).Contains("비축하기 횟수만큼 위력이 강화")) return;
   int n = a.Conditions.Stockpile;
   if (n <= 0) return;
   a.Stages[2] = Math.Max(-6, a.Stages[2] - n); a.Stages[4] = Math.Max(-6, a.Stages[4] - n); a.Conditions.Stockpile = 0;
  }

  // 기술 계획에서 건너뛸 문장(여기서 처리)
  public static bool Handles(string s) {
   return Regex.IsMatch(s, @"현재 HP가 (?:적을|많을)수록 위력이 (?:강화|약화)된다$|「능력변화:\+」에 비례해 위력이 강화|비축하기 횟수만큼 위력이 강화|연속해서 명중할 때마다 위력이 강화|「공격」 기술을 받은 횟수에 비례해|「빈사」 상태인 아군의 수에 비례해|친밀도가 (?:높을|낮을)수록|내보낼 때마다 위력이 랜덤하게|상대의 HP를 1/4만큼 회복시켜 줄 때도 있다|^(?:날씨|「필드」)에 따라 타입과 위력이 변화한다$|^1번 명중할 때마다 위력이 강화|^명중할 때마다 위력이 강화\(2배\)된다$|「봉인」 상태가 되거나 빗나갈 때까지|^\(최대 \d+\)|빗나가면 초기화된다");
  }
 }
}
