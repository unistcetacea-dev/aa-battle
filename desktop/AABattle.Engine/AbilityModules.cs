using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace AABattle {
 // 특성 모듈(사용자 결정): 특성 도감 문장을 포텐셜과 같은 트리거·효과 모듈로 읽어 같은 실행기로 돌린다.
 // 슬롯 「특성」으로 따로 두고(로그는 〔특성명〕, 「특성이 발동했을 때」 사건), 포켓몬의 포텐셜 데이터로 저장하지 않는다(배틀용 복사본에만 붙인다).
 // 기준: 도감 문장 → 오레마스 문서 → 실기. 문장으로 표현할 수 없는 특성은 전용 코드(AbilityRuntime 등)가 맡는다(Dedicated).
 public static class AbilityModules {
  public const string Slot = "특성";
  // 모듈로 표현할 수 없는 전용 규칙만 남긴다
  public static readonly HashSet<string> Dedicated = new HashSet<string>();
  // 해석하지 않은 문장은 보류하고 검증된 나머지 문장만 연결한다.
  public static readonly Dictionary<string, string> Pending = new Dictionary<string, string>(AbilityRewrites3.PendingReasons);
  // 도감 문장의 모듈 표현
  public static readonly Dictionary<string, string> Rewrites = new Dictionary<string, string> {
   { "나쁜손버릇", "상대의 공격을 받았을 때, 상대의 소지품을 빼앗는다." },
   { "노상강도", "자신의 「추격」 기술이 명중했을 때, 상대의 소지품을 빼앗는다." },
   { "매지션", "자신의 공격이 명중했을 때, 상대의 소지품을 빼앗는다." },
   { "곡예", "자신의 소지품이 소모(파괴)됐을 때, 자신의 「속도」를 강화(2배)한다." },
   { "긴장감", "상대는 소비하는 소지품을 사용할 수 없다." },
   { "서투름", "자신은 소지품을 사용할 수 없다." },
   { "점착", "자신의 소지품은 파괴되거나 약탈되지 않는다." },
   { "떠도는영혼", "상대의 「접촉」 기술을 받았을 때, 자신과 상대의 특성을 교환한다." },
   { "화학의힘", "죽어내밀기로 장소에 나올 때, 직전에 빈사한 아군의 특성을 복사한다." },
   { "논리의불꽃", "자신의 기술은 상대의 〔방호 특성〕을 관통한다. 자신의 특성은 변화되지 않는다." },
   { "준케츠", "상대의 「일격필살」 기술을 무효화한다. 자신은 공격 이외의 대미지를 받지 않는다. 자신의 특성은 변화되지 않는다." },
   { "폴리코롤", "상대의 「불꽃/전기/얼음」 기술을 무효화한다. 자신의 특성은 변화되지 않는다." },
   { "메탈프로텍트", "자신은 상대에 의해 능력이 내려가지 않는다. 자신의 특성은 무효화되지 않는다. 자신의 특성은 관통되지 않는다." },
   { "검은연기", "상대에 의해 자신은 「상태변화」 상태가 되지 않는다." },
   { "거꾸로", "필드에 있는 전원의 타입 상성을 반전한다." },
   { "중립", "필드에 있는 전원의 기술의 타입 상성을 「효과 보통」으로 한다." },
   { "재앙의검", "자신을 제외한 전원의 「방어」를 약화(0.5배)한다." },
   { "재앙의구슬", "자신을 제외한 전원의 「특방」을 약화(0.5배)한다." },
   { "재앙의그릇", "자신을 제외한 전원의 「특공」을 약화(0.5배)한다." },
   { "재앙의부적", "자신을 제외한 전원의 「공격」을 약화(0.5배)한다." },
   { "다크오라", "필드에 있는 전원의 「악」 기술의 위력을 강화(1.5배)한다." },
   { "페어리오라", "필드에 있는 전원의 「페어리」 기술의 위력을 강화(1.5배)한다." },
   { "터지는오라", "필드에 있는 전원의 「페어리/악」 기술의 위력을 강화(1.5배)한다. 자신의 기술은 상대의 〔방호 특성〕을 관통한다." },
   { "내진", "자신이 받는 「땅」 기술의 대미지를 약화(0.5배)한다." },
   { "헥사그램", "자신이 받는 「불꽃/물/풀/전기/얼음/땅」 기술의 대미지를 약화(0.5배)한다." },
   { "옥타그램", "자신이 받는 「불꽃/물/풀/전기/얼음/땅/바위/비행」 기술의 대미지를 약화(0.67배)한다." },
   { "정령", "자신의 「페어리」 기술의 위력을 강화(2배)한다. 자신이 받는 「페어리」 기술의 대미지를 강화(2배)한다." },
   { "무한의검", "자신의 「강철」 기술의 타입 일치 배율을 「2배」로 한다. 자신의 기술의 타입을 「강철」로 한다. 「강철」 타입에게 자신의 기술의 상성을 「효과가 굉장」으로 한다." },
   { "무한의불꽃", "자신의 「불꽃」 기술의 타입 일치 배율을 「2배」로 한다. 자신의 기술의 타입을 「불꽃」으로 한다. 「불꽃」 타입에게 자신의 기술의 상성을 「효과가 굉장」으로 한다." },
   { "무한의새싹", "자신의 「풀」 기술의 타입 일치 배율을 「2배」로 한다. 자신의 기술의 타입을 「풀」로 한다. 「풀」 타입에게 자신의 기술의 상성을 「효과가 굉장」으로 한다." },
   { "강철정신", "자신의 「강철」 기술의 위력을 강화(1.5배)한다." },
   { "배터리", "자신의 「특수」 기술의 위력을 강화(1.3배)한다." },
   { "스위트베일", "자신은 「잠듦」 상태가 되지 않는다." },
   { "파스텔베일", "자신은 「독/맹독」 상태가 되지 않는다." },
   { "파워스폿", "자신의 기술의 위력을 강화(1.3배)한다." },
   { "승리의별", "자신의 기술의 명중을 강화(1.2배)한다." },
   { "프렌드가드", "자신이 받는 대미지를 약화(0.75배)한다." },
   { "뇌광난무", "자신의 「전기」「공격」 기술을 내보냈을 때, 가장 먼저 같은 기술에 의한 추가행동을 얻는다(위력 0.5배)." },
   { "빙결난무", "자신의 「얼음」「공격」 기술을 내보냈을 때, 가장 먼저 같은 기술에 의한 추가행동을 얻는다(위력 0.5배)." },
   { "열화난무", "자신의 「불꽃」「공격」 기술을 내보냈을 때, 가장 먼저 같은 기술에 의한 추가행동을 얻는다(위력 0.5배)." },
   { "유성난무", "자신의 「드래곤」「공격」 기술을 내보냈을 때, 가장 먼저 같은 기술에 의한 추가행동을 얻는다(위력 0.5배)." },
   { "도검난무", "자신의 「칼/검」「공격」 기술을 내보냈을 때, 가장 먼저 같은 기술에 의한 추가행동을 얻는다(위력 0.5배)." },
   { "쌍권총", "자신의 「탄/볼」「공격」 기술을 내보냈을 때, 가장 먼저 같은 기술에 의한 추가행동을 얻는다(위력 0.5배)." },
   { "기를쓰기", "상대를 쓰러뜨렸을 때, 가장 마지막에 추가행동을 얻는다." },
   { "무희", "상대의 「춤」 기술을 내보냈을 때, 가장 먼저 상대와 같은 기술에 의한 추가행동을 얻는다." },
   { "일광욕", "상대를 쓰러뜨렸을 때, 가장 마지막에 「기술:광합성」 기술에 의한 추가행동을 얻는다." },
   { "가시비늘", "상대의 「접촉」 기술을 받았을 때, 상대에게 최대 HP의 1/8 대미지를 준다. 상대의 기술의 급소 랭크 보정을 0으로 한다." },
   { "갑옷투구", "자신의 기술의 반동 대미지를 받지 않는다. 상대의 기술의 급소 랭크 보정을 0으로 한다." },
   { "다이아가드", "자신은 공격 이외의 대미지를 받지 않는다. 상대의 기술의 급소 랭크 보정을 0으로 한다." },
   { "당세구족", "자신은 설치물의 영향을 받지 않는다. 상대의 기술의 급소 랭크 보정을 0으로 한다." },
   { "딱딱한비늘", "자신이 받는 「접촉」 기술의 대미지를 약화(0.75배)한다. 상대의 기술의 급소 랭크 보정을 0으로 한다." },
   { "마그마의비늘", "상대의 「접촉」 기술을 받았을 때, 3할로 상대를 「화상」 상태로 한다. 상대의 기술의 급소 랭크 보정을 0으로 한다." },
   { "아로마가드", "자신은 「헤롱헤롱/앙코르/트집/사슬묶기/도발/회복봉인」 상태가 되지 않는다. 상대의 기술의 급소 랭크 보정을 0으로 한다." },
   { "다운로드", "필드에 나왔을 때, 상대의 「방어」가 「특방」보다 높을 때, 자신의 「특공」이 오른다. 필드에 나왔을 때, 상대의 「방어」가 「특방」 이하일 때, 자신의 「공격」이 오른다." },
   { "단순", "자신의 능력 변화량을 1 늘린다." },
   { "심술꾸러기", "자신에 대한 능력 변화를 반대로 한다." },
   { "미러아머", "상대에 의한 능력 하락을 상대에게 되돌린다." },
   { "날씨부정", "날씨의 효과를 무효화한다." },
   { "에어록", "날씨의 효과를 무효화한다." },
   { "틈새포착", "자신의 「변화」 기술이 상대의 「방어」 기술을 관통한다." },
   { "부식", "자신이 거는 「독」「맹독」은 타입 면역을 관통한다." },
   { "자연회복", "강제교대를 포함해 장소를 떠날 때, 자신의 「상태이상」을 치유한다." },
   { "화학변화가스", "상대의 〔특성〕을 무효화한다." },
   { "가뭄", "필드에 나왔을 때, 5T 동안 날씨를 「쾌청」으로 한다." }, { "잔비", "필드에 나왔을 때, 5T 동안 날씨를 「비」로 한다." }, { "모래날림", "필드에 나왔을 때, 5T 동안 날씨를 「모래바람」으로 한다." }, { "눈퍼뜨리기", "필드에 나왔을 때, 5T 동안 날씨를 「싸라기눈」으로 한다." },
   { "그래스메이커", "필드에 나왔을 때, 5T 동안 지형을 「그래스필드」로 한다." }, { "일렉트릭메이커", "필드에 나왔을 때, 5T 동안 지형을 「일렉트릭필드」로 한다." }, { "사이코메이커", "필드에 나왔을 때, 5T 동안 지형을 「사이코필드」로 한다." }, { "미스트메이커", "필드에 나왔을 때, 5T 동안 지형을 「미스트필드」로 한다." },
   { "위협", "필드에 나왔을 때, 상대의 「공격」이 내린다." }, { "쓱쓱", "날씨가 「비」일 때, 자신의 「속도」를 강화(2배)한다." }, { "엽록소", "날씨가 「쾌청」일 때, 자신의 「속도」를 강화(2배)한다." }, { "모래헤치기", "날씨가 「모래바람」일 때, 자신의 「속도」를 강화(2배)한다. 자신은 「모래바람」의 대미지를 받지 않는다." }, { "눈치우기", "날씨가 「싸라기눈」일 때, 자신의 「속도」를 강화(2배)한다." },
   { "적응력", "자신의 기술의 타입 일치 배율을 「2배」로 한다." }, { "테크니션", "자신의 「위력 60 이하」 기술의 위력을 강화(1.5배)한다." }, { "철주먹", "자신의 「펀치」 기술의 위력을 강화(1.5배)한다." }, { "메가런처", "자신의 「파동」「탄/볼」 기술의 위력을 강화(1.5배)한다." }, { "옹골찬턱", "자신의 체력이 가득일 때, 자신은 빈사가 되는 공격을 HP 1로 버틴다. 자신의 「물기/엄니」 기술의 위력을 강화(1.5배)한다." }, { "예리함", "자신의 「칼/검」 기술의 위력을 강화(1.5배)한다." }, { "이판사판", "자신의 「반동」 기술의 위력을 강화(1.2배)한다." },
   { "타오르는불꽃", "상대의 「불꽃」 기술을 무효화한다. 상대의 「불꽃」 기술을 무효화했을 때, 자신의 「특공」이 오른다." }, { "축전", "상대의 「전기」 기술을 무효화한다. 상대의 「전기」 기술을 무효화했을 때, 자신의 체력을 최대 HP의 1/4 회복한다." }, { "저수", "상대의 「물」 기술을 무효화한다. 상대의 「물」 기술을 무효화했을 때, 자신의 체력을 최대 HP의 1/4 회복한다." }, { "피뢰침", "상대의 「전기」 기술을 무효화한다. 상대의 「전기」 기술을 무효화했을 때, 자신의 「특공」이 오른다." }, { "마중물", "상대의 「물」 기술을 무효화한다. 상대의 「물」 기술을 무효화했을 때, 자신의 「특공」이 오른다." }, { "전기엔진", "상대의 「전기」 기술을 무효화한다. 상대의 「전기」 기술을 무효화했을 때, 자신의 「속도」가 오른다." }, { "초식", "상대의 「풀」 기술을 무효화한다. 상대의 「풀」 기술을 무효화했을 때, 자신의 「공격」이 오른다." },
   { "불면", "자신은 「잠듦」상태가 되지 않는다." }, { "유연", "자신은 「마비」상태가 되지 않는다." }, { "면역", "자신은 「독/맹독」상태가 되지 않는다." }, { "마이페이스", "자신은 「혼란/도발」 상태가 되지 않는다." }, { "의기양양", "상대의 기술에 의해 자신은 「잠듦」상태가 되지 않는다." }, { "수의베일", "필드에 나왔을 때, 자신을 「아쿠아링」 상태로 한다. 자신은 「화상」상태가 되지 않는다." }, { "마그마의무장", "자신은 「얼음/동면」상태가 되지 않는다. 자신은 급소에 맞지 않는다." },
   { "멀티스케일", "자신은 급소에 맞지 않는다. 자신의 체력이 가득일 때, 자신이 받는 대미지를 약화(0.5배)한다." }, { "방음", "상대의 「소리」 기술을 무효화한다." }, { "방진", "상대의 「가루」 기술을 무효화한다. 자신은 「모래바람」「싸라기눈」의 대미지를 받지 않는다." }, { "방탄", "상대의 「탄/볼」 기술을 무효화한다. 자신이 받는 「기술:자폭」「기술:대폭발」 기술의 대미지를 약화(0.5배)한다." }, { "매직가드", "자신은 공격 이외의 대미지를 받지 않는다." }, { "부유", "자신은 공중에 떠오른다. 자신은 설치물의 영향을 받지 않는다." },
   { "하얀연기", "자신은 상대에 의해 능력이 내려가지 않는다. 자신의 「회피」를 강화(1.1배)한다." }, { "클리어바디", "자신은 상대에 의해 능력이 내려가지 않는다." }, { "오기", "상대에 의해 자신의 능력이 내려갔을 때, 자신의 「공격」이 크게 오른다." }, { "승기", "상대에 의해 자신의 능력이 내려갔을 때, 자신의 「특공」이 크게 오른다." }, { "틀깨기", "자신의 기술은 상대의 〔방호 특성〕을 관통한다." }, { "터보블레이즈", "자신의 기술은 상대의 〔방호 특성〕을 관통한다." }, { "테라볼티지", "자신의 기술은 상대의 〔방호 특성〕을 관통한다." },
   { "배짱", "자신의 「노말/격투」 기술이 「고스트」 타입의 무효를 관통한다." }, { "짓궂은마음", "자신의 「변화」 기술의 우선도를 +1한다." }, { "근성", "자신이 「상태이상」일 때, 자신의 「공격」을 강화(1.5배)한다. 「화상」「동상」의 기술 위력 저하 효과를 무효화한다." }, { "재생력", "장소를 떠날 때, 자신의 체력을 최대 HP의 1/3 회복한다." }, { "가속", "T 종료 시, 자신의 「속도」가 오른다." }, { "옹골참", "상대의 「일격필살」 기술을 무효화한다. 자신의 체력이 가득일 때, 자신은 빈사가 되는 공격을 HP 1로 버틴다." }
  };

  // 도감 문장 정리: 붙어 있는 문장 나누기, 변경 내역 괄호 메모 지우기
  public static string Clean(string effect) {
   string t = (effect ?? "").Replace('（', '(').Replace('）', ')').Replace('，', ',').Replace('．', '.').Replace('＋', '+').Replace('－', '-').Replace('：', ':');
   t = Regex.Replace(t, @"\s*\([^()]*(?:추가|변경|→|원작|실질적)[^()]*\)", "");
   t = Regex.Replace(t, @"\.(?=[^\s.\d)])", ". ");
   return Regex.Replace(t, @"\s+", " ").Trim();
  }
  static string Text(AbilityDefinition d) {
   string r;
   if (Rewrites.TryGetValue(d.name, out r) || AbilityRewrites3.Table.TryGetValue(d.name, out r) || AbilityRewrites4.Table.TryGetValue(d.name, out r)) return r;
   return AbilityPhrases.Fold(AbilityPhrases.Rewrite(Clean(d.effect)));
  }
  // 도감 표시와 효과 없음 집계용: 특성 문장 전체(효과 없음 문장 포함)
  public static string[] Sentences(AbilityDefinition d) {
   return Regex.Split(Text(d), @"(?<=[.。])\s+").Select(x => x.Trim()).Where(x => x.Length > 0).ToArray();
  }

  // 한 특성이 계속 효과와 사건 효과를 함께 가지면 「…때,」 절 단위로 포텐셜 하나씩
  public static Potential[] ToPotentials(AbilityDefinition d) {
   var parts = Split(Text(d));
   return parts.Select((x, i) => {
    string condition, effect; PotentialLibrary.Split(x, out condition, out effect);
    return new Potential { id = Slot, name = i == 0 ? d.name : d.name + " (" + (i + 1) + ")", description = x, triggers = condition.Length > 0 ? new string[0] : new[] { TriggerStructures.AlwaysLabel }, uses = "" };
   }).ToArray();
  }
  // 조건이 없는 문장(계속)과 「…때,」로 시작하는 문장(사건)을 나눈다
  static List<string> Split(string text) {
   var sentences = new List<string>();
   foreach (string raw in Regex.Split(text, @"(?<=[.。])\s+").Select(x => x.Trim()).Where(x => x.Length > 0 && !AbilityNoEffectModules.IsNoEffect(x))) {
    // 「이 대미지로는 빈사로 할 수 없다」는 앞 문장의 대미지에 붙는 보충이다
    if (raw.StartsWith("이 대미지로는 빈사로 할 수 없다") && sentences.Count > 0) sentences[sentences.Count - 1] += " " + raw;
    else sentences.Add(raw);
   }
   var result = new List<string>();
   string always = "";
   foreach (string s in sentences) {
    string condition, effect; PotentialLibrary.Split(s, out condition, out effect);
    if (condition.Length > 0) result.Add(s); else always += (always.Length > 0 ? " " : "") + s;
   }
   if (always.Length > 0) result.Insert(0, always);
   return result;
  }

  public static bool Runnable(AbilityDefinition d) {
   if (d == null || Dedicated.Contains(d.name)) return false;
   return ToPotentials(d).All(p => {
    string condition, effect; PotentialLibrary.Split(p.description, out condition, out effect);
    bool trigger = TriggerStructures.Clauses(p).All(c => c == TriggerStructures.AlwaysLabel || TriggerModules.IsModule(c));
    var sentences = Regex.Split(EffectNormalizer.NormalizeLines(effect.Length > 0 ? effect : p.description).Replace("\r", ""), @"\n|(?<=[.．。])\s+").Where(x => x.Trim().Length > 0).ToList();
    // 「이 대미지로는 빈사로 할 수 없다」는 앞 대미지 문장에 붙는 보충이다
    for (int i = sentences.Count - 1; i > 0; i--) {
     if (!sentences[i].Trim().StartsWith("이 대미지로는 빈사로 할 수 없다")) continue;
     sentences[i - 1] += " " + sentences[i].Trim();
     sentences.RemoveAt(i);
    }
    return trigger && sentences.Count > 0 && sentences.All(x => EffectModules.Classify(x).Length > 0);
   });
  }
  public static string Status(AbilityDefinition d) { return d == null ? "미구현" : Pending.ContainsKey(d.name) && Runnable(d) ? "부분 자동" : Dedicated.Contains(d.name) || Runnable(d) ? "자동" : "수동"; }

  // 배틀 포켓몬의 포텐셜 목록에서 특성 슬롯을 지금 특성(무효화 포함)에 맞춘다
  public static IEnumerable<Potential> UnsuppressedPotentials(Fighter f) {
   if (f == null || f.Data == null) return new Potential[0];
   var ordinary = (f.Data.potentials ?? new Potential[0]).Where(p => p == null || p.id != Slot);
   var definition = AbilityCatalog.Find(AbilityRuntime.Name(f));
   return Runnable(definition) ? ordinary.Concat(ToPotentials(definition)) : ordinary;
  }
  public static void Sync(Fighter f) {
   if (f == null || f.Data == null) return;
   var list = (f.Data.potentials ?? new Potential[0]).Where(p => p == null || p.id != Slot).ToList();
   if (!f.AbilitySuppressed) {
    var d = AbilityCatalog.Find(AbilityRuntime.Name(f));
    if (Runnable(d)) list.AddRange(ToPotentials(d));
   }
   f.Data.potentials = list.ToArray();
   AbilityPhrases.SyncTypes(f);
   ItemRuntime.Sync(f);   // 숙성·플레이트 타입처럼 특성에 따라 소지품 효과·타입이 달라진다
  }
  // 틀깨기: 상대가 기술을 내는 동안 자신의 특성 계속 효과는 무시된다
  public static bool Ignored(Potential p, Fighter owner, Fighter opponent, string sentence = null) {
   return p != null && p.id == Slot && opponent != null && opponent.Conditions.Acting && Mechanics.BypassAbility(opponent)
    && !AbilityCriticalFieldModules.IsCriticalRankZero(sentence) && !AbilityChangeModules.CannotIgnore(owner, opponent);
  }
 }
}
