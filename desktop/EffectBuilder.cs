using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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

 partial class DataEditorForm {
  ComboBox effectAction,effectTarget;NumericUpDown effectRate;PixelText effectPreview;
  void BuildEffectBuilderTab(){
   var page=Page("효과 작성");var panel=new Panel{Dock=DockStyle.Fill,Padding=new Padding(24),AutoScroll=true};page.Controls.Add(panel);
   var table=Theme.Table();table.Dock=DockStyle.Top;table.Height=390;panel.Controls.Add(table);
   effectTarget=Theme.Combo(new[]{"자신의 「공격」","자신의 「방어」","자신의 「특공」","자신의 「특방」","자신의 「속도」","자신의 「명중」","자신의 「회피」","자신의 전능력치","자신의 기술의 위력","자신이 주는 대미지","자신이 받는 대미지","상대의 기술의 위력","상대가 주는 대미지","상대가 받는 대미지"},"자신의 기술의 위력");effectTarget.DropDownStyle=ComboBoxStyle.DropDown;
   effectAction=Theme.Combo(new[]{"강화","약화"},"강화");effectRate=new NumericUpDown{DecimalPlaces=2,Minimum=.01M,Maximum=10M,Increment=.05M,Value=1.5M,Dock=DockStyle.Fill,Font=new Font("맑은 고딕",10),BorderStyle=BorderStyle.FixedSingle};
   effectPreview=new PixelText{Height=90,Dock=DockStyle.Fill};var apply=new FlatButton{Text="선택한 포텐셜 행에 효과 넣기",Height=46,Dock=DockStyle.Fill,Primary=true};
   Theme.Row(table,"대상",effectTarget);Theme.Row(table,"종류",effectAction);Theme.Row(table,"배율",effectRate);Theme.Row(table,"표준 효과",effectPreview);Theme.Row(table,"적용",apply);
   var help=new PixelText{Dock=DockStyle.Top,Height=180,Text="배율 효과 읽는 법\n\n강화(1.5배)는 원래 값의 150%, 약화(0.8배)는 80%로 계산합니다.\n같은 대상의 배율 효과는 각각 곱합니다. 예: 1.5배 × 0.8배 = 1.2배.\n능력치가 ‘1랭크 오른다’는 랭크 변화이며, 이 배율 강화와 별개의 효과입니다.\n대상과 배율이 다른 효과는 합치지 않습니다."};panel.Controls.Add(help);help.BringToFront();
   Action refresh=()=>effectPreview.Text=EffectNormalizer.Canonical(effectTarget.Text,effectAction.Text,(double)effectRate.Value);
   effectTarget.TextChanged+=(s,e)=>refresh();effectAction.SelectedIndexChanged+=(s,e)=>{if(effectAction.Text=="약화"&&effectRate.Value==1.5M)effectRate.Value=.8M;else if(effectAction.Text=="강화"&&effectRate.Value==.8M)effectRate.Value=1.5M;refresh();};effectRate.ValueChanged+=(s,e)=>refresh();
   apply.Click+=(s,e)=>{var row=SelectedPotentialRow();if(row==null){MessageBox.Show(this,"포텐셜 탭에서 적용할 행을 먼저 선택하세요.");return;}row.Cells["effect"].Value=effectPreview.Text;GridChanged();};refresh();
  }
 }
}
