using System;

namespace AABattle
{
  public static class HpScaledChance
  {
    public static double Probability(double statedProbability, int currentHp, int maxHp)
    {
      if (maxHp <= 1)
        return statedProbability;
      int hp = Math.Max(1, Math.Min(maxHp, currentHp));
      return .01 + (statedProbability - .01) * (maxHp - hp) / (maxHp - 1.0);
    }
  }
}
