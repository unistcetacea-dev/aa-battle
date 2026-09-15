using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace AABattle {
 public enum EntryReason {Lead,NormalSwitch,Return,Pivot,Potential,Replacement,Forced}

 public static class TriggerStructures {
  public const string AlwaysLabel="X";
  public const string EntryLabel="장소에 나올 때";
  public const string DefeatLabel="상대를 쓰러뜨렸을 때";
  public const string ObserverLabel="PT에 참여하고 있을 때";
  public const string TurnEndLabel="T 종료 시";
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
  public const string MoveUseLabel="기술을 내보낼 때";
  public const string AttackMoveUseLabel="공격기술을 내보낼 때";
  public const string StatusMoveUseLabel="변화기술을 내보낼 때";
  public const string AttackSuccessLabel="공격이 성공했을 때";
  public const string DamagedLabel="대미지를 받았을 때";

  public static string NormalizeLines(string text){return string.Join("\r\n",(text??"").Split(new[]{(char)13,(char)10},StringSplitOptions.RemoveEmptyEntries).Select(Normalize).Where(x=>x.Length>0).Distinct());}
  public static string Normalize(string text){
   string value=PotentialParts.Clean(text);string key=Regex.Replace(value,@"\s+","");
   if(key=="X"||key=="x"||key=="×")return AlwaysLabel;
   if(key=="턴중선언시")return "";
   if(Regex.IsMatch(key,@"^(?:자신이?|자신의포켓몬이)?(?:필드|장소)에(?:나와|나오면|나왔을때|나왔을때에|나올때|나온때)$"))return EntryLabel;
   if(Regex.IsMatch(key,@"^(?:자동)?(?:자신이)?상대를쓰러(?:뜨|트)렸을때$"))return DefeatLabel;
   if(Regex.IsMatch(key,@"^(?:자동)?(?:자신이)?PT에(?:참여|참가)(?:하고있을때|하고있는한|중일때)$"))return ObserverLabel;
   if(Regex.IsMatch(key,@"^(?:매)?(?:T|턴)종료시(?:에)?$"))return TurnEndLabel;
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
   string moveType;if(TryMoveType(value,out moveType))return MoveType(moveType);
   string moveName;if(TryMoveName(value,out moveName))return MoveName(moveName);
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
  public static string MoveType(string type){return "「"+PotentialParts.Clean(type)+"」 타입 기술을 내보낼 때";}
  public static bool TryMoveType(string text,out string type){var m=Regex.Match(PotentialParts.Clean(text),@"^「(?<value>[^」]+)」\s*(?:타입\s*)?기술을\s*내보낼\s*때$");type=m.Success?m.Groups["value"].Value.Trim():"";return m.Success&&type.Length>0;}
  public static string MoveName(string name){return "「"+PotentialParts.Clean(name)+"」을 내보낼 때";}
  public static string StatusCondition(string subject,string name,bool became,bool change){string suffix=became?(change?"상태변화가 되었을 때":"상태이상이 되었을 때"):(change?"상태변화일 때":"상태이상일 때");string owner=subject=="자신"?"자신이":subject=="아군"?"아군이":"상대가";return owner+" 「"+PotentialParts.Clean(name)+"」 "+suffix;}
  public static bool TryStatusCondition(string text,out string subject,out string name,out bool became,out bool change){var m=Regex.Match(PotentialParts.Clean(text),@"^(?<subject>자신|아군|상대)(?:이|가)\s*「(?<name>[^」]+)」\s*(?<kind>상태이상|상태변화)(?<became>가\s*되었을\s*때|이\s*되었을\s*때|일\s*때)$");subject=m.Success?m.Groups["subject"].Value:"";name=m.Success?m.Groups["name"].Value.Trim():"";change=m.Success&&m.Groups["kind"].Value=="상태변화";became=m.Success&&m.Groups["became"].Value.Replace(" ","")!="일때";return m.Success&&name.Length>0;}
  public static bool TryMoveName(string text,out string name){var m=Regex.Match(PotentialParts.Clean(text),@"^「(?<value>[^」]+)」(?:을|를)\s*내보낼\s*때$");name=m.Success?m.Groups["value"].Value.Trim():"";return m.Success&&name.Length>0;}
  public static bool TryOpponentType(string text,out string type){var match=Regex.Match(PotentialParts.Clean(text),@"^적진에\s*「(?<type>[^」]+)」\s*(?:타입\s*)?포켓몬이\s*있을\s*때$");type=match.Success?match.Groups["type"].Value.Trim():"";return match.Success&&type.Length>0;}
  public static string TrainerAptitude(string aptitude,string rank){return "트레이너가 「"+PotentialParts.Clean(aptitude)+":"+PotentialParts.Clean(rank).TrimEnd('-','+')+"」 이상일 때";}
  public static bool TryTrainerAptitude(string text,out string aptitude,out string rank){var match=Regex.Match(PotentialParts.Clean(text),@"^트레이너가\s*「(?<aptitude>지시|육성|통솔|능력)\s*:\s*(?<rank>[A-Za-z]+)[-+]?」\s*이상일\s*때$");aptitude=match.Success?match.Groups["aptitude"].Value:"";rank=match.Success?match.Groups["rank"].Value.ToUpperInvariant():"";return match.Success;}
  public static bool RankBand(string actual,string required){var left=Regex.Match((actual??"").ToUpperInvariant(),@"[A-Z]+").Value;var right=Regex.Match((required??"").ToUpperInvariant(),@"[A-Z]+").Value;return left.Length>0&&left==right;}
  public static IEnumerable<string> Clauses(Potential potential){
   var clauses=(potential.triggers??new string[0]).SelectMany(x=>(x??"").Split(new[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries)).ToList();string condition,effect;PotentialLibrary.Split(potential.description??"",out condition,out effect);clauses.AddRange(condition.Split(new[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries));return clauses;
  }
  public static IEnumerable<Potential> Matching(Fighter fighter,string label){return (fighter.Data.potentials??new Potential[0]).Where(p=>p!=null&&Clauses(p).Any(x=>Normalize(x)==label));}
  public static bool Has(Fighter fighter,string label){return fighter!=null&&Matching(fighter,label).Any();}
  public static void Fire(Fighter fighter,string label,string eventText,Action<string> log){
   log(eventText);
   foreach(var potential in Matching(fighter,label)){
    var clauses=Clauses(potential).ToList();string condition,effect;PotentialLibrary.Split(potential.description??"",out condition,out effect);
    if(clauses.Any(x=>Normalize(x)==label))log("『"+potential.name+"』 "+label+" 트리거 충족"+(clauses.Any(x=>Normalize(x)!=label)?" · 추가 조건 확인 필요":"")+" · 효과 판정 대기: "+effect);
   }
  }
  public static void Test(){
   foreach(var text in new[]{"필드에 나왔을때","장소에 나와","필드에 나오면","자신이 장소에 나왔을 때"})if(Normalize(text)!=EntryLabel)throw new Exception("Entry alias: "+text);
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
   if(Normalize("「강철」포켓몬을 쓰러트렸을 때")!="「강철」포켓몬을 쓰러뜨렸을 때")throw new Exception("Typed defeat spelling");
   if(Normalize("×")!=AlwaysLabel||!IsAlways("x"))throw new Exception("Always trigger alias");
   if(Normalize("선발로 필드에 나오면")==EntryLabel||Normalize("상대가 필드에 나오면")==EntryLabel)throw new Exception("Entry specificity lost");
  }
 }

 public static class EntryTriggers {
  public const string Id="field.enter";
  public const string Label=TriggerStructures.EntryLabel;
  public static bool IsEntry(EntryReason reason){return reason!=EntryReason.Forced;}
  public static string MethodLabel(EntryReason reason){if(reason==EntryReason.Lead)return TriggerStructures.LeadEntryLabel;if(reason==EntryReason.Replacement)return TriggerStructures.ReplacementEntryLabel;if(reason==EntryReason.NormalSwitch||reason==EntryReason.Return||reason==EntryReason.Pivot||reason==EntryReason.Potential)return TriggerStructures.VoluntaryEntryLabel;return "";}
  public static void Enter(Fighter fighter,EntryReason reason,Action<string> log){
   if(!IsEntry(reason))return;
   TriggerStructures.Fire(fighter,Label,fighter.Data.name+" · "+Label+" ["+reason+"]",log);
   string method=MethodLabel(reason);if(method.Length>0&&TriggerStructures.Has(fighter,method))TriggerStructures.Fire(fighter,method,fighter.Data.name+" · "+method,log);if((reason==EntryReason.Lead||reason==EntryReason.Replacement)&&TriggerStructures.Has(fighter,TriggerStructures.LeadOrReplacementEntryLabel))TriggerStructures.Fire(fighter,TriggerStructures.LeadOrReplacementEntryLabel,fighter.Data.name+" · "+TriggerStructures.LeadOrReplacementEntryLabel,log);
  }
  public static void Test(){
   TriggerStructures.Test();
   foreach(EntryReason reason in Enum.GetValues(typeof(EntryReason))){int count=0;var pokemon=new Pokemon{name="검사",level=100,@base=new[]{100,100,100,100,100,100},iv=new int[6],types=new[]{"노말"},moves=new string[0],potentials=new[]{new Potential{name="등장",description="공격이 오른다.",triggers=new[]{"장소에 나와"}}}};Enter(new Fighter(pokemon),reason,x=>count++);if(count!=(reason==EntryReason.Forced?0:2))throw new Exception("Entry dispatch");}
   var methodPokemon=new Pokemon{name="선발",level=100,@base=new[]{100,100,100,100,100,100},iv=new int[6],types=new[]{"노말"},moves=new string[0],potentials=new[]{new Potential{name="선봉",description="공격이 오른다.",triggers=new[]{TriggerStructures.LeadEntryLabel}}}};var methodLog=new System.Collections.Generic.List<string>();Enter(new Fighter(methodPokemon),EntryReason.Lead,methodLog.Add);if(!methodLog.Any(x=>x.Contains("『선봉』")))throw new Exception("Entry method dispatch");
   DefeatTriggers.Test();
   RuntimeTriggers.Test();
   AnsweredTriggers.Test();
   TriggerBatchTests.Run();
  }
 }

 public static class DefeatTriggers {
  public const string Id="opponent.defeated.by_move";
  public const string Label=TriggerStructures.DefeatLabel;
  public static void ByMove(Fighter attacker,Fighter target,Move move,int hpBefore,int damage,Action<string> log){
   if(hpBefore<=0||damage<=0||target.HP>0)return;
   TriggerStructures.Fire(attacker,Label,attacker.Data.name+"의 "+move.name+" 직접 대미지로 "+target.Data.name+"을 쓰러뜨렸다.",log);
  }
  public static void Test(){
   var source=new Fighter(new Pokemon{name="사냥꾼",level=100,@base=new[]{100,100,100,100,100,100},iv=new int[6],types=new[]{"노말"},moves=new string[0],potentials=new[]{new Potential{name="승전",description="공격이 오른다.",triggers=new[]{"상대를 쓰러트렸을 때"}}}});var target=new Fighter(new Pokemon{name="표적",level=100,@base=new[]{100,100,100,100,100,100},iv=new int[6],types=new[]{"노말"},moves=new string[0]});target.HP=0;var move=new Move{name="몸통박치기"};int count=0;ByMove(source,target,move,1,1,x=>count++);if(count!=2)throw new Exception("Direct move defeat dispatch");count=0;ByMove(source,target,move,1,0,x=>count++);ByMove(source,target,move,0,1,x=>count++);if(count!=0)throw new Exception("Indirect defeat exclusion");
  }
 }

 public enum SwitchReason {Normal,Return,Pivot,Potential,Fainted,Forced}
 public static class TrainerTriggers {
  static IEnumerable<PotentialRecord> Unique(TrainerRecord trainer){return trainer==null?Enumerable.Empty<PotentialRecord>():(trainer.potentials??new List<PotentialRecord>()).Where(p=>p!=null&&p.enabled&&p.slot=="고유"&&p.name.Length>0);}
  public static IEnumerable<PotentialRecord> Matching(TrainerRecord trainer,string label){return Unique(trainer).Where(p=>TriggerStructures.NormalizeLines(p.trigger).Replace("\r","").Split(new[]{'\n'},StringSplitOptions.RemoveEmptyEntries).Contains(label));}
  public static void Fire(TrainerRecord trainer,string label,string context,Action<string> log,Fighter ally=null){foreach(var p in Matching(trainer,label)){log("TRAINER 『"+p.name+"』 "+label+" 트리거 충족 · "+context+" · 효과 판정: "+p.effect);ApplySimpleAllyEffect(p.effect,ally,log);}}
  static void ApplySimpleAllyEffect(string effect,Fighter ally,Action<string> log){if(ally==null||ally.HP<=0)return;foreach(string line in (effect??"").Replace("\r","").Split(new[]{'\n'},StringSplitOptions.RemoveEmptyEntries)){var match=Regex.Match(PotentialParts.Clean(line),@"^(?:자신|아군)의\s*「?(?<stat>공격|방어|특공|특방|속도)」?(?:이|가|을|를)\s*(?<degree>매우 크게|크게)?\s*오른다$");if(!match.Success)continue;int index=Array.IndexOf(Engine.Names,match.Groups["stat"].Value),amount=match.Groups["degree"].Value=="매우 크게"?3:match.Groups["degree"].Value=="크게"?2:1;if(index>0){ally.Stages[index]=Math.Min(6,ally.Stages[index]+amount);log(ally.Data.name+": "+Engine.Names[index]+" +"+amount+" (고유 포텐셜)");}}}
  public static void BeforeMove(TrainerRecord trainer,Move move,Action<string> log){Fire(trainer,TriggerStructures.MoveUseLabel,move.name,log);Fire(trainer,move.category=="변화"?TriggerStructures.StatusMoveUseLabel:TriggerStructures.AttackMoveUseLabel,move.name,log);foreach(var p in Unique(trainer))if(TriggerStructures.NormalizeLines(p.trigger).Replace("\r","").Split(new[]{'\n'},StringSplitOptions.RemoveEmptyEntries).Any(c=>{string value;return TriggerStructures.TryMoveType(c,out value)&&(move.types??new string[0]).Contains(value)||TriggerStructures.TryMoveName(c,out value)&&move.name==value;}))log("TRAINER 『"+p.name+"』 기술 조건 트리거 충족 · "+move.name+" · 효과 판정 대기: "+p.effect);}
  public static IEnumerable<string> Active(TrainerRecord trainer,bool here){var labels=new List<string>{TriggerStructures.AlwaysLabel,TriggerStructures.FieldLabel};if(here)labels.Add(TriggerStructures.HereLabel);return Unique(trainer).Where(p=>labels.Any(label=>TriggerStructures.NormalizeLines(p.trigger).Contains(label))).Select(p=>"『"+p.name+"』");}
 }
 public static class RuntimeTriggers {
  public static IEnumerable<Potential> Field(Fighter fighter,Fighter opponent=null,TrainerRecord trainer=null,bool here=false){if(fighter==null||fighter.HP<=0)return Enumerable.Empty<Potential>();var active=TriggerStructures.Matching(fighter,TriggerStructures.AlwaysLabel).Concat(TriggerStructures.Matching(fighter,TriggerStructures.FieldLabel));if(here)active=active.Concat(TriggerStructures.Matching(fighter,TriggerStructures.HereLabel));active=active.Concat((fighter.Data.potentials??new Potential[0]).Where(p=>p!=null&&TriggerStructures.Clauses(p).Any(c=>{string granted;return TriggerStructures.TryGranted(c,out granted)&&HasGranted(fighter,granted);})));if(opponent!=null){active=active.Concat(AnsweredTriggers.Field(fighter,opponent,new Rules()));active=active.Concat((fighter.Data.potentials??new Potential[0]).Where(p=>p!=null&&TriggerStructures.Clauses(p).Any(c=>{string type;return TriggerStructures.TryOpponentType(c,out type)&&opponent.HP>0&&opponent.Data.types.Contains(type);})));}if(trainer!=null)active=active.Concat((fighter.Data.potentials??new Potential[0]).Where(p=>p!=null&&TriggerStructures.Clauses(p).Any(c=>TrainerCondition(c,trainer))));return active.Distinct();}
  static bool HasGranted(Fighter fighter,string name){return (fighter.Data.potentials??new Potential[0]).Any(p=>p!=null&&(p.name==name||p.name.StartsWith(name+" (")||p.name.StartsWith(name+"（")));}
  static bool TrainerCondition(string clause,TrainerRecord trainer){string aptitude,rank;if(!TriggerStructures.TryTrainerAptitude(clause,out aptitude,out rank))return false;AptitudeRecord value=aptitude=="지시"?trainer.command:aptitude=="육성"?trainer.training:aptitude=="통솔"?trainer.leadership:trainer.ability;return value!=null&&TriggerStructures.RankBand(value.rank,rank);}
  public static IEnumerable<string> Observers(IEnumerable<Fighter> reserves){return (reserves??Enumerable.Empty<Fighter>()).Where(f=>f.HP>0).SelectMany(f=>TriggerStructures.Matching(f,TriggerStructures.ObserverLabel).Select(p=>f.Data.name+" 『"+p.name+"』"));}
  public static void TurnEnd(Fighter[] fighters,Rules rules,Func<double> rng,Action<string> log){
   var eligible=Enumerable.Range(0,2).Where(i=>fighters[i].HP>0&&TriggerStructures.Has(fighters[i],TriggerStructures.TurnEndLabel)).ToList();
   eligible.Sort((left,right)=>{int speedLeft=Engine.Effective(fighters[left],fighters[1-left],5,rules),speedRight=Engine.Effective(fighters[right],fighters[1-right],5,rules);if(speedLeft!=speedRight)return speedRight.CompareTo(speedLeft);return (rng??(()=>.5))()<.5?-1:1;});
   foreach(int side in eligible)TriggerStructures.Fire(fighters[side],TriggerStructures.TurnEndLabel,fighters[side].Data.name+" · "+TriggerStructures.TurnEndLabel,log);
  }
  public static bool Voluntary(SwitchReason reason){return reason!=SwitchReason.Forced;}
  public static bool Normal(SwitchReason reason){return reason==SwitchReason.Normal||reason==SwitchReason.Return;}
  public static void Leave(Fighter outgoing,SwitchReason reason,Action<string> log){if(reason!=SwitchReason.Forced&&TriggerStructures.Has(outgoing,TriggerStructures.LeaveLabel))TriggerStructures.Fire(outgoing,TriggerStructures.LeaveLabel,outgoing.Data.name+" · "+TriggerStructures.LeaveLabel+" ["+reason+"]",log);}
  public static void Switch(Fighter outgoing,SwitchReason reason,Action<string> log){Leave(outgoing,reason,log);if(outgoing.HP>0&&Voluntary(reason)&&TriggerStructures.Has(outgoing,TriggerStructures.SwitchLabel))TriggerStructures.Fire(outgoing,TriggerStructures.SwitchLabel,outgoing.Data.name+" · 임의교대 ["+reason+"]",log);}
  public static void Received(Fighter target,Fighter attacker,Move move,int bodyDamage,Action<string> log){if(target.HP<=0||bodyDamage<=0)return;string message=target.Data.name+"이 "+attacker.Data.name+"의 "+move.name+"으로 "+bodyDamage+" 대미지를 받았다.";if(TriggerStructures.Has(target,TriggerStructures.ReceivedLabel))TriggerStructures.Fire(target,TriggerStructures.ReceivedLabel,message,log);if(TriggerStructures.Has(target,TriggerStructures.ReceivedOrNullifiedLabel))TriggerStructures.Fire(target,TriggerStructures.ReceivedOrNullifiedLabel,message,log);}
  public static void Nullified(Fighter target,Fighter attacker,Move move,string reason,Action<string> log){if(target.HP>0&&Mechanics.DefensiveNullification(move,reason)&&TriggerStructures.Has(target,TriggerStructures.ReceivedOrNullifiedLabel))TriggerStructures.Fire(target,TriggerStructures.ReceivedOrNullifiedLabel,target.Data.name+"이 "+attacker.Data.name+"의 "+move.name+"을 무효화했다. ["+reason+"]",log);}
  public static void DirectiveReceived(Fighter target,IEnumerable<Fighter> party,string order,Action<string> log){if(target!=null&&target.HP>0&&TriggerStructures.Has(target,TriggerStructures.SelfOrderLabel))TriggerStructures.Fire(target,TriggerStructures.SelfOrderLabel,target.Data.name+"이 『"+order+"』을 받았다.",log);foreach(var owner in (party??Enumerable.Empty<Fighter>()).Where(x=>x!=null&&x.HP>0&&TriggerStructures.Has(x,TriggerStructures.AllyOrderLabel)).Distinct())TriggerStructures.Fire(owner,TriggerStructures.AllyOrderLabel,"아군 "+target.Data.name+"이 『"+order+"』을 받았다.",log);}
  public static void Analyzed(Fighter analyst,IEnumerable<Fighter> party,Fighter target,Action<string> log){if(analyst!=null&&analyst.HP>0&&TriggerStructures.Has(analyst,TriggerStructures.AnalyzedLabel))TriggerStructures.Fire(analyst,TriggerStructures.AnalyzedLabel,analyst.Data.name+"이 "+target.Data.name+"의 데이터를 처음 해석했다.",log);}
  public static void BattleStart(TrainerRecord[] trainers,IEnumerable<Fighter>[] parties,Action<string> log){for(int side=0;side<2;side++){var trainer=trainers!=null&&side<trainers.Length?trainers[side]:null;TrainerTriggers.Fire(trainer,TriggerStructures.BattleStartLabel,(side+1)+"팀 배틀 시작",log);foreach(var owner in parties!=null&&side<parties.Length?parties[side]:Enumerable.Empty<Fighter>())if(owner!=null&&owner.HP>0&&TriggerStructures.Has(owner,TriggerStructures.BattleStartLabel))TriggerStructures.Fire(owner,TriggerStructures.BattleStartLabel,"배틀이 시작됐다.",log);}}
  public static void OpponentEntered(Fighter observer,Fighter entering,Action<string> log){if(observer!=null&&observer.HP>0&&entering!=null&&TriggerStructures.Has(observer,TriggerStructures.OpponentEntryLabel))TriggerStructures.Fire(observer,TriggerStructures.OpponentEntryLabel,entering.Data.name+"이 장소에 나왔다.",log);}
  public static void BeforeMove(Fighter user,Move move,Action<string> log){if(user==null||user.HP<=0||move==null)return;foreach(string label in new[]{TriggerStructures.MoveUseLabel,move.category=="변화"?TriggerStructures.StatusMoveUseLabel:TriggerStructures.AttackMoveUseLabel})if(TriggerStructures.Has(user,label))TriggerStructures.Fire(user,label,user.Data.name+"이 "+move.name+"을 내보낸다.",log);foreach(var p in user.Data.potentials??new Potential[0])if(p!=null&&TriggerStructures.Clauses(p).Any(c=>{string value;return TriggerStructures.TryMoveType(c,out value)&&(move.types??new string[0]).Contains(value)||TriggerStructures.TryMoveName(c,out value)&&move.name==value;}))log("『"+p.name+"』 "+TriggerStructures.Normalize(TriggerStructures.Clauses(p).First(c=>{string value;return TriggerStructures.TryMoveType(c,out value)&&(move.types??new string[0]).Contains(value)||TriggerStructures.TryMoveName(c,out value)&&move.name==value;}))+" 트리거 충족 · 효과 판정 대기: "+p.description);}
  public static void AttackSucceeded(Fighter user,Fighter target,Move move,Action<string> log){if(user!=null&&user.HP>0&&move!=null&&move.category!="변화"&&TriggerStructures.Has(user,TriggerStructures.AttackSuccessLabel))TriggerStructures.Fire(user,TriggerStructures.AttackSuccessLabel,user.Data.name+"의 "+move.name+" 공격이 "+target.Data.name+"에게 명중했다.",log);}
  public static void Damaged(Fighter target,int amount,string reason,Action<string> log){if(target!=null&&amount>0&&TriggerStructures.Has(target,TriggerStructures.DamagedLabel))TriggerStructures.Fire(target,TriggerStructures.DamagedLabel,target.Data.name+"이 "+reason+"로 "+amount+" 대미지를 받았다.",log);}
  public static bool HereThreshold(int initialSize,int living){return initialSize>=6?living<=3:initialSize>=4?living<=2:living<=1;}
  public static bool IsRole(Fighter fighter,params string[] names){return fighter!=null&&fighter.HP>0&&(fighter.Data.potentials??new Potential[0]).Any(p=>p!=null&&p.id=="역할"&&names.Contains(p.name));}
  public static bool HereNow(int initialSize,int living,Fighter opponent){return HereThreshold(initialSize,living)||IsRole(opponent,"에이스","탐사대장");}
  public static void Test(){
   var p=new Pokemon{name="관측자",level=100,@base=new[]{100,100,100,100,100,120},iv=new int[6],types=new[]{"노말"},moves=new string[0],potentials=new[]{new Potential{name="상시",description="공격이 오른다.",triggers=new[]{"필드에 있는 한"}},new Potential{name="관측",description="아군의 공격이 오른다.",triggers=new[]{"PT에 참가하고 있을 때"}},new Potential{name="종료",description="공격이 오른다.",triggers=new[]{"T종료시"}},new Potential{name="선회",description="공격이 오른다.",triggers=new[]{"아군과 교대할 때"}},new Potential{name="퇴장",description="공격이 오른다.",triggers=new[]{"필드를 떠날 때"}},new Potential{name="반격",description="공격이 오른다.",triggers=new[]{"상대의 공격을 받으면"}},new Potential{name="대화",description="공격이 오른다.",triggers=new[]{TriggerStructures.OpponentType("불꽃")}},new Potential{name="통솔",description="공격이 오른다.",triggers=new[]{TriggerStructures.TrainerAptitude("통솔","A")}},new Potential{name="방호",description="공격이 오른다.",triggers=new[]{TriggerStructures.ReceivedOrNullifiedLabel}}}};var fighter=new Fighter(p);if(Field(fighter).Count()!=1||Observers(new[]{fighter}).Count()!=1)throw new Exception("Continuous and observer triggers");var opponent=new Fighter(new Pokemon{name="불꽃",level=100,@base=new[]{100,100,100,100,100,100},iv=new int[6],types=new[]{"불꽃"},moves=new string[0]});var trainer=new TrainerRecord();trainer.leadership.rank="A+";if(Field(fighter,opponent,trainer).Count()!=3)throw new Exception("Type and trainer conditions");trainer.leadership.rank="AA";if(Field(fighter,opponent,trainer).Count()!=2)throw new Exception("Trainer rank band");fighter.HP=0;if(Field(fighter).Any()||Observers(new[]{fighter}).Any())throw new Exception("Fainted continuous trigger");fighter=new Fighter(p);int count=0;Switch(fighter,SwitchReason.Normal,x=>count++);if(count!=4)throw new Exception("Voluntary switch and leave triggers");count=0;Switch(fighter,SwitchReason.Forced,x=>count++);if(count!=0)throw new Exception("Forced switch exclusion");fighter.HP=0;Leave(fighter,SwitchReason.Fainted,x=>count++);if(count!=2)throw new Exception("Fainted leave trigger");var target=new Fighter(p);count=0;Received(target,fighter,new Move{name="연속공격"},3,x=>count++);if(count!=4)throw new Exception("Received after move trigger");count=0;Nullified(target,fighter,new Move{name="공격",category="물리"},"타입 상성에 의한 무효",x=>count++);if(count!=2)throw new Exception("Defensive nullification trigger");count=0;Nullified(target,fighter,new Move{name="공격",category="물리"},"방어가 기술을 막았다",x=>count++);Received(target,fighter,new Move{name="연속공격"},0,x=>count++);target.HP=0;Received(target,fighter,new Move{name="연속공격"},3,x=>count++);if(count!=0)throw new Exception("Excluded nullification, no-damage or fainted received trigger");
  }
 }
}
