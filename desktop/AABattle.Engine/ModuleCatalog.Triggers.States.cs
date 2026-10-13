using System;
using System.Collections.Generic;
using System.Linq;

namespace AABattle {
 public static partial class ModuleCatalog {
  static readonly string[] CatalogTriggerStats = { "공격", "방어", "특공", "특방", "속도", "명중", "회피" };

  static TriggerBinding TRankStats() {
   var slot = S("능력", ModuleSlotKind.Stats, "공격", new[] { "", "전능력치" }.Concat(CatalogTriggerStats).ToArray());
   slot.Multiple = true;
   return TB(slot, (s, v) => s.Stats = v.Length == 0 ? new int[0] : v == "전능력치" ? new[] { 1, 2, 3, 4, 5 } : v.Split('/').Select(x => Array.IndexOf(CatalogTriggerStats, x) + 1).ToArray(),
    s => s.Stats.SequenceEqual(new[] { 1, 2, 3, 4, 5 }) ? "전능력치" : string.Join("/", s.Stats.Select(x => CatalogTriggerStats[x - 1])));
  }

  static void RegisterTriggerStates(List<ModuleDefinition> modules) {
   foreach (string module in new[] { "상태", "상태 됨", "상태 해제" }) {
    string m = module;
    T(modules, "state." + m, m, m, "체력·상태", m == "상태" ? "{주체}가 「{이름}」 상태일 때" :
     m == "상태 됨" ? "{주체}가 「{이름}」 상태가 되었을 때" : "{주체}의 「{이름}」 상태가 해제되었을 때",
     () => new TriggerSpec { Module = m }, TS(m == "상태" ? new[] { "자신", "상대", "아군", "자신과 상대" } : new[] { "자신", "상대", "아군" }),
     TN(ModuleSlotKind.State, "화상", TriggerModules.KnownStates().Where(x => m != "상태 해제" || x != "빈사").ToArray()));
   }
   foreach (string op in new[] { "가득", "가득 아님", "이하", "이상" }) {
    string o = op;
    var bindings = new List<TriggerBinding> { TS("자신", "상대") };
    if (o == "이하" || o == "이상") {
     bindings.Add(TI("분자", ModuleSlotKind.Count, "1", (s, n) => s.Top = n, s => s.Top));
     bindings.Add(TI("분모", ModuleSlotKind.Denominator, "2", (s, n) => s.Bottom = n, s => s.Bottom));
    }
    string phrase = o == "가득" ? "가득일 때" : o == "가득 아님" ? "가득하지 않을 때" : "{분자}/{분모} " + o + "일 때";
    T(modules, "hp." + o, "체력", "현재 체력 " + o, "체력·상태", "{주체}의 체력이 " + phrase,
     () => new TriggerSpec { Module = "체력", Op = o }, bindings.ToArray());
   }
   foreach (string op in new[] { "가득", "가득 아님", "1", "감소", "이하" }) {
    string o = op;
    var bindings = new List<TriggerBinding> { TS("자신", "상대") };
    if (o == "이하") {
     bindings.Add(TI("분자", ModuleSlotKind.Count, "1", (s, n) => s.Top = n, s => s.Top));
     bindings.Add(TI("분모", ModuleSlotKind.Denominator, "2", (s, n) => s.Bottom = n, s => s.Bottom));
    }
    string phrase = o == "가득" ? "가득해졌을 때" : o == "가득 아님" ? "가득하지 않게 되었을 때" : o == "1" ? "「1」이 되었을 때" :
     o == "감소" ? "감소했을 때" : "{분자}/{분모} 이하가 되었을 때";
    T(modules, "hpchange." + o, "체력 변화", "체력 변화 " + o, "체력·상태", "{주체}의 체력이 " + phrase,
     () => new TriggerSpec { Module = "체력 변화", Op = o }, bindings.ToArray());
   }
   TF(modules, "weather.any", "날씨", "날씨 있음", "날씨·장소", "날씨가 있을 때");
   TF(modules, "weatherchange.any", "날씨 변화", "모든 날씨 변화", "날씨·장소", "날씨가 변화했을 때");
   foreach (string module in new[] { "날씨", "날씨 변화" }) {
    string m = module;
    T(modules, m == "날씨" ? "weather" : "weatherchange", m, m, "날씨·장소",
     m == "날씨" ? "날씨가 「{장소}」일 때" : "날씨가 「{장소}」가 되었을 때",
     () => new TriggerSpec { Module = m }, TP(ModuleSlotKind.Weather, "비", Mechanics.Weathers.Where(x => x != "없음").ToArray()));
   }
   foreach (string module in new[] { "장소", "장소 변화" }) {
    string m = module;
    T(modules, m == "장소" ? "terrain" : "terrainchange", m, m == "장소" ? "지형 전개 중" : "지형이 바뀜", "날씨·장소",
     m == "장소" ? "「{장소}」일 때" : "「{장소}」가 되었을 때",
     () => new TriggerSpec { Module = m }, TP(ModuleSlotKind.Terrain, "그래스필드", EffectModules.TerrainNames));
   }
   T(modules, "room", "장소", "룸·중력 전개 중", "날씨·장소", "「{장소}」이 전개되어 있을 때",
    () => new TriggerSpec { Module = "장소" }, TP(ModuleSlotKind.Room, "트릭룸", "트릭룸", "중력"));
   foreach (bool release in new[] { false, true }) {
    bool r = release;
    T(modules, "roomchange." + r, "장소 변화", r ? "룸·중력 해제" : "룸·중력 전개", "날씨·장소",
     "「{장소}」이 " + (r ? "해제" : "전개") + "되었을 때",
     () => new TriggerSpec { Module = "장소 변화", Release = r }, TP(ModuleSlotKind.Room, "트릭룸", "트릭룸", "중력"));
   }
   foreach (string variant in new[] { "상승", "큰 상승", "하락", "상대에 의한 하락" }) {
    string v = variant;
    T(modules, "rank." + v, "랭크", "능력 랭크 " + v, "능력", (v == "상대에 의한 하락" ? "상대에 의해 " : "") + "{주체}의 「{능력}」이 " +
     (v == "상승" ? "올랐을 때" : v == "큰 상승" ? "크게 올랐을 때" : "내려갔을 때"),
     () => new TriggerSpec { Module = "랭크", Rise = v == "상승" || v == "큰 상승", Big = v == "큰 상승", ByOpponent = v == "상대에 의한 하락" },
     TS(v == "상대에 의한 하락" ? new[] { "자신" } : new[] { "자신", "상대" }), TRankStats());
   }
   foreach (string op in new[] { "이하", "높음" }) {
    string o = op;
    T(modules, "stats.compare." + o, "능력치 비교", "두 능력치 비교: " + o, "능력", "{주체}의 「{왼쪽}」이 「{오른쪽}」" + (o == "높음" ? "보다 높을 때" : " 이하일 때"),
     () => new TriggerSpec { Module = "능력치 비교", Stats = new[] { 1, 3 }, Op = o }, TS("자신", "아군", "상대"),
     TB(S("왼쪽", ModuleSlotKind.Stats, "공격", CatalogTriggerStats.Take(5).ToArray()), (s, x) => s.Stats[0] = Array.IndexOf(CatalogTriggerStats, x) + 1, s => CatalogTriggerStats[s.Stats[0] - 1]),
     TB(S("오른쪽", ModuleSlotKind.Stats, "특공", CatalogTriggerStats.Take(5).ToArray()), (s, x) => s.Stats[1] = Array.IndexOf(CatalogTriggerStats, x) + 1, s => CatalogTriggerStats[s.Stats[1] - 1]));
   }
   TX(modules, "speed", "속도 비교", "상대와 속도 비교", "능력", "자신의 「속도」가 상대보다 {비교} 때", "비교", "높을", "낮을");
   TF(modules, "firstaction", "첫 행동 전", "등장 후 첫 행동 전", "턴·행동", "장소에 나와 아직 기술을 내보내지 않았을 때");
   TX(modules, "floating", "공중", "공중에 떠 있음", "체력·상태", "{주체} 공중에 떠 있을 때", "주체", "자신이", "서로");
   TF(modules, "sametype", "같은 타입", "상대와 같은 타입", "능력", "상대가 자신과 같은 타입일 때");
   TF(modules, "notlast", "최후 아님", "최후의 1체가 아님", "파티·트레이너", "자신이 「최후의 1체」가 아닐 때");
   T(modules, "party.ability", "PT 특성", "PT에 지정 특성 아군 있음", "파티·트레이너", "PT에 특성이 「{이름}」인 아군이 있을 때",
    () => new TriggerSpec { Module = "PT 특성" }, TN(ModuleSlotKind.Ability, "마이너스/플러스"));
   T(modules, "or", "또는", "두 상태 조건 중 하나", "조건 조합", "{조건A}이거나 {조건B}",
    () => new TriggerSpec { Module = "또는", Names = new string[2] },
    TB(S("조건A", ModuleSlotKind.Trigger, "날씨가 「비」일 때"), (s, x) => s.Names[0] = x, s => s.Names[0]),
    TB(S("조건B", ModuleSlotKind.Trigger, "날씨가 「쾌청」일 때"), (s, x) => s.Names[1] = x, s => s.Names[1]));
  }
 }
}
