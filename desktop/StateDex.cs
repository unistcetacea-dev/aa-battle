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
   Add(result,"상태이상",RuleBook.StatusHelp,new[]{"화상","독","맹독","마비","잠듦","얼음","동상"});
   Add(result,"상태변화",RuleBook.StatusHelp,new[]{"혼란","풀죽음","방어","도발","반동대기","대타","씨뿌리기","저주","아쿠아링·뿌리박기"});
   Add(result,"진영 상태",RuleBook.StatusHelp,new[]{"리플렉터·빛의장막","순풍"});
   Add(result,"날씨",RuleBook.WeatherHelp,new[]{"쾌청 / 큰가뭄","비 / 강한 비","모래바람","눈","싸라기눈","난기류"});
   Add(result,"필드",RuleBook.WeatherHelp,new[]{"일렉트릭","그래스","사이코","미스트"});
   Add(result,"전체 장소",RuleBook.WeatherHelp,new[]{"트릭룸","중력"});
   Add(result,"날씨 연계",RuleBook.WeatherHelp,new[]{"날씨부정·에어록","번개·폭풍","쓱쓱·엽록소·모래헤치기·눈치우기"});
   result.Add(new StateDexEntry{category="도구 상태",name="풍선 터짐",effect="풍선을 가진 포켓몬의 피격 시 BalloonPopped를 기록한다. 터진 풍선의 공중 판정은 적용하지 않는다."});
   result.Add(new StateDexEntry{category="특성 상태",name="불꽃 흡수 강화",effect="타오르는불꽃이 불꽃 기술을 흡수하면 불꽃 기술 위력을 1.5배로 한다."});
   result.Add(new StateDexEntry{category="구현 범위",name="현재 규칙과 미구현",effect="이 도감은 현재 프로그램의 RuleBook 설명을 기준으로 합니다.\n상태이상은 하나, 상태변화는 중첩됩니다. 지속 수치는 남은 처리 횟수입니다.\n등록되지 않은 효과, 등장 특성의 날씨 변경, 날씨 도구에 의한 지속 연장 등은 수동 판정입니다.\n교대 시 해제·유지 규칙은 단계적으로 구현 중입니다. 포텐셜의 자연어 효과는 아직 자동 실행되지 않습니다."});
   return result;
  }
  static void Add(List<StateDexEntry> list,string category,string help,string[] names){foreach(string name in names){var line=help.Split('\n').FirstOrDefault(x=>x.StartsWith(name+":"));if(line!=null)list.Add(new StateDexEntry{category=category,name=name,effect=line.Substring(name.Length+1).Trim()});}}
 }
 partial class DataEditorForm {
  void BuildStateDexTab(){var page=Page("상태·날씨 도감");var category=Theme.Combo(new[]{"전체"}.Concat(StateDex.Entries().Select(x=>x.category).Distinct()),"전체");category.Dock=DockStyle.Top;var search=new PixelSearchBox{Dock=DockStyle.Top};var grid=DexGrid();DexColumn(grid,"분류",150);DexColumn(grid,"이름",250);DexColumn(grid,"효과",400);var detail=new PixelText{Dock=DockStyle.Bottom,Height=180};
   Action refresh=()=>{grid.Rows.Clear();foreach(var x in StateDex.Entries().Where(x=>(category.Text=="전체"||x.category==category.Text)&&(x.name+x.effect).Contains(search.Text))){int i=grid.Rows.Add(x.category,x.name,x.effect);grid.Rows[i].Tag=x;}if(grid.Rows.Count>0){grid.CurrentCell=grid.Rows[0].Cells[0];var first=(StateDexEntry)grid.Rows[0].Tag;detail.Text=first.category+" / "+first.name+"\n\n"+first.effect;}else detail.Text="검색 결과 없음";};
   grid.SelectionChanged+=(s,e)=>{if(grid.CurrentRow!=null){var x=grid.CurrentRow.Tag as StateDexEntry;if(x!=null)detail.Text=x.category+" / "+x.name+"\n\n"+x.effect;}};
   category.SelectedIndexChanged+=(s,e)=>refresh();search.TextChanged+=(s,e)=>refresh();page.Controls.Add(new PixelGridHost(grid));page.Controls.Add(detail);page.Controls.Add(search);page.Controls.Add(category);page.Controls[0].BringToFront();refresh();
  }
 }
}
