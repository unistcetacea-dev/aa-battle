using System.Collections.Generic;
using System.Linq;

namespace AABattle {
 public static partial class ModuleCatalog {
  static void RegisterAbilityMoveInputs(List<ModuleDefinition> modules) {
   AE(modules, "surehit", "필중 부여", "자신의 {기술조건} 기술은 필중이다.", AC());
   AE(modules, "surehit.type", "필중 부여", "자신의 {기술조건} 기술은 「{타입}」 타입에게 필중이다.", AC(), S("타입", ModuleSlotKind.Type, "물"));
   AE(modules, "critical.always", "반드시 급소", "자신의 {기술조건} 기술은 반드시 급소에 맞는다.", AC());
   AE(modules, "critical.rate", "급소 배율", "자신의 급소 배율을 {배율}배로 한다.", S("배율", ModuleSlotKind.Multiplier, "3"));
   AE(modules, "move.category", "기술 분류 변경", "자신의 공격기술의 분류를 「{분류}」로 한다.", S("분류", ModuleSlotKind.Text, "물리", "물리", "특수"));
   AE(modules, "move.power", "기술 위력 지정", "자신의 {기술조건} 기술의 위력을 「{위력}」로 한다.", AC(), S("위력", ModuleSlotKind.StatValue, "60"));
   AE(modules, "move.accuracy", "기술 명중 지정", "필드에 있는 전원의 기술의 명중을 「{명중}」로 한다.", S("명중", ModuleSlotKind.StatValue, "100"));
   AE(modules, "protect.pierce", "방어 관통", "자신의 {기술조건} 기술이 상대의 「방어」 기술을 관통한다.", AC("「접촉」"));
   AE(modules, "recoil.condition", "조건 반동 무효", "자신의 {기술조건} 기술의 반동 대미지를 받지 않는다.", AC("「물리」"));
   AE(modules, "priority.lag", "조건 우선도 -0.5", "자신의 {기술조건} 기술의 우선도를 -0.5한다.", AC("「변화」"));
   AE(modules, "ability.piercecondition", "조건 특성 관통", "자신의 {기술조건} 기술은 상대의 〔방호 특성〕을 관통한다.", AC("「접촉」"));
   var chance = S("확률", ModuleSlotKind.Count, "3");
   chance.Maximum = 10;
   AE(modules, "move.replace", "상대 기술 변경", "상대의 {기술조건} 기술은 {확률}할로 「기술:{기술}」이 된다.", AC(), chance, S("기술", ModuleSlotKind.Move, "몸통박치기"));
   AE(modules, "floating.hit", "공중 상대 명중", "자신의 {기술조건} 기술은 공중에 떠 있는 상대에게도 명중한다.", AC("「땅」"));
   AE(modules, "move.fail.opponent", "기술 실패", "상대의 {기술조건} 기술이 실패한다.", AC("「소리」"));
   AE(modules, "move.fail.field", "기술 실패", "필드에 있는 전원의 {기술조건} 기술이 실패한다.", AC("「자폭」"));
   AE(modules, "priority.condition", "조건 우선도", "자신의 {기술조건} 기술의 우선도를 +{우선도}한다.", AC("「변화」"), S("우선도", ModuleSlotKind.Count, "1"));
   AE(modules, "move.type", "기술 타입 변경", "자신의 {기술조건} 기술의 타입을 「{타입}」로 한다.", AC("「노말」"), S("타입", ModuleSlotKind.Type, "페어리"));
   AE(modules, "weather.lock", "날씨 고정", "날씨를 「{날씨}」로 고정한다.", S("날씨", ModuleSlotKind.Weather, "강한 비", "큰가뭄", "강한 비", "난기류"));
   // 오라·위압 등 필드에 있는 대상 전원의 계속 배율.
   AE(modules, "field.scalar.stats", "필드 전원 수치 배율", "{대상}의 「{능력}」@object:능력@ 강화({배율}배)한다.",
    S("대상", ModuleSlotKind.Subject, "필드에 있는 전원", "필드에 있는 전원", "자신을 제외한 전원"),
    S("능력", ModuleSlotKind.Stats, "공격", "공격", "방어", "특공", "특방", "속도", "명중", "회피"),
    S("배율", ModuleSlotKind.Multiplier, "1.33"));
   AE(modules, "field.scalar.위력", "필드 전원 수치 배율", "{대상}의 {기술조건} 기술의 위력을 강화({배율}배)한다.",
    S("대상", ModuleSlotKind.Subject, "필드에 있는 전원", "필드에 있는 전원", "자신을 제외한 전원"),
    AC("「악」"), S("배율", ModuleSlotKind.Multiplier, "1.33"));
  }

  static string Particle(string word) {
   char ch = (word ?? "").LastOrDefault(c => c >= '가' && c <= '힣');
   return ch == 0 || (ch - '가') % 28 == 0 ? "를" : "을";
  }

  static void RegisterAbilityStateInputs(List<ModuleDefinition> modules) {
   AE(modules, "rank.dropimmune.all", "능력 하락 무효", "자신은 상대에 의해 능력이 내려가지 않는다.");
   AE(modules, "rank.dropimmune.named", "능력 하락 무효", "자신은 상대에 의해 「{능력}」이 내려가지 않는다.", AMulti("능력", ModuleSlotKind.Stats, "공격", CatalogTriggerStats));
   AE(modules, "weather.damageimmune", "날씨 대미지 무효", "자신은 「{날씨}」의 대미지를 받지 않는다.", AMulti("날씨", ModuleSlotKind.Weather, "모래바람/싸라기눈", Mechanics.Weathers.Where(x => x != "없음").ToArray()));
   AE(modules, "status.powerimmune", "상태이상 위력 저하 무효", "「{상태}」의 기술 위력 저하 효과를 무효화한다.", AMulti("상태", ModuleSlotKind.State, "화상"));
   AE(modules, "type.add", "타입 추가", "자신의 타입에 「{타입}」를 추가한다.", S("타입", ModuleSlotKind.Type, "고스트"));
   AE(modules, "survive.chance", "확률 버팀", "자신은 빈사가 되는 공격을 {확률} HP 1로 버틴다.", S("확률", ModuleSlotKind.Probability, "드물게", "드물게", "저확률로", "중확률로"));
   AE(modules, "damage.defer", "대미지 지연", "자신은 상대의 기술의 대미지를 {확률} 자신의 행동 후로 미룬다.", S("확률", ModuleSlotKind.Probability, "저확률로", "드물게", "저확률로", "중확률로"));
   AE(modules, "status.damageimmune", "상태이상 대미지 면역", "자신은 「{상태}」의 대미지를 받지 않는다.", AMulti("상태", ModuleSlotKind.State, "독/맹독", "독", "맹독", "화상", "동상"));
   AE(modules, "switch.trap", "통상교대 금지", "상대는 「통상교대」를 할 수 없다.");
   AE(modules, "switch.trapground", "통상교대 금지", "접지한 상대는 「통상교대」를 할 수 없다.");
   AE(modules, "switch.traptype", "통상교대 금지", "「{타입}」 타입인 상대는 「통상교대」를 할 수 없다.", AMulti("타입", ModuleSlotKind.Type, "강철"));
   AE(modules, "status.random", "무작위 상태이상 부여", "상대를 「{상태}」 중 무작위 상태이상으로 한다.", AMulti("상태", ModuleSlotKind.State, "독/마비/잠듦", Mechanics.Statuses.Where(x => x != "정상").ToArray()));
   AE(modules, "rank.set", "랭크 지정", "자신의 「{능력}」 랭크를 「{랭크}」로 한다.", S("능력", ModuleSlotKind.Stats, "공격", CatalogTriggerStats.Take(5).ToArray()), S("랭크", ModuleSlotKind.Rank, "+6"));
   AE(modules, "field.healtype", "필드 타입별 체력 변화", "필드에 있는 「{타입}」 타입 전원의 체력을 최대 HP의 1/{분모} 회복한다.", S("타입", ModuleSlotKind.Type, "독"), S("분모", ModuleSlotKind.Denominator, "8"));
   AE(modules, "field.hurtothers", "필드 타입별 체력 변화", "필드에 있는 「{타입}」 타입 이외의 전원에게 최대 HP의 1/{분모} 대미지를 준다.", S("타입", ModuleSlotKind.Type, "독"), S("분모", ModuleSlotKind.Denominator, "8"));
   // 총대장 문장은 전용 비례식이지만 공용 분류는 수치 배율이다.
   AE(modules, "fallen.scalarrate", "수치 배율", "자신의 「{능력}」을 빈사 상태인 아군의 수에 비례해 강화(1.2배~1.6배)한다.", AMulti("능력", ModuleSlotKind.Stats, "공격/특공", CatalogTriggerStats.Take(5).ToArray()));
  }
 }
}
