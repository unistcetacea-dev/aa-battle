using System;
using System.Linq;
using System.Collections.Generic;
using NUnit.Framework;

namespace AABattle {
 [TestFixture] public class BatonPursuitTests {
  static int ids;
  static Fighter F(string ability="") {
   return new Fighter(new Pokemon { id="baton"+ ++ids, name="검사"+ids, ability=ability, item="", level=100,
    types=new[]{"노말"}, @base=new[]{300,100,100,100,100,100}, iv=new[]{31,31,31,31,31,31},
    moves=new[]{"몸통박치기","유턴"}, potentials=new Potential[0] });
  }
  static Move Idle() { return new Move { name="대기", category="변화", target="자신", types=new[]{"노말"}, accuracy=100, tags=new string[0] }; }
  static Move Hit(bool pursuit=true, bool pivot=false) {
   return new Move { name="검사공격", category="물리", target="상대", types=new[]{"노말"}, power=40, accuracy=100,
    attack="공격", defense="방어", priority=-7, tags=pursuit?(pivot?new[]{"추격","유턴"}:new[]{"추격"}):new string[0] };
  }
  static Move Baton() { return new Move { name="검사인계", category="변화", target="자신", types=new[]{"노말"}, accuracy=100,
    tags=new[]{"유턴"}, effect="「유턴」 기술. 자신의 「능력변화」「상태변화」를 인계한다." }; }
  static void Rule(Fighter f,string description,string trigger="X") {
   f.Data.potentials=f.Data.potentials.Concat(new[]{new Potential {id="종족 ①",name="검사효과",description=description,triggers=new[]{trigger}}}).ToArray();
  }
  static TurnResult Run(Fighter a,Fighter b,BattleCommand ca,BattleCommand cb) {
   return Engine.Resolve(TestData.Db,new[]{a,b},new[]{ca,cb},new Rules(),new[]{new TrainerBattleState(),new TrainerBattleState()},()=>.5,q=>q.Candidates.First());
  }
  [Test] public void BatonTransfersAllowedValuesAndKeepsIncomingIdentity() {
   var a=F("클리어바디"); var b=F(); var bench=F("불면");
   a.Status="독"; a.Stages[1]=2; a.AccuracyStage=1; a.EvasionStage=-1; a.CriticalStage=2;
   a.Conditions.Charged=true; a.Conditions.Heated=true; a.Conditions.Chilled=true;
   a.Conditions.Bomb=true; a.Conditions.BombSetTurn=1; a.Conditions.Intercept=3; a.Conditions.Substitute=40;
   a.Conditions.Confusion=3; a.Conditions.FocusEnergy=true;
   var t=Run(a,b,new BattleCommand{Move=Baton(),Party=new[]{bench}},new BattleCommand{Move=Idle()});
   var next=t.Fighters[0];
   Assert.That(next.Data.id,Is.EqualTo(bench.Data.id),string.Join("\n",t.Log));
   Assert.That(next.Stages[1],Is.EqualTo(2)); Assert.That(next.AccuracyStage,Is.EqualTo(1)); Assert.That(next.CriticalStage,Is.EqualTo(2));
   Assert.That(next.Conditions.Charged && next.Conditions.Heated && next.Conditions.Chilled && next.Conditions.Bomb,Is.True);
   Assert.That(next.Conditions.Intercept,Is.EqualTo(2)); Assert.That(next.Conditions.Substitute,Is.EqualTo(40));
   Assert.That(next.Status,Is.EqualTo("정상")); Assert.That(AbilityRuntime.Name(next),Is.EqualTo("불면"));
   Assert.That(t.SwitchedOut[0].Stages[1],Is.Zero); Assert.That(t.SwitchedOut[0].Conditions.Charged,Is.False);
  }
  [TestCase("")] [TestCase("돌아와！")]
  public void PursuitHitsOutgoingBeforeNormalAndReturnSwitch(string form) {
   var a=F(); var b=F(); var bench=F(); var move=Hit();
   var doubled=move.Copy(); doubled.power*=2;
   int expected=Engine.Calculate(TestData.Db,a,b,doubled,new Rules()).Rolls[8];
   var t=Run(a,b,new BattleCommand{Move=move},new BattleCommand{SwitchIn=bench,Form=form});
   Assert.That(t.SwitchedOut[1].HP,Is.EqualTo(b.HP-expected),string.Join("\n",t.Log));
   Assert.That(t.Fighters[1].HP,Is.EqualTo(bench.HP));
   Assert.That(t.Log.Count(l=>l.Contains(a.Data.name+"의 검사공격!")),Is.EqualTo(1));
  }
  [TestCase(false)] [TestCase(true)]
  public void PursuitPrecedesPivotOrBatonAndSuppressesTrainerPreInput(bool baton) {
   var a=F(); var b=F(); var bench=F(); var pivot=baton?Baton():TestData.Db.moves.First(m=>m.name=="유턴").Copy();
   pivot.priority=7;
   var t=Run(a,b,new BattleCommand{Move=Hit()},new BattleCommand{Move=pivot,Party=new[]{bench},Form="물러나！"});
   int chase=t.Log.FindIndex(l=>l.Contains(a.Data.name+"의 검사공격!"));
   int target=t.Log.FindIndex(l=>l.Contains(b.Data.name+"의 "+pivot.name+"!")||l.Contains(b.Data.name+": 자신의")||l.Contains(b.Data.name+": 인계"));
   Assert.That(chase,Is.GreaterThanOrEqualTo(0),string.Join("\n",t.Log));
   if(!baton)Assert.That(chase,Is.LessThan(target),string.Join("\n",t.Log));
   Assert.That(t.Trainers[1].UsedForms,Does.Not.Contain("물러나！"));
   Assert.That(t.Fighters[1].Data.id,Is.EqualTo(bench.Data.id),string.Join("\n",t.Log));
  }
  [TestCase("도주")] [TestCase("신출귀몰")]
  public void PursuitEffectImmunityAllowsSwitchWithoutDoubling(string ability) {
   var a=F(); var b=F(ability); var bench=F(); var move=Hit();
   var t=Run(a,b,new BattleCommand{Move=move},new BattleCommand{SwitchIn=bench});
   int amount=Engine.Calculate(TestData.Db,a,bench,move,new Rules()).Rolls[8];
   Assert.That(t.SwitchedOut[1].HP,Is.EqualTo(b.HP));
   Assert.That(t.Fighters[1].HP,Is.EqualTo(bench.HP-amount),string.Join("\n",t.Log));
  }
  [TestCase("상대의 「추격」효과를 받지 않는다.")]
  [TestCase("상대의 기술의 「추격」효과를 받지 않는다.")]
  [TestCase("상대로부터의 「추격」효과를 받지 않는다.")]
  public void CollectedEffectImmunityVariantsPreventInterception(string phrase) {
   var a=F();var b=F();var bench=F();Rule(b,phrase);
   var t=Run(a,b,new BattleCommand{Move=Hit()},new BattleCommand{SwitchIn=bench});
   Assert.That(t.SwitchedOut[1].HP,Is.EqualTo(b.HP),string.Join("\n",t.Log));
   Assert.That(t.Log.Any(l=>l.Contains("추격이 끼어")),Is.False);
  }
  [Test] public void PursuitMoveNullificationStillIntercepts() {
   var a=F(); var b=F(); var bench=F();
   Rule(b,"상대의 「추격」 기술을 무효화한다.");
   var t=Run(a,b,new BattleCommand{Move=Hit()},new BattleCommand{SwitchIn=bench});
   Assert.That(t.Fighters[1].HP,Is.EqualTo(bench.HP),string.Join("\n",t.Log));
   Assert.That(t.Log.Any(l=>l.Contains("추격") && l.Contains("끼어")),Is.True);
  }
  [Test] public void PursuitBindCancelsNormalSwitch() {
   var a=F(); var b=F(); var bench=F(); var move=Hit();
   move.tags=new[]{"추격","조이기"}; move.effect="4T 동안 상대를 「조이기」 상태로 한다.";
   var t=Run(a,b,new BattleCommand{Move=move},new BattleCommand{SwitchIn=bench});
   Assert.That(t.Fighters[1].Data.id,Is.EqualTo(b.Data.id),string.Join("\n",t.Log));
   Assert.That(t.Fighters[1].Conditions.Bound,Is.GreaterThan(0));
   Assert.That(t.Switched[1],Is.False);
  }
  [Test] public void PursuitPivotSwitchesBeforeOriginalSwitch() {
   var a=F(); var b=F(); var ba=F(); var bb=F();
   var t=Run(a,b,new BattleCommand{Move=Hit(true,true),Party=new[]{ba}},new BattleCommand{SwitchIn=bb});
   Assert.That(t.Switches.Select(s=>s.Side),Is.EqualTo(new[]{0,1}),string.Join("\n",t.Log));
  }
  [Test] public void ForcedTransferCarriesOnlyOpponentRanks() {
   var a=F(); var b=F(); var bench=F(); b.Stages[2]=2; b.Conditions.Charged=true;
   var force=new Move {name="인계강제",category="변화",target="상대",types=new[]{"노말"},accuracy=100,tags=new[]{"강제교대"},
    effect="「강제교대」 기술. 상대의 「능력변화」를 인계한다."};
   var t=Run(a,b,new BattleCommand{Move=force},new BattleCommand{Move=Idle(),Party=new[]{bench}});
   Assert.That(t.Fighters[1].Data.id,Is.EqualTo(bench.Data.id),string.Join("\n",t.Log));
   Assert.That(t.Fighters[1].Stages[2],Is.EqualTo(2)); Assert.That(t.Fighters[1].Conditions.Charged,Is.False);
  }
  [TestCase("")] [TestCase("돌아와！")]
  public void SupportAbilityPassesRanksThroughOrdinaryAndReturnPaths(string form) {
   var a=F("필수조역");var b=F();var bench=F();a.Stages[1]=3;a.Conditions.Charged=true;
   var t=Run(a,b,new BattleCommand{SwitchIn=bench,Form=form},new BattleCommand{Move=Idle()});
   Assert.That(t.Fighters[0].Stages[1],Is.EqualTo(3),string.Join("\n",t.Log));
   Assert.That(t.Fighters[0].Conditions.Charged,Is.False);
  }
  [TestCase("배턴터치")] [TestCase("물통돌리기")]
  public void ReferenceMoveUsesTransferExecution(string name) {
   var move=TestData.Db.moves.First(m=>m.name==name);
   var a=F();var b=F();var bench=F();a.Stages[1]=2;b.Stages[2]=3;
   var t=Run(a,b,new BattleCommand{Move=move,Party=new[]{bench}},new BattleCommand{Move=Idle(),Party=new[]{bench}});
   int side=name=="물통돌리기"?1:0,stat=side==0?1:2;
   Assert.That(t.Fighters[side].Data.id,Is.EqualTo(bench.Data.id),string.Join("\n",t.Log));
   Assert.That(t.Fighters[side].Stages[stat],Is.EqualTo(side==0?2:3));
  }
  [TestCase("수면")] [TestCase("빗나감")] [TestCase("빈사")]
  public void FailedPursuitUsesOnlyOneActionAndKnockoutPreventsSwitch(string cause) {
   var a=F();var b=F();var bench=F();var move=Hit();
   if(cause=="수면"){a.Status="잠듦";a.Conditions.SleepTurns=a.Conditions.SleepStart=1;}
   if(cause=="빗나감")move.accuracy=0;
   if(cause=="빈사")move.power=10000;
   var t=Run(a,b,new BattleCommand{Move=move},new BattleCommand{SwitchIn=bench});
   if(cause=="빈사"){Assert.That(t.Fighters[1].HP,Is.Zero);Assert.That(t.Switched[1],Is.False);}
   else {Assert.That(t.Fighters[1].Data.id,Is.EqualTo(bench.Data.id));Assert.That(t.Fighters[1].HP,Is.EqualTo(bench.HP));}
  }
  [TestCase(false)] [TestCase(true)]
  public void PivotGrantedByPokemonOrTrainerIsPursued(bool trainer) {
   var a=F();var b=F();var bench=F();var move=Hit(false);move.priority=7;
   var states=new[]{new TrainerBattleState(),new TrainerBattleState()};
   string grant="자신의 기술에 유턴 효과를 부여한다.";
   if(trainer)states[1].Trainer=new TrainerRecord {id="trainer-pivot",potentials=new List<PotentialRecord>{
    new PotentialRecord {name="유턴",slot="고유",enabled=true,trigger="기술을 내보낼 때",effect=grant}
   }};
   else Rule(b,grant,"기술을 내보낼 때");
   var t=Engine.Resolve(TestData.Db,new[]{a,b},new[]{new BattleCommand{Move=Hit()},new BattleCommand{Move=move,Party=new[]{bench}}},
    new Rules(),states,()=>.5,q=>q.Candidates.First());
   Assert.That(t.Log.FindIndex(l=>l.Contains(a.Data.name+"의 검사공격!")),Is.LessThan(t.Log.FindIndex(l=>l.Contains(b.Data.name+"의 검사공격!"))),string.Join("\n",t.Log));
   Assert.That(t.Fighters[1].Data.id,Is.EqualTo(bench.Data.id),string.Join("\n",t.Log));
  }
  [TestCase("T 종료 시")] [TestCase("자신의 「공격」이 올랐을 때")]
  public void EventSwitchDoesNotMakeChosenMoveAPivot(string trigger) {
   var a=F();var b=F();var bench=F();var move=Hit(false);move.priority=7;
   Rule(b,"아군과 임의교대할 수 있다.",trigger);
   var t=Run(a,b,new BattleCommand{Move=Hit()},new BattleCommand{Move=move,Party=new[]{bench}});
   Assert.That(t.Log.Any(l=>l.Contains("추격이 끼어")),Is.False);
   Assert.That(t.Log.FindIndex(l=>l.Contains(b.Data.name+"의 검사공격!")),Is.LessThan(t.Log.FindIndex(l=>l.Contains(a.Data.name+"의 검사공격!"))));
  }
  [Test] public void DefensivePivotImmunityDoesNotPredictASelectedPivot() {
   var a=F();var b=F();var move=Hit(false);move.priority=7;
   Rule(b,"상대의 「유턴」효과가 부여된 기술을 무효화한다.");
   var t=Run(a,b,new BattleCommand{Move=Hit()},new BattleCommand{Move=move,Form="피해라！"});
   Assert.That(t.Log.Any(l=>l.Contains("추격이 끼어")),Is.False,string.Join("\n",t.Log));
   Assert.That(t.Trainers[1].UsedForms,Does.Contain("피해라！"));
  }
  [Test] public void EchoTagsSoundAndRobberUsesJudgedPursuitTag() {
   var a=F("노상강도");var b=F();var bench=F();b.Data.item="먹다남은음식";
   Rule(a,"자신의 「소리」 기술에 「추격」 효과를 부여한다.");
   var move=Hit(false);move.tags=new[]{"소리"};
   var t=Run(a,b,new BattleCommand{Move=move},new BattleCommand{SwitchIn=bench});
   Assert.That(t.Fighters[0].Data.item,Is.EqualTo("먹다남은음식"),string.Join("\n",t.Log));
   Assert.That(t.SwitchedOut[1].Data.item,Is.Empty);
   Assert.That(move.tags,Is.EqualTo(new[]{"소리"}));
  }
  [TestCase(false)] [TestCase(true)]
  public void PursuitAdditionalAttackHonorsEffectImmunity(bool immune) {
   var a=F();var b=F(immune?"도주":"");var bench=F();a.Conditions.LastMove="몸통박치기";
   Rule(a,"「추격」 효과로 가장 먼저 같은 기술에 의한 추가공격을 한다.","상대가 교대할 때");
   var t=Run(a,b,new BattleCommand{Move=Idle()},new BattleCommand{SwitchIn=bench});
   Assert.That(t.Log.Any(l=>l.Contains("추가행동: 몸통박치기")),Is.EqualTo(!immune),string.Join("\n",t.Log));
  }
  [Test] public void FrontHeatTransfersAFirstFireBoostOnlyOnce() {
   var a=F("전열");var b=F();var bench=F();
   var first=Run(a,b,new BattleCommand{SwitchIn=bench},new BattleCommand{Move=Idle()});
   var user=first.Fighters[0];var fire=Hit(false);fire.types=new[]{"불꽃"};
   var r=first.Rules;
   Assert.That(PotentialModifiers.Power(user,first.Fighters[1],fire,r,1),Is.EqualTo(1.5));
   Mechanics.AfterMove(user,fire,user.Conditions.Copy(),new List<string>(),()=>.5);
   Assert.That(PotentialModifiers.Power(user,first.Fighters[1],fire,r,1),Is.EqualTo(1));
  }
  [TestCase(true)] [TestCase(false)]
  public void OptionalGenericTransferRequiresAcceptance(bool accept) {
   var a=F();var b=F();var bench=F();a.Stages[1]=2;
   Rule(a,"능력변화를 인계할 수 있다.","아군과 임의교대할 때");
   var rules=new Rules {OptionalEffectPrompt=(owner,other,p)=>accept};
   var t=Engine.Resolve(TestData.Db,new[]{a,b},new[]{new BattleCommand{SwitchIn=bench},new BattleCommand{Move=Idle()}},
    rules,new[]{new TrainerBattleState(),new TrainerBattleState()},()=>.5);
   Assert.That(t.Fighters[0].Stages[1],Is.EqualTo(accept?2:0),string.Join("\n",t.Log));
  }
  [TestCase(false)] [TestCase(true)]
  public void OrdinaryEntryTriggerSurvivesNormalSwitchRefactoring(bool pursued) {
   var a=F();var b=F();var bench=F();
   Rule(bench,"자신의 「공격」이 오른다.","통상교대로 장소에 나올 때");
   var t=Run(a,b,new BattleCommand{Move=pursued?Hit():Idle()},new BattleCommand{SwitchIn=bench});
   Assert.That(t.Fighters[1].Stages[1],Is.EqualTo(1),string.Join("\n",t.Log));
  }
  [Test] public void TransferExcludesTypeSpeciesAbilityItemAndNonCopyStates() {
   var a=F("클리어바디");var b=F();var bench=F("불면");
   a.Data.types=new[]{"물"};a.Data.@base[1]=180;a.Data.item="먹다남은음식";
   a.Conditions.Curled=true;a.Conditions.MeanLook=true;a.Conditions.Yawn=2;a.Conditions.Stockpile=2;
   a.Conditions.Tormented=true;
   var t=Run(a,b,new BattleCommand{Move=Baton(),Party=new[]{bench}},new BattleCommand{Move=Idle()});
   var next=t.Fighters[0];
   Assert.That(next.Data.types,Is.EqualTo(bench.Data.types));Assert.That(next.Data.@base,Is.EqualTo(bench.Data.@base));
   Assert.That(next.Data.item,Is.Empty);Assert.That(AbilityRuntime.Name(next),Is.EqualTo("불면"));
   Assert.That(next.Conditions.Curled || next.Conditions.MeanLook || next.Conditions.Tormented,Is.False);
   Assert.That(next.Conditions.Yawn,Is.Zero);Assert.That(next.Conditions.Stockpile,Is.Zero);
  }
  [TestCase(false)] [TestCase(true)]
  public void IncomingInheritsPerishAndMindReaderWithoutNewDuration(bool returned) {
   var a=F();var b=F();var bench=F();a.Conditions.DeathCount=3;a.Conditions.MindReaderSide=1;a.Conditions.MindReaderSetTurn=1;
   a.Conditions.HealBlock=3;a.Conditions.Embargo=3;a.Conditions.MagnetRise=3;
   // 상태 인계는 기술과 독립된 계속 효과로도 쓴다.
   Rule(a,"자신의 「상태변화」를 인계한다.");
   var t=Run(a,b,new BattleCommand{SwitchIn=bench,Form=returned?"돌아와！":""},new BattleCommand{Move=Idle()});
   Assert.That(t.Fighters[0].Conditions.DeathCount,Is.EqualTo(2));
   Assert.That(t.Fighters[0].Conditions.MindReaderSide,Is.EqualTo(1));
   Assert.That(t.Fighters[0].Conditions.HealBlock,Is.EqualTo(2));Assert.That(t.Fighters[0].Conditions.Embargo,Is.EqualTo(2));
  }
  [Test] public void BatonPassesBindingWithItsRemainingDurationAndDamageRatio() {
   var a=F();var b=F();var bench=F();a.Conditions.Bound=3;a.Conditions.BindDivisor=6;
   var t=Run(a,b,new BattleCommand{Move=Baton(),Party=new[]{bench}},new BattleCommand{Move=Idle()});
   Assert.That(t.Fighters[0].Conditions.Bound,Is.EqualTo(2),string.Join("\n",t.Log));
   Assert.That(t.Fighters[0].Conditions.BindDivisor,Is.EqualTo(6));
   Assert.That(t.Fighters[0].HP,Is.EqualTo(bench.HP-bench.HP/6));
  }
  [Test] public void EventGrantedPursuitUsesSameSelectedActionAndExecutionModule() {
   var a=F();var b=F();var bench=F();
   Rule(a,"자신의 기술에 「추격」 효과를 부여한다.","기술을 내보낼 때");
   var t=Run(a,b,new BattleCommand{Move=Hit(false)},new BattleCommand{SwitchIn=bench});
   Assert.That(t.SwitchedOut[1].HP,Is.LessThan(b.HP),string.Join("\n",t.Log));
   Assert.That(t.Fighters[1].HP,Is.EqualTo(bench.HP));
  }
  [Test] public void ForcedSwitchIsNotPredictedAsVoluntaryPursuit() {
   var a=F();var b=F();var bench=F();
   var force=new Move {name="강제",category="변화",target="상대",types=new[]{"노말"},accuracy=100,priority=7,
    tags=new[]{"강제교대"},effect="「강제교대」 기술."};
   var t=Run(a,b,new BattleCommand{Move=force},new BattleCommand{Move=Hit(),Party=new[]{bench}});
   Assert.That(t.Switches.Any(s=>s.Reason==SwitchReason.Forced),Is.True,string.Join("\n",t.Log));
   Assert.That(t.Log.Any(l=>l.Contains("추격이 끼어")),Is.False);
  }
  [Test] public void FaintedReplacementIsNotPursuitInterception() {
   var a=F();var b=F();var bench=F();b.HP=0;
   var t=Run(a,b,new BattleCommand{Move=Hit()},new BattleCommand{SwitchIn=bench});
   Assert.That(t.Log.Any(l=>l.Contains("추격이 끼어")),Is.False);
   Assert.That(t.Fighters[1].HP,Is.LessThan(bench.HP),"들어온 상대에게 통상 기술만 명중한다");
  }
 }
}
