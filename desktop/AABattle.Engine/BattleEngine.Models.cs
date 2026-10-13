// 배틀 규칙·명령·결과와 진영 데이터
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Reflection;
using System.Web.Script.Serialization;

namespace AABattle
{
  public class SideFieldState
  {
    public bool StealthRock, StickyWeb;
    public int Spikes, ToxicSpikes;
    public int Reflect, LightScreen, IceWall, Tailwind;
    public int Safeguard, Mist, LuckyChant, WishTurns, WishHeal, SeaOfFire, Swamp, Rainbow;
    public bool QuickGuard;
    public SideFieldState Copy()
    {
      return (SideFieldState)MemberwiseClone();
    }

    public void ClearHazards()
    {
      StealthRock = StickyWeb = false;
      Spikes = ToxicSpikes = 0;
    }

    public void Clear()
    {
      ClearHazards();
      Reflect = LightScreen = IceWall = Tailwind = 0;
    }

    public bool HasScreens()
    {
      return Reflect > 0 || LightScreen > 0 || IceWall > 0;
    }

    public void ClearScreens()
    {
      Reflect = LightScreen = IceWall = 0;
    }

    public bool Any()
    {
      return StealthRock || StickyWeb || Spikes > 0 || ToxicSpikes > 0 || Reflect > 0 || LightScreen > 0 || IceWall > 0 || Tailwind > 0;
    }

    public bool HasHazards()
    {
      return StealthRock || StickyWeb || Spikes > 0 || ToxicSpikes > 0;
    }
  }

  public class Rules
  {
    public string Weather = "없음", Terrain = "없음";
    public string WeatherLockOwner = "";
    public string[] WeatherLockMembers = new string[0];
    public bool Critical, HPCorrection, IgnoreDefenderRaise;
    public Func<Fighter, Fighter, Potential, bool> OptionalEffectPrompt;
    public Func<PotentialInstance, Fighter, string[], string> ChooseStat;
    public Func<Fighter, string[], string> ChooseSealMove; // 봉인할 상대 기술 선택(없거나 null이면 마지막으로 낸 기술)
    public Dictionary<string, string> SealedMoves = new Dictionary<string, string>(); // 봉인(교대해도 유지): 멤버 키 → 기술
    public Func<Fighter, Move[], Move> ChooseExtraMove; // 추가행동 기술 선택(없으면 첫 후보)        // 「임의의 능력」 선택(없거나 null을 돌려주면 판정 대기)
    public Func<PotentialInstance, Fighter, int, int, int, int> ChooseSpeciesValue; // 등급 범위 안 종족치 선택(능력 색인, 최소, 최대 → 값, 음수면 최솟값)
    public double PowerBonus, PowerMultiplier = 1, DamageMultiplier = 1, Stab, LevelCorrection = 1;
    public int SlightSpeciesBonus = 10;
    public string[] LastFaintedAbility = new string[2], LastFaintedMember = new string[2];
    public Fighter[][] Parties =
    {
      new Fighter[0],
      new Fighter[0]
    };
    public int Turn = 1;
    public string WatchPlace;
    public int WeatherTurns = 5, TerrainTurns = 5, TrickRoom, Gravity, WaterSport, MudSport;
    public bool SheetTerrain;
    public SideFieldState[] Sides =
    {
      new SideFieldState(),
      new SideFieldState()
    };
    public FieldEffectRecord[] FieldEffects = new FieldEffectRecord[0];
    public Rules Copy()
    {
      var copy = (Rules)MemberwiseClone();
      copy.LastFaintedAbility = (string[])LastFaintedAbility.Clone();
      copy.LastFaintedMember = (string[])LastFaintedMember.Clone();
      copy.Parties = (Parties ?? new[]
      {
        new Fighter[0],
        new Fighter[0]
      }

      ).Select(x => (x ?? new Fighter[0]).Where(y => y != null).Select(y => y.Copy()).ToArray()).ToArray();
      copy.Sides = (Sides ?? new[]
      {
        new SideFieldState(),
        new SideFieldState()
      }

      ).Select(x => (x ?? new SideFieldState()).Copy()).ToArray();
      copy.FieldEffects = (FieldEffects ?? new FieldEffectRecord[0]).Where(x => x != null).Select(x => x.Copy()).ToArray();
      copy.SealedMoves = new Dictionary<string, string>(SealedMoves ?? new Dictionary<string, string>());
      return copy;
    }
  }

  public class Damage
  {
    public int Min, Max;
    public int[] Rolls;
    public double Stab, Type;
    public bool Supported;
    public string Reason = "";
  }

  // 포텐셜 전체 실행기는 단계적으로 확장 중이며, 교대 효과는 SwitchRuntime에서 실행한다.
  public enum BattleEvent
  {
    OnEnter,
    BeforeMove,
    BeforeDamage,
    AfterDamage,
    TurnEnd
  }

  public class TrainerBattleState
  {
    public TrainerRecord Trainer;
    public HashSet<string> UsedForms = new HashSet<string>(), UsedOrders = new HashSet<string>(), UnlockedCapabilities = new HashSet<string>();
    public PotentialUsage Usage = new PotentialUsage(); // 포텐셜 사용 이력(인스턴스 키 기준)
    public HashSet<string> Analyzed = new HashSet<string>(); // 이 진영이 해석한 상대(BattleRecords.Key)
    public bool Here;
    public int InitialSize; // 「여기다！」 성립 / 처음 인원
    public void RefreshUnlocks(IEnumerable<Potential> active = null)
    {
      foreach (string value in TrainerAccessRules.UnlocksFromTrainer(Trainer).Concat(TrainerAccessRules.UnlocksFromActive(active)))
        UnlockedCapabilities.Add(value);
    }

    public TrainerBattleState Copy()
    {
      var s = new TrainerBattleState
      {
        Trainer = Trainer
      };
      s.UsedForms = new HashSet<string>(UsedForms);
      s.UsedOrders = new HashSet<string>(UsedOrders);
      s.Usage = Usage.Copy();
      s.UnlockedCapabilities = new HashSet<string>(UnlockedCapabilities);
      s.Analyzed = new HashSet<string>(Analyzed);
      s.Here = Here;
      s.InitialSize = InitialSize;
      return s;
    }
  }

  public class DelayedAttackState
  {
    public Move Move;
    public string SourceName = "";
    public int SourceSide = -1, SourceLevel, SourceSpecialAttack, TurnsRemaining;
    public string[] SourceTypes = new string[0];
    public DelayedAttackState Copy()
    {
      return new DelayedAttackState
      {
        Move = Move == null ? null : new Move
        {
          name = Move.name,
          category = Move.category,
          effect = Move.effect,
          attack = Move.attack,
          defense = Move.defense,
          types = (Move.types ?? new string[0]).ToArray(),
          tags = (Move.tags ?? new string[0]).ToArray(),
          power = Move.power,
          accuracy = Move.accuracy,
          priority = Move.priority
        },
        SourceName = SourceName,
        SourceSide = SourceSide,
        SourceLevel = SourceLevel,
        SourceSpecialAttack = SourceSpecialAttack,
        TurnsRemaining = TurnsRemaining,
        SourceTypes = (SourceTypes ?? new string[0]).ToArray()
      };
    }
  }

  public class BattleCommand
  {
    public Move Move;
    public bool Extension;
    public Fighter SwitchIn;
    public TrainerRecord Trainer;
    public string Form = "", Order = "";
    public string[] PreInputs = new string[0];
    public Fighter[] Party = new Fighter[0];
    public bool IsSwitch
    {
      get
      {
        return SwitchIn != null;
      }
    }
  }

  public class TurnResult
  {
    public Rules Rules;
    public Fighter[] Fighters;
    public Fighter[] SwitchedOut = new Fighter[2];
    public TrainerBattleState[] Trainers;
    public bool[] Switched = new bool[2];
    public List<SwitchRecord> Switches = new List<SwitchRecord>();
    public List<string> Log = new List<string>();
  }
}
