using System;
using System.Linq;
using System.Collections.Generic;

namespace AABattle
{
  // 기술 이름인지 가볍게 확인한다(분류 표 기준). 정규화가 「X」를 기술 이름으로 읽을지 정하는 데 쓴다
  public static class MoveNames
  {
    static HashSet<string> names;

    public static bool Known(string name)
    {
      if (names == null)
        names = new HashSet<string>(MoveCatalog.All.Select(x => x.name));
      return names.Contains((name ?? "").Trim());
    }
  }
}
