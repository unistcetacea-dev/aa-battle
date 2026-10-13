using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace AABattle {
 // 기술 효과 실행기: 기술 설명 문장(문서 기준)을 효과 모듈 표준 문장으로 바꿔 포텐셜과 같은 실행기로 돌린다.
 // 시점(사용자 결정): 변화기술은 성공했을 때(사용), 공격기술은 명중 후. 「N할:X」는 추가효과(SecondaryEffects)로,
 // 랭크 추가효과의 대상은 +면 자신(기술마다 1번), −면 상대(타격마다).
 // 문장으로 표현할 수 없는 기술은 전용 코드(Mechanics.StatusMove·AfterHit)가 맡는다(Dedicated).
 public sealed class MovePlan {
  public List<string> Use = new List<string>();        // 변화기술 사용(성공) 시
  public List<string> After = new List<string>();      // 공격기술 명중 후(기술마다 1번)
  public List<string> Secondary = new List<string>();  // 「N할:X」 토큰
  public List<string> Unknown = new List<string>();    // 아직 모듈로 읽지 못한 문장
  public List<string> Crash = new List<string>();      // 빗나가거나 무효화됐을 때(무릎차기 등)
  public List<string> Finally = new List<string>();    // 기술을 낸 뒤 언제나(빗나가도): 자신을 「빈사」 상태로(사용자 결정)
 }

 public static class MoveRuntime {
  // 전용 코드로 처리하는 기술(문서 문장과 맞춰 두었다)
  public static readonly HashSet<string> Dedicated = new HashSet<string> {
   "대타출동", "잠자기", "멸망의노래", "리플렉터", "빛의장막", "순풍", "날개쉬기", "태만함", "도우미", "고속스핀", "객기", "발버둥"
  };
  static readonly Dictionary<string, MovePlan> cache = new Dictionary<string, MovePlan>();
  [ThreadStatic] static Move current;
  public static Move CurrentMove { get { return current; } }   // 실행 중인 기술(꿰뚫어보기 등 기술마다 다른 상태)

  static string Plain(string text) {
   string t = (text ?? "").Replace('（', '(').Replace('）', ')').Replace('，', ',').Replace('．', '.').Replace('＋', '+').Replace('－', '-').Replace('：', ':').Replace('～', '~');
   return Regex.Replace(t, @"\s+", " ").Trim();
  }
  // 괄호 안의 마침표에서는 나누지 않는다
  static IEnumerable<string> Sentences(string text) { return Regex.Split(Plain(text), @"\.(?![^()]*\))").Select(x => x.Trim()).Where(x => x.Length > 0); }

  public static MovePlan Plan(Move m) {
   string key = (m == null ? "" : m.name + "\n" + m.effect + "\n" + m.category);
   lock (cache) {
    MovePlan plan;
    if (cache.TryGetValue(key, out plan)) return plan;
    plan = new MovePlan();
    if (m != null && !Dedicated.Contains(m.name)) foreach (string s in Sentences(m.effect)) Read(m, s, m.category == "변화" ? plan.Use : plan.After, plan);
    cache[key] = plan;
    return plan;
   }
  }

  // ===== 문장 → 표준 문장 =====
  static void Read(Move m, string s, List<string> into, MovePlan plan) {
   s = s.Trim().TrimEnd(',').Trim();
   if (s.Length == 0 || s.StartsWith("(")) return;
   if (s == "「방어」 기술" && ProtectModules.IsGuard(m)) { into.Add("자신을 「방어」 상태로 한다."); return; }
   // 「…상태로 하고, …」
   var split = Regex.Match(s, @"^(?<a>.+?상태로) 하고\s*,\s*(?<b>.+)$");
   if (split.Success) { Read(m, split.Groups["a"].Value + " 한다", into, plan); Read(m, split.Groups["b"].Value, into, plan); return; }
   if (s.StartsWith("상대의 데이터를 해석하고")) { into.Add(MoveModules.Analyze + "."); Read(m, Regex.Replace(s, @"^상대의 데이터를 해석하고\s*,?\s*", ""), into, plan); return; }
   split = Regex.Match(s, @"^HP를 1/(?<n>\d+)만큼 지불하고\s*,\s*(?<b>.+)$");
   if (split.Success) { into.Add("자신의 체력을 1/" + split.Groups["n"].Value + "만큼 소비한다."); Read(m, split.Groups["b"].Value, into, plan); return; }
   if (s.StartsWith("전체 필드의 「대타출동」「설치물」 상태를 해제하고")) { into.Add(MoveModules.ClearSubsAndHazards + "."); Read(m, Regex.Replace(s, @"^전체 필드의 「대타출동」「설치물」 상태를 해제하고\s*,?\s*", ""), into, plan); return; }
   if (s.StartsWith("자신의 「비축하기」 상태를 해제해")) { into.Add(MoveModules.Swallow + "."); return; }
   if (Regex.IsMatch(s, @"^최대 3회까지 비축할 수 있다$")) { into.Insert(0, "자신을 「비축하기」 상태로 한다."); return; }
   if (Ignored(m, s)) return;
   if (s == "「빈사」가 되는 공격을 남은 HP 「1」로 버틴다") { into.Add("자신을 「버티기」 상태로 한다."); return; }
   // 반동: 준 대미지 비율은 MoveModules.Recoil, 최대 HP 비율은 기술을 낸 뒤 언제나, 「빗나가거나 무효화됐을 때」는 그때만
   var recoil = Regex.Match(s, @"^(?<c>빗나가거나 무효화됐을 때\s*,\s*)?HP의 1/(?<n>\d+)만큼 반동 대미지를 받는다$");
   if (recoil.Success) { (recoil.Groups["c"].Success ? plan.Crash : plan.Finally).Add("자신에게 최대 HP의 1/" + recoil.Groups["n"].Value + " 대미지를 준다."); return; }
   if (s.Replace("자진을", "자신을") == "자신을 「빈사」 상태로 한다") { plan.Finally.Add("자신을 「빈사」 상태로 한다."); return; }
   Match x;
   x = Regex.Match(s, @"^명중 후\s*,\s*(?<r>.+)$");
   if (x.Success) { Read(m, x.Groups["r"].Value, plan.After, plan); return; }
   // 「N할:X」(추가효과)
   if (Regex.IsMatch(s, @"^(?:「\s*\d+\s*할\s*:[^」]+」\s*,?\s*)+$")) {
    foreach (Match t in Regex.Matches(s, @"「\s*(\d+)\s*할\s*:\s*([^」]+)」")) plan.Secondary.Add("「" + t.Groups[1].Value + "할:" + Regex.Replace(t.Groups[2].Value, @"\s+", "") + "」");
    return;
   }
   // 「S+N」「S-N」: +는 자신, −는 상대
   if (Regex.IsMatch(s, @"^(?:「[^」]+[+-]\s*\d」\s*,?\s*)+$")) {
    foreach (Match t in Regex.Matches(s, @"「([^」]+?)([+-])\s*(\d)」")) Add(into, plan, s, Rank(t.Groups[2].Value == "+" ? "자신" : "상대", t.Groups[1].Value, t.Groups[2].Value, int.Parse(t.Groups[3].Value)));
    return;
   }
   x = Regex.Match(s, @"^(?<s>자신|상대)의 (?<r>(?:「[^」]+[+-]\s*\d」\s*,?\s*)+)$");
   if (x.Success) {
    foreach (Match t in Regex.Matches(x.Groups["r"].Value, @"「([^」]+?)([+-])\s*(\d)」")) Add(into, plan, s, Rank(x.Groups["s"].Value, t.Groups[1].Value, t.Groups[2].Value, int.Parse(t.Groups[3].Value)));
    return;
   }
   // 행동불능(반동): 「다음 T 종료 후까지 자신을 「행동불능」 상태로 한다」
   x = Regex.Match(s, @"^다음 T 종료 후까지\s*,?\s*(?<r>자신을 「행동불능」 상태로 한다)$");
   if (x.Success) { into.Add(x.Groups["r"].Value + "."); return; }
   // 「서로의 「X」 상태를 치유한다」 → 자신·상대
   x = Regex.Match(s, @"^서로의 (?<n>(?:「[^」]+」)+) 상태를 치유한다$");
   if (x.Success) { Add(into, plan, s, "자신의 " + x.Groups["n"].Value + " 상태를 치유한다."); Add(into, plan, s, "상대의 " + x.Groups["n"].Value + " 상태를 치유한다."); return; }
   x = Regex.Match(s, @"^자진의 「(?<v>설치물|벽)」을 해제한다$");
   if (x.Success) { Add(into, plan, s, x.Groups["v"].Value == "설치물" ? "자진의 설치물을 제거한다." : "자진의 「벽」을 해제한다."); return; }
   // 회복
   x = Regex.Match(s, @"^상대에게 HP의 1/(?<n>\d+)만큼 고정 대미지를 준다$");
   if (x.Success && m.category == "변화") { into.Add("상대에게 최대 HP의 1/" + x.Groups["n"].Value + " 대미지를 준다."); return; }
   x = Regex.Match(s, @"^HP를 1/(?<n>\d+)만큼 회복한다$");
   if (x.Success) { into.Add("자신의 체력을 최대 HP의 1/" + x.Groups["n"].Value + " 회복한다."); return; }
   // 날씨·필드·장
   x = Regex.Match(s, @"^(?<n>\d+)T 동안 날씨를 「(?<w>[^」]+)」(?:으로|로) 한다$");
   if (x.Success) { string w = x.Groups["w"].Value == "설경" ? "눈" : x.Groups["w"].Value; Add(into, plan, s, x.Groups["n"].Value + "T동안 날씨를 「" + w + "」" + EffectModules.Ro(w) + " 한다."); return; }
   x = Regex.Match(s, @"^(?<n>\d+)T 동안 전체 필드(?:를|에) 「(?<v>[^」]+)」(?: 상태로 한다|을 전개한다|를 전개한다)$");
   if (x.Success) {
    string v = x.Groups["v"].Value, n = x.Groups["n"].Value;
    Add(into, plan, s, v == "중력" || v == "트릭룸" || v == "물놀이" || v == "흙놀이" ? n + "T동안 「" + v + "」을 전개한다." : n + "T동안 지형을 「" + v + "」" + EffectModules.Ro(v) + " 한다.");
    return;
   }
   x = Regex.Match(s, @"^적진에 「(?<h>[^」]+)」(?:을|를) 설치한다$");
   if (x.Success) { string h = x.Groups["h"].Value == "압정" ? "압정뿌리기" : x.Groups["h"].Value; Add(into, plan, s, "적진에 「" + h + "」을 설치한다."); return; }
   // 얼음의벽 기술: 「4T 동안 자진에 「얼음의벽」을 전개한다」 = 진영 상태(리플렉터와 달리 전용 코드가 없다)
   x = Regex.Match(s, @"^(?<n>\d+)T 동안 자진에 「얼음의벽」을 전개한다$");
   if (x.Success) { into.Add("자신을 " + x.Groups["n"].Value + "T간 「얼음의벽」 상태로 한다."); return; }
   // 상태 부여(자진 = 자신, 적진 = 상대)
   string own = s.Replace("자진을", "자신을").Replace("적진을", "상대를");
   var state = StateEffects.Parse(own);
   if (state != null && StateEffects.Find(state.State) != null) { into.Add(own + "."); return; }
   // 그 밖: 효과 모듈 정규화로 읽히면 그대로
   string normalized = EffectModules.Normalize(s);
   if (normalized != null && EffectModules.Classify(normalized).Length > 0) { into.Add(normalized); return; }
   plan.Unknown.Add(s);
  }
  static void Add(List<string> into, MovePlan plan, string raw, string line) {
   if (line == null || EffectModules.Classify(line).Length == 0) plan.Unknown.Add(raw);
   else into.Add(line);
  }
  // 랭크 문장(1~3랭크). 그 이상은 표준 문장이 없다
  public static string Rank(string subject, string stats, string sign, int amount) {
   if (amount < 1 || amount > 3) return null;
   string adverb = amount == 3 ? "매우 크게 " : amount == 2 ? "크게 " : "";
   string verb = sign == "+" ? "오른다" : "내린다";
   stats = stats.Trim();
   if (stats == "전능력치") return subject + "의 전능력치가 " + adverb + verb + ".";
   return subject + "의 「" + stats + "」이 " + adverb + verb + ".";
  }
  // 다른 곳(계산·MoveModules·TypeRules)이 처리하는 문장
  static bool Ignored(Move m, string s) {
   if (Regex.IsMatch(s, @"^「[^」]+」 기술$|^\d+(?:~\d+)?회 「[^」]+」 기술$|^(?:특수|추가)효과 없음$")) return true;
   if (Regex.IsMatch(s, @"급소에 맞기 쉽다|반드시 급소에 맞는다|유리한 타입 상성|능력치로 대미지를 계산한다|「방어」 기술을 관통한다")) return true;
   if (FailModules.IsModule(s)) return true;
   if (VariablePower.Handles(s)) return true;
   if (Mechanics.IsDelayedMove(m) && Regex.IsMatch(s, @"^상대를 「[^」]+」 상태로 한다$|^「미래예지」 기술$")) return true;
   if (Regex.IsMatch(s, @"^상대가 「[^」]+」 상태일 때\s*,\s*위력이 강화\([^)]*\)되고 「필중」 효과가 부여된다$")) return true;
   if (m.category != "변화" && s.EndsWith("고정 대미지를 준다")) return true;
   if (Regex.IsMatch(s, @"〔방호 특성〕을 관통한다$|^적진의 「벽」을 해제한 후 기술을 내보낸다$|상대에게도 명중한다$")) return true;
   if (Regex.IsMatch(s, @"^(?:상대보다 먼저 내보낼 때|기술을 내보내기 전에 상대의 공격을 받으면|자신의 HP가 「가득」일 때|상대가 「방어」 기술을 내보내고 있으면|(?:자신이 )?「[^」]+」 상태일 때)\s*,\s*위력이 강화\(\d+(?:\.\d+)?배\)된다$")) return true;
   if (Regex.IsMatch(s, @"능력치를 반감해서 대미지를 계산한다$|능력치를 반전해 대미지를 계산한다$|의 상승\(강화\)을 관통한다$|타입일 때\s*,\s*「효과가 굉장」해진다$|^타입 상성에 의해 무효화되지 않는다$|^전체 필드가 「[^」]+」 상태일 때\s*,\s*위력이 강화")) return true;
   if (Regex.IsMatch(s, @"준 대미지의 \d+/\d+만큼 HP를 회복한다|「모으기」 기술|모습을 감추|^날씨가 「[^」]+」일 때")) return true;
   if (m.tags != null && m.tags.Contains("조이기") && s.Contains("「조이기」 상태로 한다")) return true;
   if (Regex.IsMatch(s, @"^준 대미지의 \d+/\d+만큼 반동 대미지를 받는다$")) return true;
   if (Regex.IsMatch(s, @"기술을 「방어」한다$|무효화했을 때\s*,|연속해서 내보내면 실패(?:하기 쉽다|한다)$")) return true;
   if (m.tags != null && m.tags.Contains("난동부리기") && s.Contains("「난동부리기」 상태로 한다")) return true;
   if (Regex.IsMatch(s, @"^상대가 「[^」]+」\s*(?:상태|도중|중)?일 때\s*,\s*위력이 강화\(\d+(?:\.\d+)?배\)된다$|^「[^」]+」 타입이 내보내면 「필중」 효과가 부여된다$|^1T째에 자신의 「[^」]+」을 올리고")) return true;
   return false;
  }

  // ===== 직접 실행(큐 없이, 확률·사용 제한 없음) =====
  public static EffectOutcome Run(Fighter owner, Fighter other, Move move, string line, Rules rules, Func<double> rng, List<string> log) {
   var source = new PotentialInstance { Owner = owner, Name = move == null ? "기술" : move.name, Slot = "기술", Effect = line, Description = line, Key = "move:" + (move == null ? "" : move.name) };
   var e = new PotentialEvent { Label = "기술", Owner = owner, Other = other, Move = move, Log = log.Add, Message = source.Name };
   string text = EffectNormalizer.Normalize(line);
   if (text.Length == 0) text = line;
   var lines = new List<string>();
   var call = new EffectCall { Source = source, Event = e, Line = text, Rules = rules, Rng = rng, Log = lines };
   var executor = PotentialExecutors.Find(call);
   if (executor == null) { log.Add(source.Name + ": 효과 판정 대기 — " + line); return EffectOutcome.Pending; }
   var saved = current; current = move;
   EffectOutcome outcome;
   try { outcome = executor.Run(call); } finally { current = saved; }
   log.AddRange(lines);
   return outcome;
  }

  // 문장으로 실행할 수 있는 변화기술인가(아니면 전용 코드)
  public static bool TextDriven(Move m) {
   if (m == null || Dedicated.Contains(m.name)) return false;
   var plan = Plan(m);
   return plan.Unknown.Count == 0 && (plan.Use.Count + plan.After.Count + plan.Secondary.Count + plan.Finally.Count + plan.Crash.Count) > 0;
  }

  // 변화기술: 문장 기술이면 문장대로, 아니면 전용 코드. 하나라도 성공하면 성공
  public static bool Status(Fighter a, Fighter b, Move m, Rules r, Func<double> rng, List<string> log, int side) {
   if (!TextDriven(m)) {
    var plan0 = Plan(m);
    if (Dedicated.Contains(m.name) || Mechanics.KnownMove(m) || plan0.Use.Count + plan0.Secondary.Count == 0) return Mechanics.StatusMove(a, b, m, r, rng, log, side);
   }
   var plan = Plan(m);
   bool done = false;
   if (ProtectModules.IsGuard(m)) a.Conditions.ProtectMove = m.name;
   foreach (string line in plan.Use) {
    var outcome = Run(a, b, m, line, r, rng, log);
    done |= outcome == EffectOutcome.Success;
    bool gate = line.Contains("소비한다") || line.Contains("「비축하기」 상태로 한다");
    if (gate && outcome != EffectOutcome.Success) return false;
   }
   foreach (string token in plan.Secondary) done |= SecondaryEffects.Roll(a, b, token, m.name, r, rng, log, null);
   foreach (string s in plan.Unknown) log.Add(m.name + ": 효과 판정 대기 — " + s);
   return done;
  }

  // 기술을 낸 뒤 언제나(빗나가도·막혀도): 자폭류 「빈사」
  public static void Finally(Fighter a, Move m, List<string> log, Func<double> rng) {
   if (a == null || m == null || log == null) return;
   var plan = Plan(m);
   if (plan.Finally.Count == 0) return;
   var scope = EffectScope.Current;
   var other = scope == null ? null : scope.Opponent(a);
   foreach (string line in plan.Finally) Run(a, other, m, line, scope == null ? new Rules() : scope.Rules, rng, log);
  }
  // 빗나가거나 무효화됐을 때
  public static void Crash(Fighter a, Fighter b, Move m, Rules r, Func<double> rng, List<string> log) {
   if (a == null || m == null || a.HP <= 0) return;
   foreach (string line in Plan(m).Crash) Run(a, b, m, line, r, rng, log);
  }
  // 문장 하나를 표준 문장들로(방어 반응 등)
  public static List<string> Convert(Move m, string sentence, List<string> unknown) {
   var plan = new MovePlan();
   Read(m, sentence, plan.Use, plan);
   if (unknown != null) unknown.AddRange(plan.Unknown);
   return plan.Use.Concat(plan.After).ToList();
  }

  // 공격기술 타격마다: 상대에게 거는 추가효과(대타에 막히면 걸지 않는다)
  public static void PerHit(Fighter a, Fighter b, Move m, Rules r, Func<double> rng, List<string> log) {
   if (m == null || m.category == "변화") return;
   foreach (string token in Plan(m).Secondary) SecondaryEffects.Roll(a, b, token, m.name, r, rng, log, false);
  }
  // 공격기술 명중 후(기술마다 1번): 자신에게 거는 추가효과(+랭크)와 「명중 후」 문장
  public static void AfterHits(Fighter a, Fighter b, Move m, Rules r, Func<double> rng, List<string> log) {
   if (m == null || m.category == "변화") return;
   var plan = Plan(m);
   foreach (string token in plan.Secondary) SecondaryEffects.Roll(a, b, token, m.name, r, rng, log, true);
   foreach (string line in plan.After) Run(a, b, m, line, r, rng, log);
   if (plan.Unknown.Count > 0) log.Add(m.name + ": 미구현 효과(수동 판정) — " + string.Join(" / ", plan.Unknown));
  }
 }
}
