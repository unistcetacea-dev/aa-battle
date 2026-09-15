using System;
using System.Linq;
using System.Drawing;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Web.Script.Serialization;
namespace AABattle {
 public class TeamRecord {public string name="";public TrainerRecord trainer;public List<PokemonRecord> members=new List<PokemonRecord>();public override string ToString(){return (name.Length>0?name:"이름 없는 팀")+"  ·  "+members.Count+"체";}}

 partial class DataEditorForm {
  PixelChoiceList teamLibrary;
  public EditorProject ExportProject(){SaveCurrent();return new JavaScriptSerializer{MaxJsonLength=50000000}.Deserialize<EditorProject>(Json());}
  public void ImportProject(EditorProject value){project=value??new EditorProject();if(project.teams==null)project.teams=new List<TeamRecord>();if(project.trainers==null)project.trainers=new List<TrainerRecord>();if(project.pokemon==null)project.pokemon=new List<PokemonRecord>();path="";dirty=false;RefreshTree(project.trainers.Cast<object>().Concat(project.pokemon).FirstOrDefault());RefreshTeamLibrary();}
  void BuildTeamTab(){
   var page=Page("팀 데이터");var bar=new FlowLayoutPanel{Dock=DockStyle.Top,Height=55,Padding=new Padding(5)};page.Controls.Add(bar);
   bar.Controls.Add(Button("새 팀",100,()=>EditTeam(-1)));bar.Controls.Add(Button("선택 팀 편집",130,()=>EditTeam(teamLibrary.SelectedIndex)));bar.Controls.Add(Button("선택 팀 삭제",130,DeleteTeam));
   teamLibrary=new PixelChoiceList{Dock=DockStyle.Left,Width=410};var help=new PixelText{Dock=DockStyle.Fill,Text="배틀에 쓸 팀을 이름별로 보관합니다.\n\n팀마다 트레이너 1명과 포켓몬을 원하는 수만큼 넣을 수 있습니다.\n여기서 팀 1·2를 정하지 않습니다. 시작 화면의 별도 선택 칸에서 저장된 팀 두 개를 불러옵니다.\n\n포켓몬을 클릭해 참가 여부를 바꾸고, 팀 이름과 트레이너 AA를 함께 저장하세요.\n포켓몬 데이터 수정 후에는 팀 편성을 다시 열어 확인하세요."};page.Controls.Add(help);page.Controls.Add(teamLibrary);help.BringToFront();RefreshTeamLibrary();
  }
  void RefreshTeamLibrary(){if(teamLibrary==null)return;int selected=teamLibrary.SelectedIndex;teamLibrary.Items.Clear();if(project!=null&&project.teams!=null)foreach(var team in project.teams)teamLibrary.Items.Add(team);if(teamLibrary.Items.Count>0)teamLibrary.SelectedIndex=Math.Max(0,Math.Min(selected,teamLibrary.Items.Count-1));}
  void EditTeam(int index){SaveCurrent();if(project.teams==null)project.teams=new List<TeamRecord>();TeamRecord old=index>=0&&index<project.teams.Count?project.teams[index]:null;using(var f=new TeamSetupForm(project,old))if(f.ShowDialog(this)==DialogResult.OK){if(old==null)project.teams.Add(f.Value);else project.teams[index]=f.Value;dirty=true;UpdateTitle();RefreshTeamLibrary();}}
  void DeleteTeam(){int index=teamLibrary==null?-1:teamLibrary.SelectedIndex;if(index<0||index>=project.teams.Count)return;if(MessageBox.Show(this,"선택한 팀 데이터를 삭제할까요?","팀 삭제",MessageBoxButtons.YesNo)!=DialogResult.Yes)return;project.teams.RemoveAt(index);dirty=true;UpdateTitle();RefreshTeamLibrary();}
 }

 class PixelChoiceList:ListBox {
  public PixelChoiceList(){Font=new Font("맑은 고딕",10);DrawMode=DrawMode.OwnerDrawFixed;ItemHeight=36;IntegralHeight=false;BackColor=Color.White;}
  protected override void OnDrawItem(DrawItemEventArgs e){if(e.Index<0)return;bool selected=(e.State&DrawItemState.Selected)!=0;e.Graphics.FillRectangle(selected?Brushes.Black:Brushes.White,e.Bounds);TextRenderer.DrawText(e.Graphics,Items[e.Index].ToString(),Font,new Rectangle(e.Bounds.X+8,e.Bounds.Y,e.Bounds.Width-12,e.Bounds.Height),selected?Color.White:Color.Black,TextFormatFlags.Left|TextFormatFlags.VerticalCenter);}
 }

 class TeamSetupForm:Form {
  public TeamRecord Value;ComboBox trainer;ListBox members;TextBox art,teamName;
  public TeamSetupForm(EditorProject p,TeamRecord old){Text=old==null?"새 팀":"팀 편집";Size=new Size(900,740);Font=Theme.UI();BackColor=Color.White;StartPosition=FormStartPosition.CenterParent;
   teamName=new TextBox{Dock=DockStyle.Top,Font=new Font("맑은 고딕",11),Text=old==null?"새 팀":old.name};trainer=Theme.Combo(p.trainers.Select(t=>t.name),"");trainer.Dock=DockStyle.Top;
   members=new PixelChoiceList{Dock=DockStyle.Left,Width=330,SelectionMode=SelectionMode.MultiSimple};foreach(var m in p.pokemon)members.Items.Add(m.name);
   art=new TextBox{Dock=DockStyle.Fill,Multiline=true,ScrollBars=ScrollBars.Both,WordWrap=false,Font=new Font(Fonts.AA,12)};
   trainer.SelectedIndex=old==null||old.trainer==null?-1:p.trainers.FindIndex(x=>x.name==old.trainer.name);
   if(old!=null){if(old.trainer!=null)art.Text=old.trainer.asciiArt;for(int i=0;i<p.pokemon.Count;i++)members.SetSelected(i,old.members.Any(x=>Object.ReferenceEquals(x,p.pokemon[i])||x.name==p.pokemon[i].name));}
   trainer.SelectedIndexChanged+=(s,e)=>{if(trainer.SelectedIndex>=0)art.Text=p.trainers[trainer.SelectedIndex].asciiArt;};
   var ok=new FlatButton{Text="팀 데이터 저장",Dock=DockStyle.Bottom,Height=50,Primary=true};ok.Click+=(s,e)=>{if(teamName.Text.Trim().Length==0){MessageBox.Show(this,"팀 이름을 입력하세요.");return;}if(trainer.SelectedIndex<0||members.SelectedIndices.Count<1){MessageBox.Show(this,"트레이너 1명과 포켓몬을 1체 이상 선택하세요.");return;}var t=p.trainers[trainer.SelectedIndex];t.asciiArt=art.Text;Value=new TeamRecord{name=teamName.Text.Trim(),trainer=t,members=members.SelectedIndices.Cast<int>().Select(i=>p.pokemon[i]).ToList()};DialogResult=DialogResult.OK;};
   Controls.Add(art);Controls.Add(members);Controls.Add(trainer);Controls.Add(new PixelLabel{Text="트레이너",Dock=DockStyle.Top,Height=32});Controls.Add(teamName);Controls.Add(new PixelLabel{Text="팀 이름",Dock=DockStyle.Top,Height=32});Controls.Add(new PixelLabel{Text="포켓몬 클릭: 참가 선택 · 인원 제한 없음 · 오른쪽: 트레이너 AA",Dock=DockStyle.Top,Height=40});Controls.Add(ok);
  }
 }

 static class FlowPreview {
  public static void Run(Database db,string path){
   var trainer=new TrainerRecord{name="회귀 트레이너",asciiArt="AA"};var source=new EditorProject();
   for(int side=0;side<2;side++){var team=new TeamRecord{name="보관 팀 "+(side+1),trainer=trainer};int count=side==0?8:9;for(int n=0;n<count;n++){var p=new PokemonRecord{name="팀"+side+"-"+n,types=new List<string>{"노말"},moves=new List<string>{"방어"},stats=new StatsRecord{hp=100,attack=100,defense=100,spAttack=100,spDefense=100,speed=100}};team.members.Add(p);source.pokemon.Add(p);}source.teams.Add(team);}source.trainers.Add(trainer);
   var saved=new JavaScriptSerializer().Deserialize<EditorProject>(new JavaScriptSerializer().Serialize(source));if(saved.teams[1].members.Count!=9||saved.teams[0].trainer.asciiArt!="AA")throw new Exception("Unlimited team roundtrip failed");
   var selected=saved.teams.Take(2).ToArray();var all=selected.Select(t=>t.members.Select(p=>{string warning;return new Fighter(EditorBridge.ToPokemon(p,db,out warning));}).ToList()).ToArray();using(var battle=new MainForm(db)){battle.LoadTeams(selected,all,new[]{2,4});battle.AssertTeamSetup(7,8);}
   using(var leads=new LeadForm(all,"선발 선택")){leads.Show();Application.DoEvents();Capture(leads,path+".leads.png");leads.Close();}
   using(var start=new StartForm(db)){start.SetProjectForTest(saved);start.Show();Application.DoEvents();Capture(start,path+".start.png");start.Close();}
   using(var editor=new DataEditorForm()){editor.ImportProject(saved);editor.Show();Application.DoEvents();editor.SelectTabForTest(6);Capture(editor,path+".teams.png");editor.SelectTabForTest(7);Capture(editor,path+".states.png");editor.SelectTabForTest(8);Capture(editor,path+".implementations.png");editor.CloseForTest();}
   using(var setup=new TeamSetupForm(saved,saved.teams[0])){setup.Show();Application.DoEvents();Capture(setup,path+".setup.png");setup.Close();}
   using(var intro=new VersusForm(selected)){var timer=new Timer{Interval=1800};timer.Tick+=(s,e)=>{timer.Stop();Capture(intro,path+".versus.png");};intro.Shown+=(s,e)=>timer.Start();intro.ShowDialog();timer.Dispose();}
  }
  static void Capture(Form form,string path){Application.DoEvents();using(var b=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(b,new Rectangle(Point.Empty,b.Size));b.Save(path);}}
 }

 class StartForm:Form {
  Database db;EditorProject project=new EditorProject();FlatButton start,edit,dex;ComboBox[] teamPick=new ComboBox[2];PixelLabel[] teamInfo=new PixelLabel[2];
  public StartForm(Database d){db=d;Text="포켓몬 AA 배틀";ClientSize=new Size(1100,740);MinimumSize=new Size(900,640);StartPosition=FormStartPosition.CenterScreen;BackColor=Color.White;DoubleBuffered=true;Font=Theme.UI();
   dex=new FlatButton{Text="상태·날씨·효과 도감",Size=new Size(220,42)};dex.Click+=(s,e)=>{using(var f=new StateDexForm())f.ShowDialog(this);};
   for(int i=0;i<2;i++){int side=i;teamPick[i]=Theme.Combo(new string[0],"");teamPick[i].Dock=DockStyle.None;teamPick[i].Size=new Size(330,38);teamPick[i].DropDownStyle=ComboBoxStyle.DropDownList;teamPick[i].SelectedIndexChanged+=(s,e)=>RefreshTeamInfo(side);teamInfo[i]=new PixelLabel{Size=new Size(330,42),TextAlign=ContentAlignment.MiddleCenter};Controls.Add(teamPick[i]);Controls.Add(teamInfo[i]);}
   start=new FlatButton{Text="배틀 시작",Primary=true,Size=new Size(290,62)};edit=new FlatButton{Text="팀 데이터 편집",Size=new Size(290,62)};
   start.Click+=(s,e)=>Begin();edit.Click+=(s,e)=>{using(var f=new DataEditorForm()){f.ImportProject(project);f.ShowDialog(this);project=f.ExportProject();RefreshTeamChoices();}};
   Controls.Add(dex);Controls.Add(start);Controls.Add(edit);Resize+=(s,e)=>Arrange();RefreshTeamChoices();Arrange();
  }
  internal void SetProjectForTest(EditorProject p){project=p;RefreshTeamChoices();}
  void RefreshTeamChoices(){var names=(project.teams??new List<TeamRecord>()).Select(x=>x.ToString()).ToArray();for(int side=0;side<2;side++){int old=teamPick[side].SelectedIndex;teamPick[side].Items.Clear();teamPick[side].Items.AddRange(names);if(names.Length>0)teamPick[side].SelectedIndex=Math.Min(old>=0?old:side,names.Length-1);RefreshTeamInfo(side);}}
  void RefreshTeamInfo(int side){int i=teamPick[side].SelectedIndex;teamInfo[side].Text=i>=0&&i<project.teams.Count?(side+1)+"팀 · "+project.teams[i].name+" · "+project.teams[i].members.Count+"체":(side+1)+"팀 · 팀 데이터를 선택하세요";}
  void Arrange(){dex.Location=new Point(ClientSize.Width-270,65);int gap=70,left=(ClientSize.Width-660-gap)/2;teamPick[0].Location=new Point(left,350);teamPick[1].Location=new Point(left+330+gap,350);teamInfo[0].Location=new Point(left,390);teamInfo[1].Location=new Point(left+330+gap,390);start.Location=new Point((ClientSize.Width-290)/2,475);edit.Location=new Point(start.Left,start.Top+82);}
  protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);Theme.Frame(e.Graphics,new Rectangle(35,35,ClientSize.Width-70,ClientSize.Height-70));using(var font=Theme.UI(28))Theme.Text(e.Graphics,"포켓몬 AA 배틀",font,new Rectangle(60,150,ClientSize.Width-120,95),Color.Black,ContentAlignment.MiddleCenter);using(var font=Theme.UI(11))Theme.Text(e.Graphics,"POKEMON  /  AA BATTLE",font,new Rectangle(60,250,ClientSize.Width-120,45),Color.Black,ContentAlignment.MiddleCenter);using(var font=Theme.UI(10))Theme.Text(e.Graphics,"1팀",font,new Rectangle(teamPick[0].Left,315,330,32),Color.Black,ContentAlignment.MiddleCenter);using(var font=Theme.UI(10))Theme.Text(e.Graphics,"2팀",font,new Rectangle(teamPick[1].Left,315,330,32),Color.Black,ContentAlignment.MiddleCenter);}
  void Begin(){try{if(project.teams==null||project.teams.Count==0)throw new Exception("팀 데이터 편집에서 팀을 먼저 저장하세요.");if(teamPick.Any(x=>x.SelectedIndex<0))throw new Exception("1팀과 2팀 칸에서 팀 데이터를 선택하세요.");var teams=new[]{project.teams[teamPick[0].SelectedIndex],project.teams[teamPick[1].SelectedIndex]};if(teams.Any(x=>x==null||x.trainer==null||x.members==null||x.members.Count<1))throw new Exception("선택한 팀에 트레이너와 포켓몬이 필요합니다.");
   var fighters=teams.Select(t=>t.members.Select(m=>{string warning;var f=new Fighter(EditorBridge.ToPokemon(m,db,out warning));if(warning.Length>0)throw new Exception(m.name+": "+warning);f.AA=m.asciiArt;return f;}).ToList()).ToArray();using(var intro=new VersusForm(teams)){if(intro.ShowDialog(this)!=DialogResult.OK)return;}int[] leads;using(var pick=new LeadForm(fighters,"선발 선택")){if(pick.ShowDialog(this)!=DialogResult.OK)return;leads=pick.Selected;}using(var battle=new MainForm(db)){battle.LoadTeams(teams,fighters,leads);Hide();try{battle.ShowDialog();}finally{Show();}}
  }catch(Exception ex){MessageBox.Show(this,ex.Message,"배틀 준비");}}
 }

 class VersusForm:Form {
  TeamRecord[] teams;AAView[] arts=new AAView[2];Timer timer=new Timer{Interval=16};System.Diagnostics.Stopwatch watch=new System.Diagnostics.Stopwatch();int balls,maxBalls;
  public VersusForm(TeamRecord[] value){teams=value;maxBalls=Math.Max(teams[0].members.Count,teams[1].members.Count);Text="VS";ClientSize=new Size(1100,Math.Min(900,650+Math.Max(0,(maxBalls+9)/10-2)*32));StartPosition=FormStartPosition.CenterParent;BackColor=Color.White;DoubleBuffered=true;for(int i=0;i<2;i++){arts[i]=new AAView{Art=teams[i].trainer.asciiArt??"",Size=new Size(440,420)};Controls.Add(arts[i]);}Shown+=(s,e)=>{watch.Start();timer.Start();};timer.Tick+=(s,e)=>TickIntro();FormClosed+=(s,e)=>timer.Dispose();}
  void TickIntro(){double sec=watch.Elapsed.TotalSeconds,p=Math.Min(1,sec/.85);int shift=(int)((1-p)*(1-p)*600);if(sec>=.85&&sec<1.1)shift=(int)(Math.Sin(sec*100)*10);arts[0].Location=new Point(30-shift,80);arts[1].Location=new Point(ClientSize.Width-470+shift,80);int next=Math.Min(maxBalls,Math.Max(0,(int)((sec-1)*12)));if(next>balls)System.Media.SystemSounds.Asterisk.Play();balls=next;Invalidate();double end=Math.Min(6,2.2+maxBalls*.06);if(sec>end){timer.Stop();DialogResult=DialogResult.OK;Close();}}
  protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);using(var f=Theme.UI(24))Theme.Text(e.Graphics,"VS",f,new Rectangle(Width/2-60,250,120,80),Color.Black,ContentAlignment.MiddleCenter);for(int side=0;side<2;side++){int x=side==0?45:ClientSize.Width-455;using(var f=Theme.UI(13))Theme.Text(e.Graphics,teams[side].trainer.name,f,new Rectangle(x,25,410,45),Color.Black,ContentAlignment.MiddleCenter);int shown=Math.Min(balls,teams[side].members.Count);for(int j=0;j<shown;j++){int bx=x+55+(j%10)*30,by=520+(j/10)*32;e.Graphics.DrawEllipse(Pens.Black,bx,by,24,24);e.Graphics.FillPie(Brushes.Black,bx,by,24,24,180,180);e.Graphics.DrawLine(Pens.Black,bx,by+12,bx+24,by+12);e.Graphics.FillEllipse(Brushes.White,bx+9,by+9,6,6);e.Graphics.DrawEllipse(Pens.Black,bx+9,by+9,6,6);}}}
 }

 class LeadForm:Form {
  public int[] Selected=new int[2];public LeadForm(List<Fighter>[] teams,string title){Text=title;Size=new Size(850,460);StartPosition=FormStartPosition.CenterParent;Font=Theme.UI();BackColor=Color.White;var layout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2};for(int i=0;i<2;i++){int side=i;layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));var list=new PixelChoiceList{Dock=DockStyle.Fill};foreach(var f in teams[i])list.Items.Add(f.Data.name+"  HP "+f.HP);list.SelectedIndex=0;list.SelectedIndexChanged+=(s,e)=>Selected[side]=list.SelectedIndex;var panel=new Panel{Dock=DockStyle.Fill};panel.Controls.Add(list);panel.Controls.Add(new PixelLabel{Text=(side+1)+"팀 · 선발 선택",Dock=DockStyle.Top,Height=40});list.BringToFront();layout.Controls.Add(panel,i,0);}var ok=new FlatButton{Text="선발 확정",Dock=DockStyle.Bottom,Height=55,Primary=true,DialogResult=DialogResult.OK};Controls.Add(layout);Controls.Add(ok);}
 }

 partial class MainForm {
  bool ReplaceFainted(){for(int side=0;side<2;side++)if(Fighters[side].HP<=0){var available=reserves[side].Where(x=>x.HP>0).ToList();if(available.Count==0){MessageBox.Show(this,(side==0?"2팀":"1팀")+" 승리!");return false;}using(var pick=new Form{Text=(side+1)+"팀 · 죽어내밀기",Size=new Size(460,410),StartPosition=FormStartPosition.CenterParent,BackColor=Color.White}){var list=new PixelChoiceList{Dock=DockStyle.Fill};foreach(var f in available)list.Items.Add(f.Data.name);list.SelectedIndex=0;var ok=new FlatButton{Text="포켓몬 내보내기",Dock=DockStyle.Bottom,Height=50,DialogResult=DialogResult.OK};pick.Controls.Add(list);pick.Controls.Add(ok);if(pick.ShowDialog(this)!=DialogResult.OK)return false;Save();var incoming=available[list.SelectedIndex];reserves[side].Remove(incoming);reserves[side].Add(Fighters[side]);Fighters[side]=incoming;selections[side]=new TurnSelection();var hazardLog=new List<string>();Mechanics.EnterField(db,incoming,Fighters[1-side],side,Rules,random.NextDouble,hazardLog);foreach(string line in hazardLog)log+=line+"\r\n";EntryTriggers.Enter(incoming,EntryReason.Replacement,x=>log+=x+"\r\n");}}RefreshView();return true;}
  public void AssertTeamSetup(int reserve0,int reserve1){if(Fighters[0].Data.name!="팀0-2"||Fighters[1].Data.name!="팀1-4"||reserves[0].Count!=reserve0||reserves[1].Count!=reserve1||trainers.Any(x=>x==null))throw new Exception("Team setup failed");}
  public void LoadTeams(TeamRecord[] teams,List<Fighter>[] all,int[] leads){for(int i=0;i<2;i++){trainers[i]=teams[i].trainer;Fighters[i]=all[i][leads[i]];reserves[i]=all[i].Where((x,n)=>n!=leads[i]).ToList();EntryTriggers.Enter(Fighters[i],EntryReason.Lead,logLine=>log+=logLine+"\r\n");}undo.Clear();RefreshView();}
 }
}
