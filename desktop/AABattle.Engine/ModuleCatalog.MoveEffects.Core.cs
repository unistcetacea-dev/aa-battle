using System;
using System.Collections.Generic;
using System.Linq;

namespace AABattle
{
  public static partial class ModuleCatalog
  {
    // 기술의 설명으로만 실행되는 문장은 포텐셜 입력에 제시하지 않는다.
    // 아래 이름은 Classify 종류와 별개인 기술 설명 처리기의 범위다.
    public static readonly Dictionary<string, string> CoreEffectExclusions = new Dictionary<string, string>
    {
      { "기술 설명:실패 조건 (FailModules)", "기술을 내기 직전의 실패 조건이며 포텐셜 효과 실행 대상이 아니다." },
      { "기술 설명:가변 위력 (VariablePower)", "기술의 기본 위력·타입 계산과 랜덤 분기이며 포텐셜 효과 실행 대상이 아니다." },
      { "기술 설명:드레인·반동", "명중으로 준 대미지와 기술 정보가 필요한 기술 전용 설명이다. 드레인 강화는 등록한다." },
      { "기술 설명:모으기·숨기", "기술 태그와 준비 턴·자동 공격을 함께 처리하는 설명이다. 모으기 생략은 등록한다." },
      { "기술 설명:명중·위력·상성·대미지 계산", "MoveModules의 기술 설명 처리이며 EffectModules.Classify의 효과 종류가 아니다." }
    };

    static void RegisterCoreMoveEffects(List<ModuleDefinition> modules)
    {
      const string family = "기술·소지품";
      CoreAdd(modules, "effect.move.drain.boost", "드레인 회복량 강화", "드레인 강화", family, "자신의 「드레인」 기술의 회복량을 {배율}배로 한다.", S("배율", ModuleSlotKind.Multiplier, "1.3"));
      CoreAdd(modules, "effect.move.bind.turns", "조이기 지속 지정", "조이기 지속", family, "자신이 거는 「조이기」의 지속 T를 {기간}T로 한다.", CoreNumber("기간", ModuleSlotKind.Turns, "5"));
      CoreAdd(modules, "effect.move.bind.damage", "조이기 대미지 지정", "조이기 대미지", family, "자신이 거는 「조이기」의 대미지를 최대 HP의 1/{분모}로 한다.", CoreNumber("분모", ModuleSlotKind.Denominator, "6"));
      CoreAdd(modules, "effect.move.repeat", "연속 사용 위력 강화", "연속 사용 강화", family, "자신이 같은 기술을 연속으로 내보낼 때마다 그 기술의 위력을 {배율}배씩 강화한다(최대 {최대배율}배).", S("배율", ModuleSlotKind.Multiplier, "0.1"), S("최대배율", ModuleSlotKind.Multiplier, "2"));
      CoreAdd(modules, "effect.move.priority.next", "다음 턴 우선도 상승", "다음 T 우선도", family, "다음 T 자신의 기술 우선도를 {수치} 올린다.", CoreNumber("수치", ModuleSlotKind.Rank, "1", 7));
      CoreAdd(modules, "effect.move.charge.skip", "모으기 생략", "모으기 생략", family, "자신의 「모으기」 기술의 모으기를 생략한다.");
      CoreAdd(modules, "effect.move.charge.skip.condition", "지정 기술 모으기 생략", "모으기 생략", family, "자신의 {조건} 기술의 모으기를 생략한다.", S("조건", ModuleSlotKind.MoveCondition, "「기술:솔라빔」"));
      CoreAdd(modules, "effect.exclusive.borrow", "아군 전용 빌려쓰기", ExclusiveBorrowModules.Classify(ExclusiveBorrowModules.Sentence), "트레이너", "아군의 『전용』을 자신의 『전용』으로 사용한다.");
      var forceCounters = CoreAdd(modules, "effect.counter.force", "『격』 강제 발동", ForcedCounterModules.Kind, "역할·트레이너", "아군의 『격』이 반드시 발동한다.");
      forceCounters.AllowDuration = true;
      CoreAdd(modules, "effect.bond.regain.chance", "유대 재사용 확률 상승", BondModules.KindRegainChance, "트레이너", BondModules.RegainChanceSentence);
      CoreAdd(modules, "effect.move.contact.ignore", "접촉 반응 무시", "접촉 반응 무시", family, "자신의 「접촉」 기술에 반응하는 효과를 받지 않는다.");
      CoreAdd(modules, "effect.move.rank.restore", "능력 하락 복구", "능력 하락 복구", family, "자신의 내려간 능력을 원래대로 되돌린다.");
      CoreAdd(modules, "effect.item.give", "소지품 상대에게 이동", "소지품 이동", family, "자신의 소지품을 상대에게 옮긴다.");
      CoreAdd(modules, "effect.field.clear", "전체 필드 상태 해제", "장 해제", family, MoveModules.ClearField + ".");
      CoreAdd(modules, "effect.wall.clear", "진영의 벽 해제", "벽 해제", family, "{진영}의 「벽」을 해제한다.", S("진영", ModuleSlotKind.Text, "적진", "자진", "적진"));
      CoreAdd(modules, "effect.analysis.execute", "데이터 해석 실행", "데이터 해석", "데이터", MoveModules.Analyze + ".");
      CoreAdd(modules, "effect.field.clear.substitute", "대타·설치물 해제", "대타·설치물 해제", family, MoveModules.ClearSubsAndHazards + ".");
      CoreAdd(modules, "effect.stockpile.heal", "비축하기 횟수에 따른 회복", "비축 회복", family, MoveModules.Swallow + ".");
      CoreAdd(modules, "effect.rank.reset", "능력 변화 초기화", "능력 변화 초기화", "능력 랭크", "{주체}의 능력 변화를 초기화한다.", S("주체", ModuleSlotKind.Subject, "전원", "자신", "상대", "전원"));
      CoreAdd(modules, "effect.critical.rank", "기술 급소 랭크 보정", "급소 보정", family, "자신의 {조건} 기술의 급소 랭크를 +{랭크}한다.", S("조건", ModuleSlotKind.MoveCondition, ""), CoreNumber("랭크", ModuleSlotKind.Rank, "1", 9));
      CoreBracketList(CoreAdd(modules, "effect.place.extend", "전개 상태 지속 연장", "지속 연장", "장소", "자신이 전개하는 「{상태}」의 지속 T를 +{기간}한다.", S("상태", ModuleSlotKind.Text, "쾌청", Mechanics.Weathers.Where(x => x != "없음").Concat(EffectModules.TerrainNames).Concat(new[] { "리플렉터", "빛의장막" }).ToArray()), CoreNumber("기간", ModuleSlotKind.Turns, "3")), "상태");
      CoreBracketList(CoreAdd(modules, "effect.weather.ignore", "날씨 영향 무시", "날씨 무시", "장소", "자신은 「{날씨}」의 영향을 받지 않는다.", S("날씨", ModuleSlotKind.Weather, "쾌청", Mechanics.Weathers.Where(x => x != "없음").ToArray())), "날씨");
      foreach (string verb in new[] { "한다", "할 수 있다" })
        CoreAdd(modules, "effect.ability.change." + verb, "특성 변경 " + verb, "특성 변경", family, "{주체}의 특성을 「{특성}」@ro:특성@ " + verb + ".", S("주체", ModuleSlotKind.Subject, "자신", "자신", "상대"), S("특성", ModuleSlotKind.Ability, "위협"));
      CoreAdd(modules, "effect.item.break", "소지품 소실", "소지품 소실", family, "자신의 소지품이 부서진다.");
      foreach (string verb in new[] { "한다", "할 수 있다" })
        CoreAdd(modules, "effect.baton.transfer." + verb, "교대 시 인계 " + verb, "교대 인계", "교대", "{주체}의 {인계}를 인계" + verb + ".", S("주체", ModuleSlotKind.Subject, "자신", "자신", "상대"), S("인계", ModuleSlotKind.Text, "「능력변화」", "「능력변화」", "「상태변화」", "「능력변화」「상태변화」"));
      CoreAdd(modules, "effect.baton.bilateral", "양방향 임의교대 인계", "양방향 임의교대 인계", "교대", BatonModules.BilateralRanks + ".");
      CoreAdd(modules, "effect.baton.first", "교대 아군 첫 기술 강화", "교대 아군 첫 기술 강화", "교대", "교대해 나온 아군이 최초로 내보내는 {조건} 기술의 위력을 강화({배율}배)한다.", S("조건", ModuleSlotKind.MoveCondition, "「불꽃」"), S("배율", ModuleSlotKind.Multiplier, "1.5"));
      CoreAdd(modules, "effect.pursuit.immune", "추격 효과 면역", "추격 효과 면역", "교대", PursuitModules.Immune + ".");
      CoreAdd(modules, "effect.pursuit.followup", "추격 추가공격", "추격 추가공격", "교대", PursuitModules.Followup + ".");
      CoreAdd(modules, "effect.hp.split", "체력 합산 후 배분", MoveMiscModules.KindPainSplit, family, MoveMiscModules.PainSplit + ".");
      CoreAdd(modules, "effect.heal.weather", "날씨별 회복량", MoveMiscModules.KindWeatherHeal, family, "자신의 체력을 날씨에 따라 최대 HP의 {악천후}·{보통}·{쾌청} 회복한다.", S("악천후", ModuleSlotKind.Text, "1/4"), S("보통", ModuleSlotKind.Text, "1/2"), S("쾌청", ModuleSlotKind.Text, "2/3"));
      CoreAdd(modules, "effect.rank.weather", "날씨별 능력 상승폭", MoveMiscModules.KindWeatherRank, family, "자신의 「{능력}」이 {기본}단계 오르고, 날씨가 「{날씨}」일 때는 {강화}단계 오른다.", S("능력", ModuleSlotKind.Stats, "공격/특공", "공격", "방어", "특공", "특방", "속도", "공격/특공", "방어/특방"), CoreNumber("기본", ModuleSlotKind.Rank, "1", 3), S("날씨", ModuleSlotKind.Weather, "쾌청", Mechanics.Weathers), CoreNumber("강화", ModuleSlotKind.Rank, "2", 3));
      CoreAdd(modules, "effect.bond.rise", "필드에 있는 한 지시 랭크 상승", BondModules.KindRise, "트레이너", "필드에 있는 한 트레이너의 지시를 「{랭크}」랭크 올린다.", CoreNumber("랭크", ModuleSlotKind.Rank, "1", 3));
      CoreAdd(modules, "effect.bond.regain", "트레이너 포텐셜 재사용 권한", BondModules.KindRegain, "트레이너", BondModules.RegainSentence + ".");
      CoreAdd(modules, "effect.bond.replay", "트레이너 포텐셜 재발동", BondModules.KindReplay, "트레이너", BondModules.ReplaySentence + ".");
      CoreAdd(modules, "effect.pursuit.grant", "추격 효과 부여", "추격 효과 부여", "교대", "자신의 {조건} 기술에 「추격」 효과를 부여한다.", S("조건", ModuleSlotKind.MoveCondition, ""));
      CoreAdd(modules, "effect.pursuit.scale", "추격 발동 배율", "추격 발동 배율", "교대", "자신이 「추격」 효과로 주는 대미지를 강화({배율}배)한다.", S("배율", ModuleSlotKind.Multiplier, "2"));
      CoreBracketList(CoreAdd(modules, "effect.potential.nullify", "이름 지정 포텐셜 무효화", PotentialNullify.Kind, "포텐셜", "{대상}의 『{이름}』을 무효화한다.", S("대상", ModuleSlotKind.Subject, "상대", "자신", "상대"), S("이름", ModuleSlotKind.Text, "투쟁본능")), "이름", "』『");
      CoreAdd(modules, "effect.potential.nullify.kind", "효과 종류별 포텐셜 무효화", PotentialNullify.ByEffectKind, "포텐셜", "{대상}의 「{효과}」 효과를 가진 포텐셜을 무효화한다.", S("대상", ModuleSlotKind.Subject, "상대", "자신", "상대"), S("효과", ModuleSlotKind.Text, "공격을 회피한다", PotentialNullify.EffectKindNames.ToArray()));
      CoreAdd(modules, "effect.release.all", "상태변화 전체 해제", "상태변화 전체 해제", family, "{주체}의 모든 상태변화를 해제한다.", S("주체", ModuleSlotKind.Subject, "상대", "자신", "상대"));
    }
  }
}

