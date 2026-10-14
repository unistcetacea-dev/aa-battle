using System;
using System.Collections.Generic;
using System.Linq;

namespace AABattle
{
  public static partial class ModuleCatalog
  {
    // 출처가 아니라 문장이 실행하는 기능으로 분류한다(지시서 O).
    static string AbilityEffectFamily(string id)
    {
      if (id.StartsWith("critical.")) return "급소";
      if (id.StartsWith("rank.") || id == "intimidate.immune") return "능력 랭크";
      if (id.StartsWith("surehit") || id == "protect.pierce" || id == "status.protectpierce"
        || id.StartsWith("ability.pierce") && id != "ability.pierceimmune" || id == "floating.hit") return "방호·회피";
      if (id.StartsWith("ability.")) return "특성 조작";
      if (id.StartsWith("field.scalar.") || id == "fallen.scalarrate") return "수치 배율";
      if (id.StartsWith("field.") || id.StartsWith("indirect.") || id == "sturdy"
        || id.StartsWith("survive.") || id.StartsWith("recoil.") || id.StartsWith("damage.")
        || id == "guard.restore" || id == "drain.reverse") return "체력";
      if (id.StartsWith("weather.") || id == "hazard.immune" || id == "screens.clear") return "장소";
      if (id.StartsWith("type.") || id == "item.type" || id == "aura.reverse") return "타입·상성";
      if (id.StartsWith("item.") || id.StartsWith("berry.") || id == "machine.extra") return "기술·소지품";
      if (id.StartsWith("secondary.")) return "추가효과";
      if (id.StartsWith("move.") || id == "contact.remove" || id == "multihit.max"
        || id == "movelock" || id == "truant") return "기술 변형";
      if (id.StartsWith("status.") || id == "sleep.half" || id == "poison.pierce") return "상태";
      if (id.StartsWith("switch.")) return "교대";
      if (id.StartsWith("priority.")) return "우선도";
      if (id == "stats.copyhighest") return "종족치";
      if (id.StartsWith("noeffect.")) return "효과 없음";
      throw new ArgumentException("기능 분류가 없는 특성 모듈: " + id);
    }

    static void MergeAbilityChoices(List<ModuleDefinition> modules)
    {
      AbilityChoice(modules, "ability.item.ban", "소지품 사용 금지", "금지범위",
        new[] { "자신의 모든 소지품", "상대의 소비 소지품" }, "자신은 소지품을 사용할 수 없다.",
        "ability.item.ban.self", "ability.item.ban.opponent");
      AbilityChoice(modules, "ability.secondary.remove", "기술 추가효과 무효", "주체",
        new[] { "자신", "상대" }, "{주체}의 기술의 추가효과를 무효화한다.",
        "ability.secondary.remove.self", "ability.secondary.remove.opponent");
      AbilityChoice(modules, "ability.move.fail", "조건 기술 실패", "주체",
        new[] { "상대", "필드에 있는 전원" }, "{주체}의 {기술조건} 기술이 실패한다.",
        "ability.move.fail.opponent", "ability.move.fail.field");
      AbilityChoice(modules, "ability.surehit", "기술 필중", "필중범위",
        new[] { "자신의 조건 기술", "지정 타입 상대", "필드 전원" }, "자신의 {기술조건} 기술은 필중이다.",
        "ability.surehit", "ability.surehit.type", "ability.surehit.field");
      AbilityChoice(modules, "ability.recoil.condition", "기술 반동 무효", "반동범위",
        new[] { "조건 기술", "전체 기술" }, "자신의 {기술조건} 기술의 반동 대미지를 받지 않는다.",
        "ability.recoil.condition", "ability.recoil.immune");
      AbilityChoice(modules, "ability.survive.chance", "공격 버팀", "버팀방식",
        new[] { "확률", "확정" }, "자신은 빈사가 되는 공격을 {확률} HP 1로 버틴다.",
        "ability.survive.chance", "ability.sturdy");
      AbilityChoice(modules, "ability.switch.trap", "상대 통상교대 금지", "교대금지조건",
        new[] { "조건 없음", "접지", "지정 타입" }, "상대는 「통상교대」를 할 수 없다.",
        "ability.switch.trap", "ability.switch.trapground", "ability.switch.traptype");
      AbilityChoice(modules, "ability.ability.copy.opponent", "특성 복사", "복사원천",
        new[] { "상대", "직전에 빈사한 아군" }, "{복사원천}의 특성을 복사한다.",
        "ability.ability.copy.opponent", "ability.ability.copy.ally");
      AbilityChoice(modules, "ability.rank.dropimmune.all", "능력 하락 무효", "능력범위",
        new[] { "모든 능력", "지정 능력" }, "자신은 상대에 의해 능력이 내려가지 않는다.",
        "ability.rank.dropimmune.all", "ability.rank.dropimmune.named");
      // 다른 분류의 모듈과 문장 틀이 같은 쌍(지시서 O 3단계). 선택값이 원래 문장 글자를 그대로 만든다.
      AbilityChoice(modules, "effect.switch.voluntary", "아군과 임의교대", "교대방식",
        new[] { "한다", "할 수 있다" }, "아군과 임의교대{교대방식}.",
        "effect.switch.voluntary", "abaddon.switch.optional");
      AbilityChoice(modules, "effect.switch.pivot", "유턴 효과 부여", "유턴대상",
        new[] { "기술", "공격기술" }, "자신의 {유턴대상}에 「유턴」 효과를 부여한다.",
        "effect.switch.pivot", "abaddon.pivot.attack");
      // 우선도 모듈 하나: 기본 effect.priority.올린다(기술 조건 빈칸 포함)와 조건 우선도 -0.5를 선택값으로 묶는다
      AbilityChoice(modules, "effect.priority.올린다", "기술 우선도 변경", "우선도방식",
        new[] { "올린다", "-0.5" }, "{주체}의 {기술조건} 기술 우선도를 변경한다.",
        "effect.priority.올린다", "ability.priority.lag");
    }

    // TX와 같은 선택 서식 도우미. 선택값마다 원래 서식의 Fill/TryRead를 그대로 사용한다.
    // 확률·범위가 다른 선택지는 글자를 보존하므로 실행기의 서로 다른 조건도 유지된다.
    static void AbilityChoice(List<ModuleDefinition> modules, string id, string name,
      string choiceId, string[] choices, string template, params string[] originals)
    {
      var variants = originals.Select(key => modules.Single(m => m.Id == key)).ToArray();
      var choice = S(choiceId, ModuleSlotKind.Text, choices[0], choices);
      choice.Strict = true;
      var slots = new[] { choice }.Concat(variants.SelectMany(m => m.Slots)
        .GroupBy(s => s.Id).Select(g => g.First())).ToArray();
      var definition = new ModuleDefinition(id, name, ModuleKind.Effect, variants[0].Family, template, slots)
      {
        Expected = variants[0].Expected,
        AdditionalExpectedKinds = variants.SelectMany(m => m.ExpectedKinds).Distinct().ToArray(),
        AllowDuration = variants.Any(m => m.AllowDuration)
      };
      definition.Formatter = values =>
      {
        int index = Array.IndexOf(choices, values[choiceId]);
        if (index < 0) throw new ArgumentException("목록에 없는 선택값: " + values[choiceId]);
        var variant = variants[index];
        return variant.Fill(variant.Slots.ToDictionary(s => s.Id, s => values[s.Id]));
      };
      var bodyFormatter = definition.Formatter;
      var bodyDefaults = definition.Defaults();
      definition.Reader = text =>
      {
        for (int index = 0; index < variants.Length; index++)
        {
          Dictionary<string,string> read;
          if (!variants[index].TryRead(text, out read)) continue;
          var values = new Dictionary<string,string>(bodyDefaults);
          values[choiceId] = choices[index];
          foreach (var value in read) values[value.Key] = value.Value;
          if (bodyFormatter(values) == text) return values;
        }
        return null;
      };
      int insertion = modules.IndexOf(variants[0]);
      foreach (var variant in variants) modules.Remove(variant);
      modules.Insert(insertion, definition);
    }
  }
}
