using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace AABattle
{
  public static partial class ModuleCatalog
  {
    static void RegisterCoreActions(List<ModuleDefinition> modules)
    {
      foreach (string optional in new[] { "행동한다", "행동할 수 있다" })
        CoreAdd(modules, "effect.order." + optional, "먼저 " + optional, "먼저 행동", "행동", "자신은 {순서} " + optional + ".", S("순서", ModuleSlotKind.Timing, "가장 먼저", "가장 먼저", "상대보다 먼저"));
      foreach (string action in new[] { "무효화", "회피", "반사" })
        foreach (bool once in new[] { false, true })
          CoreAdd(modules, "effect.guard." + action + (once ? ".once" : ""), "상대 기술 " + action + (once ? " (1번만)" : ""), action == "회피" ? "회피" : action == "반사" ? "기술 반사" : "기술 무효화", "방호·회피", "상대의 {조건} 기술을 " + (once ? "1번만 " : "") + "{확률} " + action + "한다.", S("조건", ModuleSlotKind.MoveCondition, ""), S("확률", ModuleSlotKind.Text, "", "", "드물게", "저확률로", "중확률로"));
      foreach (bool trainer in new[] { false, true })
        CoreAdd(modules, "effect.guard.pierce." + (trainer ? "trainer" : "pokemon"), trainer ? "트레이너 방호 관통" : "방호 관통", "방호 관통", "방호·회피", "자신의 {조건} 기술이 상대의 " + (trainer ? "트레이너 " : "") + "방호 포텐셜을 무시한다.", S("조건", ModuleSlotKind.MoveCondition, ""));
      foreach (string mode in new[] { "", "같은 기술에 의한 ", "상대와 같은 기술에 의한 ", "{조건} 기술에 의한 " })
        foreach (bool periodic in new[] { false, true })
        {
          var slots = new List<ModuleSlot>();
          if (periodic) slots.Add(CoreNumber("배수", ModuleSlotKind.Count, "2"));
          slots.Add(S("순서", ModuleSlotKind.Timing, "마지막에", "먼저", "마지막에"));
          if (mode.Contains("{조건}")) slots.Add(S("조건", ModuleSlotKind.MoveCondition, "「불꽃」"));
          string id = mode == "" ? "any" : mode.StartsWith("상대") ? "opponent" : mode.StartsWith("같은") ? "same" : "condition";
          CoreAdd(modules, "effect.extra." + id + (periodic ? ".periodic" : ""), "추가행동 " + (id == "any" ? "기술 선택" : id == "same" ? "같은 기술" : id == "opponent" ? "상대와 같은 기술" : "조건 기술") + (periodic ? " (배수 T)" : ""), "추가행동", "행동", (periodic ? "「{배수}」배수 T, " : "") + "가장 {순서} " + mode + "추가행동을 얻는다.", slots.ToArray());
        }
      CoreAdd(modules, "effect.extra.reduced", "위력이 약화된 추가행동", "추가행동", "행동", "가장 {순서} 같은 기술에 의한 추가행동을 얻는다(위력 {배율}배).", S("순서", ModuleSlotKind.Timing, "마지막에", "먼저", "마지막에"), new ModuleSlot("배율", ModuleSlotKind.Multiplier, "0.5") { Minimum = 0.01M, Maximum = 0.99M, Decimals = 2 });
      foreach (string verb in new[] { "올린다", "내린다" })
        CoreAdd(modules, "effect.priority." + verb, "기술 우선도 " + verb, "우선도 변화", "행동", "{주체}의 기술 우선도를 {수치} " + verb + ".", S("주체", ModuleSlotKind.Subject, "자신", "자신", "상대", "아군", "아군 1체", "아군 전체"), CoreNumber("수치", ModuleSlotKind.Rank, "1", 7));
      foreach (string mode in new[] { "상승(강화)", "강화" })
        CoreAdd(modules, "effect.stat.ignore." + mode, "능력 " + mode + " 무시", "능력 상승·강화 무시", "능력 랭크", "{대상}의 {능력} " + mode + (mode == "강화" ? "를" : "을") + " 무시한다.", S("대상", ModuleSlotKind.Text, "상대", "상대", "『야생의 두목』"), S("능력", ModuleSlotKind.Text, "능력", "능력", "전능력치", "공격", "방어", "특공", "특방", "속도", "명중", "회피", "C", "공/특공", "방/특방", "속도/회피"));
      CoreAdd(modules, "effect.secondary.state", "상태 추가효과 부여", "추가효과 부여", "추가효과", "자신의 {조건} {범위}에 「{확률}할:{상태}」의 추가효과를 부여한다.", S("조건", ModuleSlotKind.MoveCondition, ""), S("범위", ModuleSlotKind.Text, "기술", "공격", "기술"), CoreNumber("확률", ModuleSlotKind.Count, "3", 10), S("상태", ModuleSlotKind.State, "마비"));
      CoreAdd(modules, "effect.secondary.rank", "능력 랭크 추가효과 부여", "추가효과 부여", "추가효과", "자신의 {조건} {범위}에 「{확률}할:{능력}{방향}{랭크}」의 추가효과를 부여한다.", S("조건", ModuleSlotKind.MoveCondition, ""), S("범위", ModuleSlotKind.Text, "기술", "공격", "기술"), CoreNumber("확률", ModuleSlotKind.Count, "3", 10), S("능력", ModuleSlotKind.Stats, "공격", "공격", "방어", "특공", "특방", "속도", "명중", "회피", "C", "전능력치"), S("방향", ModuleSlotKind.Text, "-", "+", "-"), CoreNumber("랭크", ModuleSlotKind.Rank, "1", 3));
      CoreAdd(modules, "effect.secondary.tokens", "복수 추가효과 부여", "추가효과 부여", "추가효과", "자신의 {조건} {범위}에 {추가효과}의 추가효과를 부여한다.", S("조건", ModuleSlotKind.MoveCondition, ""), S("범위", ModuleSlotKind.Text, "기술", "공격", "기술"), S("추가효과", ModuleSlotKind.Text, "「3할:마비」「1할:공격-1」"));
      var extension = CoreAdd(modules, "effect.extension", "기능확장 부여", "기능확장 부여", "기능확장", "{주체}이 「기능확장:{기술}」@object:기술@ 내보낼 수 있다.", S("주체", ModuleSlotKind.Subject, "자신", "자신", "아군 전원"), new ModuleSlot("기술", ModuleSlotKind.Move, "몸통박치기") { Multiple = true });
      extension.Formatter = values => FunctionExtensions.Canonical(values["주체"], values["기술"].Split('/'));
      extension.Reader = text =>
      {
        string subject;
        string[] moves;
        if (!FunctionExtensions.TryGrant(text, out subject, out moves)) return null;
        return new Dictionary<string, string> { { "주체", subject }, { "기술", string.Join("/", moves) } };
      };
    }
  }
}
