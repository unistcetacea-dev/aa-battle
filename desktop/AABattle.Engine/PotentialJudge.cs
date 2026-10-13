// 포텐셜 문장 하나가 엔진에서 실행되는지 판정한다(트리거 모듈 + 효과 실행기).
// 연구용 도구다: 시트에서 뽑은 문장을 돌려 "자동 / 정규화 필요 / 새 모듈 필요"를 가른다.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace AABattle
{
  public sealed class JudgeStep
  {
    public string Line = "";
    public string Kind = ""; // 실행기 이름 / 모듈 이름 / 빈 문자열(읽지 못함)
    public bool Ok;
  }

  public sealed class JudgeResult
  {
    public string Trigger = "";
    public string Effect = "";
    public string Activation = "";
    public bool TriggerOk;
    public string TriggerModules = "";
    public List<string> BadTriggers = new List<string>();
    public List<JudgeStep> Steps = new List<JudgeStep>();
    public bool EffectOk
    {
      get
      {
        return Steps.Count > 0 && Steps.All(s => s.Ok);
      }
    }

    public bool Ok
    {
      get
      {
        return TriggerOk && EffectOk;
      }
    }
  }

  public static class PotentialJudge
  {
    // description = 포텐셜 본문(트리거와 효과가 한 문장). trainer = 트레이너 포텐셜인지
    public static JudgeResult Judge(string description, bool trainer)
    {
      var result = new JudgeResult();
      string activation;
      string body = PotentialParts.StripActivation(description ?? "", out activation);
      result.Activation = activation;
      string trigger, effect;
      PotentialLibrary.Split(body, out trigger, out effect);
      if (effect.Length == 0 && trigger.Length > 0)
      {
        // 효과만 있는 문장
        effect = trigger;
        trigger = "";
      }

      result.Trigger = trigger;
      result.Effect = effect;
      var specs = new List<TriggerSpec>();
      foreach (string line in TriggerStructures.NormalizeLines(trigger).Replace("\r", "").Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries))
      {
        var spec = TriggerModules.Parse(line);
        specs.Add(spec);
        if (spec.Module.Length == 0)
          result.BadTriggers.Add(line);
      }

      result.TriggerOk = result.BadTriggers.Count == 0;
      result.TriggerModules = string.Join(" + ", specs.Where(x => x.Module.Length > 0).Select(x => x.Module).Distinct());
      result.Steps = PotentialQueue.JudgeSteps(effect, trainer);
      return result;
    }
  }

  // 특성·기술 문장 판정(시트 원문 기준)
  public static class SheetJudge
  {
    // 특성: 도감에 같은 이름이 있으면 도감 상태, 없으면 시트 문장을 임시 특성으로 만들어 판정
    public static string Ability(string name, string effect, out string note)
    {
      note = "";
      var known = AbilityCatalog.Find(name);
      if (known != null)
      {
        bool same = AbilityModules.Clean(known.effect) == AbilityModules.Clean(effect);
        note = "도감 동명 " + (same ? "동일문" : "다른문");
        return AbilityModules.Status(known) == "수동" ? "MANUAL" : "OK";
      }

      var temp = new AbilityDefinition
      {
        name = "(시트)" + name,
        effect = effect
      };
      note = "도감에 없음";
      return AbilityModules.Runnable(temp) ? "OK" : "MANUAL";
    }

    // 기술 한 줄: 분류/타입/위력/명중/대상/우선도/접촉/효과문장. 읽지 못한 문장 목록을 돌려준다
    public static string Move(string name, string line, out string unknown)
    {
      unknown = "";
      var parts = line.Split('/');
      string effect = parts.Length >= 8 ? string.Join("/", parts.Skip(7)) : line;
      string category = parts.Length >= 8 ? parts[0].Trim() : "공격";
      var move = new Move
      {
        name = name,
        category = category == "변화" ? "변화" : category,
        effect = effect
      };
      if (MoveRuntime.Dedicated.Contains(name))
        return "DEDICATED";
      var plan = MoveRuntime.Plan(move);
      unknown = string.Join(" | ", plan.Unknown);
      return plan.Unknown.Count == 0 ? "OK" : "UNKNOWN";
    }
  }

  public static partial class PotentialQueue
  {
    // 효과 문장을 큐와 같은 방식으로 나눠 각 줄을 처리하는 실행기(또는 상시 규칙)가 있는지 본다
    internal static List<JudgeStep> JudgeSteps(string effect, bool trainer)
    {
      var list = new List<JudgeStep>();
      foreach (var step in Split(effect))
      {
        var judged = new JudgeStep { Line = step.Line };
        string kind = "";
        try
        {
          var potential = new Potential { id = "포켓몬", name = "?", description = step.Line };
          var instance = new PotentialInstance
          {
            Pokemon = trainer ? null : potential,
            Trainer = trainer ? new TrainerRecord() : null,
            Record = trainer ? new PotentialRecord() : null,
            Effect = step.Line,
            Description = step.Line,
            Name = "?"
          };
          var call = new EffectCall
          {
            Source = instance,
            Event = new PotentialEvent(),
            Line = step.Line
          };
          var executor = PotentialExecutors.Find(call);
          if (executor != null)
            kind = executor.Name;
        }
        catch (Exception)
        {
          kind = "";
        }

        if (kind.Length == 0)
          kind = Passive(step.Line);
        judged.Kind = kind;
        judged.Ok = kind.Length > 0;
        list.Add(judged);
      }

      return list;
    }

    // 실행기 없이 상시 규칙(방호·관통·무시·교대 제한 등)이 읽는 문장
    static string Passive(string line)
    {
      string kind;
      MoveCondition condition;
      double? rate;
      bool trainer;
      if (PotentialProtection.TryGuard(line, out kind, out condition, out rate))
        return "방호";
      if (PotentialProtection.TryPierce(line, out condition, out trainer))
        return "관통";
      if (PotentialProtection.IsAnalysisImmunity(line))
        return "해석 면역";
      if (EffectStructures.IsStatIgnore(line))
        return "능력치 무시";
      if (EffectStructures.IsSwitchRestriction(line))
        return "교대 제한";
      string module = EffectModules.Classify(line);
      if (module.Length > 0)
        return "모듈:" + module;
      return "";
    }
  }
}
