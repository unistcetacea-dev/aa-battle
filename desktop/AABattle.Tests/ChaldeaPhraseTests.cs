using NUnit.Framework;

namespace AABattle.Tests
{
  // 피니스 칼데아 시트의 표기 변형이 기존 모듈 표준 문장으로 읽히는지 본다
  [TestFixture]
  public class ChaldeaPhraseTests
  {
    [TestCase("자신의 기술이 「필중」한다")]
    [TestCase("자신의 공격이 「필중」한다")]
    [TestCase("자신의 기술에 「추격」효과를 부여하고, 상대에게 「필중」시킨다")]
    [TestCase("자신의 「비행」기술의 위력을 강화(1.5배)해, 「추격」효과를 부여한다")]
    [TestCase("자신의 「물」기술이 상대의 「방어」등의 기술을 무시한다")]
    [TestCase("상대 포켓몬의 「방어」등의 기술, 특성, 상대 포켓몬과 상대 트레이너의 방호 포텐셜을 무효화한다")]
    [TestCase("상대의 공격 이외의 대미지로 쓰러지지 않는다")]
    [TestCase("자신의 「변화기술」의 우선도를 +1한다")]
    [TestCase("자신의 기술의 우선도를 +1한다")]
    [TestCase("T종료시까지 자신의 방어(특방)을 강화(1.5배)한다.")]
    [TestCase("T종료시까지 자신의 「공」을 배화한다")]
    [TestCase("상대로부터의 「상태이상」을 무효화한다")]
    [TestCase("자신의 소지품이 파괴(약탈)되지 않는다")]
    [TestCase("상대는 「통상교대」이외의 수단으로 교대할 수 없다")]
    [TestCase("자신의 「모으기기술」의 모으기를 생략하고 내보낼 수 있다")]
    public void EffectVariantsBecomeExecutableModules(string sentence)
    {
      var steps = PotentialQueue.JudgeSteps(sentence, false);
      Assert.That(steps, Is.Not.Empty);
      foreach (var step in steps)
        Assert.That(step.Ok, Is.True, step.Line);
    }

    [TestCase("자신의 기술이 「필중」한다", "자신의 기술은 필중이다.")]
    [TestCase("상대의 공격 이외의 대미지를 무시한다", "자신은 공격 이외의 대미지를 받지 않는다.")]
    [TestCase("드물게 기합으로 상대의 공격을 버틴다", "자신은 빈사가 되는 공격을 드물게 HP 1로 버틴다.")]
    [TestCase("드물게 기합으로 상대의 공격을 버틴다. 남은 체력이 많을수록 발동하기 어렵다", "자신은 빈사가 되는 공격을 드물게 HP 1로 버틴다 (남은 HP 비례).")]
    public void EffectVariantsUseTheStandardSentence(string raw, string expected)
    {
      Assert.That(EffectNormalizer.Normalize(raw), Is.EqualTo(expected));
    }

    [TestCase("자신의 공격 명중시에", "자신의 공격이 명중했을 때")]
    [TestCase("자신의 기술이 명중 했을 때", "자신의 기술이 명중했을 때")]
    [TestCase("아군과 교대로 장소에 나올 때", "임의교대로 장소에 나올 때")]
    [TestCase("상대를 빈사로 했을때", "상대를 쓰러뜨렸을 때")]
    [TestCase("「죽어내밀기」로 상대가 나왔을 때", "상대가 죽어내밀기로 장소에 나왔을 때")]
    [TestCase("자신이 나무열매를 소모했을 때", "자신이 「나무열매」를 사용했을 때")]
    [TestCase("트레이너가 「유키나리」일 때", "「유키나리」가 트레이너일 때")]
    [TestCase("상대보다 속도가 높을 때", "자신의 「속도」가 상대보다 높을 때")]
    public void TriggerVariantsUseTheStandardSentence(string raw, string expected)
    {
      Assert.That(TriggerModules.Normalize(raw), Is.EqualTo(expected));
    }

    [Test]
    public void GenderTriggerIsNotTakenForATrainerName()
    {
      Assert.That(PhrasePipeline.TriggerNormalize("트레이너가 「남성」일 때"), Is.Null);
    }
  }
}
