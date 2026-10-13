// 표준 효과 문장 분류
using System;
using System.Linq;
using System.Globalization;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace AABattle
{
  public static partial class EffectModules
  {
    public static string Classify(string text)
    {
      return classified.GetOrAdd(text ?? "", ClassifyUncached);
    }

    static string ClassifyUncached(string text)
    {
      string value = EffectNormalizer.Normalize(text ?? "");
      string probability, body;
      if (EffectNormalizer.TryProbabilityPrefix(value, out probability, out body))
        value = body;
      value = Regex.Replace(Prepare(value), @"^(?:\d+\s*T\s*(?:간|동안)|T\s*종료\s*시\s*까지)\s*,?\s*", "");
      value = Activation.Replace(value, "");
      var sentences = Regex.Split(value, @"(?<=[.．。])\s+").Where(x => x.Trim().Length > 0).ToArray();
      if (sentences.Length == 0)
        return "";
      var kinds = sentences.Select(ClassifySentence).ToArray();
      return kinds.Any(x => x.Length == 0) ? "" : string.Join(" + ", kinds.Distinct());
    }

    static string ClassifySentence(string s)
    {
      ScalarEffect scalar;
      SpeciesEffect species;
      FractionEffect fraction;
      RankEffect rank;
      string target;
      if (SecondaryEffects.IsModule(s))
        return "추가효과 부여";
      string moveKind = MoveModules.Classify(s);
      string batonKind = BatonModules.Classify(s);
      if (batonKind.Length == 0)
        batonKind = PursuitModules.Classify(s);
      if (batonKind.Length == 0)
        batonKind = MoveMiscModules.Classify(s);
      if (batonKind.Length == 0)
        batonKind = BondModules.Classify(s);
      if (batonKind.Length == 0)
        batonKind = RoleModules.ClassifyEffect(s);
      if (batonKind.Length == 0)
        batonKind = ExclusiveModules.Classify(s);
      if (batonKind.Length == 0)
        batonKind = PotentialNullify.Classify(s);
      if (batonKind.Length > 0)
        return batonKind;
      if (moveKind.Length > 0)
        return moveKind;
      string criticalKind = AbilityCriticalFieldModules.Classify(s);
      if (criticalKind.Length > 0)
        return criticalKind;
      string itemAbility = AbilityItemModules.Classify(s);
      if (itemAbility.Length > 0)
        return itemAbility;
      string noEffectKind = AbilityNoEffectModules.Classify(s);
      if (noEffectKind.Length > 0)
        return noEffectKind;
      string abilityMoveKind = AbilityMoveModules.Classify(s);
      if (abilityMoveKind.Length > 0)
        return abilityMoveKind;
      string abilityStateKind = AbilityStateModules.Classify(s);
      if (abilityStateKind.Length > 0)
        return abilityStateKind;
      string abilityEventKind = AbilityEventModules.Classify(s);
      if (abilityEventKind.Length > 0)
        return abilityEventKind;
      string changeKind = AbilityChangeModules.Classify(s);
      if (changeKind.Length > 0)
        return changeKind;
      string abilityKind = AbilityPhrases.Classify(s);
      if (abilityKind.Length > 0)
        return abilityKind;
      string miscKind = MiscModules.Classify(s);
      if (miscKind.Length > 0)
        return miscKind;
      string cureTarget;
      string[] cureNames;
      bool cureRelease;
      if (StateCure.TryParse(s, out cureTarget, out cureNames, out cureRelease))
        return cureRelease ? "상태 해제" : "상태 치유";
      string grantSubject;
      string[] grantMoves;
      if (FunctionExtensions.TryGrant(s, out grantSubject, out grantMoves))
        return "기능확장 부여";
      string typed = TypeRules.Classify(s);
      if (typed.Length > 0)
        return typed;
      string remaining4 = AbilityRemaining4Modules.Classify(s);
      if (remaining4.Length > 0)
        return remaining4;
      if (TryScalar(s, out scalar))
        return "수치 배율";
      if (TrySpecies(s, out species))
        return "종족치 변경";
      if (TryFraction(s, out fraction))
        return "최대 HP의 1/N";
      if (TryCure(s, out target))
        return "상태이상 치유";
      if (TryRank(s, out rank))
        return "능력 랭크 변화";
      string kind, value;
      bool install;
      string[] statuses;
      if (TryPlace(s, out kind, out value))
        return "장소 전개";
      if (TryHazard(s, out install, out value))
        return "설치물";
      if (IsForcedSwitch(s))
        return "강제교대";
      if (IsForcedSwitchImmunity(s))
        return "강제교대 무효";
      if (IsFloating(s))
        return "부유";
      if (IsActFirst(s))
        return "먼저 행동";
      MoveCondition guardCondition;
      double? guardRate;
      string guardKind;
      if (PotentialProtection.TryGuard(s, out guardKind, out guardCondition, out guardRate))
        return guardKind == "회피" ? "회피" : guardKind == "반사" ? "기술 반사" : "기술 무효화";
      if (PotentialProtection.TryPierce(s, out guardCondition))
        return "방호 관통";
      if (PotentialProtection.IsAnalysisImmunity(s))
        return "데이터 해석 무효";
      ExtraActionGrant extra;
      int every;
      if (ExtraActionRuntime.TryParse(s, out extra, out every))
        return "추가행동";
      if (TryStatusImmunity(s, out statuses))
        return "상태이상 면역";
      if (EffectStructures.IsPriority(s))
        return "우선도 변화";
      if (EffectStructures.IsStatIgnore(s))
        return "능력 상승·강화 무시";
      if (EffectStructures.IsSwitchRestriction(s))
        return "교대 제한";
      if (EffectStructures.IsSwitchEffect(s))
        return "교대 효과";
      if (EffectStructures.IsPivotGrant(s))
        return "유턴 효과";
      if (EffectStructures.IsAnalysisPermission(s))
        return "데이터 해석";
      var state = StateEffects.Parse(s);
      if (state != null && StateEffects.Find(state.State) != null)
        return "상태 부여";
      return "";
    }
  }
}
