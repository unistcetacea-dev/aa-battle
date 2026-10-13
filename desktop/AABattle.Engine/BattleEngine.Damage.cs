// 능력치와 일반·예약 공격 대미지 계산
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Reflection;
using System.Web.Script.Serialization;

namespace AABattle
{
  public static partial class Engine
  {
    public static readonly string[] Names =
    {
      "체력",
      "공격",
      "방어",
      "특공",
      "특방",
      "속도"
    };
    public static int[] Stats(Fighter f, int other, Rules r)
    {
      var p = f.Data;
      int low = Math.Min(p.level, other);
      double c = 1 + Math.Max(0, p.level - other) * r.LevelCorrection / 100;
      return p.@base.Select((v, i) =>
      {
        int species = v + (i == 0 ? PrivilegeRules.HpBonus(f, r) : 0);
        return i == 0 ? (int)(r.HPCorrection ? Math.Floor((Math.Floor((species * 2 + p.iv[i]) * low / 100.0) + 10 + low) * c) : Math.Floor((species * 2 + p.iv[i]) * p.level / 100.0 + 10 + p.level)) : (int)Math.Floor((Math.Floor((species * 2 + p.iv[i]) * low / 100.0) + 5) * c);
      }).ToArray();
    }

    public static double Stage(int n)
    {
      return n >= 0 ? (2 + n) / 2.0 : 2.0 / (2 - n);
    }

    public static int Effective(Fighter a, Fighter b, int i, Rules r, int role = 0, int side = -1)
    {
      return EffectiveAtLevel(a, b, b.Data.level, i, r, role, side);
    }

    public static int EffectiveAtLevel(Fighter a, Fighter environmentOpponent, int otherLevel, int i, Rules r, int role = 0, int side = -1)
    {
      int s = a.Stages[i];
      double m = a.Multipliers[i] * PotentialModifiers.Stat(a, environmentOpponent, i, r);
      bool ignoreRank, ignoreBoost;
      if (StatIgnoreRuntime.TryIgnorePositive(environmentOpponent, a, i, out ignoreRank, out ignoreBoost))
      {
        if (ignoreRank)
          s = Math.Min(0, s);
        if (ignoreBoost)
          m = Math.Min(1, m);
      }

      if (r.Critical && role == 1)
      {
        s = Math.Max(0, s);
        m = Math.Max(1, m);
      }

      if ((r.Critical || r.IgnoreDefenderRaise) && role == 2)
      {
        s = Math.Min(0, s);
        m = Math.Min(1, m);
      }

      return Math.Max(1, (int)Math.Floor(Stats(a, otherLevel, r)[i] * m * Stage(s) * Mechanics.StatFactor(a, environmentOpponent, i, r, side) * StateTicks.StatFactor(a, i, r)));
    }

    public static double Match(Database db, string[] at, string[] dt)
    {
      return at.Length == 0 ? 1 : at.Max(t => dt.Aggregate(1.0, (n, d) => n * (db.chart.ContainsKey(d) && db.chart[d].ContainsKey(t) ? db.chart[d][t] : 1)));
    }

    public static Damage Calculate(Database db, Fighter a, Fighter b, Move m, Rules r, int attackerSide = -1, bool actionOverride = true, bool alreadyJudged = false)
    {
      m = alreadyJudged ? m : Mechanics.JudgedMove(a, actionOverride ? Mechanics.ActionMove(db, a, m) : m, b, r);
      Rules active = r;
      if (r.Critical && AbilityRuntime.BlocksCritical(a, b))
      {
        active = r.Copy();
        active.Critical = false;
      }

      int ai = Math.Max(1, Array.IndexOf(Names, m.attack ?? (m.category == "물리" ? "공격" : "특공"))), di = Math.Max(1, Array.IndexOf(Names, m.defense ?? (m.category == "물리" ? "방어" : "특방")));
      double basePower = VariablePower.BasePower(a, b, m, active) ?? m.power;
      double power = Math.Max(0, Math.Floor(basePower + active.PowerBonus) * active.PowerMultiplier);
      power *= Mechanics.PowerFactor(a, b, m, active) * AbilityRuntime.Power(a, m);
      double normalStab = active.Stab > 0 ? active.Stab : (AbilityPhrases.UsesMoveType(a, b, active) || TypeRules.MoveTypes(a, b, m, active).Any(t => a.Data.types.Contains(t))) ? 1.5 : 1, stab = AbilityRuntime.Stab(a, TypeRules.StabOverride(a, b, m, active, normalStab)), type = Mechanics.TypeEffect(db, a, b, m, active);
      string reason = Mechanics.Block(db, a, b, m, active);
      if (reason.Length > 0)
        type = 0;
      power *= PotentialModifiers.Power(a, b, m, active, type);
      int defenderSide = attackerSide >= 0 && attackerSide < 2 ? 1 - attackerSide : -1;
      double basis = Math.Floor(((2 * a.Data.level + 10) / 250.0 * MoveModules.AttackStat(a, b, m, ai, active, attackerSide, defenderSide) / MoveModules.DefenseStat(a, b, m, di, active, attackerSide, defenderSide) * power + 2) * stab * type * (active.Critical ? AbilityMoveModules.CriticalRate(a, b, active) : 1) * active.DamageMultiplier * Mechanics.DamageFactor(a, b, m, active, defenderSide) * AbilityRuntime.Damage(a, b, active) * PotentialModifiers.Damage(a, b, m, active, type));
      bool supported = m.category != "변화" && (basePower > 0 || Mechanics.Ohko(m));
      int[] rolls = Enumerable.Range(85, 16).Select(v => supported ? (Mechanics.Ohko(m) ? (type > 0 ? b.HP : 0) : (int)Math.Min(int.MaxValue, Math.Floor(basis * v / 100))) : 0).ToArray();
      var fixedRolls = MoveModules.FixedRolls(a, b, m, active);
      if (fixedRolls != null)
      {
        supported = true;
        rolls = fixedRolls.Select(x => type > 0 ? x : 0).ToArray();
      }

      return new Damage
      {
        Min = rolls[0],
        Max = rolls[15],
        Rolls = rolls,
        Stab = stab,
        Type = type,
        Supported = supported,
        Reason = reason
      };
    }

    public static Damage CalculateDelayed(Database db, Fighter target, Fighter environmentOpponent, DelayedAttackState delayed, Rules r)
    {
      var m = delayed.Move;
      int di = Math.Max(1, Array.IndexOf(Names, m.defense ?? "특방"));
      double power = Math.Max(0, Math.Floor(m.power));
      var source = Mechanics.DelayedSource(delayed);
      double stab = (delayed.SourceTypes ?? new string[0]).Any(t => (m.types ?? new string[0]).Contains(t)) ? 1.5 : 1;
      double type = Mechanics.TypeEffect(db, source, target, m, r);
      int defense = EffectiveAtLevel(target, environmentOpponent, delayed.SourceLevel, di, r, 2);
      double basis = Math.Floor(((2 * delayed.SourceLevel + 10) / 250.0 * delayed.SourceSpecialAttack / defense * power + 2) * stab * type * Mechanics.DelayedDamageFactor(target, m));
      bool supported = m.category != "변화" && m.power > 0;
      int[] rolls = Enumerable.Range(85, 16).Select(v => supported ? (int)Math.Min(int.MaxValue, Math.Floor(basis * v / 100)) : 0).ToArray();
      return new Damage
      {
        Min = rolls[0],
        Max = rolls[15],
        Rolls = rolls,
        Stab = stab,
        Type = type,
        Supported = supported
      };
    }
  }
}
