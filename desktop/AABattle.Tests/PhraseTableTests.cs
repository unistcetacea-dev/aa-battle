using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using NUnit.Framework;

namespace AABattle
{
  // 정규화 왕복 검사 표: Data/phrases-effect.tsv, Data/phrases-trigger.tsv (열: 입력 · 기대 · 출처 · 추가검사)
  // 새 정규화 검사는 이 표에 한 줄을 추가한다(검사 코드를 새로 만들지 않는다).
  public static class PhraseTable
  {
    public sealed class Row
    {
      public string Raw;
      public string Expected;
      public string Source;
      public string Check;
      public override string ToString()
      {
        return Source + ": " + Raw;
      }
    }

    public static List<Row> Load(string kind)
    {
      string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "phrases-" + kind + ".tsv");
      var rows = new List<Row>();
      foreach (string line in File.ReadAllLines(path, Encoding.UTF8).Skip(1))
      {
        if (line.Trim().Length == 0 || line.StartsWith("#"))
          continue;
        string[] cells = line.Split('\t');
        rows.Add(new Row
        {
          Raw = cells[0],
          Expected = cells.Length > 1 ? cells[1] : "",
          Source = cells.Length > 2 ? cells[2] : "",
          Check = cells.Length > 3 ? cells[3] : ""
        });
      }

      return rows;
    }

    public static IEnumerable<TestCaseData> Cases(string kind)
    {
      foreach (var row in Load(kind))
        yield return new TestCaseData(row).SetName(kind + ": " + row.Raw);
    }
  }

  [TestFixture]
  public class PhraseTableTests
  {
    public static IEnumerable<TestCaseData> EffectRows()
    {
      return PhraseTable.Cases("effect");
    }

    public static IEnumerable<TestCaseData> TriggerRows()
    {
      return PhraseTable.Cases("trigger");
    }

    [TestCaseSource("EffectRows")]
    public void EffectPhraseNormalizes(PhraseTable.Row row)
    {
      Assert.That(EffectNormalizer.Normalize(row.Raw), Is.EqualTo(row.Expected));
      if (row.Check == "classify-raw")
        Assert.That(EffectModules.Classify(row.Raw), Is.Not.Empty);
    }

    [TestCaseSource("TriggerRows")]
    public void TriggerPhraseNormalizes(PhraseTable.Row row)
    {
      Assert.That(TriggerStructures.Normalize(row.Raw), Is.EqualTo(row.Expected));
      Assert.That(TriggerStructures.Normalize(row.Expected), Is.EqualTo(row.Expected), "canonical is stable");
      if (!row.Expected.StartsWith("서로"))
        Assert.That(TriggerModules.Classify(row.Expected), Is.Not.Empty, "canonical is a module");
    }
  }
}
