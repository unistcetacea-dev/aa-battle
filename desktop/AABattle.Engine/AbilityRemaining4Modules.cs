using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace AABattle {
 // 남은 특성 모듈화 4차의 효과 모듈(특성·포텐셜 공용 표준 문장)
 //  필드 타입별 체력      필드에 있는 「독」 타입 전원의 체력을 최대 HP의 1/8 회복한다. / 필드에 있는 「독」 타입 이외의 전원에게 최대 HP의 1/8 대미지를 준다.
 //  올라간 만큼 올림      자신의 능력을 올라간 만큼 올린다.                      (사건의 랭크 변화량만큼 같은 능력을 더 올린다)
 //  빈사 아군 비례 배율   자신의 「공격/특공」을 빈사 상태인 아군의 수에 비례해 강화(1.2배~1.6배)한다.   (0명 1배, n명 1.1+0.1n배, 5명 이상 1.6배)
 //  위협 하락 무효        상대의 〔위협〕에 의한 능력 하락을 무효화한다.
 //  기술머신 추가행동     가장 마지막에 소지품 「기술머신」의 기술에 의한 추가행동을 얻을 수 있다.
 //  상대 종족치 복사      상대의 가장 높은 종족치를 자신의 같은 능력에 복사한다.
 //  소지품 효과 공유      그 소지품의 효과를 자신도 받는다.
 public static class AbilityRemaining4Modules {
  static readonly Regex FieldHeal = new Regex(@"^필드에 있는 「(?<t>[^」]+)」 타입 전원의 체력을 최대 HP의 1/(?<n>\d+) 회복한다$");
  static readonly Regex FieldHurt = new Regex(@"^필드에 있는 「(?<t>[^」]+)」 타입 이외의 전원에게 최대 HP의 1/(?<n>\d+) 대미지를 준다$");
  public const string RiseAgain = "자신의 능력을 올라간 만큼 올린다";
  const string IntimidateImmunity = "상대의 〔위협〕에 의한 능력 하락을 무효화한다";
  static readonly Regex MachineExtra = new Regex(@"^가장 마지막에 소지품 「기술머신」의 기술에 의한 추가행동을 얻을 수 있다$");
  const string CopyHighest = "상대의 가장 높은 종족치를 자신의 같은 능력에 복사한다";
  const string ShareItem = "그 소지품의 효과를 자신도 받는다";
  static readonly Regex Proportional = new Regex(@"^자신의 「(?<st>[^」]+)」[을를] 빈사 상태인 아군의 수에 비례해 강화\(1\.2배~1\.6배\)한다$");

  static string Clean(string text) {
   return Regex.Replace(PotentialParts.Clean(text ?? ""), @"\s+", " ").Trim().TrimEnd('.', '。', '．').Trim();
  }

  public static bool IsIntimidateImmunity(string sentence) { return Clean(sentence) == IntimidateImmunity; }

  public static string Classify(string sentence) {
   string s = Clean(sentence);
   if (FieldHeal.IsMatch(s) || FieldHurt.IsMatch(s)) return "필드 타입별 체력 변화";
   if (s == RiseAgain) return "올라간 만큼 올림";
   if (s == IntimidateImmunity) return "위협 하락 무효";
   if (MachineExtra.IsMatch(s)) return "기술머신 추가행동";
   if (s == CopyHighest) return "상대 종족치 복사";
   if (s == ShareItem) return "소지품 효과 공유";
   return "";
  }

  // ===== 빈사 아군 수에 비례한 배율(총대장) =====
  // 0명이면 1배, 1명 1.2배 … 5명 이상 1.6배(= 1.1 + 0.1×n, n은 최대 5)
  public static double FallenAllyRate(int fallen) {
   if (fallen <= 0) return 1;
   return Math.Round(1.1 + 0.1 * Math.Min(5, fallen), 2);
  }
  static int FallenAllies(Fighter owner, Rules rules) {
   if (owner == null || rules == null) return 0;
   int side = Mechanics.PartySide(owner, rules);
   if (side < 0 || side > 1) return 0;
   return (rules.Parties[side] ?? new Fighter[0]).Count(x => x != null && x.HP <= 0 && !Mechanics.SameMember(x, owner));
  }
  public static bool TryProportional(string text, out ScalarEffect effect) {
   effect = null;
   var m = Proportional.Match(Clean(text));
   if (!m.Success) return false;
   var stats = m.Groups["st"].Value.Split('/').Select(x => EffectModules.StatIndex(x.Trim())).ToArray();
   if (stats.Length == 0 || stats.Any(i => i < 1 || i > 5)) return false;
   effect = new ScalarEffect { Subject = "자신", Stage = "능력치", Stats = stats, Text = text, Rate = FallenAllyRate(1), Dynamic = (owner, rules) => FallenAllyRate(FallenAllies(owner, rules)) };
   return true;
  }

  static IEnumerable<Fighter> OnField(EffectScope scope) {
   return (scope == null || scope.Fighters == null ? new Fighter[0] : scope.Fighters).Where(x => x != null && x.Data != null && x.OnField && x.HP > 0);
  }
  static Fighter Other(EffectScope scope, Fighter f) {
   return scope.Fighters.FirstOrDefault(x => x != null && !ReferenceEquals(x, f)) ?? f;
  }

  public static void Register() {
   // 필드 타입별 체력 변화(오물의비): 해당 타입 전원은 회복, 그 이외는 같은 양의 대미지
   PotentialExecutors.Register("필드 타입별 체력 변화", c => FieldHeal.IsMatch(Clean(c.Line)) || FieldHurt.IsMatch(Clean(c.Line)), c => {
    string line = Clean(c.Line);
    var heal = FieldHeal.Match(line);
    var hurt = FieldHurt.Match(line);
    var m = heal.Success ? heal : hurt;
    var scope = EffectScope.Current;
    var rules = c.Rules ?? new Rules();
    if (scope == null) return EffectOutcome.Failed;
    string type = m.Groups["t"].Value;
    int denominator = int.Parse(m.Groups["n"].Value);
    bool any = false;
    foreach (var target in OnField(scope).ToList()) {
     if (target.HP <= 0 || Mechanics.Has(target, type) != heal.Success) continue;
     var other = Other(scope, target);
     int amount = FractionalHpRule.Amount(Mechanics.MaxHP(target, other, rules), denominator);
     int before = target.HP;
     if (heal.Success) Mechanics.Heal(target, other, rules, amount, c.Source.Label, c.Log);
     else Mechanics.Hurt(target, other, rules, amount, c.Source.Label, c.Log);
     any |= target.HP != before;
    }
    return any ? EffectOutcome.Success : EffectOutcome.Failed;
   });

   // 올라간 만큼 올림(욕심쟁이): 이 효과로 오른 것은 같은 연쇄에서 다시 발동하지 않는다(포텐셜 큐의 연쇄 규칙)
   PotentialExecutors.Register("올라간 만큼 올림", c => Clean(c.Line) == RiseAgain, c => {
    var self = c.Owner; var fact = c.Event.Fact;
    if (self == null || self.HP <= 0 || fact == null || fact.Kind != "랭크" || fact.Delta <= 0 || fact.Stat < 1 || fact.Stat > 5) return EffectOutcome.Failed;
    return Mechanics.ChangeStage(self, c.Opponent, fact.Stat, fact.Delta, c.Rules ?? new Rules(), c.Source.Label, c.Log ?? new List<string>()) ? EffectOutcome.Success : EffectOutcome.Failed;
   });

   // 위협 하락 무효는 능력 변화 판정(Mechanics.ChangeStage)이 문장을 읽는다. 실행할 것은 없다
   PotentialExecutors.Register("위협 하락 무효", c => Clean(c.Line) == IntimidateImmunity, c => EffectOutcome.Success);

   // 기술머신 추가행동(멀티태스크): 통상의 행동 뒤에 소지품 기술머신의 기술을 기능확장으로 낸다(추가행동 중에 얻은 것은 버려진다)
   PotentialExecutors.Register("기술머신 추가행동", c => MachineExtra.IsMatch(Clean(c.Line)), c => {
    var scope = EffectScope.Current;
    var owner = c.Owner;
    if (scope == null || scope.Db == null || owner == null || owner.HP <= 0) return EffectOutcome.Failed;
    var machine = Regex.Match(ItemRuntime.Name(owner), @"^기술머신\((?<m>[^)]+)\)$");
    var move = machine.Success ? scope.Db.moves.FirstOrDefault(x => x != null && x.name == machine.Groups["m"].Value.Trim()) : null;
    if (move == null) { c.Say(c.Source.Label + ": 쓸 수 있는 기술머신이 없다"); return EffectOutcome.Failed; }
    int side = Array.IndexOf(scope.Fighters, owner);
    if (side < 0) return EffectOutcome.Failed;
    scope.ExtraActions.Add(new ExtraActionGrant { Side = side, Owner = owner, First = false, FixedMove = move.Copy(), Source = c.Source.Label });
    owner.Conditions.PendingAction = true;
    c.Say(owner.Data.name + ": 기술머신 「" + move.name + "」로 추가행동을 얻었다 (턴 마지막)");
    return EffectOutcome.Success;
   });

   // 상대 종족치 복사(트랜스): 상대의 가장 높은 종족치를 자신의 같은 능력에 복사한다. 퇴장하면 복원된다(OriginalBase)
   PotentialExecutors.Register("상대 종족치 복사", c => Clean(c.Line) == CopyHighest, c => {
    var self = c.Owner; var other = c.Opponent;
    if (self == null || other == null || self.HP <= 0 || other.HP <= 0 || other.Data.@base == null || other.Data.@base.Length < 6) return EffectOutcome.Failed;
    int index = EffectModules.StatIndex(PotentialStatRules.ChooseExtremeBase(other.Data, true, c.Rng ?? (() => .5)));
    int value = other.Data.@base[index];
    if (self.Data.@base[index] == value) return EffectOutcome.Failed;
    if (self.Conditions.OriginalBase == null) self.Conditions.OriginalBase = (int[])self.Data.@base.Clone();
    int before = self.Data.@base[index];
    self.Data.@base[index] = value;
    c.Say(self.Data.name + ": " + Engine.Names[index] + " 종족치 " + before + "→" + value + " (" + c.Source.Label + ", " + other.Data.name + "의 가장 높은 종족치)");
    return EffectOutcome.Success;
   });

   // 소지품 효과 공유(화이부실): 상대가 소모한 소지품의 효과를 자신도 받는다. 자신의 소지품은 그대로 둔다
   PotentialExecutors.Register("소지품 효과 공유", c => Clean(c.Line) == ShareItem, c => {
    var self = c.Owner; var fact = c.Event.Fact;
    if (self == null || self.HP <= 0 || fact == null || fact.Kind != "소지품 소실" || string.IsNullOrEmpty(fact.Item) || !PotentialQueue.Active) return EffectOutcome.Failed;
    if (ItemCatalog.Find(fact.Item) == null) return EffectOutcome.Failed;
    string own = self.Data.item, lostItem = self.LostItem, lostBerry = self.LostBerry;
    self.Data.item = fact.Item;
    ItemRuntime.Sync(self);
    if (!AbilityItemModules.CanUseItem(self)) { self.Data.item = own; ItemRuntime.Sync(self); return EffectOutcome.Failed; }
    c.Say(self.Data.name + ": 상대가 쓴 「" + fact.Item + "」의 효과를 자신도 받는다 (" + c.Source.Label + ")");
    PotentialQueue.Raise(new PotentialEvent { Owner = self, Other = c.Opponent, Spec = clause => true, Label = "소지품 효과 공유", Message = "", Log = c.Say, SlotOnly = ItemRuntime.Slot });
    // 빌린 소지품은 쓰든 안 쓰든 돌려놓는다(소모 기록으로 되돌려받기 효과가 생기지 않게 한다)
    self.Data.item = own;
    self.LostItem = lostItem;
    self.LostBerry = lostBerry;
    ItemRuntime.Sync(self);
    return EffectOutcome.Success;
   });
  }
 }
}
