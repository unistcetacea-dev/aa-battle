using System;
using System.Collections.Generic;
using System.Globalization;

namespace AABattle {
 public static partial class ModuleCatalog {
  static void RegisterTriggerConditions(List<ModuleDefinition> modules) {
   TF(modules, "always", "상시", "상시", "상시·배틀", TriggerStructures.AlwaysLabel);
   TF(modules, "onfield", "상시", "장소에 있는 한", "상시·배틀", TriggerStructures.FieldLabel);
   TF(modules, "here", "여기다", "여기다", "상시·배틀", TriggerStructures.HereLabel);
   TF(modules, "battlestart", "배틀 시작", "배틀 시작", "상시·배틀", TriggerStructures.BattleStartLabel);
   TF(modules, "observer", "PT 참가", "PT에 참여 중", "파티·트레이너", TriggerStructures.ObserverLabel);
   TX(modules, "order", "지령", "지령을 받음", "파티·트레이너", "{주체} 지령을 받았을 때", "주체", "자신이", "아군이");
   TF(modules, "analyzed", "해석", "상대 데이터 해석", "데이터 해석", TriggerStructures.AnalyzedLabel);
   TF(modules, "analyzed.self", "해석당함", "자신의 데이터가 해석됨", "데이터 해석", "자신의 데이터가 해석되었을 때");
   TX(modules, "analyzedstate", "해석 상태", "데이터 해석 상태", "데이터 해석", "{상태}", "상태",
    "적진에 데이터가 해석된 상대가 있을 때", "자신의 데이터가 해석되어 있을 때", "자신의 데이터가 해석되지 않았을 때");
   TF(modules, "receivedornullified", "피격·무효화", "공격 피격 또는 무효화", "기술·대미지", TriggerStructures.ReceivedOrNullifiedLabel);
   TF(modules, "damaged", "대미지", "대미지를 받음", "기술·대미지", TriggerStructures.DamagedLabel);
   TF(modules, "missed", "빗나감", "기술이 빗나감", "기술·대미지", "자신의 기술이 빗나갔을 때");
   TF(modules, "indirect", "간접 대미지", "상대가 간접 대미지를 받음", "기술·대미지", "상대가 공격 이외의 대미지를 받았을 때");
   TF(modules, "drain", "드레인", "상대 체력 드레인", "기술·대미지", "상대의 체력을 드레인했을 때");
   TF(modules, "faint.any", "필드 빈사", "필드의 누군가 빈사", "체력·상태", "필드의 누군가가 「빈사」 상태가 되었을 때");
   TF(modules, "berry", "나무열매 사용", "나무열매 사용", "소지품·특성", "자신이 「나무열매」를 사용했을 때");
   TF(modules, "damage.small", "작은 대미지", "작은 대미지 이하를 받음", "기술·대미지", "「작은」 이하의 대미지를 받았을 때");
   TF(modules, "rank.nullified", "하락 무효", "상대에 의한 능력 하락 무효", "능력", "상대에 의한 능력 하락을 무효화했을 때");
   TF(modules, "intimidated", "위협 받음", "위협을 받음", "소지품·특성", "상대의 〔위협〕을 받았을 때");
   TF(modules, "received.fullhp", "가득 피격", "체력이 가득인 상태로 피격", "기술·대미지", "체력이 가득인 상태로 공격을 받았을 때");
   TX(modules, "protect", "방어 성공", "방어로 무효화", "기술·대미지", "「방어」 등의 기술로 상대의 {대상} 무효화했을 때", "대상", "공격을", "기술을");
   TF(modules, "protect.state", "방어 성공", "방어 상태가 공격을 막음", "기술·대미지", "자신의 방어 상태가 공격을 막았을 때");
   TX(modules, "selected.both", "행동 선택", "서로 같은 종류 선택", "턴·행동", "서로 {종류} 선택했을 때", "종류", "변화기술을", "공격기를", "필중 기술을");
   TX(modules, "selected.idle", "행동 선택", "공격하지 않는 행동 선택", "턴·행동", "{주체} 공격하지 않는 행동을 선택했을 때", "주체", "자신이", "상대가");
   TX(modules, "order.before", "행동 선후", "먼저 행동할 때", "턴·행동", "{주체} 먼저 행동할 때", "주체", "자신이 상대보다", "상대가 자신보다");
   TX(modules, "order.after", "먼저 행동함", "먼저 행동했을 때", "턴·행동", "{주체} 먼저 행동했을 때", "주체", "자신이 상대보다", "상대가 자신보다");
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
   foreach (string module in new[] { "부여", "포텐셜 보유", "생존" }) {
    string m = module;
    T(modules, "named." + m, m, m, "파티·트레이너", m == "부여" ? "『{이름}』이 부여되어 있을 때" :
     m == "포텐셜 보유" ? "자신이 『{이름}』를 보유하고 있을 때" : "아군에 「{이름}」가 생존해 있을 때",
     () => new TriggerSpec { Module = m }, TN(ModuleSlotKind.Text, m == "부여" ? "계약의 특권" : m == "포텐셜 보유" ? "에이스" : "피카츄"));
   }
   TF(modules, "last", "생존", "최후의 1체", "파티·트레이너", "자신만 생존해 있을 때");
   TX(modules, "faintcount", "빈사 수", "빈사 수 비교", "파티·트레이너", "아군의 빈사 수가 상대보다 {비교} 때", "비교", "많을", "같을", "적을");
   TF(modules, "item.exists", "소지품", "소지품을 보유함", "소지품·특성", "자신이 소지품을 가지고 있을 때");
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
