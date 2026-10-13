using System;
using System.Linq;
using System.Collections.Generic;

namespace AABattle {
 // 포켓몬별 상태변화 값과 특권 규칙
 public static class PrivilegeRules {
  public static bool Has(Fighter fighter,string name){return fighter!=null&&(fighter.Data.potentials??new Potential[0]).Any(p=>p!=null&&p.id=="특권"&&p.name==name);}
  public static string ContractType(Fighter fighter){var p=fighter==null?null:(fighter.Data.potentials??new Potential[0]).FirstOrDefault(x=>x!=null&&x.id=="특권"&&x.name.StartsWith("계약의 특권"));if(p==null)return "";var match=System.Text.RegularExpressions.Regex.Match(p.name,@"[（(](?<type>[^）)]+)[）)]");return match.Success?match.Groups["type"].Value.Trim():"";}
  public static int HpBonus(Fighter fighter,Rules rules){return Has(fighter,"주인의 특권")||Has(fighter,"왕자의 특권")?Math.Max(0,rules==null?10:rules.SlightSpeciesBonus):0;}
 }
 public class ConditionState {
  public int SleepTurns=2,SleepStart=2,ToxicStage=1,Confusion,Taunt,Substitute,ProtectChain,PriorityModifier,PriorityModifierTurns;
  public bool Protect,Flinch,Recharge,Seed,Curse,AquaRing,Ingrain,FlashFire,BalloonPopped,AbilityEntryApplied,RoleEntryApplied,EntryTriggered;public int ActFirst;
  public bool Heated,Chilled,Charged,Dark,Soaked,Electrified;
  public bool HelpingHand,Curled,SleepFromRest;public int AttractSide=-1,DeathCount,Hibernate,RampageTurns;public string RampageMove="";public int EncoreTurns;public string EncoreMove="";public int RolloutCount;public string RolloutMove="";
  public bool FocusSashUsed;public bool Bomb;public int Intercept,Rage,MindReaderSide=-1,BombSetTurn,MindReaderSetTurn;
  public int Silenced,Forgotten,HealBlock,Disabled,Bound;public bool Tormented,MeanLook;public string DisabledMove="",LastMove="";
  public List<DelayedAttackState> DelayedAttacks=new List<DelayedAttackState>();
  public List<ActiveModifier> Modifiers=new List<ActiveModifier>();   // 사건으로 걸린 수치 배율(PotentialModifiers)
  public int[] OriginalBase;                                           // 종족치 변경 전 값(교대하면 되돌린다)
  public string[] OriginalTypes;                                       // 포켓몬 타입 변경 전 값(교대하면 되돌린다)
  public List<TriggerFact> PrevTurnFacts=new List<TriggerFact>();       // 지난 T에 일어난 트리거 사건(「…한 다음 T 종료 시」)
  public List<TriggerFact> TurnFacts=new List<TriggerFact>();          // 이번 T에 일어난 트리거 사건(「…한 T 종료 시」)
  public int FieldTurn;                                                // 장소에 나온 뒤 몇 T째인가(등장한 T = 1T째, 인터벌 등장은 다음 T가 1T째)
  public EntryReason PendingEntry=EntryReason.Lead;
  public List<string> ExtensionMoves=new List<string>();               // 사건으로 얻은 기능확장 기술(장소를 떠날 때까지)
  public string AbilityBefore;public int AbilityTurns;                    // 특성 변경 전 특성 / 남은 T(0 = 장소를 떠날 때까지)
  public int ActionOrder;public bool PendingAction;                      // 이번 T에 몇 번째로 행동했나(0 = 아직, 교대는 행동이 아니다) / 아직 기술 행동이 남았나(행동권)
  public string ChargeMove="",Hidden="";public bool HeldInSky;       // 모으기 중인 기술 / 숨은 곳(하늘 위·땅속…) / 프리폴로 붙잡힘
  public string ProtectMove="";public bool Enduring;public int SoloChain;  // 「방어」 상태를 만든 기술(막는 범위·반응) / 버티기 / 「별도 판정」 연속 사용 수
  public string Selected="";                                          // 이번 T에 고른 기술의 분류(물리·특수·변화, 교대면 "")
  public string[] AddedTypes=new string[0];                            // 특성 「타입 추가」로 붙인 타입(특성이 바뀌면 뗀다)
  public bool PiercingAbility,Acting;                                 // 이번 기술이 〔방호 특성〕을 관통한다 / 지금 기술을 내는 중(틀깨기 판정)                                        // 이번 기술이 〔방호 특성〕을 관통한다
  public int TakenPhysical,TakenSpecial;                               // 이번 T에 상대 공격으로 받은 물리·특수 대미지(카운터·미러코트·메탈버스트)
  public bool Minimized,FocusEnergy,MagicCoat,DestinyBond,Nightmare,Grudge,Powder;public string Foresight="",Sealed="";  // 새 상태(StateEffectsMore)
  public int Yawn,MagnetRise,Telekinesis,Embargo,Stockpile;
  public int UproarTurns,BideTurns,BideStored;public string UproarMove="",BideMove="";   // 소란(남은 T/자동 행동 기술) · 참기(남은 T/모은 대미지/기술)
  public int LaserFocus,RolledPower,TimesHit;public bool FollowMe,Quash;  // 예민해지기(남은 T) / 이번 랜덤 위력 / 공격을 받은 횟수(분노의주먹) / 날따름·순서미루기(싱글은 효과 없음)
  public int MeanLookTurns;                                            // 검은눈빛 남은 T(0 = 장소를 떠날 때까지)
  public int BindDivisor;                                              // 조이기 대미지 분모(0 = 8)
  public string RepeatMove="";public int RepeatCount;                  // 같은 기술 연속 사용(메트로놈)
  public int NextPriority;                                             // 다음 T 기술 우선도(애슈열매)
  public bool StageByOpponent;                                           // 직전 랭크 변화가 상대에 의한 것(주눅구슬 등)
  public string[] ItemTypeBackup;                                      // 소지품 플레이트로 타입이 바뀌기 전 값
  public HashSet<string> GuardUsed=new HashSet<string>();               // 이미 쓴 「1번만」 방호 문장(특성)
  public bool TruantRest;                                              // 다음 행동은 쉰다(게으름)
  public bool Gulping;                                                 // 먹이를 문 상태(그대로꿀꺽미사일)
  public Move ActingMove;                                              // 지금 내는 기술(조건부 〔방호 특성〕 관통 판정)
  public int LastDamageTaken;                                          // 마지막으로 공격에서 받은 대미지(내용물분출)
  public bool MovedSinceEntry;                                         // 장소에 나온 뒤 기술을 한 번이라도 내보냈는가(떠나면 초기화)
  public string ChoiceMove="";                                          // 구애: 장소에 나와 처음 내보낸 기술(떠나면 해제)
  public bool NotAttacking;                                            // 이번 T에 공격기술을 고르지 않았다(교대·변화기술 포함, 확정 12)
  public string WatchTypes;public int Repay;                           // 타입 변화 비교 기준 / 턴 끝에 반납할 회복량(체력 가득 회복 포텐셜)
  public string WatchStates,WatchStages;public int WatchHP=-1;                 // StateWatch 비교 기준(등장할 때 맞춘다)                    // 효과 범위 밖에서 나온 포켓몬의 등장 방식(다음 T 처음에 처리)
  public ConditionState Copy(){var copy=(ConditionState)MemberwiseClone();copy.DelayedAttacks=(DelayedAttacks??new List<DelayedAttackState>()).Where(x=>x!=null).Select(x=>x.Copy()).ToList();copy.Modifiers=(Modifiers??new List<ActiveModifier>()).Where(x=>x!=null).Select(x=>x.Copy()).ToList();copy.OriginalBase=OriginalBase==null?null:(int[])OriginalBase.Clone();copy.OriginalTypes=OriginalTypes==null?null:(string[])OriginalTypes.Clone();copy.GuardUsed=new HashSet<string>(GuardUsed??new HashSet<string>());copy.TurnFacts=new List<TriggerFact>(TurnFacts??new List<TriggerFact>());copy.PrevTurnFacts=new List<TriggerFact>(PrevTurnFacts??new List<TriggerFact>());copy.ExtensionMoves=new List<string>(ExtensionMoves??new List<string>());return copy;}
  public IEnumerable<string> Labels(Fighter f){if(f.Status!="정상")yield return f.Status+(f.Status=="잠듦"?" "+SleepTurns:f.Status=="맹독"?" "+ToxicStage:"");if(Gulping)yield return "먹이를 문";if(Confusion>0)yield return "혼란 "+Confusion;if(UproarTurns>0)yield return "소란 "+UproarTurns;if(BideMove.Length>0)yield return "참기 "+BideStored;if(Taunt>0)yield return "도발 "+Taunt;if(Protect)yield return "방어";if(Enduring)yield return "버티기";if(Minimized)yield return "작아지기";if(Foresight.Length>0)yield return "꿰뚫어보기";if(Yawn>0)yield return "하품";if(FocusEnergy)yield return "기충전";if(MagicCoat)yield return "매직코트";if(DestinyBond)yield return "길동무";if(Nightmare)yield return "악몽";if(Grudge)yield return "원념";if(Powder)yield return "분진";if(MagnetRise>0)yield return "전자부유 "+MagnetRise+"T";if(Telekinesis>0)yield return "텔레키네시스 "+Telekinesis+"T";if(Embargo>0)yield return "금제 "+Embargo+"T";if(Stockpile>0)yield return "비축하기 "+Stockpile;if(Sealed.Length>0)yield return "봉인("+Sealed+")";if(LaserFocus>0)yield return "예민해지기";if(FollowMe)yield return "날따름";if(Quash)yield return "순서미루기";if(Flinch)yield return "풀죽음";if(Recharge)yield return "반동대기";if(Substitute>0)yield return "대타 "+Substitute;if(Seed)yield return "씨뿌리기";if(Curse)yield return "저주";if(AquaRing)yield return "아쿠아링";if(Ingrain)yield return "뿌리박기";if(FlashFire)yield return "타오르는불꽃";if(Heated)yield return "가열";if(Chilled)yield return "냉각";if(Charged)yield return "충전";if(Dark)yield return "악인";if(Soaked)yield return "침수";if(Electrified)yield return "송전";if(HelpingHand)yield return "도우미";if(AttractSide>=0)yield return "헤롱헤롱";if(DeathCount>0)yield return "죽음의 카운트 "+DeathCount;if(Hibernate>0)yield return "동면";if(RampageTurns>0)yield return "난동부리기 "+RampageTurns+"T";if(Curled)yield return "웅크리기";if(!string.IsNullOrEmpty(RolloutMove))yield return RolloutMove+" 연속 명중 "+RolloutCount+"/5";if(EncoreTurns>0)yield return "앙코르 "+EncoreMove+" "+EncoreTurns+"T";if(Bomb)yield return "폭탄";if(Intercept>0)yield return "요격 "+Intercept+"T";if(Rage>0)yield return "분노 "+Rage+"T";if(MindReaderSide>=0)yield return "마음의눈";if(Silenced>0)yield return "침묵";if(Forgotten>0)yield return "망각 "+Forgotten+"T";if(Tormented)yield return "트집";if(HealBlock>0)yield return "회복봉인 "+HealBlock+"T";if(Disabled>0)yield return "사슬묶기("+DisabledMove+") "+Disabled+"T";if(MeanLook)yield return "검은눈빛";if(Bound>0)yield return "조이기 "+Bound+"T";if(PriorityModifier!=0)yield return "기술 우선도 "+(PriorityModifier>0?"+":"")+PriorityModifier+" (이번 T)";foreach(var attack in DelayedAttacks??new List<DelayedAttackState>())if(attack!=null&&attack.Move!=null)yield return attack.Move.name+" "+Math.Max(1,attack.TurnsRemaining)+"T";foreach(var m in Modifiers??new List<ActiveModifier>())if(m!=null)yield return "배율 "+m.Source+(m.Turns>0?" "+m.Turns+"T":"");if(ChargeMove.Length>0)yield return "모으기 "+ChargeMove;if(Hidden.Length>0)yield return Hidden;if(NextPriority!=0)yield return "다음 T 우선도 +"+NextPriority;if(OriginalBase!=null)yield return "종족치 변경";}
 }
}
