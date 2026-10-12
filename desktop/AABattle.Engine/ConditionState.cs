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
  public ConditionState Copy(){var copy=(ConditionState)MemberwiseClone();copy.DelayedAttacks=(DelayedAttacks??new List<DelayedAttackState>()).Where(x=>x!=null).Select(x=>x.Copy()).ToList();copy.Modifiers=(Modifiers??new List<ActiveModifier>()).Where(x=>x!=null).Select(x=>x.Copy()).ToList();copy.OriginalBase=OriginalBase==null?null:(int[])OriginalBase.Clone();return copy;}
  public IEnumerable<string> Labels(Fighter f){if(f.Status!="정상")yield return f.Status+(f.Status=="잠듦"?" "+SleepTurns:f.Status=="맹독"?" "+ToxicStage:"");if(Confusion>0)yield return "혼란 "+Confusion;if(Taunt>0)yield return "도발 "+Taunt;if(Protect)yield return "방어";if(Flinch)yield return "풀죽음";if(Recharge)yield return "반동대기";if(Substitute>0)yield return "대타 "+Substitute;if(Seed)yield return "씨뿌리기";if(Curse)yield return "저주";if(AquaRing)yield return "아쿠아링";if(Ingrain)yield return "뿌리박기";if(FlashFire)yield return "타오르는불꽃";if(Heated)yield return "가열";if(Chilled)yield return "냉각";if(Charged)yield return "충전";if(Dark)yield return "악인";if(Soaked)yield return "침수";if(Electrified)yield return "송전";if(HelpingHand)yield return "도우미";if(AttractSide>=0)yield return "헤롱헤롱";if(DeathCount>0)yield return "죽음의 카운트 "+DeathCount;if(Hibernate>0)yield return "동면";if(RampageTurns>0)yield return "난동부리기 "+RampageTurns+"T";if(Curled)yield return "웅크리기";if(!string.IsNullOrEmpty(RolloutMove))yield return RolloutMove+" 연속 명중 "+RolloutCount+"/5";if(EncoreTurns>0)yield return "앙코르 "+EncoreMove+" "+EncoreTurns+"T";if(Bomb)yield return "폭탄";if(Intercept>0)yield return "요격 "+Intercept+"T";if(Rage>0)yield return "분노 "+Rage+"T";if(MindReaderSide>=0)yield return "마음의눈";if(Silenced>0)yield return "침묵";if(Forgotten>0)yield return "망각 "+Forgotten+"T";if(Tormented)yield return "트집";if(HealBlock>0)yield return "회복봉인 "+HealBlock+"T";if(Disabled>0)yield return "사슬묶기("+DisabledMove+") "+Disabled+"T";if(MeanLook)yield return "검은눈빛";if(Bound>0)yield return "조이기 "+Bound+"T";if(PriorityModifier!=0)yield return "기술 우선도 "+(PriorityModifier>0?"+":"")+PriorityModifier+" (이번 T)";foreach(var attack in DelayedAttacks??new List<DelayedAttackState>())if(attack!=null&&attack.Move!=null)yield return attack.Move.name+" "+Math.Max(1,attack.TurnsRemaining)+"T";foreach(var m in Modifiers??new List<ActiveModifier>())if(m!=null)yield return "배율 "+m.Source+(m.Turns>0?" "+m.Turns+"T":"");if(OriginalBase!=null)yield return "종족치 변경";}
 }
}
