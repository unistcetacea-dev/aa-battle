using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Globalization;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace AABattle {
 // 효과 문장 정규화와 구조 판정. 편집기 UI(EffectBuilder.cs)와 엔진이 함께 쓴다
 public static class EffectNormalizer {
  static readonly Regex Scalar=new Regex(@"(?<target>(?:자신|상대|아군)[^,.\n]*?)\s*(?:을|를|이|가)\s*(?<action>강화|약화|저하)\s*\(\s*(?<rate>[0-9]+(?:\.[0-9]+)?)\s*배\s*\)\s*(?:한다|시킨다|된다)",RegexOptions.Compiled);
  static readonly Regex Probability=new Regex(@"^(?<label>드물게|저확률|중확률|고확률)(?:로)?\s+(?<effect>.+)$",RegexOptions.Compiled);
  const string RankTarget=@"(?<target>.+?(?:전능력치|능력치|능력|「(?:공격|방어|특공|특방|속도|속|명중|회피|C|공/특공|방/특방|공격/특공|방어/특방)」|공격|방어|특공|특방|속도|명중|회피))";
  static readonly Regex RankUp=new Regex("^"+RankTarget+@"(?:을|를|이|가)\s*(?:(?<rank>[123])\s*랭크\s*)?(?<degree>매우 크게|크게)?\s*(?:올린다|오른다|상승시킨다|상승한다)$",RegexOptions.Compiled);
  static readonly Regex RankDown=new Regex("^"+RankTarget+@"(?:을|를|이|가)\s*(?:(?<rank>[123])\s*랭크\s*)?(?<degree>매우 크게|크게)?\s*(?:내린다|떨어진다|저하한다|하락한다|저하시킨다)$",RegexOptions.Compiled);
  public static readonly string[] ProbabilityChoices={"없음","드물게","저확률","중확률"};
  public static bool TryProbabilityPrefix(string text,out string probability,out string effect){probability=effect="";string value=(text??"").Normalize(System.Text.NormalizationForm.FormKC).Trim();var match=Probability.Match(value);if(!match.Success)return false;probability=match.Groups["label"].Value;effect=match.Groups["effect"].Value.Trim();return effect.Length>0;}
  public static string WithProbability(string probability,string effect){string value=(effect??"").Trim();if(value.Length==0)return "";switch(probability??""){case "드물게":return "드물게 "+value;case "저확률":return "저확률로 "+value;case "중확률":return "중확률로 "+value;case "고확률":return "고확률로 "+value;default:return value;}}
  public static string Normalize(string text){
   string probability,body;bool probabilistic=TryProbabilityPrefix(text,out probability,out body);string clean=PotentialParts.Clean(probabilistic?body:text);
   clean=Regex.Replace(clean,@"임의의\s*능력치","임의의 능력");
   clean=Regex.Replace(clean,@"랜덤한\s*능력(?!치)","랜덤한 능력치");
   clean=Regex.Replace(clean,@"제일\s*(높은|낮은)\s*능력치?",m=>"가장 "+m.Groups[1].Value+" 능력");
   clean=Regex.Replace(clean,@"가장\s*(높은|낮은)\s*능력치",m=>"가장 "+m.Groups[1].Value+" 능력");
   // 편집기 모듈 표준 문장으로 바꿀 수 있는 변형(배율 동사·기술 조건·종족치·최대 HP 1/N)
   string module=EffectModules.Normalize(clean);
   if(module!=null)return probabilistic?WithProbability(probability,module):module;
   var rank=RankUp.Match(clean);if(!rank.Success)rank=RankDown.Match(clean);string normalized;if(rank.Success&&!rank.Groups["target"].Value.Contains("까지")){int amount=rank.Groups["rank"].Success?int.Parse(rank.Groups["rank"].Value):rank.Groups["degree"].Value=="매우 크게"?3:rank.Groups["degree"].Value=="크게"?2:1;normalized=CanonicalRank(rank.Groups["target"].Value,RankDown.IsMatch(clean)?-amount:amount);}else normalized=Scalar.Replace(clean,m=>Canonical(m.Groups["target"].Value,m.Groups["action"].Value,double.Parse(m.Groups["rate"].Value,System.Globalization.CultureInfo.InvariantCulture)));return probabilistic?WithProbability(probability,normalized):normalized;
  }
  public static string NormalizeLines(string text){return string.Join("\r\n",(text??"").Replace("\r","").Split('\n').Select(Normalize).Where(x=>x.Length>0));}
  public static string Canonical(string target,string action,double rate){
   target=PotentialParts.Clean(target);string verb=action=="강화"?"강화":"약화";
   return target+Particle(target)+" "+verb+"("+rate.ToString("0.##",System.Globalization.CultureInfo.InvariantCulture)+"배)한다.";
  }
  public static string CanonicalRank(string target,int amount){target=PotentialParts.Clean(target);bool down=amount<0;amount=Math.Max(1,Math.Min(3,Math.Abs(amount)));return target+(down?Particle(target):SubjectParticle(target))+" "+(amount==3?"매우 크게 ":amount==2?"크게 ":"")+(down?"내린다.":"오른다.");}
  public static string CanonicalPriority(string target,int amount){target=PotentialParts.Clean(target);int value=Math.Max(1,Math.Min(7,Math.Abs(amount)));return target+"의 기술 우선도를 "+value+" "+(amount<0?"내린다.":"올린다.");}
  public static string CanonicalSwitch(){return "아군과 임의교대한다.";}
  public static string CanonicalPivotGrant(){return "자신의 기술에 「유턴」 효과를 부여한다.";}
  public static string CanonicalSwitchRestriction(){return "상대는 「통상교대」 이외의 수단으로 교대할 수 없다.";}
  public static string CanonicalAnalysisPermission(){return "상대의 데이터를 해석할 수 있다.";}
  public static string CanonicalStatIgnore(string target,string stats,string mode){target=PotentialParts.Clean(target);stats=PotentialParts.Clean(stats);string value=mode=="강화"?"강화":"상승(강화)";return target+"의 "+stats+" "+value+(mode=="강화"?"를":"을")+" 무시한다.";}
  public static bool TryFraction(string text,out string target,out string action,out int denominator){return EffectStructures.TryFraction(text,out target,out action,out denominator);}
  static string Particle(string value){if(string.IsNullOrEmpty(value))return "을";for(int i=value.Length-1;i>=0;i--){char c=value[i];if(c>='가'&&c<='힣')return ((c-'가')%28)==0?"를":"을";}return "을";}
  static string SubjectParticle(string value){if(string.IsNullOrEmpty(value))return "이";for(int i=value.Length-1;i>=0;i--){char c=value[i];if(c>='가'&&c<='힣')return ((c-'가')%28)==0?"가":"이";}return "이";}
  public static void Test(){
   if(Normalize("자신의 기술의 위력이 강화(1.50배)된다.")!="자신의 기술의 위력을 강화(1.5배)한다.")throw new Exception("Effect normalization failed");
   string weak=Normalize("상대의 「방어」를 저하(0.8배)시킨다.");if(weak!="상대의 「방어」를 약화(0.8배)한다.")throw new Exception("Effect weakening normalization failed: "+weak);
   if(Normalize("자신의 「공격」을 1랭크 올린다.").Contains("강화"))throw new Exception("Rank effect must stay distinct");
   string probability,effect;if(!TryProbabilityPrefix("저확률로 자신의 「공격」을 1랭크 올린다.",out probability,out effect)||probability!="저확률"||effect!="자신의 「공격」을 1랭크 올린다.")throw new Exception("Probability prefix parsing failed");if(Normalize("저확률로 자신의 「공격」을 2랭크 올린다.")!="저확률로 자신의 「공격」이 크게 오른다.")throw new Exception("Probability prefix normalization failed");if(WithProbability("중확률","자신의 공격이 오른다.")!="중확률로 자신의 공격이 오른다.")throw new Exception("Probability prefix formatting failed");
   if(Normalize("자신의 「공격」을 상승시킨다")!="자신의 「공격」이 오른다."||Normalize("자신의 「공격」을 크게 올린다")!="자신의 「공격」이 크게 오른다."||Normalize("자신의 「공격」을 3랭크 올린다")!="자신의 「공격」이 매우 크게 오른다.")throw new Exception("Rank wording normalization failed");
   if(Normalize("상대의 「방어」가 떨어진다")!="상대의 「방어」를 내린다."||Normalize("상대의 「방어」를 크게 내린다")!="상대의 「방어」를 크게 내린다."||Normalize("상대의 「방어」를 3랭크 내린다")!="상대의 「방어」를 매우 크게 내린다.")throw new Exception("Rank decrease normalization failed");
   if(Normalize("자신의 임의의 능력치를 올린다")!="자신의 임의의 능력이 오른다."||!Normalize("자신의 랜덤한 능력을 올린다").Contains("랜덤한 능력치"))throw new Exception("Stat choice wording normalization failed");
   if(Normalize("상대의 제일 높은 능력치를 내린다")!="상대의 가장 높은 능력을 내린다."||Normalize("자신의 가장 낮은 능력치를 올린다")!="자신의 가장 낮은 능력이 오른다.")throw new Exception("Extreme stat wording normalization failed");
   if(CanonicalPriority("상대",-2)!="상대의 기술 우선도를 2 내린다."||CanonicalSwitch()!="아군과 임의교대한다."||CanonicalPivotGrant().IndexOf("유턴",StringComparison.Ordinal)<0)throw new Exception("Priority or switch wording failed");
  }
 }
 public static class PotentialStatRules {
  public static readonly string[] Arbitrary={"공격","방어","특공","특방","속도","명중","회피","C"};
  public static readonly string[] Random={"공격","방어","특공","특방","속도"};
  public static string ChooseRandom(Func<double> random){if(random==null)throw new ArgumentNullException("random");double roll=Math.Max(0,Math.Min(.999999999,random()));return Random[(int)(roll*Random.Length)];}
  public static int ExtremeIndex(int[] values,bool highest,Func<double> random){if(values==null||values.Length!=5)throw new ArgumentException("Five battle stat values are required","values");if(random==null)throw new ArgumentNullException("random");int extreme=highest?values.Max():values.Min();var tied=Enumerable.Range(0,5).Where(i=>values[i]==extreme).ToArray();double roll=Math.Max(0,Math.Min(.999999999,random()));return tied[(int)(roll*tied.Length)];}
  public static string ChooseExtreme(Fighter fighter,Fighter opponent,Rules rules,bool highest,Func<double> random){var values=Enumerable.Range(1,5).Select(i=>Engine.Effective(fighter,opponent,i,rules)).ToArray();return Random[ExtremeIndex(values,highest,random)];}
  public static string ChooseExtremeBase(Pokemon pokemon,bool highest,Func<double> random){if(pokemon==null||pokemon.@base==null||pokemon.@base.Length<6)throw new ArgumentException("Pokemon base stats are required","pokemon");return Random[ExtremeIndex(pokemon.@base.Skip(1).Take(5).ToArray(),highest,random)];}
  public static int CopyBase(Pokemon pokemon,int sourceIndex){if(pokemon==null||pokemon.@base==null||pokemon.@base.Length<6||sourceIndex<0||sourceIndex>5)throw new ArgumentException("Pokemon base stat source is required");return pokemon.@base[sourceIndex];}
  public static void Test(){var p=new Pokemon{@base=new[]{1,100,200,200,50,70}};if(Arbitrary.Length!=8||Arbitrary.Last()!="C"||Random.Length!=5||Random.Contains("명중")||ChooseRandom(()=>0)!="공격"||ChooseRandom(()=>.999)!="속도"||ExtremeIndex(new[]{100,200,200,50,70},true,()=>0)!=1||ExtremeIndex(new[]{100,200,200,50,50},true,()=>.999)!=2||ExtremeIndex(new[]{100,200,200,50,50},false,()=>.999)!=4||ChooseExtremeBase(p,true,()=>.999)!="특공"||CopyBase(p,4)!=50)throw new Exception("Potential stat selection rules failed");}
 }
 public static class SpeciesStatRanks {
  static readonly int[] Starts={1,40,60,90,110,140,170,200};
  public static readonly string[] Labels={"E-","E","E+","D-","D","D+","C-","C","C+","B-","B","B+","A-","A","A+","AA-","AA","AA+","AAA-","AAA","AAA+","S"};
  public static int[] RaisedRange(int value,int amount=1){int tier=Tier(value),modifier=Modifier(value,tier);int next=Math.Min(Starts.Length-1,tier+Math.Max(1,amount));if(next==Starts.Length-1)return new[]{200,999};int start=Starts[next],end=Starts[next+1]-1;if(modifier<0)return new[]{start,start};if(modifier>0)return new[]{end-4,end};return new[]{start+1,end-5};}
  static int Tier(int value){for(int i=Starts.Length-1;i>=0;i--)if(value>=Starts[i])return i;return 0;}
  static int Modifier(int value,int tier){if(tier>=Starts.Length-1)return 0;int start=Starts[tier],end=Starts[tier+1]-1;if(value==start)return -1;if(value>=end-4)return 1;return 0;}
  // 등급 지정: 표시(-·+)가 있으면 그 범위, 없으면 현재 값의 표시를 이어받는다(B-→A-, B+→A+, B→A 범위)
  public static int[] GradeRange(int current,string grade){grade=(grade??"").Trim();if(grade=="S"||grade.EndsWith("-")||grade.EndsWith("+"))return LabelRange(grade);int tier=Tier(current),modifier=Modifier(current,tier);return LabelRange(grade+(modifier<0?"-":modifier>0?"+":""));}
  public static int[] LabelRange(string label){if(label=="S")return new[]{200,999};int modifier=label.EndsWith("-")?-1:label.EndsWith("+")?1:0;string core=label.TrimEnd('-','+');int tier=Array.IndexOf(new[]{"E","D","C","B","A","AA","AAA"},core);if(tier<0)throw new ArgumentException("Unknown species rank","label");int start=Starts[tier],end=Starts[tier+1]-1;if(modifier<0)return new[]{start,start};if(modifier>0)return new[]{end-4,end};return new[]{start+1,end-5};}
  public static void Test(){if(!RaisedRange(90).SequenceEqual(new[]{110,110})||!RaisedRange(100).SequenceEqual(new[]{111,134})||!RaisedRange(105).SequenceEqual(new[]{135,139})||!RaisedRange(135).SequenceEqual(new[]{165,169})||!RaisedRange(90,2).SequenceEqual(new[]{140,140})||!RaisedRange(105,2).SequenceEqual(new[]{165,169})||!LabelRange("A").SequenceEqual(new[]{111,134})||!LabelRange("AA+").SequenceEqual(new[]{165,169}))throw new Exception("Species stat rank ranges failed");}
 }
 public static class FractionalHpRule {
  public static int Amount(int maxHp,int denominator){if(denominator<1)throw new ArgumentOutOfRangeException("denominator");return Math.Max(1,maxHp/denominator);}
  public static int DamageHp(int currentHp,int maxHp,int denominator,bool canFaint){int left=currentHp-Amount(maxHp,denominator);return canFaint?Math.Max(0,left):Math.Max(1,left);}
  public static int HealHp(int currentHp,int maxHp,int denominator){return Math.Min(maxHp,currentHp+Amount(maxHp,denominator));}
  public static void Test(){if(Amount(101,4)!=25||DamageHp(10,100,4,true)!=0||DamageHp(10,100,4,false)!=1||HealHp(90,101,4)!=101)throw new Exception("Fractional HP rule failed");}
 }
 public static class EffectStructures {
  static readonly Regex Fraction=new Regex(@"^(?<target>자신|상대|아군 1체|아군 전체|아군)(?:의|에게)?\s*(?:(?:최대|전체)\s*)?체력(?:의|을|를|이|가)?\s*1/(?<n>[0-9]+)(?:만큼|을|를|의)?\s*(?<action>회복한다|회복시킨다|감소시킨다|감소한다|대미지를 준다|데미지를 준다)$",RegexOptions.Compiled);
  static readonly Regex Scalar=new Regex(@"^(?<target>(?:자신|상대|아군)[^,.\n]*?)\s*(?:을|를)\s*(?<action>강화|약화)\((?<rate>[0-9]+(?:\.[0-9]+)?)배\)한다$",RegexOptions.Compiled);
  static readonly Regex Priority=new Regex(@"^(?<target>자신|상대|아군(?:\s+1체|\s+전체)?)의 기술 우선도를 (?<amount>[1-7]) (?<direction>올린다|내린다)\.?$",RegexOptions.Compiled);
  static readonly Regex StatIgnore=new Regex(@"^(?<target>『[^』]+』|자신|상대|아군)의\s*(?<stats>전능력치|능력|「[^」]+」|[가-힣A-Za-z/]+?)(?:의)?\s*(?<mode>상승\(강화\)|강화)(?:을|를)\s*무시한다\.?\s*(?:\([^)]*\))?$",RegexOptions.Compiled);
  static string Body(string text){string normalized=EffectNormalizer.Normalize(text);string probability,effect;return EffectNormalizer.TryProbabilityPrefix(normalized,out probability,out effect)?effect:normalized;}
  public static bool IsScalar(string text){return Regex.IsMatch(PotentialParts.Clean(Body(text)),@"^(?:자신|상대|아군)[^,]*?\s*(?:을|를)\s*(?:강화|약화)\([0-9]+(?:\.[0-9]+)?배\)한다$");}
  public static bool IsRank(string text){return Regex.IsMatch(PotentialParts.Clean(Body(text)),@"^.+?(?:(?:이|가)\s*(?:매우 크게 |크게 )?오른다|(?:을|를)\s*(?:매우 크게 |크게 )?내린다)$");}
  public static bool IsPriority(string text){string target,action;int amount;return TryPriority(text,out target,out action,out amount);}
  public static bool IsSwitchEffect(string text){return SwitchEffectRuntime.IsSwitchEffect(Body(text));}
  public static bool IsPivotGrant(string text){return SwitchEffectRuntime.IsPivotGrant(Body(text));}
  public static bool IsSwitchRestriction(string text){return PotentialParts.Clean(Body(text)).StartsWith(PotentialParts.Clean(EffectNormalizer.CanonicalSwitchRestriction()),StringComparison.Ordinal);}
  public static bool IsAnalysisPermission(string text){return PotentialParts.Clean(Body(text)).StartsWith(PotentialParts.Clean(EffectNormalizer.CanonicalAnalysisPermission()),StringComparison.Ordinal);}
  public static bool TryScalar(string text,out string target,out string action,out double rate){var m=Scalar.Match(PotentialParts.Clean(Body(text)));target=action="";rate=0;if(!m.Success)return false;target=m.Groups["target"].Value;action=m.Groups["action"].Value;return double.TryParse(m.Groups["rate"].Value,System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out rate)&&rate>0;}
  public static bool TryPriority(string text,out string target,out string action,out int amount){var m=Priority.Match(PotentialParts.Clean(Body(text)));target=action="";amount=0;if(!m.Success)return false;target=m.Groups["target"].Value;action=m.Groups["direction"].Value=="내린다"?"하락":"상승";return int.TryParse(m.Groups["amount"].Value,out amount)&&amount>0;}
  public static bool TryStatIgnore(string text,out string target,out string stats,out string mode){var match=StatIgnore.Match(PotentialParts.Clean(Body(text)));target=stats=mode="";if(!match.Success)return false;target=match.Groups["target"].Value;stats=match.Groups["stats"].Value.TrimEnd('의');mode=match.Groups["mode"].Value;return true;}
  public static bool IsStatIgnore(string text){string target,stats,mode;return TryStatIgnore(text,out target,out stats,out mode);}
  public static bool TryFraction(string text,out string target,out string action,out int denominator){var m=Fraction.Match(PotentialParts.Clean(text));target=action="";denominator=0;if(!m.Success)return false;target=m.Groups["target"].Value;action=m.Groups["action"].Value.Contains("회복")?"회복":"대미지";return int.TryParse(m.Groups["n"].Value,out denominator)&&denominator>0;}
  // 도감용: 같은 모듈 문장(변형)을 모듈 템플릿 하나로 묶는다. 모듈 판정은 EffectModules.Classify
  public static List<PotentialPart> CollapseByModule(IEnumerable<PotentialPart> source){var input=source.Where(x=>!x.excludedFromImplementation).ToList();var result=new List<PotentialPart>();var grouped=new HashSet<PotentialPart>();foreach(var group in input.Where(x=>!x.protectedTemplate).GroupBy(x=>EffectModules.Classify(x.text)).Where(g=>g.Key.Length>0&&!g.Key.Contains(" + "))){var list=group.ToList();foreach(var x in list)grouped.Add(x);result.Add(Template("module-"+group.Key,group.Key+" — 편집기 구조화 효과 모듈",list));}result.AddRange(input.Where(x=>!grouped.Contains(x)));return result.OrderBy(x=>string.IsNullOrEmpty(x.structureKind)?1:0).ThenBy(x=>x.text).ToList();}
  public static List<PotentialPart> Collapse(IEnumerable<PotentialPart> source){var input=source.Where(x=>!x.excludedFromImplementation).ToList();var result=new List<PotentialPart>();var scalar=input.Where(x=>!x.protectedTemplate&&IsScalar(x.text)).ToList();var fraction=input.Where(x=>!x.protectedTemplate&&TryFractionOnly(x.text)).ToList();var rank=input.Where(x=>!x.protectedTemplate&&IsRank(x.text)).ToList();var priority=input.Where(x=>!x.protectedTemplate&&IsPriority(x.text)).ToList();var statIgnore=input.Where(x=>!x.protectedTemplate&&IsStatIgnore(x.text)).ToList();var switches=input.Where(x=>!x.protectedTemplate&&IsSwitchEffect(x.text)).ToList();var pivots=input.Where(x=>!x.protectedTemplate&&IsPivotGrant(x.text)).ToList();var grouped=new HashSet<PotentialPart>(scalar.Concat(fraction).Concat(rank).Concat(priority).Concat(statIgnore).Concat(switches).Concat(pivots));result.AddRange(input.Where(x=>!grouped.Contains(x)));if(scalar.Count>0)result.Add(Template("scalar","수치 배율 — 대상 / 강화·약화 / 배율을 선택",scalar));if(fraction.Count>0)result.Add(Template("fraction","최대 HP의 1/N — 대상 / 회복·대미지 / 빈사 여부를 선택",fraction));if(rank.Count>0)result.Add(Template("rank","능력 랭크 변화 — 대상 / 상승·하락 / 1·2·3랭크를 선택",rank));if(priority.Count>0)result.Add(Template("priority","우선도 변화 — 대상 / 상승·하락 / 1~7을 선택",priority));if(statIgnore.Count>0)result.Add(Template("stat-ignore","능력 상승·강화 무시 — 대상 / 능력치 범위 / 상승(강화)·강화만을 선택",statIgnore));if(switches.Count>0)result.Add(Template("switch","교대 효과 — 아군과 임의교대",switches));if(pivots.Count>0)result.Add(Template("pivot","유턴 효과 — 기술에 유턴 효과 부여",pivots));return result.OrderBy(x=>x.text).ToList();}
  static bool TryFractionOnly(string text){string target,action;int n;return TryFraction(text,out target,out action,out n);}
  static PotentialPart Template(string kind,string text,List<PotentialPart> variants){var p=new PotentialPart{id="structure-"+kind,key="structure-"+kind,text=text,structureKind=kind,variantCount=variants.Count,searchText=string.Join(" ",variants.Select(x=>x.text))};foreach(var x in variants){foreach(var n in x.names)if(!p.names.Contains(n))p.names.Add(n);foreach(var s in x.sources)if(!p.sources.Contains(s))p.sources.Add(s);foreach(var r in x.raws)if(!p.raws.Contains(r))p.raws.Add(r);}return p;}
  public static void Test(){string target,action;int n;double rate;int amount;if(!TryFraction("자신의 체력을 1/4 회복한다",out target,out action,out n)||target!="자신"||action!="회복"||n!=4||!TryScalar("자신의 기술의 위력을 강화(1.5배)한다",out target,out action,out rate)||Math.Abs(rate-1.5)>0.001||!TryPriority("상대의 기술 우선도를 2 내린다",out target,out action,out amount)||target!="상대"||action!="하락"||amount!=2||!IsSwitchEffect(EffectNormalizer.CanonicalSwitch())||!IsPivotGrant(EffectNormalizer.CanonicalPivotGrant())||!IsSwitchRestriction("상대는 「통상교대」 이외의 수단으로 교대할 수 없다."))throw new Exception("Fraction, scalar, priority or switch structure failed");if(!TryScalar("중확률로 자신의 기술의 위력을 강화(1.5배)한다",out target,out action,out rate)||!IsRank("저확률로 자신의 「공격」이 크게 오른다."))throw new Exception("Probability-wrapped effect structure failed");string ignoreTarget,ignoreStats,ignoreMode;if(!TryStatIgnore("『야생의 두목』의 능력 상승(강화)을 무시한다. (필요 「통솔：A」 이상)",out ignoreTarget,out ignoreStats,out ignoreMode)||ignoreTarget!="『야생의 두목』"||ignoreStats!="능력"||ignoreMode!="상승(강화)"||!IsStatIgnore("상대의 「방/특방」 강화를 무시한다.")||!TryStatIgnore("상대의 특방의 상승(강화)를 무시한다.",out ignoreTarget,out ignoreStats,out ignoreMode)||ignoreStats!="특방")throw new Exception("Stat ignore structure failed");var collapsed=Collapse(PotentialParts.Build(PotentialParts.StandardEntries()).effects);if(!collapsed.Any(x=>x.structureKind=="scalar")||!collapsed.Any(x=>x.structureKind=="fraction")||!collapsed.Any(x=>x.structureKind=="rank")||!collapsed.Any(x=>x.structureKind=="stat-ignore")||!collapsed.Any(x=>x.structureKind=="switch")||!collapsed.Any(x=>x.structureKind=="pivot"))throw new Exception("Effect structure collapse failed");FractionalHpRule.Test();PotentialStatRules.Test();SpeciesStatRanks.Test();}
 }
}
