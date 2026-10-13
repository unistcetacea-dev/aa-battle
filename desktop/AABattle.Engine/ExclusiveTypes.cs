using System;
using System.Linq;
using System.Collections.Generic;

namespace AABattle {
 // 오레마스 「전용 포텐셜」 유형 템플릿. 포켓몬 편집의 슬롯 「전용」에서 이름 선택 「범용」을 고르면 이 목록이 나온다.
 // 유형 이름은 포텐셜 이름이 아니다. 유형은 사용 제한·발동 방식·트리거·효과를 불러오는 템플릿일 뿐이며 저장되지 않는다.
 // 원문: research/potentials/oremas-exclusive-potentials.txt, 판정 표: research/potentials/EXCLUSIVE_TYPES.md
 public sealed class ExclusiveType {
  public string Name = "", Uses = "", Activation = "자동", Trigger = "", Effect = "";
  // 습득 조건·해설 등 효과가 아닌 안내(편집 창 도움말 줄에만 쓴다)
  public string Help = "";
  // 구현하지 않은 부분의 사유(비어 있으면 모든 문장이 실행기로 읽힌다)
  public string Pending = "";
  // 자리 표시를 채워 실행기로 읽히는지 확인할 때 쓰는 값(검사용)
  public string Sample = "";

  // 효과 문장의 자리 표시(「○」「○○」)가 남아 있는지
  public bool HasPlaceholder { get { return ExclusiveTypes.HasPlaceholder(Trigger) || ExclusiveTypes.HasPlaceholder(Effect); } }
 }

 public static partial class ExclusiveTypes {
  public const string TemplateName = "범용";
  public const string MoveUse = "자신이 기술을 내보낼 때";
  public const string AttackMoveUse = "자신이 「공격」 기술을 내보낼 때";

  public static bool HasPlaceholder(string text) { return (text ?? "").Contains("○"); }

  static string Lines(params string[] lines) { return string.Join("\r\n", lines); }

  // 방호 무효화 4문장(삼각 계열): 방어 등의 기술·방호 특성·방호 포텐셜(포켓몬·트레이너)
  static readonly string[] Triangle = {
   "자신의 기술이 상대의 「방어」 기술을 관통한다.",
   "자신의 기술은 상대의 〔방호 특성〕을 관통한다.",
   "자신의 기술이 상대의 방호 포텐셜을 무시한다.",
   "자신의 기술이 상대의 트레이너 방호 포텐셜을 무시한다."
  };

  public static readonly ExclusiveType[] All = Build();

  // 자리 표시(○)를 채울 값: 검사에서 템플릿이 실행기로 읽히는지 볼 때 쓴다
  static readonly Dictionary<string, string> Samples = new Dictionary<string, string> {
   { "대삼각", "불꽃" }, { "강화관통", "불꽃" }, { "강행돌파", "불꽃" }, { "고속설치", "스텔스록" }, { "확장추미", "몸통박치기" },
   { "확장삼각", "몸통박치기" }, { "마검", "몸통박치기" }, { "추가반동", "파괴광선" }, { "아라가미탄", "불꽃" }
  };
  public static string Filled(ExclusiveType type, string text) {
   string sample;
   if (!Samples.TryGetValue(type.Name, out sample)) return text;
   return text.Replace("○○", sample).Replace("○", sample);
  }

  public static ExclusiveType Find(string name) { return All.FirstOrDefault(x => x.Name == name); }
  public static string[] Names { get { return All.Select(x => x.Name).ToArray(); } }

  static ExclusiveType T(string name, string uses, string activation, string trigger, string effect, string help = "", string pending = "") {
   return new ExclusiveType { Name = name, Uses = uses, Activation = activation, Trigger = trigger, Effect = effect, Help = help, Pending = pending };
  }

  static ExclusiveType[] Build() {
   const string oneShot = "1/시";
   return new[] {
    T("필중추미", oneShot, "선행", MoveUse,
      Lines("자신의 기술에 「추격」 효과를 부여한다.", "자신의 기술은 필중이다."),
      "어태커의 가장 흔한 전용. 「필중기술」 이외에 필중을 얻기 어렵고 『돌아와！』를 무효화할 수 있다. 따라가때리기의 2배 효과는 없다."),
    T("강화추미", oneShot, "선행", MoveUse,
      Lines("자신의 기술에 「추격」 효과를 부여한다.", "자신의 기술은 필중이다.", "자신이 「추격」 효과로 주는 대미지를 강화(2배)한다."),
      "『천부의 재능』과 「능력형」 트레이너만 가능한 『필중추미』의 강화형."),
    T("확장추미", oneShot, "선행", MoveUse,
      "자신이 「기능확장:○○」를 내보낼 수 있다.",
      "○○ = ○/○/200/─/단일/±0/×/「필중」기술. 「추격」기술. 「추격」 효과의 발동 시 기술의 데미지를 2배로 한다. 기술 데이터에 이 기술을 따로 만든다. 스레와 어장에 따라 위력이 조정된다."),
    T("삼각효과", oneShot, "선행", MoveUse, Lines(Triangle),
      "대타출동·리플렉터는 적용 외. 공격 또는 특공이 「AA」 이상인 포켓몬만 가능(「A」는 제약·열화)."),
    T("대삼각", oneShot, "선행", MoveUse,
      Lines(new[] { "자신의 「○」 기술의 위력을 강화(1.5배)한다." }.Concat(Triangle).ToArray()),
      "타입 상성으로 무효화할 수 있는 타입의 기술만 가능. 기본적으로 『천부의 재능』만."),
    T("확장삼각", oneShot, "선행", MoveUse,
      Lines(new[] { "자신이 「기능확장:○○」를 내보낼 수 있다." }.Concat(Triangle).ToArray()),
      "별명 『암반나누기』. 「오의」급 기능확장과 『삼각효과』가 합쳐진 형태."),
    T("마검", oneShot, "선행", MoveUse,
      "자신이 「기능확장:○○」를 내보낼 수 있다.",
      "통상 교대로 필드에 나온 턴의 가장 마지막에도 발동할 수 있다(『와일드』라고도 부른다). 보통 『돌아와！』와 함께 쓴다.",
      "통상교대로 나온 T의 최후 트리거가 아직 배틀 실행 경로에 연결되지 않았다."),
    T("선행유턴", oneShot, "선행", AttackMoveUse,
      Lines("자신의 「공격」 기술에 「유턴」 효과를 부여한다.", "자신의 기술 우선도를 1 올린다."), ""),
    T("귀신유턴", oneShot, "선행", AttackMoveUse,
      Lines("자신의 「공격」 기술에 「유턴」 효과를 부여한다.", "자신의 기술 우선도를 2 올린다."),
      "「능력형」과 『천부의 재능』만 익히는 『선행유턴』의 강화계."),
    T("버서크", oneShot, "선행", "자신의 공격이 명중했을 때",
      Lines("자신의 현재 HP의 1/2을 소비한다.", "이때 가장 먼저 같은 기술에 의한 추가행동을 얻는다."),
      "체력이 「1」일 때는 사용할 수 없다."),
    T("영격", oneShot, "선행", "상대가 「공격」 기술을 내보낼 때",
      Lines("상대의 「공격」 기술을 1번만 회피한다.", ""),
      "우선도 -7. 회피 성공 후 효과는 아래 빈 효과 칸에서 선택한다. 방어 또는 특방이 「D」 이하인 포켓몬만 습득. 양쪽이 「D」 이하면 공격기술, 한쪽만이면 해당 분류의 기술만 회피한다."),
    T("페이스메이커", "", "자동", "자신이 장소를 떠날 때",
      "아군의 『에이스』를 발동시킨다.",
      "『어시스트』(구 『베테랑』)만 전용으로 가질 수 있다. 「특성：무드메이커」 포켓몬이 습득할 수 있다는 이야기가 있다.",
      "역할 포텐셜을 강제로 발동시키는 실행기가 아직 연결되지 않았다."),
    T("텔레포터", "", "자동", "",
      "아군과 임의교대할 수 있다.",
      "필드에 있는 아군과 교대해 나오는 능력의 총칭. 조건은 변형마다 다르다(트리거 칸을 직접 채운다)."),
    T("스토킹", "", "자동", "상대의 『에이스』가 장소에 나왔을 때",
      "아군과 임의교대할 수 있다.",
      "주로 『킬러』가 가진다.",
      "대기 포켓몬이 상대 역할 등장 사건을 받아 교대하는 경로가 아직 연결되지 않았다."),
    T("역선회", "", "자동", "장소에 나와 「2」T째 종료 시",
      "아군과 임의교대할 수 있다.",
      "『텔레포터』의 하위. 교대 조건에 따라 『역윤회』『역장전』『역둔주』 등의 변형이 있다."),
    T("강화관통", oneShot, "선행", MoveUse,
      Lines("자신의 「○」 기술의 위력을 강화(1.5배)한다.", "자신의 기술이 상대의 「방어」 기술을 관통한다."), ""),
    T("강행돌파", oneShot, "선행", MoveUse,
      Lines("자신의 「○」 기술의 위력을 강화(1.5배)한다.", "상대의 모든 상태변화를 해제한다."),
      "『강화관통』의 강화형. 「방어」도 「상태변화」의 일종이다. 일부 스레에서 실험적으로 등장."),
    T("추가반동", "", "선행", MoveUse,
      "가장 마지막에 「기술:○○」 기술에 의한 추가행동을 얻는다.",
      "○○ 자리에는 다음 T 행동불능이 되는 종류의 기술(파괴광선 등)이 들어간다."),
    T("메가진화", "", "자동", "T 개시 시",
      "메가진화한다.",
      "일부 『올드 타입』만 가질 수 있다. 종족치 증강·종족 포텐셜 일부 변화, 분류가 『메가진화』로, 속별이 『~나이트』 계통으로 바뀐다.",
      "폼 변경 구조가 아직 없어 구현하지 않았다."),
    T("삼점방호", "", "자동", "아군과 교대해 장소에 나온 T의 종료 시까지",
      "상대의 랭크 상승·강화·상태변화에 의한 위력 상승을 무시한다.",
      "『삼각효과』에서 따온 이름. 교대 턴 지속과 위력 상승 무시는 아직 실행기로 연결되지 않았다.",
      "위력 계산과 턴 종료 해제 경로가 미구현."),
    T("강체술", oneShot, "자동", "자신의 공격이 명중하면",
      "자신이 주는 대미지를 강화(2배)한다.",
      "공격 명중 시 사용한다. 격투 타입이 주로 쓰며 헛손질이 없어 지시가 낮은 트레이너가 쓴다."),
    T("고속설치", "", "자동", "선발 또는 죽어내밀기로 장소에 나올 때",
      Lines("자신의 체력을 최대 HP의 1/4 소비한다.", "적진에 「○○」을 설치할 수 있다."),
      "오레마스부터 있던 전용이며 정식 유형 이름은 Try에서 붙었다."),
    T("범프업", "", "자동", "장소에 나올 때",
      "자신의 전종족치를 1랭크 올린다.",
      "트레이너 지시 랭크 하락 디메리트는 적용하지 않는다."),
    T("속사", "", "자동", Lines(MoveUse, "장소에 나와 아직 기술을 내보내지 않았을 때"),
      "자신의 기술 우선도를 1 올린다.",
      "트레이너의 「능력」이 높으면 우선도 증가량이 +2가 된다."),
    T("아라가미탄", oneShot, "선행", MoveUse,
      "자신이 「기능확장:아라가미탄(○)」를 내보낼 수 있다.",
      "아라가미탄(○) = 물리/○/150/─/단일/±0/×/「필중」기술. 「탄」「볼」기술. 상대의 「방어」 등의 기술을 관통한다. 기술 데이터에 따로 만든다. 「아라가미」 종이 주로 쓴다.")
   };
  }
 }
}
