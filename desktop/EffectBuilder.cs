using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace AABattle {
 public static class EffectNormalizer {
  static readonly Regex Scalar=new Regex(@"(?<target>(?:자신|상대|아군)[^,.\n]*?)\s*(?:을|를|이|가)\s*(?<action>강화|약화|저하)\s*\(\s*(?<rate>[0-9]+(?:\.[0-9]+)?)\s*배\s*\)\s*(?:한다|시킨다|된다)",RegexOptions.Compiled);
  const string RankTarget=@"(?<target>.+?(?:전능력치|능력치|능력|「(?:공격|방어|특공|특방|속도|속|명중|회피|C|공/특공|방/특방|공격/특공|방어/특방)」|공격|방어|특공|특방|속도|명중|회피))";
  static readonly Regex RankUp=new Regex("^"+RankTarget+@"(?:을|를|이|가)\s*(?:(?<rank>[123])\s*랭크\s*)?(?<degree>매우 크게|크게)?\s*(?:올린다|오른다|상승시킨다|상승한다)$",RegexOptions.Compiled);
  static readonly Regex RankDown=new Regex("^"+RankTarget+@"(?:을|를|이|가)\s*(?:(?<rank>[123])\s*랭크\s*)?(?<degree>매우 크게|크게)?\s*(?:내린다|떨어진다|저하한다|하락한다|저하시킨다)$",RegexOptions.Compiled);
  public static string Normalize(string text){
   string clean=PotentialParts.Clean(text);
   clean=Regex.Replace(clean,@"임의의\s*능력치","임의의 능력");
   clean=Regex.Replace(clean,@"랜덤한\s*능력(?!치)","랜덤한 능력치");
   clean=Regex.Replace(clean,@"제일\s*(높은|낮은)\s*능력치?",m=>"가장 "+m.Groups[1].Value+" 능력");
   clean=Regex.Replace(clean,@"가장\s*(높은|낮은)\s*능력치",m=>"가장 "+m.Groups[1].Value+" 능력");
   var rank=RankUp.Match(clean);if(!rank.Success)rank=RankDown.Match(clean);if(rank.Success&&!rank.Groups["target"].Value.Contains("까지")){int amount=rank.Groups["rank"].Success?int.Parse(rank.Groups["rank"].Value):rank.Groups["degree"].Value=="매우 크게"?3:rank.Groups["degree"].Value=="크게"?2:1;return CanonicalRank(rank.Groups["target"].Value,RankDown.IsMatch(clean)?-amount:amount);}return Scalar.Replace(clean,m=>Canonical(m.Groups["target"].Value,m.Groups["action"].Value,double.Parse(m.Groups["rate"].Value,System.Globalization.CultureInfo.InvariantCulture)));
  }
  public static string NormalizeLines(string text){return string.Join("\r\n",(text??"").Replace("\r","").Split('\n').Select(Normalize).Where(x=>x.Length>0));}
  public static string Canonical(string target,string action,double rate){
   target=PotentialParts.Clean(target);string verb=action=="강화"?"강화":"약화";
   return target+Particle(target)+" "+verb+"("+rate.ToString("0.##",System.Globalization.CultureInfo.InvariantCulture)+"배)한다.";
  }
  public static string CanonicalRank(string target,int amount){target=PotentialParts.Clean(target);bool down=amount<0;amount=Math.Max(1,Math.Min(3,Math.Abs(amount)));return target+(down?Particle(target):SubjectParticle(target))+" "+(amount==3?"매우 크게 ":amount==2?"크게 ":"")+(down?"내린다.":"오른다.");}
  static string Particle(string value){if(string.IsNullOrEmpty(value))return "을";for(int i=value.Length-1;i>=0;i--){char c=value[i];if(c>='가'&&c<='힣')return ((c-'가')%28)==0?"를":"을";}return "을";}
  static string SubjectParticle(string value){if(string.IsNullOrEmpty(value))return "이";for(int i=value.Length-1;i>=0;i--){char c=value[i];if(c>='가'&&c<='힣')return ((c-'가')%28)==0?"가":"이";}return "이";}
  public static void Test(){
   if(Normalize("자신의 기술의 위력이 강화(1.50배)된다.")!="자신의 기술의 위력을 강화(1.5배)한다.")throw new Exception("Effect normalization failed");
   string weak=Normalize("상대의 「방어」를 저하(0.8배)시킨다.");if(weak!="상대의 「방어」를 약화(0.8배)한다.")throw new Exception("Effect weakening normalization failed: "+weak);
   if(Normalize("자신의 「공격」을 1랭크 올린다.").Contains("강화"))throw new Exception("Rank effect must stay distinct");
   if(Normalize("자신의 「공격」을 상승시킨다")!="자신의 「공격」이 오른다."||Normalize("자신의 「공격」을 크게 올린다")!="자신의 「공격」이 크게 오른다."||Normalize("자신의 「공격」을 3랭크 올린다")!="자신의 「공격」이 매우 크게 오른다.")throw new Exception("Rank wording normalization failed");
   if(Normalize("상대의 「방어」가 떨어진다")!="상대의 「방어」를 내린다."||Normalize("상대의 「방어」를 크게 내린다")!="상대의 「방어」를 크게 내린다."||Normalize("상대의 「방어」를 3랭크 내린다")!="상대의 「방어」를 매우 크게 내린다.")throw new Exception("Rank decrease normalization failed");
   if(Normalize("자신의 임의의 능력치를 올린다")!="자신의 임의의 능력이 오른다."||!Normalize("자신의 랜덤한 능력을 올린다").Contains("랜덤한 능력치"))throw new Exception("Stat choice wording normalization failed");
   if(Normalize("상대의 제일 높은 능력치를 내린다")!="상대의 가장 높은 능력을 내린다."||Normalize("자신의 가장 낮은 능력치를 올린다")!="자신의 가장 낮은 능력이 오른다.")throw new Exception("Extreme stat wording normalization failed");
  }
 }

 public static class PotentialStatRules {
  public static readonly string[] Arbitrary={"공격","방어","특공","특방","속도","명중","회피","C"};
  public static readonly string[] Random={"공격","방어","특공","특방","속도"};
  public static string ChooseRandom(Func<double> random){if(random==null)throw new ArgumentNullException("random");double roll=Math.Max(0,Math.Min(.999999999,random()));return Random[(int)(roll*Random.Length)];}
  public static int ExtremeIndex(int[] values,bool highest,Func<double> random){if(values==null||values.Length!=5)throw new ArgumentException("Five battle stat values are required","values");if(random==null)throw new ArgumentNullException("random");int extreme=highest?values.Max():values.Min();var tied=Enumerable.Range(0,5).Where(i=>values[i]==extreme).ToArray();double roll=Math.Max(0,Math.Min(.999999999,random()));return tied[(int)(roll*tied.Length)];}
  public static string ChooseExtreme(Fighter fighter,Fighter opponent,Rules rules,bool highest,Func<double> random){var values=Enumerable.Range(1,5).Select(i=>Engine.Effective(fighter,opponent,i,rules)).ToArray();return Random[ExtremeIndex(values,highest,random)];}
  public static void Test(){if(Arbitrary.Length!=8||Arbitrary.Last()!="C"||Random.Length!=5||Random.Contains("명중")||ChooseRandom(()=>0)!="공격"||ChooseRandom(()=>.999)!="속도"||ExtremeIndex(new[]{100,200,200,50,70},true,()=>0)!=1||ExtremeIndex(new[]{100,200,200,50,50},true,()=>.999)!=2||ExtremeIndex(new[]{100,200,200,50,50},false,()=>.999)!=4)throw new Exception("Potential stat selection rules failed");}
 }

 public static class FractionalHpRule {
  public static int Amount(int maxHp,int denominator){if(denominator<1)throw new ArgumentOutOfRangeException("denominator");return Math.Max(1,maxHp/denominator);}
  public static int DamageHp(int currentHp,int maxHp,int denominator,bool canFaint){int left=currentHp-Amount(maxHp,denominator);return canFaint?Math.Max(0,left):Math.Max(1,left);}
  public static int HealHp(int currentHp,int maxHp,int denominator){return Math.Min(maxHp,currentHp+Amount(maxHp,denominator));}
  public static void Test(){if(Amount(101,4)!=25||DamageHp(10,100,4,true)!=0||DamageHp(10,100,4,false)!=1||HealHp(90,101,4)!=101)throw new Exception("Fractional HP rule failed");}
 }
 public static class EffectStructures {
  static readonly Regex Fraction=new Regex(@"^(?<target>자신|상대|아군 1체|아군 전체|아군)(?:의|에게)?\s*(?:(?:최대|전체)\s*)?체력(?:의|을|를|이|가)?\s*1/(?<n>[0-9]+)(?:만큼|을|를|의)?\s*(?<action>회복한다|회복시킨다|감소시킨다|감소한다|대미지를 준다|데미지를 준다)$",RegexOptions.Compiled);
  public static bool IsScalar(string text){string normalized=EffectNormalizer.Normalize(text);return Regex.IsMatch(PotentialParts.Clean(normalized),@"^(?:자신|상대|아군)[^,]*?\s*(?:을|를)\s*(?:강화|약화)\([0-9]+(?:\.[0-9]+)?배\)한다$");}
  public static bool IsRank(string text){return Regex.IsMatch(PotentialParts.Clean(EffectNormalizer.Normalize(text)),@"^.+?(?:(?:이|가)\s*(?:매우 크게 |크게 )?오른다|(?:을|를)\s*(?:매우 크게 |크게 )?내린다)$");}
  public static bool TryFraction(string text,out string target,out string action,out int denominator){var m=Fraction.Match(PotentialParts.Clean(text));target=action="";denominator=0;if(!m.Success)return false;target=m.Groups["target"].Value;action=m.Groups["action"].Value.Contains("회복")?"회복":"대미지";return int.TryParse(m.Groups["n"].Value,out denominator)&&denominator>0;}
  public static List<PotentialPart> Collapse(IEnumerable<PotentialPart> source){var input=source.Where(x=>!x.excludedFromImplementation).ToList();var result=new List<PotentialPart>();var scalar=input.Where(x=>!x.protectedTemplate&&IsScalar(x.text)).ToList();var fraction=input.Where(x=>!x.protectedTemplate&&TryFractionOnly(x.text)).ToList();var rank=input.Where(x=>!x.protectedTemplate&&IsRank(x.text)).ToList();var grouped=new HashSet<PotentialPart>(scalar.Concat(fraction).Concat(rank));result.AddRange(input.Where(x=>!grouped.Contains(x)));if(scalar.Count>0)result.Add(Template("scalar","수치 배율 — 대상 / 강화·약화 / 배율을 선택",scalar));if(fraction.Count>0)result.Add(Template("fraction","최대 HP의 1/N — 대상 / 회복·대미지 / 빈사 여부를 선택",fraction));if(rank.Count>0)result.Add(Template("rank","능력 랭크 변화 — 대상 / 상승·하락 / 1·2·3랭크를 선택",rank));return result.OrderBy(x=>x.text).ToList();}
  static bool TryFractionOnly(string text){string target,action;int n;return TryFraction(text,out target,out action,out n);}
  static PotentialPart Template(string kind,string text,List<PotentialPart> variants){var p=new PotentialPart{id="structure-"+kind,key="structure-"+kind,text=text,structureKind=kind,variantCount=variants.Count,searchText=string.Join(" ",variants.Select(x=>x.text))};foreach(var x in variants){foreach(var n in x.names)if(!p.names.Contains(n))p.names.Add(n);foreach(var s in x.sources)if(!p.sources.Contains(s))p.sources.Add(s);foreach(var r in x.raws)if(!p.raws.Contains(r))p.raws.Add(r);}return p;}
  public static void Test(){string target,action;int n;if(!TryFraction("자신의 체력을 1/4 회복한다",out target,out action,out n)||target!="자신"||action!="회복"||n!=4)throw new Exception("Fraction structure failed");var collapsed=Collapse(PotentialParts.Build(PotentialParts.StandardEntries()).effects);if(!collapsed.Any(x=>x.structureKind=="scalar")||!collapsed.Any(x=>x.structureKind=="fraction")||!collapsed.Any(x=>x.structureKind=="rank"))throw new Exception("Effect structure collapse failed");FractionalHpRule.Test();PotentialStatRules.Test();}
 }

 partial class DataEditorForm {
  ComboBox effectKind,effectAction,effectTarget;NumericUpDown effectRate,effectDenominator;CheckBox effectCanFaint;PixelText effectPreview;
  Control BuildInlineEffectBuilder(){
   var panel=new Panel{Dock=DockStyle.Bottom,Height=278,Padding=new Padding(2)};var table=Theme.Table();table.Dock=DockStyle.Fill;panel.Controls.Add(table);
   effectKind=Theme.Combo(new[]{"선택 원문 그대로","수치 배율","최대 HP의 1/N","능력 랭크 변화"},"선택 원문 그대로");effectKind.DrawMode=DrawMode.Normal;
   effectTarget=Theme.Combo(new[]{"자신","상대","아군 1체","아군 전체","자신의 「공격」","자신의 「방어」","자신의 「특공」","자신의 「특방」","자신의 「속도」","자신의 「명중」","자신의 「회피」","자신의 「C」","자신의 임의의 능력","자신의 랜덤한 능력치","자신의 가장 높은 능력","자신의 가장 낮은 능력","자신의 전능력치","상대의 「공격」","상대의 「방어」","상대의 「특공」","상대의 「특방」","상대의 「속도」","상대의 「명중」","상대의 「회피」","상대의 「C」","상대의 임의의 능력","상대의 랜덤한 능력치","상대의 가장 높은 능력","상대의 가장 낮은 능력","자신의 기술의 위력","자신이 주는 대미지","자신이 받는 대미지","상대의 기술의 위력","상대가 주는 대미지","상대가 받는 대미지"},"자신의 기술의 위력");effectTarget.DropDownStyle=ComboBoxStyle.DropDown;
   effectAction=Theme.Combo(new[]{"강화","약화","회복","대미지","상승","하락"},"강화");effectAction.DrawMode=DrawMode.Normal;effectRate=new NumericUpDown{DecimalPlaces=2,Minimum=.01M,Maximum=10M,Increment=.05M,Value=1.5M,Dock=DockStyle.Fill,Font=new Font("맑은 고딕",10),BorderStyle=BorderStyle.FixedSingle};effectDenominator=new NumericUpDown{Minimum=1,Maximum=999,Value=4,Dock=DockStyle.Fill,Font=new Font("맑은 고딕",10),BorderStyle=BorderStyle.FixedSingle};effectCanFaint=new CheckBox{Text="이 대미지로 빈사 가능",Checked=true,Dock=DockStyle.Fill,Font=Theme.UI(8)};effectPreview=new PixelText{Dock=DockStyle.Fill};
   Theme.Row(table,"구조",effectKind);Theme.Row(table,"대상",effectTarget);Theme.Row(table,"종류",effectAction);Theme.Row(table,"배율 / N·랭크",Pair(effectRate,effectDenominator));Theme.Row(table,"빈사",effectCanFaint);Theme.Row(table,"입력될 효과",effectPreview);
   EventHandler refresh=(s,e)=>RefreshStructuredEffect();effectKind.SelectedIndexChanged+=refresh;effectTarget.TextChanged+=refresh;effectAction.SelectedIndexChanged+=refresh;effectRate.ValueChanged+=refresh;effectDenominator.ValueChanged+=refresh;effectCanFaint.CheckedChanged+=refresh;RefreshStructuredEffect();return panel;
  }
  Control Pair(Control a,Control b){var row=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,Margin=Padding.Empty};row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));row.Controls.Add(a,0,0);row.Controls.Add(b,1,0);return row;}
  void SelectEffectStructure(PotentialPart part){if(effectKind==null)return;if(part==null||string.IsNullOrEmpty(part.structureKind))effectKind.SelectedIndex=0;else if(part.structureKind=="scalar"){effectKind.SelectedIndex=1;effectTarget.Text="자신의 기술의 위력";effectAction.SelectedIndex=0;}else if(part.structureKind=="fraction"){effectKind.SelectedIndex=2;effectTarget.Text="자신";effectAction.SelectedIndex=2;}else{effectKind.SelectedIndex=3;effectTarget.Text="자신의 「공격」";effectAction.SelectedIndex=4;effectDenominator.Value=1;}RefreshStructuredEffect();}
  void RefreshStructuredEffect(){if(effectPreview==null)return;bool scalar=effectKind.Text=="수치 배율",fraction=effectKind.Text=="최대 HP의 1/N",rank=effectKind.Text=="능력 랭크 변화";effectRate.Enabled=scalar;effectDenominator.Enabled=fraction||rank;effectCanFaint.Enabled=fraction&&effectAction.Text=="대미지";if(scalar){effectPreview.Text=EffectNormalizer.Canonical(effectTarget.Text,effectAction.Text=="약화"?"약화":"강화",(double)effectRate.Value);}else if(fraction){string target=new[]{"자신","상대","아군 1체","아군 전체"}.Contains(effectTarget.Text)?effectTarget.Text:"자신";int n=(int)effectDenominator.Value;effectPreview.Text=effectAction.Text=="대미지"?target+"에게 최대 HP의 1/"+n+" 대미지를 준다."+(effectCanFaint.Checked?"":" 이 대미지로는 빈사로 할 수 없다."):target+"의 체력을 최대 HP의 1/"+n+" 회복한다.";}else if(rank){if(effectDenominator.Value>3)effectDenominator.Value=3;effectPreview.Text=EffectNormalizer.CanonicalRank(effectTarget.Text,(effectAction.Text=="하락"?-1:1)*(int)effectDenominator.Value);}else effectPreview.Text="선택한 원문을 그대로 입력합니다.";}
  string ConfiguredEffect(PotentialPart part){return part==null?"":string.IsNullOrEmpty(part.structureKind)?part.text:effectPreview.Text;}
 }
}
