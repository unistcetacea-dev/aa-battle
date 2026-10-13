using System;
using System.Linq;
using System.IO;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using NUnit.Framework;

namespace AABattle {
 [TestFixture]
 public class EngineFileSplitTests {
  static int ids;
  static Fighter F(string ability = "") {
   return new Fighter(new Pokemon {
    id = "split" + ++ids, name = "검사" + ids, ability = ability, item = "", level = 100,
    types = new[] { "노말" }, @base = new[] { 300, 100, 100, 100, 100, 100 },
    iv = new[] { 31, 31, 31, 31, 31, 31 }, moves = new[] { "몸통박치기" }, potentials = new Potential[0]
   });
  }
  static Move Idle() {
   return new Move { name = "대기", category = "변화", target = "자신", types = new[] { "노말" }, accuracy = 100, tags = new string[0] };
  }
  [Test]
  public void ForcedIncomingDoesNotUseOutgoingChosenMove() {
   var a = F(); var b = F(); var bench = F();
   var force = new Move { name = "강제검사", category = "변화", target = "상대", types = new[] { "노말" },
    accuracy = 100, priority = 7, tags = new[] { "강제교대" }, effect = "「강제교대」 기술." };
   var hit = TestData.Db.moves.First(m => m.name == "몸통박치기");
   var turn = Engine.Resolve(TestData.Db, new[] { a, b }, new[] {
    new BattleCommand { Move = force }, new BattleCommand { Move = hit, Party = new[] { bench } }
   }, new Rules(), new[] { new TrainerBattleState(), new TrainerBattleState() }, () => .5, q => q.Candidates.First());
   Assert.That(turn.Fighters[1].Data.id, Is.EqualTo(bench.Data.id), string.Join("\n", turn.Log));
   Assert.That(turn.Fighters[0].HP, Is.EqualTo(a.HP), string.Join("\n", turn.Log));
   Assert.That(turn.Log.Any(l => l.Contains(bench.Data.name + "의 몸통박치기!")), Is.False);
  }
  [TestCase(false, true)] [TestCase(true, true)]
  [TestCase(false, false)] [TestCase(true, false)]
  public void PuppetTransfersOutgoingOrIncomingRanksOnlyWhenAccepted(bool entering, bool accept) {
   var a = F(entering ? "" : "인형사"); var b = F(); var bench = F(entering ? "인형사" : "");
   bench.OnField = false; a.Stages[1] = 3; a.AccuracyStage = 2; a.Conditions.Charged = true;
   int prompts = 0;
   var rules = new Rules { OptionalEffectPrompt = (owner, other, potential) => {
    Assert.That(owner.Data.id, Is.EqualTo(entering ? bench.Data.id : a.Data.id)); prompts++; return accept;
   } };
   var turn = Engine.Resolve(TestData.Db, new[] { a, b }, new[] {
    new BattleCommand { SwitchIn = bench }, new BattleCommand { Move = Idle() }
   }, rules, new[] { new TrainerBattleState(), new TrainerBattleState() }, () => .5, q => q.Candidates.First());
   Assert.That(prompts, Is.EqualTo(1), string.Join("\n", turn.Log));
   Assert.That(turn.Fighters[0].Stages[1], Is.EqualTo(accept ? 3 : 0));
   Assert.That(turn.Fighters[0].AccuracyStage, Is.EqualTo(accept ? 2 : 0));
   Assert.That(turn.Fighters[0].Conditions.Charged, Is.False);
   Assert.That(bench.Stages[1], Is.Zero);
  }
  [TestCase(SwitchReason.Forced)] [TestCase(SwitchReason.Fainted)]
  public void PuppetDoesNotTransferForNonVoluntarySwitch(SwitchReason reason) {
   var a = F("인형사"); var bench = F("인형사"); a.Stages[1] = 3;
   bool prompted = false;
   var rules = new Rules { OptionalEffectPrompt = (owner, other, potential) => { prompted = true; return true; } };
   var snapshot = BatonModules.Capture(a, F(), rules, reason, bench);
   Assert.That(snapshot, Is.Null);
   Assert.That(prompted, Is.False);
  }
  [TestCase(SwitchReason.Normal)] [TestCase(SwitchReason.Return)]
  [TestCase(SwitchReason.Pivot)] [TestCase(SwitchReason.Potential)]
  public void GenericBilateralModuleUsesExistingVoluntarySwitchReasons(SwitchReason reason) {
   var a = F(); var bench = F(); bench.OnField = false; a.Stages[2] = -2;
   bench.Data.potentials = new[] { new Potential { id = "종족 ①", name = "인계",
    description = "자신이 나가거나 들어오는 임의교대의 「능력변화」를 인계할 수 있다.", triggers = new[] { TriggerStructures.AlwaysLabel } } };
   var snapshot = BatonModules.Capture(a, F(), new Rules { OptionalEffectPrompt = (x, y, p) => true }, reason, bench);
   Assert.That(snapshot, Is.Not.Null);
   BatonModules.Apply(snapshot, bench, null);
   Assert.That(bench.Stages[2], Is.EqualTo(-2));
  }
  [Test]
  public void IncomingPuppetIsSuppressedByGasWithoutChangingBench() {
   var a = F(); var bench = F("인형사"); bench.OnField = false; a.Stages[1] = 3;
   bool prompted = false;
   var snapshot = BatonModules.Capture(a, F("화학변화가스"), new Rules {
    OptionalEffectPrompt = (x, y, p) => { prompted = true; return true; }
   }, SwitchReason.Normal, bench);
   Assert.That(snapshot, Is.Null);
   Assert.That(prompted, Is.False);
   Assert.That(bench.AbilitySuppressed, Is.False);
   Assert.That(bench.OnField, Is.False);
  }
  [TestCase(false, "")] [TestCase(true, "")]
  [TestCase(false, "돌아와！")] [TestCase(true, "돌아와！")]
  public void AcceptedPuppetRaisesAbilityActivationForItsActualOwner(bool entering, string form) {
   var a = F(entering ? "" : "인형사"); var b = F(); var bench = F(entering ? "인형사" : "");
   bench.OnField = false; a.Stages[1] = 2;
   var owner = entering ? bench : a;
   owner.Data.potentials = new[] { new Potential { id = "종족 ①", name = "인형사 반응",
    description = "자신의 「방어」가 오른다.", triggers = new[] { "자신의 「인형사」 특성이 발동했을 때" } } };
   var turn = Engine.Resolve(TestData.Db, new[] { a, b }, new[] {
    new BattleCommand { SwitchIn = bench, Form = form }, new BattleCommand { Move = Idle() }
   }, new Rules { OptionalEffectPrompt = (x, y, p) => true },
    new[] { new TrainerBattleState(), new TrainerBattleState() }, () => .5, q => q.Candidates.First());
   Assert.That(turn.Fighters[0].Stages[1], Is.EqualTo(2), string.Join("\n", turn.Log));
   Assert.That(turn.Fighters[0].Stages[2], Is.EqualTo(1), string.Join("\n", turn.Log));
  }
  [TestCase(true)] [TestCase(false)] [Category("UI")]
  public void IncomingPuppetPromptOffersInheritanceWithoutOpeningAnalysis(bool accept) {
   var incoming = F("인형사"); incoming.OnField = false;
   var source = AbilityModules.ToPotentials(AbilityCatalog.Find("인형사")).Single();
   bool shown = false;
   using (var form = new MainForm(TestData.Db))
   using (var timer = new System.Windows.Forms.Timer { Interval = 30 }) {
    timer.Tick += (sender, args) => {
     var dialog = Application.OpenForms.Cast<Form>().FirstOrDefault(x => x.Text == "선택 발동");
     if (dialog == null) return;
     shown = true; timer.Stop();
     if (accept) {
      string path = Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "../../../../outputs/candidate/puppet-choice.png"));
      Directory.CreateDirectory(Path.GetDirectoryName(path));
      using (var bitmap = new Bitmap(dialog.Width, dialog.Height)) {
       dialog.DrawToBitmap(bitmap, new Rectangle(0, 0, bitmap.Width, bitmap.Height));
       bitmap.Save(path);
      }
     }
     dialog.DialogResult = accept ? DialogResult.Yes : DialogResult.No;
    };
    timer.Start();
    bool selected = (bool)typeof(MainForm).GetMethod("ConfirmOptionalEffect", BindingFlags.Instance | BindingFlags.NonPublic)
     .Invoke(form, new object[] { incoming, F(), source });
    Assert.That(shown, Is.True);
    Assert.That(selected, Is.EqualTo(accept));
    Assert.That(Application.OpenForms.Cast<Form>().Any(x => x is AnalysisForm), Is.False);
   }
  }
 }
}
