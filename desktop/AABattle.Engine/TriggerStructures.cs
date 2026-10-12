using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace AABattle {
 // 트리거 조건 구조와 표준 문구
 public static class TriggerStructures {
  public const string AlwaysLabel="X";
  public const string EntryLabel="장소에 나올 때";
  public const string DefeatLabel="상대를 쓰러뜨렸을 때";
  public const string ObserverLabel="PT에 참여하고 있을 때";
  public const string TurnEndLabel="T 종료 시";
  public const string TurnStartLabel="T 개시 시";      // 서로 지시를 내린 뒤 가장 먼저(교대·기술보다 앞)
  public const string TurnLastLabel="T의 최후";        // 기술·교대를 모두 처리한 뒤, T 종료 전
  public const string FieldLabel="장소에 있는 한";
  public const string SwitchLabel="아군과 임의교대할 때";
  public const string ReceivedLabel="상대의 공격을 받았을 때";
  public const string LeaveLabel="장소를 떠날 때";
  public const string LeadEntryLabel="선발로 장소에 나올 때";
  public const string ReplacementEntryLabel="죽어내밀기로 장소에 나올 때";
  public const string VoluntaryEntryLabel="임의교대로 장소에 나올 때";
  public const string LeadOrReplacementEntryLabel="선발 또는 죽어내밀기로 장소에 나올 때";
  public const string ReceivedOrNullifiedLabel="상대의 공격을 받았거나 무효화했을 때";
  public const string HereLabel="「여기다！」일 때";
  public const string SelfOrderLabel="자신이 지령을 받았을 때";
  public const string AllyOrderLabel="아군이 지령을 받았을 때";
  public const string ContractLabel="『계약의 특권』이 부여되어 있을 때";
  public const string AnalyzedLabel="상대의 데이터를 해석했을 때";
  public const string BattleStartLabel="배틀 시작 시";
  public const string OpponentEntryLabel="상대가 장소에 나왔을 때";
  public const string SelfAndOpponentEntryLabel=EntryLabel+"\r\n"+OpponentEntryLabel;
  public const string MoveUseLabel="기술을 내보낼 때";
  public const string AttackMoveUseLabel="「공격」 기술을 내보낼 때";
  public const string StatusMoveUseLabel="「변화」 기술을 내보낼 때";
  public const string AttackSuccessLabel="자신의 공격이 명중했을 때";   // 사용자 결정: 명중 = 공격 성공
  public const string DamagedLabel="대미지를 받았을 때";

  public static string NormalizeLines(string text){return string.Join("\r\n",(text??"").Split(new[]{(char)13,(char)10},StringSplitOptions.RemoveEmptyEntries).SelectMany(x=>Normalize(x).Split(new[]{(char)13,(char)10},StringSplitOptions.RemoveEmptyEntries)).Where(x=>x.Length>0).Distinct());}
  static readonly System.Collections.Concurrent.ConcurrentDictionary<string,string> normalized=new System.Collections.Concurrent.ConcurrentDictionary<string,string>();
  public static string Normalize(string text){return normalized.GetOrAdd(text??"",NormalizeUncached);}
  static string NormalizeUncached(string text){
   string value=PotentialParts.Clean(text);string key=Regex.Replace(value,@"\s+","");
   if(key=="X"||key=="x"||key=="×")return AlwaysLabel;
   if(key=="턴중선언시")return "";
   if(Regex.IsMatch(key,@"^자신\(상대\)이?(?:필드|장소)에(?:나와|나오면|나왔을때|나왔을때에|나올때|나온때)$"))return SelfAndOpponentEntryLabel;
   // 트리거 모듈(등장·교대·퇴장·타이밍·피격·명중·급소·쓰러뜨림·기술 사용)의 표준 문장
   string module=TriggerModules.Normalize(value);if(module!=null)return module;
   if(Regex.IsMatch(key,@"^(?:자신이?|자신의포켓몬이)?(?:필드|장소)에(?:나와|나오면|나왔을때|나왔을때에|나올때|나온때)$"))return EntryLabel;
   if(Regex.IsMatch(key,@"^(?:자동)?(?:자신이)?상대를쓰러(?:뜨|트)렸을때$"))return DefeatLabel;
   if(Regex.IsMatch(key,@"^(?:자동)?(?:자신이)?PT에(?:참여|참가)(?:하고있을때|하고있는한|중일때)$"))return ObserverLabel;
   if(Regex.IsMatch(key,@"^(?:매)?(?:T|턴)종료시(?:에)?$"))return TurnEndLabel;
   if(Regex.IsMatch(key,@"^(?:매)?(?:T|턴)(?:개시|시작)시(?:에)?$"))return TurnStartLabel;
   if(Regex.IsMatch(key,@"^(?:T|턴)의?(?:최후|마지막)(?:에)?$"))return TurnLastLabel;
   if(Regex.IsMatch(key,@"^(?:자신이)?(?:필드|장소)에(?:있는한|있을때)$"))return FieldLabel;
   if(Regex.IsMatch(key,@"^(?:자동)?아군과(?:임의)?교대할때$"))return SwitchLabel;
   if(Regex.IsMatch(key,@"^(?:자동)?상대의공격을(?:받았을때|받으면)$"))return ReceivedLabel;
   if(Regex.IsMatch(key,@"^(?:필드|장소)를떠날때$"))return LeaveLabel;
   if(Regex.IsMatch(key,@"^선발로(?:필드|장소)에(?:나올때|나왔을때|나오면)$"))return LeadEntryLabel;
   if(Regex.IsMatch(key,@"^[「『]?(?:죽어내밀기)[」』]?로(?:필드|장소)?에?(?:나올때|나왔을때|나오면)$"))return ReplacementEntryLabel;
   if(Regex.IsMatch(key,@"^(?:임의교대로|아군과교대해)(?:필드|장소)에(?:나올때|나왔을때|나오면)$"))return VoluntaryEntryLabel;
   if(Regex.IsMatch(key,@"^(?:선발\(죽어내밀기\)|[「『]\(선발\)죽어내밀기[」』])로(?:필드|장소)에(?:나올때|나왔을때|나오면)$"))return LeadOrReplacementEntryLabel;
   if(Regex.IsMatch(key,@"^상대의공격을(?:받았을|받으면)\((?:무효화했을|무효화하면|무효화)\)(?:때)?$"))return ReceivedOrNullifiedLabel;
   if(Regex.IsMatch(key,@"^[「『]여기다[!！][」』]일때$"))return HereLabel;
   if(Regex.IsMatch(key,@"^(?:자동)?자신이(?:『)?지령(?:』)?을받았을때$"))return SelfOrderLabel;
   if(Regex.IsMatch(key,@"^(?:트레이너가턴중지령을사용했을때|(?:자동)?『?지령』?을받았을때)$"))return SelfOrderLabel;
   if(Regex.IsMatch(key,@"^(?:자동)?아군이(?:『)?지령(?:』)?을받았을때$"))return AllyOrderLabel;
   if(Regex.IsMatch(key,@"^(?:상대의데이터를|상대를)해석했을때$"))return AnalyzedLabel;
   if(Regex.IsMatch(key,@"^(?:배틀|시합)(?:시작|개시)(?:시|시점|했을때)$"))return BattleStartLabel;
   if(Regex.IsMatch(key,@"^상대가(?:필드|장소)에(?:나왔을때|나올때|나오면)$"))return OpponentEntryLabel;
   if(Regex.IsMatch(key,@"^(?:자신이)?기술을내보낼때$"))return MoveUseLabel;
   if(Regex.IsMatch(key,@"^(?:자신이)?(?:공격기술|공격기)을내보낼때$"))return AttackMoveUseLabel;
   if(Regex.IsMatch(key,@"^(?:자신이)?(?:변화기술|변화기)을내보낼때$"))return StatusMoveUseLabel;
   if(Regex.IsMatch(key,@"^(?:자신의)?공격이(?:성공|명중)했을때$"))return AttackSuccessLabel;
   if(Regex.IsMatch(key,@"^(?:자신이)?(?:데미지|대미지)를받았을때$"))return DamagedLabel;
   string granted;if(TryGranted(value,out granted))return Granted(granted);
   string statusSubject,statusName;bool statusBecame,statusChange;if(TryStatusCondition(value,out statusSubject,out statusName,out statusBecame,out statusChange))return StatusCondition(statusSubject,statusName,statusBecame,statusChange);
   string parsed;if(TryOpponentType(value,out parsed))return OpponentType(parsed);string aptitude,rank;if(TryTrainerAptitude(value,out aptitude,out rank))return TrainerAptitude(aptitude,rank);
   value=Regex.Replace(value,@"(?:필드|장소)에\s*(?:나왔을\s*때(?:에)?|나오면|나온\s*때|나올\s*때|나와)$",EntryLabel);
   value=value.Replace("쓰러트렸을 때","쓰러뜨렸을 때");
   return Regex.Replace(value,@"교대해서\s*","교대해 ");
  }
  public static bool IsAlways(string trigger){return Normalize(trigger)==AlwaysLabel;}
  public static string OpponentType(string type){return "적진에 「"+PotentialParts.Clean(type)+"」 포켓몬이 있을 때";}
  public static string Granted(string name){name=PotentialParts.Clean(name).Trim('『','』');char last=name.Length>0?name[name.Length-1]:' ';bool batchim=last>='가'&&last<='힣'&&(last-'가')%28!=0;return "『"+name+"』"+(batchim?"이":"가")+" 부여되어 있을 때";}
  public static bool TryGranted(string text,out string name){var m=Regex.Match(PotentialParts.Clean(text),@"^[『「](?<name>[^』」]+)[』」](?:이|가)\s*부여되어\s*있을\s*때$");name=m.Success?m.Groups["name"].Value.Trim():"";return m.Success&&name.Length>0;}
  public static string MoveType(string type){return "「"+PotentialParts.Clean(type)+"」 기술을 내보낼 때";}
  public static bool TryMoveType(string text,out string type){var s=TriggerModules.Parse(Normalize(text));type=s.Module=="기술 사용"&&!s.Opponent&&!s.After&&s.Condition.Types.Count==1&&s.Condition.Tags.Count==0&&s.Condition.Category==""?s.Condition.Types[0]:"";return type.Length>0;}
  public static string MoveName(string name){return "「"+PotentialParts.Clean(name)+"」 기술을 내보낼 때";}
  public static string StatusCondition(string subject,string name,bool became,bool change){string suffix=became?(change?"상태변화가 되었을 때":"상태이상이 되었을 때"):(change?"상태변화일 때":"상태이상일 때");string owner=subject=="자신"?"자신이":subject=="아군"?"아군이":"상대가";return owner+" 「"+PotentialParts.Clean(name)+"」 "+suffix;}
  public static bool TryStatusCondition(string text,out string subject,out string name,out bool became,out bool change){var m=Regex.Match(PotentialParts.Clean(text),@"^(?<subject>자신|아군|상대)(?:이|가)\s*「(?<name>[^」]+)」\s*(?<kind>상태이상|상태변화)(?<became>가\s*되었을\s*때|이\s*되었을\s*때|일\s*때)$");subject=m.Success?m.Groups["subject"].Value:"";name=m.Success?m.Groups["name"].Value.Trim():"";change=m.Success&&m.Groups["kind"].Value=="상태변화";became=m.Success&&m.Groups["became"].Value.Replace(" ","")!="일때";return m.Success&&name.Length>0;}
  public static bool TryMoveName(string text,out string name){var s=TriggerModules.Parse(Normalize(text));name=s.Module=="기술 사용"&&!s.Opponent&&!s.After&&s.Condition.Tags.Count==1&&s.Condition.Types.Count==0&&s.Condition.Category==""?s.Condition.Tags[0]:"";return name.Length>0;}
  public static bool TryOpponentType(string text,out string type){var match=Regex.Match(PotentialParts.Clean(text),@"^적진에\s*「(?<type>[^」]+)」\s*(?:타입\s*)?포켓몬이\s*있을\s*때$");type=match.Success?match.Groups["type"].Value.Trim():"";return match.Success&&type.Length>0;}
  public static string TrainerAptitude(string aptitude,string rank){return "트레이너가 「"+PotentialParts.Clean(aptitude)+":"+PotentialParts.Clean(rank).TrimEnd('-','+')+"」 이상일 때";}
  public static bool TryTrainerAptitude(string text,out string aptitude,out string rank){var match=Regex.Match(PotentialParts.Clean(text),@"^트레이너가\s*「(?<aptitude>지시|육성|통솔|능력)\s*:\s*(?<rank>[A-Za-z]+)[-+]?」\s*이상일\s*때$");aptitude=match.Success?match.Groups["aptitude"].Value:"";rank=match.Success?match.Groups["rank"].Value.ToUpperInvariant():"";return match.Success;}
  public static bool RankBand(string actual,string required){var left=Regex.Match((actual??"").ToUpperInvariant(),@"[A-Z]+").Value;var right=Regex.Match((required??"").ToUpperInvariant(),@"[A-Z]+").Value;return left.Length>0&&left==right;}
  public static IEnumerable<string> Clauses(Potential potential){
   var clauses=(potential.triggers??new string[0]).SelectMany(x=>(x??"").Split(new[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries)).ToList();string condition,effect;PotentialLibrary.Split(potential.description??"",out condition,out effect);clauses.AddRange(condition.Split(new[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries));return clauses.SelectMany(x=>Normalize(x).Split(new[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries)).ToList();
  }
  public static IEnumerable<Potential> Matching(Fighter fighter,string label){return (fighter.Data.potentials??new Potential[0]).Where(p=>p!=null&&Clauses(p).Any(x=>Normalize(x)==label));}
  public static bool Has(Fighter fighter,string label){return fighter!=null&&Matching(fighter,label).Any();}
  // 트리거 라벨 사건. 효과 실행 범위 안이면 공통 효과 큐(PotentialQueue)가 처리하고, 밖이면 예전처럼 로그만 남긴다
  public static void Fire(Fighter fighter,string label,string eventText,Action<string> log){
   if(PotentialQueue.Active&&fighter!=null){
    PotentialQueue.Raise(LabelEvent(fighter,label,eventText,log));
    return;
   }
   log(eventText);
   foreach(var potential in Matching(fighter,label))log(PendingText(potential,label,Clauses(potential).Any(x=>Normalize(x)!=label)));
  }
  public static PotentialEvent LabelEvent(Fighter fighter,string label,string eventText,Action<string> log){
   return new PotentialEvent{Owner=fighter,Label=label,Message=eventText??"",Log=log,Announce=true,PendingText=(source,extra)=>PendingText(source.Pokemon,label,extra)};
  }
  static string PendingText(Potential potential,string label,bool extra){
   string condition,effect;PotentialLibrary.Split(potential.description??"",out condition,out effect);
   return "『"+potential.name+"』 "+label+" 트리거 충족"+(extra?" · 추가 조건 확인 필요":"")+" · 효과 판정 대기: "+effect;
  }
  public static void Test(){
   foreach(var text in new[]{"필드에 나왔을때","장소에 나와","필드에 나오면","자신이 장소에 나왔을 때"})if(Normalize(text)!=EntryLabel)throw new Exception("Entry alias: "+text);
   if(Normalize("자신(상대)이 장소에 나왔을 때")!=SelfAndOpponentEntryLabel)throw new Exception("Self and opponent entry alias");
   foreach(var text in new[]{"상대를 쓰러뜨렸을 때","상대를 쓰러트렸을 때","자신이 상대를 쓰러트렸을 때","자동 상대를 쓰러뜨렸을 때"})if(Normalize(text)!=DefeatLabel)throw new Exception("Defeat alias: "+text);
   foreach(var text in new[]{"PT에 참가하고 있을 때","PT에 참가하고 있는 한","자신이 PT에 참여중일 때"})if(Normalize(text)!=ObserverLabel)throw new Exception("Observer alias: "+text);
   foreach(var text in new[]{"T종료시","T종료시에","매T종료 시에","턴 종료시"})if(Normalize(text)!=TurnEndLabel)throw new Exception("Turn end alias: "+text);
   foreach(var text in new[]{"필드에 있는 한","필드에 있을 때","장소에 있는 한"})if(Normalize(text)!=FieldLabel)throw new Exception("Field alias: "+text);
   foreach(var text in new[]{"아군과 교대할 때","아군과 임의 교대할 때","자동 아군과 교대할 때"})if(Normalize(text)!=SwitchLabel)throw new Exception("Switch alias: "+text);
   foreach(var text in new[]{"상대의 공격을 받았을 때","상대의 공격을 받으면","자동 상대의 공격을 받았을 때"})if(Normalize(text)!=ReceivedLabel)throw new Exception("Received alias: "+text);
   if(Normalize("필드를 떠날 때")!=LeaveLabel||Normalize("장소를 떠날 때")!=LeaveLabel)throw new Exception("Leave alias");
   if(Normalize("선발로 필드에 나오면")!=LeadEntryLabel||Normalize("「죽어내밀기」로 장소에 나올 때")!=ReplacementEntryLabel||Normalize("아군과 교대해 필드에 나올 때")!=VoluntaryEntryLabel||Normalize("선발(죽어내밀기)로 장소에 나올 때")!=LeadOrReplacementEntryLabel)throw new Exception("Entry method aliases");
   if(Normalize("상대의 공격을 받으면(무효화하면)")!=ReceivedOrNullifiedLabel)throw new Exception("Received or nullified alias");
   if(Normalize("「여기다!」일 때")!=HereLabel||Normalize("자신이 『지령』을 받았을 때")!=SelfOrderLabel||Normalize("아군이 지령을 받았을 때")!=AllyOrderLabel||Normalize("상대를 해석했을 때")!=AnalyzedLabel||Normalize("시합 개시시")!=BattleStartLabel||Normalize("상대가 필드에 나오면")!=OpponentEntryLabel)throw new Exception("Here, order, analysis or start aliases");
   string statusSubject,statusName;bool statusBecame,statusChange;if(!TryStatusCondition(StatusCondition("아군","화상",true,false),out statusSubject,out statusName,out statusBecame,out statusChange)||statusSubject!="아군"||statusName!="화상"||!statusBecame||statusChange)throw new Exception("Structured status trigger");
   string named;if(!TryGranted("『계약의 특권』이 부여되어 있을 때",out named)||named!="계약의 특권"||!TryMoveType("「불꽃」기술을 내보낼 때",out named)||named!="불꽃"||!TryMoveName("「파괴광선」을 내보낼 때",out named)||named!="파괴광선")throw new Exception("Parameterized granted or move trigger");
   string type,aptitude,rank;if(!TryOpponentType("적진에 「불꽃」 포켓몬이 있을 때",out type)||type!="불꽃"||!TryTrainerAptitude("트레이너가 「통솔:A+」 이상일 때",out aptitude,out rank)||aptitude!="통솔"||rank!="A"||!RankBand("A-","A")||RankBand("AA","A"))throw new Exception("Parameterized trigger parsing");
   if(Normalize("「강철」포켓몬을 쓰러트렸을 때")!="「강철」 타입 포켓몬을 쓰러뜨렸을 때")throw new Exception("Typed defeat spelling");
   if(Normalize("×")!=AlwaysLabel||!IsAlways("x"))throw new Exception("Always trigger alias");
   if(Normalize("선발로 필드에 나오면")==EntryLabel||Normalize("상대가 필드에 나오면")==EntryLabel)throw new Exception("Entry specificity lost");
  }
 }
}
