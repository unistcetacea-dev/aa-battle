using System;
using System.Linq;
using System.Collections.Generic;

namespace AABattle {
 // 기술 문장의 새 상태(사용자 결정: 오레마스 문서의 상태 정의, 없으면 실기)
 //  오레마스 문서: 작아지기(「작아지기」에게 필중·2배는 기술 문장), 꿰뚫어보기(실기 효과 + 데이터 해석), 봉인(상대 기술 1개),
 //               길동무(방어처럼 연속 사용 판정)
 //  실기: 하품·희망사항·저주·기충전(C+2)·신비의부적·흰안개·주술(급소 방지)·전자부유·텔레키네시스·매직코트·길동무·악몽·원념·
 //        금제(소지품 무효)·분진·패스트가드·날따름·순서미루기(싱글은 효과 없음)·비축하기·대타출동·물놀이·흙놀이
 public static partial class StateEffects {
  static void AddMore(List<StateEffectRule> rules) {
   rules.Add(Rule("작아지기", c => Flag(c, "작아지기", c.Target.Conditions.Minimized, () => c.Target.Conditions.Minimized = true), true));
   rules.Add(Rule("꿰뚫어보기", c => { c.Target.Conditions.Foresight = MoveRuntime.CurrentMove == null ? "꿰뚫어보기" : MoveRuntime.CurrentMove.name; return true; }, true));
   rules.Add(Rule("하품", c => {
    var t = c.Target;
    if (t.Status != "정상" || t.Conditions.Yawn > 0 || StateTicks.Guarded(t, c.Source, c.Rules)) { c.Say(t.Data.name + ": 하품 실패"); return false; }
    t.Conditions.Yawn = 2;
    return true;
   }));
   rules.Add(Rule("희망사항", c => {
    var side = StateTicks.Side(c.Target, c.Rules);
    if (side == null || side.WishTurns > 0) { c.Say("희망사항 실패"); return false; }
    side.WishTurns = 2; side.WishHeal = Math.Max(1, Mechanics.MaxHP(c.Target, c.Target, c.Rules) / 2);
    return true;
   }, true));
   rules.Add(Rule("저주", c => Flag(c, "저주", c.Target.Conditions.Curse, () => c.Target.Conditions.Curse = true)));
   rules.Add(Rule("기충전", c => Flag(c, "기충전", c.Target.Conditions.FocusEnergy, () => c.Target.Conditions.FocusEnergy = true), true));
   // 날따름·순서미루기(사용자 결정): 상태변화로 걸리지만 싱글에서는 효과가 없다(트리거는 될 수 있다)
   rules.Add(Rule("날따름", c => Flag(c, "날따름", c.Target.Conditions.FollowMe, () => { c.Target.Conditions.FollowMe = true; c.Say("싱글 배틀에서는 효과가 없다"); }), true));
   rules.Add(Rule("순서미루기", c => Flag(c, "순서미루기", c.Target.Conditions.Quash, () => { c.Target.Conditions.Quash = true; c.Say("싱글 배틀에서는 효과가 없다"); })));
   // 예민해지기(사용자 결정: 실기의 레이저포커스) — 다음 T까지 다음 공격이 반드시 급소
   rules.Add(Rule("예민해지기", c => Flag(c, "예민해지기", c.Target.Conditions.LaserFocus > 0, () => c.Target.Conditions.LaserFocus = 2), true));
   // 맹세 합체 필드(실기, 4T): 불바다 — 상대 진영 불꽃 타입 외 매 T 1/8 / 습원 — 상대 진영 속도 1/4 / 무지개 — 자기 진영 추가효과 확률 2배
   foreach (string pledge in new[] { "불바다", "습원", "무지개" }) {
    string name = pledge;
    rules.Add(Rule(name, c => {
     var s = StateTicks.Side(c.Target, c.Rules);
     if (s == null) return false;
     int turns = c.Turns > 0 ? c.Turns : 4;
     if (name == "불바다") { if (s.SeaOfFire > 0) return Fail(c, name); s.SeaOfFire = turns; }
     else if (name == "습원") { if (s.Swamp > 0) return Fail(c, name); s.Swamp = turns; }
     else { if (s.Rainbow > 0) return Fail(c, name); s.Rainbow = turns; }
     return true;
    }, name == "무지개"));
   }
   foreach (var side in new[] { new { n = "신비의부적", t = 5 }, new { n = "흰안개", t = 5 }, new { n = "주술", t = 5 } }) {
    string name = side.n; int basic = side.t;
    rules.Add(Rule(name, c => {
     var s = StateTicks.Side(c.Target, c.Rules);
     if (s == null) return false;
     int turns = c.Turns > 0 ? c.Turns : basic;
     if (name == "신비의부적") { if (s.Safeguard > 0) return Fail(c, name); s.Safeguard = turns; }
     else if (name == "흰안개") { if (s.Mist > 0) return Fail(c, name); s.Mist = turns; }
     else { if (s.LuckyChant > 0) return Fail(c, name); s.LuckyChant = turns; }
     return true;
    }, true));
   }
   // 특성·포텐셜로 거는 진영 상태(기술은 Mechanics.StatusMove가 따로 처리한다): 순풍 4T · 리플렉터/빛의장막 5T(기간 연장 소지품 적용)
   foreach (string wall in new[] { "순풍", "리플렉터", "빛의장막" }) {
    string name = wall;
    rules.Add(Rule(name, c => {
     var s = StateTicks.Side(c.Target, c.Rules);
     if (s == null) return false;
     int turns = MiscModules.ExtendTurns(c.Source, name, c.Turns > 0 ? c.Turns : name == "순풍" ? 4 : 5);
     if (name == "순풍") { if (s.Tailwind > 0) return Fail(c, name); s.Tailwind = turns; }
     else if (name == "리플렉터") { if (s.Reflect > 0) return Fail(c, name); s.Reflect = turns; }
     else { if (s.LightScreen > 0) return Fail(c, name); s.LightScreen = turns; }
     c.Say((StateTicks.SideIndex(c.Target, c.Rules) + 1) + "팀 진영: " + name + " " + turns + "턴");
     return true;
    }, true));
   }
   // 먹이를 문 상태(그대로꿀꺽미사일): 효과는 특성 문장이 정한다
   rules.Add(Rule("먹이를 문", c => Flag(c, "먹이를 문", c.Target.Conditions.Gulping, () => c.Target.Conditions.Gulping = true), true));
   rules.Add(Rule("패스트가드", c => { var s = StateTicks.Side(c.Target, c.Rules); if (s == null) return false; s.QuickGuard = true; return true; }, true));
   rules.Add(Rule("전자부유", c => Flag(c, "전자부유", c.Target.Conditions.MagnetRise > 0, () => c.Target.Conditions.MagnetRise = c.Turns > 0 ? c.Turns : 5), true));
   rules.Add(Rule("텔레키네시스", c => Flag(c, "텔레키네시스", c.Target.Conditions.Telekinesis > 0, () => c.Target.Conditions.Telekinesis = c.Turns > 0 ? c.Turns : 3)));
   rules.Add(Rule("매직코트", c => Flag(c, "매직코트", c.Target.Conditions.MagicCoat, () => c.Target.Conditions.MagicCoat = true), true));
   rules.Add(Rule("길동무", c => { c.Target.Conditions.DestinyBond = true; return true; }, true));
   rules.Add(Rule("악몽", c => {
    if (c.Target.Status != "잠듦") { c.Say("악몽 실패: 잠들어 있지 않다"); return false; }
    return Flag(c, "악몽", c.Target.Conditions.Nightmare, () => c.Target.Conditions.Nightmare = true);
   }));
   rules.Add(Rule("원념", c => { c.Target.Conditions.Grudge = true; return true; }, true));
   rules.Add(Rule("금제", c => Flag(c, "금제", c.Target.Conditions.Embargo > 0, () => { c.Target.Conditions.Embargo = c.Turns > 0 ? c.Turns : 5; ItemRuntime.Sync(c.Target); })));
   rules.Add(Rule("분진", c => Flag(c, "분진", c.Target.Conditions.Powder, () => c.Target.Conditions.Powder = true)));
   rules.Add(Rule("비축하기", c => {
    if (c.Target.Conditions.Stockpile >= 3) { c.Say(c.Target.Data.name + ": 더는 비축할 수 없다"); return false; }
    c.Target.Conditions.Stockpile++;
    c.Say(c.Target.Data.name + ": 비축하기 " + c.Target.Conditions.Stockpile);
    return true;
   }, true));
   rules.Add(Rule("대타출동", c => {
    var t = c.Target;
    if (t.Conditions.Substitute > 0) { c.Say("대타출동 실패: 이미 대타가 있다"); return false; }
    t.Conditions.Substitute = Math.Max(1, Mechanics.MaxHP(t, t, c.Rules) / 4);
    c.Say(t.Data.name + ": 대타 HP " + t.Conditions.Substitute);
    return true;
   }, true));
   // 봉인(오레마스): 상대 기술 1개를 골라 봉인한다(고르는 창이 없으면 마지막으로 낸 기술, 없으면 첫 기술)
   rules.Add(Rule("봉인", c => {
    var t = c.Target;
    var moves = (t.Data.moves ?? new string[0]).Where(x => !string.IsNullOrEmpty(x)).ToArray();
    c.Say(t.Data.name + "의 기술: " + string.Join(" · ", moves));
    string fallback = !string.IsNullOrEmpty(t.Conditions.LastMove) ? t.Conditions.LastMove : moves.FirstOrDefault() ?? "";
    string move = c.Rules.ChooseSealMove == null ? fallback : c.Rules.ChooseSealMove(t, moves) ?? fallback;
    if (move.Length == 0 || t.Conditions.Sealed == move) return false;
    StateTicks.Seal(t, move, c.Rules);
    c.Say(t.Data.name + ": 「" + move + "」 봉인");
    return true;
   }));
  }
  static bool Fail(StateEffectContext c, string name) { c.Say(name + " 실패: 이미 전개되어 있다."); return false; }
 }

 // 새 상태의 기간·턴 끝 처리와 판정 지점
 public static class StateTicks {
  public const string Reflected = "매직코트가 기술을 되돌려 보냈다";
  public static int SideIndex(Fighter f, Rules r) { return f == null || r == null ? -1 : Mechanics.PartySide(f, r); }
  public static SideFieldState Side(Fighter f, Rules r) {
   if (f == null || r == null) return null;
   int side = Mechanics.PartySide(f, r);
   return side < 0 || side > 1 ? null : r.Sides[side];
  }
  // 신비의부적: 상대가 거는 상태이상·혼란·하품을 막는다
  public static bool Guarded(Fighter target, Fighter source, Rules r) {
   if (target == null || source == null || ReferenceEquals(target, source)) return false;
   var s = Side(target, r);
   return s != null && s.Safeguard > 0 && Mechanics.PartySide(source, r) != Mechanics.PartySide(target, r);
  }
  public static bool Mist(Fighter target, Rules r) { var s = Side(target, r); return s != null && s.Mist > 0; }
  public static bool NoCrit(Fighter target, Rules r) { var s = Side(target, r); return s != null && s.LuckyChant > 0; }
  public static bool QuickGuarded(Fighter a, Fighter b, Move m, Rules r) {
   var s = Side(b, r);
   return s != null && s.QuickGuard && m != null && Mechanics.Priority(a, m, b) > 0;
  }
  // 봉인(오레마스): 걸리면 교대해도 계속된다. 진영 기록(Rules.SealedMoves)에 남기고 장소에 나올 때 되살린다
  public static void Seal(Fighter f, string move, Rules r) {
   f.Conditions.Sealed = move;
   if (r != null) r.SealedMoves[Mechanics.MemberKey(f.Data)] = move;
  }
  public static void Restore(Fighter f, Rules r) {
   string move;
   if (f != null && r != null && r.SealedMoves.TryGetValue(Mechanics.MemberKey(f.Data), out move)) f.Conditions.Sealed = move;
  }
  // 습원: 진영의 속도 1/4
  public static double StatFactor(Fighter f, int stat, Rules r) { if (stat != 5) return 1; var s = Side(f, r); return s != null && s.Swamp > 0 ? .25 : 1; }
  public static bool Rainbow(Fighter f, Rules r) { var s = Side(f, r); return s != null && s.Rainbow > 0; }
  public static bool Floating(Fighter f) { return f != null && (f.Conditions.MagnetRise > 0 || f.Conditions.Telekinesis > 0); }
  // 분진: 「불꽃」 기술을 내면 실패하고 최대 HP 1/4
  public static string Powder(Fighter a, Fighter b, Move m, Rules r, List<string> log) {
   if (a == null || !a.Conditions.Powder || m == null || !(m.types ?? new string[0]).Contains("불꽃")) return "";
   Mechanics.Hurt(a, b, r, Math.Max(1, Mechanics.MaxHP(a, b, r) / 4), "분진 폭발", log);
   return "분진이 폭발해 실패했다.";
  }
  // 길동무·원념: 상대의 공격으로 쓰러졌을 때
  public static void Fainted(Fighter attacker, Fighter fainted, Move m, List<string> log) {
   if (attacker == null || fainted == null || fainted.HP > 0) return;
   if (fainted.Conditions.DestinyBond && attacker.HP > 0) { attacker.HP = 0; log.Add(attacker.Data.name + "은 길동무로 함께 쓰러졌다!"); }
   if (fainted.Conditions.Grudge && m != null) { Seal(attacker, m.name, EffectScope.Current == null ? null : EffectScope.Current.Rules); log.Add(attacker.Data.name + ": 원념으로 「" + m.name + "」을 낼 수 없게 되었다"); }
  }

  public static void EndTurn(Fighter[] f, Rules r, List<string> log, Func<double> rng) {
   foreach (var x in f) {
    if (x == null) continue;
    var c = x.Conditions;
    c.MagicCoat = c.Powder = c.FollowMe = c.Quash = false;
    if (x.HP <= 0) continue;
    if (c.LaserFocus > 0) c.LaserFocus--;
    if (c.Yawn > 0 && --c.Yawn == 0) { log.Add(x.Data.name + ": 하품으로 잠이 들었다"); Mechanics.ApplyStatus(x, x, "잠듦", r, rng, log); }
    if (c.Nightmare) { if (x.Status == "잠듦") Mechanics.Hurt(x, x, r, Math.Max(1, Mechanics.MaxHP(x, x, r) / 4), "악몽", log); else c.Nightmare = false; }
    if (c.MagnetRise > 0 && --c.MagnetRise == 0) log.Add(x.Data.name + ": 전자부유가 풀렸다");
    if (c.Telekinesis > 0 && --c.Telekinesis == 0) log.Add(x.Data.name + ": 텔레키네시스가 풀렸다");
    if (c.Embargo > 0 && --c.Embargo == 0) { log.Add(x.Data.name + ": 금제가 풀렸다"); ItemRuntime.Sync(x); }
   }
   for (int i = 0; i < r.Sides.Length && i < f.Length; i++) {
    var s = r.Sides[i];
    s.QuickGuard = false;
    if (s.SeaOfFire > 0 && f[i] != null && f[i].HP > 0 && !Mechanics.Has(f[i], "불꽃")) Mechanics.Hurt(f[i], f[1 - i], r, Math.Max(1, Mechanics.MaxHP(f[i], f[1 - i], r) / 8), "불바다", log);
    if (s.SeaOfFire > 0 && --s.SeaOfFire == 0) log.Add((i + 1) + "팀 진영: 불바다가 사라졌다");
    if (s.Swamp > 0 && --s.Swamp == 0) log.Add((i + 1) + "팀 진영: 습원이 사라졌다");
    if (s.Rainbow > 0 && --s.Rainbow == 0) log.Add((i + 1) + "팀 진영: 무지개가 사라졌다");
    if (s.Safeguard > 0 && --s.Safeguard == 0) log.Add((i + 1) + "팀 진영: 신비의부적이 사라졌다");
    if (s.Mist > 0 && --s.Mist == 0) log.Add((i + 1) + "팀 진영: 흰안개가 사라졌다");
    if (s.LuckyChant > 0 && --s.LuckyChant == 0) log.Add((i + 1) + "팀 진영: 주술이 풀렸다");
    if (s.WishTurns > 0 && --s.WishTurns == 0 && f[i] != null && f[i].HP > 0) Mechanics.Heal(f[i], f[1 - i], r, s.WishHeal, "희망사항", log);
   }
   if (r.WaterSport > 0 && --r.WaterSport == 0) log.Add("물놀이가 끝났다");
   if (r.MudSport > 0 && --r.MudSport == 0) log.Add("흙놀이가 끝났다");
  }
 }
}
