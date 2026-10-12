using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Reflection;
using System.Web.Script.Serialization;

namespace AABattle {
 // 포켓몬·기술·내장 데이터와 전투 중 포켓몬(Fighter)
 public class Potential { public string id, name, description, activation, uses; public string[] triggers,unlocks; }
 public class Pokemon {
  public string id, team, name, ability, item;
  public bool ain;   // 아인종(「아인」을 쓰러뜨렸을 때 등)
  public int level; public string[] types, moves; public int[] @base, iv;
  public Potential[] potentials;
 }
 public class Move {
  public string name, category, effect, attack, defense, target; public string[] types, tags;
  public double power, accuracy; public int priority;
  public bool extension;   // 기능확장으로 낸 기술(배틀 중 사본에만 켠다)
  public Move Copy(){var copy=(Move)MemberwiseClone();copy.types=types==null?null:(string[])types.Clone();copy.tags=tags==null?null:(string[])tags.Clone();return copy;}
 }
 public class Database {
  public Pokemon[] pokemon; public Move[] moves; public string[] types;
  public Dictionary<string,Dictionary<string,double>> chart;
  public static Database Load() { var json=new JavaScriptSerializer {MaxJsonLength=10000000};var db=json.Deserialize<Database>(json.Serialize(Normalize(json.DeserializeObject(Resource.Text("data.json")))));MoveCatalog.Apply(db);MoveCatalog.Test(db);return db; }
  static object Normalize(object v){var d=v as Dictionary<string,object>;if(d!=null)return d.ToDictionary(p=>p.Key,p=>Normalize(p.Value));var a=v as object[];if(a!=null)return a.Select(Normalize).ToArray();if(v is decimal){decimal n=(decimal)v;if(n==decimal.Truncate(n)&&n>=int.MinValue&&n<=int.MaxValue)return (int)n;}return v;}
 }
 public static class Resource {
  public static byte[] Bytes(string name) { using(var s=Assembly.GetExecutingAssembly().GetManifestResourceStream(name)) { if(s==null)throw new Exception("Missing resource: "+name);using(var m=new MemoryStream()){s.CopyTo(m);return m.ToArray();} } }
  public static string Text(string name){return System.Text.Encoding.UTF8.GetString(Bytes(name));}
 }
 public class Fighter {
  internal int FieldSide=-1;internal Fighter FieldOpponent;
  public Pokemon Data; public string AA="", Status="정상",OriginalAbility="",CurrentAbility=""; public bool AbilityChanged,AbilitySuppressed,OnField=true;public int HP; public int[] Stages=new int[6]; public int AccuracyStage,EvasionStage,CriticalStage;public int TrainerAccuracyStage,TrainerCriticalStage; public double AccuracyMultiplier=1,EvasionMultiplier=1; public double[] Multipliers={1,1,1,1,1,1};
  public string[] Moves; public int Selected; public ConditionState Conditions=new ConditionState();
  public Fighter(Pokemon p){Data=ClonePokemon(p);Moves=(p.moves??new string[0]).Take(4).ToArray();OriginalAbility=CurrentAbility=p.ability??"";HP=Engine.Stats(this,p.level,new Rules())[0];}
  public Fighter Copy(){var f=new Fighter(Data);f.FieldSide=FieldSide;f.AA=AA;f.Status=Status;f.OriginalAbility=OriginalAbility;f.CurrentAbility=CurrentAbility;f.AbilityChanged=AbilityChanged;f.AbilitySuppressed=AbilitySuppressed;f.OnField=OnField;f.HP=HP;f.Stages=(int[])Stages.Clone();f.AccuracyStage=AccuracyStage;f.EvasionStage=EvasionStage;f.CriticalStage=CriticalStage;f.TrainerAccuracyStage=TrainerAccuracyStage;f.TrainerCriticalStage=TrainerCriticalStage;f.AccuracyMultiplier=AccuracyMultiplier;f.EvasionMultiplier=EvasionMultiplier;f.Multipliers=(double[])Multipliers.Clone();f.Moves=(string[])Moves.Clone();f.Selected=Selected;f.Conditions=Conditions.Copy();return f;}
  static Pokemon ClonePokemon(Pokemon p){return new Pokemon{id=p.id,ain=p.ain,name=p.name,team=p.team,ability=p.ability,item=p.item,level=p.level,types=(string[])p.types.Clone(),@base=(int[])p.@base.Clone(),iv=(int[])p.iv.Clone(),moves=(string[])p.moves.Clone(),potentials=p.potentials};}
 }
}
