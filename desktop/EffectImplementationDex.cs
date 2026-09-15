using System;
using System.Linq;
using System.Drawing;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Windows.Forms;

namespace AABattle {
 public class EffectImplementationEntry {public string status,family,effect,interpretation,implementation,sources;}
 public static class EffectImplementationDex {
  static readonly HashSet<string> DirectiveNames=new HashSet<string>(EditorTemplate.Orders(false).Concat(EditorTemplate.Orders(true)).Concat(EditorTemplate.ExtendedOrders(false)).Concat(EditorTemplate.ExtendedOrders(true)).Select(x=>x.name));
  public static List<EffectImplementationEntry> Entries(){return PotentialParts.Build(PotentialParts.StandardEntries()).effects.Where(x=>!x.excludedFromImplementation).Select(Analyze).ToList();}
  public static EffectImplementationEntry Analyze(PotentialPart part){
   string value=part.text??"",family=Family(value),status="미구현",implementation="포텐셜 원문은 배틀 데이터에 전달되지만 효과 실행기에는 연결되지 않았습니다. 현재는 배틀 로그와 상태 창을 보며 수동 판정합니다.";
   int directiveCount=part.names.Count(DirectiveNames.Contains);
   if(directiveCount>0){status=directiveCount==part.names.Count?"자동 구현":"일부 자동 구현";family="트레이너 4식·지령";implementation="이 효과가 트레이너 지령으로 사용될 때 Engine.Resolve가 선언 횟수와 행동 조건을 확인합니다. 4식은 회피·필중·버티기·후공 교대를 처리하고, Engine.ApplyOrder가 능력 랭크·1턴 배율·회복을 적용합니다. 같은 문장을 일반 포켓몬 포텐셜에서 사용하면 아직 자동 실행되지 않습니다.";}
   else if(PotentialProbability.Rate(value).HasValue){status="구현 규칙 준비";family="확률 발동";implementation=PotentialProbability.Help+" 포켓몬 포텐셜 실행기가 연결되면 이 판정기를 호출합니다.";}
   else if(value.Contains("확률로")){status="기준 질문 필요";family="확률 발동";implementation="확률 수치가 적혀 있지 않아 자동 판정할 수 없습니다.";}
   else if(family!="기타 원문"){status="구현 규칙 준비";implementation=Prepared(family);}
   string interpretation="문장 분류: "+family+". "+Interpret(value)+(part.protectedTemplate?" 기본 템플릿이므로 정리·치환하지 않습니다.":"");
   return new EffectImplementationEntry{status=status,family=family,effect=value,interpretation=interpretation,implementation=implementation,sources=string.Join(" / ",part.sources.Take(4))+(part.sources.Count>4?" 외 "+(part.sources.Count-4)+"곳":"")};
  }
  static string Family(string value){
   if(EffectStructures.IsRank(value)||Regex.IsMatch(value,@"(랭크|능력치).*(올린|오른|상승|내린|저하)|전능력치가 오른다"))return "능력 랭크 변화";
   if(Regex.IsMatch(value,@"(강화|약화|완화|반감|배가|[0-9.]+배)"))return "수치 배율 변화";
   if(Regex.IsMatch(value,@"(체력|HP).*(회복|감소)|회복한다"))return "체력 회복·소모";
   if(Regex.IsMatch(value,@"(상태이상|상태변화|화상|독|맹독|마비|잠듦|얼음|동상|혼란|풀죽음).*(한다|된다|해제|회복|무효)"))return "상태이상·상태변화";
   if(Regex.IsMatch(value,@"(날씨|쾌청|비|모래바람|싸라기눈|필드|트릭룸|중력|설치기|스텔스록|압정)"))return "장소·날씨·설치물";
   if(Regex.IsMatch(value,@"(교대|필드에 내보|장소에 내보|장소를 떠)"))return "교대·등장";
   if(Regex.IsMatch(value,@"(무효화|무효로|관통|회피|반사|버틴다|미룬다)"))return "방호·무효·관통";
   if(Regex.IsMatch(value,@"(우선도|먼저 행동|행동 순서|추가행동)"))return "행동 순서·우선도";
   if(Regex.IsMatch(value,@"(타입을|타입이|타입으로|타입 상성|효과가 굉장|효과가 별로)"))return "타입·상성 변경";
   if(Regex.IsMatch(value,@"(기술 「|기능확장|내보낼 수|기술을 변경|기술로 변경|기술을 잊)"))return "기술 생성·변경";
   if(Regex.IsMatch(value,@"(소지품|도구|리사이클|아이템)"))return "소지품";
   if(Regex.IsMatch(value,@"(데이터를 해석|상세를 확인|정보를|데이터 해석)"))return "정보·해석";
   if(Regex.IsMatch(value,@"(대미지|데미지|위력|급소|명중률|명중|회피율)"))return "공격 판정·대미지";
   return "기타 원문";
  }
  static string Prepared(string family){
   switch(family){
    case "능력 랭크 변화":return "대상, 능력 종류, 증감 랭크를 구조화한 뒤 Fighter.Stages, AccuracyStage, EvasionStage, CriticalStage에 연결할 예정입니다. 임의의 능력은 공격·방어·특공·특방·속도·명중·회피·C 중 사용자가 선택하고, 랜덤한 능력치는 공격·방어·특공·특방·속도 중 자동 선택합니다. 아직 포텐셜에서 자동 적용되지는 않습니다.";
    case "수치 배율 변화":return "대상 수치와 괄호 안 배율을 구조화한 뒤 Fighter.Multipliers 또는 대미지 계산 배율에 곱하도록 준비한 분류입니다. 적용 기간과 중첩 기준 확정이 필요합니다.";
    case "체력 회복·소모":return "최대 HP/N은 소수점을 버리고 최소 1로 계산합니다. 회복은 최대 HP를 넘지 않고, 대미지는 기본적으로 빈사가 가능하며 ‘빈사로 할 수 없다’가 지정된 경우만 HP 1에서 멈춥니다. 포텐셜 실행기에서 Mechanics.Heal/Hurt에 연결할 예정입니다.";
    case "상태이상·상태변화":return "상태 이름과 부여·해제 대상을 구조화해 Mechanics.ApplyStatus와 ConditionState에 연결할 예정입니다. 기존 타입·특성 면역 판정은 재사용합니다.";
    case "장소·날씨·설치물":return "Rules의 Weather, Terrain, TrickRoom, Gravity, Sides 상태에 연결할 예정입니다. 지속 턴과 덮어쓰기 규칙은 상태·날씨 도감의 기존 규칙을 사용합니다.";
    case "교대·등장":return "교대 요청과 등장 이벤트를 Engine.Resolve 및 EntryTriggers에 연결할 예정입니다. 강제 교대, 임의 교대, 죽어내밀기의 행동 순서 구분이 필요합니다.";
    case "방호·무효·관통":return "공격 판정 단계별 방호 태그와 관통 범위를 만든 뒤 Block/대미지 처리에 연결할 예정입니다. 트레이너 방호는 포켓몬 관통 대상에서 제외합니다.";
    case "행동 순서·우선도":return "기술 우선도 계산과 행동 순서 결정 전에 보정하도록 분류했습니다. 같은 시점 효과가 겹칠 때의 우선순위가 필요합니다.";
    case "타입·상성 변경":return "공격·방어 타입과 일시 타입을 분리해 Mechanics.TypeEffect에 전달할 예정입니다. 원래 타입의 유지 여부를 문장별로 확인해야 합니다.";
    case "기술 생성·변경":return "기술 데이터 조회 후 이번 행동의 Move를 복제·교체하도록 분류했습니다. 기능확장과 기술 습득은 별도 취급할 예정입니다.";
    case "소지품":return "현재 소지품, 파괴·소모 여부, 복원 대상을 별도 상태로 만든 뒤 처리할 예정입니다.";
    case "정보·해석":return "상대 데이터와 선행입력 공개 범위를 UI에 표시하는 효과입니다. 공개되는 항목 범위를 확정해야 합니다.";
    default:return "대미지 계산의 위력·명중·급소·최종 대미지 단계 중 어느 위치에 적용할지 구조화할 예정입니다. 문장별 계산 단계 확인이 필요합니다.";
   }
  }
  static string Interpret(string value){var rate=Regex.Match(value,@"([0-9]+(?:\.[0-9]+)?)배");var rank=Regex.Match(value,@"([0-9]+)랭크");var chance=PotentialProbability.Rate(value);return (rate.Success?"인식 배율 "+rate.Groups[1].Value+"배. ":"")+(rank.Success?"인식 랭크 "+rank.Groups[1].Value+". ":"")+(chance.HasValue?"인식 발동률 "+(int)(chance.Value*100)+"%. ":"")+"원문을 실행 규칙으로 바꾸기 전 검토 결과입니다.";}
  public static Control CreateView(){
   var entries=Entries();var host=new Panel{Dock=DockStyle.Fill,BackColor=Color.White};var status=Theme.Combo(new[]{"전체"}.Concat(entries.Select(x=>x.status).Distinct()),"전체");status.Dock=DockStyle.Top;var search=new PixelSearchBox{Dock=DockStyle.Top};var count=Theme.Label("",30);count.Dock=DockStyle.Top;
   var grid=new PixelDexGrid{Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,AllowUserToDeleteRows=false,AllowUserToResizeRows=false,RowHeadersVisible=false,MultiSelect=false,SelectionMode=DataGridViewSelectionMode.FullRowSelect,BackgroundColor=Color.White,BorderStyle=BorderStyle.None,EnableHeadersVisualStyles=false,ColumnHeadersHeight=34,DefaultCellStyle=new DataGridViewCellStyle{Font=new Font("맑은 고딕",9),SelectionBackColor=Color.Black,SelectionForeColor=Color.White},ColumnHeadersDefaultCellStyle=new DataGridViewCellStyle{BackColor=Color.Black,ForeColor=Color.White,Font=Theme.UI(8)}};
   grid.Columns.Add(new DataGridViewTextBoxColumn{HeaderText="구현 상태",Width=120});grid.Columns.Add(new DataGridViewTextBoxColumn{HeaderText="분류",Width=150});grid.Columns.Add(new DataGridViewTextBoxColumn{HeaderText="효과 원문",AutoSizeMode=DataGridViewAutoSizeColumnMode.Fill,MinimumWidth=480});var detail=new PixelText{Dock=DockStyle.Bottom,Height=210};
   Action show=()=>{var x=grid.CurrentRow==null?null:grid.CurrentRow.Tag as EffectImplementationEntry;detail.Text=x==null?"검색 결과 없음":"【"+x.status+"】 "+x.family+"\n\n원문: "+x.effect+"\n\n해석: "+x.interpretation+"\n\n실제 처리: "+x.implementation+"\n\n출처: "+x.sources;};
   Action refresh=()=>{string q=search.Text.Trim();var shown=entries.Where(x=>(status.Text=="전체"||x.status==status.Text)&&(x.effect+x.family+x.status+x.interpretation+x.implementation+x.sources).IndexOf(q,StringComparison.OrdinalIgnoreCase)>=0).ToList();grid.Rows.Clear();foreach(var x in shown){int i=grid.Rows.Add(x.status,x.family,x.effect);grid.Rows[i].Tag=x;}count.Text="구현 대상 "+entries.Count+"개 · 현재 표시 "+shown.Count+"개 · "+string.Join(" / ",entries.GroupBy(x=>x.status).Select(x=>x.Key+" "+x.Count()));if(grid.RowCount>0)grid.CurrentCell=grid.Rows[0].Cells[0];show();};
   grid.SelectionChanged+=(s,e)=>show();status.SelectedIndexChanged+=(s,e)=>refresh();search.TextChanged+=(s,e)=>refresh();host.Controls.Add(new PixelGridHost(grid));host.Controls.Add(detail);host.Controls.Add(search);host.Controls.Add(status);host.Controls.Add(count);host.Controls[0].BringToFront();refresh();return host;
  }
  public static void Test(){PotentialProbability.Test();var all=Entries();int expected=PotentialParts.Build(PotentialParts.StandardEntries()).effects.Count(x=>!x.excludedFromImplementation);if(all.Count!=expected)throw new Exception("Implementation dex effect count mismatch");if(!all.Any(x=>x.status=="자동 구현"&&x.family=="트레이너 4식·지령")||!all.Any(x=>x.family=="확률 발동"&&x.status=="구현 규칙 준비")||!all.Any(x=>x.status=="구현 규칙 준비")||!all.Any(x=>x.status=="미구현"))throw new Exception("Implementation dex statuses missing");}
 }
 partial class DataEditorForm {void BuildEffectImplementationTab(){var page=Page("효과 구현 방식");page.Controls.Add(EffectImplementationDex.CreateView());}}
}
