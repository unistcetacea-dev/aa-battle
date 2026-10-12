using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Globalization;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace AABattle {
 // 도감·포텐셜 분리 회귀 검사
 public static class CatalogTests {
  static void Check(bool value,string message){if(!value)throw new Exception("Catalog test failed: "+message);}
  public static void Run(){
   AbilityCatalog.Test();AbilityRuntime.Test();string trigger,effect;PotentialLibrary.Split("자신의 기술의 위력을 강화(1.2배)한다.",out trigger,out effect);Check(trigger==""&&effect.StartsWith("자신의"),"do not split 자신의");
   PotentialLibrary.Split("선발로 필드에 나오면, 자신의 공격이 오른다.",out trigger,out effect);Check(trigger=="선발로 필드에 나오면"&&effect=="자신의 공격이 오른다.","entry condition");
   PotentialLibrary.Split("필드에 있는 한, 상대는 교대할 수 없다.",out trigger,out effect);Check(trigger=="필드에 있는 한"&&!effect.Contains("있는 한"),"continuous condition");
   PotentialLibrary.Split("「여기다！」일 때, 필드에 나오면 자신의 공격이 오른다.",out trigger,out effect);Check(trigger.Contains("여기다")&&trigger.Contains("필드에 나오면")&&effect=="자신의 공격이 오른다.","chained conditions");
   PotentialLibrary.Split("T종료시까지 자신의 공격이 오른다.",out trigger,out effect);Check(trigger==""&&effect.StartsWith("T종료시까지"),"duration remains effect");
   PotentialLibrary.Split("「가열」상태가 된 T 종료시, 아군과 교대한다.",out trigger,out effect);Check(trigger.EndsWith("종료시")&&effect=="아군과 교대한다.","compact turn timing");
   PotentialParts.Test();EffectStructures.Test();EffectImplementationDex.Test();var data=PotentialLibrary.Load();Check(data.Count>0,"embedded definitions");Check(data.Any(x=>x.record.name=="선의 선"&&x.record.trigger.Length>0),"research triggers");Check(data.All(x=>x.source.Length>0&&x.urls.Length>0&&x.record.raw.Length>0),"provenance and raw");
   var order=EditorTemplate.BaseOrders().First();Check(order.name=="물러나！"&&order.trigger.Length>0&&order.effect.Contains("한 번"),"user directive semantics");var explorer=EditorTemplate.Role("탐사대원");Check(explorer!=null&&explorer.template!=null&&explorer.template.trigger==TriggerStructures.EntryLabel&&explorer.template.effect==EffectNormalizer.CanonicalRank("자신의 임의의 능력",1),"structured explorer role template");var wild=EditorTemplate.Role("야생 포켓몬");var boss=EditorTemplate.Role("야생의 두목");Check(wild!=null&&boss!=null&&wild.category=="야생"&&boss.category=="야생"&&wild.template!=null&&boss.template!=null&&wild.template.trigger==TriggerStructures.EntryLabel&&wild.template.effect==EffectNormalizer.CanonicalRank("자신의 임의의 능력",2)&&boss.template.effect==EffectNormalizer.CanonicalRank("자신의 전능력치",2),"structured wild role templates");var hunter=EditorTemplate.Role("수렵대원");string ignoreTarget,ignoreStats,ignoreMode;Check(hunter!=null&&hunter.template!=null&&hunter.template.trigger==TriggerStructures.FieldLabel&&EffectStructures.TryStatIgnore(hunter.template.effect,out ignoreTarget,out ignoreStats,out ignoreMode)&&ignoreTarget=="『야생의 두목』"&&ignoreStats=="능력"&&ignoreMode=="상승(강화)","structured hunter stat-ignore template");var tracker=EditorTemplate.Role("추적대원");Check(tracker!=null&&tracker.template!=null&&tracker.template.trigger==TriggerStructures.FieldLabel&&EffectStructures.IsSwitchRestriction(tracker.template.effect)&&tracker.template.Text.Contains("장소에 있는 한")&&tracker.template.Text.Contains("통솔：A"),"structured tracker role template");var supply=EditorTemplate.Role("보급대원");var rescue=EditorTemplate.Role("구급대원");string rescueAdjunctTrigger,rescueAdjunctEffect;PotentialLibrary.Split(rescue==null?"":rescue.adjunctEffect,out rescueAdjunctTrigger,out rescueAdjunctEffect);Check(supply!=null&&supply.template!=null&&supply.template.trigger==TriggerStructures.LeaveLabel&&supply.effect.StartsWith(TriggerStructures.LeaveLabel+",")&&!supply.effect.Contains("필드를 떠날 때")&&rescue!=null&&rescue.template!=null&&rescue.template.trigger==TriggerStructures.LeaveLabel&&rescue.effect.StartsWith(TriggerStructures.LeaveLabel+",")&&rescueAdjunctTrigger==TriggerStructures.LeaveLabel&&!rescue.adjunctEffect.Contains("필드를 떠날 때"),"structured crew leave role templates");
   var researcher=EditorTemplate.Role("조사대원");Check(researcher!=null&&researcher.template!=null&&researcher.template.trigger==TriggerStructures.SelfAndOpponentEntryLabel&&researcher.template.effect==EffectNormalizer.CanonicalAnalysisPermission(),"structured researcher entry and analysis template");
   Check(EffectStructures.IsAnalysisPermission(EffectNormalizer.CanonicalAnalysisPermission()+" (필요 「통솔：A」 이상)"),"analysis effect structure");
   var arbitrary=EditorTemplate.Parse("『검사』… 필드를 떠날 때, 아군의 임의의 능력을 올린다.","역할");Check(arbitrary.effect.Contains("임의의 능력")&&arbitrary.activation=="","preserve optional target wording");
   Check(EditorTemplate.FixedOptions("특권").Length==21&&EditorTemplate.FixedOption("특권","계약의 특권").effect.Contains("○")&&EditorTemplate.FixedOption("특권","익스펜션（불꽃）").effect.Contains("\r\n"),"fixed privilege and expansion templates");string expansionTrigger,expansionEffects;PotentialLibrary.Split(EditorTemplate.FixedOption("특권","익스펜션（불꽃）").effect,out expansionTrigger,out expansionEffects);Check(expansionTrigger.Length==0&&expansionEffects.Split('\n').Length==2,"expansion trigger and multiple effects split");Check(StateDex.StatusNames("상태이상").Contains("화상")&&StateDex.StatusNames("상태변화").Contains("혼란")&&StateDex.StatusNames("상태변화").Contains("미래예지")&&StateDex.StatusNames("상태변화").Contains("번개구름")&&StateDex.StatusNames("진영 상태").Contains("흙놀이"),"status dex trigger choices");Check(TriggerStructures.Normalize("턴 중 선언 시")=="","ambiguous turn declaration excluded");
  }
 }
}
