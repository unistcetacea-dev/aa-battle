// 기본 랭크·배율·종족치·HP 비율 실행
using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace AABattle
{
  public static partial class PotentialQueue
  {
    // 기존에 동작하던 실행기를 감싼 등록(F1에서는 동작을 바꾸지 않는다)
    internal static partial class BuiltIn
    {
      // 「NT동안 / T종료시까지」 접두어를 떼고 기간을 돌려준다(없으면 0, T종료시까지는 1)
      static string Duration(string line, out int turns)
      {
        turns = 0;
        var m = Regex.Match(line ?? "", @"^(?:(?<n>\d+)\s*T\s*(?:동안|간)|(?<end>T\s*종료\s*시\s*까지))\s*,?\s*");
        if (!m.Success)
          return line;
        turns = m.Groups["end"].Success ? 1 : int.Parse(m.Groups["n"].Value);
        return line.Substring(m.Length);
      }

      static Fighter Recipient(EffectCall c, string subject)
      {
        return subject == "상대" ? c.Opponent : c.Owner;
      }

      // 기술·피격 관련 사건으로 걸린 배율은 이번 T만(확정: 조건이 사라질 때까지)
      static bool ActionEvent(PotentialEvent e)
      {
        string label = e.Label ?? "";
        return e.Move != null || PriorityEffectRuntime.SelectionLabel(label) || label == TriggerStructures.AttackSuccessLabel || label == TriggerStructures.ReceivedLabel || label == TriggerStructures.ReceivedOrNullifiedLabel || label == TriggerStructures.DamagedLabel;
      }

      static EffectOutcome RunRank(EffectCall c)
      {
        RankEffect r;
        EffectModules.TryRank(c.Line, out r);
        var target = Recipient(c, r.Subject);
        if (target == null || target.HP <= 0)
          return EffectOutcome.Failed;
        var other = ReferenceEquals(target, c.Owner) ? c.Opponent : c.Owner;
        var stats = r.Stats.ToList();
        if (r.Mode == "랜덤")
          stats = new List<int>
          {
            EffectModules.StatIndex(PotentialStatRules.ChooseRandom(c.Rng ?? (() => .5)))
          };
        else if (r.Mode == "최고" || r.Mode == "최저")
          stats = new List<int>
          {
            EffectModules.StatIndex(PotentialStatRules.ChooseExtreme(target, other ?? target, c.Rules, r.Mode == "최고", c.Rng ?? (() => .5)))
          };
        else if (r.Mode == "임의")
        {
          var choose = c.Rules == null ? null : c.Rules.ChooseStat;
          if (choose == null)
          {
            c.Say(c.Source.Label + ": 임의의 능력은 사용자가 고른다 — 판정 대기");
            return EffectOutcome.Pending;
          }

          string chosen = choose(c.Source, target, PotentialStatRules.Arbitrary);
          if (string.IsNullOrEmpty(chosen))
            return EffectOutcome.Pending;
          stats = new List<int>
          {
            EffectModules.StatIndex(chosen)
          };
        }

        bool changed = false;
        var log = c.Log ?? new List<string>();
        foreach (int index in stats)
        {
          if (index >= 1 && index <= 5)
            changed |= Mechanics.ChangeStage(target, other ?? target, index, r.Amount, c.Rules, c.Source.Label, log);
          else if (index == 6 || index == 7 || index == 8)
          {
            int before = index == 6 ? target.AccuracyStage : index == 7 ? target.EvasionStage : target.CriticalStage;
            int after = index == 8 ? Math.Max(0, Math.Min(3, before + r.Amount)) : Math.Max(-6, Math.Min(6, before + r.Amount));
            if (after == before)
              continue;
            if (index == 6)
              target.AccuracyStage = after;
            else if (index == 7)
              target.EvasionStage = after;
            else
              target.CriticalStage = after;
            log.Add(target.Data.name + ": " + (index == 6 ? "명중" : index == 7 ? "회피" : "급소") + " " + (r.Amount > 0 ? "+" : "") + (after - before) + " (" + c.Source.Label + ")");
            if (index != 8)
              AnsweredTriggers.StageChanged(target, other, index, before, after, log.Add);
            changed = true;
          }
        }

        return changed ? EffectOutcome.Success : EffectOutcome.Failed;
      }

      static EffectOutcome RunScalar(EffectCall c)
      {
        int turns;
        string body = Duration(c.Line, out turns);
        ScalarEffect s;
        EffectModules.TryScalar(body, out s);
        if (s.Condition.Placeholder)
        {
          c.Say(c.Source.Label + ": 기술 조건의 타입이 비어 있다 — 판정 대기");
          return EffectOutcome.Pending;
        }

        bool action = turns == 0 && ActionEvent(c.Event);
        var owner = c.Owner;
        if (owner == null || owner.HP <= 0)
          return EffectOutcome.Failed;
        PotentialModifiers.Add(owner, new ActiveModifier { Key = c.Source.Key, Text = body, Source = c.Source.Label, Turns = turns == 0 ? -1 : turns, Action = action });
        c.Say(owner.Data.name + ": " + body + (action ? " (이번 행동)" : turns > 0 ? " (" + turns + "T)" : " (필드를 떠날 때까지)"));
        return EffectOutcome.Success;
      }

      static EffectOutcome RunSpecies(EffectCall c)
      {
        SpeciesEffect s;
        EffectModules.TrySpecies(c.Line, out s);
        var target = Recipient(c, s.Subject);
        if (target == null || target.HP <= 0)
          return EffectOutcome.Failed;
        var b = target.Data.@base;
        if (target.Conditions.OriginalBase == null)
          target.Conditions.OriginalBase = (int[])b.Clone();
        int trainerFloor = -1;
        string trainerGrade = "";
        if (s.Operation == "트레이너")
        {
          var scope = EffectScope.Current;
          int side = target.FieldSide;
          var trainer = scope != null && scope.States != null && side >= 0 && side < scope.States.Length && scope.States[side] != null ? scope.States[side].Trainer : null;
          var aptitude = trainer == null ? null : s.Aptitude == "지시" ? trainer.command : s.Aptitude == "육성" ? trainer.training : s.Aptitude == "통솔" ? trainer.leadership : trainer.ability;
          try
          {
            trainerGrade = aptitude == null ? "" : (aptitude.rank ?? "").Trim();
            trainerFloor = trainerGrade.Length == 0 ? -1 : SpeciesStatRanks.LabelRange(trainerGrade)[0];
          }
          catch
          {
            trainerFloor = -1;
          }

          if (trainerFloor < 0)
          {
            c.Say(c.Source.Label + ": 트레이너 「" + s.Aptitude + "」 등급을 알 수 없다 — 판정 대기");
            return EffectOutcome.Pending;
          }
        }

        bool changed = false;
        foreach (int i in s.Stats)
        {
          int current = b[i], next = current;
          int floor = s.Grade.Length > 0 ? GradeValue(c, target, i, current, s.Grade, s.Value) : s.Value;
          switch (s.Operation)
          {
            case "변경":
              next = floor;
              break;
            case "상승":
              next = Math.Max(current, floor);
              break;
            case "하락":
              next = Math.Min(current, floor);
              break;
            case "랭크":
              next = SpeciesStatRanks.RaisedRange(current, s.Ranks)[0];
              break;
            case "수치":
              next = s.Value;
              break;
            case "최고 복사":
              next = PotentialStatRules.CopyBase(target.Data, EffectModules.StatIndex(PotentialStatRules.ChooseExtremeBase(target.Data, true, c.Rng ?? (() => .5))));
              break;
            case "최저 복사":
              next = PotentialStatRules.CopyBase(target.Data, EffectModules.StatIndex(PotentialStatRules.ChooseExtremeBase(target.Data, false, c.Rng ?? (() => .5))));
              break;
            case "복사":
              next = b[s.Source];
              break;
            case "복사 상승":
              next = Math.Max(current, b[s.Source]);
              break;
            case "트레이너":
              next = Math.Max(current, trainerFloor < 0 ? current : GradeValue(c, target, i, current, trainerGrade, -1));
              break;
          }

          if (next == current)
            continue;
          b[i] = next;
          changed = true;
          c.Say(target.Data.name + ": " + Engine.Names[i] + " 종족치 " + current + "→" + next + " (" + c.Source.Label + ")");
        }

        if (!changed && target.Conditions.OriginalBase != null && target.Conditions.OriginalBase.SequenceEqual(b))
          target.Conditions.OriginalBase = null;
        if (changed && s.Stats.Contains(0))
          target.HP = Math.Min(target.HP, Engine.Stats(target, (c.Opponent ?? target).Data.level, c.Rules ?? new Rules())[0]);
        return changed ? EffectOutcome.Success : EffectOutcome.Failed;
      }

      // 등급 문장의 값: 현재 값의 -·+를 유지한 범위(B-→A-, B+→A+, 무표기→A 범위). 지정 값이 범위 안이면 그 값, 아니면 선택 또는 최솟값
      static int GradeValue(EffectCall c, Fighter target, int stat, int current, string grade, int value)
      {
        var range = SpeciesStatRanks.GradeRange(current, grade);
        if (value >= range[0] && value <= range[1])
          return value;
        if (range[0] == range[1])
          return range[0];
        var choose = c.Rules == null ? null : c.Rules.ChooseSpeciesValue;
        int chosen = choose == null ? -1 : choose(c.Source, target, stat, range[0], range[1]);
        if (chosen >= range[0] && chosen <= range[1])
          return chosen;
        c.Say(c.Source.Label + ": 「" + grade + "」 범위 " + range[0] + "~" + range[1] + " 중 최솟값 " + range[0] + "을 썼다(편집기에서 값을 지정할 수 있다)");
        return range[0];
      }

      static EffectOutcome RunFraction(EffectCall c)
      {
        FractionEffect f;
        EffectModules.TryFraction(c.Line, out f);
        var target = f.Target == "상대" ? c.Opponent : c.Owner;
        if (target == null)
          return EffectOutcome.Failed;
        var other = ReferenceEquals(target, c.Owner) ? c.Opponent ?? target : c.Owner;
        var rules = c.Rules ?? new Rules();
        var log = c.Log ?? new List<string>();
        int max = Mechanics.MaxHP(target, other, rules);
        int amount = FractionalHpRule.Amount(max, f.Denominator);
        if (f.Action == "회복")
        {
          if (target.HP <= 0)
          {
            c.Say(target.Data.name + ": 빈사라 회복할 수 없다");
            return EffectOutcome.Failed;
          }

          return Mechanics.Heal(target, other, rules, amount, c.Source.Label, log) > 0 ? EffectOutcome.Success : EffectOutcome.Failed;
        }

        if (f.Action == "소비")
        {
          if (target.HP <= amount)
          {
            c.Say(target.Data.name + ": 체력이 부족해 " + amount + "을 소비할 수 없다");
            return EffectOutcome.Failed;
          }

          int before = target.HP;
          target.HP -= amount;
          c.Say(target.Data.name + " — " + c.Source.Label + " 대가: HP -" + amount);
          AnsweredTriggers.HpChanged(target, other, rules, before, target.HP, c.Source.Label, log.Add);
          return EffectOutcome.Success;
        }

        if (target.HP <= 0)
          return EffectOutcome.Failed;
        if (AbilityPhrases.IgnoresIndirect(target))
        {
          c.Say(target.Data.name + ": 간접 대미지 무효로 대미지를 받지 않는다");
          return EffectOutcome.Failed;
        }

        if (!f.CanFaint)
          amount = Math.Min(amount, target.HP - 1);
        if (amount <= 0)
          return EffectOutcome.Failed;
        Mechanics.Hurt(target, other, rules, amount, c.Source.Label, log);
        return EffectOutcome.Success;
      }
    }
  }
}
