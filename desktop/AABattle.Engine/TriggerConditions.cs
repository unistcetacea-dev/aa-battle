using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace AABattle {
 // 트리거 모듈 3단계: 적진·아군 구성 · 진영 · 소지품 · 특성 · 포텐셜 보유 · 트레이너 조건 · 플레이버 · 행동 선택.
 // 확정 25(진영 효과), 26(설치 여부·층수), 30·31(현재 소지품), 33(현재 특성, 무효여도 보유), 34(같은 이름 포텐셜도 보유),
 // 36(대기 포함·빈사 제외·이름), 37(자신만 생존), 38(현재 빈사 수), 39(인물·성별·소속 = 상시), 12(공격하지 않음: 변화기·교대 선택 포함), 18(능력 변화 합)
 public static partial class TriggerModules {
  // 이미 사건 지점이 있는 트리거(라벨·절 판정 그대로). 모듈로 인식만 한다
  static readonly Dictionary<string, string> LabelModules = new Dictionary<string, string> {
   { TriggerStructures.BattleStartLabel, "배틀 시작" }, { TriggerStructures.SelfOrderLabel, "지령" }, { TriggerStructures.AllyOrderLabel, "지령" },
   { TriggerStructures.AnalyzedLabel, "해석" }, { TriggerStructures.ReceivedOrNullifiedLabel, "피격·무효화" }, { TriggerStructures.DamagedLabel, "대미지" },
   { "서로 공격기를 선택했을 때", "행동 선택" }, { "서로 변화기술을 선택했을 때", "행동 선택" }, { "서로 필중 기술을 선택했을 때", "행동 선택" }, { "자신이 공격하지 않는 행동을 선택했을 때", "행동 선택" }, { "상대가 공격하지 않는 행동을 선택했을 때", "행동 선택" },
   { "「방어」 등의 기술로 상대의 공격을 무효화했을 때", "방어 성공" }, { "「방어」 등의 기술로 상대의 기술을 무효화했을 때", "방어 성공" }, { "자신의 방어 상태가 공격을 막았을 때", "방어 성공" },
   { "상대가 공격 이외의 대미지를 받았을 때", "간접 대미지" }, { "상대의 체력을 드레인했을 때", "드레인" }
  };
  public static readonly string[] ConditionModules = { "행동 선후", "상시", "PT 참가", "여기다", "해석 상태", "부여", "적진 타입", "트레이너 자질", "플레이버", "포텐셜 보유", "소지품", "특성", "생존", "빈사 수", "진영", "공격하지 않음", "능력 변화 합" };
  static readonly string[] SideNames = { "스텔스록", "압정", "독압정", "끈적끈적네트", "순풍", "리플렉터", "빛의장막" };

  static bool TryParseCondition(string t, out TriggerSpec spec) {
   spec = null; Match m;
   string label;
   if (LabelModules.TryGetValue(t, out label)) { spec = new TriggerSpec { Module = label, Place = t }; return true; }
   if (Regex.IsMatch(t, @"^「?방어」?\s*등의\s*기술로\s*상대의\s*(?:공격|공격기술|기술)을\s*무효화했을\s*때$") || Regex.IsMatch(t, @"^상대의\s*공격을\s*「?방어」?\s*등의\s*기술로\s*무효화했을\s*때$")) {
    string text = t.Contains("기술을") && !t.Contains("공격기술") ? "「방어」 등의 기술로 상대의 기술을 무효화했을 때" : "「방어」 등의 기술로 상대의 공격을 무효화했을 때";
    spec = new TriggerSpec { Module = "방어 성공", Place = text }; return true;
   }
   if (t == TriggerStructures.FieldLabel || t == TriggerStructures.AlwaysLabel) { spec = new TriggerSpec { Module = "상시", Place = t }; return true; }
   if (t == TriggerStructures.ObserverLabel) { spec = new TriggerSpec { Module = "PT 참가", Place = t }; return true; }
   // 적진 타입(「」는 템플릿 타입 자리)
   m = Regex.Match(t, @"^(?:적진에\s*「(?<t>[^」]*)」\s*(?:타입\s*)?(?:의\s*)?포켓몬이\s*(?:있을|있는)\s*(?:때|한)|상대가\s*「(?<t>[^」]+)」\s*타입일\s*때)$");
   if (m.Success) { string type = m.Groups["t"].Value.Trim(); if (type.Length > 0 && !type.Split('/').All(MoveCondition.TypeNames.Contains)) return false; spec = new TriggerSpec { Module = "적진 타입", Names = type.Length == 0 ? new string[0] : type.Split('/') }; return true; }
   // 트레이너 자질(기존 판정: 같은 등급대)
   m = Regex.Match(t, @"^(?<o>상대\s*)?트레이너가\s*「(?<a>지시|육성|통솔|능력)\s*[:：]\s*(?<r>[A-Za-z]+)[-+]?」\s*이상일\s*때$");
   if (m.Success) { spec = new TriggerSpec { Module = "트레이너 자질", Subject = m.Groups["o"].Success ? "상대" : "자신", Names = new[] { m.Groups["a"].Value, m.Groups["r"].Value.ToUpperInvariant() } }; return true; }
   // 플레이버(확정 39: 상시)
   m = Regex.Match(t, @"^소속이\s*《\s*(?<v>[^》]+?)\s*》\s*일\s*때$");
   if (m.Success) { spec = new TriggerSpec { Module = "플레이버", Place = "소속이 《" + m.Groups["v"].Value + "》일 때" }; return true; }
   m = Regex.Match(t, @"^「(?<v>[^」]+)」(?:이|가)\s*트레이너일\s*때$");
   if (m.Success) { spec = new TriggerSpec { Module = "플레이버", Place = "「" + m.Groups["v"].Value + "」" + Particle(m.Groups["v"].Value, "이", "가") + " 트레이너일 때" }; return true; }
   m = Regex.Match(t, @"^트레이너가\s*「(?<v>남성|여성)」\s*일\s*때$");
   if (m.Success) { spec = new TriggerSpec { Module = "플레이버", Place = "트레이너가 「" + m.Groups["v"].Value + "」일 때" }; return true; }
   // 「여기다！」(진영 기록)
   if (t == TriggerStructures.HereLabel || Regex.IsMatch(t, @"^(?:필드가\s*)?「여기다\s*[!！]?」\s*일\s*때$")) { spec = new TriggerSpec { Module = "여기다" }; return true; }
   // 해석(확정 42·43: 진영의 완전 해석 기록)
   if (Regex.IsMatch(t, @"^(?:적진에\s*데이터가\s*해석된\s*상대가\s*있을\s*때|상대의\s*데이터가\s*(?:이미\s*)?해석된\s*상태일\s*때)$")) { spec = new TriggerSpec { Module = "해석 상태", Op = "상대 해석됨" }; return true; }
   if (Regex.IsMatch(t, @"^자신의\s*데이터가\s*해석되지\s*않았을\s*때$")) { spec = new TriggerSpec { Module = "해석 상태", Op = "자신 해석 안 됨" }; return true; }
   if (Regex.IsMatch(t, @"^자신의\s*데이터가\s*해석되어\s*있을\s*때$")) { spec = new TriggerSpec { Module = "해석 상태", Op = "자신 해석됨" }; return true; }
   if (Regex.IsMatch(t, @"^(?:자신의\s*데이터가\s*해석되었을\s*때|자신이\s*「?해석」?\s*됐을\s*때)$")) { spec = new TriggerSpec { Module = "해석당함" }; return true; }
   // 부여된 포텐셜(『계약의 특권』 등)
   m = Regex.Match(t, @"^『(?<v>[^』]+)』(?:이|가)\s*부여되어\s*있을\s*때$");
   if (m.Success) { spec = new TriggerSpec { Module = "부여", Names = new[] { m.Groups["v"].Value.Trim() } }; return true; }
   // 포텐셜 보유(확정 34)
   m = Regex.Match(t, @"^(?:자신이\s*)?『(?<v>[^』]+)』\s*(?:을|를)\s*보유하고\s*있을\s*때$");
   if (!m.Success) m = Regex.Match(t, @"^(?:자신이\s*)?『(?<v>[^』]+)』\s*일\s*때$");
   if (m.Success) { spec = new TriggerSpec { Module = "포텐셜 보유", Names = new[] { m.Groups["v"].Value.Trim() } }; return true; }
   // 소지품(확정 30·31)
   if (Regex.IsMatch(t, @"^(?:자신이\s*)?(?:「?소지품」?(?:을|를)\s*가지고\s*있을\s*때|소지품이\s*있을\s*때)$")) { spec = new TriggerSpec { Module = "소지품" }; return true; }
   m = Regex.Match(t, @"^(?:(?<s>자신|상대)(?:이|가|의)\s*)?(?:「?소지품」?(?:을|를)\s*가지고\s*있지\s*않을\s*때|소지품이\s*없을\s*때)$");
   if (m.Success) { spec = new TriggerSpec { Module = "소지품", Op = "없음", Subject = m.Groups["s"].Success ? m.Groups["s"].Value : "자신" }; return true; }
   m = Regex.Match(t, @"^(?:(?<s>자신|상대)의\s*)?소지품이\s*「(?<v>[^」]+)」\s*일\s*때$");
   if (!m.Success) m = Regex.Match(t, @"^(?:(?<s>자신|상대)(?:이|가)\s*)?「(?<v>[^」]+)」\s*소지품을\s*가지고\s*있을\s*때$");
   if (m.Success) { spec = new TriggerSpec { Module = "소지품", Subject = m.Groups["s"].Success ? m.Groups["s"].Value : "자신", Names = new[] { m.Groups["v"].Value.Trim() } }; return true; }
   // 특성(확정 33)
   m = Regex.Match(t, @"^(?:(?<s>자신|상대)의\s*)?특성이\s*「(?<v>[^」]+)」\s*일\s*때$");
   if (!m.Success) m = Regex.Match(t, @"^(?:(?<s>자신|상대)(?:이|가)\s*)?「(?<v>[^」]+)」\s*특성을\s*가지고\s*있을\s*때$");
   if (m.Success) { spec = new TriggerSpec { Module = "특성", Subject = m.Groups["s"].Success ? m.Groups["s"].Value : "자신", Names = new[] { m.Groups["v"].Value.Trim() } }; return true; }
   // 행동 선후(교대는 행동이 아니다, 사용자 결정)
   m = Regex.Match(t, @"^(?<s>상대|자신)(?:이|가)\s*(?:자신|상대)?(?:보다)?\s*먼저\s*행동(?<v>할|했을)\s*때$");
   if (m.Success) { bool opp = m.Groups["s"].Value == "상대"; spec = m.Groups["v"].Value == "할" ? new TriggerSpec { Module = "행동 선후", Op = opp ? "상대 먼저" : "자신 먼저" } : new TriggerSpec { Module = "먼저 행동함", Opponent = opp }; return true; }
   // 무효화(흡수 특성 등): 상대의 「X」 기술을 무효화했을 때
   m = Regex.Match(t, @"^상대의\s*(?<c>(?:「[^」]+」)+)\s*(?:타입\s*)?(?:기술|공격)을\s*무효화했을\s*때$");
   MoveCondition nullified;
   if (m.Success && MoveCondition.TryParse(m.Groups["c"].Value, out nullified)) { spec = new TriggerSpec { Module = "무효화", Condition = nullified }; return true; }
   // 빗나감(명중 판정으로 빗나갔을 때)
   if (Regex.IsMatch(t, @"^(?:자신의\s*기술이|(?:명중률에\s*의해\s*)?기술이)\s*빗나갔을\s*때$")) { spec = new TriggerSpec { Module = "빗나감" }; return true; }
   // 특성 발동(확정 15: 실제로 발동한 순간)
   m = Regex.Match(t, @"^(?:자신의\s*)?(?:특성\s*「(?<v>[^」]+)」(?:이|가)|「(?<v>[^」]+)」\s*특성이|특성이)\s*발동했을\s*때$");
   if (m.Success) { spec = new TriggerSpec { Module = "특성 발동", Names = m.Groups["v"].Success ? new[] { m.Groups["v"].Value.Trim() } : new string[0] }; return true; }
   // 생존·빈사 수(확정 36~38)
   if (Regex.IsMatch(t, @"^(?:자신만\s*생존해\s*있을\s*때|「?최후의\s*1체」?\s*일\s*때)$")) { spec = new TriggerSpec { Module = "생존", Op = "최후" }; return true; }
   m = Regex.Match(t, @"^(?:아군에|PT에)\s*「(?<v>[^」]+)」(?:이|가)\s*(?:생존해\s*)?있을\s*때$");
   if (m.Success) { spec = new TriggerSpec { Module = "생존", Names = new[] { m.Groups["v"].Value.Trim() } }; return true; }
   m = Regex.Match(t, @"^(?:아군의\s*(?:「빈사」\s*상태인\s*포켓몬의\s*수|빈사\s*수)|「빈사」\s*(?:상태)?인\s*아군(?:의\s*수)?)(?:가|이)\s*상대(?:보다|와)\s*(?<o>많을|같을|적을)\s*때$");
   if (m.Success) { spec = new TriggerSpec { Module = "빈사 수", Op = m.Groups["o"].Value }; return true; }
   // 진영(확정 25·26)
   m = Regex.Match(t, @"^(?<s>적진|자진|상대\s*진영|자신의\s*진영|필드)(?:에|이|가|의)\s*「(?<v>[^」]+)」(?:이|가)?\s*(?<v2>있을\s*때|설치되어\s*있을\s*때|일\s*때|(?<n>\d+)\s*겹\s*이상일\s*때)$");
   if (m.Success) {
    string name = m.Groups["v"].Value.Trim(); if (name == "압정뿌리기") name = "압정";
    if (!SideNames.Contains(name)) return false;
    string side = m.Groups["s"].Value.Replace(" ", ""); side = side == "적진" || side == "상대진영" ? "상대" : side == "필드" ? "양쪽" : "자신";
    spec = new TriggerSpec { Module = "진영", Subject = side, Names = new[] { name }, Top = m.Groups["n"].Success ? int.Parse(m.Groups["n"].Value) : 0 };
    return true;
   }
   // 공격하지 않음(확정 12, 타이밍 규칙 6: 그 턴 동안)
   m = Regex.Match(t, @"^(?<s>자신|상대)(?:이|가)\s*공격(?:을)?\s*하지\s*않을\s*때$");
   if (m.Success) { spec = new TriggerSpec { Module = "공격하지 않음", Subject = m.Groups["s"].Value }; return true; }
   // 능력 변화 합(확정 18)
   m = Regex.Match(t, @"^(?:자신의\s*)?능력\s*(?:변화|상승합)?(?:가|이)\s*「?(?<sign>[+-])(?<n>\d*)」?\s*(?<o>이상|이하)?\s*일\s*때$");
   if (m.Success) { int n = m.Groups["n"].Value.Length == 0 ? 1 : int.Parse(m.Groups["n"].Value); spec = new TriggerSpec { Module = "능력 변화 합", Top = m.Groups["sign"].Value == "+" ? n : -n }; return true; }
   return false;
  }

  static string CanonicalCondition(TriggerSpec s) {
   if (LabelModules.ContainsValue(s.Module) || s.Module == "상시" || s.Module == "PT 참가" || s.Module == "플레이버") return s.Place;
   switch (s.Module) {
    case "적진 타입": return "적진에 「" + string.Join("/", s.Names) + "」 포켓몬이 있을 때";
    case "트레이너 자질": return (s.Subject == "상대" ? "상대 " : "") + "트레이너가 「" + s.Names[0] + ":" + s.Names[1] + "」 이상일 때";
    case "부여": return TriggerStructures.Granted(s.Names[0]);
    case "여기다": return TriggerStructures.HereLabel;
    case "해석 상태": return s.Op == "상대 해석됨" ? "적진에 데이터가 해석된 상대가 있을 때" : s.Op == "자신 해석 안 됨" ? "자신의 데이터가 해석되지 않았을 때" : "자신의 데이터가 해석되어 있을 때";
    case "해석당함": return "자신의 데이터가 해석되었을 때";
    case "포텐셜 보유": return "자신이 『" + s.Names[0] + "』" + Particle(s.Names[0], "을", "를") + " 보유하고 있을 때";
    case "소지품": if (s.Op == "없음") return s.Subject + (s.Subject == "상대" ? "가" : "이") + " 소지품을 가지고 있지 않을 때"; return s.Names.Length == 0 ? "자신이 소지품을 가지고 있을 때" : Of(s.Subject) + " 소지품이 「" + s.Names[0] + "」일 때";
    case "특성": return Of(s.Subject) + " 특성이 「" + s.Names[0] + "」일 때";
    case "빗나감": return "자신의 기술이 빗나갔을 때";
    case "무효화": return "상대의 " + s.Condition.Text + " 기술을 무효화했을 때";
    case "행동 선후": return s.Op == "상대 먼저" ? "상대가 자신보다 먼저 행동할 때" : "자신이 상대보다 먼저 행동할 때";
    case "먼저 행동함": return s.Opponent ? "상대가 자신보다 먼저 행동했을 때" : "자신이 상대보다 먼저 행동했을 때";
    case "특성 발동": return s.Names.Length == 0 ? "자신의 특성이 발동했을 때" : "자신의 「" + s.Names[0] + "」 특성이 발동했을 때";
    case "생존": return s.Op == "최후" ? "자신만 생존해 있을 때" : "아군에 「" + s.Names[0] + "」" + Particle(s.Names[0], "이", "가") + " 생존해 있을 때";
    case "빈사 수": return "아군의 빈사 수가 상대보다 " + s.Op + " 때";
    case "진영":
     string side = s.Subject == "상대" ? "적진" : s.Subject == "양쪽" ? "필드" : "자진";
     return s.Top > 0 ? side + "의 「" + s.Names[0] + "」" + Particle(s.Names[0], "이", "가") + " " + s.Top + "겹 이상일 때" : side + "에 「" + s.Names[0] + "」" + Particle(s.Names[0], "이", "가") + " 있을 때";
    case "공격하지 않음": return Owner(s.Subject) + " 공격하지 않을 때";
    case "능력 변화 합": return "자신의 능력 변화가 " + (s.Top > 0 ? "+" + s.Top + " 이상" : s.Top + " 이하") + "일 때";
   }
   return CanonicalAbilityState(s);
  }

  static int SideOf(Fighter f) { return f == null ? -1 : f.FieldSide; }
  static TrainerRecord TrainerOf(int side) {
   var scope = EffectScope.Current;
   return scope == null || scope.States == null || side < 0 || side >= scope.States.Length || scope.States[side] == null ? null : scope.States[side].Trainer;
  }
  static IEnumerable<Fighter> Party(Rules rules, int side, Fighter current) {
   var list = rules != null && side >= 0 && side < rules.Parties.Length && rules.Parties[side] != null && rules.Parties[side].Length > 0 ? rules.Parties[side] : new[] { current };
   return list.Where(x => x != null);
  }
  static int SideValue(SideFieldState state, string name) {
   switch (name) {
    case "스텔스록": return state.StealthRock ? 1 : 0;
    case "끈적끈적네트": return state.StickyWeb ? 1 : 0;
    case "압정": return state.Spikes;
    case "독압정": return state.ToxicSpikes;
    case "순풍": return state.Tailwind > 0 ? 1 : 0;
    case "리플렉터": return state.Reflect > 0 ? 1 : 0;
    case "빛의장막": return state.LightScreen > 0 ? 1 : 0;
    case "얼음의벽": return state.IceWall > 0 ? 1 : 0;
   }
   return 0;
  }

  static bool HoldsCondition(TriggerSpec s, Fighter owner, Fighter opponent, Rules rules) {
   var target = s.Subject == "상대" ? opponent : owner;
   int side = SideOf(owner);
   switch (s.Module) {
    case "상시": return s.Place == TriggerStructures.AlwaysLabel || owner.HP > 0 && owner.OnField;
    case "플레이버": return owner.HP > 0;
    case "PT 참가": return false;   // 대기 멤버의 관측 효과는 RuntimeTriggers.Observers가 따로 다룬다
    case "적진 타입": return s.Names.Length > 0 && opponent != null && opponent.HP > 0 && opponent.OnField && (opponent.Data.types ?? new string[0]).Any(s.Names.Contains);
    case "트레이너 자질": {
     var trainer = TrainerOf(s.Subject == "상대" ? 1 - side : side);
     if (trainer == null) return false;
     var value = s.Names[0] == "지시" ? trainer.command : s.Names[0] == "육성" ? trainer.training : s.Names[0] == "통솔" ? trainer.leadership : trainer.ability;
     return value != null && TriggerStructures.RankBand(value.rank, s.Names[1]);
    }
    case "행동 선후": {
     int mine = owner.Conditions.ActionOrder, theirs = opponent == null ? 0 : opponent.Conditions.ActionOrder;
     return s.Op == "상대 먼저" ? theirs > 0 && (mine == 0 || theirs < mine) : mine > 0 && (theirs == 0 || mine < theirs);
    }
    case "여기다": { var state = BattleRecords.StateOf(owner); return state != null && state.Here; }
    case "해석 상태": {
     if (s.Op == "상대 해석됨") return opponent != null && opponent.OnField && BattleRecords.IsAnalyzed(BattleRecords.StateOf(owner), opponent);
     var analyzer = opponent == null ? null : BattleRecords.StateOf(opponent);
     if (analyzer == null) { var scope = EffectScope.Current; int other = 1 - owner.FieldSide; analyzer = scope == null || scope.States == null || other < 0 || other >= scope.States.Length ? null : scope.States[other]; }
     bool analyzed = BattleRecords.IsAnalyzed(analyzer, owner);
     return s.Op == "자신 해석됨" ? analyzed : !analyzed;
    }
    case "부여":
    case "포텐셜 보유": return (owner.Data.potentials ?? new Potential[0]).Any(p => p != null && (p.name == s.Names[0] || (p.name ?? "").StartsWith(s.Names[0] + " (") || (p.name ?? "").StartsWith(s.Names[0] + "（")));
    case "소지품": if (s.Op == "없음") return target != null && string.IsNullOrEmpty(target.Data.item); return target != null && (s.Names.Length == 0 ? !string.IsNullOrEmpty(target.Data.item) : target.Data.item == s.Names[0]);
    case "특성": return target != null && AbilityRuntime.Name(target) == s.Names[0];
    case "생존": {
     var party = Party(rules, side, owner).ToList();
     if (s.Op == "최후") return owner.HP > 0 && party.Count(x => x.HP > 0) == 1;
     return party.Any(x => x.HP > 0 && x.Data.name == s.Names[0]);
    }
    case "빈사 수": {
     int mine = Party(rules, side, owner).Count(x => x.HP <= 0), theirs = Party(rules, 1 - side, opponent).Count(x => x.HP <= 0);
     return s.Op == "많을" ? mine > theirs : s.Op == "같을" ? mine == theirs : mine < theirs;
    }
    case "진영": {
     if (rules == null || side < 0 || side > 1) return false;
     var sides = s.Subject == "양쪽" ? new[] { 0, 1 } : new[] { s.Subject == "상대" ? 1 - side : side };
     return sides.Any(i => SideValue(rules.Sides[i], s.Names[0]) >= Math.Max(1, s.Top));
    }
    case "공격하지 않음": return target != null && target.Conditions.NotAttacking;
    case "능력 변화 합": {
     int total = (owner.Stages ?? new int[0]).Skip(1).Sum() + owner.AccuracyStage + owner.EvasionStage;
     return s.Top > 0 ? total >= s.Top : total <= s.Top;
    }
   }
   return HoldsAbilityState(s, owner, opponent, rules);
  }

  static bool MatchesCondition(TriggerSpec s, TriggerFact f) {
   if (s.Module == "특성 발동") return f.Kind == "특성 발동" && f.Self && (s.Names.Length == 0 || s.Names.Contains(f.Method));
   if (s.Module == "해석당함") return f.Kind == "해석당함" && f.Self;
   if (s.Module == "빗나감") return f.Kind == "빗나감" && f.Self;
   if (s.Module == "무효화") return f.Kind == "무효화" && f.Self && f.Move != null && s.Condition.Matches(f.Move, f.User, 1);
   if (s.Module == "먼저 행동함") return f.Kind == "먼저 행동함" && f.Self != s.Opponent;
   return MatchesAbilityEvent(s, f);
  }
 }
}
