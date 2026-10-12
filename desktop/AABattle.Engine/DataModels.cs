using System;
using System.Linq;
using System.Collections.Generic;

namespace AABattle {
 // 저장 파일(.aabdata.json)과 연구 자료의 데이터 모델. 엔진과 편집기가 함께 쓴다
 public class EditorProject {public List<TeamRecord> teams=new List<TeamRecord>();public string format=ProjectStore.Format;public List<TrainerRecord> trainers=new List<TrainerRecord>();public List<PokemonRecord> pokemon=new List<PokemonRecord>();}
 public class AptitudeRecord {public string rank="",description="";}
 public class StatsRecord {public int hp,attack,defense,spAttack,spDefense,speed;public int Total(){return hp+attack+defense+spAttack+spDefense+speed;}}
 public class PotentialRecord {public bool enabled=true;public string slot="",targetType="",name="",activation="",uses="",triggerSubject="",effectSubject="",trigger="",effect="",raw="";public string[] unlocks=new string[0];}
 public class TrainerRecord {public string id="",name="",origin="",asciiArt="";public AptitudeRecord command=new AptitudeRecord(),training=new AptitudeRecord(),leadership=new AptitudeRecord(),ability=new AptitudeRecord();public List<PotentialRecord> potentials=new List<PotentialRecord>();}
 public class PokemonRecord {public string id="",name="",ability="",model="",item="",asciiArt="";public bool ain;public int level=100;public List<string> types=new List<string>(),moves=new List<string>();public StatsRecord stats=new StatsRecord();public List<PotentialRecord> potentials=new List<PotentialRecord>();}
 // trainerId·memberIds가 저장되는 참조다. trainer·members는 불러온 뒤 ProjectStore가 연결하는 원본 객체(옛 파일에서는 사본)
 public class TeamRecord {public string name="",trainerId="";public List<string> memberIds=new List<string>();public TrainerRecord trainer;public List<PokemonRecord> members=new List<PokemonRecord>();public FieldEffectRecord fieldEffect=new FieldEffectRecord();public override string ToString(){return (name.Length>0?name:"이름 없는 팀")+"  ·  "+members.Count+"체"+(fieldEffect!=null&&fieldEffect.enabled&&fieldEffect.name.Length>0?"  ·  《"+fieldEffect.name+"》":"");}}
 public class ResearchClause { public string text; }
 public class ResearchSource { public string source_page,source_url,character; public int line; }
 public class ResearchUses { public int count;public string per; }
 public class ResearchActivation { public string mode,probability,confidence; public ResearchUses uses; }
 public class ResearchCatalog { public ResearchPotential[] items; }
 public class ResearchPotential {
  public string id,name,category,raw; public ResearchClause[] triggers,effects;
  public ResearchActivation activation; public string[] needs_review; public ResearchSource[] sources;
 }
 public class DexPotential {
  public PotentialRecord record; public string source,review; public string[] urls;
  public string Search { get { return record.name+" "+record.slot+" "+record.trigger+" "+record.effect+" "+record.raw+" "+source; } }
 }
 public class TurnSelection {public string Form="",Order="";public int SwitchIndex=-1;public List<string> PreInputs=new List<string>();public TurnSelection Copy(){return new TurnSelection{Form=Form,Order=Order,SwitchIndex=SwitchIndex,PreInputs=new List<string>(PreInputs??new List<string>())};}}
}
