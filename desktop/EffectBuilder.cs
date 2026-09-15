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
  public static string Normalize(string text){
   string clean=PotentialParts.Clean(text);
   return Scalar.Replace(clean,m=>Canonical(m.Groups["target"].Value,m.Groups["action"].Value,double.Parse(m.Groups["rate"].Value,System.Globalization.CultureInfo.InvariantCulture)));
  }
  public static string NormalizeLines(string text){return string.Join("\r\n",(text??"").Replace("\r","").Split('\n').Select(Normalize).Where(x=>x.Length>0));}
  public static string Canonical(string target,string action,double rate){
   target=PotentialParts.Clean(target);string verb=action=="강화"?"강화":"약화";
   return target+Particle(target)+" "+verb+"("+rate.ToString("0.##",System.Globalization.CultureInfo.InvariantCulture)+"배)한다.";
  }
  static string Particle(string value){if(string.IsNullOrEmpty(value))return "을";for(int i=value.Length-1;i>=0;i--){char c=value[i];if(c>='가'&&c<='힣')return ((c-'가')%28)==0?"를":"을";}return "을";}
  public static void Test(){
   if(Normalize("자신의 기술의 위력이 강화(1.50배)된다.")!="자신의 기술의 위력을 강화(1.5배)한다.")throw new Exception("Effect normalization failed");
   string weak=Normalize("상대의 「방어」를 저하(0.8배)시킨다.");if(weak!="상대의 「방어」를 약화(0.8배)한다.")throw new Exception("Effect weakening normalization failed: "+weak);
   if(Normalize("자신의 「공격」을 1랭크 올린다.").Contains("강화"))throw new Exception("Rank effect must stay distinct");
  }
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
  public static bool TryFraction(string text,out string target,out string action,out int denominator){var m=Fraction.Match(PotentialParts.Clean(text));target=action="";denominator=0;if(!m.Success)return false;target=m.Groups["target"].Value;action=m.Groups["action"].Value.Contains("회복")?"회복":"대미지";return int.TryParse(m.Groups["n"].Value,out denominator)&&denominator>0;}
  public static List<PotentialPart> Collapse(IEnumerable<PotentialPart> source){var input=source.Where(x=>!x.excludedFromImplementation).ToList();var result=new List<PotentialPart>();var scalar=input.Where(x=>!x.protectedTemplate&&IsScalar(x.text)).ToList();var fraction=input.Where(x=>!x.protectedTemplate&&TryFractionOnly(x.text)).ToList();var grouped=new HashSet<PotentialPart>(scalar.Concat(fraction));result.AddRange(input.Where(x=>!grouped.Contains(x)));if(scalar.Count>0)result.Add(Template("scalar","수치 배율 — 대상 / 강화·약화 / 배율을 선택",scalar));if(fraction.Count>0)result.Add(Template("fraction","최대 HP의 1/N — 대상 / 회복·대미지 / 빈사 여부를 선택",fraction));return result.OrderBy(x=>x.text).ToList();}
  static bool TryFractionOnly(string text){string target,action;int n;return TryFraction(text,out target,out action,out n);}
  static PotentialPart Template(string kind,string text,List<PotentialPart> variants){var p=new PotentialPart{id="structure-"+kind,key="structure-"+kind,text=text,structureKind=kind,variantCount=variants.Count,searchText=string.Join(" ",variants.Select(x=>x.text))};foreach(var x in variants){foreach(var n in x.names)if(!p.names.Contains(n))p.names.Add(n);foreach(var s in x.sources)if(!p.sources.Contains(s))p.sources.Add(s);foreach(var r in x.raws)if(!p.raws.Contains(r))p.raws.Add(r);}return p;}
  public static void Test(){string target,action;int n;if(!TryFraction("자신의 체력을 1/4 회복한다",out target,out action,out n)||target!="자신"||action!="회복"||n!=4)throw new Exception("Fraction structure failed");var collapsed=Collapse(PotentialParts.Build(PotentialParts.StandardEntries()).effects);if(collapsed.Count>=3124||!collapsed.Any(x=>x.structureKind=="scalar")||!collapsed.Any(x=>x.structureKind=="fraction"))throw new Exception("Effect structure collapse failed");FractionalHpRule.Test();}
 }

 partial class DataEditorForm {
  ComboBox effectKind,effectAction,effectTarget;NumericUpDown effectRate,effectDenominator;CheckBox effectCanFaint;PixelText effectPreview;
  Control BuildInlineEffectBuilder(){
   var panel=new Panel{Dock=DockStyle.Bottom,Height=278,Padding=new Padding(2)};var table=Theme.Table();table.Dock=DockStyle.Fill;panel.Controls.Add(table);
   effectKind=Theme.Combo(new[]{"선택 원문 그대로","수치 배율","최대 HP의 1/N"},"선택 원문 그대로");
   effectTarget=Theme.Combo(new[]{"자신","상대","아군 1체","아군 전체","자신의 「공격」","자신의 「방어」","자신의 「특공」","자신의 「특방」","자신의 「속도」","자신의 「명중」","자신의 「회피」","자신의 전능력치","자신의 기술의 위력","자신이 주는 대미지","자신이 받는 대미지","상대의 기술의 위력","상대가 주는 대미지","상대가 받는 대미지"},"자신의 기술의 위력");effectTarget.DropDownStyle=ComboBoxStyle.DropDown;
   effectAction=Theme.Combo(new[]{"강화","약화","회복","대미지"},"강화");effectRate=new NumericUpDown{DecimalPlaces=2,Minimum=.01M,Maximum=10M,Increment=.05M,Value=1.5M,Dock=DockStyle.Fill,Font=new Font("맑은 고딕",10),BorderStyle=BorderStyle.FixedSingle};effectDenominator=new NumericUpDown{Minimum=1,Maximum=999,Value=4,Dock=DockStyle.Fill,Font=new Font("맑은 고딕",10),BorderStyle=BorderStyle.FixedSingle};effectCanFaint=new CheckBox{Text="이 대미지로 빈사 가능",Checked=true,Dock=DockStyle.Fill,Font=Theme.UI(8)};effectPreview=new PixelText{Dock=DockStyle.Fill};
   Theme.Row(table,"구조",effectKind);Theme.Row(table,"대상",effectTarget);Theme.Row(table,"종류",effectAction);Theme.Row(table,"배율 / 분모 N",Pair(effectRate,effectDenominator));Theme.Row(table,"빈사",effectCanFaint);Theme.Row(table,"입력될 효과",effectPreview);
   EventHandler refresh=(s,e)=>RefreshStructuredEffect();effectKind.SelectedIndexChanged+=refresh;effectTarget.TextChanged+=refresh;effectAction.SelectedIndexChanged+=refresh;effectRate.ValueChanged+=refresh;effectDenominator.ValueChanged+=refresh;effectCanFaint.CheckedChanged+=refresh;RefreshStructuredEffect();return panel;
  }
  Control Pair(Control a,Control b){var row=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,Margin=Padding.Empty};row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));row.Controls.Add(a,0,0);row.Controls.Add(b,1,0);return row;}
  void SelectEffectStructure(PotentialPart part){if(effectKind==null)return;if(part==null||string.IsNullOrEmpty(part.structureKind))effectKind.SelectedItem="선택 원문 그대로";else if(part.structureKind=="scalar"){effectKind.SelectedItem="수치 배율";effectTarget.Text="자신의 기술의 위력";effectAction.SelectedItem="강화";}else{effectKind.SelectedItem="최대 HP의 1/N";effectTarget.Text="자신";effectAction.SelectedItem="회복";}RefreshStructuredEffect();}
  void RefreshStructuredEffect(){if(effectPreview==null)return;bool scalar=effectKind.Text=="수치 배율",fraction=effectKind.Text=="최대 HP의 1/N";effectRate.Enabled=scalar;effectDenominator.Enabled=fraction;effectCanFaint.Enabled=fraction&&effectAction.Text=="대미지";if(scalar){effectPreview.Text=EffectNormalizer.Canonical(effectTarget.Text,effectAction.Text=="약화"?"약화":"강화",(double)effectRate.Value);}else if(fraction){string target=new[]{"자신","상대","아군 1체","아군 전체"}.Contains(effectTarget.Text)?effectTarget.Text:"자신";int n=(int)effectDenominator.Value;effectPreview.Text=effectAction.Text=="대미지"?target+"에게 최대 HP의 1/"+n+" 대미지를 준다."+(effectCanFaint.Checked?"":" 이 대미지로는 빈사로 할 수 없다."):target+"의 체력을 최대 HP의 1/"+n+" 회복한다.";}else effectPreview.Text="선택한 원문을 그대로 입력합니다.";}
  string ConfiguredEffect(PotentialPart part){return part==null?"":string.IsNullOrEmpty(part.structureKind)?part.text:effectPreview.Text;}
 }
}
