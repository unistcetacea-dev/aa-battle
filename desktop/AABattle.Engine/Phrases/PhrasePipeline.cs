using System;
using System.Text.RegularExpressions;

namespace AABattle
{
  // 정규화 파이프라인: 수집 문장의 변형을 표준 문장으로 바꾸는 규칙의 "적용 순서"를 이 파일 한 곳에만 둔다.
  // 규칙 본체는 Phrases/*.cs(기능별 PhraseRules)에 있고, 파일 이동으로 순서가 바뀌지 않게 여기서 순서대로 나열한다.
  //
  // 순서 의존 규칙(앞 규칙의 결과를 뒤 규칙이 읽는다)
  //  - PreAllyIsSelf → PreRankPass: 「자신(아군)의」를 먼저 「자신의」로 고쳐야 ^자신의 능력 상승 인계 규칙이 맞는다.
  //  - PrePriorityRaise → PreMoveKindPriority: 둘 다 기술 우선도 표기라 먼저 나온 것이 먼저 처리한다.
  //  - PreAllyState → PreMiddleDuration: 아군→자신 치환 뒤에 기간을 문장 앞으로 옮긴다(앞머리가 T면 건너뛴다).
  //  - 칼데아 효과 규칙: 앞의 구체적인 규칙(위력 강화+추격 등)이 뒤의 일반 규칙(관통 단일 문장)보다 먼저여야 한다.
  //  - DamageVariantModules → 칼데아 3차 → PotentialGrantModules: 대미지 변형이 먼저, 부여 문장은 맨 끝.
  //  - 칼데아 효과 → 실프(Abaddon) 효과: 칼데아 규칙이 먼저 시도된다.
  public static class PhrasePipeline
  {
    // ===== 효과 앞처리(글자만 다른 표기 통일, 앞 단계의 결과를 다음 단계가 받는다) =====
    static readonly Func<string, string>[] EffectPreSteps =
    {
      PhraseRules.PreChargeSkip,
      PhraseRules.PreAttackPower,
      PhraseRules.PreExtraActionPosition,
      PhraseRules.PrePriorityRaise,
      PhraseRules.PreNormalSpelling,
      PhraseRules.PreAllyIsSelf,
      PhraseRules.PreRankPass,
      PhraseRules.PreForcedSwitch,
      PhraseRules.PreNormalSwitchOnly,
      PhraseRules.PreStatList,
      PhraseRules.PreStatParen,
      PhraseRules.PreMoveKindPriority,
      PhraseRules.PreDoubleHalve,
      PhraseRules.PreAllyState,
      PhraseRules.PreMiddleDuration
    };

    public static string EffectPre(string clean)
    {
      string text = clean ?? "";
      if (text.Length == 0)
        return text;
      foreach (var step in EffectPreSteps)
        text = step(text);
      return text;
    }

    // ===== 효과 정규화: 칼데아 시트 규칙(위에서 아래로, 처음 성공한 것이 답) =====
    static readonly Func<string, string>[] ChaldeaEffectRules =
    {
      PhraseRules.FixedHit,
      PhraseRules.PursuitFixedHit,
      PhraseRules.PowerPursuitPierce,
      PhraseRules.PierceGuardA,
      PhraseRules.PierceGuardB,
      PhraseRules.PierceAll,
      PhraseRules.IgnoreGuardPotential,
      PhraseRules.PriorityPlus,
      PhraseRules.NonAttackDamage,
      PhraseRules.StatusProtect,
      PhraseRules.ItemProtect,
      PhraseRules.NoCriticalHit,
      DamageVariantModules.Normalize,
      // ---- 칼데아 3차 ----
      PhraseRules.AlreadyCanonical,
      PhraseRules.ChargeSkipByName,
      PhraseRules.HalveAndEndure,
      PhraseRules.NonAttackDamageSelf,
      PhraseRules.RecoilIgnore,
      PhraseRules.HalveReceived,
      PhraseRules.CureAndHeal,
      PhraseRules.SpeciesRank,
      PhraseRules.FixedHitGrant,
      PhraseRules.StabRate,
      PhraseRules.NoStatDrop,
      PhraseRules.StateBecomes,
      PhraseRules.CostExtraHalfHp,
      PhraseRules.CostExtraState,
      PhraseRules.CostExtraLast,
      PhraseRules.BondGuideRise,
      PhraseRules.RoleAppoint,
      PotentialGrantModules.Normalize
    };

    static string CleanSentence(string text)
    {
      return Regex.Replace(PotentialParts.Clean(text ?? ""), @"\s+", " ").Trim().TrimEnd('.', '。', '．').Trim();
    }

    // 표준 문장으로 바꿀 수 있으면 바꾼다. 아니면 null(Prepare를 거친 문장, 끝 마침표 없음)
    public static string ChaldeaEffect(string text)
    {
      string s = CleanSentence(text);
      if (s.Length == 0)
        return null;
      foreach (var rule in ChaldeaEffectRules)
      {
        string result = rule(s);
        if (result != null)
          return result;
      }

      return null;
    }

    // ===== 효과 정규화: 실프 인헨스먼츠 규칙(칼데아 규칙에서 못 바꾼 문장만 온다) =====
    static readonly Func<string, string>[] AbaddonEffectRules =
    {
      PhraseRules.InvertRank,
      PhraseRules.FoeMoveType,
      PhraseRules.LowerPower,
      PhraseRules.SealCure,
      PhraseRules.ContactPierce,
      PhraseRules.RecoilNoDamage,
      PhraseRules.OwnMoveType,
      PhraseRules.FirstPriority,
      PhraseRules.SameTypeHalve,
      PhraseRules.FullHeal,
      PhraseRules.RepayDamage,
      PhraseRules.OffField,
      PhraseRules.OptionalSwitch,
      PhraseRules.AttackPivot
    };

    static string FirstResult(Func<string, string>[] rules, string s)
    {
      foreach (var rule in rules)
      {
        string result = rule(s);
        if (result != null)
          return result;
      }

      return null;
    }

    public static string AbaddonEffect(string text)
    {
      string s = CleanSentence(text);
      if (s.Length == 0)
        return null;
      return FirstResult(AbaddonEffectRules, s);
    }

    // ===== 특성 도감 문장 다시쓰기(문장 하나 단위) =====
    static readonly Func<string, string>[] AbilityRewritePreSteps =
    {
      PhraseRules.AbilityWeatherNames,
      PhraseRules.AbilityWeatherCondition,
      PhraseRules.AbilityFieldState,
      PhraseRules.AbilityContactChip,
      PhraseRules.AbilityStatusImmuneSplit,
      PhraseRules.AbilityCriticalBonus
    };

    public static string AbilityRewritePre(string t)
    {
      foreach (var step in AbilityRewritePreSteps)
        t = step(t);
      return t;
    }

    // 특성 효과 문장: 앞에서부터 처음 맞는 규칙(없으면 null)
    static readonly Func<string, string>[] AbilityEffectRules =
    {
      PhraseRules.AbilityWeatherDamage,
      PhraseRules.AbilityNoCritical,
      PhraseRules.AbilitySturdy,
      PhraseRules.AbilityMagicGuard,
      PhraseRules.AbilityDropImmuneAll,
      PhraseRules.AbilityDropImmuneStat,
      PhraseRules.AbilityHazardProof,
      PhraseRules.AbilityPierce,
      PhraseRules.AbilityAddType,
      PhraseRules.AbilityNoForcedSwitch,
      PhraseRules.AbilityFoeFractionDamage,
      PhraseRules.AbilitySelfFractionHeal,
      PhraseRules.AbilityTakenDamageRate,
      PhraseRules.AbilityStateImmune,
      PhraseRules.AbilityChanceState,
      PhraseRules.AbilitySpeciesGrade,
      PhraseRules.AbilityConditionPriority,
      PhraseRules.AbilityWeatherTurns
    };

    public static string AbilityEffect(string s)
    {
      return FirstResult(AbilityEffectRules, s);
    }

    // 특성의 타입 변경·반동·특성 교체 문장
    static readonly Func<string, string>[] AbilityTypeSentenceRules =
    {
      PhraseRules.AbilityChangeAbility,
      PhraseRules.AbilityTauntInfatuation,
      PhraseRules.AbilityMatchMoveType,
      PhraseRules.AbilityMoveTypeChange,
      PhraseRules.AbilityNoRecoil
    };

    public static string AbilityTypeSentence(string s)
    {
      return FirstResult(AbilityTypeSentenceRules, s);
    }

    // ===== 트리거 절 정규화 =====
    static readonly Func<string, string>[] TriggerPreSteps =
    {
      TriggerPhraseRules.PreNormalSpelling,
      TriggerPhraseRules.PreHeadCut
    };

    static readonly Func<string, string>[] TriggerVariantRules =
    {
      TriggerPhraseRules.HitSelf,
      TriggerPhraseRules.CriticalReceived,
      TriggerPhraseRules.SwitchEntry,
      TriggerPhraseRules.DefeatByFaint,
      TriggerPhraseRules.ReplacementEntry,
      TriggerPhraseRules.OmittedStateSubject,
      TriggerPhraseRules.SpeedCompare,
      TriggerPhraseRules.TrainerName,
      TriggerPhraseRules.NoAttack
    };

    // 정리된 트리거 절(Clean)을 받아 표준 문장을 돌려준다. 해당 없으면 null
    public static string TriggerNormalize(string clause)
    {
      string t = Regex.Replace(clause ?? "", @"\s+", " ").Trim();
      if (t.Length == 0)
        return null;
      foreach (var step in TriggerPreSteps)
        t = step(t);
      string result = FirstResult(TriggerVariantRules, t);
      if (result != null)
        return result;
      return t == (clause ?? "").Trim() ? null : t;
    }

    // 변형이 아니면 입력 그대로
    public static string TriggerVariant(string t)
    {
      return TriggerNormalize(t) ?? t;
    }

    static readonly Func<string, string>[] TriggerMutualRules =
    {
      TriggerPhraseRules.MutualFixedHit,
      TriggerPhraseRules.MutualAttack,
      TriggerPhraseRules.MutualStatusMove
    };

    public static string TriggerMutual(string t)
    {
      return FirstResult(TriggerMutualRules, t);
    }

    // 특성 도감 트리거 절(문장 하나, 맞는 규칙이 없으면 그대로)
    static readonly Func<string, string>[] AbilityTriggerRules =
    {
      TriggerPhraseRules.AbilityContactReceived,
      TriggerPhraseRules.AbilityNonContactReceived,
      TriggerPhraseRules.AbilityHpBelow,
      TriggerPhraseRules.AbilityHpRange,
      TriggerPhraseRules.AbilityHpFull,
      TriggerPhraseRules.AbilityAttackReceived,
      TriggerPhraseRules.AbilityPhysicalSpecialReceived,
      TriggerPhraseRules.AbilityPhysicalSpecialHit,
      TriggerPhraseRules.AbilityDefeat,
      TriggerPhraseRules.AbilityStatusState
    };

    public static string AbilityTrigger(string c)
    {
      return FirstResult(AbilityTriggerRules, c) ?? c;
    }

    // 확률 접두어까지 한 문장으로 읽어야 하는 변형(방호·회피처럼 확률이 문장 안으로 들어가는 것)
    public static string EffectWhole(string clean)
    {
      return PhraseRules.HpScaledEndureWhole(clean);
    }
  }
}
