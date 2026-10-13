using System;
using System.Collections.Generic;
using System.Globalization;

namespace AABattle {
 public static partial class ModuleCatalog {
  static void RegisterTriggerConditions(List<ModuleDefinition> modules) {
   TL(modules, "always", "상시", "상시", TriggerStructures.AlwaysLabel);
   TL(modules, "onfield", "상시", "장소에 있는 한", TriggerStructures.FieldLabel);
   TL(modules, "observer", "PT 참가", "PT에 참여 중", TriggerStructures.ObserverLabel);
   TL(modules, "here", "여기다", "여기다", TriggerStructures.HereLabel);
   TL(modules, "battlestart", "배틀 시작", "배틀 시작", TriggerStructures.BattleStartLabel);
   TL(modules, "order.self", "지령", "자신이 지령 받음", TriggerStructures.SelfOrderLabel);
   TL(modules, "order.ally", "지령", "아군이 지령 받음", TriggerStructures.AllyOrderLabel);
   TL(modules, "analyzed", "해석", "상대 데이터 해석", TriggerStructures.AnalyzedLabel);
   TL(modules, "receivedornullified", "피격·무효화", "공격 피격 또는 무효화", TriggerStructures.ReceivedOrNullifiedLabel);
   TL(modules, "damaged", "대미지", "대미지를 받음", TriggerStructures.DamagedLabel);
   TL(modules, "missed", "빗나감", "기술이 빗나감", "자신의 기술이 빗나갔을 때");
   TL(modules, "indirect", "간접 대미지", "상대가 간접 대미지를 받음", "상대가 공격 이외의 대미지를 받았을 때");
   TL(modules, "drain", "드레인", "상대 체력 드레인", "상대의 체력을 드레인했을 때");
   TL(modules, "analyzed.self", "해석당함", "자신의 데이터가 해석됨", "자신의 데이터가 해석되었을 때");
   TL(modules, "faint.any", "필드 빈사", "필드의 누군가 빈사", "필드의 누군가가 「빈사」 상태가 되었을 때");
   TL(modules, "berry", "나무열매 사용", "나무열매 사용", "자신이 「나무열매」를 사용했을 때");
   TL(modules, "damage.small", "작은 대미지", "작은 대미지 이하를 받음", "「작은」 이하의 대미지를 받았을 때");
   TL(modules, "rank.nullified", "하락 무효", "상대에 의한 능력 하락 무효", "상대에 의한 능력 하락을 무효화했을 때");
   TL(modules, "intimidated", "위협 받음", "위협을 받음", "상대의 〔위협〕을 받았을 때");
   TL(modules, "received.fullhp", "가득 피격", "체력이 가득인 상태로 피격", "체력이 가득인 상태로 공격을 받았을 때");
   foreach (string obj in new[] { "공격", "기술" })
    TL(modules, "protect." + obj, "방어 성공", "방어로 " + obj + " 무효화", "「방어」 등의 기술로 상대의 " + obj + "을 무효화했을 때");
   TL(modules, "protect.state", "방어 성공", "방어 상태가 공격을 막음", "자신의 방어 상태가 공격을 막았을 때");
   TL(modules, "selected.both.변화기술", "행동 선택", "서로 변화기술 선택", "서로 변화기술을 선택했을 때");
   TL(modules, "selected.both.공격기", "행동 선택", "서로 공격기 선택", "서로 공격기를 선택했을 때");
   TL(modules, "selected.both.필중 기술", "행동 선택", "서로 필중 기술 선택", "서로 필중 기술을 선택했을 때");
   foreach (string subject in new[] { "자신", "상대" }) {
    string owner = subject == "상대" ? "상대가" : "자신이";
    string other = subject == "상대" ? "자신" : "상대";
    TL(modules, "selected.idle." + subject, "행동 선택", subject + "이 공격하지 않는 행동 선택", owner + " 공격하지 않는 행동을 선택했을 때");
    TL(modules, "order.before." + subject, "행동 선후", subject + "이 먼저 행동할 때", owner + " " + other + "보다 먼저 행동할 때");
    TL(modules, "order.after." + subject, "먼저 행동함", subject + "이 먼저 행동했을 때", owner + " " + other + "보다 먼저 행동했을 때");
   }
   T(modules, "idle", "공격하지 않음", "그 턴에 공격하지 않음", "턴·행동", "{주체}가 공격하지 않을 때",
    () => new TriggerSpec { Module = "공격하지 않음" }, TS("자신", "상대"));
   T(modules, "opponent.type", "적진 타입", "적진의 타입", "파티·트레이너", "적진에 「{이름}」 포켓몬이 있을 때",
    () => new TriggerSpec { Module = "적진 타입" }, TN(ModuleSlotKind.Type, "불꽃"));
   foreach (bool opponent in new[] { false, true }) {
    bool o = opponent;
    T(modules, "trainer.aptitude." + o, "트레이너 자질", (o ? "상대 " : "") + "트레이너 자질 등급", "파티·트레이너",
     (o ? "상대 " : "") + "트레이너가 「{자질}:{등급}」 이상일 때",
     () => new TriggerSpec { Module = "트레이너 자질", Subject = o ? "상대" : "자신", Names = new string[2] },
     TB(S("자질", ModuleSlotKind.TrainerAptitude, "지시", "지시", "육성", "통솔", "능력"), (s, v) => s.Names[0] = v, s => s.Names[0]),
     TB(S("등급", ModuleSlotKind.Grade, "A", "E", "D", "C", "B", "A", "AA", "AAA", "S"), (s, v) => s.Names[1] = v, s => s.Names[1]));
   }
   TL(modules, "analyzedstate.opponent", "해석 상태", "상대의 데이터가 해석된 상태", "적진에 데이터가 해석된 상대가 있을 때");
   TL(modules, "analyzedstate.self", "해석 상태", "자신의 데이터가 해석된 상태", "자신의 데이터가 해석되어 있을 때");
   TL(modules, "analyzedstate.unknown", "해석 상태", "자신의 데이터가 미해석", "자신의 데이터가 해석되지 않았을 때");
   foreach (string module in new[] { "부여", "포텐셜 보유", "생존" }) {
    string m = module;
    T(modules, "named." + m, m, m, "파티·트레이너", m == "부여" ? "『{이름}』이 부여되어 있을 때" :
     m == "포텐셜 보유" ? "자신이 『{이름}』를 보유하고 있을 때" : "아군에 「{이름}」가 생존해 있을 때",
     () => new TriggerSpec { Module = m }, TN(ModuleSlotKind.Text, m == "부여" ? "계약의 특권" : m == "포텐셜 보유" ? "에이스" : "피카츄"));
   }
   TL(modules, "last", "생존", "최후의 1체", "자신만 생존해 있을 때");
   foreach (string op in new[] { "많을", "같을", "적을" })
    TL(modules, "faintcount." + op, "빈사 수", "빈사 수가 상대보다 " + op + " 때", "아군의 빈사 수가 상대보다 " + op + " 때");
   TL(modules, "item.exists", "소지품", "소지품을 보유함", "자신이 소지품을 가지고 있을 때");
   T(modules, "item.absent", "소지품", "소지품이 없음", "소지품·특성", "{주체}가 소지품을 가지고 있지 않을 때",
    () => new TriggerSpec { Module = "소지품", Op = "없음" }, TS("자신", "상대"));
   foreach (string module in new[] { "소지품", "특성" }) {
    string m = module;
    T(modules, "held." + m, m, "현재 " + m, "소지품·특성", "{주체}의 " + m + "이 「{이름}」일 때",
     () => new TriggerSpec { Module = m }, TS("자신", "상대"), TN(m == "특성" ? ModuleSlotKind.Ability : ModuleSlotKind.Item, m == "특성" ? "부유" : "먹다남은음식"));
   }
   foreach (string side in new[] { "자신", "상대", "양쪽" }) foreach (bool layers in new[] { false, true }) {
    string s = side;
    bool l = layers;
    var bindings = new List<TriggerBinding> { TN(ModuleSlotKind.Hazard, l ? "압정" : "스텔스록", "스텔스록", "압정", "독압정", "끈적끈적네트", "순풍", "리플렉터", "빛의장막") };
    if (l) bindings.Add(TI("겹", ModuleSlotKind.Count, "2", (x, n) => x.Top = n, x => x.Top));
    string label = s == "상대" ? "적진" : s == "양쪽" ? "필드" : "자진";
    T(modules, "side." + s + "." + l, "진영", label + (l ? " 설치물 층수" : " 효과 있음"), "날씨·장소",
     label + (l ? "의 「{이름}」이 {겹}겹 이상일 때" : "에 「{이름}」이 있을 때"),
     () => new TriggerSpec { Module = "진영", Subject = s, Top = 0 }, bindings.ToArray());
   }
   foreach (bool positive in new[] { false, true }) {
    bool p = positive;
    T(modules, "rank.total." + p, "능력 변화 합", p ? "능력 변화 합이 양수" : "능력 변화 합이 음수", "능력",
     "자신의 능력 변화가 " + (p ? "+{합} 이상" : "-{합} 이하") + "일 때",
     () => new TriggerSpec { Module = "능력 변화 합" }, TI("합", ModuleSlotKind.Count, "3", (s, n) => s.Top = p ? n : -n, s => Math.Abs(s.Top)));
   }
   T(modules, "flavor.affiliation", "플레이버", "트레이너 소속", "파티·트레이너", "소속이 《{이름}》일 때",
    () => new TriggerSpec { Module = "플레이버" }, TB(S("이름", ModuleSlotKind.Text, "학원"),
     (s, v) => s.Place = "소속이 《" + v + "》일 때", s => ExtractTriggerName(s.Place, '《', '》')));
   T(modules, "flavor.gender", "플레이버", "트레이너 성별", "파티·트레이너", "트레이너가 「{이름}」일 때",
    () => new TriggerSpec { Module = "플레이버" }, TB(S("이름", ModuleSlotKind.Text, "남성", "남성", "여성"),
     (s, v) => s.Place = "트레이너가 「" + v + "」일 때", s => ExtractTriggerName(s.Place, '「', '」')));
   T(modules, "flavor.name", "플레이버", "트레이너 이름", "파티·트레이너", "「{이름}」가 트레이너일 때",
    () => new TriggerSpec { Module = "플레이버" }, TB(S("이름", ModuleSlotKind.Text, "레드"),
     (s, v) => s.Place = TriggerModules.Normalize("「" + v + "」가 트레이너일 때"), s => ExtractTriggerName(s.Place, '「', '」')));
  }

  static string ExtractTriggerName(string text, char open, char close) {
   int start = text.IndexOf(open), end = text.IndexOf(close);
   return start < 0 || end <= start ? "" : text.Substring(start + 1, end - start - 1);
  }
 }
}
