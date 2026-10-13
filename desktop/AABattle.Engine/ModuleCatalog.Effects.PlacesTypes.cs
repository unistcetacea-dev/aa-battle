using System;
using System.Linq;
using System.Collections.Generic;

namespace AABattle
{
  public static partial class ModuleCatalog
  {
    static void RegisterCorePlaces(List<ModuleDefinition> modules)
    {
      foreach (string optional in new[] { "한다", "할 수 있다" })
      {
        CoreAdd(modules, "effect.place.weather." + optional, "날씨 변경 " + optional, "장소 전개", "장소", "날씨를 「{날씨}」@ro:날씨@ " + optional + ".", S("날씨", ModuleSlotKind.Weather, "비", Mechanics.Weathers));
        CoreAdd(modules, "effect.place.terrain." + optional, "지형 변경 " + optional, "장소 전개", "장소", "지형을 「{지형}」@ro:지형@ " + optional + ".", S("지형", ModuleSlotKind.Terrain, "그래스필드", EffectModules.TerrainNames));
        CoreAdd(modules, "effect.place.room." + optional, "장소 상태 전개 " + optional, "장소 전개", "장소", "「{룸}」을 전개" + optional + ".", S("룸", ModuleSlotKind.Room, "트릭룸", "트릭룸", "중력", "물놀이", "흙놀이"));
        CoreAdd(modules, "effect.hazard.install." + optional, "적진 설치물 설치 " + optional, "설치물", "장소", "적진에 「{설치물}」을 설치" + optional + ".", S("설치물", ModuleSlotKind.Hazard, "스텔스록", Mechanics.HazardNames));
      }
      CoreAdd(modules, "effect.hazard.clear", "자진 설치물 제거", "설치물", "장소", "자진의 설치물을 제거한다.");
      CoreAdd(modules, "effect.switch.force", "상대 강제교대", "강제교대", "교대", "상대를 강제교대시킨다.");
      CoreAdd(modules, "effect.switch.force.optional", "상대 강제교대 허용", "강제교대", "교대", "상대를 강제교대시킬 수 있다.");
      CoreAdd(modules, "effect.switch.force.immune", "강제교대 면역", "강제교대 무효", "교대", "자신은 강제교대되지 않는다.");
      CoreAdd(modules, "effect.floating", "공중 부유", "부유", "상태", "자신은 공중에 떠오른다.");
      CoreAdd(modules, "effect.switch.voluntary", "아군과 임의교대", "교대 효과", "교대", EffectNormalizer.CanonicalSwitch());
      CoreAdd(modules, "effect.switch.pivot", "유턴 효과 부여", "유턴 효과", "교대", EffectNormalizer.CanonicalPivotGrant());
      CoreAdd(modules, "effect.switch.restrict", "통상교대 외 교대 제한", "교대 제한", "교대", EffectNormalizer.CanonicalSwitchRestriction());
      CoreAdd(modules, "effect.analysis.permission", "데이터 해석 허용", "데이터 해석", "데이터", EffectNormalizer.CanonicalAnalysisPermission());
      CoreAdd(modules, "effect.analysis.immune", "데이터 해석 무효", "데이터 해석 무효", "데이터", "자신에 대한 데이터 해석을 무효화한다.");
      foreach (string prefix in new[] { "", "상대의 기술에 의해 ", "상대에 의해 " })
        CoreAdd(modules, "effect.state.immunity." + (prefix.Length == 0 ? "all" : prefix.Contains("기술") ? "move" : "opponent"), "상태 면역 " + prefix, "상태이상 면역", "상태", prefix + "자신은 「{상태}」상태가 되지 않는다.",
          new ModuleSlot("상태", ModuleSlotKind.State, "마비", StateEffects.Table.Select(x => x.Name).Concat(new[] { "상태이상", "상태변화" }).Distinct().ToArray()) { Multiple = true });
      foreach (bool ignore in new[] { false, true })
        CoreAdd(modules, "effect.state.grant." + (ignore ? "ignore" : "normal"), ignore ? "면역을 무시한 상태 부여" : "상태 부여", "상태 부여", "상태", (ignore ? "상태이상 면역을 무시하고 " : "") + "{대상}@object:대상@ 「{상태}」상태로 한다.", S("대상", ModuleSlotKind.Subject, "상대", "자신", "상대"), S("상태", ModuleSlotKind.State, "마비"));
      CoreAdd(modules, "effect.state.become", "자신 상태 변화", "상태 부여", "상태", "「{상태}」상태가 된다.", S("상태", ModuleSlotKind.State, "마비"));
      CoreAdd(modules, "effect.state.cure.all", "상태이상 전체 치유", "상태이상 치유", "상태", "{대상}의 상태이상을 치유한다.", S("대상", ModuleSlotKind.Subject, "자신", "자신", "상대", "아군", "아군 1체", "아군 전체"));
      CoreBracketList(CoreAdd(modules, "effect.state.cure.named", "지정 상태이상 치유", "상태 치유", "상태", "{대상}의 「{상태}」 상태를 치유한다.", S("대상", ModuleSlotKind.Subject, "자신", "자신", "상대"), S("상태", ModuleSlotKind.State, "혼란", Mechanics.Statuses.Where(x => x != "정상").Concat(new[] { "혼란", "상태이상" }).ToArray())), "상태");
      CoreBracketList(CoreAdd(modules, "effect.state.release", "지정 상태변화 해제", "상태 해제", "상태", "{대상}의 「{상태}」 상태를 해제한다.", S("대상", ModuleSlotKind.Subject, "자신", "자신", "상대"), S("상태", ModuleSlotKind.State, "조이기", StateEffects.Table.Where(x => x.Category != "상태이상" && x.Name != "혼란").Select(x => x.Name).ToArray())), "상태");
    }

    static void RegisterCoreTypes(List<ModuleDefinition> modules)
    {
      var condition = S("조건", ModuleSlotKind.MoveCondition, "");
      CoreAdd(modules, "effect.type.resist", "타입 내성 관통", "타입 내성 관통", "타입·상성", "자신의 {조건} 기술이 상대의 타입 내성을 관통한다.", condition);
      CoreAdd(modules, "effect.type.resist.named", "지정 타입 내성 관통", "타입 내성 관통", "타입·상성", "자신의 {조건} 기술이 「{타입}」 타입의 타입 내성을 관통한다.", condition, S("타입", ModuleSlotKind.Type, "고스트"));
      CoreAdd(modules, "effect.type.ignore", "타입 상성 무시", "타입 상성 무시", "타입·상성", "자신의 {조건} 기술이 상대의 타입 상성을 무시한다.", condition);
      CoreAdd(modules, "effect.type.immune", "상성 무효 관통", "상성 무효 관통", "타입·상성", "{주체}의 {조건} 기술이 타입 상성에 의한 무효를 관통한다.", S("주체", ModuleSlotKind.Subject, "자신", "자신", "상대"), condition);
      CoreAdd(modules, "effect.type.immune.named", "지정 타입 무효 관통", "타입 무효 관통", "타입·상성", "자신의 {조건} 기술이 「{타입}」 타입의 무효를 관통한다.", condition, S("타입", ModuleSlotKind.Type, "고스트"));
      CoreAdd(modules, "effect.type.stab", "타입 일치 배율 지정", "자속 배율", "타입·상성", "자신의 {조건} 기술의 타입 일치 배율을 「{배율}배」로 한다.", condition, S("배율", ModuleSlotKind.Multiplier, "2"));
      foreach (string optional in new[] { "한다", "할 수 있다" })
      {
        CoreAdd(modules, "effect.type.add." + optional, "유리한 타입 추가 " + optional, "타입 추가", "타입·상성", "자신의 {조건} 기술에 「{타입}」 타입을 추가해 유리한 상성으로 판정" + optional + ".", condition, S("타입", ModuleSlotKind.Type, "불꽃"));
        CoreAdd(modules, "effect.type.change." + optional, "타입 변경 " + optional, "타입 변경", "타입·상성", "{주체}의 타입을 「{타입}」@ro:타입@ " + optional + ".", S("주체", ModuleSlotKind.Subject, "상대", "자신", "상대"), S("타입", ModuleSlotKind.Type, "물"));
      }
      foreach (bool named in new[] { false, true })
        CoreAdd(modules, "effect.type.rating." + (named ? "named" : "all"), "상성 지정" + (named ? " (지정 타입)" : ""), "상성 지정", "타입·상성", (named ? "「{타입}」 타입에게 " : "") + "{주체}의 {조건} 기술의 상성을 「{상성}」@ro:상성@ 한다.",
          named ? new[] { S("타입", ModuleSlotKind.Type, "물"), S("주체", ModuleSlotKind.Subject, "자신", "자신", "상대"), condition, S("상성", ModuleSlotKind.Text, "효과가 굉장", "효과가 굉장", "효과가 별로", "효과 보통") } : new[] { S("주체", ModuleSlotKind.Subject, "자신", "자신", "상대"), condition, S("상성", ModuleSlotKind.Text, "효과가 굉장", "효과가 굉장", "효과가 별로", "효과 보통") });
      CoreAdd(modules, "effect.type.favorable", "유리한 상성 판정", "유리한 상성", "타입·상성", "자신의 기술을 유리한 상성으로 판정한다.");
      CoreAdd(modules, "effect.type.inverse", "필드 전원 타입 상성 반전", "필드 타입 상성 반전", "타입·상성", TypeRules.InverseField + ".");
      CoreAdd(modules, "effect.type.neutral", "필드 전원 타입 상성 중립", "필드 타입 상성 중립", "타입·상성", TypeRules.NeutralField + ".");
    }
  }
}

