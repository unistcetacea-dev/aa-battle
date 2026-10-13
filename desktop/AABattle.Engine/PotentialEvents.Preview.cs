// 효과 문장이 실행기로 읽히는지 미리 보는 도구(템플릿 판정 표·검사용). 실제 실행에는 쓰지 않는다.
using System;
using System.Linq;
using System.Collections.Generic;

namespace AABattle
{
  public static partial class PotentialQueue
  {
    // 효과 글자를 큐와 같은 규칙으로 나눠, 줄마다 실행기 이름을 돌려준다(실행기가 없으면 빈 글자).
    public static List<KeyValuePair<string, string>> Preview(string effect, string slot = "전용")
    {
      var result = new List<KeyValuePair<string, string>>();
      var fighter = new Fighter(new Pokemon
      {
        id = "preview",
        name = "미리보기",
        level = 100,
        types = new[] { "노말" },
        ability = "",
        item = "",
        @base = new[] { 100, 100, 100, 100, 100, 100 },
        iv = new[] { 31, 31, 31, 31, 31, 31 },
        moves = new string[0],
        potentials = new Potential[0]
      });
      var potential = new Potential { id = slot, name = "미리보기", description = effect ?? "", triggers = new[] { TriggerStructures.FieldLabel } };
      var source = new PotentialInstance { Owner = fighter, Pokemon = potential, Key = "preview", Name = "미리보기", Slot = slot, Effect = effect ?? "" };
      var e = new PotentialEvent { Owner = fighter, Label = "", Message = "", Log = x => { } };
      foreach (var step in Split(effect ?? ""))
      {
        var call = new EffectCall { Source = source, Event = e, Line = step.Line, Rules = new Rules(), Rng = () => .5, Log = new List<string>() };
        PotentialExecutor executor = null;
        try
        {
          executor = FindExecutor(call, step);
        }
        catch (Exception)
        {
          executor = null;
        }

        result.Add(new KeyValuePair<string, string>(step.Line, executor == null ? "" : executor.Name));
      }

      return result;
    }
  }
}
