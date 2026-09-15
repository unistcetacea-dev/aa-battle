using System;
using System.Linq;
using System.Drawing;
using System.Windows.Forms;
using System.Collections.Generic;
namespace AABattle {
 public class StateDexEntry {public string category,name,effect;}
 public static class StateDex {
  public static List<StateDexEntry> Entries(){
   var result=new List<StateDexEntry>();
   Add(result,"포텐셜 수치 효과","강화(배율)","지정한 값을 괄호 안의 배율만큼 곱합니다. 강화(1.5배)는 원래 값의 150%입니다. 대상과 배율이 같을 때만 같은 효과로 정리합니다.");
   Add(result,"포텐셜 수치 효과","약화(배율)","지정한 값을 괄호 안의 배율만큼 곱합니다. 약화(0.8배)는 원래 값의 80%입니다. ‘저하(0.8배)’처럼 같은 뜻인 표기는 약화(0.8배)로 통일합니다.");
   Add(result,"포텐셜 수치 효과","배율의 중첩","여러 배율 효과가 함께 걸리면 곱해서 계산합니다. 예: 강화(1.5배)와 약화(0.8배)는 1.5×0.8=1.2배입니다. 적용 순서는 곱셈 결과에 영향을 주지 않습니다.");
   Add(result,"포텐셜 수치 효과","능력 랭크와 배율","‘공격이 오른다’ 또는 ‘1랭크 올린다’는 능력 랭크 변화입니다. ‘공격을 강화(1.5배)한다’는 최종 수치에 곱하는 배율 효과이므로 서로 다른 효과입니다.");
   Add(result,"포텐셜 수치 효과","능력치 / 기술 위력 / 대미지","공격·방어·특공·특방·속도 강화는 해당 능력치를 바꿉니다. 기술 위력 강화는 기술의 위력값을, 주는·받는 대미지 강화나 약화는 대미지 계산 결과를 바꿉니다. 문장에 적힌 대상만 적용합니다.");
   Add(result,"포텐셜 수치 효과","명중 / 회피","명중 강화는 자신의 기술이 맞을 확률을 높이고, 회피 강화는 상대 기술이 자신에게 맞을 확률을 낮춥니다. ‘공격을 회피한다’처럼 판정 자체를 성공시키는 효과와는 다릅니다.");
   Add(result,"포텐셜 수치 효과","임의의 능력 / 랜덤한 능력치","‘임의의 능력’은 효과가 발동할 때 공격·방어·특공·특방·속도·명중·회피·C 중 하나를 사용자가 선택합니다. ‘랜덤한 능력치’는 공격·방어·특공·특방·속도 중 하나를 같은 확률로 자동 선택합니다. C는 급소 랭크이며 배틀 상태 창에 다른 랭크와 함께 표시합니다.");
   Add(result,"포텐셜 수치 효과","가장 높은 / 낮은 능력","공격·방어·특공·특방·속도의 현재 실능력값을 비교합니다. 현재 랭크와 직접 강화·약화 배율 및 적용 중인 상태·장소 보정을 반영한 값을 사용하며, 같은 값이 여러 개면 해당 후보 중 하나를 같은 확률로 자동 선택합니다. 종족치를 지정한 문장은 이 규칙과 별개입니다.");
   Add(result,"포텐셜 수치 효과","가장 높은 / 낮은 종족치","공격·방어·특공·특방·속도의 원래 종족값을 비교합니다. 랭크·강화·약화·상태·장소 보정은 반영하지 않으며, 같은 종족값이 여러 개면 해당 후보 중 하나를 같은 확률로 자동 선택합니다.");
   Add(result,"포텐셜 수치 효과","종족치 1·2랭크 상승","현재 종족치 등급의 -, 무표기, +를 유지하고 지정한 수만큼 문자 등급을 올립니다. 예: B-→A-, B-→AA-(2랭크), B+→A+, B+→AA+(2랭크), A+→AA+. 새 등급의 허용 범위 안에서 적용할 실제 종족치를 설정합니다. S는 200 이상 범위로 처리합니다.");
   Add(result,"포텐셜 수치 효과","지정 종족치 등급","‘A까지 올린다’는 현재 종족치가 설정값보다 낮을 때만 올리고 이미 높으면 유지합니다. ‘A까지 내린다’는 현재 종족치가 설정값보다 높을 때만 내립니다. ‘A로 한다’는 현재 값과 관계없이 A 범위에서 선택한 값으로 변경합니다.");
   Add(result,"포텐셜 수치 효과","구조화된 수치 입력","포텐셜 도감에서 효과를 고르는 자리에서 대상, 강화·약화, 배율을 직접 정합니다. 1.33을 입력하면 1.33배, 0.67을 입력하면 0.67배로 저장됩니다. 능력 랭크는 오른다/내린다(±1), 크게 오른다/내린다(±2), 매우 크게 오른다/내린다(±3) 중에서 선택합니다.");
   Add(result,"포텐셜 수치 효과","방호 포텐셜 관통","기술 무효화·반사, 방어/특방의 직접 강화, 위력·대미지의 저하·완화·반감은 방호 포텐셜에 포함됩니다. 포켓몬의 관통 포텐셜은 트레이너 포텐셜과 『버텨라！』를 관통하지 않습니다. 회피, 대미지 미루기, 반격 대미지, 『기합』은 관통 대상이 아닙니다.");
   Add(result,"포텐셜 확률","발동 확률",PotentialProbability.Help);
   Add(result,"포텐셜 체력 효과","최대 HP의 1/N","최대 HP를 N으로 나눈 뒤 소수점을 버리고 최소 1로 처리합니다. 회복은 최대 HP를 넘지 않습니다. 대미지는 기본적으로 빈사가 가능하며, ‘이 대미지로는 빈사로 할 수 없다’가 있는 효과만 HP 1에서 멈춥니다.");
   Add(result,"상태이상",RuleBook.StatusHelp,new[]{"화상","독","맹독","마비","잠듦","얼음","동상"});
   Add(result,"상태변화",RuleBook.StatusHelp,new[]{"혼란","풀죽음","방어","도발","반동대기","대타","씨뿌리기","저주","아쿠아링·뿌리박기"});
   Add(result,"진영 상태",RuleBook.StatusHelp,new[]{"리플렉터·빛의장막","순풍"});
   Add(result,"설치물","스텔스록","교대로 나온 포켓몬에게 최대 HP의 1/8×바위 타입 상성만큼 대미지를 줍니다. 한 번만 설치할 수 있으며 비접지 포켓몬에게도 적용됩니다.");
   Add(result,"설치물","압정","접지 상태로 교대해 나온 포켓몬에게 1겹은 최대 HP의 1/8, 2겹은 1/6, 3겹은 1/4 대미지를 줍니다. 최대 3겹입니다.");
   Add(result,"설치물","독압정","접지 상태로 교대해 나온 포켓몬을 1겹이면 독, 2겹이면 맹독 상태로 만듭니다. 독 타입이 나오면 독압정을 흡수하며, 강철 타입에게는 통하지 않습니다. 최대 2겹입니다.");
   Add(result,"설치물","끈적끈적네트","접지 상태로 교대해 나온 포켓몬의 속도를 1랭크 내립니다. 한 번만 설치할 수 있습니다.");
   Add(result,"설치물","설치물 제거","고속스핀이 명중하면 사용자 진영의 설치물을 제거합니다. 안개제거는 양쪽 진영의 설치물을 제거합니다.");
   Add(result,"날씨",RuleBook.WeatherHelp,new[]{"쾌청 / 큰가뭄","비 / 강한 비","모래바람","눈","싸라기눈","난기류"});
   Add(result,"필드",RuleBook.WeatherHelp,new[]{"일렉트릭","그래스","사이코","미스트"});
   Add(result,"전체 장소",RuleBook.WeatherHelp,new[]{"트릭룸","중력"});
   Add(result,"날씨 연계",RuleBook.WeatherHelp,new[]{"날씨부정·에어록","번개·폭풍","쓱쓱·엽록소·모래헤치기·눈치우기"});
   Add(result,"도구 상태","풍선 터짐","풍선을 가진 포켓몬의 피격 시 BalloonPopped를 기록한다. 터진 풍선의 공중 판정은 적용하지 않는다.");
   Add(result,"특성 상태","불꽃 흡수 강화","타오르는불꽃이 불꽃 기술을 흡수하면 불꽃 기술 위력을 1.5배로 한다.");
   Add(result,"구현 범위","현재 규칙과 미구현","이 도감은 현재 프로그램의 RuleBook 설명을 기준으로 합니다.\n상태이상은 하나, 상태변화는 중첩됩니다. 지속 수치는 남은 처리 횟수입니다.\n등록되지 않은 효과, 등장 특성의 날씨 변경, 날씨 도구에 의한 지속 연장 등은 수동 판정입니다.\n교대 시 해제·유지 규칙은 단계적으로 구현 중입니다. 포텐셜의 자연어 효과는 아직 자동 실행되지 않습니다.");
   return result;
  }
  static void Add(List<StateDexEntry> list,string category,string name,string effect){list.Add(new StateDexEntry{category=category,name=name,effect=effect});}
  static void Add(List<StateDexEntry> list,string category,string help,string[] names){foreach(string name in names){var line=help.Split('\n').FirstOrDefault(x=>x.StartsWith(name+":"));if(line!=null)Add(list,category,name,line.Substring(name.Length+1).Trim());}}
  public static Control CreateView(){
   var host=new Panel{Dock=DockStyle.Fill,BackColor=Color.White};var category=Theme.Combo(new[]{"전체"}.Concat(Entries().Select(x=>x.category).Distinct()),"전체");category.Dock=DockStyle.Top;var search=new PixelSearchBox{Dock=DockStyle.Top};var grid=new PixelDexGrid{Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,AllowUserToDeleteRows=false,AllowUserToResizeRows=false,RowHeadersVisible=false,MultiSelect=false,SelectionMode=DataGridViewSelectionMode.FullRowSelect,BackgroundColor=Color.White,BorderStyle=BorderStyle.None,EnableHeadersVisualStyles=false,ColumnHeadersHeight=34,DefaultCellStyle=new DataGridViewCellStyle{Font=new Font("맑은 고딕",9),SelectionBackColor=Color.Black,SelectionForeColor=Color.White},ColumnHeadersDefaultCellStyle=new DataGridViewCellStyle{BackColor=Color.Black,ForeColor=Color.White,Font=Theme.UI(8)}};grid.Columns.Add(new DataGridViewTextBoxColumn{HeaderText="분류",Width=150});grid.Columns.Add(new DataGridViewTextBoxColumn{HeaderText="이름",Width=250});grid.Columns.Add(new DataGridViewTextBoxColumn{HeaderText="설명",AutoSizeMode=DataGridViewAutoSizeColumnMode.Fill,MinimumWidth=520});var detail=new PixelText{Dock=DockStyle.Bottom,Height=185};
   Action refresh=()=>{grid.Rows.Clear();foreach(var x in Entries().Where(x=>(category.Text=="전체"||x.category==category.Text)&&(x.name+x.effect).Contains(search.Text))){int i=grid.Rows.Add(x.category,x.name,x.effect);grid.Rows[i].Tag=x;}if(grid.Rows.Count>0){grid.CurrentCell=grid.Rows[0].Cells[0];ShowDetail(detail,(StateDexEntry)grid.Rows[0].Tag);}else detail.Text="검색 결과 없음";};
   grid.SelectionChanged+=(s,e)=>{if(grid.CurrentRow!=null)ShowDetail(detail,grid.CurrentRow.Tag as StateDexEntry);};category.SelectedIndexChanged+=(s,e)=>refresh();search.TextChanged+=(s,e)=>refresh();host.Controls.Add(new PixelGridHost(grid));host.Controls.Add(detail);host.Controls.Add(search);host.Controls.Add(category);host.Controls[0].BringToFront();refresh();return host;
  }
  static void ShowDetail(PixelText box,StateDexEntry x){if(x!=null)box.Text=x.category+" / "+x.name+"\n\n"+x.effect;}
 }
 public class StateDexForm:Form {public StateDexForm(){Text="상태·날씨·효과 도감";ClientSize=new Size(1050,720);MinimumSize=new Size(800,560);StartPosition=FormStartPosition.CenterParent;BackColor=Color.White;Controls.Add(StateDex.CreateView());}}
 partial class DataEditorForm {void BuildStateDexTab(){var page=Page("상태·날씨 도감");page.Controls.Add(StateDex.CreateView());}}
}
