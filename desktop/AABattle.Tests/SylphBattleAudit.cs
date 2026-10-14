using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using NUnit.Framework;

namespace AABattle {
 // 감사용 하니스(Explicit): 실프 인헨스먼츠 샘플 파티끼리 여러 판 돌려 턴마다 로그를 파일로 남긴다.
 // 창을 하나도 띄우지 않는다: MainForm을 쓰지 않고 StartForm.Begin(LoadTeams)·MainForm.Battle()·ReplaceFainted와 같은 순서를
 // 엔진 API로 재현하며, 모든 선택은 Rules 콜백(OptionalEffectPrompt·ChooseStat·ChooseExtraMove·교대 대상 등)을 코드로 답한다.
 // 환경변수: SYLPH_AUDIT_OUT(출력 폴더) SYLPH_AUDIT_ONLY(시나리오 ID 접두어, 쉼표 구분) SYLPH_AUDIT_SEEDS SYLPH_AUDIT_TURNS
 [TestFixture, Explicit("감사용 하니스: 배틀을 수백 판 돌려 로그를 남긴다")]
 public class SylphBattleAudit {
  static string SamplePath() {
   string dir = AppDomain.CurrentDomain.BaseDirectory;
   while (dir != null) {
    string path = Path.Combine(dir, "samples", "abaddon-sylph.aabdata.json");
    if (File.Exists(path)) return path;
    dir = Path.GetDirectoryName(dir);
   }
   throw new FileNotFoundException("samples/abaddon-sylph.aabdata.json");
  }

  class Scenario {
   public int LeadA, LeadB, Seed, Variant;
   public bool Yes;
   public string Id { get { return "L" + LeadA + "v" + LeadB + "_s" + Seed + "_v" + Variant + (Yes ? "_yes" : "_no"); } }
  }

  // 한 판의 상태(MainForm의 필드에 해당)
  class Sim {
   public Database Db;
   public Fighter[] Fighters;
   public Rules Rules = new Rules();
   public TrainerRecord[] Trainers = new TrainerRecord[2];
   public TrainerBattleState[] States = { new TrainerBattleState(), new TrainerBattleState() };
   public List<Fighter>[] Reserves = { new List<Fighter>(), new List<Fighter>() };
   public int[] InitialSizes = new int[2];
   public Random Random;
   public List<string> Log = new List<string>();
   public List<string> Prompts = new List<string>();
   public bool Yes;
   public int Turn = 1;
  }

  // MainForm.ConfirmOptionalEffect와 같은 판단(창 없이 답만 한다)
  static bool Confirm(Sim sim, Fighter source, Fighter target, Potential potential) {
   if (source == null || target == null || potential == null) return false;
   if (BatonModules.Classify(potential.description) == "양방향 임의교대 인계") {
    sim.Prompts.Add("[선택 발동] " + potential.name + "으로 능력변화를 인계할까요? → " + (sim.Yes ? "예" : "아니오"));
    return sim.Yes;
   }
   int side = Array.FindIndex(sim.Fighters, x => x != null && x.Data != null && Mechanics.SameMember(x, source));
   if (side < 0 || BattleRecords.IsAnalyzed(sim.States[side], target)) {
    sim.Prompts.Add("[선택 발동 자동 거절] 『" + potential.name + "』 (해석 질문 조건: " + (side < 0 ? "필드 밖" : "이미 해석한 상대") + ")");
    return false;
   }
   sim.Prompts.Add("[선택 발동] 『" + potential.name + "』으로 " + target.Data.name + "의 데이터를 해석할까요? → " + (sim.Yes ? "예" : "아니오"));
   if (!sim.Yes) return false;
   if (!PotentialQueue.Active) sim.States[side].Analyzed.Add(BattleRecords.Key(target));
   return true;
  }

  static void Hook(Sim sim) {
   var rules = sim.Rules;
   rules.OptionalEffectPrompt = (s, t, p) => Confirm(sim, s, t, p);
   rules.ChooseStat = (s, f, list) => { string pick = sim.Yes ? list.First() : list.Last(); sim.Prompts.Add("[임의의 능력] " + s.Label + " → " + pick); return pick; };
   rules.ChooseSpeciesValue = (s, f, stat, min, max) => { sim.Prompts.Add("[종족치] " + s.Label + " " + min + "~" + max); return sim.Yes ? max : min; };
   rules.ChooseExtraMove = (f, list) => { var pick = sim.Yes ? list.First() : list.Last(); sim.Prompts.Add("[추가행동 기술] " + f.Data.name + " " + string.Join("/", list.Select(x => x.name)) + " → " + pick.name); return pick; };
   rules.ChooseSealMove = (f, list) => { string pick = sim.Yes ? list.First() : list.Last(); sim.Prompts.Add("[봉인] " + f.Data.name + " → " + pick); return pick; };
   rules.ChooseBondPotential = (f, list) => { sim.Prompts.Add("[유대] " + f.Data.name + " " + string.Join("/", list)); return sim.Yes ? 0 : -1; };
  }

  // StartForm.Begin → MainForm.LoadTeams
  static void LoadTeams(Sim sim, TeamRecord[] teams, List<Fighter>[] all, int[] leads) {
   Action<string> log = x => sim.Log.Add(x);
   for (int i = 0; i < 2; i++) {
    sim.Trainers[i] = teams[i].trainer;
    sim.States[i].Trainer = sim.Trainers[i];
    sim.States[i].RefreshUnlocks();
    foreach (var member in all[i]) {
     sim.States[i].RefreshUnlocks(member.Data.potentials);
     member.Data.potentials = (member.Data.potentials ?? new Potential[0]).Where(x => TrainerAccessRules.CanUseRuntimePotentialSlot(x.id, sim.Trainers[i], sim.States[i], member.Data.potentials)).ToArray();
     TrainerAccessRules.ApplyTrainerPassives(member, sim.Trainers[i], sim.States[i]);
    }
    sim.InitialSizes[i] = all[i].Count;
    sim.States[i].InitialSize = all[i].Count;
    sim.Fighters[i] = all[i][leads[i]];
    sim.Fighters[i].OnField = true;
    sim.Reserves[i] = all[i].Where((x, n) => n != leads[i]).ToList();
    foreach (var reserve in sim.Reserves[i]) reserve.OnField = false;
   }
   sim.Rules.FieldEffects = FieldEffectRuntime.FromTeams(teams);
   RuntimeTriggers.BattleStart(sim.Trainers, new IEnumerable<Fighter>[] { all[0], all[1] }, log);
   FieldEffectRuntime.Fire(sim.Rules, TriggerStructures.BattleStartLabel, sim.Fighters, "배틀 개시", sim.Random.NextDouble, log);
   Mechanics.SetParties(sim.Rules, sim.Fighters);
   var scope = EffectScope.Begin(sim.Fighters, sim.Rules, sim.Random.NextDouble, sim.States, sim.Db);
   for (int i = 0; i < 2; i++) {
    EntryTriggers.Enter(sim.Fighters[i], sim.Fighters[1 - i], sim.Rules, sim.Random.NextDouble, EntryReason.Lead, log);
    sim.Fighters[i].Conditions.EntryTriggered = true;
    FieldEffectRuntime.Fire(sim.Rules, TriggerStructures.EntryLabel, sim.Fighters, sim.Fighters[i].Data.name + "이 장소에 나옴", sim.Random.NextDouble, log);
    FieldEffectRuntime.Fire(sim.Rules, TriggerStructures.OpponentEntryLabel, sim.Fighters, sim.Fighters[i].Data.name + "이 장소에 나옴", sim.Random.NextDouble, log);
    TrainerTriggers.Fire(sim.Trainers[i], TriggerStructures.EntryLabel, sim.Fighters[i].Data.name + "이 아군 장소에 나옴", log, sim.Fighters[i]);
    TrainerTriggers.Fire(sim.Trainers[1 - i], TriggerStructures.OpponentEntryLabel, sim.Fighters[i].Data.name + "이 상대 장소에 나옴", log, sim.Fighters[1 - i]);
   }
   RuntimeTriggers.OpponentEntered(sim.Fighters[0], sim.Fighters[1], sim.Rules, log);
   RuntimeTriggers.OpponentEntered(sim.Fighters[1], sim.Fighters[0], sim.Rules, log);
   scope.Dispose();
  }

  // MainForm.RefreshView 중 상태에 영향을 주는 부분
  static void Refresh(Sim sim) {
   var encoreLog = new List<string>();
   foreach (var fighter in sim.Fighters) {
    Mechanics.RefreshRollout(sim.Db, fighter, encoreLog);
    Mechanics.RefreshEncore(sim.Db, fighter, encoreLog);
   }
   sim.Log.AddRange(encoreLog);
   Mechanics.SetParties(sim.Rules, sim.Fighters, new IEnumerable<Fighter>[] { sim.Reserves[0], sim.Reserves[1] });
  }

  static void UpdateHere(Sim sim) {
   for (int side = 0; side < 2; side++) {
    int living = (sim.Fighters[side].HP > 0 ? 1 : 0) + sim.Reserves[side].Count(x => x.HP > 0);
    BattleRecords.UpdateHere(sim.States[side], sim.InitialSizes[side], living, sim.Fighters[1 - side], side, x => sim.Log.Add(x));
   }
  }

  // MainForm.Battle()
  static void BattleTurn(Sim sim, TurnSelection[] selections, StringBuilder sb) {
   var commands = new BattleCommand[2];
   var rangeLines = new List<string>();
   for (int i = 0; i < 2; i++) {
    var pick = selections[i];
    var party = sim.Reserves[i].Concat(new[] { sim.Fighters[i] }).ToArray();
    if (Mechanics.AutoAction(sim.Fighters[i])) {
     commands[i] = new BattleCommand { Move = Mechanics.ActionMove(sim.Db, sim.Fighters[i], null), Party = party };
     rangeLines.Add(sim.Fighters[i].Data.name + " · " + Mechanics.AutoActionLabel(sim.Fighters[i]) + " 자동 행동");
     continue;
    }
    if (pick.SwitchIndex >= 0) {
     commands[i] = new BattleCommand { SwitchIn = sim.Reserves[i][pick.SwitchIndex], Form = pick.Form, Order = pick.Order, PreInputs = (pick.PreInputs ?? new List<string>()).ToArray(), Party = party };
     rangeLines.Add(sim.Fighters[i].Data.name + " · 교대 → " + commands[i].SwitchIn.Data.name);
    } else {
     var m = sim.Db.moves.First(x => x.name == sim.Fighters[i].Moves[Math.Min(sim.Fighters[i].Selected, sim.Fighters[i].Moves.Length - 1)]);
     commands[i] = new BattleCommand { Move = m, Form = pick.Form, Order = pick.Order, PreInputs = (pick.PreInputs ?? new List<string>()).ToArray(), Party = party };
     var d = Engine.Calculate(sim.Db, sim.Fighters[i], sim.Fighters[1 - i], m, sim.Rules, i);
     rangeLines.Add(sim.Fighters[i].Data.name + " · " + m.name + "   " + (d.Supported ? d.Min + " – " + d.Max + "  (자속 ×" + d.Stab + ", 상성 ×" + d.Type + ")" : "상태 기술/수동 판정"));
    }
   }
   var result = Engine.Resolve(sim.Db, sim.Fighters, commands, sim.Rules, sim.States, sim.Random.NextDouble, request => {
    var pickIn = request.Candidates.FirstOrDefault();
    sim.Prompts.Add("[교대 대상 선택] " + request.Source + " " + (request.Timing == SwitchTiming.TurnEnd ? "턴 마지막" : "턴 중간") + " 후보 " + string.Join(",", request.Candidates.Select(x => x.Data.name)) + " → " + (pickIn == null ? "없음" : pickIn.Data.name));
    return pickIn;
   });
   foreach (var change in result.Switches ?? new List<SwitchRecord>()) {
    if (change == null || change.Incoming == null || change.Outgoing == null) continue;
    int incomingIndex = sim.Reserves[change.Side].FindIndex(x => Mechanics.SameMember(x, change.Incoming));
    if (incomingIndex >= 0) sim.Reserves[change.Side].RemoveAt(incomingIndex);
    var outgoing = change.Outgoing.Copy();
    Mechanics.LeaveField(outgoing);
    if (!sim.Reserves[change.Side].Any(x => Mechanics.SameMember(x, outgoing))) sim.Reserves[change.Side].Add(outgoing);
   }
   for (int i = 0; i < 2; i++)
    for (int k = 0; k < sim.Reserves[i].Count; k++) {
     var state = result.Rules.Parties[i].FirstOrDefault(x => Mechanics.SameMember(x, sim.Reserves[i][k]));
     if (state != null && !state.OnField) sim.Reserves[i][k] = state.Copy();
    }
   sb.AppendLine("TURN " + sim.Turn);
   foreach (string line in rangeLines) sb.AppendLine(line);
   sb.AppendLine();
   foreach (string line in result.Log) sb.AppendLine(line);
   sim.Fighters = result.Fighters;
   sim.Rules = result.Rules;
   sim.States = result.Trainers;
   sim.Turn++;
   UpdateHere(sim);
   Refresh(sim);
  }

  // MainForm.ReplaceFainted (선택은 pick번째 후보)
  static bool ReplaceFainted(Sim sim, int pick, StringBuilder sb) {
   for (int side = 0; side < 2; side++) {
    if (sim.Fighters[side].HP > 0) continue;
    var available = sim.Reserves[side].Where(x => x.HP > 0).ToList();
    if (available.Count == 0) return false;
    Action<string> log = x => sim.Log.Add(x);
    sim.Prompts.Add("[" + (side + 1) + "팀 · 죽어내밀기] " + string.Join(",", available.Select(x => x.Data.name)) + " → " + available[pick % available.Count].Data.name);
    RuntimeTriggers.Leave(sim.Fighters[side], SwitchReason.Fainted, log);
    var outgoing = sim.Fighters[side];
    var leaveLog = new List<string>();
    Mechanics.LeaveField(outgoing, leaveLog);
    sim.Log.AddRange(leaveLog);
    var incoming = available[pick % available.Count];
    incoming.OnField = true;
    sim.Reserves[side].Remove(incoming);
    sim.Reserves[side].Add(outgoing);
    sim.Fighters[side] = incoming;
    var hazardLog = new List<string>();
    Mechanics.EnterField(sim.Db, incoming, sim.Fighters[1 - side], side, sim.Rules, sim.Random.NextDouble, hazardLog);
    sim.Log.AddRange(hazardLog);
    Mechanics.SetParties(sim.Rules, sim.Fighters);
    using (EffectScope.Begin(sim.Fighters, sim.Rules, sim.Random.NextDouble, sim.States, sim.Db)) {
     EntryTriggers.Enter(incoming, sim.Fighters[1 - side], sim.Rules, sim.Random.NextDouble, EntryReason.Replacement, log, null, true);
     incoming.Conditions.EntryTriggered = true;
     RuntimeTriggers.OpponentEntered(sim.Fighters[1 - side], incoming, sim.Rules, log);
    }
    FieldEffectRuntime.Fire(sim.Rules, TriggerStructures.EntryLabel, sim.Fighters, incoming.Data.name + "이 장소에 나옴", sim.Random.NextDouble, log);
    FieldEffectRuntime.Fire(sim.Rules, TriggerStructures.OpponentEntryLabel, sim.Fighters, incoming.Data.name + "이 장소에 나옴", sim.Random.NextDouble, log);
    TrainerTriggers.Fire(sim.Trainers[side], TriggerStructures.EntryLabel, incoming.Data.name + "이 장소에 나옴", log, incoming);
    TrainerTriggers.Fire(sim.Trainers[1 - side], TriggerStructures.OpponentEntryLabel, incoming.Data.name + "이 상대 장소에 나옴", log, sim.Fighters[1 - side]);
   }
   Refresh(sim);
   return true;
  }

  [Test]
  public void RunAudit() {
   string outDir = Environment.GetEnvironmentVariable("SYLPH_AUDIT_OUT");
   if (string.IsNullOrEmpty(outDir)) outDir = Path.Combine(Path.GetTempPath(), "sylph-audit");
   Directory.CreateDirectory(outDir);
   string only = Environment.GetEnvironmentVariable("SYLPH_AUDIT_ONLY");
   int seeds = int.Parse(Environment.GetEnvironmentVariable("SYLPH_AUDIT_SEEDS") ?? "2");
   int maxTurns = int.Parse(Environment.GetEnvironmentVariable("SYLPH_AUDIT_TURNS") ?? "40");
   var project = ProjectStore.Parse(File.ReadAllText(SamplePath(), Encoding.UTF8));
   var team = project.teams.Single();
   var db = TestData.Db;
   DumpCatalog(Path.Combine(outDir, "_catalog.txt"), team, db);
   var summary = new StringBuilder();
   int count = 0;
   var watch = System.Diagnostics.Stopwatch.StartNew();
   for (int a = 0; a < 6; a++)
    for (int b = 0; b < 6; b++)
     for (int seed = 1; seed <= seeds; seed++)
      for (int variant = 0; variant < 4; variant++)
       foreach (bool yes in new[] { true, false }) {
        var sc = new Scenario { LeadA = a, LeadB = b, Seed = seed, Variant = variant, Yes = yes };
        if (!string.IsNullOrEmpty(only) && !only.Split(',').Any(x => sc.Id.StartsWith(x))) continue;
        count++;
        string result;
        try {
         result = RunOne(sc, team, db, Path.Combine(outDir, sc.Id + ".log"), maxTurns);
        } catch (Exception ex) {
         result = "HARNESS-EXCEPTION " + ex.GetType().Name + ": " + ex.Message;
        }
        summary.AppendLine(sc.Id + "\t" + result);
       }
   File.WriteAllText(Path.Combine(outDir, "_summary" + (string.IsNullOrEmpty(only) ? "" : "_" + Math.Abs(only.GetHashCode())) + ".txt"), summary.ToString() + "elapsed " + watch.Elapsed + "\r\n", Encoding.UTF8);
   Assert.Pass(count + " battles");
  }


  // 고정 난수(0이면 모든 확률 성공·최소 대미지, 0.99면 확률 실패) — 스크립트 시나리오용
  class ConstantRandom : Random {
   readonly double value;
   public ConstantRandom(double v) { value = v; }
   public override double NextDouble() { return value; }
  }

  // 스크립트 시나리오: 환경변수 SYLPH_SCRIPT 파일의 명령을 읽어 특정 상황을 재현한다.
  //   scenario <이름> / lead <A> <B> / rng <0~1|seed:N> / answer yes|no / hp <side> <값> / rank <side> <능력색인> <값> / faint <side>
  //   t <1팀 행동> | <2팀 행동>   행동 = 기술이름 또는 switch=<대기번호>, 뒤에 ;form=..;order=..;pre=a,b
  [Test]
  public void RunScripts() {
   string scriptPath = Environment.GetEnvironmentVariable("SYLPH_SCRIPT");
   if (string.IsNullOrEmpty(scriptPath)) Assert.Ignore("SYLPH_SCRIPT 없음");
   string outDir = Environment.GetEnvironmentVariable("SYLPH_AUDIT_OUT");
   if (string.IsNullOrEmpty(outDir)) outDir = Path.Combine(Path.GetTempPath(), "sylph-audit");
   Directory.CreateDirectory(outDir);
   var project = ProjectStore.Parse(File.ReadAllText(SamplePath(), Encoding.UTF8));
   var team = project.teams.Single();
   var db = TestData.Db;
   var teams = new[] { team, team };
   Sim sim = null;
   StringBuilder sb = null;
   string name = null;
   int[] leads = { 0, 0 };
   double rngValue = -1;
   int seed = 1;
   bool yes = true;
   Action finish = () => {
    if (sb != null) File.WriteAllText(Path.Combine(outDir, "script_" + name + ".log"), sb.ToString(), Encoding.UTF8);
   };
   Action start = () => {
    var all = teams.Select(t => t.members.Select(m => {
     string warning;
     var f = new Fighter(EditorBridge.ToPokemon(m, db, out warning, t.trainer));
     f.AA = m.asciiArt;
     return f;
    }).ToList()).ToArray();
    sim = new Sim { Db = db, Yes = yes, Fighters = new Fighter[2], Random = rngValue >= 0 ? new ConstantRandom(rngValue) : new Random(seed) };
    Hook(sim);
    LoadTeams(sim, teams, all, leads);
    Refresh(sim);
    Flush(sim, sb, "시작");
    sb.AppendLine(State(sim));
   };
   foreach (string raw in File.ReadAllLines(scriptPath, Encoding.UTF8)) {
    string line = raw.Trim();
    if (line.Length == 0 || line.StartsWith("//")) continue;
    var parts = line.Split(new[] { ' ' }, 2);
    string cmd = parts[0], rest = parts.Length > 1 ? parts[1] : "";
    try {
     if (cmd == "scenario") {
      finish();
      name = rest;
      sb = new StringBuilder("# scenario " + rest + Environment.NewLine);
      sim = null; leads = new[] { 0, 0 }; rngValue = -1; seed = 1; yes = true;
     } else if (cmd == "lead") {
      var v = rest.Split(' ').Select(int.Parse).ToArray();
      leads = new[] { v[0], v[1] };
     } else if (cmd == "rng") {
      if (rest.StartsWith("seed:")) { rngValue = -1; seed = int.Parse(rest.Substring(5)); } else rngValue = double.Parse(rest, System.Globalization.CultureInfo.InvariantCulture);
     } else if (cmd == "answer") {
      yes = rest == "yes";
     } else {
      if (sim == null) { sb.AppendLine("# lead " + leads[0] + " " + leads[1] + " rng " + rngValue + " answer " + (yes ? "yes" : "no")); start(); }
      if (cmd == "hp") {
       var v = rest.Split(' ');
       sim.Fighters[int.Parse(v[0])].HP = int.Parse(v[1]);
       sb.AppendLine("(설정) " + (int.Parse(v[0]) + 1) + "팀 HP=" + v[1]);
      } else if (cmd == "rank") {
       var v = rest.Split(' ').Select(int.Parse).ToArray();
       sim.Fighters[v[0]].Stages[v[1]] = v[2];
       sb.AppendLine("(설정) " + (v[0] + 1) + "팀 랭크[" + v[1] + "]=" + v[2]);
      } else if (cmd == "strip") {
       // strip <side> <포텐셜 이름>: 그 포텐셜을 제거한다(효과 유무 비교용)
       var v = rest.Split(new[] { ' ' }, 2);
       var target = sim.Fighters[int.Parse(v[0])];
       target.Data.potentials = target.Data.potentials.Where(x => x.name != v[1]).ToArray();
       Mechanics.SetParties(sim.Rules, sim.Fighters, new IEnumerable<Fighter>[] { sim.Reserves[0], sim.Reserves[1] });
       sb.AppendLine("(설정) " + (int.Parse(v[0]) + 1) + "팀 『" + v[1] + "』 제거");
      } else if (cmd == "calc") {
       // calc <side> <기술>: 대미지 범위 계산 결과
       var v = rest.Split(new[] { ' ' }, 2);
       int side = int.Parse(v[0]);
       var m = sim.Db.moves.First(x => x.name == v[1]);
       var d = Engine.Calculate(sim.Db, sim.Fighters[side], sim.Fighters[1 - side], m, sim.Rules, side);
       sb.AppendLine("(계산) " + (side + 1) + "팀 " + sim.Fighters[side].Data.name + " " + v[1] + " → " + d.Min + "–" + d.Max + " 자속×" + d.Stab + " 상성×" + d.Type + (d.Supported ? "" : " (수동)"));
      } else if (cmd == "report") {
       var rep = BattleStateReport.Build(sim.Db, sim.Fighters, sim.Rules, new[] { new TurnSelection(), new TurnSelection() }, sim.Trainers, sim.States, sim.Reserves);
       sb.AppendLine("(상태 보고) " + rep);
      } else if (cmd == "pots") {
       var target = sim.Fighters[int.Parse(rest)];
       sb.AppendLine("(포텐셜) " + target.Data.name + ": " + string.Join(", ", (target.Data.potentials ?? new Potential[0]).Select(x => x.name)));
      } else if (cmd == "faint") {
       sim.Fighters[int.Parse(rest)].HP = 0;
       bool ok = ReplaceFainted(sim, 0, sb);
       sb.AppendLine("--- 죽어내밀기 ---");
       Flush(sim, sb, "죽어내밀기");
       sb.AppendLine(State(sim));
      } else if (cmd == "t") {
       var acts = rest.Split('|');
       var selections = new TurnSelection[2] { new TurnSelection(), new TurnSelection() };
       for (int side = 0; side < 2; side++) {
        var tokens = acts[side].Trim().Split(';');
        string first = tokens[0].Trim();
        if (first.StartsWith("switch=")) selections[side].SwitchIndex = int.Parse(first.Substring(7));
        else {
         int idx = Array.IndexOf(sim.Fighters[side].Moves, first);
         if (idx < 0) throw new Exception("기술 없음: " + first + " (" + sim.Fighters[side].Data.name + ")");
         sim.Fighters[side].Selected = idx;
        }
        foreach (string token in tokens.Skip(1)) {
         var kv = token.Split(new[] { '=' }, 2);
         if (kv[0] == "form") selections[side].Form = kv[1];
         else if (kv[0] == "order") selections[side].Order = kv[1];
         else if (kv[0] == "pre") selections[side].PreInputs = kv[1].Split(',').ToList();
        }
        sb.AppendLine("[선택] " + (side + 1) + "팀 " + sim.Fighters[side].Data.name + " → " + acts[side].Trim());
       }
       if (sim.Fighters.Any(f => f.HP <= 0)) { ReplaceFainted(sim, 0, sb); Flush(sim, sb, "죽어내밀기"); }
       BattleTurn(sim, selections, sb);
       Flush(sim, sb, "턴");
       sb.AppendLine(State(sim));
      } else throw new Exception("알 수 없는 명령 " + cmd);
     }
    } catch (Exception ex) {
     if (sb != null) sb.AppendLine("!! EXCEPTION at '" + line + "': " + ex);
    }
   }
   finish();
  }

  static void DumpCatalog(string path, TeamRecord team, Database db) {
   var sb = new StringBuilder();
   foreach (var m in team.members) {
    string warning;
    var p = EditorBridge.ToPokemon(m, db, out warning, team.trainer);
    var fighter = new Fighter(p);
    sb.AppendLine("===== " + p.name + " ability=" + p.ability + " item=" + p.item + " moves=" + string.Join(",", p.moves) + " warn=" + warning);
    var ability = AbilityCatalog.Find(p.ability);
    sb.AppendLine("  (특성 원문) " + (ability == null ? "도감에 없음" : ability.effect + " | 상태=" + AbilityModules.Status(ability)));
    foreach (var pot in fighter.Data.potentials ?? new Potential[0])
     sb.AppendLine("  [" + pot.id + "] " + pot.name + " | act=" + pot.activation + " uses=" + pot.uses + " | triggers=" + string.Join(" // ", pot.triggers ?? new string[0]) + " | effect=" + (pot.description ?? "").Replace("\r\n", " // ").Replace("\n", " // "));
    foreach (string mv in p.moves) {
     var move = db.moves.FirstOrDefault(x => x.name == mv);
     sb.AppendLine("  (기술) " + mv + (move == null ? " <<DB에 없음>>" : " | " + string.Join("/", move.types) + " " + move.category + " pow=" + move.power + " acc=" + move.accuracy + " pri=" + move.priority + " | " + (move.effect ?? "").Replace("\r\n", " // ")));
    }
   }
   File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
  }

  static string RunOne(Scenario sc, TeamRecord team, Database db, string logPath, int maxTurns) {
   var teams = new[] { team, team };
   var all = teams.Select(t => t.members.Select(m => {
    string warning;
    var f = new Fighter(EditorBridge.ToPokemon(m, db, out warning, t.trainer));
    f.AA = m.asciiArt;
    return f;
   }).ToList()).ToArray();
   var sim = new Sim { Db = db, Yes = sc.Yes, Random = new Random(sc.Seed * 7919 + sc.LeadA * 31 + sc.LeadB), Fighters = new Fighter[2] };
   Hook(sim);
   var sb = new StringBuilder();
   sb.AppendLine("# " + sc.Id + " 선발: 1팀 " + all[0][sc.LeadA].Data.name + " / 2팀 " + all[1][sc.LeadB].Data.name);
   string result = "ongoing";
   try {
    LoadTeams(sim, teams, all, new[] { sc.LeadA, sc.LeadB });
   } catch (Exception ex) {
    sb.AppendLine("!! EXCEPTION in LoadTeams: " + ex);
    File.WriteAllText(logPath, sb.ToString(), Encoding.UTF8);
    return "EXCEPTION(LoadTeams) " + ex.GetType().Name + ": " + ex.Message;
   }
   Refresh(sim);
   Flush(sim, sb, "LoadTeams");
   sb.AppendLine(State(sim));
   for (int turn = 1; turn <= maxTurns; turn++) {
    if (sim.Fighters.Any(f => f.HP <= 0)) {
     bool replaced;
     try {
      replaced = ReplaceFainted(sim, sc.Seed + turn, sb);
     } catch (Exception ex) {
      sb.AppendLine("!! EXCEPTION in ReplaceFainted: " + ex);
      result = "EXCEPTION(Replace) " + ex.GetType().Name + ": " + ex.Message;
      break;
     }
     sb.AppendLine("--- 죽어내밀기 ---");
     Flush(sim, sb, "죽어내밀기");
     if (!replaced || sim.Fighters.Any(f => f.HP <= 0)) {
      result = Winner(sim);
      break;
     }
     sb.AppendLine(State(sim));
    }
    var selections = Decide(sim, sc, turn, sb);
    var watch = System.Diagnostics.Stopwatch.StartNew();
    try {
     BattleTurn(sim, selections, sb);
    } catch (Exception ex) {
     sb.AppendLine("!! EXCEPTION in Battle() turn " + turn + ": " + ex);
     result = "EXCEPTION(turn " + turn + ") " + ex.GetType().Name + ": " + ex.Message;
     break;
    }
    sb.AppendLine("(" + watch.ElapsedMilliseconds + "ms)");
    Flush(sim, sb, "턴 " + turn);
    sb.AppendLine(State(sim));
    if (turn == maxTurns) result = "TIMEOUT(" + maxTurns + "턴) " + State(sim).Replace("\r\n", " | ");
   }
   if (result == "ongoing") result = Winner(sim);
   sb.AppendLine("# RESULT " + result);
   File.WriteAllText(logPath, sb.ToString(), Encoding.UTF8);
   return result;
  }

  static void Flush(Sim sim, StringBuilder sb, string where) {
   if (sim.Prompts.Count > 0) {
    sb.AppendLine("--- 프롬프트 (" + where + ") ---");
    foreach (string p in sim.Prompts) sb.AppendLine(p);
    sim.Prompts.Clear();
   }
   if (sim.Log.Count > 0) {
    foreach (string line in sim.Log) sb.AppendLine(line);
    sim.Log.Clear();
   }
  }

  static string State(Sim sim) {
   var sb = new StringBuilder();
   for (int i = 0; i < 2; i++) {
    var f = sim.Fighters[i];
    sb.Append("  >> " + (i + 1) + "팀 " + f.Data.name + " HP " + f.HP + " " + f.Status + " 랭크[" + string.Join(",", f.Stages) + "] 대기:" + string.Join(",", sim.Reserves[i].Select(x => x.Data.name + "(" + x.HP + ")")) + "\r\n");
   }
   return sb.ToString().TrimEnd();
  }

  static string Winner(Sim sim) {
   bool[] alive = new bool[2];
   for (int i = 0; i < 2; i++) alive[i] = sim.Fighters[i].HP > 0 || sim.Reserves[i].Any(x => x.HP > 0);
   if (alive[0] && alive[1]) return "ongoing";
   return alive[0] ? "1팀 승리" : alive[1] ? "2팀 승리" : "무승부";
  }

  // DirectiveForm과 같은 선택 조건으로 행동·4식·지령·선행입력을 정한다
  static TurnSelection[] Decide(Sim sim, Scenario sc, int turn, StringBuilder sb) {
   var selections = new TurnSelection[2] { new TurnSelection(), new TurnSelection() };
   var rng = new Random(sc.Seed * 104729 + turn * 31 + sc.LeadA * 7 + sc.LeadB);
   for (int side = 0; side < 2; side++) {
    var f = sim.Fighters[side];
    var sel = selections[side];
    var trainer = sim.Trainers[side];
    var state = sim.States[side];
    var enabled = (trainer == null || trainer.potentials == null ? Enumerable.Empty<PotentialRecord>() : trainer.potentials.Where(x => x.enabled && x.slot == "지령" && x.name.Length > 0)).Select(x => x.name).ToArray();
    bool switching = false;
    var alive = sim.Reserves[side].Select((x, n) => new { x, n }).Where(v => v.x.HP > 0).ToList();
    if (alive.Count > 0 && ((sc.Variant == 2 && turn % 3 == 0) || (sc.Variant == 3 && rng.Next(5) == 0))) {
     switching = true;
     sel.SwitchIndex = alive[rng.Next(alive.Count)].n;
    }
    if (sc.Variant >= 1) {
     var forms = enabled.Where(DirectiveRules.IsForm).Where(x => TrainerAccessRules.CanUseForm(x, trainer, state, f.Data.potentials)).Where(x => !state.UsedForms.Contains(x)).Where(x => switching ? x.Contains("돌아와") : !x.Contains("돌아와")).Distinct().ToArray();
     if (forms.Length > 0 && (sc.Variant == 1 || rng.Next(2) == 0)) sel.Form = forms[(turn + side) % forms.Length];
     if (!switching) {
      var orders = enabled.Where(x => !DirectiveRules.IsForm(x)).Where(x => TrainerAccessRules.CanUseOrder(x, trainer, state, f.Data.potentials)).Where(x => !state.UsedOrders.Contains(x)).Distinct().ToArray();
      if (orders.Length > 0 && (sc.Variant == 1 || rng.Next(2) == 0)) sel.Order = orders[(turn + side) % orders.Length];
     }
     foreach (var p in (f.Data.potentials ?? new Potential[0]).Where(x => x != null && x.activation == "선행" && (!(x.uses ?? "").Contains("1/") || state.Usage.CanUse(PotentialKeys.For(f.Data, x), x.uses)))) {
      if (sc.Variant == 1 || rng.Next(2) == 0) {
       if ((p.description ?? "").Contains("할 수 있다") && !sc.Yes) continue;
       sel.PreInputs.Add(p.name);
      }
     }
    }
    int start = (turn + side + sc.Seed + (sc.Variant == 3 ? rng.Next(8) : 0)) % f.Moves.Length;
    int chosen = -1;
    for (int k = 0; k < f.Moves.Length; k++) {
     int n = (start + k) % f.Moves.Length;
     var m = sim.Db.moves.FirstOrDefault(x => x.name == f.Moves[n]);
     if (m == null) continue;
     if (Mechanics.MoveBlock(f, m).Length > 0) continue;
     chosen = n;
     break;
    }
    if (chosen < 0) chosen = 0;
    f.Selected = chosen;
    sb.AppendLine("[선택] " + (side + 1) + "팀 " + f.Data.name + " → " + (sel.SwitchIndex >= 0 ? "교대 " + sim.Reserves[side][sel.SwitchIndex].Data.name : f.Moves[chosen]) + " 4식:" + (sel.Form.Length > 0 ? sel.Form : "-") + " 지령:" + (sel.Order.Length > 0 ? sel.Order : "-") + " 선행:" + (sel.PreInputs.Count > 0 ? string.Join(",", sel.PreInputs) : "-"));
   }
   return selections;
  }
 }
}
