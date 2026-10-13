namespace AABattle
{
  // 약탈 사건은 기존 소지품 소실 모듈의 Method 구분으로 표현한다.
  public static class TriggerTheft
  {
    public static bool IsTheft(TriggerSpec spec)
    {
      return spec != null && spec.Module == "소지품 소실" && spec.Method == "약탈";
    }

    public static bool TryParse(string text, out TriggerSpec spec)
    {
      spec = null;
      if (text != "자신의 소지품을 빼앗겼을 때" && text != "상대의 소지품을 빼앗겼을 때")
        return false;
      spec = new TriggerSpec { Module = "소지품 소실", Method = "약탈", Opponent = text.StartsWith("상대") };
      return true;
    }

    public static string Canonical(TriggerSpec spec)
    {
      return (spec.Opponent ? "상대" : "자신") + "의 소지품을 빼앗겼을 때";
    }

    public static bool Matches(TriggerSpec spec, TriggerFact fact)
    {
      return fact.Kind == "소지품 소실" && fact.Method == "약탈" && spec.Opponent != fact.Self;
    }
  }
}
