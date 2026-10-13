using System;
using System.Linq;
using System.Collections.Generic;

namespace AABattle {
 public static partial class Mechanics {
  public static readonly string[] Statuses={"정상","화상","독","맹독","마비","잠듦","얼음","동상"};
  public static readonly string[] Weathers={"없음","쾌청","비","모래바람","눈","싸라기눈","큰가뭄","강한 비","난기류"};
  public static readonly string[] Terrains={"없음","그래스필드","일렉트릭필드","사이코필드","미스트필드"};
  public static readonly string[] KnownAbilities={"부유","타오르는불꽃","축전","저수","피뢰침","마중물","전기엔진","초식","방진","날씨부정","에어록","불면","의기양양","유연","면역","수의베일","마그마의무장","마이페이스","매직가드","근성","쓱쓱","엽록소","모래헤치기","눈치우기","틀깨기","터보블레이즈","테라볼티지","배짱","짓궂은마음","방음","방탄","틈새포착","부식"};
  public static readonly string[] KnownItems={"풍선","철구","방진고글","먹다남은음식","검은진흙","통굽부츠"};
  public static bool Has(Fighter f,string type){return f.Data.types.Contains(type);}
  public static bool Ability(Fighter f,params string[] names){return f!=null&&names.Any(x=>AbilityRuntime.Has(f,x));}
  public static bool Grounded(Fighter f,Rules r){if(r.Gravity>0||ItemRuntime.Holds(f,"검은철구","철구")||f.Conditions.Ingrain)return true;return !Has(f,"비행")&&!(ItemRuntime.Name(f)=="풍선"&&!f.Conditions.BalloonPopped)&&!PotentialModifiers.Floating(f,r)&&!StateTicks.Floating(f);}
  public static string Weather(Fighter a,Fighter b,Rules r){return AbilityPhrases.NegatesWeather(a,b,r)?"없음":r.Weather;}
  public static bool Sunny(string w){return w=="쾌청"||w=="큰가뭄";}
  public static bool Rain(string w){return w=="비"||w=="강한 비";}
  public static bool BypassAbility(Fighter a){return a!=null&&(a.Conditions.PiercingAbility||AbilityPhrases.PiercesAbility(a));}
  public static bool Sound(Move m){return (m.tags??new string[0]).Any(t=>t.Contains("소리"))||(m.effect??"").Contains("「소리」");}
  public static bool Powder(Move m){return new[]{"수면가루","독가루","저리가루","버섯포자","분노가루","분진"}.Contains(m.name);}
  public static bool BypassSub(Fighter a,Move m){return Sound(m)||m.name=="도발"||m.name=="앙코르";}
  public static double TypeEffect(Database db,Fighter a,Fighter b,Move m,Rules r){
   if(m.types.Length==0)return 1;string weather=Weather(a,b,r);
   var p=TypeRules.For(a,b,m,r);
   if(p.IgnoreAll)return TypeRules.Adjust(a,b,m,r,1);
   var each=m.types.Concat(p.Added).Distinct().Select(t=>b.Data.types.Aggregate(1.0,(v,d)=>{double k=db.chart.ContainsKey(d)&&db.chart[d].ContainsKey(t)?db.chart[d][t]:1;
    k=TypeRules.Matchup(p,k);
    if(t=="땅"&&d=="비행"&&Grounded(b,r))k=1;
    if(k==0&&p.PierceImmuneTypes.Contains(d))k=1;
    if(d=="비행"&&k>1&&weather=="난기류")k=1;k=MoveModules.TypeOverride(a,m,d,k);
    // 포텐셜: 상성 무효 관통(0→1), 타입 내성 관통(반감·무효→1, 사용자 결정)
    if(k==0&&p.PierceImmune)k=1;
    if(k<1&&(p.PierceAll||p.PierceTypes.Contains(d)))k=1;
    return v*k;})).ToArray();
   // 확정 30: 복수 타입 기술은 각 타입 배율의 곱. 타입 추가·유리한 상성이면 가장 유리한 타입 하나
   double type=p.Favorable?each.Max():each.Aggregate(1.0,(x,y)=>x*y);
   return TypeRules.Adjust(a,b,m,r,type);
  }
  public static int Priority(Fighter a,Move m){return m.priority+AbilityPhrases.PriorityBonus(a,m);}
  public static int Priority(Fighter a,Move m,Fighter opponent){return Priority(a,m)+PriorityEffectRuntime.ForMove(a,opponent,null,null,null);}
  public static int Priority(Fighter a,Move m,Fighter opponent,TrainerRecord ownTrainer,TrainerRecord opponentTrainer){return Priority(a,m)+PriorityEffectRuntime.ForMove(a,opponent,ownTrainer,opponentTrainer,null);}
  public static string Block(Database db,Fighter a,Fighter b,Move m,Rules r){
   if((ProtectModules.Blocks(db,b,m)||StateTicks.QuickGuarded(a,b,m,r))&&!AbilityPhrases.PiercesProtect(a,b,m,r))return ProtectModules.Blocked;if(m.category=="변화"&&b.Conditions.MagicCoat&&!a.Conditions.MagicCoat)return StateTicks.Reflected;if(Ohko(m)){if(a.Data.level<b.Data.level)return "일격필살 실패: 대상 레벨이 더 높다";if(!BypassAbility(a)&&AbilityPhrases.BlocksOhko(b,a))return (AbilityRuntime.Name(b).Length>0?"〔"+AbilityRuntime.Name(b)+"〕":"특성/포텐셜")+": 일격필살 무효";}
   if(Powder(m)&&(Has(b,"풀")||ItemRuntime.Name(b)=="방진고글"))return "풀 타입 / 방진고글: 가루 기술 무효";
   if(r.Terrain=="사이코필드"&&Grounded(b,r)&&Priority(a,m,b)>0)return "사이코필드: 접지 대상에게 우선도 기술 무효";
   string w=Weather(a,b,r);
   if(m.category!="변화"&&((w=="큰가뭄"&&m.types.Contains("물"))||(w=="강한 비"&&m.types.Contains("불꽃"))))return w+": 해당 공격기 실패";
   if(m.types.Contains("땅")&&!Grounded(b,r)&&!Has(b,"비행")&&!AbilityMoveModules.HitsFloating(a,b,m,r)&&!(AbilityPhrases.FloatsFromAbility(b)&&BypassAbility(a)&&!(ItemRuntime.Name(b)=="풍선"&&!b.Conditions.BalloonPopped)))return "공중 상태: 땅 기술 무효";
   if(m.types.Contains("땅")&&ItemRuntime.Name(b)=="풍선"&&!b.Conditions.BalloonPopped&&!Grounded(b,r))return "풍선: 땅 기술 무효";
   if((m.category!="변화"||m.name=="전기자석파")&&TypeEffect(db,a,b,m,r)==0)return "타입 상성에 의한 무효";
   if(m.category=="변화"&&b.Conditions.Substitute>0&&!BypassSub(a,m))return "대타: 변화기 차단";
   if(StatusMoves.ContainsKey(m.name))return StatusBlock(b,a,StatusMoves[m.name],r);
   if(m.name=="씨뿌리기"&&Has(b,"풀"))return "풀 타입: 씨뿌리기 무효";
   if((m.name=="이상한빛"||m.name=="초음파")&&(!BypassAbility(a)&&PotentialModifiers.StatusImmune(b,a,"혼란",r)||r.Terrain=="미스트필드"&&Grounded(b,r)))return "특성/포텐셜 / 미스트필드: 혼란 무효";
   return "";
  }
  public static bool DefensiveNullification(Move move,string reason){if(move==null||move.category=="변화"||string.IsNullOrEmpty(reason)||reason.StartsWith("방어")||reason.StartsWith("대타"))return false;return new[]{"타입 상성","타오르는불꽃","축전","저수","피뢰침","마중물","전기엔진","초식","부유","풍선","방음","방탄","방호 포텐셜"}.Any(reason.Contains);}
  public static string StatusBlock(Fighter target,Fighter source,string status,Rules r,bool fromRest=false){if(StateTicks.Guarded(target,source,r))return "신비의부적";if(!BypassAbility(source)&&PotentialModifiers.StatusImmune(target,source,status,r))return "특성/포텐셜: 「"+status+"」상태가 되지 않는다";
   if(status=="정상")return "";if(target.HP<=0)return "기절 상태";if(target.Status!="정상")return "이미 상태이상이 있음";if(status=="잠듦"&&!fromRest){string sleepBlock=SleepBlock(target,r);if(sleepBlock.Length>0)return sleepBlock;}
   if(!Object.ReferenceEquals(target,source)&&PrivilegeRules.Has(target,"왕자의 특권"))return "왕자의 특권: 상대로부터의 상태이상 무효";
   if(r.Terrain=="미스트필드"&&Grounded(target,r))return "미스트필드: 접지 포켓몬의 상태이상 방지";
   if(status=="잠듦"&&r.Terrain=="일렉트릭필드"&&Grounded(target,r))return "일렉트릭필드: 접지 포켓몬의 잠듦 방지";
   if(status=="화상"&&Has(target,"불꽃"))return "불꽃 타입: 화상 무효";
   if((status=="독"||status=="맹독")&&(Has(target,"독")||Has(target,"강철"))&&!AbilityPhrases.PiercesStatusType(source,target,status,r))return "독·강철 타입: 독 무효";
   if(status=="마비"&&Has(target,"전기"))return "전기 타입: 마비 무효";
   if((status=="얼음"||status=="동상")&&Has(target,"얼음"))return "얼음 타입: 결빙 무효";
   if(status=="얼음"&&Sunny(Weather(target,source,r)))return "쾌청·큰가뭄: 새로운 얼음 상태 방지";
   return "";
  }
  public static bool ApplyStatus(Fighter target,Fighter source,string status,Rules r,Func<double> rng,List<string> log,bool fromRest=false,bool ignoreImmunity=false){string reason=!ignoreImmunity?StatusBlock(target,source,status,r,fromRest):target.Status!="정상"?"이미 상태이상이 있다.":status=="잠듦"?SleepBlock(target,r):"";if(reason.Length>0){log.Add(target.Data.name+" — "+reason);return false;}target.Status=status;target.Conditions.ToxicStage=1;if(status=="잠듦"){target.Conditions.SleepTurns=AbilityStateModules.SleepTurns(target,fromRest?2:SleepDuration(rng),r);target.Conditions.SleepStart=target.Conditions.SleepTurns;target.Conditions.SleepFromRest=fromRest;}log.Add(target.Data.name+": "+status);return true;}
  public static double StatFactor(Fighter a,Fighter b,int index,Rules r,int side=-1){double v=1;string w=Weather(a,b,r);if(index==5){if(a.Status=="마비")v*=.5;if(side>=0&&side<2&&r.Sides[side].Tailwind>0)v*=2;}if(index==4&&w=="모래바람"&&Has(a,"바위"))v*=1.5;if(index==2&&w=="눈"&&Has(a,"얼음"))v*=1.5;return v;}
  public static double PowerFactor(Fighter a,Fighter b,Move m,Rules r){double v=1;string w=Weather(a,b,r);if(a.Status=="화상"&&m.category=="물리"&&!AbilityPhrases.IgnoresStatusPowerPenalty(a,"화상")&&m.name!="객기")v*=.5;if(a.Status=="동상"&&m.category=="특수"&&!AbilityPhrases.IgnoresStatusPowerPenalty(a,"동상"))v*=.5;bool umbrella=(Sunny(w)||Rain(w))&&(MiscModules.IgnoresWeather(a,w,r)||MiscModules.IgnoresWeather(b,w,r));if(!umbrella&&Sunny(w))v*=m.types.Contains("불꽃")?1.5:m.types.Contains("물")?.5:1;if(!umbrella&&Rain(w))v*=m.types.Contains("물")?1.5:m.types.Contains("불꽃")?.5:1;
   if(IsRollout(m)&&a.Conditions.RolloutMove==m.name)v*=Math.Pow(2,Math.Max(0,Math.Min(4,a.Conditions.RolloutCount)));v*=ChargePower(a,m);v*=MoveModules.PowerFactor(a,b,m,r);if(a.Conditions.HelpingHand)v*=1.5;
   if(m.name=="객기"&&new[]{"화상","독","맹독","마비"}.Contains(a.Status))v*=2;
   string type=r.Terrain=="그래스필드"?"풀":r.Terrain=="일렉트릭필드"?"전기":r.Terrain=="사이코필드"?"에스퍼":r.SheetTerrain&&r.Terrain=="미스트필드"?"페어리":"";
   if(Grounded(a,r)&&m.types.Contains(type))v*=r.SheetTerrain?1.5:1.3;if(r.Terrain=="미스트필드"&&Grounded(b,r)&&m.types.Contains("드래곤"))v*=.5;if(r.Terrain=="그래스필드"&&Grounded(b,r)&&new[]{"지진","땅고르기","매그니튜드"}.Contains(m.name))v*=.5;if(a.Conditions.FlashFire&&m.types.Contains("불꽃"))v*=1.5;return v;}
  public static bool IsDelayedMove(Move m){return m!=null&&(m.tags??new string[0]).Contains("미래예지");}
  public static double DelayedDamageFactor(Fighter target,Move m){return PrivilegeRules.ContractType(target).Length>0&&(m.types??new string[0]).Contains(PrivilegeRules.ContractType(target))?.5:1;}
  public static Fighter DelayedSource(DelayedAttackState delayed){return new Fighter(new Pokemon{id="delayed-source",name=delayed.SourceName,level=delayed.SourceLevel,types=(delayed.SourceTypes??new string[0]).ToArray(),moves=new string[0],@base=new int[6],iv=new int[6],potentials=new Potential[0]});}
  public static void ScheduleDelayedMove(Fighter source,Fighter target,Move move,Rules r,int sourceSide,List<string> log){var delayed=new DelayedAttackState{Move=new Move{name=move.name,category=move.category,effect=move.effect,attack=move.attack,defense=move.defense,types=(move.types??new string[0]).ToArray(),tags=(move.tags??new string[0]).ToArray(),power=move.power*ChargePower(source,move)*(source.Conditions.HelpingHand?1.5:1),accuracy=move.accuracy,priority=move.priority},SourceName=source.Data.name,SourceSide=sourceSide,SourceLevel=source.Data.level,SourceSpecialAttack=Engine.Stats(source,target.Data.level,r)[move.category=="물리"?1:3],SourceTypes=(source.Data.types??new string[0]).ToArray(),TurnsRemaining=2};if(target.Conditions.DelayedAttacks==null)target.Conditions.DelayedAttacks=new List<DelayedAttackState>();target.Conditions.DelayedAttacks.Add(delayed);log.Add(target.Data.name+": "+move.name+" 상태 — 다음 T의 최후에 판정 (방어 관통)");}
  public static void ResolveDelayedMoves(Database db,Fighter[] fighters,Rules r,Func<double> rng,List<string> log,Action<int,DelayedAttackState,int,bool> onDamage=null){for(int side=0;side<2;side++){var target=fighters[side];var opponent=fighters[1-side];var pending=(target.Conditions.DelayedAttacks??new List<DelayedAttackState>()).Where(x=>x!=null).ToArray();target.Conditions.DelayedAttacks.Clear();foreach(var delayed in pending){if(delayed.TurnsRemaining>1){delayed.TurnsRemaining--;target.Conditions.DelayedAttacks.Add(delayed);continue;}if(target.HP<=0)continue;double accuracy=DelayedAccuracy(target,delayed,r);if((rng==null?0:rng()*100)>=accuracy){log.Add(target.Data.name+"의 "+delayed.Move.name+"! 그러나 최후 명중판정에서 빗나갔다!");continue;}var damage=Engine.CalculateDelayed(db,target,opponent,delayed,r);if(damage.Type==0){log.Add(target.Data.name+"의 "+delayed.Move.name+" — 타입 상성으로 무효");continue;}if(!damage.Supported){log.Add(delayed.Move.name+": 예약 위력 — 수동 판정");continue;}int amount=damage.Rolls[Math.Min(15,(int)((rng==null?0:rng())*16))];if(target.Conditions.Substitute>0){target.Conditions.Substitute=Math.Max(0,target.Conditions.Substitute-amount);log.Add(delayed.Move.name+": 대타에 "+amount+" 대미지. 남은 대타 HP "+target.Conditions.Substitute);continue;}int before=target.HP;target.HP=Math.Max(0,target.HP-amount);int actual=before-target.HP;log.Add(delayed.SourceName+"의 "+delayed.Move.name+"! "+target.Data.name+"에게 "+amount+" 대미지."+(damage.Type>1?" 효과가 굉장했다!":damage.Type<1?" 효과가 별로다.":""));if(actual>0){var source=DelayedSource(delayed);RuntimeTriggers.Damaged(target,actual,delayed.SourceName+"의 "+delayed.Move.name,log.Add);AnsweredTriggers.HpChanged(target,source,r,before,target.HP,delayed.SourceName+"의 "+delayed.Move.name,log.Add);RuntimeTriggers.Received(target,source,delayed.Move,actual,log.Add);RuntimeTriggers.AttackSucceeded(source,target,delayed.Move,log.Add);RuntimeTriggers.Hit(source,target,delayed.Move,actual,false,false,1,log.Add);}if(target.HP==0)log.Add(target.Data.name+"은 쓰러졌다!");if(onDamage!=null)onDamage(side,delayed,actual,target.HP==0);}}}
  static double DelayedAccuracy(Fighter target,DelayedAttackState delayed,Rules r){double stage=Engine.Stage(-target.EvasionStage);return Math.Min(100,(delayed.Move.accuracy<=0?100:delayed.Move.accuracy)*(r.Gravity>0?5.0/3:1)*stage/Math.Max(.0001,target.EvasionMultiplier));}
  public static double DamageFactor(Fighter a,Fighter b,Move m,Rules r,int targetSide=-1){double v=PrivilegeRules.ContractType(b).Length>0&&m.types.Contains(PrivilegeRules.ContractType(b))?.5:1;if(r.Critical)return v;var field=targetSide>=0&&targetSide<2?r.Sides[targetSide]:null;if(field!=null&&(m.category=="물리"&&field.Reflect>0||m.category=="특수"&&field.LightScreen>0))v*=.5;return v;}
  static int Max(Fighter a,Fighter b,Rules r){return Engine.Stats(a,b.Data.level,r)[0];}
  public static int MaxHP(Fighter a,Fighter b,Rules r){return Max(a,b,r);}
  public static bool Contact(Move move){return move!=null&&(move.tags??new string[0]).Any(x=>x=="접촉"||x.Contains("접촉"));}
  public static bool SureHit(Move move){return move!=null&&(move.tags??new string[0]).Any(x=>x.Contains("필중"))||move!=null&&new[]{"손가락흔들기"}.Contains(move.name);}
  internal static void Hurt(Fighter a,Fighter b,Rules r,int damage,string reason,List<string> log,bool indirect=true){if(a.HP<=0||indirect&&(AbilityPhrases.IgnoresIndirect(a)||AbilityStateModules.IgnoresDamageSource(a,reason)))return;int before=a.HP;int loss=Math.Min(a.HP,Math.Max(1,damage));if(indirect&&loss>=a.HP&&AbilityStateModules.SurvivesIndirect(a))loss=a.HP-1;if(loss<=0)return;a.HP-=loss;log.Add(a.Data.name+" — "+reason+": HP -"+loss);RuntimeTriggers.Damaged(a,loss,reason,log.Add);AnsweredTriggers.HpChanged(a,b,r,before,a.HP,reason,log.Add);if(indirect)AnsweredTriggers.IndirectDamage(a,b,loss,reason,log.Add);if(a.HP==0)log.Add(a.Data.name+"은 쓰러졌다!");}
  // 오레마스: 「매 T종료 시의 회복」은 중복되지 않고 회복량이 가장 큰 효과 하나만 적용한다. 아쿠아링은 1/8.
  public static IEnumerable<KeyValuePair<string,int>> TurnEndHeals(Fighter a,Fighter b,Rules r,int max){var c=a.Conditions;if(r.Terrain=="그래스필드"&&Grounded(a,r))yield return new KeyValuePair<string,int>("그래스필드",Math.Max(1,max/16));if(ItemRuntime.Name(a)=="먹다남은음식")yield return new KeyValuePair<string,int>("먹다남은음식",Math.Max(1,max/16));if(ItemRuntime.Name(a)=="검은진흙"&&Has(a,"독"))yield return new KeyValuePair<string,int>("검은진흙",Math.Max(1,max/16));if(c.AquaRing)yield return new KeyValuePair<string,int>("아쿠아링",Math.Max(1,max/8));if(c.Ingrain)yield return new KeyValuePair<string,int>("뿌리박기",Math.Max(1,max/16));}
  public static KeyValuePair<string,int> TurnEndHeal(Fighter a,Fighter b,Rules r,int max){var all=TurnEndHeals(a,b,r,max).ToList();if(all.Count==0)return new KeyValuePair<string,int>("",0);var best=all.OrderByDescending(x=>x.Value).First();return all.Count==1?best:new KeyValuePair<string,int>(best.Key+" (중복 회복은 가장 큰 효과만)",best.Value);}
  public static int Heal(Fighter a,Fighter b,Rules r,int amount,string reason,List<string> log){if(a.HP<=0)return 0;if(a.Conditions.HealBlock>0){log.Add(a.Data.name+": 회복봉인으로 회복하지 못했다 ("+reason+")");return 0;}int gain=Math.Min(Max(a,b,r)-a.HP,Math.Max(1,amount));if(gain>0){a.HP+=gain;log.Add(a.Data.name+" — "+reason+": HP +"+gain);AnsweredTriggers.Healed(a,gain,reason,log.Add);}return gain;}
  public static bool ChangeStage(Fighter target,Fighter other,int index,int amount,Rules r,string reason,List<string> log){return ChangeStage(target,other,index,amount,r,reason,log,false);}
  static bool ChangeStage(Fighter target,Fighter other,int index,int amount,Rules r,string reason,List<string> log,bool reflected){
   if(target==null||index<0||index>=target.Stages.Length||amount==0)return false;bool opponent=other!=null&&!Object.ReferenceEquals(target,other);int resolved=amount;
   resolved=AbilityPhrases.StageAmount(target,other,r,resolved);
   if(resolved<0&&opponent&&AbilityPhrases.BlocksDrop(target,index)){log.Add(target.Data.name+"의 「"+AbilityRuntime.Name(target)+"」: 능력이 내려가지 않는다");return false;}if(resolved<0&&opponent&&StateTicks.Mist(target,r)){log.Add(target.Data.name+": 흰안개로 능력이 내려가지 않는다");return false;}
   if(resolved<0&&opponent&&!reflected&&AbilityPhrases.ReflectsStage(target,other,r,log))return ChangeStage(other,target,index,resolved,r,"능력 하락 반사",log,true);
   int before=target.Stages[index],after=Math.Max(-6,Math.Min(6,before+resolved));if(before==after)return false;target.Stages[index]=after;target.Conditions.StageByOpponent=opponent;StateWatch.CheckCurrent(log.Add);target.Conditions.StageByOpponent=false;log.Add(target.Data.name+": "+Engine.Names[index]+" "+(resolved>0?"+":"")+(after-before)+(reason.Length>0?" ("+reason+")":""));AnsweredTriggers.StageChanged(target,other,index,before,after,log.Add);
   return true;
  }
  public static void Absorb(Fighter a,Fighter b,string reason,Rules r,List<string> log){}
  public static bool BeforeMove(Fighter a,Fighter b,Move m,Rules r,Func<double> rng,List<string> log){var c=a.Conditions;
   if(a.Status=="잠듦"){if(c.SleepTurns>0){c.SleepTurns--;log.Add(a.Data.name+": 쿨쿨 잠들어 있다");return false;}a.Status="정상";c.SleepFromRest=false;log.Add(a.Data.name+": 잠에서 깨어났다.");}
   if(c.Recharge){c.Recharge=false;log.Add(a.Data.name+": 반동으로 행동할 수 없다.");return false;}
   if(a.Status=="얼음"){if(rng()<.2||new[]{"열탕","플레어드라이브","화염자동차","성스러운불꽃"}.Contains(m.name)){a.Status="정상";log.Add(a.Data.name+": 얼음이 녹았다.");}else{log.Add(a.Data.name+": 얼어서 움직일 수 없다.");return false;}}
   if(c.Flinch){c.Flinch=false;log.Add(a.Data.name+": 풀죽어서 움직일 수 없다.");return false;}
   string unavailable=MoveBlock(a,m,false);if(unavailable.Length>0){if(c.Forgotten>0&&m.name!="발버둥")log.Add(a.Data.name+": "+unavailable);else{log.Add(a.Data.name+": "+unavailable);return false;}}
   if(c.AttractSide>=0&&rng()<.5){log.Add(a.Data.name+": 헤롱헤롱해서 움직이지 못했다");return false;}
   if(c.Confusion>0){c.Confusion--;if(rng()<1.0/3){var plain=new Rules{LevelCorrection=r.LevelCorrection,HPCorrection=r.HPCorrection};int attack=Engine.Effective(a,b,1,plain),defense=Engine.Effective(a,b,2,plain);int damage=(int)Math.Floor(((2*a.Data.level+10)/250.0*attack/defense*40+2)*(.85+(int)(rng()*16)/100.0));Hurt(a,b,r,damage,"혼란 자해 (위력40)",log,false);return false;}log.Add(a.Data.name+": 혼란을 버티고 행동했다.");}
   if(a.Status=="마비"&&rng()<.25){log.Add(a.Data.name+": 몸이 저려 움직일 수 없다.");return false;}return true;
  }
  public static double Accuracy(Fighter a,Fighter b,Move m,Rules r,int attackerSide=-1){if(SureHit(b,m,attackerSide))return double.PositiveInfinity;if(Ohko(m))return a.Data.level<b.Data.level?0:Math.Min(100,m.accuracy+a.Data.level-b.Data.level);if(m.name=="발버둥")return double.PositiveInfinity;string w=Weather(a,b,r);if(MoveModules.SureHit(a,b,m,r)||b.Conditions.Telekinesis>0&&!Ohko(m))return double.PositiveInfinity;if((m.name=="번개"||m.name=="폭풍")&&Sunny(w))return 50;double stage=Engine.Stage(a.AccuracyStage-(MoveModules.IgnoresRaise(m,"회피")||a.Conditions.Foresight.Length>0?Math.Min(0,b.EvasionStage):b.EvasionStage));return Math.Min(100,m.accuracy*(r.Gravity>0?5.0/3:1)*stage*a.AccuracyMultiplier*PotentialModifiers.Stat(a,b,6,r)*PotentialModifiers.Accuracy(a,b,m,r)/(b.EvasionMultiplier*PotentialModifiers.Stat(b,a,7,r)*AbilityRuntime.Evasion(a,b)));}
  public static readonly Dictionary<string,string> StatusMoves=new Dictionary<string,string>{{"도깨비불","화상"},{"맹독","맹독"},{"독가루","독"},{"독가스","독"},{"전기자석파","마비"},{"저리가루","마비"},{"뱀눈초리","마비"},{"버섯포자","잠듦"},{"수면가루","잠듦"},{"최면술","잠듦"},{"악마의키스","잠듦"}};
  public static readonly Dictionary<string,string> WeatherMoves=new Dictionary<string,string>{{"쾌청","쾌청"},{"비바라기","비"},{"모래바람","모래바람"},{"설경","눈"},{"싸라기눈","싸라기눈"}};
  public static readonly string[] HazardMoves={"스텔스록","압정뿌리기","독압정","끈적끈적네트"};
  public static readonly string[] SelfMoves={"방어","판별","대타출동","자기재생","태만함","날개쉬기","아쿠아링","뿌리박기","리플렉터","빛의장막","순풍","칼춤","고속이동","나쁜음모","명상","충전","분노","도우미","멸망의노래","웅크리기","잠자기"};
  public static bool TargetsOpponent(Move m){if(m==null)return false;if(!string.IsNullOrWhiteSpace(m.target))return !new[]{"자신","자진","필드","아군"}.Contains(m.target);return m.category!="변화"||!(SelfMoves.Contains(m.name)||WeatherMoves.ContainsKey(m.name)||Terrains.Contains(m.name)||m.name=="트릭룸"||m.name=="중력");}
  public static bool KnownMove(Move m){return m.category!="변화"||MoveRuntime.TextDriven(m)||StatusMoves.ContainsKey(m.name)||SelfMoves.Contains(m.name)||WeatherMoves.ContainsKey(m.name)||Terrains.Contains(m.name)||HazardMoves.Contains(m.name)||new[]{"이상한빛","초음파","씨뿌리기","도발","트릭룸","중력","안개제거","트집","사슬묶기","회복봉인","검은눈빛","록온","마음의눈","헤롱헤롱","앙코르"}.Contains(m.name);}
  // 장소 전개(기술·포텐셜 공용, 확정 24): 특수 날씨는 덮어쓸 수 없고 같은 날씨·지형이면 실패
  public static bool SetWeather(Fighter a,Fighter b,string next,int turns,Rules r,List<string> log,string source){if(AbilityPhrases.HasWeatherLock(a,b,r)||r.Weather=="큰가뭄"||r.Weather=="강한 비"||r.Weather=="난기류"){log.Add("특수 날씨는 일반 날씨 효과로 덮어쓸 수 없다.");return false;}if(r.Weather==next){log.Add(source+" 실패: 이미 "+next+" 날씨다.");return false;}turns=MiscModules.ExtendTurns(a,next,turns);string before=r.Weather;r.Weather=next;r.WeatherTurns=turns;log.Add("날씨: "+r.Weather+" ("+turns+"턴)");AnsweredTriggers.WeatherChanged(a,b,before,r.Weather,log.Add);return true;}
  public static bool SetTerrain(Fighter a,Fighter b,string next,int turns,Rules r,List<string> log,string source){if(r.Terrain==next){log.Add(source+" 실패: 이미 "+next+"다.");return false;}turns=MiscModules.ExtendTurns(a,next,turns);string before=r.Terrain;r.Terrain=next;r.TerrainTurns=turns;log.Add("필드: "+next+" ("+turns+"턴)");AnsweredTriggers.TerrainChanged(a,b,before,r.Terrain,log.Add);return true;}
  public static readonly string[] HazardNames={"스텔스록","압정뿌리기","독압정","끈적끈적네트"};
  // 설치물(확정 25): 층수 제한을 넘으면 실패
  static string DirectCondition(string move) {
   switch(move) {
    case "록온": return "마음의눈";
    case "이상한빛": case "초음파": return "혼란";
    case "트집": case "사슬묶기": case "회복봉인": case "검은눈빛":
    case "마음의눈": case "헤롱헤롱": case "씨뿌리기": case "도발": case "앙코르": return move;
    default: return "";
   }
  }
  public static bool InstallHazard(int side,string name,Rules r,List<string> log){var hazards=r.Sides[Math.Max(0,Math.Min(1,side))];switch(name){case "스텔스록":if(hazards.StealthRock){log.Add("스텔스록 실패: 이미 설치되어 있다.");return false;}hazards.StealthRock=true;log.Add((side+1)+"팀 진영: 스텔스록");return true;case "압정뿌리기":if(hazards.Spikes>=3){log.Add("압정뿌리기 실패: 이미 3겹이다.");return false;}hazards.Spikes++;log.Add((side+1)+"팀 진영: 압정 "+hazards.Spikes+"겹");return true;case "독압정":if(hazards.ToxicSpikes>=2){log.Add("독압정 실패: 이미 2겹이다.");return false;}hazards.ToxicSpikes++;log.Add((side+1)+"팀 진영: 독압정 "+hazards.ToxicSpikes+"겹");return true;case "끈적끈적네트":if(hazards.StickyWeb){log.Add("끈적끈적네트 실패: 이미 설치되어 있다.");return false;}hazards.StickyWeb=true;log.Add((side+1)+"팀 진영: 끈적끈적네트");return true;}return false;}
  public static bool StatusMove(Fighter a,Fighter b,Move m,Rules r,Func<double> rng,List<string> log,int attackerSide=0){var c=a.Conditions;int max=Max(a,b,r);int targetSide=1-Math.Max(0,Math.Min(1,attackerSide));var hazards=r.Sides[targetSide];
   string conditionName = DirectCondition(m.name);
   if(conditionName.Length>0 && StateEffects.BlockedCondition(b,a,conditionName,r,false,log))return false;
   if(m.name=="앙코르")return ApplyEncore(b,log);
   if(StatusMoves.ContainsKey(m.name)){return ApplyStatus(b,a,StatusMoves[m.name],r,rng,log);}
   if(WeatherMoves.ContainsKey(m.name))return SetWeather(a,b,WeatherMoves[m.name],5,r,log,m.name);
   if(Terrains.Contains(m.name)&&m.name!="없음")return SetTerrain(a,b,m.name,5,r,log,m.name);
   if(HazardNames.Contains(m.name))return InstallHazard(targetSide,m.name,r,log);
   if(m.name=="안개제거"){r.Sides[0].ClearHazards();r.Sides[1].ClearHazards();log.Add("안개제거: 양쪽 진영의 설치물을 제거했다.");SecondaryEffects.Roll(a,b,"「10할:회피-1」",m.name,r,rng,log,null);return true;}
   if(new[]{"트집","사슬묶기","회복봉인","검은눈빛"}.Contains(m.name))return ApplyActionState(b,m.name,0,log);
   if(m.name=="록온"||m.name=="마음의눈"){if(AlreadyHas(b,"마음의눈",log))return false;ApplyMindReader(b,attackerSide,r);log.Add(b.Data.name+": 마음의눈 ("+(attackerSide+1)+"팀)");return true;}
   if(m.name=="헤롱헤롱"){if(AlreadyHas(b,"헤롱헤롱",log))return false;b.Conditions.AttractSide=attackerSide;log.Add(b.Data.name+": 헤롱헤롱");return true;}
   if(m.name=="멸망의노래"){foreach(var target in new[]{a,b})if(target.OnField&&target.HP>0&&target.Conditions.DeathCount==0&&!StateEffects.BlockedCondition(target,a,"죽음의 카운트",r,false,log)){target.Conditions.DeathCount=4;log.Add(target.Data.name+": 죽음의 카운트 4");}return true;}
   if(m.name=="잠자기"){if(c.HealBlock>0){Heal(a,b,r,max,m.name,log);return false;}string previous=a.Status;a.Status="정상";if(!ApplyStatus(a,a,"잠듦",r,rng,log,true)){a.Status=previous;return false;}Heal(a,b,r,max,m.name,log);return true;}
   switch(m.name){
    case "도우미":c.HelpingHand=true;log.Add(a.Data.name+": 도우미");return true;
    case "웅크리기":c.Curled=true;ChangeStage(a,a,2,1,r,m.name,log);ChangeStage(a,a,4,1,r,m.name,log);return true;
    case "분노":if(AlreadyHas(a,"분노",log))return false;c.Rage=5;log.Add(a.Data.name+": 분노 "+c.Rage+"T");return true;
    case "방어":case "판별":if(rng()<Math.Pow(1.0/3,c.ProtectChain)){c.Protect=true;c.ProtectChain++;log.Add(a.Data.name+": 방어 태세!");}else{c.ProtectChain=0;log.Add(a.Data.name+": 연속 방어 실패.");return false;}break;
    case "대타출동":int cost=Math.Max(1,max/4);if(c.Substitute>0||a.HP<=cost){log.Add("대타출동 실패: HP 부족 또는 이미 대타 존재.");return false;}a.HP-=cost;c.Substitute=cost;log.Add(a.Data.name+": 대타 HP "+cost);break;
    case "자기재생":case "태만함":return Heal(a,b,r,max/2,m.name,log)>0;
    case "날개쉬기":log.Add("날개쉬기: 비행 타입 일시 제거까지 포함하므로 현재 수동 판정.");return false;
    case "충전":if(c.Charged){log.Add(a.Data.name+": 충전 실패: 이미 상태변화가 있다.");return false;}else{c.Charged=true;log.Add(a.Data.name+": 충전");}break;
    case "아쿠아링":if(c.AquaRing){log.Add(a.Data.name+": 아쿠아링 실패: 이미 상태변화가 있다.");return false;}else{c.AquaRing=true;log.Add(a.Data.name+": 아쿠아링");}break;
    case "뿌리박기":if(AlreadyHas(a,"뿌리박기",log))return false;c.Ingrain=true;log.Add(a.Data.name+": 뿌리박기 (접지)");break;
    case "리플렉터":r.Sides[targetSide].Reflect=MiscModules.ExtendTurns(a,"리플렉터",5);log.Add((targetSide+1)+"팀 진영: 리플렉터 "+r.Sides[targetSide].Reflect+"턴");break;
    case "빛의장막":r.Sides[targetSide].LightScreen=MiscModules.ExtendTurns(a,"빛의장막",5);log.Add((targetSide+1)+"팀 진영: 빛의장막 "+r.Sides[targetSide].LightScreen+"턴");break;
    case "순풍":r.Sides[attackerSide].Tailwind=4;log.Add((attackerSide+1)+"팀 진영: 순풍 4턴");break;
    case "칼춤":return ChangeStage(a,b,1,2,r,m.name,log);
    case "고속이동":return ChangeStage(a,b,5,2,r,m.name,log);
    case "나쁜음모":return ChangeStage(a,b,3,2,r,m.name,log);
    case "명상":bool raised=ChangeStage(a,b,3,1,r,m.name,log);return ChangeStage(a,b,4,1,r,m.name,log)||raised;
    case "이상한빛":case "초음파":if(!BypassAbility(a)&&PotentialModifiers.StatusImmune(b,a,"혼란",r)||r.Terrain=="미스트필드"&&Grounded(b,r))log.Add("혼란 무효: 특성/포텐셜 / 미스트필드");else if(b.Conditions.Confusion==0){b.Conditions.Confusion=1+(int)(rng()*4);log.Add(b.Data.name+": 혼란");}break;
    case "씨뿌리기":if(AlreadyHas(b,"씨뿌리기",log))return false;if(Has(b,"풀"))log.Add("풀 타입: 씨뿌리기 무효");else{b.Conditions.Seed=true;log.Add(b.Data.name+": 씨뿌리기");}break;
    case "도발":if(AlreadyHas(b,"도발",log))return false;b.Conditions.Taunt=3;log.Add(b.Data.name+": 도발 3턴");break;
    case "트릭룸":r.TrickRoom=r.TrickRoom>0?0:5;log.Add("트릭룸 "+(r.TrickRoom>0?"5턴":"해제"));break;
    case "중력":r.Gravity=5;log.Add("중력 5턴");break;
    default:log.Add(m.name+": 자동 처리 미등록 — 기술 효과를 수동 판정하세요.");return false;
   }return true;
  }
  public static void AfterHit(Fighter a,Fighter b,Move m,Rules r,Func<double> rng,List<string> log,bool substitute,int attackerSide=0,int bodyDamage=0){
   if(m.name=="고속스핀"){ClearTurns(ref a.Conditions.Bound,a,"조이기",log);bool removed=r.Sides[attackerSide].HasHazards();r.Sides[attackerSide].ClearHazards();ChangeStage(a,a,5,1,r,m.name,log);if(removed)log.Add(a.Data.name+"의 고속스핀: 아군 진영의 설치물 제거");}
   if(substitute)return;if(bodyDamage>0&&m.category!="변화"){if(b.Conditions.Rage>0)ChangeStage(b,b,1,1,r,"분노",log);if(b.Conditions.Bomb&&Contact(m)&&!MoveModules.IgnoresContact(a)){Hurt(b,a,r,Math.Max(1,Max(b,a,r)/4),"폭탄",log);ClearFlag(ref b.Conditions.Bomb,b,"폭탄",log);}}if(ItemRuntime.Name(b)=="풍선"&&!b.Conditions.BalloonPopped){b.Conditions.BalloonPopped=true;log.Add(b.Data.name+": 풍선이 터졌다.");}
   if(b.Status=="얼음"&&m.types.Contains("불꽃")){b.Status="정상";log.Add(b.Data.name+": 불꽃 공격으로 얼음이 녹았다.");}
  }
  public static void EnterField(Database db,Fighter entering,Fighter opponent,int side,Rules r,Func<double> rng,List<string> log){StateTicks.Restore(entering,r);
   var hazards=r.Sides[side];if(hazards==null||!hazards.Any()||entering.HP<=0)return;if(ItemRuntime.Name(entering)=="통굽부츠"||AbilityPhrases.IgnoresHazards(entering)){log.Add(entering.Data.name+": 통굽부츠로 설치물의 영향을 받지 않는다.");return;}int max=Max(entering,opponent,r);
   if(hazards.StealthRock){double type=Engine.Match(db,new[]{"바위"},entering.Data.types);Hurt(entering,opponent,r,(int)Math.Floor(max*type/8.0),"스텔스록",log);if(entering.HP<=0)return;}
   bool grounded=Grounded(entering,r);if(grounded&&hazards.Spikes>0){double[] rate={0,1.0/8,1.0/6,1.0/4};Hurt(entering,opponent,r,(int)Math.Floor(max*rate[Math.Min(3,hazards.Spikes)]),"압정 "+hazards.Spikes+"겹",log);if(entering.HP<=0)return;}
   if(grounded&&hazards.ToxicSpikes>0){if(Has(entering,"독")){hazards.ToxicSpikes=0;log.Add(entering.Data.name+": 독 타입이 독압정을 흡수했다.");}else if(Has(entering,"강철"))log.Add(entering.Data.name+": 강철 타입이라 독압정이 통하지 않는다.");else{var source=opponent.Copy();source.Data.ability="";ApplyStatus(entering,source,hazards.ToxicSpikes>=2?"맹독":"독",r,rng,log);}}
   if(grounded&&hazards.StickyWeb&&entering.HP>0)ChangeStage(entering,opponent,5,-1,r,"끈적끈적네트",log);
  }
  public static void EndTurn(Fighter[] f,Rules r,List<string> log,Func<double> rng=null){
   string weather=Weather(f[0],f[1],r);int[] order=SpeedOrder(f,r,rng);
   foreach(int i in order){var a=f[i];var b=f[1-i];var c=a.Conditions;int max=Max(a,b,r);
    if(weather=="모래바람"&&!a.Data.types.Any(t=>new[]{"바위","땅","강철"}.Contains(t))&&!AbilityPhrases.IgnoresWeatherDamage(a,weather)&&ItemRuntime.Name(a)!="방진고글")Hurt(a,b,r,max/16,"모래바람",log);if(weather=="싸라기눈"&&!Has(a,"얼음")&&!AbilityPhrases.IgnoresWeatherDamage(a,weather)&&ItemRuntime.Name(a)!="방진고글")Hurt(a,b,r,max/16,"싸라기눈",log);
    if(ItemRuntime.Name(a)=="검은진흙"&&!Has(a,"독"))Hurt(a,b,r,max/8,"검은진흙",log);if(a.Status=="독")Hurt(a,b,r,max/8,"독",log);if(a.Status=="맹독"){Hurt(a,b,r,Math.Max(1,(int)Math.Floor(max*c.ToxicStage/16.0)),"맹독 "+c.ToxicStage+"/16",log);c.ToxicStage=Math.Min(15,c.ToxicStage+1);}if(a.Status=="화상"||a.Status=="동상")Hurt(a,b,r,max/16,a.Status,log);if(c.Curse)Hurt(a,b,r,max/4,"저주",log);if(c.Bound>0)Hurt(a,b,r,Math.Max(1,max/(c.BindDivisor>0?c.BindDivisor:8)),"조이기",log);
   }
   foreach(int i in order){var a=f[i];var b=f[1-i];var c=a.Conditions;int max=Max(a,b,r);if(c.Seed&&a.HP>0&&!AbilityPhrases.IgnoresIndirect(a)){int before=a.HP;Hurt(a,b,r,max/8,"씨뿌리기",log);int loss=before-a.HP;if(loss>0){int gain=Heal(b,a,r,loss,"씨뿌리기 흡수",log);AnsweredTriggers.Drained(b,a,loss,gain,log.Add);}}}
   foreach(int i in order){var a=f[i];var b=f[1-i];var c=a.Conditions;int max=Max(a,b,r);var turnHeal=TurnEndHeal(a,b,r,max);if(turnHeal.Value>0)Heal(a,b,r,turnHeal.Value,turnHeal.Key,log);}
   foreach(int i in order){var a=f[i];var c=a.Conditions;ClearFlag(ref c.HelpingHand,a,"도우미",log);ClearTypeSwap(a,log);Tick(ref c.EncoreTurns,"앙코르",a,log);if(c.EncoreTurns==0)c.EncoreMove="";Tick(ref c.Intercept,"요격",a,log);Tick(ref c.Rage,"분노",a,log);if(c.Bomb&&c.BombSetTurn<r.Turn)ClearFlag(ref c.Bomb,a,"폭탄",log);if(c.MindReaderSide>=0&&c.MindReaderSetTurn<r.Turn)ClearMindReader(a,log);Tick(ref c.Silenced,"침묵",a,log);Tick(ref c.Forgotten,"망각",a,log);Tick(ref c.HealBlock,"회복봉인",a,log);Tick(ref c.Disabled,"사슬묶기",a,log);if(c.Disabled==0)c.DisabledMove="";Tick(ref c.Bound,"조이기",a,log);if(c.MeanLook&&c.MeanLookTurns>0&&--c.MeanLookTurns==0)ClearFlag(ref c.MeanLook,a,"검은눈빛",log);c.Protect=false;c.Enduring=false;c.Flinch=false;Tick(ref c.Taunt,"도발",a,log);Tick(ref r.Sides[i].Reflect,"리플렉터",a,log);Tick(ref r.Sides[i].LightScreen,"빛의장막",a,log);Tick(ref r.Sides[i].Tailwind,"순풍",a,log);}
   foreach(var x in f)PotentialModifiers.Tick(x,log);if(r.Weather!="없음"&&r.WeatherTurns>0&&--r.WeatherTurns==0){log.Add(r.Weather+" 종료");r.Weather="없음";}
   if(r.Terrain!="없음"&&r.TerrainTurns>0&&--r.TerrainTurns==0){log.Add(r.Terrain+" 종료");r.Terrain="없음";}
   if(r.TrickRoom>0&&--r.TrickRoom==0)log.Add("트릭룸 종료");if(r.Gravity>0&&--r.Gravity==0)log.Add("중력 종료");PriorityEffectRuntime.EndTurn(f[0],log.Add);PriorityEffectRuntime.EndTurn(f[1],log.Add);
   foreach(var a in f.Concat((r.Parties??new Fighter[2][]).Where(x=>x!=null).SelectMany(x=>x)).Distinct()){if(a.Conditions.Hibernate>0&&a.Conditions.Hibernate<=r.Turn){a.Conditions.Hibernate=0;ApplyStatus(a,f[0]==a?f[1]:f[0],"얼음",r,rng,log);}}
   foreach(var a in f)if(a.OnField&&a.HP>0&&a.Conditions.DeathCount>0){a.Conditions.DeathCount--;log.Add(a.Data.name+": 죽음의 카운트 "+a.Conditions.DeathCount);if(a.Conditions.DeathCount==0){int before=a.HP;a.HP=0;log.Add(a.Data.name+"은 죽음의 카운트로 쓰러졌다");AnsweredTriggers.HpChanged(a,f[0]==a?f[1]:f[0],r,before,0,"죽음의 카운트",log.Add);}}r.Turn++;
  }
  public static int[] SpeedOrder(Fighter[] f,Rules r,Func<double> rng){int left=Engine.Effective(f[0],f[1],5,r,0,0),right=Engine.Effective(f[1],f[0],5,r,0,1);if(left==right)return rng!=null&&rng()>=.5?new[]{1,0}:new[]{0,1};return left>right?new[]{0,1}:new[]{1,0};}
  static void Tick(ref int value,string name,Fighter a,List<string> log){if(value>0&&--value==0)log.Add(a.Data.name+": "+name+" 종료");}
 }

}
