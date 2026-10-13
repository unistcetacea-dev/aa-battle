using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace AABattle {
 // 추가행동 하나(확정 29): 기술·대상을 새로 고른다(「같은 기술」이면 직전 기술). 추가행동 중에는 추가행동을 얻지 못하고,
 // 교대·빈사면 사라지며 행동불능이면 그 행동이 실패한다
 public sealed class ExtraActionGrant {
  public int Side = -1;
  public Fighter Owner;
  public bool First;                       // 가장 먼저(일반 행동 전) / 가장 마지막에(일반 행동 뒤)
  public bool SameMove;
  public bool OpponentMove;
  public Move FixedMove;
  public double PowerRate = 1;
  public MoveCondition Condition = new MoveCondition();
  public string Source = "";
 }

 // 턴의 행동 칸: 일반 행동 또는 추가행동
 public sealed class ActionSlot {
  public int Side;
  public ExtraActionGrant Extra;
  public BattleCommand Command;            // 추가행동이면 고른 기술로 만든 명령
 }

 public static class ExtraActionRuntime {
  // 「「4」배수 T, 가장 먼저 「강철」 기술에 의한 추가행동을 얻는다」
  static readonly Regex Sentence = new Regex(@"^(?:「(?<n>\d+)」\s*배수\s*T\s*,?\s*)?(?:가장\s*(?<pos>먼저|마지막에)\s*)?(?:(?<opp>상대와 같은 기술)|(?<same>같은 기술)|(?<c>(?:「[^」]+」\s*)+)\s*기술)?(?:에 의한|로)?\s*추가행동을 얻는다(?:\(위력 (?<rate>0\.\d+)배\))?$");

  public static bool TryParse(string text, out ExtraActionGrant grant, out int every) {
   grant = null; every = 0;
   var m = Sentence.Match(Regex.Replace(PotentialParts.Clean(text ?? "").Trim().TrimEnd('.', '。', '．').Trim(), @"\s+", " "));
   if (!m.Success) return false;
   MoveCondition c = new MoveCondition();
   if (m.Groups["c"].Success && !MoveCondition.TryParse(m.Groups["c"].Value, out c)) return false;
   if (m.Groups["n"].Success) every = int.Parse(m.Groups["n"].Value);
   grant = new ExtraActionGrant { First = m.Groups["pos"].Value == "먼저", SameMove = m.Groups["same"].Success, OpponentMove = m.Groups["opp"].Success, Condition = c,
    PowerRate = m.Groups["rate"].Success ? double.Parse(m.Groups["rate"].Value, System.Globalization.CultureInfo.InvariantCulture) : 1 };
   return true;
  }

  // 표준 문장
  public static string Canonical(int every, bool first, MoveCondition condition, bool same) {
   return (every > 0 ? "「" + every + "」배수 T, " : "") + "가장 " + (first ? "먼저" : "마지막에") + " " + (same ? "같은 기술에 의한 " : condition != null && !condition.Empty ? condition.Text + " 기술에 의한 " : "") + "추가행동을 얻는다.";
  }

  // 턴 시작: 계속 성립하는 포텐셜의 추가행동(배수 턴 조건 포함)
  public static List<ActionSlot> Build(IEnumerable<int> movers, Fighter[] f, Rules r) {
   var first = new List<ActionSlot>(); var last = new List<ActionSlot>();
   for (int side = 0; side < 2; side++) {
    var owner = f[side];
    if (owner == null || owner.HP <= 0 || !owner.OnField) continue;
    foreach (string s in PotentialModifiers.Sentences(owner, f[1 - side], r, "추가")) {
     ExtraActionGrant grant; int every;
     if (!TryParse(s, out grant, out every) || every > 0 && (r.Turn <= 0 || r.Turn % every != 0)) continue;
     grant.Side = side; grant.Owner = owner; grant.Source = owner.Data.name + "의 포텐셜";
     (grant.First ? first : last).Add(new ActionSlot { Side = side, Extra = grant });
    }
   }
   Func<ActionSlot, int> speed = s => Engine.Effective(f[s.Side], f[1 - s.Side], 5, r);
   return first.OrderByDescending(speed).Concat(movers.Select(i => new ActionSlot { Side = i })).Concat(last.OrderByDescending(speed)).ToList();
  }

  // 행동 사이: 사건으로 얻은 추가행동을 넣는다. 추가행동 중에 얻은 것은 버린다
  public static void Pull(List<ActionSlot> slots, int next, List<string> log) {
   var scope = EffectScope.Current;
   if (scope == null || scope.ExtraActions.Count == 0) return;
   var grants = scope.ExtraActions.ToList();
   scope.ExtraActions.Clear();
   bool duringExtra = next > 0 && next - 1 < slots.Count && slots[next - 1].Extra != null;
   foreach (var g in grants) {
    if (duringExtra) { log.Add(g.Source + ": 추가행동 중에는 추가행동을 얻을 수 없다"); continue; }
    var slot = new ActionSlot { Side = g.Side, Extra = g };
    if (g.First) slots.Insert(Math.Min(next, slots.Count), slot); else slots.Add(slot);
   }
  }

  // 추가행동 직전: 주인이 그대로 장소에 있는지 확인하고 기술을 고른다(조건에 맞고 지금 쓸 수 있는 기술)
  public static bool Prepare(Database db, ActionSlot slot, Fighter[] f, BattleCommand original, Rules r, List<string> log) {
   var g = slot.Extra; var a = f[g.Side]; var b = f[1 - g.Side];
   if (a == null || a.HP <= 0 || !a.OnField || !Mechanics.SameMember(a, g.Owner)) { log.Add(g.Source + ": 교대·빈사로 추가행동이 사라졌다"); return false; }
   var moves = (a.Moves ?? new string[0]).Select(n => db.moves.FirstOrDefault(x => x != null && x.name == n)).Where(x => x != null).ToList();
   if (g.FixedMove != null) moves = new List<Move> { g.FixedMove };
   else if (g.SameMove) moves = moves.Where(x => x.name == a.Conditions.LastMove).ToList();
   else if (g.Condition.Names.Count > 0) moves = db.moves.Where(x => g.Condition.Names.Contains(x.name)).ToList();
   var candidates = moves.Where(x => Mechanics.MoveBlock(a, x, false).Length == 0 && g.Condition.Matches(x, a, b == null ? 1 : Mechanics.TypeEffect(db, a, b, x, r))).ToArray();
   if (candidates.Length == 0) { log.Add(g.Source + ": 추가행동에 쓸 수 있는 기술이 없다"); return false; }
   var chosen = r.ChooseExtraMove == null ? null : r.ChooseExtraMove(a, candidates);
   if (chosen == null || !candidates.Contains(chosen)) chosen = candidates[0];
   slot.Command = new BattleCommand { Move = chosen, Extension = !(a.Moves ?? new string[0]).Contains(chosen.name), Party = original == null ? null : original.Party, PreInputs = original == null ? null : original.PreInputs, Trainer = original == null ? null : original.Trainer };
   log.Add(g.Source + " — " + a.Data.name + "의 추가행동: " + chosen.name);
   return true;
  }
 }
}
