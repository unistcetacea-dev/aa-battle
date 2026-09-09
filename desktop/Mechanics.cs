using System;
using System.Linq;
using System.Collections.Generic;

namespace AABattle {
 public class ConditionState {
  public int SleepTurns=2,ToxicStage=1,Confusion,Taunt,Reflect,LightScreen,Tailwind,Substitute,ProtectChain;
  public bool Protect,Flinch,Recharge,Seed,Curse,AquaRing,Ingrain,FlashFire,BalloonPopped;
  public ConditionState Copy(){return (ConditionState)MemberwiseClone();}
  public IEnumerable<string> Labels(Fighter f){if(f.Status!="정상")yield return f.Status+(f.Status=="잠듦"?" "+SleepTurns:f.Status=="맹독"?" "+ToxicStage:"");if(Confusion>0)yield return "혼란 "+Confusion;if(Taunt>0)yield return "도발 "+Taunt;if(Protect)yield return "방어";if(Flinch)yield return "풀죽음";if(Recharge)yield return "반동대기";if(Substitute>0)yield return "대타 "+Substitute;if(Seed)yield return "씨뿌리기";if(Curse)yield return "저주";if(AquaRing)yield return "아쿠아링";if(Ingrain)yield return "뿌리박기";if(Reflect>0)yield return "리플렉터 "+Reflect;if(LightScreen>0)yield return "빛의장막 "+LightScreen;if(Tailwind>0)yield return "순풍 "+Tailwind;if(FlashFire)yield return "타오르는불꽃";}
 }
 public static class Mechanics {
  public static readonly string[] Statuses={"정상","화상","독","맹독","마비","잠듦","얼음","동상"};
  public static readonly string[] Weathers={"없음","쾌청","비","모래바람","눈","싸라기눈","큰가뭄","강한 비","난기류"};
  public static readonly string[] Terrains={"없음","그래스필드","일렉트릭필드","사이코필드","미스트필드"};
  public static readonly string[] KnownAbilities={"부유","타오르는불꽃","축전","저수","피뢰침","마중물","전기엔진","초식","방진","날씨부정","에어록","불면","의기양양","유연","면역","수의베일","마그마의무장","마이페이스","매직가드","근성","쓱쓱","엽록소","모래헤치기","눈치우기","틀깨기","터보블레이즈","테라볼티지","배짱","짓궂은마음","방음","방탄","틈새포착","부식"};
  public static readonly string[] KnownItems={"풍선","철구","방진고글","먹다남은음식","검은진흙"};
  public static bool Has(Fighter f,string type){return f.Data.types.Contains(type);}
  public static bool Ability(Fighter f,params string[] names){return names.Contains(f.Data.ability);}
  public static bool Grounded(Fighter f,Rules r){if(r.Gravity>0||f.Data.item=="철구"||f.Conditions.Ingrain)return true;return !Has(f,"비행")&&!Ability(f,"부유")&&!(f.Data.item=="풍선"&&!f.Conditions.BalloonPopped);}
  public static string Weather(Fighter a,Fighter b,Rules r){return (a.HP>0&&Ability(a,"날씨부정","에어록"))||(b.HP>0&&Ability(b,"날씨부정","에어록"))?"없음":r.Weather;}
  public static bool Sunny(string w){return w=="쾌청"||w=="큰가뭄";}
  public static bool Rain(string w){return w=="비"||w=="강한 비";}
  public static bool BypassAbility(Fighter a){return Ability(a,"틀깨기","터보블레이즈","테라볼티지");}
  public static bool Sound(Move m){return (m.tags??new string[0]).Any(t=>t.Contains("소리"))||(m.effect??"").Contains("「소리」");}
  public static bool Powder(Move m){return new[]{"수면가루","독가루","저리가루","버섯포자","분노가루","분진"}.Contains(m.name);}
  public static bool BypassSub(Fighter a,Move m){return Ability(a,"틈새포착")||Sound(m)||m.name=="도발";}
  public static double TypeEffect(Database db,Fighter a,Fighter b,Move m,Rules r){
   if(m.types.Length==0)return 1;string weather=Weather(a,b,r);
   return m.types.Max(t=>b.Data.types.Aggregate(1.0,(v,d)=>{double k=db.chart.ContainsKey(d)&&db.chart[d].ContainsKey(t)?db.chart[d][t]:1;
    if(t=="땅"&&d=="비행"&&Grounded(b,r))k=1;
    if(d=="고스트"&&(t=="노말"||t=="격투")&&Ability(a,"배짱"))k=1;
    if(d=="비행"&&k>1&&weather=="난기류")k=1;return v*k;}));
  }
  public static int Priority(Fighter a,Move m){return m.priority+(m.category=="변화"&&Ability(a,"짓궂은마음")?1:0);}
  public static string Block(Database db,Fighter a,Fighter b,Move m,Rules r){
   if(b.Conditions.Protect&&m.name!="페인트")return "방어가 기술을 막았다";
   if(Powder(m)&&(Has(b,"풀")||!BypassAbility(a)&&Ability(b,"방진")||b.Data.item=="방진고글"))return "풀 타입 / 방진 / 방진고글: 가루 기술 무효";
   if(m.category=="변화"&&Ability(a,"짓궂은마음")&&Has(b,"악"))return "악 타입: 짓궂은마음 변화기 무효";
   if(r.Terrain=="사이코필드"&&Grounded(b,r)&&Priority(a,m)>0)return "사이코필드: 접지 대상에게 우선도 기술 무효";
   string w=Weather(a,b,r);
   if(m.category!="변화"&&((w=="큰가뭄"&&m.types.Contains("물"))||(w=="강한 비"&&m.types.Contains("불꽃"))))return w+": 해당 공격기 실패";
   if(m.types.Contains("땅")&&!Grounded(b,r)&&!Has(b,"비행")&&!(Ability(b,"부유")&&BypassAbility(a)&&!(b.Data.item=="풍선"&&!b.Conditions.BalloonPopped)))return "공중 상태: 땅 기술 무효";
   if(m.types.Contains("땅")&&b.Data.item=="풍선"&&!b.Conditions.BalloonPopped&&!Grounded(b,r))return "풍선: 땅 기술 무효";
   if(!BypassAbility(a)){
    if(m.types.Contains("땅")&&Ability(b,"부유")&&!Grounded(b,r))return "부유: 땅 기술 무효";
    if(m.types.Contains("불꽃")&&Ability(b,"타오르는불꽃"))return "타오르는불꽃";
    if(m.types.Contains("전기")&&Ability(b,"축전","피뢰침","전기엔진"))return b.Data.ability;
    if(m.types.Contains("물")&&Ability(b,"저수","마중물"))return b.Data.ability;
    if(m.types.Contains("풀")&&Ability(b,"초식"))return "초식";
    if(Sound(m)&&Ability(b,"방음"))return "방음: 소리 기술 무효";
    if((m.tags??new string[0]).Any(t=>t.Contains("탄")||t.Contains("볼"))&&Ability(b,"방탄"))return "방탄: 탄/볼 기술 무효";
   }
   if((m.category!="변화"||m.name=="전기자석파")&&TypeEffect(db,a,b,m,r)==0)return "타입 상성에 의한 무효";
   if(m.category=="변화"&&b.Conditions.Substitute>0&&!BypassSub(a,m))return "대타: 변화기 차단";
   if(StatusMoves.ContainsKey(m.name))return StatusBlock(b,a,StatusMoves[m.name],r);
   if(m.name=="씨뿌리기"&&Has(b,"풀"))return "풀 타입: 씨뿌리기 무효";
   if((m.name=="이상한빛"||m.name=="초음파")&&(!BypassAbility(a)&&Ability(b,"마이페이스")||r.Terrain=="미스트필드"&&Grounded(b,r)))return "마이페이스 / 미스트필드: 혼란 무효";
   return "";
  }
  public static string StatusBlock(Fighter target,Fighter source,string status,Rules r){
   if(status=="정상")return "";if(target.HP<=0)return "기절 상태";if(target.Status!="정상")return "이미 상태이상이 있음";
   if(r.Terrain=="미스트필드"&&Grounded(target,r))return "미스트필드: 접지 포켓몬의 상태이상 방지";
   if(status=="잠듦"&&r.Terrain=="일렉트릭필드"&&Grounded(target,r))return "일렉트릭필드: 접지 포켓몬의 잠듦 방지";
   if(status=="화상"&&(Has(target,"불꽃")||(!BypassAbility(source)&&Ability(target,"수의베일"))))return "불꽃 타입 / 수의베일: 화상 무효";
   if((status=="독"||status=="맹독")&&((Has(target,"독")||Has(target,"강철"))&&!Ability(source,"부식")||(!BypassAbility(source)&&Ability(target,"면역"))))return "독·강철 타입 / 면역: 독 무효";
   if(status=="마비"&&(Has(target,"전기")||(!BypassAbility(source)&&Ability(target,"유연"))))return "전기 타입 / 유연: 마비 무효";
   if((status=="얼음"||status=="동상")&&(Has(target,"얼음")||(!BypassAbility(source)&&Ability(target,"마그마의무장"))))return "얼음 타입 / 마그마의무장: 결빙 무효";
   if(status=="얼음"&&Sunny(Weather(target,source,r)))return "쾌청·큰가뭄: 새로운 얼음 상태 방지";
   if(status=="잠듦"&&!BypassAbility(source)&&Ability(target,"불면","의기양양"))return "불면 / 의기양양: 잠듦 무효";return "";
  }
  public static bool ApplyStatus(Fighter target,Fighter source,string status,Rules r,Func<double> rng,List<string> log){string reason=StatusBlock(target,source,status,r);if(reason.Length>0){log.Add(target.Data.name+" — "+reason);return false;}target.Status=status;target.Conditions.ToxicStage=1;target.Conditions.SleepTurns=1+(int)(rng()*3);log.Add(target.Data.name+": "+status);return true;}
  public static double StatFactor(Fighter a,Fighter b,int index,Rules r){double v=1;string w=Weather(a,b,r);if(index==5){if(a.Status=="마비")v*=.5;if(a.Conditions.Tailwind>0)v*=2;if(Rain(w)&&Ability(a,"쓱쓱")||Sunny(w)&&Ability(a,"엽록소")||w=="모래바람"&&Ability(a,"모래헤치기")||(w=="눈"||w=="싸라기눈")&&Ability(a,"눈치우기"))v*=2;}if(index==1&&a.Status!="정상"&&Ability(a,"근성"))v*=1.5;if(index==4&&w=="모래바람"&&Has(a,"바위"))v*=1.5;if(index==2&&w=="눈"&&Has(a,"얼음"))v*=1.5;return v;}
  public static double PowerFactor(Fighter a,Fighter b,Move m,Rules r){double v=1;string w=Weather(a,b,r);if(a.Status=="화상"&&m.category=="물리"&&!Ability(a,"근성")&&m.name!="객기")v*=.5;if(a.Status=="동상"&&m.category=="특수")v*=.5;if(Sunny(w))v*=m.types.Contains("불꽃")?1.5:m.types.Contains("물")?.5:1;if(Rain(w))v*=m.types.Contains("물")?1.5:m.types.Contains("불꽃")?.5:1;
   if(m.name=="객기"&&new[]{"화상","독","맹독","마비"}.Contains(a.Status))v*=2;
   string type=r.Terrain=="그래스필드"?"풀":r.Terrain=="일렉트릭필드"?"전기":r.Terrain=="사이코필드"?"에스퍼":r.SheetTerrain&&r.Terrain=="미스트필드"?"페어리":"";
   if(Grounded(a,r)&&m.types.Contains(type))v*=r.SheetTerrain?1.5:1.3;if(r.Terrain=="미스트필드"&&Grounded(b,r)&&m.types.Contains("드래곤"))v*=.5;if(r.Terrain=="그래스필드"&&Grounded(b,r)&&new[]{"지진","땅고르기","매그니튜드"}.Contains(m.name))v*=.5;if(a.Conditions.FlashFire&&m.types.Contains("불꽃"))v*=1.5;return v;}
  public static double DamageFactor(Fighter a,Fighter b,Move m,Rules r){if(r.Critical||Ability(a,"틈새포착"))return 1;return m.category=="물리"&&b.Conditions.Reflect>0||m.category=="특수"&&b.Conditions.LightScreen>0?.5:1;}
  static int Max(Fighter a,Fighter b,Rules r){return Engine.Stats(a,b.Data.level,r)[0];}
  static void Hurt(Fighter a,int damage,string reason,List<string> log,bool indirect=true){if(a.HP<=0||indirect&&Ability(a,"매직가드"))return;int loss=Math.Min(a.HP,Math.Max(1,damage));a.HP-=loss;log.Add(a.Data.name+" — "+reason+": HP -"+loss);if(a.HP==0)log.Add(a.Data.name+"은 쓰러졌다!");}
  public static void Heal(Fighter a,Fighter b,Rules r,int amount,string reason,List<string> log){if(a.HP<=0)return;int gain=Math.Min(Max(a,b,r)-a.HP,Math.Max(1,amount));if(gain>0){a.HP+=gain;log.Add(a.Data.name+" — "+reason+": HP +"+gain);}}
  public static void Absorb(Fighter a,Fighter b,string reason,Rules r,List<string> log){if(reason=="축전"||reason=="저수")Heal(b,a,r,Max(b,a,r)/4,reason,log);if(reason=="피뢰침"||reason=="마중물")b.Stages[3]=Math.Min(6,b.Stages[3]+1);if(reason=="전기엔진")b.Stages[5]=Math.Min(6,b.Stages[5]+1);if(reason=="초식")b.Stages[1]=Math.Min(6,b.Stages[1]+1);if(reason=="타오르는불꽃")b.Conditions.FlashFire=true;}
  public static bool BeforeMove(Fighter a,Fighter b,Move m,Rules r,Func<double> rng,List<string> log){var c=a.Conditions;
   if(c.Recharge){c.Recharge=false;log.Add(a.Data.name+": 반동으로 행동할 수 없다.");return false;}
   if(a.Status=="잠듦"){if(c.SleepTurns>0){c.SleepTurns--;log.Add(a.Data.name+": 잠들어 있다.");return false;}a.Status="정상";log.Add(a.Data.name+": 잠에서 깨어났다.");}
   if(a.Status=="얼음"){if(rng()<.2||new[]{"열탕","플레어드라이브","화염자동차","성스러운불꽃"}.Contains(m.name)){a.Status="정상";log.Add(a.Data.name+": 얼음이 녹았다.");}else{log.Add(a.Data.name+": 얼어서 움직일 수 없다.");return false;}}
   if(c.Flinch){c.Flinch=false;log.Add(a.Data.name+": 풀죽어서 움직일 수 없다.");return false;}
   if(c.Taunt>0&&m.category=="변화"){log.Add(a.Data.name+": 도발 때문에 변화기를 쓸 수 없다.");return false;}
   if(c.Confusion>0){c.Confusion--;if(rng()<1.0/3){var plain=new Rules{LevelCorrection=r.LevelCorrection,HPCorrection=r.HPCorrection};int attack=Engine.Effective(a,b,1,plain),defense=Engine.Effective(a,b,2,plain);int damage=(int)Math.Floor(((2*a.Data.level+10)/250.0*attack/defense*40+2)*(.85+(int)(rng()*16)/100.0));Hurt(a,damage,"혼란 자해 (위력40)",log,false);return false;}log.Add(a.Data.name+": 혼란을 버티고 행동했다.");}
   if(a.Status=="마비"&&rng()<.25){log.Add(a.Data.name+": 몸이 저려 움직일 수 없다.");return false;}return true;
  }
  public static double Accuracy(Fighter a,Fighter b,Move m,Rules r){string w=Weather(a,b,r);if((m.name=="번개"||m.name=="폭풍")&&Rain(w)||m.name=="눈보라"&&(w=="눈"||w=="싸라기눈"))return 100;if((m.name=="번개"||m.name=="폭풍")&&Sunny(w))return 50;double stage=Engine.Stage(a.AccuracyStage-b.EvasionStage);return Math.Min(100,m.accuracy*(r.Gravity>0?5.0/3:1)*stage*a.AccuracyMultiplier/b.EvasionMultiplier);}
  public static readonly Dictionary<string,string> StatusMoves=new Dictionary<string,string>{{"도깨비불","화상"},{"맹독","맹독"},{"독가루","독"},{"독가스","독"},{"전기자석파","마비"},{"저리가루","마비"},{"뱀눈초리","마비"},{"버섯포자","잠듦"},{"수면가루","잠듦"},{"최면술","잠듦"},{"악마의키스","잠듦"}};
  public static readonly Dictionary<string,string> WeatherMoves=new Dictionary<string,string>{{"쾌청","쾌청"},{"비바라기","비"},{"모래바람","모래바람"},{"설경","눈"},{"싸라기눈","싸라기눈"}};
  public static readonly string[] SelfMoves={"방어","판별","대타출동","자기재생","태만함","날개쉬기","아쿠아링","뿌리박기","리플렉터","빛의장막","순풍","칼춤","고속이동","나쁜음모","명상"};
  public static bool TargetsOpponent(Move m){return m.category!="변화"||!(SelfMoves.Contains(m.name)||WeatherMoves.ContainsKey(m.name)||Terrains.Contains(m.name)||m.name=="트릭룸"||m.name=="중력");}
  public static bool KnownMove(Move m){return m.category!="변화"||StatusMoves.ContainsKey(m.name)||SelfMoves.Contains(m.name)||WeatherMoves.ContainsKey(m.name)||Terrains.Contains(m.name)||new[]{"이상한빛","초음파","씨뿌리기","도발","트릭룸","중력"}.Contains(m.name);}
  public static void StatusMove(Fighter a,Fighter b,Move m,Rules r,Func<double> rng,List<string> log){var c=a.Conditions;int max=Max(a,b,r);
   if(StatusMoves.ContainsKey(m.name)){ApplyStatus(b,a,StatusMoves[m.name],r,rng,log);return;}
   if(WeatherMoves.ContainsKey(m.name)){if(r.Weather=="큰가뭄"||r.Weather=="강한 비"||r.Weather=="난기류"){log.Add("특수 날씨는 일반 날씨 기술로 덮어쓸 수 없다.");return;}r.Weather=WeatherMoves[m.name];r.WeatherTurns=5;log.Add("날씨: "+r.Weather+" (5턴)");return;}
   if(Terrains.Contains(m.name)&&m.name!="없음"){r.Terrain=m.name;r.TerrainTurns=5;log.Add("필드: "+m.name+" (5턴)");return;}
   switch(m.name){
    case "방어":case "판별":if(rng()<Math.Pow(1.0/3,c.ProtectChain)){c.Protect=true;c.ProtectChain++;log.Add(a.Data.name+": 방어 태세!");}else{c.ProtectChain=0;log.Add(a.Data.name+": 연속 방어 실패.");}break;
    case "대타출동":int cost=Math.Max(1,max/4);if(c.Substitute>0||a.HP<=cost){log.Add("대타출동 실패: HP 부족 또는 이미 대타 존재.");break;}a.HP-=cost;c.Substitute=cost;log.Add(a.Data.name+": 대타 HP "+cost);break;
    case "자기재생":case "태만함":Heal(a,b,r,max/2,m.name,log);break;
    case "날개쉬기":log.Add("날개쉬기: 비행 타입 일시 제거까지 포함하므로 현재 수동 판정.");break;
    case "아쿠아링":c.AquaRing=true;log.Add(a.Data.name+": 아쿠아링");break;
    case "뿌리박기":c.Ingrain=true;log.Add(a.Data.name+": 뿌리박기 (접지)");break;
    case "리플렉터":c.Reflect=5;log.Add(a.Data.name+": 리플렉터 5턴");break;
    case "빛의장막":c.LightScreen=5;log.Add(a.Data.name+": 빛의장막 5턴");break;
    case "순풍":c.Tailwind=4;log.Add(a.Data.name+": 순풍 4턴");break;
    case "칼춤":a.Stages[1]=Math.Min(6,a.Stages[1]+2);log.Add(a.Data.name+": 공격 +2");break;
    case "고속이동":a.Stages[5]=Math.Min(6,a.Stages[5]+2);log.Add(a.Data.name+": 속도 +2");break;
    case "나쁜음모":a.Stages[3]=Math.Min(6,a.Stages[3]+2);log.Add(a.Data.name+": 특공 +2");break;
    case "명상":a.Stages[3]=Math.Min(6,a.Stages[3]+1);a.Stages[4]=Math.Min(6,a.Stages[4]+1);log.Add(a.Data.name+": 특공·특방 +1");break;
    case "이상한빛":case "초음파":if(!BypassAbility(a)&&Ability(b,"마이페이스")||r.Terrain=="미스트필드"&&Grounded(b,r))log.Add("혼란 무효: 마이페이스 / 미스트필드");else if(b.Conditions.Confusion==0){b.Conditions.Confusion=1+(int)(rng()*4);log.Add(b.Data.name+": 혼란");}break;
    case "씨뿌리기":if(Has(b,"풀"))log.Add("풀 타입: 씨뿌리기 무효");else{b.Conditions.Seed=true;log.Add(b.Data.name+": 씨뿌리기");}break;
    case "도발":b.Conditions.Taunt=3;log.Add(b.Data.name+": 도발 3턴");break;
    case "트릭룸":r.TrickRoom=r.TrickRoom>0?0:5;log.Add("트릭룸 "+(r.TrickRoom>0?"5턴":"해제"));break;
    case "중력":r.Gravity=5;log.Add("중력 5턴");break;
    default:log.Add(m.name+": 자동 처리 미등록 — 기술 효과를 수동 판정하세요.");break;
   }
  }
  public static void AfterHit(Fighter a,Fighter b,Move m,Rules r,Func<double> rng,List<string> log,bool substitute){
   if(m.name=="파괴광선"||m.name=="기가임팩트")a.Conditions.Recharge=true;
   if(substitute)return;if(b.Data.item=="풍선"&&!b.Conditions.BalloonPopped){b.Conditions.BalloonPopped=true;log.Add(b.Data.name+": 풍선이 터졌다.");}
   if(b.Status=="얼음"&&m.types.Contains("불꽃")){b.Status="정상";log.Add(b.Data.name+": 불꽃 공격으로 얼음이 녹았다.");}
   string status="";double chance=0;
   if(m.name=="화염방사"||m.name=="불대문자"){status="화상";chance=.1;}
   if(m.name=="10만볼트"){status="마비";chance=.1;}
   if(m.name=="번개"){status="마비";chance=.3;}
   if(m.name=="냉동빔"||m.name=="눈보라"){status="얼음";chance=.1;}
   if(m.name=="독찌르기"||m.name=="오물폭탄"){status="독";chance=.3;}
   if(status.Length>0&&rng()<chance)ApplyStatus(b,a,status,r,rng,log);
   if(b.HP>0&&((m.name=="에어슬래시"||m.name=="스톤샤워")&&rng()<.3||m.name=="악의파동"&&rng()<.2)){b.Conditions.Flinch=true;log.Add(b.Data.name+": 풀죽음");}
  }
  public static void EndTurn(Fighter[] f,Rules r,List<string> log){
   string weather=Weather(f[0],f[1],r);
   for(int i=0;i<2;i++){var a=f[i];var b=f[1-i];int max=Max(a,b,r);if(weather=="모래바람"&&!a.Data.types.Any(t=>new[]{"바위","땅","강철"}.Contains(t))&&!Ability(a,"방진","모래헤치기")&&a.Data.item!="방진고글")Hurt(a,max/16,"모래바람",log);if(weather=="싸라기눈"&&!Has(a,"얼음")&&!Ability(a,"방진")&&a.Data.item!="방진고글")Hurt(a,max/16,"싸라기눈",log);}
   for(int i=0;i<2;i++){var a=f[i];var b=f[1-i];var c=a.Conditions;int max=Max(a,b,r);
    if(r.Terrain=="그래스필드"&&Grounded(a,r))Heal(a,b,r,max/16,"그래스필드",log);
    if(a.Data.item=="먹다남은음식")Heal(a,b,r,max/16,"먹다남은음식",log);
    if(a.Data.item=="검은진흙"){if(Has(a,"독"))Heal(a,b,r,max/16,"검은진흙",log);else Hurt(a,max/8,"검은진흙",log);}
    if(c.AquaRing)Heal(a,b,r,max/16,"아쿠아링",log);if(c.Ingrain)Heal(a,b,r,max/16,"뿌리박기",log);
    if(c.Seed&&a.HP>0&&!Ability(a,"매직가드")){int before=a.HP;Hurt(a,max/8,"씨뿌리기",log);if(before>a.HP)Heal(b,a,r,before-a.HP,"씨뿌리기 흡수",log);}
    if(a.Status=="독")Hurt(a,max/8,"독",log);if(a.Status=="맹독"){Hurt(a,Math.Max(1,(int)Math.Floor(max*c.ToxicStage/16.0)),"맹독 "+c.ToxicStage+"/16",log);c.ToxicStage=Math.Min(15,c.ToxicStage+1);}
    if(a.Status=="화상"||a.Status=="동상")Hurt(a,max/16,a.Status,log);if(c.Curse)Hurt(a,max/4,"저주",log);
    c.Protect=false;c.Flinch=false;Tick(ref c.Taunt,"도발",a,log);Tick(ref c.Reflect,"리플렉터",a,log);Tick(ref c.LightScreen,"빛의장막",a,log);Tick(ref c.Tailwind,"순풍",a,log);
   }
   if(r.Weather!="없음"&&r.WeatherTurns>0&&--r.WeatherTurns==0){log.Add(r.Weather+" 종료");r.Weather="없음";}
   if(r.Terrain!="없음"&&r.TerrainTurns>0&&--r.TerrainTurns==0){log.Add(r.Terrain+" 종료");r.Terrain="없음";}
   if(r.TrickRoom>0&&--r.TrickRoom==0)log.Add("트릭룸 종료");if(r.Gravity>0&&--r.Gravity==0)log.Add("중력 종료");
  }
  static void Tick(ref int value,string name,Fighter a,List<string> log){if(value>0&&--value==0)log.Add(a.Data.name+": "+name+" 종료");}
 }
}
