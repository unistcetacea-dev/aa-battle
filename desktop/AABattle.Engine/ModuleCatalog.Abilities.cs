using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace AABattle {
 public static partial class ModuleCatalog {
  // 특성에서 도입한 효과도 포텐셜에 같은 표준 문장으로 입력한다. 제외 항목 없음.
  public static readonly Dictionary<string, string> AbilityEffectExclusions = new Dictionary<string, string>();

  static void AE(List<ModuleDefinition> modules, string id, string expected, string template, params ModuleSlot[] slots) {
   var definition = new ModuleDefinition("ability." + id, expected, ModuleKind.Effect, "특성·계속 효과", template, slots) { Expected = expected };
   switch (id) {
    case "item.ban.self": definition.Name = "자신의 소지품 사용 금지"; break;
    case "item.ban.opponent": definition.Name = "상대의 소비 소지품 사용 금지"; break;
    case "ability.copy.opponent": definition.Name = "상대의 특성 복사"; break;
    case "ability.copy.ally": definition.Name = "직전에 빈사한 아군의 특성 복사"; break;
    case "surehit.field": definition.Name = "필드 전원의 기술 필중"; break;
    case "surehit": definition.Name = "조건 기술에 필중 부여"; break;
    case "surehit.type": definition.Name = "지정 타입 상대에게 필중"; break;
    case "secondary.remove.self": definition.Name = "자신의 기술 추가효과 무효"; break;
    case "secondary.remove.opponent": definition.Name = "상대의 기술 추가효과 무효"; break;
    case "move.fail.opponent": definition.Name = "상대의 조건 기술 실패"; break;
    case "move.fail.field": definition.Name = "필드 전원의 조건 기술 실패"; break;
    case "rank.dropimmune.all": definition.Name = "능력 전체 하락 무효"; break;
    case "rank.dropimmune.named": definition.Name = "지정 능력 하락 무효"; break;
    case "switch.trap": definition.Name = "상대의 통상교대 금지"; break;
    case "switch.trapground": definition.Name = "접지한 상대의 통상교대 금지"; break;
    case "switch.traptype": definition.Name = "지정 타입 상대의 통상교대 금지"; break;
    case "field.healtype": definition.Name = "필드의 지정 타입 전원 회복"; break;
    case "field.hurtothers": definition.Name = "필드의 지정 타입 이외 전원 대미지"; break;
    case "fallen.scalarrate": definition.Name = "빈사 아군 수에 비례한 배율"; break;
   }
   if (id.StartsWith("field.scalar.")) definition.Name = "필드 전원의 " + id.Substring("field.scalar.".Length) + " 배율";
   definition.Formatter = values => {
    string filled = Regex.Replace(template, @"\{([^{}]+)\}", m => values[m.Groups[1].Value]);
    filled = Regex.Replace(filled, @"[ \t]+", " ").Trim();
    // 타입·숫자의 조사만 공용 표준 조사에 맞춘다.
    filled = Regex.Replace(filled, "「([^」]+)」로 한다", m => "「" + m.Groups[1].Value + "」" + EffectModules.Ro(m.Groups[1].Value) + " 한다");
    if (EffectModules.Classify(filled) != expected) throw new ArgumentException("이 빈칸 조합은 효과 모듈로 읽을 수 없습니다.");
    return filled;
   };
   var formatter = definition.Formatter;
   definition.Reader = text => {
    if (EffectModules.Classify(text) != expected) return null;
    // 선택지·숫자 모양으로 먼저 읽고(엄격), 안 맞으면 느슨하게 읽는다.
    foreach (bool strict in new[] { true, false }) {
     string pattern = "^";
     int position = 0;
     foreach (Match token in Regex.Matches(template, @"\{([^{}]+)\}")) {
      pattern += AbilityLiteral(template.Substring(position, token.Index - position));
      int i = Array.FindIndex(slots, s => s.Id == token.Groups[1].Value);
      pattern += "(?<v" + i + ">" + (strict ? StrictSlotPattern(slots[i]) : ".*?") + ")";
      position = token.Index + token.Length;
     }
     pattern += AbilityLiteral(template.Substring(position)) + "$";
     var match = Regex.Match((text ?? "").Trim(), pattern);
     if (!match.Success) continue;
     var values = slots.Select((s, i) => new { s.Id, Value = match.Groups["v" + i].Value.Trim() }).ToDictionary(x => x.Id, x => x.Value);
     if (formatter(values) == text.Trim()) return values;
    }
    return null;
   };
   modules.Add(definition);
  }

  static string AbilityLiteral(string text) {
   return Regex.Escape(text).Replace(@"\ ", @"\s*").Replace("」로", "」(?:으로|로)");
  }

  static ModuleSlot AC(string value = "") { return S("기술조건", ModuleSlotKind.MoveCondition, value); }
  static ModuleSlot AMulti(string id, ModuleSlotKind kind, string value, params string[] choices) {
   var slot = S(id, kind, value, choices);
   slot.Multiple = true;
   return slot;
  }

  static partial void RegisterAbilityEffects(List<ModuleDefinition> modules) {
   RegisterAbilityConstants(modules);
   RegisterAbilityMoveInputs(modules);
   RegisterAbilityStateInputs(modules);
  }

  static void RegisterAbilityConstants(List<ModuleDefinition> modules) {
   var constants = new[] {
    new[] { "critical.zero", "급소 랭크 보정 0", AbilityCriticalFieldModules.CriticalRankZero },
    new[] { "critical.immune", "급소 무효", AbilityPhrases.NoCritical },
    new[] { "sturdy", "반드시 버팀", AbilityPhrases.Sturdy },
    new[] { "indirect.immune", "간접 대미지 무효", AbilityPhrases.MagicGuard },
    new[] { "hazard.immune", "설치물 무시", AbilityPhrases.HazardProof },
    new[] { "ability.pierce", "특성 관통", AbilityPhrases.Pierce },
    new[] { "rank.extra", "능력 변화량 추가", AbilityPhrases.ExtraStage },
    new[] { "rank.reverse", "능력 변화 반전", AbilityPhrases.ReverseStage },
    new[] { "rank.reflect", "능력 하락 반사", AbilityPhrases.ReflectStage },
    new[] { "weather.cancel", "날씨 효과 무효", AbilityPhrases.NoWeather },
    new[] { "status.protectpierce", "변화기술 방어 관통", AbilityPhrases.StatusProtectPierce },
    new[] { "poison.pierce", "독 타입 면역 관통", AbilityPhrases.PoisonPierce },
    new[] { "ability.suppress", "상대 특성 무효", AbilityPhrases.SuppressAbility },
    new[] { "type.move", "기술 타입으로 변화", AbilityPhrases.MatchMoveType },
    new[] { "recoil.immune", "기술 반동 무효", AbilityPhrases.NoRecoil },
    new[] { "item.take", "소지품 약탈", AbilityItemModules.Take },
    new[] { "item.break", "소지품 파괴", AbilityItemModules.Break },
    new[] { "item.frisk", "소지품 해석", AbilityItemModules.Frisk },
    new[] { "berry.use", "나무열매 사용", AbilityItemModules.UseBerry },
    new[] { "berry.recycle", "나무열매 되돌려받기", AbilityItemModules.Recycle },
    new[] { "berry.replay", "나무열매 효과 다시 받기", AbilityItemModules.Replay },
    new[] { "berry.ripen", "나무열매 효과 2배", AbilityItemModules.RipenRule },
    new[] { "item.type", "소지품 타입 변화", AbilityItemModules.PlateType },
    new[] { "item.ban.self", "소지품 사용 금지", AbilityItemModules.NoItems },
    new[] { "item.ban.opponent", "소지품 사용 금지", AbilityItemModules.NoConsumedItems },
    new[] { "item.protect", "소지품 보호", AbilityItemModules.ProtectItem },
    new[] { "type.copy", "타입 복사", AbilityEventModules.CopyType },
    new[] { "type.received", "받은 기술 타입으로 변화", AbilityEventModules.TakenType },
    new[] { "status.reflect", "상태이상 반사", AbilityEventModules.MirrorStatus },
    new[] { "rank.follow", "랭크 따라가기", AbilityEventModules.FollowRise },
    new[] { "damage.return", "받은 대미지 반격", AbilityEventModules.ReturnDamage },
    new[] { "screens.clear", "전체 필드 벽 해제", AbilityEventModules.ClearScreens },
    new[] { "guard.restore", "1번만 효과 복구", AbilityEventModules.RestoreGuard },
    new[] { "ability.swap", "특성 교환", AbilityChangeModules.Swap },
    new[] { "ability.copy.opponent", "특성 복사", AbilityChangeModules.CopyOpponent },
    new[] { "ability.copy.ally", "특성 복사", AbilityChangeModules.CopyFaintedAlly },
    new[] { "ability.changeimmune", "특성 변경 불가", AbilityChangeModules.ImmuneChange },
    new[] { "ability.suppressimmune", "특성 무효화 불가", AbilityChangeModules.ImmuneSuppress },
    new[] { "ability.pierceimmune", "특성 관통 불가", AbilityChangeModules.ImmuneBypass },
    new[] { "noeffect.multi", "다인전 전용 효과 없음", AbilityNoEffectModules.MultiBattle },
    new[] { "noeffect.outside", "배틀 밖 효과 없음", AbilityNoEffectModules.OutOfBattle },
    new[] { "surehit.field", "필중 부여", AbilityMoveModules.FieldSureHit },
    new[] { "contact.remove", "접촉 취급 해제", AbilityMoveModules.NotContact },
    new[] { "secondary.remove.self", "추가효과 무효", AbilityMoveModules.NoOwnSecondary },
    new[] { "secondary.remove.opponent", "추가효과 무효", AbilityMoveModules.NoTakenSecondary },
    new[] { "secondary.double", "추가효과 확률 배율", AbilityMoveModules.DoubleSecondary },
    new[] { "multihit.max", "연속기 최대 횟수", AbilityMoveModules.FullMultiHit },
    new[] { "truant", "행동 후 쉼", AbilityMoveModules.Truant },
    new[] { "movelock", "기술 고정", AbilityStateModules.MoveLock },
    new[] { "sleep.half", "잠듦 기간 절반", AbilityStateModules.SleepHalf },
    new[] { "indirect.survive", "간접 대미지 빈사 방지", AbilityStateModules.SurviveIndirect },
    new[] { "drain.reverse", "드레인 반전", AbilityStateModules.ReverseDrain },
    new[] { "aura.reverse", "오라 반전", AbilityStateModules.AuraBreak },
    new[] { "rank.riseagain", "올라간 만큼 올림", AbilityRemaining4Modules.RiseAgain },
    new[] { "intimidate.immune", "위협 하락 무효", "상대의 〔위협〕에 의한 능력 하락을 무효화한다" },
    new[] { "machine.extra", "기술머신 추가행동", "가장 마지막에 소지품 「기술머신」의 기술에 의한 추가행동을 얻을 수 있다" },
    new[] { "stats.copyhighest", "상대 종족치 복사", "상대의 가장 높은 종족치를 자신의 같은 능력에 복사한다" },
    new[] { "item.share", "소지품 효과 공유", "그 소지품의 효과를 자신도 받는다" }
   };
   foreach (var item in constants) AE(modules, item[0], item[1], item[2] + ".");
  }
 }
}
