using System.Linq;

namespace AABattle
{
  // 출처: 칼데아 『베이직：킹』 · 사용자 결정(전용 용량을 6으로 덮어쓴다).
  public static class ExclusiveCapacityRules
  {
    public static int Limit(TrainerRecord trainer, int ordinaryLimit)
    {
      bool capacitySix = trainer != null && (trainer.potentials ?? new System.Collections.Generic.List<PotentialRecord>())
        .Any(p => p != null && p.enabled && !string.IsNullOrWhiteSpace(p.name) && p.slot == "고유"
          && (p.effect ?? "").Contains("자신의 『전용』 용량을 「6」으로 한다"));
      return capacitySix ? 6 : ordinaryLimit;
    }
  }
}
