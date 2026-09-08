using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Reflection;
using System.Web.Script.Serialization;

namespace AABattle {
 public class Potential { public string id, name, description; public string[] triggers; }
 public class Pokemon {
  public string id, team, name, ability, item;
  public int level; public string[] types, moves; public int[] @base, iv;
  public Potential[] potentials;
 }
 public class Move {
  public string name, category, effect, attack, defense; public string[] types, tags;
  public double power, accuracy; public int priority;
 }
 public class Database {
  public Pokemon[] pokemon; public Move[] moves; public string[] types;
  public Dictionary<string,Dictionary<string,double>> chart;
  public static Database Load() { var json=new JavaScriptSerializer {MaxJsonLength=10000000};return json.Deserialize<Database>(json.Serialize(Normalize(json.DeserializeObject(Resource.Text("data.json"))))); }
  static object Normalize(object v){var d=v as Dictionary<string,object>;if(d!=null)return d.ToDictionary(p=>p.Key,p=>Normalize(p.Value));var a=v as object[];if(a!=null)return a.Select(Normalize).ToArray();if(v is decimal){decimal n=(decimal)v;if(n==decimal.Truncate(n)&&n>=int.MinValue&&n<=int.MaxValue)return (int)n;}return v;}
 }
 public static class Resource {
  public static byte[] Bytes(string name) { using(var s=Assembly.GetExecutingAssembly().GetManifestResourceStream(name)) { if(s==null)throw new Exception("Missing resource: "+name);using(var m=new MemoryStream()){s.CopyTo(m);return m.ToArray();} } }
  public static string Text(string name){return System.Text.Encoding.UTF8.GetString(Bytes(name));}
 }
 public class Fighter {
  public Pokemon Data; public string AA="", Status="정상"; public int HP; public int[] Stages=new int[6]; public double[] Multipliers={1,1,1,1,1,1};
  public string[] Moves; public int Selected; public ConditionState Conditions=new ConditionState();
  public Fighter(Pokemon p){Data=ClonePokemon(p);Moves=(p.moves??new string[0]).Take(4).ToArray();HP=Engine.Stats(this,p.level,new Rules())[0];}
  public Fighter Copy(){var f=new Fighter(Data);f.AA=AA;f.Status=Status;f.HP=HP;f.Stages=(int[])Stages.Clone();f.Multipliers=(double[])Multipliers.Clone();f.Moves=(string[])Moves.Clone();f.Selected=Selected;f.Conditions=Conditions.Copy();return f;}
  static Pokemon ClonePokemon(Pokemon p){return new Pokemon{id=p.id,name=p.name,team=p.team,ability=p.ability,item=p.item,level=p.level,types=(string[])p.types.Clone(),@base=(int[])p.@base.Clone(),iv=(int[])p.iv.Clone(),moves=(string[])p.moves.Clone(),potentials=p.potentials};}
 }
 public class Rules {
  public string Weather="없음", Terrain="없음"; public bool Critical, HPCorrection;
  public double PowerBonus, PowerMultiplier=1, DamageMultiplier=1, Stab, LevelCorrection=1;
  public int WeatherTurns=5,TerrainTurns=5,TrickRoom,Gravity;public bool SheetTerrain;public Rules Copy(){return (Rules)MemberwiseClone();}
 }
 public class Damage { public int Min,Max; public int[] Rolls;public double Stab,Type; public bool Supported;public string Reason=""; }
 // Potential execution is intentionally absent. Event names reserve the extension boundary.
 public enum BattleEvent { OnEnter,BeforeMove,BeforeDamage,AfterDamage,TurnEnd }
 public class TurnResult { public Rules Rules; public Fighter[] Fighters;public List<string> Log=new List<string>(); }
 public static class Engine {
  public static readonly string[] Names={"체력","공격","방어","특공","특방","속도"};
  public static int[] Stats(Fighter f,int other,Rules r){var p=f.Data;int low=Math.Min(p.level,other);double c=1+Math.Max(0,p.level-other)*r.LevelCorrection/100;return p.@base.Select((v,i)=>i==0?(int)(r.HPCorrection?Math.Floor((Math.Floor((v*2+p.iv[i])*low/100.0)+10+low)*c):Math.Floor((v*2+p.iv[i])*p.level/100.0+10+p.level)):(int)Math.Floor((Math.Floor((v*2+p.iv[i])*low/100.0)+5)*c)).ToArray();}
  public static double Stage(int n){return n>=0?(2+n)/2.0:2.0/(2-n);}
  public static int Effective(Fighter a,Fighter b,int i,Rules r,int role=0){int s=a.Stages[i];double m=a.Multipliers[i];if(r.Critical&&role==1){s=Math.Max(0,s);m=Math.Max(1,m);}if(r.Critical&&role==2){s=Math.Min(0,s);m=Math.Min(1,m);}return Math.Max(1,(int)Math.Floor(Stats(a,b.Data.level,r)[i]*m*Stage(s)*Mechanics.StatFactor(a,b,i,r)));}
  public static double Match(Database db,string[] at,string[] dt){return at.Length==0?1:at.Max(t=>dt.Aggregate(1.0,(n,d)=>n*(db.chart.ContainsKey(d)&&db.chart[d].ContainsKey(t)?db.chart[d][t]:1)));}
  public static Damage Calculate(Database db,Fighter a,Fighter b,Move m,Rules r){
   int ai=Math.Max(1,Array.IndexOf(Names,m.attack??(m.category=="물리"?"공격":"특공"))),di=Math.Max(1,Array.IndexOf(Names,m.defense??(m.category=="물리"?"방어":"특방")));
   double power=Math.Max(0,Math.Floor(m.power+r.PowerBonus)*r.PowerMultiplier);
   power*=Mechanics.PowerFactor(a,b,m,r);
   double stab=r.Stab>0?r.Stab:m.types.Any(t=>a.Data.types.Contains(t))?1.5:1,type=Mechanics.TypeEffect(db,a,b,m,r);
   string reason=Mechanics.Block(db,a,b,m,r);if(reason.Length>0)type=0;
   double basis=Math.Floor(((2*a.Data.level+10)/250.0*Effective(a,b,ai,r,1)/Effective(b,a,di,r,2)*power+2)*stab*type*(r.Critical?2:1)*r.DamageMultiplier*Mechanics.DamageFactor(a,b,m,r));
   bool supported=m.category!="변화"&&m.power>0;
   int[] rolls=Enumerable.Range(85,16).Select(v=>supported?(int)Math.Min(int.MaxValue,Math.Floor(basis*v/100)):0).ToArray();
   return new Damage{Min=rolls[0],Max=rolls[15],Rolls=rolls,Stab=stab,Type=type,Supported=supported,Reason=reason};
  }
  public static TurnResult Resolve(Database db,Fighter[] input,Move[] moves,Rules original,Func<double> rng){
   var result=new TurnResult{Fighters=input.Select(p=>p.Copy()).ToArray(),Rules=original.Copy()};var f=result.Fighters;var r=result.Rules;
   int delta=Mechanics.Priority(f[0],moves[0]).CompareTo(Mechanics.Priority(f[1],moves[1]));if(delta==0){delta=Effective(f[0],f[1],5,r).CompareTo(Effective(f[1],f[0],5,r));if(r.TrickRoom>0)delta=-delta;}int first=delta>0?0:delta<0?1:rng()<.5?0:1;
   foreach(int i in new[]{first,1-first}){var a=f[i];var b=f[1-i];var m=moves[i];if(a.HP<=0)continue;
    if(!Mechanics.BeforeMove(a,b,m,r,rng,result.Log))continue;
    if(m.name!="방어"&&m.name!="판별")a.Conditions.ProtectChain=0;
    bool target=Mechanics.TargetsOpponent(m);string reason=target?Mechanics.Block(db,a,b,m,r):"";
    if(reason.Length>0){result.Log.Add(a.Data.name+"의 "+m.name+" — "+reason);Mechanics.Absorb(a,b,reason,r,result.Log);continue;}
    if(target&&b.Conditions.Substitute>0&&m.category=="변화"&&!Mechanics.BypassSub(a,m)){result.Log.Add("대타가 "+m.name+"을 막았다.");continue;}
    if(rng()*100>=Mechanics.Accuracy(a,b,m,r)){result.Log.Add(a.Data.name+"의 "+m.name+"! 그러나 빗나갔다!");continue;}
    if(m.category=="변화"){Mechanics.StatusMove(a,b,m,r,rng,result.Log);continue;}
    var d=Calculate(db,a,b,m,r);if(!d.Supported){result.Log.Add(m.name+": 가변 위력 — 수동 판정.");continue;}
    int damage=d.Rolls[Math.Min(15,(int)(rng()*16))];bool sub=b.Conditions.Substitute>0&&!Mechanics.BypassSub(a,m);
    if(sub){b.Conditions.Substitute=Math.Max(0,b.Conditions.Substitute-damage);result.Log.Add(m.name+": 대타에 "+damage+" 대미지. 남은 대타 HP "+b.Conditions.Substitute);}
    else{b.HP=Math.Max(0,b.HP-damage);result.Log.Add(a.Data.name+"의 "+m.name+"! "+b.Data.name+"에게 "+damage+" 대미지."+(r.Critical?" 급소!":"")+(d.Type>1?" 효과가 굉장했다!":d.Type<1?" 효과가 별로다.":""));if(b.HP==0)result.Log.Add(b.Data.name+"은 쓰러졌다!");}
    if(damage>0)Mechanics.AfterHit(a,b,m,r,rng,result.Log,sub);
   }
   Mechanics.EndTurn(f,r,result.Log);return result;
  }
  public static string Test(Database db){
   var a=new Fighter(db.pokemon.First(p=>p.name=="선데이"));var b=new Fighter(db.pokemon.First(p=>p.name=="잭 한마"));var m=db.moves.First(v=>v.name=="파괴광선");var r=new Rules();b.Stages[4]=1;
   Check(Stats(a,b.Data.level,r)[0]==598,"HP");Check(Effective(a,b,1,r)==269,"level correction");var d=Calculate(db,a,b,m,r);Check(d.Min==290&&d.Max==342&&d.Rolls[2]==297,"spreadsheet damage");
   Check(Match(db,new[]{"노말"},new[]{"고스트"})==0,"immunity");Check(Match(db,new[]{"물"},new[]{"불꽃","땅","바위"})==8,"triple types");
   var ko=b.Copy();ko.HP=1;var fast=new Move{name=m.name,category=m.category,types=m.types,power=m.power,accuracy=100,priority=7,attack=m.attack,defense=m.defense};var t=Resolve(db,new[]{a,ko},new[]{fast,m},r,()=>0);Check(t.Fighters[1].HP==0&&t.Fighters[0].HP==a.HP&&ko.HP==1,"KO order and input isolation");
   var burned=a.Copy();burned.Status="동상";Check(Calculate(db,burned,b,m,r).Max<d.Max,"frostbite");var debuff=a.Copy();debuff.Stages[3]=-6;r.Critical=true;Check(Calculate(db,debuff,b,m,r).Max==Calculate(db,a,b,m,r).Max,"critical ranks");
   return "PASS: HP, level correction, spreadsheet 290-342 / .87=297, immunity, triple types, KO order, isolated inputs, frostbite, critical ranks.";
  }
  static void Check(bool ok,string label){if(!ok)throw new Exception("FAIL: "+label);}
 }
}
