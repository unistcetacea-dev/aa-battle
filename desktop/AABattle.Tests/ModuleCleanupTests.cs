using System;
using System.Linq;
using System.Collections.Generic;
using NUnit.Framework;

namespace AABattle
{
  // 정규화 규칙·모듈 목록의 구조 검사: 재정규화 불변, 중복 모듈 없음
  [TestFixture]
  public class ModuleCleanupTests
  {
    // 표준 문장 출력은 끝 마침표까지 엄격히 비교한다.
    static string Bare(string text)
    {
      return (text ?? "").TrimEnd('.', '。', '．').Trim();
    }

    // 기본값 문장이 표준 문장과 다른 모듈(사유와 함께). 정규화가 기본값을 고쳐 쓰는 경우다
    static readonly Dictionary<string, string> DefaultDrift = new Dictionary<string, string>
    {
      { "trigger.entry.both", "기본값이 두 줄 문장이라 한 줄로 합쳐진다" },
      { "trigger.label.29", "옛 표기(나무열매를 소모했을 때)를 표준 표기로 바꾼다" }
    };

    public static IEnumerable<TestCaseData> Defaults()
    {
      foreach (var module in ModuleCatalog.All)
        yield return new TestCaseData(module).SetName("ModuleDefault_" + module.Id);
    }

    [TestCaseSource("Defaults")]
    public void ModuleDefaultSentenceIsStableUnderNormalize(ModuleDefinition module)
    {
      string text = module.Fill();
      bool effect = module.Kind == ModuleKind.Effect;
      Func<string, string> normalize = effect ? (Func<string, string>)EffectNormalizer.Normalize : TriggerStructures.Normalize;
      string once = normalize(text);
      Assert.That(normalize(once), Is.EqualTo(once), "재정규화");
      if (!DefaultDrift.ContainsKey(module.Id))
        Assert.That(once, Is.EqualTo(text), "기본값이 표준 문장");
    }

    [Test]
    public void DefaultDriftExceptionsAreStillNeeded()
    {
      foreach (string id in DefaultDrift.Keys)
      {
        var module = ModuleCatalog.All.First(x => x.Id == id);
        string text = module.Fill();
        string once = module.Kind == ModuleKind.Effect ? EffectNormalizer.Normalize(text) : TriggerStructures.Normalize(text);
        Assert.That(Bare(once), Is.Not.EqualTo(Bare(text)), id + ": 예외 목록에서 지운다");
      }
    }

    public static IEnumerable<TestCaseData> Rows()
    {
      foreach (string kind in new[] { "effect", "trigger" })
        foreach (var row in PhraseTable.Load(kind))
          yield return new TestCaseData(kind, row).SetName("TableRow_" + kind + "_" + row.Expected);
    }

    [TestCaseSource("Rows")]
    public void TableExpectedSentenceIsStableUnderNormalize(string kind, PhraseTable.Row row)
    {
      Func<string, string> normalize = kind == "effect" ? (Func<string, string>)EffectNormalizer.Normalize : TriggerStructures.Normalize;
      Assert.That(normalize(row.Expected), Is.EqualTo(row.Expected));
    }

    // 모듈 하나가 다른 모듈과 같은 기본값 문장을 가지면 안 된다(같은 종류 안에서)
    [Test]
    public void NoTwoModulesShareTheSameDefaultSentence()
    {
      var groups = ModuleCatalog.All
        .Select(x => new { x.Id, x.Kind, Text = Bare(x.Fill()) })
        .GroupBy(x => x.Kind + "|" + x.Text)
        .Where(g => g.Count() > 1)
        .Select(g => string.Join(", ", g.Select(x => x.Id)))
        .ToList();
      Assert.That(groups, Is.Empty);
    }

    // 같은 분류 종류(Classify)는 같은 계열(ID 앞 두 마디) 모듈끼리만 나눈다.
    // 계열이 다른 모듈이 같은 분류를 쓰면 중복 후보라 아래 목록에 사유와 함께 둔다(research/potentials/MODULE_OVERLAPS.md)
    static readonly Dictionary<string, string> CrossFamilyKinds = new Dictionary<string, string>
    {
      { "TriggerEvent:상태 됨", "역할 변경 사건과 상태 됨이 같은 사건 모듈을 쓴다(입력 서식만 별도)" },
      { "TriggerEvent:소지품 소실", "빼앗김·소모·파괴는 한 사건 모듈의 서식 변형이다" },
      { "TriggerEvent:타이밍", "트레이너 전용 『통상교대 마지막』 서식이 타이밍 모듈을 쓴다" },
      { "TriggerState:장소", "지형·룸은 한 장소 상태 모듈의 서식이다" },
      { "TriggerEvent:장소 변화", "지형·룸 변화는 한 장소 변화 모듈의 서식이다" },
      { "TriggerState:상시", "항상·필드에 있는 한은 같은 상시 모듈이다" },
      { "TriggerState:생존", "이름 지정 생존과 최후 아님은 같은 생존 모듈이다" },
      { "TriggerState:소지품", "소지품 있음·없음·보유 서식이 한 모듈을 쓴다" },
      { "TriggerEvent:타입 바뀜", "타입 바뀜 두 서식(실프)이 한 모듈을 쓴다" },
      { "TriggerState:타입 바뀜 T", "타입 바뀜 T 두 서식(실프)이 한 모듈을 쓴다" },
      { "E:수치 배율", "총대장의 빈사 아군 비례 배율은 수치 배율의 변형이다" },
      { "E:교대 효과", "MODULE_OVERLAPS.md: 선택형 교대(실프)와 임의교대가 같은 뜻인지 확인 대기" },
      { "E:유턴 효과", "MODULE_OVERLAPS.md: 공격기술 유턴 부여(실프)와 유턴 부여가 같은 뜻인지 확인 대기" },
      { "E:타입 추가", "특성 타입 추가와 효과 타입 추가가 같은 모듈이다" }
    };

    static string Family(string id)
    {
      return string.Join(".", id.Split('.').Take(2));
    }

    static IEnumerable<KeyValuePair<string, string[]>> ClassKinds()
    {
      var items = ModuleCatalog.All.Select(x =>
      {
        string kind = x.Kind == ModuleKind.Effect ? "E:" + EffectModules.Classify(x.Fill()) : x.Kind + ":" + TriggerModules.Classify(x.Fill());
        return new { x.Id, Kind = kind };
      });
      foreach (var g in items.GroupBy(x => x.Kind))
        yield return new KeyValuePair<string, string[]>(g.Key, g.Select(x => x.Id).ToArray());
    }

    [Test]
    public void CrossFamilyClassSharingIsListedWithAReason()
    {
      var unlisted = new List<string>();
      foreach (var pair in ClassKinds())
      {
        if (pair.Key.EndsWith(":"))
          continue;
        if (pair.Value.Select(Family).Distinct().Count() > 1 && !CrossFamilyKinds.ContainsKey(pair.Key))
          unlisted.Add(pair.Key + " <- " + string.Join(", ", pair.Value));
      }

      Assert.That(unlisted, Is.Empty, string.Join("\n", unlisted));
    }

    [Test]
    public void CrossFamilyExceptionsAreStillNeeded()
    {
      var current = ClassKinds().Where(p => p.Value.Select(Family).Distinct().Count() > 1).Select(p => p.Key).ToList();
      Assert.That(CrossFamilyKinds.Keys.Except(current), Is.Empty, "더 이상 계열이 겹치지 않는 예외");
    }
  }
}
