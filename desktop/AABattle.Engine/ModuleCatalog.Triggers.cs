using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace AABattle {
 public static partial class ModuleCatalog {
  // 슬롯과 TriggerSpec의 대응을 함께 보관해 표준 문장의 조사·분류를 엔진에 맡긴다.
  sealed class TriggerBinding {
   public ModuleSlot Slot;
   public Action<TriggerSpec, string> Write;
   public Func<TriggerSpec, string> Read;
  }

  static TriggerBinding TB(ModuleSlot slot, Action<TriggerSpec, string> write, Func<TriggerSpec, string> read) {
   return new TriggerBinding { Slot = slot, Write = write, Read = read };
  }

  static void T(List<ModuleDefinition> modules, string id, string module, string name, string family,
   string template, Func<TriggerSpec> create, params TriggerBinding[] bindings) {
   var definition = new ModuleDefinition("trigger." + id, name,
    TriggerModules.EventModules.Contains(module) ? ModuleKind.TriggerEvent : ModuleKind.TriggerState,
    family, template, bindings.Select(x => x.Slot).ToArray()) { Expected = module };
   definition.Formatter = values => {
    var spec = create();
    foreach (var binding in bindings) binding.Write(spec, values[binding.Slot.Id]);
    string filled = TriggerModules.Canonical(spec);
    if (TriggerModules.Parse(filled).Module != module) throw new ArgumentException("이 빈칸 조합은 트리거 조건으로 읽을 수 없습니다.");
    return filled;
   };
   definition.Reader = text => {
    var spec = TriggerModules.Parse(text);
    if (spec.Module != module) return null;
    var values = bindings.ToDictionary(x => x.Slot.Id, x => x.Read(spec));
    // 슬롯에 없는 선택(공격/기술, 직전/직후 등)은 별개 문장 틀로 구분한다.
    try {
     return definition.Formatter(values) == TriggerModules.Canonical(spec) ? values : null;
    } catch (ArgumentException) { return null; }
   };
   modules.Add(definition);
  }

  static TriggerBinding TS(params string[] choices) {
   return TB(S("주체", ModuleSlotKind.Subject, "자신", choices),
    (s, v) => { s.Subject = v; s.Opponent = v == "상대"; }, s => s.Opponent ? "상대" : s.Subject);
  }

  static TriggerBinding TC(string value = "") {
   return TB(S("기술조건", ModuleSlotKind.MoveCondition, value), (s, v) => {
    MoveCondition condition;
    if (!MoveCondition.TryParse(v, out condition)) throw new ArgumentException("기술 조건을 읽을 수 없습니다.");
    s.Condition = condition;
   }, s => s.Condition.Text);
  }

  static TriggerBinding TN(ModuleSlotKind kind, string value, params string[] choices) {
   var slot = S("이름", kind, value, choices);
   slot.Multiple = kind == ModuleSlotKind.State || kind == ModuleSlotKind.Type;
   return TB(slot, (s, v) => s.Names = v.Split('/'), s => string.Join("/", s.Names));
  }

  static TriggerBinding TT() {
   var slot = S("타입", ModuleSlotKind.Type, "불꽃");
   slot.Multiple = true;
   return TB(slot, (s, v) => s.Types = v.Split('/'), s => string.Join("/", s.Types));
  }

  static TriggerBinding TP(ModuleSlotKind kind, string value, params string[] choices) {
   return TB(S("장소", kind, value, choices), (s, v) => s.Place = v, s => s.Place);
  }

  static TriggerBinding TI(string id, ModuleSlotKind kind, string value, Action<TriggerSpec, int> write,
   Func<TriggerSpec, int> read) {
   var slot = S(id, kind, value);
   slot.Minimum = 1;
   return TB(slot, (s, v) => write(s, int.Parse(v, CultureInfo.InvariantCulture)),
    s => read(s).ToString(CultureInfo.InvariantCulture));
  }

  static ModuleKind TriggerKind(string module) {
   return TriggerModules.EventModules.Contains(module) ? ModuleKind.TriggerEvent : ModuleKind.TriggerState;
  }

  // 빈칸 없는 고정 문장 모듈.
  static void TF(List<ModuleDefinition> modules, string id, string module, string name, string family, string text) {
   T(modules, id, module, name, family, text, () => TriggerModules.Parse(text));
  }

  // 같은 꼴의 고정 문장을 선택 빈칸 하나로 합친 모듈. 선택지에 조사까지 넣어 표준 문장을 글자 그대로 만든다.
  static void TX(List<ModuleDefinition> modules, string id, string module, string name, string family,
   string template, string slotId, params string[] choices) {
   var slot = S(slotId, ModuleSlotKind.Text, choices[0], choices);
   Func<string, string> sentence = choice => template.Replace("{" + slotId + "}", choice);
   var definition = new ModuleDefinition("trigger." + id, name, TriggerKind(module), family, template, slot) { Expected = module };
   definition.Formatter = values => {
    string choice = values[slotId];
    if (!choices.Contains(choice)) throw new ArgumentException("선택지에 없는 값입니다.");
    var spec = TriggerModules.Parse(sentence(choice));
    if (spec.Module != module) throw new ArgumentException("이 선택은 트리거 조건으로 읽을 수 없습니다.");
    return TriggerModules.Canonical(spec);
   };
   definition.Reader = text => {
    var read = TriggerModules.Parse(text);
    if (read.Module != module) return null;
    string canonical = TriggerModules.Canonical(read);
    foreach (string choice in choices)
     if (TriggerModules.Canonical(TriggerModules.Parse(sentence(choice))) == canonical)
      return new Dictionary<string, string> { { slotId, choice } };
    return null;
   };
   modules.Add(definition);
  }

  static partial void RegisterTriggers(List<ModuleDefinition> modules) {
   RegisterTriggerEvents(modules);
   RegisterTriggerStates(modules);
   RegisterTriggerSpeciesGrades(modules);
   RegisterTurnConditions(modules);
   RegisterTriggerConditions(modules);
   RegisterExclusiveTriggers(modules);
   RegisterAbaddonTriggers(modules);
  }

  static void RegisterTriggerEvents(List<ModuleDefinition> modules) {
   TX(modules, "role.changed", RoleModules.EventModule, "역할이 됨", "역할·트레이너", "자신이 {역할} 되었을 때", "역할", RoleModules.Choices.Select(RoleModules.TriggerChoice).ToArray());
   TF(modules, "item.stolen", "소지품 소실", "소지품을 빼앗김", "소지품·특성", "자신의 소지품을 빼앗겼을 때");
   T(modules, "entry", "등장", "장소에 등장", "등장·교대", "{주체}가 장소에 나올 때",
    () => new TriggerSpec { Module = "등장" }, TS("자신", "상대"));
   T(modules, "entry.method", "등장", "방식을 지정해 등장", "등장·교대", "{주체}가 {방식}로 장소에 나올 때",
    () => new TriggerSpec { Module = "등장" }, TS("자신", "상대"),
    TB(S("방식", ModuleSlotKind.Text, "선발", TriggerModules.EntryMethods), (s, v) => s.Method = v, s => s.Method));
   T(modules, "entry.type", "등장", "타입 아군과 교대해 등장", "등장·교대", "「{타입}」 타입 아군과 교대해 장소에 나올 때",
    () => new TriggerSpec { Module = "등장" }, TT());
   TF(modules, "switch.self", "교대", "아군과 임의교대", "등장·교대", TriggerStructures.SwitchLabel);
   T(modules, "switch.type", "교대", "타입 아군과 교대", "등장·교대", "「{타입}」 타입 아군과 교대할 때",
    () => new TriggerSpec { Module = "교대", Method = "임의" }, TT());
   TX(modules, "switch.opponent", "교대", "상대 교대", "등장·교대", "상대가 {방식}할 때", "방식", "교대", "임의교대");
   foreach (bool forced in new[] { false, true }) {
    bool f = forced;
    T(modules, "leave." + f, "퇴장", f ? "강제교대를 포함한 퇴장" : "장소에서 퇴장", "등장·교대",
     "{주체}가 " + (f ? "강제교대를 포함해 " : "") + "장소를 떠날 때",
     () => new TriggerSpec { Module = "퇴장", IncludeForced = f }, TS("자신", "상대"));
   }
   T(modules, "timing", "타이밍", "턴 타이밍", "턴·행동", "{시점}",
    () => new TriggerSpec { Module = "타이밍" },
    TB(S("시점", ModuleSlotKind.Timing, TriggerStructures.TurnEndLabel, TriggerStructures.TurnStartLabel,
     TriggerStructures.TurnEndLabel, TriggerStructures.TurnLastLabel), (s, v) => s.Method = v, s => s.Method));
   T(modules, "timing.every", "타이밍", "배수 턴 타이밍", "턴·행동", "「{배수}」배수 {시점}",
    () => new TriggerSpec { Module = "타이밍" }, TI("배수", ModuleSlotKind.Count, "2", (s, n) => s.Every = n, s => s.Every),
    TB(S("시점", ModuleSlotKind.Timing, TriggerStructures.TurnEndLabel, TriggerStructures.TurnStartLabel,
     TriggerStructures.TurnEndLabel), (s, v) => s.Method = v, s => s.Method));
   T(modules, "timing.field", "타이밍", "등장 후 지정 턴 종료", "턴·행동", "장소에 나와 「{턴}」T째 종료 시",
    () => new TriggerSpec { Module = "타이밍", Method = TriggerStructures.TurnEndLabel },
    TI("턴", ModuleSlotKind.Turns, "1", (s, n) => s.FieldTurn = n, s => s.FieldTurn));
   foreach (bool next in new[] { false, true }) {
    bool n = next;
    T(modules, "timing.event." + n, "타이밍", n ? "사건 다음 턴 종료" : "사건 성립 턴 종료", "턴·행동",
     "「{사건}」가 성립한 " + (n ? "다음 " : "") + "T 종료 시",
     () => new TriggerSpec { Module = "타이밍", NextTurn = n, Method = TriggerStructures.TurnEndLabel },
     TB(S("사건", ModuleSlotKind.Trigger, TriggerStructures.ReceivedLabel), (s, v) => s.Inner = v, s => s.Inner));
   }
   foreach (string module in new[] { "피격", "명중" }) foreach (bool attack in new[] { true, false }) foreach (bool ain in module == "피격" ? new[] { false, true } : new[] { false }) {
    string m = module;
    bool a = attack, h = ain;
    string obj = a ? "공격" : "기술";
    T(modules, (m == "피격" ? "received" : "hit") + "." + a + "." + h, m,
     (h ? "아인종 " : "") + obj + (m == "피격" ? "을 받음" : " 명중"), "기술·대미지",
     m == "피격" ? (h ? "아인종 " : "") + "상대의 {기술조건} " + obj + "을 받았을 때" : "자신의 {기술조건} " + obj + "이 명중했을 때",
     () => new TriggerSpec { Module = m, Attack = a, Ain = h }, TC());
   }
   foreach (string module in new[] { "급소", "급소 받음" }) {
    string m = module;
    T(modules, m == "급소" ? "critical" : "critical.received", m, m, "기술·대미지",
     (m == "급소" ? "자신의" : "상대의") + " {기술조건} 기술이 급소에 맞았을 때",
     () => new TriggerSpec { Module = m }, TC());
   }
   foreach (bool after in new[] { false, true }) {
    bool a = after;
    T(modules, "move." + a, "기술 사용", a ? "기술을 내보낸 직후" : "기술을 내보내기 직전", "기술·대미지",
     "{주체}가 {기술조건} 기술을 " + (a ? "내보냈을 때" : "내보낼 때"),
     () => new TriggerSpec { Module = "기술 사용", After = a }, TS("자신", "상대"), TC());
   }
   TX(modules, "defeat", "쓰러뜨림", "상대를 쓰러뜨림", "기술·대미지", "{대상} 쓰러뜨렸을 때", "대상", "상대를", "아인종 포켓몬을");
   T(modules, "defeat.type", "쓰러뜨림", "타입 포켓몬을 쓰러뜨림", "기술·대미지", "「{타입}」 타입 포켓몬을 쓰러뜨렸을 때",
    () => new TriggerSpec { Module = "쓰러뜨림" }, TT());
   T(modules, "defeat.move", "쓰러뜨림", "조건 기술로 쓰러뜨림", "기술·대미지", "자신의 {기술조건} 기술로 상대를 쓰러뜨렸을 때",
    () => new TriggerSpec { Module = "쓰러뜨림" }, TC("「불꽃」"));
   T(modules, "nullified", "무효화", "조건 기술을 무효화", "기술·대미지", "상대의 {기술조건} 기술을 무효화했을 때",
    () => new TriggerSpec { Module = "무효화" }, TC("「불꽃」"));
   T(modules, "recover", "회복", "체력을 회복", "체력·상태", "{주체}가 체력을 회복했을 때",
    () => new TriggerSpec { Module = "회복" }, TS("자신", "상대"));
   foreach (string method in new[] { "소모", "파괴", "소모 또는 파괴" }) {
    string m = method;
    T(modules, "itemloss." + m, "소지품 소실", "소지품 " + m, "소지품·특성", "{주체}의 소지품이 " + (m == "소모 또는 파괴" ? "소모(파괴)" : m) + "됐을 때",
     () => new TriggerSpec { Module = "소지품 소실", Method = m }, TS("자신", "상대"));
   }
   TF(modules, "ability.any", "특성 발동", "특성 발동(전체)", "소지품·특성", "자신의 특성이 발동했을 때");
   T(modules, "ability.named", "특성 발동", "지정 특성 발동", "소지품·특성", "자신의 「{이름}」 특성이 발동했을 때",
    () => new TriggerSpec { Module = "특성 발동" }, TN(ModuleSlotKind.Ability, "부유"));
  }
 }
}
