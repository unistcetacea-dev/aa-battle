using System;
using System.Linq;
using System.Collections.Generic;

namespace AABattle {
 // 상태변화(타입 변경·행동 제한·반응·자동 행동·수면 등) 부여·해제·판정
 public static partial class Mechanics {
  // 타입 변경 후 위력을 판정하며 원본 기술은 보존한다.
  public static Move JudgedMove(Fighter a,Move m){var c=a.Conditions;string type=c.Dark?"악":c.Soaked?"물":c.Electrified?"전기":"";if(type.Length==0)return m;var copy=m.Copy();copy.types=new[]{type};return copy;}
  public static double ChargePower(Fighter a,Move m){var c=a.Conditions;double v=1;if(c.Heated&&m.types.Contains("불꽃"))v*=2;if(c.Chilled&&m.types.Contains("얼음"))v*=2;if(c.Charged&&m.types.Contains("전기"))v*=2;return v;}
  static void ClearFlag(ref bool flag,Fighter a,string name,List<string> log){if(!flag)return;flag=false;if(log!=null)log.Add(a.Data.name+"의 "+name+"이 풀렸다");}
  public static void ClearTypeSwap(Fighter a,List<string> log){var c=a.Conditions;ClearFlag(ref c.Dark,a,"악인",log);ClearFlag(ref c.Soaked,a,"침수",log);ClearFlag(ref c.Electrified,a,"송전",log);}
  public static void SetTypeSwap(Fighter a,string name,List<string> log){if(name!="악인"&&name!="침수"&&name!="송전")return;var c=a.Conditions;if(name!="악인")ClearFlag(ref c.Dark,a,"악인",log);if(name!="침수")ClearFlag(ref c.Soaked,a,"침수",log);if(name!="송전")ClearFlag(ref c.Electrified,a,"송전",log);c.Dark=name=="악인";c.Soaked=name=="침수";c.Electrified=name=="송전";}
  public static void AfterMove(Fighter a,Move m,ConditionState used,List<string> log,Func<double> rng=null,bool hit=false){var c=a.Conditions;PotentialModifiers.ClearAction(a,log);if(EffectScope.Current!=null)PotentialModifiers.ClearAction(EffectScope.Current.Opponent(a),log);if(!string.IsNullOrEmpty(used.RolloutMove))FinishRollout(a,m,hit,log);if(used.HelpingHand)ClearFlag(ref c.HelpingHand,a,"도우미",log);if(used.RampageTurns>0)FinishRampage(a,rng,log);if(used.Bomb)ClearFlag(ref c.Bomb,a,"폭탄",log);if(used.Heated&&m.types.Contains("불꽃"))ClearFlag(ref c.Heated,a,"가열",log);if(used.Chilled&&m.types.Contains("얼음"))ClearFlag(ref c.Chilled,a,"냉각",log);if(used.Charged&&m.types.Contains("전기"))ClearFlag(ref c.Charged,a,"충전",log);if(used.Dark)ClearFlag(ref c.Dark,a,"악인",log);if(used.Soaked)ClearFlag(ref c.Soaked,a,"침수",log);if(used.Electrified)ClearFlag(ref c.Electrified,a,"송전",log);}
  public static void ClearMoveStates(Fighter a,List<string> log){var c=a.Conditions;ClearFlag(ref c.Heated,a,"가열",log);ClearFlag(ref c.Chilled,a,"냉각",log);ClearFlag(ref c.Charged,a,"충전",log);ClearTypeSwap(a,log);}
  public static string MoveBlock(Fighter a,Move m,bool selection=true,bool ignoreEncore=false){if(a==null||m==null)return "기술 없음";var c=a.Conditions;if(selection&&AutoAction(a))return "「"+AutoActionLabel(a)+"」: 자동 행동 중";if(!ignoreEncore&&!AutoAction(a)&&c.EncoreTurns>0&&c.EncoreMove!=m.name)return "「앙코르」: "+c.EncoreMove+"만 사용 가능";if(m.name=="발버둥")return "";if(c.ChoiceMove.Length>0&&ItemRuntime.Choice(a)&&m.name!=c.ChoiceMove&&!m.extension)return "「구애」: "+c.ChoiceMove+"만 사용 가능";if(c.Forgotten>0)return "「망각」: 발버둥으로 대체";if(c.Silenced>0&&Sound(m))return "「침묵」: 소리 기술 사용 불가";if(c.Tormented&&c.LastMove==m.name)return "「트집」: 같은 기술 연속 사용 불가";if(c.Disabled>0&&c.DisabledMove==m.name)return "「사슬묶기」: "+m.name+" 사용 불가";if(c.Taunt>0&&m.category=="변화")return "도발 때문에 변화기를 쓸 수 없다.";return "";}
  public static Move ActionMove(Database db,Fighter a,Move m){string automatic=AutoMoveName(a);if(automatic.Length>0){m=db.moves.FirstOrDefault(x=>x.name==automatic);if(m==null)throw new Exception("자동 행동 기술이 없다: "+automatic);}else if(a.Conditions.EncoreTurns>0){m=db.moves.FirstOrDefault(x=>x.name==a.Conditions.EncoreMove);if(m==null)throw new Exception("앙코르 기술이 없다: "+a.Conditions.EncoreMove);}if(a.Conditions.Forgotten<=0)return m;var stored=db.moves.FirstOrDefault(x=>x.name=="발버둥");var move=stored==null?new Move{name="발버둥",tags=new[]{"접촉"}}:stored.Copy();move.category="물리";move.types=new[]{"노말"};move.power=50;move.accuracy=double.PositiveInfinity;move.attack="공격";move.defense="방어";move.priority=0;move.target="상대";return move;}
  public static bool IsRollout(Move move){return move!=null&&(move.name=="구르기"||move.name=="아이스볼");}
  public static string AutoMoveName(Fighter a){return a.Conditions.RampageTurns>0?a.Conditions.RampageMove:!string.IsNullOrEmpty(a.Conditions.ChargeMove)?a.Conditions.ChargeMove:a.Conditions.RolloutMove??"";}
  public static bool AutoAction(Fighter a){return !string.IsNullOrEmpty(AutoMoveName(a));}
  public static string AutoActionLabel(Fighter a){return a.Conditions.RampageTurns>0?"난동부리기":!string.IsNullOrEmpty(a.Conditions.ChargeMove)?"모으기":a.Conditions.RolloutMove;}
  public static void BeginRollout(Fighter a,Move move){if(!IsRollout(move)||a.Conditions.RampageTurns>0)return;if(a.Conditions.RolloutMove!=move.name){a.Conditions.RolloutCount=0;a.Conditions.RolloutMove=move.name;}}
  public static void ClearRollout(Fighter a,List<string> log){if(!string.IsNullOrEmpty(a.Conditions.RolloutMove)&&log!=null)log.Add(a.Data.name+"의 "+a.Conditions.RolloutMove+" 연속 사용이 풀렸다");a.Conditions.RolloutCount=0;a.Conditions.RolloutMove="";}
  public static void FinishRollout(Fighter a,Move move,bool hit,List<string> log){if(!hit||move.name!=a.Conditions.RolloutMove){ClearRollout(a,log);return;}a.Conditions.RolloutCount++;if(a.Conditions.RolloutCount>=5)ClearRollout(a,log);}
  public static void RefreshRollout(Database db,Fighter a,List<string> log=null){if(string.IsNullOrEmpty(a.Conditions.RolloutMove)){a.Conditions.RolloutCount=0;return;}var move=db.moves.FirstOrDefault(x=>x.name==a.Conditions.RolloutMove);if(!IsRollout(move)||a.Conditions.RolloutCount<0||a.Conditions.RolloutCount>=5||MoveBlock(a,move,false,true).Length>0)ClearRollout(a,log);}
  static bool EncoreExcluded(string name){return new[]{"앙코르","발버둥","흉내내기","따라하기","흉내쟁이","스케치","변신"}.Contains(name);}
  public static string EncoreGrantBlock(Fighter target,string selected=null){var c=target.Conditions;if(c.EncoreTurns>0)return "이미 앙코르 중";if(string.IsNullOrEmpty(c.LastMove))return "마지막으로 사용한 기술이 없다";string move=selected??c.LastMove;if(move!=c.LastMove)return "직전에 사용한 기술만 앙코르할 수 있다";if(EncoreExcluded(move))return "앙코르할 수 없는 기술: "+move;if(!(target.Moves??new string[0]).Contains(move))return "지금 기억하고 있지 않은 기술: "+move;return "";}
  public static bool ApplyEncore(Fighter target,List<string> log,int turns=3,string selected=null){string reason=EncoreGrantBlock(target,selected);if(reason.Length>0){if(log!=null)log.Add(target.Data.name+": 앙코르 실패: "+reason);return false;}target.Conditions.EncoreMove=target.Conditions.LastMove;target.Conditions.EncoreTurns=Math.Max(1,turns);if(log!=null)log.Add(target.Data.name+": 앙코르 "+target.Conditions.EncoreMove+" "+target.Conditions.EncoreTurns+"T");return true;}
  public static void ClearEncore(Fighter target,List<string> log){ClearTurns(ref target.Conditions.EncoreTurns,target,"앙코르",log);target.Conditions.EncoreMove="";}
  public static void RefreshEncore(Database db,Fighter target,List<string> log=null){var c=target.Conditions;if(c.EncoreTurns<=0){c.EncoreMove="";return;}var move=db.moves.FirstOrDefault(x=>x.name==c.EncoreMove);if(move==null||EncoreExcluded(c.EncoreMove)||!(target.Moves??new string[0]).Contains(c.EncoreMove)||MoveBlock(target,move,false,true).Length>0)ClearEncore(target,log);}
  // 상태변화가 이미 걸려 있는가(확정 23: 다시 걸면 실패하고 남은 턴은 유지). 상태 편집 창은 이 판정을 쓰지 않고 직접 값을 정한다
  public static bool HasCondition(Fighter f,string name){
   var c=f.Conditions;
   switch(name){
    case "혼란":return c.Confusion>0;
    case "도발":return c.Taunt>0;
    case "풀죽음":return c.Flinch;
    case "씨뿌리기":return c.Seed;
    case "침묵":return c.Silenced>0;
    case "망각":return c.Forgotten>0;
    case "트집":return c.Tormented;
    case "회복봉인":return c.HealBlock>0;
    case "사슬묶기":return c.Disabled>0;
    case "검은눈빛":return c.MeanLook;
    case "조이기":return c.Bound>0;
    case "행동불능":return c.Recharge;
    case "하늘 위":case "땅속":case "물속":case "불꽃속":case "어둠속":return c.Hidden==name;
    case "앙코르":return c.EncoreTurns>0;
    case "악인":return c.Dark;
    case "침수":return c.Soaked;
    case "송전":return c.Electrified;
    case "가열":return c.Heated;
    case "냉각":return c.Chilled;
    case "충전":return c.Charged;
    case "폭탄":return c.Bomb;
    case "요격":return c.Intercept>0;
    case "분노":return c.Rage>0;
    case "마음의눈":return c.MindReaderSide>=0;
    case "도우미":return c.HelpingHand;
    case "웅크리기":return c.Curled;
    case "아쿠아링":return c.AquaRing;
    case "뿌리박기":return c.Ingrain;
    case "헤롱헤롱":return c.AttractSide>=0;
    case "동면":return c.Hibernate>0;
    case "죽음의 카운트":return c.DeathCount>0;
    default:return false;
   }
  }
  // 이미 걸려 있으면 실패 로그를 남기고 true
  public static bool AlreadyHas(Fighter f,string name,List<string> log){
   if(f==null||!HasCondition(f,name))return false;
   if(log!=null)log.Add(f.Data.name+": 이미 「"+name+"」 상태라 실패했다(남은 턴 유지).");
   return true;
  }
  public static void RecordMove(Fighter a,Move m){if(m.name!="발버둥")a.Conditions.LastMove=m.name;}
  public static bool ApplyActionState(Fighter target,string name,int turns,List<string> log){if(name=="앙코르")return ApplyEncore(target,log,turns>0?turns:3);if(AlreadyHas(target,name,log))return false;var c=target.Conditions;if(name=="사슬묶기"&&string.IsNullOrEmpty(c.LastMove)){if(log!=null)log.Add(target.Data.name+": 사슬묶기 실패: 마지막으로 사용한 기술이 없다.");return false;}switch(name){case "침묵":c.Silenced=Math.Max(1,turns);break;case "망각":c.Forgotten=Math.Max(1,turns);break;case "트집":c.Tormented=true;break;case "회복봉인":c.HealBlock=turns>0?turns:5;break;case "사슬묶기":c.Disabled=turns>0?turns:4;c.DisabledMove=c.LastMove;break;case "검은눈빛":c.MeanLook=true;c.MeanLookTurns=turns>0?turns:0;break;case "조이기":c.Bound=turns>0?turns:4;break;default:return false;}if(log!=null)log.Add(target.Data.name+": "+name);return true;}
  static void ClearTurns(ref int turns,Fighter a,string name,List<string> log){if(turns<=0)return;turns=0;if(log!=null)log.Add(a.Data.name+"의 "+name+"이 풀렸다");}
  public static void LeaveField(Fighter a,List<string> log=null,Func<double> rng=null){var c=a.Conditions;MoveModules.Leave(a);c.ChoiceMove="";MiscModules.RestoreAbility(a,log);if(c.ExtensionMoves!=null)c.ExtensionMoves.Clear();if(c.Modifiers!=null&&c.Modifiers.Count>0){c.Modifiers.Clear();if(log!=null)log.Add(a.Data.name+"의 배율 효과가 풀렸다");}if(c.OriginalBase!=null){a.Data.@base=(int[])c.OriginalBase.Clone();c.OriginalBase=null;if(log!=null)log.Add(a.Data.name+"의 종족치가 원래대로 돌아갔다");}if(c.OriginalTypes!=null){a.Data.types=(string[])c.OriginalTypes.Clone();c.OriginalTypes=null;if(log!=null)log.Add(a.Data.name+"의 타입이 원래대로 돌아갔다");}bool wasOnField=a.OnField;ClearRollout(a,log);ClearEncore(a,log);if(wasOnField&&a.FieldOpponent!=null&&a.FieldOpponent.Conditions.AttractSide==a.FieldSide)ClearAttract(a.FieldOpponent,log);if(wasOnField&&a.Status=="잠듦")c.SleepTurns=c.SleepStart;ClearFlag(ref c.HelpingHand,a,"도우미",log);ClearFlag(ref c.Curled,a,"웅크리기",log);ClearAttract(a,log);ClearTurns(ref c.DeathCount,a,"죽음의 카운트",log);ClearTurns(ref c.RampageTurns,a,"난동부리기",log);c.RampageMove="";ClearMoveStates(a,log);ClearTurns(ref c.Silenced,a,"침묵",log);ClearTurns(ref c.Forgotten,a,"망각",log);ClearTurns(ref c.HealBlock,a,"회복봉인",log);ClearTurns(ref c.Disabled,a,"사슬묶기",log);ClearTurns(ref c.Bound,a,"조이기",log);ClearFlag(ref c.Tormented,a,"트집",log);ClearFlag(ref c.MeanLook,a,"검은눈빛",log);ClearFlag(ref c.Bomb,a,"폭탄",log);ClearTurns(ref c.Intercept,a,"요격",log);ClearTurns(ref c.Rage,a,"분노",log);ClearMindReader(a,log);c.BombSetTurn=c.MindReaderSetTurn=0;ClearFlag(ref c.AquaRing,a,"아쿠아링",log);ClearFlag(ref c.Recharge,a,"반동대기",log);c.DisabledMove=c.LastMove="";c.Confusion=c.Taunt=c.Substitute=c.ProtectChain=c.PriorityModifier=c.PriorityModifierTurns=0;c.Protect=c.Flinch=c.Seed=c.Curse=c.Ingrain=c.FlashFire=c.AbilityEntryApplied=c.EntryTriggered=false;c.ActFirst=0;a.Stages=new int[6];a.AccuracyStage=a.EvasionStage=a.CriticalStage=a.TrainerAccuracyStage=a.TrainerCriticalStage=0;a.AccuracyMultiplier=a.EvasionMultiplier=1;a.Multipliers=new[]{1d,1d,1d,1d,1d,1d};a.AbilitySuppressed=false;a.OnField=false;}
  public static void StruggleRecoil(Fighter a,Fighter b,Rules r,List<string> log){Hurt(a,b,r,Math.Max(1,Max(a,b,r)/4),"발버둥 반동",log,false);}
  public static void ApplyBomb(Fighter target,Rules r){target.Conditions.Bomb=true;target.Conditions.BombSetTurn=r.Turn;}
  public static void ApplyIntercept(Fighter target,int turns=1){target.Conditions.Intercept=Math.Max(1,turns);}
  public static void ApplyMindReader(Fighter target,int sourceSide,Rules r){if(sourceSide<0||sourceSide>1)return;target.Conditions.MindReaderSide=sourceSide;target.Conditions.MindReaderSetTurn=r.Turn;}
  public static void ClearMindReader(Fighter target,List<string> log){if(target.Conditions.MindReaderSide<0)return;target.Conditions.MindReaderSide=-1;target.Conditions.MindReaderSetTurn=0;if(log!=null)log.Add(target.Data.name+"의 마음의눈이 풀렸다");}
  public static void MindReaderHit(Fighter target,int sourceSide,List<string> log){if(sourceSide>=0&&sourceSide==target.Conditions.MindReaderSide)ClearMindReader(target,log);}
  public static bool MultiHit(Move move){return move!=null&&(move.tags??new string[0]).Contains("연속");}
  public static bool TripleHit(Move move){return MultiHit(move)&&(move.name.StartsWith("트리플킥")||move.name.StartsWith("트리플악셀"));}
  public static int HitCount(Move move,Func<double> rng,List<string> log){if(!MultiHit(move))return 1;string effect=move.effect??"";if(System.Text.RegularExpressions.Regex.IsMatch(effect,@"2\s*[～~∼–-]\s*5\s*회")){double roll=rng();return roll<.35?2:roll<.7?3:roll<.85?4:5;}var fixedHits=System.Text.RegularExpressions.Regex.Match(effect,@"(?<![0-9])([23])\s*회\s*「연속」");if(fixedHits.Success)return int.Parse(fixedHits.Groups[1].Value);log.Add(move.name+": 횟수 수동 판정 (1회)");return 1;}
  public static Move HitMove(Move move,int hit){if(!TripleHit(move))return move;var copy=move.Copy();copy.power=(move.name.StartsWith("트리플킥")?10:20)*hit;return copy;}
  public static bool HitCritical(Fighter a,Move move,Rules r,Func<double> rng){if(r.Critical)return true;if(MoveModules.AlwaysCrit(move))return true;int stage=Math.Max(0,Math.Min(4,a.CriticalStage+MiscModules.CritBonus(a,null,move,r)+MoveModules.CritBonus(move)));return rng()<1.0/new[]{16,8,4,3,2}[stage];}
  public static bool Ohko(Move move){return move!=null&&(new[]{"가위자르기","뿔드릴","땅가르기","절대영도"}.Contains(move.name)||(move.tags??new string[0]).Any(x=>x.Contains("일격필살")));}
  public static bool SureHit(Fighter target,Move move,int sourceSide){return sourceSide>=0&&target.Conditions.MindReaderSide==sourceSide||!Ohko(move)&&SureHit(move);}
  // 기술 전체의 본체 대미지를 받아 한 번만 반동을 처리한다.
  public static void AfterDamage(Fighter attacker,Fighter target,Move move,Rules r,int bodyDamage,List<string> log){if(bodyDamage>0&&move.category!="변화"&&target.Conditions.Intercept>0)Hurt(attacker,target,r,Math.Max(1,bodyDamage/2),"요격 반동",log);}
  static readonly Random StateRandom=new Random();
  static double Roll(Func<double> rng){return rng==null?StateRandom.NextDouble():rng();}
  static int SleepDuration(Func<double> rng){return 1+Math.Min(2,(int)(Roll(rng)*3));}
  public static string SleepBlock(Fighter f,Rules r){int side=PartySide(f,r);return side>=0&&(r.Parties[side]??new Fighter[0]).Count(x=>x!=null&&x.HP>0&&x.Status=="잠듦"&&!x.Conditions.SleepFromRest)>=2?"파티 잠듦 2체 제한: 더 잠들 수 없다":"";}
  public static int PartySide(Fighter f,Rules r){if(f.FieldSide>=0)return f.FieldSide;for(int side=0;side<2;side++)if((r.Parties[side]??new Fighter[0]).Any(x=>Object.ReferenceEquals(x,f)))return side;return -1;}
  // 같은 파티 멤버인가. 둘 다 id가 있으면 id로, 아니면 이름으로 비교한다(같은 종족 두 마리를 구분)
  public static bool SameMember(Pokemon a,Pokemon b){if(a==null||b==null)return false;if(!string.IsNullOrEmpty(a.id)&&!string.IsNullOrEmpty(b.id))return a.id==b.id;return a.name==b.name;}
  public static bool SameMember(Fighter a,Fighter b){return a!=null&&b!=null&&SameMember(a.Data,b.Data);}
  public static string MemberKey(Pokemon p){return p==null?"":!string.IsNullOrEmpty(p.id)?"#"+p.id:p.name;}
  public static void SetParties(Rules r,Fighter[] fighters,IEnumerable<Fighter>[] members=null){for(int side=0;side<2;side++){var all=members==null?r.Parties[side]:members[side];r.Parties[side]=new[]{fighters[side]}.Concat((all??Enumerable.Empty<Fighter>()).Where(x=>x!=null&&!SameMember(x,fighters[side]))).ToArray();foreach(var member in r.Parties[side])member.FieldSide=side;fighters[side].FieldOpponent=fighters[1-side];}}
  public static void ClearAttract(Fighter target,List<string> log){if(target.Conditions.AttractSide<0)return;target.Conditions.AttractSide=-1;if(log!=null)log.Add(target.Data.name+"의 헤롱헤롱이 풀렸다");}
  public static void ApplyHibernate(Fighter target,Rules r){target.Conditions.Hibernate=r.Turn+1;}
  public static bool IsRampage(Move m){return m!=null&&(m.tags??new string[0]).Contains("난동부리기");}
  public static void BeginRampage(Fighter a,Move m,List<string> log){if(!IsRampage(m)||a.Conditions.RampageTurns>0)return;a.Conditions.RampageTurns=2;a.Conditions.RampageMove=m.name;log.Add(a.Data.name+": 난동부리기 2T ("+m.name+")");}
  public static void FinishRampage(Fighter a,Func<double> rng,List<string> log){var c=a.Conditions;if(c.RampageTurns<=0)return;if(--c.RampageTurns==0){c.RampageMove="";c.Confusion=2+Math.Min(3,(int)(Roll(rng)*4));log.Add(a.Data.name+": 난동부리기가 끝나 혼란 "+c.Confusion+"T");}}
 }
}
