using System;
using System.Linq;
using System.Collections.Generic;

namespace AABattle {
 // 소지품 종류별로 값이 달라지는 기술(내던지기·자연의은혜·배의뭉치·심판의뭉치 계열)의 실기 표를 한곳에 둔다.
 // 소지품 도감(아바돈 소지품)에는 이 값이 없어서 Showdown 표를 가져왔다(출처: 실기, 문서에 값이 없을 때만 쓴다).

 public sealed class FlingEntry {
  public int Power;
  public string[] Effects = new string[0];   // 맞은 상대에게 거는 표준 문장
  public bool EatsBerry;                      // 상대가 그 나무열매를 먹는다
 }

 public static class FlingTable {
  public const int DefaultPower = 30;   // 표에 없는 소지품(오리지널 소지품 등)
  static readonly Dictionary<string, int> Powers = new Dictionary<string, int> {
   { "먹다남은음식", 10 }, { "생명의구슬", 30 }, { "기합의띠", 10 }, { "레드카드", 10 }, { "울퉁불퉁멧", 30 }, { "탈출버튼", 30 }, { "탈출팩", 50 },
   { "구애머리띠", 10 }, { "구애안경", 10 }, { "구애스카프", 10 }, { "플레이트", 90 }, { "○주얼", 30 },
   { "실크스카프", 10 }, { "부드러운모래", 10 }, { "은빛가루", 10 }, { "예리한부리", 50 }, { "딱딱한돌", 100 }, { "독바늘", 70 }, { "용의이빨", 70 },
   { "왕의징표석", 30 }, { "아름다운허물", 10 }, { "반짝가루", 10 }, { "방진고글", 80 }, { "검은진흙", 30 }, { "조개껍질방울", 30 }, { "큰뿌리", 10 },
   { "방호패드", 30 }, { "주눅구슬", 30 }, { "조임밴드", 30 }, { "메트로놈", 30 }, { "박식안경", 10 }, { "힘의머리띠", 10 }, { "초점렌즈", 30 },
   { "광각렌즈", 10 }, { "포커스렌즈", 10 }, { "기합의머리띠", 10 }, { "선제공격손톱", 80 }, { "빨간실", 10 }, { "통굽부츠", 80 }, { "만능우산", 60 },
   { "허탕보험", 80 }, { "룸서비스", 100 }, { "목스프레이", 30 }, { "멘탈허브", 10 }, { "파워풀허브", 10 }, { "하양허브", 10 },
   { "뜨거운바위", 60 }, { "보송보송바위", 60 }, { "차가운바위", 40 }, { "축축한바위", 60 }, { "빛의점토", 30 }, { "끈기갈고리손톱", 90 }, { "그라운드코트", 60 },
   { "끈적끈적바늘", 80 }, { "검은철구", 130 }, { "겨냥표적", 10 }, { "느림보꼬리", 10 }, { "풍선", 10 },
   { "그래스시드", 10 }, { "미스트시드", 10 }, { "사이코시드", 10 }, { "일렉트릭시드", 10 },
   { "화염구슬", 30 }, { "독독구슬", 30 }, { "전기구슬", 30 }, { "날카로운이빨", 30 }
  };
  static readonly Dictionary<string, string> Statuses = new Dictionary<string, string> {
   { "왕의징표석", "상대를 「풀죽음」 상태로 한다." }, { "날카로운이빨", "상대를 「풀죽음」 상태로 한다." },
   { "독바늘", "상대를 「독」 상태로 한다." }, { "독독구슬", "상대를 「맹독」 상태로 한다." },
   { "화염구슬", "상대를 「화상」 상태로 한다." }, { "전기구슬", "상대를 「마비」 상태로 한다." }
  };

  // 던질 수 있는 소지품의 위력과 효과. 나무열매는 위력 10에 상대가 먹는다
  public static FlingEntry Find(string item) {
   if (string.IsNullOrEmpty(item)) return null;
   var def = ItemCatalog.Find(item);
   string name = def == null ? item : def.Name;
   var entry = new FlingEntry();
   int power;
   if (ItemRuntime.IsBerry(item)) { entry.Power = 10; entry.EatsBerry = true; return entry; }
   if (name.EndsWith("플레이트")) power = 90;
   else if (name.EndsWith("주얼")) power = 30;
   else if (!Powers.TryGetValue(name, out power)) power = DefaultPower;
   entry.Power = power;
   string status;
   if (Statuses.TryGetValue(name, out status)) entry.Effects = new[] { status };
   return entry;
  }
 }

 public static class NaturalGiftTable {
  // 나무열매 → 타입·위력. 반감 열매·상태이상 치유 열매 80, 능력상승·기타 열매 100(Showdown)
  static readonly Dictionary<string, KeyValuePair<string, int>> Table = Build(
   "버치열매:불꽃:80", "유루열매:물:80", "복슝열매:전기:80", "복분열매:풀:80", "배리열매:얼음:80", "시몬열매:땅:80", "리샘열매:비행:80", "자뭉열매:에스퍼:80",
   "오카열매:불꽃:80", "꼬시개열매:물:80", "초나열매:전기:80", "린드열매:풀:80", "플카열매:얼음:80", "로플열매:격투:80", "으름열매:독:80", "슈캐열매:땅:80",
   "바코열매:비행:80", "야파열매:에스퍼:80", "리체열매:벌레:80", "루미열매:바위:80", "수불열매:고스트:80", "하반열매:드래곤:80", "마코열매:악:80", "바리비열매:강철:80",
   "로셀열매:페어리:80", "카리열매:노말:80",
   "치리열매:풀:100", "용아열매:얼음:100", "야타비열매:독:100", "규살열매:땅:100", "캄라열매:격투:100", "미클열매:바위:100", "랑사열매:비행:100", "스타열매:에스퍼:100",
   "애슈열매:고스트:100", "악키열매:페어리:100", "타라프열매:악:100", "의문열매:벌레:100", "자보열매:드래곤:100", "애터열매:악:100");
  public const string FallbackType = "노말";
  public const int FallbackPower = 80;   // 표에 없는 나무열매(보코·마쥬 등 오리지널)

  static Dictionary<string, KeyValuePair<string, int>> Build(params string[] rows) {
   var map = new Dictionary<string, KeyValuePair<string, int>>();
   foreach (string row in rows) {
    var p = row.Split(':');
    map[p[0]] = new KeyValuePair<string, int>(p[1], int.Parse(p[2]));
   }
   return map;
  }
  public static bool Find(string berry, out string type, out int power) {
   KeyValuePair<string, int> v;
   if (berry != null && Table.TryGetValue(berry, out v)) { type = v.Key; power = v.Value; return true; }
   type = FallbackType; power = FallbackPower;
   return false;
  }
 }

 // 지닌 소지품에 따라 기술의 타입이 변하는 기술(심판의뭉치=플레이트, 멀티어택=메모리, 테크노버스터=카세트)
 public static class HeldTypeTable {
  static readonly Dictionary<string, string> Cassettes = new Dictionary<string, string> {
   { "아쿠아카세트", "물" }, { "이나즈마카세트", "전기" }, { "블레이즈카세트", "불꽃" }, { "프리즈카세트", "얼음" }
  };
  // kind: 플레이트 · 메모리 · 카세트. 해당하는 소지품이 없으면 null
  public static string TypeOf(string kind, string item) {
   if (string.IsNullOrEmpty(item)) return null;
   if (kind == "카세트") { string t; return Cassettes.TryGetValue(item, out t) ? t : null; }
   var def = ItemCatalog.Find(item);
   if (kind == "플레이트") return def != null && def.Name.EndsWith("플레이트") && def.Type.Length > 0 ? def.Type : null;
   if (item.EndsWith("메모리")) {
    string type = item.Substring(0, item.Length - 3);
    if (MoveCondition.TypeNames.Contains(type)) return type;
   }
   return null;
  }
 }
}
