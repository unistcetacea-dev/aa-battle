using System.Collections.Generic;

namespace AABattle {
 public static partial class ModuleCatalog {
  // 모듈 목록에 넣지 않은 효과 종류와 그 이유. 목록에 없는 종류는 누락 검사가 실패한다.
  public static readonly Dictionary<string, string> OmittedEffectKinds = new Dictionary<string, string>();
 }
}
