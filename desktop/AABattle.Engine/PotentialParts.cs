using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.IO;
using System.Web.Script.Serialization;

namespace AABattle {
 public class PotentialPart {
  public string id,key,text,activation,uses,exclusionReason,structureKind,searchText;public int variantCount;public bool protectedTemplate,excludedFromImplementation;
  public List<string> names=new List<string>(),sources=new List<string>(),raws=new List<string>(),variants=new List<string>();
  public string Display {get{var meta=new[]{protectedTemplate?"기본 보호":"",!string.IsNullOrEmpty(structureKind)?"구조 템플릿 · "+variantCount+"개 원문":"",uses,activation}.Where(x=>!string.IsNullOrEmpty(x));string prefix=string.Join(" · ",meta);return (prefix.Length>0?"["+prefix+"] ":"")+text;}}
  public string Search {get{return Display+" "+(searchText??"");}}
 }
 public class PotentialParts {
  public List<PotentialPart> triggers=new List<PotentialPart>(),effects=new List<PotentialPart>();
  public static string Clean(string text){var result=Regex.Replace((text??"").Normalize(NormalizationForm.FormKC).Replace('，',',').Replace('、',','),@"\s+"," ").Trim().TrimEnd('.',',','。').Trim();if(Regex.Matches(result,@"\[\d+\]").Count==1)result=Regex.Replace(result,@"^\[\d+\]\s*","");return result;}

  public static string Key(string text){return Regex.Replace(Clean(text),@"\s+","");}
  // 트리거는 표기만 다른 같은 조건을 하나로 합친다. 괄호·띄어쓰기·맞춤법 차이만 지우고, 시점이 달라질 수 있는 「할 때/했을 때」는 남긴다.
  static readonly string[][] TriggerSpellings={new[]{"「",""},new[]{"」",""},new[]{"『",""},new[]{"』",""},new[]{",",""},new[]{"대미지","데미지"},new[]{"퍼텐셜","포텐셜"},new[]{"스피드","속도"},new[]{"필드","장소"},new[]{"나오면","나왔을때"},new[]{"나올때","나왔을때"},new[]{"받으면","받았을때"},new[]{"받을때","받았을때"},new[]{"됐을때","되었을때"},new[]{"쓰러트","쓰러뜨"},new[]{"턴종료시","T종료시"},new[]{"T종료시에","T종료시"},new[]{"매T종료시","T종료시"}};
  // 뜻이 같은 문장 규칙. 생략된 주체는 자신이다. 「공격기술」은 「공격」과 같은 범위지만 「기술」(변화기술 포함)과는 다르다.
  static readonly string[][] TriggerMeanings={new[]{"카운터","카운트"},new[]{"나와서","나와"},new[]{"째종료시","째의T종료시"},new[]{"공격기술","공격"},new[]{"공격을실시하지","공격을하지"},new[]{"공격하지않을때","공격을하지않을때"},new[]{"빈사가되었을때","빈사상태가되었을때"},new[]{"빈사상대가","빈사상태가"},new[]{"접해있을때","접하고있을때"},new[]{"회복되었을때","회복했을때"},new[]{"가득할때","가득일때"}};
  static readonly string[][] TriggerPatterns={
   new[]{@"^자신(이|의|은)",""},
   new[]{@"^(아군)?트레이너(가|의)(지시|육성|통솔|능력)(:|이|가)?([A-Z]+[+-]?)(이상|이하)일(때|경우)$","트레이너가$3:$5$6일때"},
   new[]{@"^(아군과)?(임의)?교대(해|로)장소에나왔을때$","임의교대로장소에나왔을때"},
   new[]{@"^아군과(임의)?교대(시|할때)$","아군과임의교대할때"},
   new[]{@"^아군과임의교대로나온T","임의교대로나온T"}};
  // 규칙으로 일반화하기 어려운 같은 뜻의 문장. 왼쪽을 오른쪽으로 합친다. 공격/기술, 주체, 「있을 때/되었을 때」가 다른 문장은 넣지 않는다.
  static readonly string[][] TriggerAliases={
   new[]{"상대의 공격을 「방어」등의 기술로 무효화했을 때","「방어」등의 기술로 상대의 공격을 무효화했을 때"},
   new[]{"상대의 기술을 「방어」등의 기술로 무효화했을 때","「방어」등의 기술로 상대의 기술을 무효화했을 때"},
   new[]{"「죽어내밀기」로 상대가 나왔을 때","상대가 「죽어내밀기」로 나왔을 때"},
   new[]{"자신의 공격 명중시에","자신의 공격 명중 시"},new[]{"자신의 공격명중 시","자신의 공격 명중 시"},new[]{"자신의 공격기술의 명중 시","자신의 공격 명중 시"},
   new[]{"「짓궂은마음」이 발동했을 때","특성 「짓궂은마음」이 발동했을 때"},
   new[]{"자진에 「리플렉터(빛의장막)」가 설치되었을 때","자진에 「리플렉터(빛의장막)」이 전개되었을 때"},new[]{"자진에 「리플렉터(빛의장막)」이 설치되었을 때","자진에 「리플렉터(빛의장막)」이 전개되었을 때"},
   new[]{"「혼란」상대일 때","「혼란」상태일 때"},
   new[]{"상대가 「독(맹독)」가 됐을 때","상대가 「독(맹독)」상태가 됐을 때"},new[]{"상대가 「독(맹독)」일 때","상대가 「독(맹독)」 상태일 때"},
   new[]{"「접촉기술」을 받았을 때","상대의 「접촉」기술을 받았을 때"},new[]{"「물」기술을 받았을 때","상대의 「물」기술을 받았을 때"},
   new[]{"상대의 공격을 맞았을 때","상대의 공격을 받았을 때"},
   new[]{"「선발(죽어내밀기)」로 장소에 나올 때","선발 또는 죽어내밀기로 장소에 나올 때"},
   new[]{"「죽어내밀기」로 필드에 나온 T 종료시","「죽어내밀기」로 나온 T종료시"},
   new[]{"상대에게 「죽음의 카운터」가 있을 때","상대에게 「죽음의 카운터」가 발생하고 있을 때"},
   new[]{"필드가 《 무대/드리미 씨어터 》일때","전체 필드가 《 무대/드리미 시어터 》일 때"},
   new[]{"적진에 데이터가 해석된 적이 있을 때","적진에 데이터가 해석된 상대가 있을 때"},new[]{"적진에 데이터가 해석된 포켓몬이 있을 때","적진에 데이터가 해석된 상대가 있을 때"},
   new[]{"상대의 「속」 종족치가 자신보다 낮을 때","상대의 「속」의 종족치의 랭크가 자신보다 낮을 때"},
   new[]{"상대가 「고스트」일 때","상대가 「고스트」타입일 때"},new[]{"「고스트」와 교대해 장소에 나올 때","「고스트」타입과 교대해 장소에 나올 때"},
   new[]{"「반동」 타입 기술을 내보낼 때","「반동기술」을 내보낼 때"},
   new[]{"적진에 「불」 포켓몬이 있을 때","적진에 「불꽃」 포켓몬이 있을 때"},
   new[]{"적진에 「」 포켓몬이 있을 때","적진에 「○」 포켓몬이 있을 때"},new[]{"적진에 「??」 포켓몬이 있을 때","적진에 「○」 포켓몬이 있을 때"},
   new[]{"아군의 「빈사」상태인 포켓몬의 수가 상대보다 많을 때","「빈사」상태인 아군의 수가 상대보다 많을 때"},
   new[]{"자신의 기술이 「효과가굉장」일 때","자신의 기술이 「효과가 굉장」할 때"},
   new[]{"자신의 지시 랭크가 「S」일 때","자신의 지시가 「S」일 때"},
   new[]{"트레이너의 『숙달』지령을 받았을 때","『숙달지령』을 받았을 때"},
   new[]{"필드가 「모래바람」일 때","날씨가 「모래바람」일 때"},
   new[]{"적진에 「아이스번」이 설치되어 있을 때","적진에 「아이스번」이 있을 때"},
   new[]{"데이터를 해석했을 때","상대의 데이터를 해석했을 때"},
   new[]{"「아마네 카나타」가 트레이너일 때","「카나타」가 트레이너일 때"},new[]{"「이소노 나미헤이」가 트레이너일 때","「이소노 나미히라」가 트레이너일 때"}};
  static Dictionary<string,string> aliasKeys;
  static string BaseTriggerKey(string text){string key=Key((text??"").Replace("「속」","「속도」").Replace("「공」","「공격」"));foreach(var pair in TriggerSpellings)key=key.Replace(pair[0],pair[1]);foreach(var pair in TriggerMeanings)key=key.Replace(pair[0],pair[1]);foreach(var pattern in TriggerPatterns)key=Regex.Replace(key,pattern[0],pattern[1]);return key;}
  public static string TriggerKey(string text){if(aliasKeys==null){var map=new Dictionary<string,string>();foreach(var pair in TriggerAliases)map[BaseTriggerKey(pair[0])]=BaseTriggerKey(pair[1]);aliasKeys=map;}string key=BaseTriggerKey(text);string target;return aliasKeys.TryGetValue(key,out target)?target:key;}
  // 효과도 같은 효과를 하나로 합친다. 확률·횟수·배율·대상·지속시간·타입·범위(공격/기술, 물리/특수 등)가 다르면 합치지 않는다.
  static readonly string[][] EffectSpellings={new[]{"대미지","데미지"},new[]{"퍼텐셜","포텐셜"},new[]{"스피드","속도"},new[]{"필드","장소"},new[]{"됐을때","되었을때"},new[]{"쓰러트","쓰러뜨"},new[]{"턴종료시","T종료시"},new[]{"T종료시에","T종료시"}};
  static readonly string[][] EffectMeanings={new[]{"타입불일치인","타입불일치"},new[]{"타입불일치의","타입불일치"},new[]{"명중률","명중"},new[]{"위력:","위력"},new[]{"수가있다","수있다"},new[]{"상대기술","상대의기술"},new[]{"자신의",""},new[]{"자신이",""},new[]{"의상승(강화)","상승(강화)"},new[]{"최대체력","체력"},new[]{"전체체력","체력"},new[]{"추가행동권","추가행동"},new[]{"공격기술","공격"},new[]{"기능확장:","기능확장"},new[]{"특성:","특성"},new[]{"상대포켓몬","상대"},new[]{"의공의","의공격의"},new[]{"의속의","의속도의"},new[]{"의속을","의속도를"},new[]{"의공을","의공격을"},new[]{"처음으로","최초로"},new[]{"이상인","이상의"},new[]{"이하인","이하의"},new[]{"제일","가장"},new[]{"하고","해"},new[]{"(강화)을","(강화)를"},new[]{"속도을","속도를"},new[]{"방어을","방어를"},new[]{"추가공격권을얻는다","추가공격을행한다"},new[]{"같은랭크","동랭크"},new[]{"극화한다","극에달한다"},new[]{"기술로받는데미지","기술의데미지"},new[]{"감소(저하)되지","감소(저하)하지"}};
  static readonly string[][] EffectPatterns={new[]{@"^아군트레이너","트레이너"},new[]{@"기술을(강화|완화)\(","기술의위력을$1("},new[]{@"(으)?로서?(취급)?한다$","로한다"},new[]{@"(?<!추가)효과를부여","추가효과를부여"},new[]{@"(이|가)2배가된다$","2배가된다"}};
  static readonly string[][] EffectAliases={
   new[]{"자신을 포함한 아군 전원이 PT내에서 가장 많이 경험치를 얻은 아군과 같은 양의 경험치를 얻는다","자신을 포함한 아군 전원이 PT내에서 가장 많은 경험치를 얻은 아군과 같은 양의 경험치를 얻는다"},
   new[]{"자신의 「체력」종족치가 1랭크 오른고, 상대로부터의 상태이상을 받지 않는다","자신의 「체력」종족치가 1랭크 오르고, 상대로부터의 상태이상을 받지 않는다"},
   new[]{"자신의 타입 불일치「얼음/불꽃/전기」기술의 위력을 강화(1.5배)한다.","자신의 타입 불일치인 『불꽃/얼음/전기』기술의 위력을 강화(1.5배)한다."},
   new[]{"궁극의 『셀프 커맨드』에 의하 자신에게 『성의 주인』을 부여한다","궁극의 『셀프 커맨드』에 의해 자신에게 『성의 주인』을 부여한다"},
   new[]{"상대의 「물리기술」의 위력을 약화(0.67배)한다.","상대의 물리기술의 위력을 완화(0.67배)한다"},
   new[]{"「고스트」타입에게도 자신의 『회피』『내성』『격』이 발동한다","자신의 『회피』『내성』『격』이 「고스트」타입에게도 발동한다"},
   new[]{"상대 포켓몬에게 발생하는 「회복」을 「데미지」로 변경한다","상대 포켓몬에게 발생하는 「회복」을 「대미지」로 변환한다"},
   new[]{"상대의 「특수기술」을 무효화하고, 자신의 체력을 1/4감소한다","상대의 「특수기술」을 무효화하고, 자신의 체력을 1/4감소시킨다"},
   new[]{"상대의 특수기술을 무효화하고 체력을 1/4 소비한다","상대의 「특수기술」을 무효화하고, 자신의 체력을 1/4감소시킨다"},
   new[]{"상대의「효과가 굉장」의 기술의 위력을 반감한다","상대의 「효과가 굉장」한 기술의 위력을 반감한다"},
   new[]{"상대를 T종료시까지 「검은눈빛」상태로 한다.*1","T종료시까지 상대를 「검은눈빛」 상태로 한다"},
   new[]{"2T간 상대를 「텔레키네시스」상태로 한다","상대를 2T간 「텔레키네시스」상태로 한다"},
   new[]{"상대를 T 종료시까지 「혼란」상태로 한다","T종료시까지 상대를 「혼란」상태로 한다"},
   new[]{"상대의 소지품을 파괴해 자신의 기술의 위력을 강화(1.5배)한다.","자신의 기술의 위력을 강화(1.5배)해 상대의 소지품을 파괴한다"},
   new[]{"상대의 「데이터해석을 무효화」하는 포텐셜을 무시한다","상대의 「데이터 해석」을 무효화하는 『포텐셜』을 관통한다"},
   new[]{"T종료시까지 상대의 방호 포텐셜을 무시한다","T종료시까지 상대의 『방호포텐셜』을 무효화한다"},
   new[]{"자신의 기술의 「모으기」를 생략하고 기술을 내보낼 수 있다","자신의 「모으기기술」의 모으기를 생략하고 내보낼 수 있다"},
   new[]{"자신의 「격투」기술이 상대의 방어 등의 기술을 무시한다","자신의 「격투」기술이 상대의 「방어」등의 기술을 무효화한다"},
   new[]{"「추격」효과가 부여된 기술로 받는 데미지를 반감한다","「추격」효과가 부여된 기술의 데미지를 반감한다"},
   new[]{"자신의 기술의 「반동대미지」를 받지 않는다","자신의 「반동기술」의 대미지를 받지 않는다"},new[]{"자신의 기술의 반동의 데미지를 받지 않는다","자신의 「반동기술」의 대미지를 받지 않는다"},
   new[]{"자신으로의 데이터 해석을 무효화한다","자신에게로의 「데이터 해석」을 무효화한다"}};
  static Dictionary<string,string> effectAliasKeys;
  static string CleanEffectTail(string text){string t=Regex.Replace(text??"",@"(\s|\\|\.\*\d*|\.)+$","");while(t.EndsWith(")")&&t.Count(c=>c==')')>t.Count(c=>c=='(')){t=t.Substring(0,t.Length-1);t=Regex.Replace(t,@"(\s|\\|\.\*\d*|\.)+$","");}return t;}
  static string ExpandStats(string text){return Regex.Replace(text,"「([^」]*)」",m=>"「"+string.Join("/",m.Groups[1].Value.Split('/').Select(x=>x=="공"?"공격":x=="방"?"방어":x=="속"?"속도":x))+"」");}
  static string BaseEffectKey(string text){string prefix;string t=CleanEffectTail(StripActivation((text??"").Trim(),out prefix));foreach(var pair in new[]{new[]{"「」","「○」"},new[]{"「???」","「○」"},new[]{"「??」","「○」"},new[]{"「?」","「○」"},new[]{"『","「"},new[]{"』","」"},new[]{"」「","/"},new[]{"「불」","「불꽃」"}})t=t.Replace(pair[0],pair[1]);t=ExpandStats(t);string key=Regex.Replace(t,@"\s+","");foreach(string mark in new[]{"「","」",",",".","*","\\"})key=key.Replace(mark,"");foreach(var pair in EffectSpellings)key=key.Replace(pair[0],pair[1]);foreach(var pair in EffectMeanings)key=key.Replace(pair[0],pair[1]);foreach(var pattern in EffectPatterns)key=Regex.Replace(key,pattern[0],pattern[1]);return key;}
  public static string EffectKey(string text){if(effectAliasKeys==null){var map=new Dictionary<string,string>();foreach(var pair in EffectAliases)map[BaseEffectKey(pair[0])]=BaseEffectKey(pair[1]);effectAliasKeys=map;}string key=BaseEffectKey(text);string target;return effectAliasKeys.TryGetValue(key,out target)?target:key;}
  // 「자동」「선행」「1/자동」처럼 문장 앞에 붙은 발동 방식은 트리거가 아니므로 떼어 발동 방식으로 옮긴다.
  static readonly Regex ActivationPrefix=new Regex(@"^((?:\d+/)?(?:자동|선행|T시)(?:/\S+)?)\s+(.+)$");
  public static string StripActivation(string text,out string activation){activation="";string rest=text??"";Match m;while((m=ActivationPrefix.Match(rest)).Success){activation=Merge(activation,m.Groups[1].Value);rest=m.Groups[2].Value.Trim();}return rest;}
  static string Merge(string current,string value){var parts=(current??"").Split(new[]{" / "},StringSplitOptions.RemoveEmptyEntries).ToList();if(!string.IsNullOrEmpty(value)&&!parts.Contains(value))parts.Add(value);return string.Join(" / ",parts);}
  static void Add(Dictionary<string,PotentialPart> map,string kind,string text,PotentialRecord p,string source){
   text=kind=="T"?TriggerStructures.Normalize(text):EffectNormalizer.Normalize(text);if(text.Length==0)return;
   // 발동 방식·사용 횟수는 트리거가 아니라 포텐셜의 속성이므로 트리거를 나누지 않고 함께 모아 표시한다. 원문은 raws에 남는다.
   string mode=kind=="T"?Clean(p.activation):"",uses=kind=="T"?Clean(p.uses):"";
   if(kind=="T"){string prefix;text=StripActivation(text,out prefix);if(text.Length==0)return;mode=Merge(mode,prefix);}
   string key=kind=="T"?TriggerKey(text):EffectKey(text);PotentialPart part;
   if(!map.TryGetValue(key,out part)){string id;using(var sha=SHA256.Create())id=BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(kind+key))).Replace("-","").Substring(0,16).ToLowerInvariant();part=new PotentialPart{id=kind.ToLowerInvariant()+"-"+id,key=key,text=text,activation="",uses=""};map.Add(key,part);}
   if(kind=="T"){part.activation=Merge(part.activation,mode);part.uses=Merge(part.uses,uses);}part.variants.Add(text);
   if(!part.names.Contains(p.name))part.names.Add(p.name);if(!part.sources.Contains(source))part.sources.Add(source);if(!part.raws.Contains(p.raw))part.raws.Add(p.raw);
  }
  static IEnumerable<string> Lines(string value){return (value??"").Replace("\r","").Split('\n').Select(x=>Regex.Replace(x.Trim(),@"^\[[0-9]+\]\s*","")).Where(x=>x.Length>0);}
  public static PotentialParts Build(IEnumerable<DexPotential> entries){
   var triggers=new Dictionary<string,PotentialPart>();var effects=new Dictionary<string,PotentialPart>();
   foreach(var item in entries){var p=item.record;string extraTrigger,cleanEffect;PotentialLibrary.Split(p.effect,out extraTrigger,out cleanEffect);
    foreach(var text in Lines(p.trigger).Concat(Lines(extraTrigger)))Add(triggers,"T",text,p,item.source);
    foreach(var text in Lines(cleanEffect))Add(effects,"E",text,p,item.source);
   }
   // 합쳐진 트리거는 가장 많이 쓰인 표기를, 같으면 용어 괄호가 있는 표기를 대표 문장으로 쓴다.
   foreach(var part in triggers.Values){part.text=part.variants.GroupBy(x=>x).OrderByDescending(x=>x.Count()).ThenByDescending(x=>BaseTriggerKey(x.Key)==part.key).ThenByDescending(x=>x.Key.Count(c=>c=='「'||c=='『')).ThenBy(x=>x.Key.Length).First().Key;part.variants=part.variants.Distinct().ToList();}
   foreach(var part in effects.Values){part.text=part.variants.GroupBy(x=>x).OrderByDescending(x=>x.Count()).ThenByDescending(x=>BaseEffectKey(x.Key)==part.key).ThenByDescending(x=>!Regex.IsMatch(x.Key,@"^(?:\d+/)?(?:자동|선행|T시)\s")).ThenByDescending(x=>x.Key.Count(c=>c=='「'||c=='『')).ThenBy(x=>x.Key.Length).First().Key;part.variants=part.variants.Distinct().ToList();}
   var result=new PotentialParts{triggers=triggers.Values.OrderBy(x=>x.text).ToList(),effects=effects.Values.OrderBy(x=>x.text).ToList()};var protectedKeys=new HashSet<string>(ProtectedEffectTexts().Select(EffectKey));foreach(var effect in result.effects){effect.protectedTemplate=protectedKeys.Contains(effect.key);if(!effect.protectedTemplate)effect.excludedFromImplementation=TryExclusionReason(effect.text,out effect.exclusionReason);}return result;
  }
  public static bool TryExclusionReason(string text,out string reason){
   string value=Clean(text);reason="";
   if(value.Contains("고확률")){reason="지원하지 않는 확률 표현";return true;}
   if(Regex.IsMatch(value,@"(쭉\s*오른다|급격히\s*오른다)")){reason="삭제된 랭크 표현";return true;}
   if(Regex.IsMatch(value,@"^[\(（].*[\)）]$")){reason="독립 주석";return true;}
   if(value=="PT포텐셜"){reason="분류 표기";return true;}
   if(Regex.IsMatch(value,@"(대회.*참가할 수 없다|대회.*밖에 참가할 수 없다|PT.*(참가|엔트리|소속).*수 (있|없)다|PT.*(참가|엔트리|소속).*수가 없다|함께 엔트리할 수 없다|배틀에 엔트리할 수 있다|트레이너.*PT.*(참가|소속)|트레이너에게 소속될 수 없다|필요통솔을)")||Regex.IsMatch(value,@"^이 (PT는|포텐셜은 PT의|퍼텐셜은 PT의).*(사용|발동)")){reason="편성·공유 규칙";return true;}
   if(Regex.IsMatch(value,@"(『유대』를 얻을 수 없다|경험치를 얻|획득상금)")){reason="배틀 외 획득 규칙";return true;}
   return false;
  }
  static IEnumerable<string> ProtectedEffectTexts(){
   var options=EditorTemplate.RolePotentials.Concat(EditorTemplate.CommonPotentials).Concat(EditorTemplate.InitiativePotentials).Concat(EditorTemplate.PrivilegePotentials).Concat(EditorTemplate.CounterTypeNames().SelectMany(type=>new[]{"회피","내성","격"}.SelectMany(slot=>EditorTemplate.CounterOptions(slot,type))));
   foreach(var option in options){string trigger,effect;PotentialLibrary.Split(option.effect,out trigger,out effect);foreach(string line in Lines(effect))yield return EffectNormalizer.Normalize(line);if(option.adjunctEffect.Length>0){PotentialLibrary.Split(option.adjunctEffect,out trigger,out effect);foreach(string line in Lines(effect))yield return EffectNormalizer.Normalize(line);}}
   foreach(var record in EditorTemplate.Orders(false).Concat(EditorTemplate.Orders(true)).Concat(EditorTemplate.ExtendedOrders(false)).Concat(EditorTemplate.ExtendedOrders(true))){string trigger,effect;PotentialLibrary.Split(record.effect,out trigger,out effect);foreach(string line in Lines(effect))yield return EffectNormalizer.Normalize(line);}
  }
  public static IEnumerable<DexPotential> StandardEntries(){
   var orders=EditorTemplate.Orders(false).Concat(EditorTemplate.Orders(true)).Concat(EditorTemplate.ExtendedOrders(false)).Concat(EditorTemplate.ExtendedOrders(true)).GroupBy(x=>x.name).Select(x=>new DexPotential{record=x.First(),source="사용자 제공 양식 / 지령",urls=new string[0]});
   var conditions=EditorTemplate.PotentialTriggerCatalog.Select(x=>new DexPotential{record=new PotentialRecord{trigger=x,name="공통 조건",raw=x},source="기본 트리거 목록",urls=new string[0]});
   return PotentialLibrary.Load().Concat(orders).Concat(conditions);
  }
  public static void Export(string path){var data=Build(StandardEntries());File.WriteAllText(path,new JavaScriptSerializer{MaxJsonLength=30000000}.Serialize(data),new UTF8Encoding(false));}
  public static void Test(){
   var data=Build(new[]{
    new DexPotential{record=new PotentialRecord{name="A",trigger="필드에 나왔을 때,",effect="위력을 강화(１.５배)한다.",uses="1/시"},source="A"},
    new DexPotential{record=new PotentialRecord{name="B",trigger="[1] 필드에 나왔을때",effect="위력을  강화(1.5배)한다",uses="1/시"},source="B"},
    new DexPotential{record=new PotentialRecord{name="C",trigger="필드에 나왔을 때",effect="위력을 강화(2배)한다",uses="2/시"},source="C"}});
   if(data.triggers.Count!=1||data.triggers[0].uses!="1/시 / 2/시"||data.triggers[0].sources.Count!=3||data.effects.Count!=2||!data.effects.Any(x=>x.sources.Count==2))throw new Exception("Parts deduplication/provenance failed");
   var spelling=Build(new[]{new DexPotential{record=new PotentialRecord{name="D",trigger="자신의 체력이 1/2 이하가 됐을 때"},source="D"},new DexPotential{record=new PotentialRecord{name="E",trigger="자신의 체력이 「1/2」이하가 되었을 때"},source="E"},new DexPotential{record=new PotentialRecord{name="F",trigger="상대보다 먼저 행동할 때"},source="F"},new DexPotential{record=new PotentialRecord{name="G",trigger="상대보다 먼저 행동했을 때"},source="G"},new DexPotential{record=new PotentialRecord{name="H",trigger="상대의 「접촉」기술을 받으면"},source="H"},new DexPotential{record=new PotentialRecord{name="I",trigger="상대의 접촉기술을 받았을 때"},source="I"}});
   var meaning=Build(new[]{new DexPotential{record=new PotentialRecord{name="J",trigger="자동 「방어」등의 기술로 상대의 공격기술을 무효화했을 때"},source="J"},new DexPotential{record=new PotentialRecord{name="K",trigger="상대의 공격을 「방어」등의 기술로 무효화했을 때"},source="K"},new DexPotential{record=new PotentialRecord{name="L",trigger="「방어」등의 기술로 상대의 기술을 무효화했을 때"},source="L"},new DexPotential{record=new PotentialRecord{name="M",trigger="「도발」상태일 때"},source="M"},new DexPotential{record=new PotentialRecord{name="N",trigger="자신이 「도발」상태일 때"},source="N"},new DexPotential{record=new PotentialRecord{name="O",trigger="상대가 「도발」상태일 때"},source="O"},new DexPotential{record=new PotentialRecord{name="P",trigger="아군 트레이너가 「통솔:A」이상일 때"},source="P"},new DexPotential{record=new PotentialRecord{name="Q",trigger="트레이너가 통솔 「A」이상일 때"},source="Q"},new DexPotential{record=new PotentialRecord{name="R",trigger="트레이너가 「통솔:B」 이상일 때"},source="R"}});
   if(meaning.triggers.Count!=6||!meaning.triggers.Any(x=>x.sources.Count==2&&x.activation=="자동"&&x.text.Contains("공격"))||!meaning.triggers.Any(x=>x.sources.Count==2&&x.text.Contains("도발"))||!meaning.triggers.Any(x=>x.sources.Contains("P")&&x.sources.Contains("Q")))throw new Exception("Trigger meaning merge failed: "+string.Join(" / ",meaning.triggers.Select(x=>x.text+"["+x.activation+"]("+x.sources.Count+")")));
   var effectMerge=Build(new[]{new DexPotential{record=new PotentialRecord{name="S",effect="자동 상대의 「추격」효과가 부여된 기술을 무효화한다"},source="S"},new DexPotential{record=new PotentialRecord{name="T",effect="상대의 「추격」효과가 부여된 기술을 무효화한다.*1"},source="T"},new DexPotential{record=new PotentialRecord{name="U",effect="상대의 「」 기술의 데미지를 반감한다"},source="U"},new DexPotential{record=new PotentialRecord{name="V",effect="상대의 기술의 대미지를 반감한다"},source="V"},new DexPotential{record=new PotentialRecord{name="W",effect="자신의 공격에 「3할:화상」의 추가효과를 부여한다"},source="W"},new DexPotential{record=new PotentialRecord{name="X",effect="자신의 기술에 「3할:화상」의 추가 효과를 부여한다"},source="X"},new DexPotential{record=new PotentialRecord{name="Y",effect="저확률로 상대를 「독」상태로 한다"},source="Y"},new DexPotential{record=new PotentialRecord{name="Z",effect="중확률로 상대를 「독」 상태로 한다"},source="Z"},new DexPotential{record=new PotentialRecord{name="AA",effect="자신의 「방/특방」을 강화(1.33배)한다."},source="AA"},new DexPotential{record=new PotentialRecord{name="AB",effect="방어/특방을 강화(1.33배)한다"},source="AB"}});
   if(effectMerge.effects.Count!=8||!effectMerge.effects.Any(x=>x.sources.Contains("S")&&x.sources.Contains("T")&&!x.text.StartsWith("자동"))||effectMerge.effects.Any(x=>x.sources.Contains("U")&&x.sources.Contains("V"))||effectMerge.effects.Any(x=>x.sources.Contains("W")&&x.sources.Contains("X"))||effectMerge.effects.Any(x=>x.sources.Contains("Y")&&x.sources.Contains("Z"))||!effectMerge.effects.Any(x=>x.sources.Contains("AA")&&x.sources.Contains("AB")))throw new Exception("Effect merge failed: "+string.Join(" / ",effectMerge.effects.Select(x=>x.text+"("+string.Join(",",x.sources)+")")));
   if(spelling.triggers.Count!=4||!spelling.triggers.Any(x=>x.sources.Count==2&&x.text.Contains("1/2")))throw new Exception("Trigger spelling merge failed: "+string.Join(" / ",spelling.triggers.Select(x=>x.text)));
   if(Key("저확률로 회피") == Key("중확률로 회피"))throw new Exception("Probability collapsed");
   var all=Build(StandardEntries());if(all.triggers.Count<100||all.effects.Count<100||all.triggers.Select(x=>x.key).Distinct().Count()!=all.triggers.Count||all.effects.Select(x=>x.key).Distinct().Count()!=all.effects.Count)throw new Exception("Part catalog uniqueness failed");if(all.effects.Count(x=>x.protectedTemplate)<20||!all.effects.Any(x=>x.protectedTemplate&&x.text.Contains("전능력치"))||all.effects.Any(x=>x.protectedTemplate&&x.excludedFromImplementation))throw new Exception("Template effect protection failed");if(all.effects.Count(x=>x.excludedFromImplementation)!=49)throw new Exception("Unexpected implementation exclusion count");
   string reason;if(!TryExclusionReason("(※1T이 아니라, 1번의 공격에 대해 반응)",out reason)||reason!="독립 주석"||!TryExclusionReason("PT에 참가할 수 없다",out reason)||reason!="편성·공유 규칙"||!TryExclusionReason("속도가 쭉 오른다",out reason)||reason!="삭제된 랭크 표현"||TryExclusionReason("PT 전원이 기술 「파도타기」를 내보낼 수 있다",out reason))throw new Exception("Implementation exclusion classification failed");
  }
 }
}
