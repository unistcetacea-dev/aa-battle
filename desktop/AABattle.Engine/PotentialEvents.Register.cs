// 기본 효과 실행기 등록(순서 유지)
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
      public const string AnalysisName = "해석 허가", SwitchName = "교대·유턴 부여";
      public static void Register()
      {
        // 우선도: 기술 선택 단계(PriorityEffectRuntime.PrimeTurn)가 따로 처리하는 기술 사용 라벨은 제외
        PotentialExecutors.Register("우선도", c => !c.Source.IsTrainer && !PriorityEffectRuntime.SelectionLabel(c.Event.Label) && !PriorityEffectRuntime.Continuous(c.Source.Pokemon) && PriorityEffectRuntime.Matches(c.Line), c => PriorityEffectRuntime.ApplyLine(c.Line, c.Owner, c.Opponent, c.Event.Label, c.Event.Message, c.Say) ? EffectOutcome.Success : EffectOutcome.Failed);
        // 해석 허가: 「할 수 있다」 확인은 큐가 한다
        PotentialExecutors.Register(AnalysisName, c => !c.Source.IsTrainer && EffectStructures.IsAnalysisPermission(c.Line), c =>
        {
          var target = c.Opponent;
          if (target == null || target.HP <= 0)
            return EffectOutcome.Failed;
          if (PotentialProtection.AnalysisBlocked(target, c.Owner, c.Rules))
          {
            c.Say(target.Data.name + ": 데이터 해석을 무효화했다");
            return EffectOutcome.Failed;
          }

          var state = BattleRecords.StateOf(c.Owner);
          if (BattleRecords.IsAnalyzed(state, target))
          {
            c.Say(target.Data.name + ": 이미 해석한 상대다");
            return EffectOutcome.Failed;
          }

          c.Say("『" + c.Source.Name + "』 선택 발동 · " + target.Data.name + "의 데이터를 해석한다.");
          if (state != null)
            BattleRecords.RecordAnalysis(state, c.Owner, new[] { c.Owner }, target, c.Say);
          else
            RuntimeTriggers.Analyzed(c.Owner, new[] { c.Owner }, target, c.Say);
          return EffectOutcome.Success;
        });
        // 교대·유턴 부여: 실제 교대는 엔진의 교대 단계(ResolveTriggeredSwitches)가 로그를 읽어 처리한다
        PotentialExecutors.Register(SwitchName, c => SwitchEffectRuntime.IsSwitchEffect(c.Line) || SwitchEffectRuntime.IsPivotGrant(c.Line), c => EffectOutcome.Deferred);
        // 트레이너 고유: 아군 능력 상승(기존 TrainerTriggers 동작)
        PotentialExecutors.Register("트레이너 아군 능력 상승", c => c.Source.IsTrainer && TrainerTriggers.AllyRise(c.Line) != null, c => TrainerTriggers.ApplyAllyRise(c.Line, c.Owner, c.Opponent, c.Say) ? EffectOutcome.Success : EffectOutcome.Failed);
        // 상태 부여 문장(StateEffects 표). 확률은 큐가 포텐셜당 1회 판정했으므로 다시 굴리지 않는다
        PotentialExecutors.Register("상태 부여", c => StateEffects.Parse(c.Line) != null && StateEffects.Find(StateEffects.Parse(c.Line).State) != null, c => StateEffects.Apply(c.Line, c.Owner, c.Opponent, c.Rules, c.Rng, c.Log, c.Source.Label));
        // F2 능력 랭크 변화(기간이 붙은 랭크는 되돌리는 규칙이 없어 판정 대기)
        PotentialExecutors.Register("능력 랭크 변화", c =>
        {
          RankEffect r;
          int turns;
          return Duration(c.Line, out turns) == c.Line && EffectModules.TryRank(c.Line, out r);
        }, RunRank);
        // F2 수치 배율: 사건으로 걸리면 기간(NT동안·T종료시까지)·이번 T(기술 관련 사건)·필드를 떠날 때까지 유지
        PotentialExecutors.Register("수치 배율", c =>
        {
          ScalarEffect s;
          int turns;
          return EffectModules.TryScalar(Duration(c.Line, out turns), out s);
        }, RunScalar);
        // F2 종족치 변경(확정 13: 가장 최근 효과로 덮어쓰고 교대하면 원래 값)
        PotentialExecutors.Register("종족치 변경", c =>
        {
          SpeciesEffect s;
          return EffectModules.TrySpecies(c.Line, out s);
        }, RunSpecies);
        // F3 최대 HP의 1/N: 회복(빈사 제외) · 대미지(간접, 매직가드에 막힘) · 소비(대가, 매직가드 무시, 부족하면 실패)
        PotentialExecutors.Register("최대 HP의 1/N", c =>
        {
          FractionEffect f;
          return EffectModules.TryFraction(c.Line, out f);
        }, RunFraction);
        // F4a 장소 전개(확정 24: 기술과 같은 규칙, 기간이 없으면 5T)
        PotentialExecutors.Register("장소 전개", c =>
        {
          int turns;
          string kind, value;
          return EffectModules.TryPlace(Duration(c.Line, out turns), out kind, out value);
        }, c =>
        {
          int turns;
          string kind, value;
          EffectModules.TryPlace(Duration(c.Line, out turns), out kind, out value);
          if (turns <= 0)
            turns = 5;
          var rules = c.Rules ?? new Rules();
          var log = c.Log ?? new List<string>();
          if (kind == "날씨")
            return Mechanics.SetWeather(c.Owner, c.Opponent, value, turns, rules, log, c.Source.Label) ? EffectOutcome.Success : EffectOutcome.Failed;
          if (kind == "지형")
            return Mechanics.SetTerrain(c.Owner, c.Opponent, value, turns, rules, log, c.Source.Label) ? EffectOutcome.Success : EffectOutcome.Failed;
          if (kind == "트릭룸")
          {
            rules.TrickRoom = rules.TrickRoom > 0 ? 0 : turns;
            c.Say("트릭룸 " + (rules.TrickRoom > 0 ? turns + "턴" : "해제") + " (" + c.Source.Label + ")");
            return EffectOutcome.Success;
          }

          if (kind == "물놀이")
          {
            rules.WaterSport = turns;
            c.Say("물놀이 " + turns + "턴");
            return EffectOutcome.Success;
          }

          if (kind == "흙놀이")
          {
            rules.MudSport = turns;
            c.Say("흙놀이 " + turns + "턴");
            return EffectOutcome.Success;
          }

          rules.Gravity = turns;
          c.Say("중력 " + turns + "턴 (" + c.Source.Label + ")");
          return EffectOutcome.Success;
        });
        // F4a 설치물(확정 25: 층수 제한을 넘으면 실패, 묶인 뒤 효과도 실패)
        PotentialExecutors.Register("설치물", c =>
        {
          bool install;
          string name;
          return EffectModules.TryHazard(c.Line, out install, out name);
        }, c =>
        {
          bool install;
          string name;
          EffectModules.TryHazard(c.Line, out install, out name);
          var rules = c.Rules ?? new Rules();
          int own = Mechanics.PartySide(c.Owner, rules);
          if (own < 0)
            own = 0;
          if (install)
            return Mechanics.InstallHazard(1 - own, name, rules, c.Log ?? new List<string>()) ? EffectOutcome.Success : EffectOutcome.Failed;
          if (!rules.Sides[own].Any())
          {
            c.Say((own + 1) + "팀 진영에 제거할 설치물이 없다");
            return EffectOutcome.Failed;
          }

          rules.Sides[own].ClearHazards();
          c.Say((own + 1) + "팀 진영의 설치물을 제거했다 (" + c.Source.Label + ")");
          return EffectOutcome.Success;
        });
        // F4a 강제교대(확정 26): 행동 뒤 교대 단계에서 상대 멤버 중 무작위
        PotentialExecutors.Register("강제교대", c => EffectModules.IsForcedSwitch(c.Line), c =>
        {
          var scope = EffectScope.Current;
          var target = c.Opponent;
          if (scope == null || target == null || target.HP <= 0)
            return EffectOutcome.Failed;
          int side = Mechanics.PartySide(target, c.Rules ?? new Rules());
          if (side < 0)
            side = Array.IndexOf(scope.Fighters, target);
          if (side < 0)
            return EffectOutcome.Failed;
          scope.ForcedSwitches.Add(new KeyValuePair<int, string>(side, c.Source.Label));
          c.Say(target.Data.name + ": " + c.Source.Label + "의 강제교대 요청");
          return EffectOutcome.Success;
        });
        // F4b 추가행동(확정 29): 사건으로 얻으면 행동 사이에 넣는다(가장 먼저 → 바로 다음, 가장 마지막에 → 턴 끝)
        PotentialExecutors.Register("추가행동", c =>
        {
          ExtraActionGrant g;
          int every;
          return ExtraActionRuntime.TryParse(c.Line, out g, out every);
        }, c =>
        {
          ExtraActionGrant g;
          int every;
          ExtraActionRuntime.TryParse(c.Line, out g, out every);
          var scope = EffectScope.Current;
          var rules = c.Rules ?? new Rules();
          if (scope == null || c.Owner == null || c.Owner.HP <= 0)
            return EffectOutcome.Failed;
          if (every > 0 && (rules.Turn <= 0 || rules.Turn % every != 0))
          {
            c.Say(c.Source.Label + ": 「" + every + "」배수 T가 아니다");
            return EffectOutcome.Failed;
          }

          g.Side = Array.IndexOf(scope.Fighters, c.Owner);
          if (g.Side < 0)
            g.Side = Mechanics.PartySide(c.Owner, rules);
          if (g.Side < 0)
            return EffectOutcome.Failed;
          g.Owner = c.Owner;
          g.Source = c.Source.Label;
          if ((g.SameMove || g.OpponentMove) && c.Event.Move != null)
            g.FixedMove = c.Event.Move.Copy();
          scope.ExtraActions.Add(g);
          c.Owner.Conditions.PendingAction = true;
          c.Say(c.Owner.Data.name + ": 추가행동을 얻었다 (" + (g.First ? "바로 다음" : "턴 마지막") + ")");
          return EffectOutcome.Success;
        });
        // G 사건으로 걸린 방호·회피·관통·해석 무효: 기간(NT동안·T종료시까지)·이번 행동·필드를 떠날 때까지
        PotentialExecutors.Register("방호 상태", c =>
        {
          int turns;
          string body = Duration(c.Line, out turns);
          string k;
          bool tr;
          MoveCondition m;
          double? rate;
          return PotentialProtection.TryGuard(body, out k, out m, out rate) || PotentialProtection.TryPierce(body, out m, out tr) || PotentialProtection.IsAnalysisImmunity(body);
        }, c =>
        {
          int turns;
          string body = Duration(c.Line, out turns);
          var owner = c.Owner;
          if (owner == null || owner.HP <= 0)
            return EffectOutcome.Failed;
          bool action = turns == 0 && ActionEvent(c.Event);
          PotentialModifiers.Add(owner, new ActiveModifier { Key = c.Source.Key, Text = body, Source = c.Source.Label, Turns = turns == 0 ? -1 : turns, Action = action, Trainer = c.Source.IsTrainerPotential });
          c.Say(owner.Data.name + ": " + body + (action ? " (이번 행동)" : turns > 0 ? " (" + turns + "T)" : " (필드를 떠날 때까지)"));
          return EffectOutcome.Success;
        });
        // 먼저 행동(사용자 결정: 우선도와 관계없이 먼저). 사건으로 얻으면 다음 순서 결정에 한 번 쓴다
        PotentialExecutors.Register("먼저 행동", c => EffectModules.IsActFirst(c.Line), c =>
        {
          if (c.Owner == null || c.Owner.HP <= 0)
            return EffectOutcome.Failed;
          c.Owner.Conditions.ActFirst = Math.Max(c.Owner.Conditions.ActFirst, EffectModules.ActFirstTier(c.Line));
          c.Say(c.Owner.Data.name + ": 다음 행동 순서에서 상대보다 먼저 행동한다");
          return EffectOutcome.Success;
        });
        // F4b 포켓몬 타입 변경(확정 30): 같은 타입이면 실패, 다르면 마지막 것. 교대하면 원래 타입
        PotentialExecutors.Register("타입 변경", c =>
        {
          int turns;
          string subject;
          string[] types;
          return Duration(c.Line, out turns) == c.Line && TypeRules.TryTypeChange(c.Line, out subject, out types);
        }, c =>
        {
          string subject;
          string[] types;
          TypeRules.TryTypeChange(c.Line, out subject, out types);
          var target = Recipient(c, subject);
          if (target == null || target.HP <= 0)
            return EffectOutcome.Failed;
          var current = target.Data.types ?? new string[0];
          if (current.Length == types.Length && current.All(types.Contains))
          {
            c.Say(target.Data.name + ": 이미 「" + string.Join("/", types) + "」 타입이다");
            return EffectOutcome.Failed;
          }

          if (target.Conditions.OriginalTypes == null)
            target.Conditions.OriginalTypes = current.ToArray();
          target.Data.types = types.ToArray();
          c.Say(target.Data.name + ": 「" + string.Join("/", types) + "」 타입이 되었다 (" + c.Source.Label + ")");
          return EffectOutcome.Success;
        });
        // F4b 사건으로 걸린 상성 지정·타입 내성 관통·자속 배율·유리한 상성: 수치 배율과 같은 기간 규칙
        PotentialExecutors.Register("상성 상태", c =>
        {
          int turns;
          string k = TypeRules.Classify(Duration(c.Line, out turns));
          return k.Length > 0 && k != "타입 변경";
        }, c =>
        {
          int turns;
          string body = Duration(c.Line, out turns);
          var owner = c.Owner;
          if (owner == null || owner.HP <= 0)
            return EffectOutcome.Failed;
          bool action = turns == 0 && ActionEvent(c.Event);
          PotentialModifiers.Add(owner, new ActiveModifier { Key = c.Source.Key, Text = body, Source = c.Source.Label, Turns = turns == 0 ? -1 : turns, Action = action });
          c.Say(owner.Data.name + ": " + body + (action ? " (이번 행동)" : turns > 0 ? " (" + turns + "T)" : " (필드를 떠날 때까지)"));
          return EffectOutcome.Success;
        });
        // F3 상태이상 치유
        PotentialExecutors.Register("상태 치유·해제", c =>
        {
          string t;
          string[] n;
          bool r;
          return StateCure.TryParse(c.Line, out t, out n, out r);
        }, c =>
        {
          string t;
          string[] n;
          bool r;
          StateCure.TryParse(c.Line, out t, out n, out r);
          var lines = new List<string>();
          bool done = StateCure.Cure(t == "상대" ? c.Opponent : c.Owner, n, lines);
          foreach (var x in lines)
            c.Say(x);
          return done ? EffectOutcome.Success : EffectOutcome.Failed;
        });
        // 특성 변경(기간이 있으면 끝날 때, 없으면 장소를 떠날 때 원래대로)
        PotentialExecutors.Register("특성 변경", c =>
        {
          int turns;
          string sj, ab;
          return MiscModules.TryAbility(Duration(c.Line, out turns), out sj, out ab);
        }, c =>
        {
          int turns;
          string sj, ab;
          MiscModules.TryAbility(Duration(c.Line, out turns), out sj, out ab);
          var target = sj == "상대" ? c.Opponent : c.Owner;
          if (target == null || target.HP <= 0)
            return EffectOutcome.Failed;
          if (AbilityRuntime.Name(target) == ab)
          {
            c.Say(target.Data.name + ": 이미 「" + ab + "」 특성이다");
            return EffectOutcome.Failed;
          }

          var lines = new List<string>();
          bool changed = MiscModules.ChangeAbility(target, ab, turns, lines, c.Source.Label);
          foreach (var x in lines)
            c.Say(x);
          return changed ? EffectOutcome.Success : EffectOutcome.Failed;
        });
        PotentialExecutors.Register("소지품 소실", c => MiscModules.IsBreak(c.Line), c =>
        {
          if (c.Owner == null || string.IsNullOrEmpty(c.Owner.Data.item))
            return EffectOutcome.Failed;
          ItemRuntime.Lose(c.Owner, "부서졌다", c.Say);
          return EffectOutcome.Success;
        });
        PotentialExecutors.Register("기능확장 부여", c =>
        {
          string subject;
          string[] moves;
          return FunctionExtensions.TryGrant(c.Line, out subject, out moves);
        }, c =>
        {
          string subject;
          string[] moves;
          FunctionExtensions.TryGrant(c.Line, out subject, out moves);
          var owner = c.Owner;
          if (owner == null || owner.HP <= 0)
            return EffectOutcome.Failed;
          foreach (string move in moves)
            if (!owner.Conditions.ExtensionMoves.Contains(move))
              owner.Conditions.ExtensionMoves.Add(move);
          c.Say(owner.Data.name + ": 기능확장 " + string.Join("·", moves) + "을 내보낼 수 있다 (장소를 떠날 때까지)");
          return EffectOutcome.Success;
        });
        PotentialExecutors.Register("상태이상 치유", c =>
        {
          string t;
          return EffectModules.TryCure(c.Line, out t);
        }, c =>
        {
          string t;
          EffectModules.TryCure(c.Line, out t);
          var target = t == "상대" ? c.Opponent : c.Owner;
          if (target == null || target.HP <= 0 || target.Status == "정상")
            return EffectOutcome.Failed;
          if (AbaddonRuntime.Sealed(target, "상태이상", c.Log))
            return EffectOutcome.Failed;
          c.Say(target.Data.name + ": " + target.Status + " 치유 (" + c.Source.Label + ")");
          target.Status = "정상";
          target.Conditions.ToxicStage = 1;
          target.Conditions.SleepFromRest = false;
          return EffectOutcome.Success;
        });
      }
    }
  }
}
