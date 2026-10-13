using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Globalization;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace AABattle {
 // 표준 포텐셜 템플릿·선택지와 편집 데이터 → 배틀 데이터 변환
 public class StructuredPotentialTemplate {
  public string trigger,effect,qualification;
  public StructuredPotentialTemplate(string t,string e,string q=""){trigger=t??"";effect=e??"";qualification=q??"";}
  public string EffectText {get{return effect+(qualification.Length==0?"":" "+qualification);}}
  public string Text {get{return trigger.Length==0?EffectText:trigger+", "+EffectText;}}
  public static StructuredPotentialTemplate EntryRandomStat(){return new StructuredPotentialTemplate(TriggerStructures.EntryLabel,EffectNormalizer.CanonicalRank("자신의 임의의 능력",1));}
  public static StructuredPotentialTemplate WildRandomStat(){return new StructuredPotentialTemplate(TriggerStructures.EntryLabel,EffectNormalizer.CanonicalRank("자신의 임의의 능력",2));}
  public static StructuredPotentialTemplate WildAllStats(){return new StructuredPotentialTemplate(TriggerStructures.EntryLabel,EffectNormalizer.CanonicalRank("자신의 전능력치",2));}
  public static StructuredPotentialTemplate SelfAndOpponentEntry(string effect,string qualification=""){return new StructuredPotentialTemplate(TriggerStructures.SelfAndOpponentEntryLabel,effect,qualification);}
  public static StructuredPotentialTemplate Leave(string effect,string qualification=""){return new StructuredPotentialTemplate(TriggerStructures.LeaveLabel,effect,qualification);}
  public static StructuredPotentialTemplate HunterWildBossStatIgnore(){return new StructuredPotentialTemplate(TriggerStructures.FieldLabel,EffectNormalizer.CanonicalStatIgnore("『야생의 두목』","능력","상승(강화)"),"(필요 「통솔：A」 이상)");}
  public static StructuredPotentialTemplate FieldSwitchRestriction(){return new StructuredPotentialTemplate(TriggerStructures.FieldLabel,EffectNormalizer.CanonicalSwitchRestriction(),"(필요 「통솔：A」 이상)");}
 }
 public class RolePotentialOption {
  public string name,effect,adjunct,adjunctEffect,category;public StructuredPotentialTemplate template;
  public RolePotentialOption(string n,string e):this(n,e,"",""){}
  public RolePotentialOption(string n,string e,string a,string ae){name=n;effect=e;adjunct=a;adjunctEffect=ae;category="";}
  public RolePotentialOption(string n,string e,string a,string ae,string c):this(n,e,a,ae){category=c??"";}
  public RolePotentialOption(string n,StructuredPotentialTemplate t,string a,string ae):this(n,t==null?"":t.Text,a,ae){template=t;}
  public RolePotentialOption(string n,StructuredPotentialTemplate t,string a,string ae,string c):this(n,t,a,ae){category=c??"";}
 }
 public class CatalogEntry {public string label,value;public CatalogEntry(string l,string v){label=l;value=v;}public override string ToString(){return label;}}
 public static class EditorBridge {
 public static Pokemon ToPokemon(PokemonRecord source,Database db,out string warning){if(source==null)throw new Exception("포켓몬을 선택하세요.");int[] bases={source.stats.hp,source.stats.attack,source.stats.defense,source.stats.spAttack,source.stats.spDefense,source.stats.speed};if(bases.Any(x=>x<=0))throw new Exception("6개 종족값을 모두 1 이상 입력하세요.");var types=source.types.Where(x=>db.types.Contains(x)).Distinct().ToArray();var badTypes=source.types.Where(x=>!db.types.Contains(x)).Distinct().ToArray();if(types.Length==0)throw new Exception("등록된 타입을 하나 이상 입력하세요."+(badTypes.Length>0?" 미등록: "+string.Join(", ",badTypes):""));var moves=source.moves.Where(x=>db.moves.Any(m=>m.name==x)).Distinct().ToArray();var badMoves=source.moves.Where(x=>!db.moves.Any(m=>m.name==x)).Distinct().ToArray();if(moves.Length==0)throw new Exception("계산기에 등록된 기술을 하나 이상 입력하세요."+(badMoves.Length>0?" 미등록: "+string.Join(", ",badMoves):""));warning=(badTypes.Length>0?"미등록 타입 제외: "+string.Join(", ",badTypes)+". ":"")+(badMoves.Length>0?"미등록 기술 제외: "+string.Join(", ",badMoves)+".":"");return new Pokemon{id=source.id??"",ain=source.ain,gender=Genders.Normalize(source.gender),team="팀 편집기",name=source.name.Length>0?source.name:"이름 없는 포켓몬",ability=source.ability,item=source.item,level=source.level,types=types,moves=moves,@base=bases,iv=new[]{31,31,31,31,31,31},potentials=source.potentials.Where(x=>x.enabled&&x.name.Length>0).Select(x=>new Potential{id=x.slot,name=x.slot=="특권"&&x.name=="계약의 특권"&&x.targetType.Length>0?x.name+" ("+x.targetType+")":x.name,description=x.effect.Length>0?x.effect:x.raw,activation=x.activation,uses=x.uses,unlocks=x.unlocks??new string[0],triggers=x.trigger.Length>0?new[]{x.trigger}:new string[0]}).ToArray()};}
   public static Pokemon ToPokemon(PokemonRecord source,Database db,out string warning,TrainerRecord trainer,TrainerBattleState state=null){var result=ToPokemon(source,db,out warning);var records=source.potentials??new List<PotentialRecord>();result.potentials=result.potentials.Where(x=>TrainerAccessRules.CanUsePotentialSlot(x.id,trainer,state,records)).ToArray();return result;}
   public static int[] PreviewStats(PokemonRecord p){int l=Math.Max(1,p.level);int[] b={p.stats.hp,p.stats.attack,p.stats.defense,p.stats.spAttack,p.stats.spDefense,p.stats.speed};return b.Select((x,i)=>i==0?(int)Math.Floor((x*2+31)*l/100.0)+l+10:(int)Math.Floor((x*2+31)*l/100.0)+5).ToArray();}
 }
 static class EditorTemplate {
  public static readonly string[] TrainerSlotNames={"고유","지령"};
  public static readonly string[] PokemonSlotNames={"역할","분류","주인","이명","종족 ①","종족 ②","종족 ③","종족 ④","속별","유대","선","회피","내성","격","범용","부수","특권","PT ①","PT ②"};
  // 편집 화면의 「포텐셜 종류」 선택지. 전용은 포켓몬 편집에서 단다(트레이너 쪽 기존 전용은 불러와 그대로 동작한다)
  public static readonly string[] TrainerEditSlotNames={"고유","지령"};
  public static readonly string[] PokemonEditSlotNames=PokemonSlotNames.Concat(new[]{TeamRules.ExclusiveSlot}).ToArray();
  public static readonly RolePotentialOption[] RolePotentials={
   new RolePotentialOption("선발","선발로 필드에 나오면, 자신의 전능력치를 강화(1.1배)한다."),
   new RolePotentialOption("귀인","선발로 필드에 나오면, 자신의 「공격」이 오른다."),new RolePotentialOption("경화","선발로 필드에 나오면, 자신의 「방어」가 오른다."),
   new RolePotentialOption("마술사","선발로 필드에 나오면, 자신의 「특공」이 오른다."),new RolePotentialOption("현자","선발로 필드에 나오면, 자신의 「특방」이 오른다."),
   new RolePotentialOption("광주","선발로 필드에 나오면, 자신의 「속도」가 오른다."),new RolePotentialOption("매의 눈","선발로 필드에 나오면, 자신의 「명중」이 오른다."),
   new RolePotentialOption("심안","선발로 필드에 나오면, 자신의 「회피」가 오른다."),new RolePotentialOption("사냥꾼","선발로 필드에 나오면, 자신의 「C」가 오른다."),
   new RolePotentialOption("톱 배터","선발로 필드에 나오면, 자신의 임의의 능력을 크게 올린다."),
   new RolePotentialOption("에이스","「여기다！」일 때, 자신의 전능력치를 강화(1.33배)하고 기술이 급소에 맞기 쉬워진다. (C+1)"),
   new RolePotentialOption("부동의 에이스","「여기다！」일 때, 자신의 전능력치가 상승하고 기술의 크리티컬율이 오른다."),
   new RolePotentialOption("선구의 에이스","선발로 필드에 나오면, 자신의 전능력치를 크게 올린다."),
   new RolePotentialOption("에이스 어시스트","아군 『에이스』와 교대할 때, 『에이스』의 임의의 능력을 올린다."),
   new RolePotentialOption("에이스 어시스트α","필드를 떠날 때, 아군 『에이스』가 처음으로 내보내는 기술의 위력을 2배로 한다."),
   new RolePotentialOption("에이스 어시스트β","필드를 떠날 때, 아군 『에이스』가 필드에 나와 처음으로 받는 대미지를 반감한다."),
   new RolePotentialOption("에이스 어시스트γ","필드를 떠날 때, 아군 『에이스』의 임의의 능력을 올린다."),
   new RolePotentialOption("에이스 킬러","『에이스』에게 주는 대미지를 강화(1.5배)한다."),
   new RolePotentialOption("에이스 킬러α","『에이스』에게 주는 대미지를 강화(1.5배)하고 『에이스』의 「방/특방」 상승(강화)을 무시한다."),
   new RolePotentialOption("에이스 킬러β","『에이스』로부터 받는 대미지를 반감하고 『에이스』의 「공/특공」 상승(강화)을 무시한다."),
   new RolePotentialOption("에이스 킬러γ","적진에 『에이스』가 있을 때, 자신의 「속도」를 2배로 하고 『에이스』의 「속도/회피」 상승(강화)을 무시한다."),
   new RolePotentialOption("양대간판","자신의 전능력치를 강화(1.33배)하고 기술이 급소에 맞기 쉬워진다. (C+1)"),
   new RolePotentialOption("어벤저","죽어내밀기로 필드에 나왔을 때, 자신의 임의의 능력을 크게 올린다."),
   new RolePotentialOption("인챈터","필드를 떠날 때, 아군의 임의의 능력을 올릴 수 있다."),
   new RolePotentialOption("스카우터","필드에 있는 동안, 트레이너의 지시 전에 상대의 데이터를 해석할 수 있다."),
   new RolePotentialOption("체이서","자신의 공격 기술에 「추격」 효과를 부여한다."),
   new RolePotentialOption("엠퍼러","필드에 나왔을 때, 상대의 임의의 능력을 내리고 상대의 전능력치를 저하(0.85배)시킨다."),
   new RolePotentialOption("타이트 엔드","『선발』『킬러』『어시스트』의 『역할』을 갖는다."),
   new RolePotentialOption("타이트 엔드α","『선발』『킬러α』『어시스트α』의 『역할』을 갖는다."),new RolePotentialOption("타이트 엔드β","『선발』『킬러β』『어시스트β』의 『역할』을 갖는다."),new RolePotentialOption("타이트 엔드γ","『선발』『킬러γ』『어시스트γ』의 『역할』을 갖는다."),
   new RolePotentialOption("베테랑","트레이너에게 「타임」을 사용하게 할 수 있다. 독자적으로 전용 포텐셜을 보유한다."),
   new RolePotentialOption("야생 포켓몬",StructuredPotentialTemplate.WildRandomStat(),"","","야생"),new RolePotentialOption("야생의 두목",StructuredPotentialTemplate.WildAllStats(),"","","야생"),
   new RolePotentialOption("어드벤처","던전어택 시, 필드에 나올 때 자신의 임의의 능력을 크게 올린다."),
    new RolePotentialOption("탐사대원",StructuredPotentialTemplate.EntryRandomStat(),"프라이멀 헌트","상대의 『투쟁본능』을 무효화한다."),
   new RolePotentialOption("탐사대장","필드에 나오면 자신의 전능력치가 오른다.","프라이멀 헌트","상대의 『투쟁본능』을 무효화한다."),
   new RolePotentialOption("수렵대원",StructuredPotentialTemplate.HunterWildBossStatIgnore(),"와일드 헌트","상대의 「공격을 회피한다」 효과를 가진 포텐셜을 무효화한다."),
   new RolePotentialOption("보급대원",StructuredPotentialTemplate.Leave("아군의 소모(파괴)된 도구를 「리사이클」할 수 있다.","(필요 「통솔：A」 이상)"),"브레이크 헌트","아군과 임의교대할 때, 중확률로 상대의 소지품을 파괴한다."),
   new RolePotentialOption("구급대원",StructuredPotentialTemplate.Leave("아군의 체력을 1/4 회복해 상태이상을 해제한다.","(필요 「통솔：A」 이상)"),"레스큐 헌트",TriggerStructures.LeaveLabel+", 아군의 랜덤한 능력치를 올린다."),
    new RolePotentialOption("추적대원",StructuredPotentialTemplate.FieldSwitchRestriction(),"새비지 헌트","상대의 『주인의 호령』 발동 시, 상대보다 먼저 행동한다."),
   new RolePotentialOption("조사대원",StructuredPotentialTemplate.SelfAndOpponentEntry(EffectNormalizer.CanonicalAnalysisPermission(),"(필요 「통솔：A」 이상)"),"리서치 헌트","PT에 참가하고 있을 때, 상대가 「선행입력」한 포텐셜의 상세를 확인할 수 있다.")
  };
  public static readonly RolePotentialOption[] CommonPotentials={
   new RolePotentialOption("기합","드물게 기합으로 상대의 공격을 버틴다. 남은 체력이 많을수록 발동하기 어렵다."),
   new RolePotentialOption("초근성","중확률로 상대의 공격 이외의 대미지를 무시한다."),
   new RolePotentialOption("전투속행","저확률로 상대의 기술의 대미지를 자신의 행동 후로 미룬다."),
   new RolePotentialOption("진지작성","자진에 전개하는 기술의 지속 T 수를 ＋1한다."),
   new RolePotentialOption("군략","자신의 「전체기술」의 위력을 강화(1.2배)하고, 상대의 「전체기술」의 위력을 저하(0.8배)시킨다."),
   new RolePotentialOption("초기합","드물게 기합으로 상대의 공격을 버틴다. 중확률로 공격 이외의 대미지를 무시한다.")
  };
  public static readonly RolePotentialOption[] InitiativePotentials={
   new RolePotentialOption("선의 선","상대가 우선도 ＋1 이상의 기술을 사용했을 때, 저확률로 먼저 행동한다."),
   new RolePotentialOption("대의 선","상대의 「속도」의 종족치가 자신과 동랭크일 때, 중확률로 자신의 기술의 우선도를 ＋1한다."),
   new RolePotentialOption("후의 선","상대가 자신보다 먼저 행동했을 때, 드물게 상대보다 먼저 행동할 수 있다."),
   new RolePotentialOption("삼종의 신기","고도의 육성으로 인해 『선의 선』『대의 선』『후의 선』을 모두 극에 달하고 있다. (필요 「육성：AA」 이상)")
  };
  public static readonly RolePotentialOption[] PrivilegePotentials={
   new RolePotentialOption("익스펜션（노말）","자신의 타입 불일치인 기술의 위력을 강화(1.5배)한다."),
   new RolePotentialOption("익스펜션（불꽃）","「얼음」상태가 되지 않는다.\r\n자신의 공격기술에 「1할：화상」의 추가효과를 부여한다."),
   new RolePotentialOption("익스펜션（물）","중확률로 자신의 상태이상을 치유한다."),new RolePotentialOption("익스펜션（풀）","취득하는 경험치량이 증가한다."),new RolePotentialOption("익스펜션（전기）","자신의 공격기술에 「1할：마비」의 추가효과를 부여한다."),
   new RolePotentialOption("익스펜션（얼음）","「화상」상태가 되지 않는다.\r\n자신의 공격기술에 「1할：얼음」의 추가효과를 부여한다."),new RolePotentialOption("익스펜션（격투）","자신의 「격투」기술이 상대의 방호 포텐셜을 무시한다."),new RolePotentialOption("익스펜션（독）","상대가 받는 「독」상태의 대미지가 증가한다. (1/8 → 1/6)"),new RolePotentialOption("익스펜션（땅）","설치기술의 대미지를 무시한다."),
   new RolePotentialOption("익스펜션（비행）","필드의 날씨 디메리트를 받지 않는다.\r\n상대로부터의 강제교대를 받지 않는다."),new RolePotentialOption("익스펜션（에스퍼）","「혼란」상태가 되지 않는다.\r\n자신의 공격기술에 「1할：혼란」의 추가효과를 부여한다."),new RolePotentialOption("익스펜션（벌레）","자신의 「벌레」기술이 급소에 맞기 쉬워진다. (C+1)"),new RolePotentialOption("익스펜션（바위）","「풀죽음」상태가 되지 않는다."),new RolePotentialOption("익스펜션（고스트）","자신의 「고스트」기술이 상대의 「방어」등의 기술을 관통한다."),new RolePotentialOption("익스펜션（드래곤）","자신의 「체력」종족치가 1랭크 오른다."),new RolePotentialOption("익스펜션（악）","상대의 기술의 「추격」효과를 받지 않는다."),new RolePotentialOption("익스펜션（강철）","상대의 「악」「고스트」기술이 「효과가 별로」가 된다."),
   new RolePotentialOption("익스펜션（페어리）","상대에 의해 「잠듦」상태가 되지 않는다.\r\n자신의 공격기술에 「1할：잠듦」의 추가효과를 부여한다."),new RolePotentialOption("계약의 특권","『계약의 특권』이 부여되어 있을 때, 상대의 「○」기술의 대미지를 반감한다."),new RolePotentialOption("주인의 특권","자신의 「체력」종족치가 조금 오른다."),new RolePotentialOption("왕자의 특권","자신의 「체력」종족치가 조금 오른다.\r\n상대로부터의 상태이상을 받지 않는다.")
  };
  // 유대: 지시 랭크 상승(상시) + 재사용 권한/재발동. 재판정은 T 개시 시(지시·지령 처리 직후)
  static RolePotentialOption Bond(string name, string rise, string reroll) {
   string text = (rise.Length == 0 ? "" : rise + "\r\n") + reroll;
   return new RolePotentialOption(name, new StructuredPotentialTemplate(TriggerStructures.TurnStartLabel, text), "", "");
  }
  static readonly string BondRise = BondModules.RiseSentence(1);
  static readonly string BondRegain = "저확률로 " + BondModules.RegainSentence + ".";
  static readonly string BondReplay = "저확률로 " + BondModules.ReplaySentence + ".";
  public static readonly RolePotentialOption[] BondPotentials={
   Bond("영혼의 유대", BondRise, BondRegain), Bond("사랑의 유대", BondRise, BondRegain), Bond("왕의 유대", BondRise, BondReplay),
   Bond("뜨거운 유대", "자신의 모든 종족치에 「++」를 부여한다.", BondRegain),
   Bond("불타는 유대", BondRise, BondReplay), Bond("얼어붙는 유대", BondRise, BondReplay), Bond("노래의 유대", BondRise, BondReplay), Bond("꿈의 유대", BondRise, BondReplay)
  };
  public static RolePotentialOption Role(string name){return RolePotentials.FirstOrDefault(x=>x.name==name);}
  public static RolePotentialOption[] FixedOptions(string slot){return slot=="역할"?RolePotentials:slot=="범용"?CommonPotentials:slot=="선"?InitiativePotentials:slot=="특권"?PrivilegePotentials:slot=="유대"?BondPotentials:null;}
  public static RolePotentialOption FixedOption(string slot,string name){var options=FixedOptions(slot);return options==null?null:options.FirstOrDefault(x=>x.name==name);}
  static readonly string[][] CounterTypes={new[]{"불꽃","화"},new[]{"물","수"},new[]{"풀","초"},new[]{"격투","투"},new[]{"얼음","빙"},new[]{"땅","지"},new[]{"고스트","령"},new[]{"바위","암"},new[]{"전기","전"},new[]{"강철","강"},new[]{"독","독"},new[]{"벌레","충"},new[]{"비행","비"},new[]{"에스퍼","염"},new[]{"악","악"},new[]{"드래곤","룡"},new[]{"페어리","요"}};
  public static string[] CounterTypeNames(){return CounterTypes.Select(x=>x[0]).ToArray();}
  public static bool IsCounterSlot(string slot){return slot=="회피"||slot=="내성"||slot=="격";}
  public static bool IsTypedPotential(string slot,string name){return IsCounterSlot(slot)||slot=="특권"&&name=="계약의 특권";}
  static string CounterCode(string type){var pair=CounterTypes.FirstOrDefault(x=>x[0]==type);return pair==null?"":pair[1];}
  public static string InferCounterType(string name){var pair=CounterTypes.FirstOrDefault(x=>name.StartsWith("대"+x[1])||name.StartsWith("극"+x[1]));return pair==null?"":pair[0];}
  public static RolePotentialOption[] CounterOptions(string slot,string type){string code=CounterCode(type);if(code.Length==0||!IsCounterSlot(slot))return new RolePotentialOption[0];var list=new List<RolePotentialOption>();if(slot=="회피"){list.Add(new RolePotentialOption("대"+code+"회피","적진에 「"+type+"」 포켓몬이 있을 때, 상대의 「"+type+"」 기술의 명중률을 감소(0.85배)시킨다."));list.Add(new RolePotentialOption("극"+code+"회피","상대의 「"+type+"」 기술을 저확률로 회피한다. (필요 「육성：AA」 이상)"));}else if(slot=="내성"){list.Add(new RolePotentialOption("대"+code+"내성","적진에 「"+type+"」 포켓몬이 있을 때, 상대의 「"+type+"」 기술의 대미지를 완화(0.67배)한다."));list.Add(new RolePotentialOption("극"+code+"내성","상대의 「"+type+"」 기술의 대미지를 반감한다. (필요 「육성：AA」 이상)"));}else foreach(string grade in new[]{"대","극"})foreach(string kind in new[]{"저격","추격","박격","반격"}){string chance=grade=="대"?"저확률":"중확률";string effect=kind=="저격"?chance+"로 자신의 기술이 「필중」한다.":kind=="추격"?chance+"로 자신의 기술에 「추격」 효과를 부여한다.":kind=="박격"?chance+"로 자신의 기술의 대미지가 2배가 된다.":chance+"로 가장 마지막에 추가행동을 얻는다.";effect="적진에 「"+type+"」 포켓몬이 있을 때, "+effect+(grade=="극"?" (필요 「육성：AA」 이상)":"");list.Add(new RolePotentialOption(grade+code+kind,effect));}return list.ToArray();}
  public static RolePotentialOption CounterOption(string slot,string type,string name){return CounterOptions(slot,type).FirstOrDefault(x=>x.name==name);}
  public static readonly string[] PotentialTriggerCatalog={TriggerStructures.BattleStartLabel,TriggerStructures.EntryLabel,TriggerStructures.OpponentEntryLabel,TriggerStructures.VoluntaryEntryLabel,TriggerStructures.ReplacementEntryLabel,TriggerStructures.MoveUseLabel,TriggerStructures.AttackMoveUseLabel,TriggerStructures.StatusMoveUseLabel,TriggerStructures.AttackSuccessLabel,TriggerStructures.ReceivedLabel,TriggerStructures.DamagedLabel,TriggerStructures.DefeatLabel,"빈사 상태가 되었을 때",TriggerStructures.SwitchLabel,TriggerStructures.LeaveLabel,TriggerStructures.TurnStartLabel,TriggerStructures.TurnLastLabel,TriggerStructures.TurnEndLabel,TriggerStructures.HereLabel,TriggerStructures.SelfOrderLabel,TriggerStructures.AllyOrderLabel,TriggerStructures.AnalyzedLabel,TriggerStructures.Granted("계약의 특권"),TriggerStructures.MoveType("불꽃"),TriggerStructures.MoveName("파괴광선"),"상대가 교대할 때","자신의 체력이 1/2 이하가 되었을 때"};
  public static CatalogEntry[] PotentialEffectCatalog(){var fixedEntries=RolePotentials.Concat(CommonPotentials).Concat(InitiativePotentials).Concat(PrivilegePotentials).Select(x=>new CatalogEntry("『"+x.name+"』 "+x.effect,x.effect));var adjunctEntries=RolePotentials.Where(x=>x.adjunct.Length>0).Select(x=>new CatalogEntry("『"+x.adjunct+"』 "+x.adjunctEffect,x.adjunctEffect));var counterEntries=CounterTypeNames().SelectMany(type=>new[]{"회피","내성","격"}.SelectMany(slot=>CounterOptions(slot,type))).Select(x=>new CatalogEntry("『"+x.name+"』 "+x.effect,x.effect));var directives=Orders(false).Concat(Orders(true)).Concat(ExtendedOrders(false)).Concat(ExtendedOrders(true)).GroupBy(x=>x.name+"\n"+x.effect).Select(x=>x.First()).Select(x=>new CatalogEntry("『"+x.name+"』 "+x.effect,x.effect));return fixedEntries.Concat(adjunctEntries).Concat(counterEntries).Concat(directives).GroupBy(x=>x.label).Select(x=>x.First()).ToArray();}
  static PotentialRecord P(string name,string activation,string uses,string trigger,string effect){return new PotentialRecord{slot="지령",name=name,activation=activation,uses=uses,trigger=trigger,effect=effect,raw=(uses.Length>0?uses+"/":"")+(activation.Length>0?activation+"/":"")+effect};}
  public static List<PotentialRecord> BaseOrders(){return new List<PotentialRecord>{P("물러나！","선행","1/시","상대가 아군 1체에게 물리 기술을 사용할 때","그 한 번의 물리 공격을 회피시킨다."),P("피해라！","선행","1/시","상대가 아군 1체에게 특수 기술을 사용할 때","그 한 번의 특수 공격을 회피시킨다."),P("돌아와！","임의","1/시","아군이 통상 교대할 때","턴 초반 아군을 볼로 되돌려 상대의 대상 지정 행동을 피하고, 턴 마지막 교대 시점에 새 아군을 필드에 낸다."),P("버텨라！","임의","1/시","아군 1체가 상대 기술의 대미지를 받을 때","그 한 번의 대미지를 반감하고 반드시 버틴다.")};}
  static readonly string[] Stats={"공격","방어","특공","특방","민첩","회복"};
  public static List<PotentialRecord> Orders(bool deploy){var list=BaseOrders();foreach(string stat in Stats){string target=stat=="민첩"?"속도":stat;string name=(deploy?"전개 ":"숙달 ")+stat+" 지령";string effect=stat=="회복"?(deploy?"행동을 스킵하고 아군 1체의 체력을 1/2 회복한다.":"아군 1체의 체력을 1/4 회복한다."):(deploy?"1T 동안 아군 1체의 「"+target+"」가 2배가 된다.":"아군 1체의 「"+target+"」을 1랭크 올린다.");list.Add(P(name,"T시","1/시","트레이너가 턴 중 지령을 사용했을 때",effect));}return list;}
  public static List<PotentialRecord> ExtendedOrders(bool deploy){var list=new List<PotentialRecord>{P("맞춰라！","선행","1/시","아군 1체가 기술을 사용할 때","그 기술을 필중시킨다.")};foreach(string stat in new[]{"저격","회피"}){string target=stat=="저격"?"명중":"회피";list.Add(P((deploy?"전개 ":"숙달 ")+stat+" 지령","T시","1/시","트레이너가 턴 중 지령을 사용했을 때",deploy?"1T 동안 아군 1체의 「"+target+"」이 2배가 된다.":"아군 1체의 「"+target+"」을 1랭크 올린다."));}return list;}
  public static List<PotentialRecord> PokemonSlots(){return PokemonSlotNames.Select(x=>new PotentialRecord{slot=x}).ToList();}
  public static PotentialRecord Parse(string line,string defaultSlot){var p=new PotentialRecord{slot=defaultSlot,raw=line.Trim()};var m=Regex.Match(line,@"『([^』]+)』\s*…?\s*(.*)");if(!m.Success)return p;p.name=m.Groups[1].Value.Trim();string body=m.Groups[2].Value.Trim();var uses=Regex.Match(body,@"([0-9０-９]+\s*/\s*(?:시|시합|선발|턴))");if(uses.Success){p.uses=uses.Groups[1].Value;body=body.Remove(uses.Index,uses.Length).Trim(' ','/');}var mode=Regex.Match(body,@"^(선행|임의|자동|T시|Ｔ시)\s*/\s*");if(mode.Success){p.activation=mode.Groups[1].Value.Replace('Ｔ','T');body=body.Substring(mode.Length);}PotentialLibrary.Split(body,out p.trigger,out p.effect);if(p.trigger.Length==0)p.trigger=p.activation=="선행"?"선행 선언 시":p.activation=="T시"?"턴 중 선언 시":"";return p;}
 }
}
