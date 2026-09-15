using System;
using System.Linq;
using System.Drawing;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Web.Script.Serialization;
namespace AABattle {
 public class TeamRecord {public string name="";public TrainerRecord trainer;public List<PokemonRecord> members=new List<PokemonRecord>();}
 partial class DataEditorForm {
  public EditorProject ExportProject(){SaveCurrent();return new JavaScriptSerializer{MaxJsonLength=50000000}.Deserialize<EditorProject>(Json());}
  public void ImportProject(EditorProject value){project=value;path="";dirty=false;RefreshTree(project.trainers.Cast<object>().Concat(project.pokemon).FirstOrDefault());}
  void BuildTeamTab(){
   var page=Page("1팀 / 2팀");var bar=new FlowLayoutPanel{Dock=DockStyle.Top,Height=55};page.Controls.Add(bar);
   bar.Controls.Add(Button("1팀 편성",150,()=>EditTeam(0)));bar.Controls.Add(Button("2팀 편성",150,()=>EditTeam(1)));
   var help=new PixelText{Dock=DockStyle.Fill,Text="1팀은 왼쪽, 2팀은 오른쪽입니다.\n\n각 팀에 트레이너와 포켓몬 1~6체를 지정하세요.\n트레이너 AA도 여기서 입력합니다.\n\n편성을 저장한 뒤 시작 화면에서 배틀 시작을 누르면 대전 연출 → 선발 선택 순서로 진행합니다.\n\n편성은 현재 데이터를 복사합니다. 포켓몬을 수정했다면 다시 편성해 적용하세요."};page.Controls.Add(help);help.BringToFront();
  }
  void EditTeam(int side){SaveCurrent();if(project.teams==null||project.teams.Length!=2)project.teams=new TeamRecord[2];using(var f=new TeamSetupForm(project,side))if(f.ShowDialog(this)==DialogResult.OK){project.teams[side]=f.Value;dirty=true;UpdateTitle();}}
 }
  class PixelChoiceList:ListBox {
  public PixelChoiceList(){Font=Theme.UI();DrawMode=DrawMode.OwnerDrawFixed;ItemHeight=36;IntegralHeight=false;BackColor=Color.White;}
  protected override void OnDrawItem(DrawItemEventArgs e){if(e.Index<0)return;bool selected=(e.State&DrawItemState.Selected)!=0;e.Graphics.FillRectangle(selected?Brushes.Black:Brushes.White,e.Bounds);Theme.Text(e.Graphics,Items[e.Index].ToString(),Font,e.Bounds,selected?Color.White:Color.Black,ContentAlignment.MiddleLeft);}
 }
 class TeamSetupForm:Form {
  public TeamRecord Value;ComboBox trainer;ListBox members;TextBox art;
  public TeamSetupForm(EditorProject p,int side){Text=(side+1)+"팀 편성";Size=new Size(850,720);Font=Theme.UI();BackColor=Color.White;StartPosition=FormStartPosition.CenterParent;
   trainer=Theme.Combo(p.trainers.Select(t=>t.name),"");trainer.Dock=DockStyle.Top;
   members=new PixelChoiceList{Dock=DockStyle.Left,Width=300,SelectionMode=SelectionMode.MultiSimple};foreach(var m in p.pokemon)members.Items.Add(m.name);
   art=new TextBox{Dock=DockStyle.Fill,Multiline=true,ScrollBars=ScrollBars.Both,WordWrap=false,Font=new Font(Fonts.AA,12)};
   var old=p.teams[side];trainer.SelectedIndex=old==null||old.trainer==null?-1:p.trainers.FindIndex(x=>x.name==old.trainer.name);
   if(old!=null&&old.trainer!=null){art.Text=old.trainer.asciiArt;for(int i=0;i<p.pokemon.Count;i++)members.SetSelected(i,old.members.Any(x=>x.name==p.pokemon[i].name));}
   trainer.SelectedIndexChanged+=(s,e)=>{if(trainer.SelectedIndex>=0)art.Text=p.trainers[trainer.SelectedIndex].asciiArt;};
   var ok=new FlatButton{Text="팀 저장",Dock=DockStyle.Bottom,Height=50,Primary=true};ok.Click+=(s,e)=>{if(trainer.SelectedIndex<0||members.SelectedIndices.Count<1||members.SelectedIndices.Count>6){MessageBox.Show(this,"트레이너 1명과 포켓몬 1~6체를 선택하세요.");return;}var t=p.trainers[trainer.SelectedIndex];t.asciiArt=art.Text;Value=new TeamRecord{name=(side+1)+"팀",trainer=t,members=members.SelectedIndices.Cast<int>().Select(i=>p.pokemon[i]).ToList()};DialogResult=DialogResult.OK;};
   Controls.Add(art);Controls.Add(members);Controls.Add(trainer);Controls.Add(new PixelLabel{Text="트레이너 선택 / 포켓몬 클릭으로 참가 선택 / 오른쪽에 트레이너 AA 입력",Dock=DockStyle.Top,Height=42});Controls.Add(ok);
  }
 }
  static class FlowPreview {
    public static void Run(Database db,string path){
   var trainer=new TrainerRecord{name="회귀 트레이너",asciiArt="AA"};
   var source=new EditorProject();source.teams=new[]{new TeamRecord{trainer=trainer},new TeamRecord{trainer=trainer}};
   for(int i=0;i<2;i++)for(int n=0;n<6;n++){var p=new PokemonRecord{name="팀"+i+"-"+n,types=new List<string>{"노말"},moves=new List<string>{"방어"},stats=new StatsRecord{hp=100,attack=100,defense=100,spAttack=100,spDefense=100,speed=100}};source.teams[i].members.Add(p);}
   var saved=new JavaScriptSerializer().Deserialize<EditorProject>(new JavaScriptSerializer().Serialize(source));
   if(saved.teams[1].members.Count!=6||saved.teams[0].trainer.asciiArt!="AA")throw new Exception("Team roundtrip failed");
   var all=saved.teams.Select(t=>t.members.Select(p=>{string warning;return new Fighter(EditorBridge.ToPokemon(p,db,out warning));}).ToList()).ToArray();
   using(var battle=new MainForm(db)){battle.LoadTeams(saved.teams,all,new[]{2,4});battle.AssertTeamSetup();}
   using(var leads=new LeadForm(all,"선발 선택")){leads.Show();Application.DoEvents();Capture(leads,path+".leads.png");leads.Close();}
   using(var start=new StartForm(db)){start.Show();Application.DoEvents();Capture(start,path+".start.png");start.Close();}
   using(var editor=new DataEditorForm()){editor.Sample();editor.Show();Application.DoEvents();editor.SelectTabForTest(6);Capture(editor,path+".teams.png");editor.SelectTabForTest(7);Capture(editor,path+".states.png");editor.CloseForTest();}
   var demo=new EditorProject();demo.trainers.Add(new TrainerRecord{name="트레이너 1",asciiArt="  /\\_/\\\r\n ( o.o )\r\n  > ^ <"});demo.pokemon.Add(new PokemonRecord{name="샘플 포켓몬"});
   using(var setup=new TeamSetupForm(demo,0)){setup.Show();Application.DoEvents();Capture(setup,path+".setup.png");setup.Close();}
   var teams=new[]{new TeamRecord{trainer=demo.trainers[0],members=demo.pokemon},new TeamRecord{trainer=new TrainerRecord{name="트레이너 2",asciiArt=demo.trainers[0].asciiArt},members=demo.pokemon}};
   using(var intro=new VersusForm(teams)){var t=new Timer{Interval=1800};t.Tick+=(s,e)=>{t.Stop();Capture(intro,path+".versus.png");};intro.Shown+=(s,e)=>t.Start();intro.ShowDialog();t.Dispose();}
  }
  static void Capture(Form form,string path){Application.DoEvents();using(var b=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(b,new Rectangle(Point.Empty,b.Size));b.Save(path);}}
 }
 class StartForm:Form {
  Database db;EditorProject project=new EditorProject();FlatButton start,edit;
  public StartForm(Database d){db=d;Text="포켓몬 AA 배틀";ClientSize=new Size(1100,740);MinimumSize=new Size(900,640);StartPosition=FormStartPosition.CenterScreen;BackColor=Color.White;DoubleBuffered=true;Font=Theme.UI();
   start=new FlatButton{Text="배틀 시작",Primary=true,Size=new Size(290,62)};edit=new FlatButton{Text="팀 편집",Size=new Size(290,62)};
   start.Click+=(s,e)=>Begin();edit.Click+=(s,e)=>{using(var f=new DataEditorForm()){f.ImportProject(project);f.ShowDialog(this);project=f.ExportProject();}};
   Controls.Add(start);Controls.Add(edit);Resize+=(s,e)=>Arrange();Arrange();
  }
  void Arrange(){start.Location=new Point((ClientSize.Width-290)/2,ClientSize.Height-235);edit.Location=new Point(start.Left,start.Top+82);}
  protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);Theme.Frame(e.Graphics,new Rectangle(35,35,ClientSize.Width-70,ClientSize.Height-70));using(var font=Theme.UI(28))Theme.Text(e.Graphics,"포켓몬 AA 배틀",font,new Rectangle(60,170,ClientSize.Width-120,95),Color.Black,ContentAlignment.MiddleCenter);using(var font=Theme.UI(11))Theme.Text(e.Graphics,"POKEMON  /  AA BATTLE",font,new Rectangle(60,290,ClientSize.Width-120,45),Color.Black,ContentAlignment.MiddleCenter);}
  void Begin(){try{if(project.teams==null||project.teams.Any(x=>x==null))throw new Exception("팀 편집 → 1팀 / 2팀에서 두 팀을 먼저 편성하세요.");
   var fighters=project.teams.Select(t=>t.members.Select(m=>{string warning;var f=new Fighter(EditorBridge.ToPokemon(m,db,out warning));if(warning.Length>0)throw new Exception(m.name+": "+warning);f.AA=m.asciiArt;return f;}).ToList()).ToArray();
   using(var intro=new VersusForm(project.teams)){if(intro.ShowDialog(this)!=DialogResult.OK)return;}
   int[] leads=new int[2];using(var pick=new LeadForm(fighters,"선발 선택")){if(pick.ShowDialog(this)!=DialogResult.OK)return;leads=pick.Selected;}
   using(var battle=new MainForm(db)){battle.LoadTeams(project.teams,fighters,leads);Hide();try{battle.ShowDialog();}finally{Show();}}
  }catch(Exception ex){MessageBox.Show(this,ex.Message,"배틀 준비");}}
 }
 class VersusForm:Form {
  TeamRecord[] teams;AAView[] arts=new AAView[2];Timer timer=new Timer{Interval=16};System.Diagnostics.Stopwatch watch=new System.Diagnostics.Stopwatch();int balls;
  public VersusForm(TeamRecord[] t){teams=t;Text="VS";ClientSize=new Size(1100,700);StartPosition=FormStartPosition.CenterParent;BackColor=Color.White;DoubleBuffered=true;
   for(int i=0;i<2;i++){arts[i]=new AAView{Art=t[i].trainer.asciiArt??"",Size=new Size(440,430)};Controls.Add(arts[i]);}
   Shown+=(s,e)=>{watch.Start();timer.Start();};timer.Tick+=(s,e)=>TickIntro();FormClosed+=(s,e)=>timer.Dispose();
  }
  void TickIntro(){double sec=watch.Elapsed.TotalSeconds;double p=Math.Min(1,sec/.85);int shift=(int)((1-p)*(1-p)*600);if(sec>=.85&&sec<1.1)shift=(int)(Math.Sin(sec*100)*10);arts[0].Location=new Point(30-shift,90);arts[1].Location=new Point(ClientSize.Width-470+shift,90);int next=Math.Min(6,Math.Max(0,(int)((sec-1)*8)));if(next>balls)System.Media.SystemSounds.Asterisk.Play();balls=next;Invalidate();if(sec>2.7){timer.Stop();DialogResult=DialogResult.OK;Close();}}
  protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);using(var f=Theme.UI(24))Theme.Text(e.Graphics,"VS",f,new Rectangle(Width/2-60,260,120,80),Color.Black,ContentAlignment.MiddleCenter);
   for(int side=0;side<2;side++){int x=side==0?40:ClientSize.Width-450;using(var f=Theme.UI(13))Theme.Text(e.Graphics,teams[side].trainer.name,f,new Rectangle(x,35,430,45),Color.Black,ContentAlignment.MiddleCenter);for(int j=0;j<balls;j++){int bx=x+j*65;e.Graphics.DrawEllipse(Pens.Black,bx,560,40,40);if(j<teams[side].members.Count)e.Graphics.FillPie(Brushes.Black,bx,560,40,40,180,180);e.Graphics.DrawLine(Pens.Black,bx,580,bx+40,580);e.Graphics.FillEllipse(Brushes.White,bx+15,575,10,10);e.Graphics.DrawEllipse(Pens.Black,bx+15,575,10,10);}}}
 }
 class LeadForm:Form {
  public int[] Selected=new int[2];public LeadForm(List<Fighter>[] teams,string title){Text=title;Size=new Size(850,460);StartPosition=FormStartPosition.CenterParent;Font=Theme.UI();BackColor=Color.White;var layout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2};for(int i=0;i<2;i++){int side=i;layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));var list=new PixelChoiceList{Dock=DockStyle.Fill};foreach(var f in teams[i])list.Items.Add(f.Data.name+"  HP "+f.HP);list.SelectedIndex=0;list.SelectedIndexChanged+=(s,e)=>Selected[side]=list.SelectedIndex;var panel=new Panel{Dock=DockStyle.Fill};panel.Controls.Add(list);panel.Controls.Add(new PixelLabel{Text=(side+1)+"팀 · 선발 선택",Dock=DockStyle.Top,Height=40});list.BringToFront();layout.Controls.Add(panel,i,0);}var ok=new FlatButton{Text="선발 확정",Dock=DockStyle.Bottom,Height=55,Primary=true,DialogResult=DialogResult.OK};Controls.Add(layout);Controls.Add(ok);}
 }
 partial class MainForm {
    bool ReplaceFainted(){
   for(int side=0;side<2;side++)if(Fighters[side].HP<=0){
    var available=reserves[side].Where(x=>x.HP>0).ToList();if(available.Count==0){MessageBox.Show(this,(side==0?"2팀":"1팀")+" 승리!");return false;}
    using(var pick=new Form{Text=(side+1)+"팀 · 죽어내밀기",Size=new Size(460,410),StartPosition=FormStartPosition.CenterParent,BackColor=Color.White}){
     var list=new PixelChoiceList{Dock=DockStyle.Fill};foreach(var f in available)list.Items.Add(f.Data.name);list.SelectedIndex=0;
     var ok=new FlatButton{Text="포켓몬 내보내기",Dock=DockStyle.Bottom,Height=50,DialogResult=DialogResult.OK};pick.Controls.Add(list);pick.Controls.Add(ok);
     if(pick.ShowDialog(this)!=DialogResult.OK)return false;
     Save();var incoming=available[list.SelectedIndex];reserves[side].Remove(incoming);reserves[side].Add(Fighters[side]);Fighters[side]=incoming;selections[side]=new TurnSelection();EntryTriggers.Enter(incoming,EntryReason.Replacement,x=>log+=x+"\r\n");
    }
   }RefreshView();return true;
  }
  public void AssertTeamSetup(){if(Fighters[0].Data.name!="팀0-2"||Fighters[1].Data.name!="팀1-4"||reserves.Any(x=>x.Count!=5)||trainers.Any(x=>x==null))throw new Exception("Team setup failed");} public void LoadTeams(TeamRecord[] teams,List<Fighter>[] all,int[] leads){for(int i=0;i<2;i++){trainers[i]=teams[i].trainer;Fighters[i]=all[i][leads[i]];reserves[i]=all[i].Where((x,n)=>n!=leads[i]).ToList();EntryTriggers.Enter(Fighters[i],EntryReason.Lead,logLine=>log+=logLine+"\r\n");}undo.Clear();RefreshView();}
 }
}
