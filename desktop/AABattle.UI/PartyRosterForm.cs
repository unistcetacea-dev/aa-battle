using System;
using System.Linq;
using System.Drawing;
using System.Windows.Forms;
using System.Collections.Generic;

namespace AABattle {
 class PartyRosterForm:Form {
  readonly TeamRecord[] source;
  readonly PixelComboBox[] trainers=new PixelComboBox[2];
  readonly PixelCheckedListBox[] members=new PixelCheckedListBox[2];
  public TeamRecord[] SelectedTeams { get; private set; }

  public PartyRosterForm(TeamRecord[] teams) {
   source=teams;
   Load+=(s,e)=>Theme.Enlarge(this);
   Text="출전 선택";Size=new Size(1040,700);MinimumSize=new Size(900,580);
   StartPosition=FormStartPosition.CenterParent;BackColor=Color.White;Font=Theme.UI();
   var columns=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=2,Padding=new Padding(12)};
   columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));
   columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));
   columns.RowStyles.Add(new RowStyle(SizeType.Percent,100));
   columns.RowStyles.Add(new RowStyle(SizeType.Absolute,58));
   for(int side=0;side<2;side++) {
    int rosterSide=side;
    var team=teams[side];
    var panel=new Panel{Dock=DockStyle.Fill,Padding=new Padding(8)};
    var title=new PixelLabel{Text=(side+1)+"팀 · "+team.name,Dock=DockStyle.Top,Height=38};
    trainers[side]=Theme.Combo((team.trainers??new List<TrainerRecord>()).Select(t=>t.name),"");
    trainers[side].Dock=DockStyle.Top;
    trainers[side].DropDownStyle=ComboBoxStyle.DropDownList;
    members[side]=new PixelCheckedListBox{Dock=DockStyle.Fill,CheckOnClick=true,Font=Theme.Input(),IntegralHeight=false};
    var candidates=(team.members??new List<PokemonRecord>()).Where(p=>p!=null&&string.IsNullOrWhiteSpace(p.formOf)).ToList();
    foreach(var pokemon in candidates) members[side].Items.Add(pokemon.name);
    int trainerIndex=team.trainers.FindIndex(t=>t.id==team.defaultTrainerId);
    if(trainerIndex<0)trainerIndex=team.trainers.FindIndex(t=>team.trainer!=null&&t.id==team.trainer.id);
    if(trainerIndex<0&&team.trainers.Count>0)trainerIndex=0;
    trainers[side].SelectedIndex=trainerIndex;
    var chosen=team.defaultMemberIds==null?new List<string>():team.defaultMemberIds;
    for(int i=0;i<candidates.Count;i++) {
     bool selected=chosen.Count>0?chosen.Contains(candidates[i].id):i<6;
     if(selected&&members[side].CheckedItems.Count<6)members[side].SetItemChecked(i,true);
    }
    members[side].ItemCheck+=(s,e)=>{
     if(e.NewValue==CheckState.Checked&&members[rosterSide].CheckedItems.Count>=6) e.NewValue=CheckState.Unchecked;
    };
    panel.Controls.Add(members[side]);
    panel.Controls.Add(trainers[side]);
    panel.Controls.Add(new PixelLabel{Text="출전 트레이너",Dock=DockStyle.Top,Height=32});
    panel.Controls.Add(title);
    columns.Controls.Add(panel,side,0);
   }
   var save=new FlatButton{Text="선택한 구성으로 배틀",Dock=DockStyle.Fill,Primary=true};
   save.Click+=(s,e)=>ConfirmSelection();
   columns.Controls.Add(save,0,1);columns.SetColumnSpan(save,2);
   Controls.Add(columns);
  }

  internal void SetSelectionForTest(int side,int trainerIndex,IEnumerable<int> rosterIndexes) {
   trainers[side].SelectedIndex=trainerIndex;
   for(int i=0;i<members[side].Items.Count;i++) members[side].SetItemChecked(i,false);
   foreach(int index in rosterIndexes) members[side].SetItemChecked(index,true);
  }

  internal void ConfirmForTest() { ConfirmSelection(); }

  void ConfirmSelection() {
   var selected=new List<TeamRecord>();
   for(int side=0;side<2;side++) {
    if(trainers[side].SelectedIndex<0) { GameDialog.Show(this,(side+1)+"팀 트레이너를 선택하세요.");return; }
    var team=source[side];
    var candidates=team.members.Where(p=>p!=null&&string.IsNullOrWhiteSpace(p.formOf)).ToList();
    var picks=Enumerable.Range(0,members[side].Items.Count).Where(i=>members[side].GetItemChecked(i)&&i<candidates.Count).Select(i=>candidates[i]).ToList();
    if(picks.Count<1||picks.Count>6) { GameDialog.Show(this,(side+1)+"팀 포켓몬을 1~6체 선택하세요.");return; }
    var trainer=team.trainers[trainers[side].SelectedIndex];
    selected.Add(new TeamRecord {
     name=team.name,trainer=trainer,trainers=new List<TrainerRecord>{trainer},trainerId=trainer.id,
     trainerIds=new List<string>{trainer.id},defaultTrainerId=trainer.id,members=picks,
     memberIds=picks.Select(p=>p.id).ToList(),defaultMemberIds=picks.Select(p=>p.id).ToList(),
     fieldEffect=team.fieldEffect
    });
   }
   SelectedTeams=selected.ToArray();DialogResult=DialogResult.OK;Close();
  }
 }
}
