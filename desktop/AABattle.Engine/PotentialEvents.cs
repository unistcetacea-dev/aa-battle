// 사건·효과 실행기 모델과 사용 이력
using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace AABattle
{
  // 포텐셜 공통 효과 큐 (EFFECT_QUESTIONS_30 확정 01~09, TRIGGER_QUESTIONS_50 T-05·T-06).
  //
  // 흐름: 트리거 성립(기존 매칭 함수) → PotentialQueue.Raise(사건)
  //   → 후보 정렬(속도 내림차순, 동속 rng) → 포텐셜마다
  //     a. 중복 차단(같은 사건 1회, 같은 연쇄 1회) b. 사용 제한(1/시·1/T·최초) c. 확률(소유자·사건 묶음 1회)
  //     d. 「할 수 있다」 확인 e. 사용 횟수 소비 f. 효과 실행(독립 / 대가·「이때」 종속)
  // 효과 실행은 PotentialExecutors에 등록된 실행기가 맡는다. 실행기가 없는 효과는 예전처럼 "효과 판정 대기"로 남긴다.
  public enum EffectOutcome
  {
    Success, // 효과가 실제로 적용됨
    Failed, // 면역·중복 등으로 실패(사용 횟수는 돌려주지 않는다)
    Pending, // 엔진이 처리하지 않음(사람이 판정). 실패가 아니다
    Deferred // 다른 단계가 처리(교대 요청 등). 사용 횟수도 그 단계가 소비한다
  }

  // 포텐셜을 발동시킬 수 있는 사건 하나
  public sealed class PotentialEvent
  {
    public string Label = ""; // 정규화 트리거 라벨(라벨 사건). 조건식 사건이면 설명용
    public Func<string, bool> Clause; // 조건식 사건: 정규화 전 절 문자열(공백 제거)을 받아 성립 여부
    public Func<string, bool> Spec; // 트리거 모듈 사건: 절 문자열을 그대로 받아 성립 여부(TriggerModules)
    public Fighter Owner; // 발동 관점의 포켓몬(「자신」). 트레이너 사건이면 장소의 자기 포켓몬
    public TrainerRecord Trainer; // 트레이너 포텐셜 사건이면 트레이너
    public Fighter Other; // 상대(없으면 장소의 상대)
    public string Message = ""; // 사건 문맥(로그용)
    public Move Move;
    public TriggerFact Fact; // 트리거 모듈 사건이면 그 사건(랭크 변화량·상태 이름 등을 읽는 효과용)
    public string SlotOnly = ""; // 지정하면 그 슬롯의 포텐셜만 대상(나무열매를 강제로 사용할 때 소지품 슬롯)
    public int Damage, Hit;
    public Action<string> Log; // 호출한 쪽의 로그
    public bool Announce; // 처리 직전에 Message를 로그에 남긴다(TriggerStructures.Fire 형식)
    public Func<PotentialInstance, bool, string> PendingText; // 실행하지 않을 때의 로그(호출 경로별 예전 문구). bool = 추가 조건 있음
    internal int Id, ChainId, Depth;
    public bool Matches(string clause)
    {
      if (Spec != null)
        return Spec(clause ?? "");
      if (Clause != null)
        return Clause(Regex.Replace(PotentialParts.Clean(clause ?? ""), @"\s+", ""));
      return TriggerStructures.Normalize(clause) == Label;
    }
  }

  // 사건에 걸린 포텐셜 하나(포켓몬 또는 트레이너). Key = 소유자 데이터 ID + 슬롯 (+ 같은 슬롯 안의 순번)
  public sealed class PotentialInstance
  {
    public Fighter Owner;
    public TrainerRecord Trainer;
    public Potential Pokemon;
    public PotentialRecord Record;
    public string Key = "", Name = "", Slot = "", Uses = "", Activation = "", Effect = "", Description = "";
    public string[] Clauses = new string[0];
    public bool IsTrainer
    {
      get
      {
        return Trainer != null;
      }
    }

    // 트레이너 포텐셜이다: 트레이너 쪽 기록이거나, 포켓몬 편집에서 단 「전용」(작용 대상은 그 포켓몬).
    // 트레이너 방호·관통과 로그 표기는 이쪽을 본다. 실행기 분담(IsTrainer)은 기록 출처만 본다
    public bool IsTrainerPotential
    {
      get
      {
        return IsTrainer || Pokemon != null && Pokemon.id == TeamRules.ExclusiveSlot;
      }
    }

    public string Label
    {
      get
      {
        return IsTrainerPotential ? "TRAINER 『" + Name + "』" : Slot == AbilityModules.Slot ? "〔" + Name + "〕" : "『" + Name + "』";
      }
    }

    // OptionalEffectPrompt는 Potential을 받으므로 트레이너 포텐셜은 임시 Potential로 넘긴다
    public Potential AsPotential()
    {
      return Pokemon ?? new Potential
      {
        id = Slot,
        name = Name,
        description = Effect,
        activation = Activation,
        uses = Uses,
        triggers = Clauses
      };
    }
  }

  // 실행기 한 번의 호출
  public sealed class EffectCall
  {
    public PotentialInstance Source;
    public PotentialEvent Event;
    public string Line = "";
    public Rules Rules;
    public Func<double> Rng;
    public List<string> Log; // 실행기가 남긴 줄. 큐가 사건의 로그로 옮긴다
    public Fighter Owner
    {
      get
      {
        return Source.Owner;
      }
    }

    public Fighter Opponent
    {
      get
      {
        return Event.Other ?? PotentialQueue.OpponentOf(Source.Owner);
      }
    }

    public void Say(string text)
    {
      if (Log != null)
        Log.Add(text);
    }
  }

  // 실행기: 효과 문장 한 줄을 처리할 수 있는지(CanRun)와 결과(Run)
  public sealed class PotentialExecutor
  {
    public string Name = "";
    public Func<EffectCall, bool> CanRun;
    public Func<EffectCall, EffectOutcome> Run;
  }

  // 실행기 등록부. F2 이후 작업은 Register로 실행기를 붙인다(먼저 등록한 실행기가 우선)
  public static class PotentialExecutors
  {
    static readonly List<PotentialExecutor> all = new List<PotentialExecutor>();
    public static IEnumerable<PotentialExecutor> All
    {
      get
      {
        return all;
      }
    }

    static PotentialExecutors()
    {
      MoveMiscModules.Register();
      AbilityRemaining4Modules.Register();
      PotentialQueue.BuiltIn.Register();
      MoveModules.RegisterExecutors();
      AbilityChangeModules.Register();
      BatonModules.Register();
      PursuitModules.Register();
      AbilityItemModules.Register();
      AbilityEventModules.Register();
      AbilityNoEffectModules.Register();
      BondModules.Register();
      PotentialQueue.BuiltIn.RegisterExclusive();
    }

    public static PotentialExecutor Register(string name, Func<EffectCall, bool> canRun, Func<EffectCall, EffectOutcome> run)
    {
      var executor = new PotentialExecutor
      {
        Name = name,
        CanRun = canRun,
        Run = run
      };
      all.Add(executor);
      return executor;
    }

    public static void Unregister(PotentialExecutor executor)
    {
      all.Remove(executor);
    }

    public static PotentialExecutor Find(EffectCall call)
    {
      return all.FirstOrDefault(x => x.CanRun(call));
    }
  }

  // 포켓몬·트레이너 포텐셜 사용 이력(인스턴스 키 기준). TrainerBattleState가 진영별로 들고 배틀 내내 복사된다
  public sealed class PotentialUsage
  {
    public Dictionary<string, int> Battle = new Dictionary<string, int>(); // 1/시·최초 1회
    public Dictionary<string, int> Turn = new Dictionary<string, int>(); // 1/T (턴 종료에 초기화)
    public HashSet<string> Declared = new HashSet<string>(); // 이번 T 선행입력 선언(선언 때 이미 소비)
    public Dictionary<string, List<BondEntry>> Bond = new Dictionary<string, List<BondEntry>>(); // 포켓몬 키 → 발동했거나 받은 트레이너 포텐셜 이력(유대)
    public PotentialUsage Copy()
    {
      return new PotentialUsage
      {
        Battle = new Dictionary<string, int>(Battle),
        Turn = new Dictionary<string, int>(Turn),
        Declared = new HashSet<string>(Declared),
        Bond = Bond.ToDictionary(x => x.Key, x => new List<BondEntry>(x.Value))
      };
    }

    // "1/시"·"2/시"·"최초" → (횟수, 배틀 단위) / "1/T" → (횟수, 턴 단위) / 그 밖 → 제한 없음
    public static bool TryLimit(string uses, out int count, out bool perTurn)
    {
      count = 0;
      perTurn = false;
      string value = PotentialParts.Clean(uses ?? "").Replace(" ", "");
      if (value.Length == 0)
        return false;
      if (value.Contains("최초"))
      {
        count = 1;
        return true;
      }

      var match = Regex.Match(value, @"(?<n>\d+)/(?<unit>시|T|턴)");
      if (!match.Success)
        return false;
      count = int.Parse(match.Groups["n"].Value);
      perTurn = match.Groups["unit"].Value != "시";
      return count > 0;
    }

    public bool CanUse(string key, string uses)
    {
      int count;
      bool perTurn;
      if (!TryLimit(uses, out count, out perTurn))
        return true;
      var table = perTurn ? Turn : Battle;
      int used;
      table.TryGetValue(key, out used);
      return used < count;
    }

    public void Use(string key, string uses)
    {
      int count;
      bool perTurn;
      if (!TryLimit(uses, out count, out perTurn))
        return;
      var table = perTurn ? Turn : Battle;
      int used;
      table.TryGetValue(key, out used);
      table[key] = used + 1;
    }

    public bool Used(string key)
    {
      return Battle.ContainsKey(key) || Turn.ContainsKey(key);
    }

    // 소지품을 되돌려받으면 그 소지품의 사용 이력도 지운다(다시 쓸 수 있다)
    public void Forget(string prefix)
    {
      foreach (string key in Battle.Keys.Where(x => x.StartsWith(prefix, StringComparison.Ordinal)).ToList())
        Battle.Remove(key);
      foreach (string key in Turn.Keys.Where(x => x.StartsWith(prefix, StringComparison.Ordinal)).ToList())
        Turn.Remove(key);
    }

    public void EndTurn()
    {
      Turn.Clear();
      Declared.Clear();
    }
  }

  public static class PotentialKeys
  {
    static string Owner(Pokemon p)
    {
      return p == null ? "" : !string.IsNullOrEmpty(p.id) ? p.id : p.name;
    }

    // 같은 슬롯에 포텐셜이 여럿이면 순번으로 구분한다
    public static string For(Pokemon owner, Potential potential)
    {
      var list = (owner == null ? null : owner.potentials) ?? new Potential[0];
      string slot = potential == null ? "" : potential.id ?? "";
      int ordinal = list.Where(x => x != null && (x.id ?? "") == slot).ToList().IndexOf(potential);
      return Owner(owner) + "|" + slot + "|" + Math.Max(0, ordinal);
    }

    public static string For(TrainerRecord trainer, PotentialRecord record)
    {
      var list = (trainer == null ? null : trainer.potentials) ?? new List<PotentialRecord>();
      string slot = record == null ? "" : record.slot ?? "";
      int ordinal = list.Where(x => x != null && (x.slot ?? "") == slot).ToList().IndexOf(record);
      string owner = trainer == null ? "" : !string.IsNullOrEmpty(trainer.id) ? trainer.id : trainer.name;
      return "T:" + owner + "|" + slot + "|" + Math.Max(0, ordinal);
    }

    // 같은 슬롯의 모든 키가 공유하는 앞부분(사용 이력 초기화용)
    public static string SlotPrefix(Pokemon owner, string slot)
    {
      return Owner(owner) + "|" + slot + "|";
    }

    // 선행입력 선언처럼 이름만 아는 경우
    public static string ForName(Pokemon owner, string name)
    {
      var potential = (owner == null ? null : owner.potentials ?? new Potential[0]).FirstOrDefault(x => x != null && x.name == name);
      return potential == null ? Owner(owner) + "|?|" + name : For(owner, potential);
    }
  }
}
