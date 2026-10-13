using System;
using System.Linq;
using NUnit.Framework;

namespace AABattle {
 // 업데이트 확인의 순수 로직: 버전 비교·version.txt 해석·교체 스크립트 (네트워크는 쓰지 않는다)
 [TestFixture] public class UpdaterTests {
  [TestCase("2026.10.14.1", "2026.10.14.1", false)]
  [TestCase("2026.10.14.2", "2026.10.14.1", true)]
  [TestCase("2026.10.15.1", "2026.10.14.9", true)]
  [TestCase("2026.10.14.1", "2026.10.14.2", false)]
  [TestCase("2026.10.14", "2026.10.14.0", false)]
  [TestCase("2026.10.14.1", "0.0.0.0", true)]
  [TestCase("abc", "2026.10.14.1", false)]
  [TestCase("2026.10.14.1", "x.y", false)]
  [TestCase("", "2026.10.14.1", false)]
  [TestCase("1.2.3.4.5", "1.0.0.0", false)]
  public void IsNewerComparesNumericParts(string remote, string local, bool expected) {
   Assert.AreEqual(expected, UpdateVersion.IsNewer(remote, local));
  }

  [Test] public void ManifestReadsFirstLineAsVersionAndRestAsNotes() {
   var manifest = UpdateManifest.Parse("\uFEFF\r\n2026.10.14.1\r\n첫 줄\r\n둘째 줄\r\n");
   Assert.AreEqual("2026.10.14.1", manifest.Version);
   Assert.AreEqual("첫 줄\r\n둘째 줄", manifest.Notes);
  }

  [TestCase("")]
  [TestCase("   \r\n  ")]
  [TestCase("버전 없음\r\n설명")]
  [TestCase("<html>404: Not Found</html>")]
  public void ManifestRejectsEmptyOrMalformed(string text) {
   Assert.IsNull(UpdateManifest.Parse(text));
  }

  [Test] public void ScriptQuotesPathsWithSpacesAndKorean() {
   string script = UpdateScript.Build(1234, @"C:\프로그램 파일\AA 배틀", @"C:\Temp\100%\update", new[] { "AABattle.exe", "AABattleDataEditor.exe" }, "AABattle.exe");
   Assert.That(script, Does.Contain("\"C:\\프로그램 파일\\AA 배틀\\AABattle.exe\""));
   Assert.That(script, Does.Contain("\"C:\\프로그램 파일\\AA 배틀\\AABattle.old.exe\""));
   Assert.That(script, Does.Contain("\"C:\\Temp\\100%%\\update\\AABattleDataEditor.exe\""));
   Assert.That(script, Does.Contain("PID eq 1234"));
   Assert.That(script, Does.Contain("start \"\" \"C:\\프로그램 파일\\AA 배틀\\AABattle.exe\""));
   // 따옴표는 항상 짝이 맞는다
   foreach (string line in script.Split('\n')) Assert.AreEqual(0, line.Count(c => c == '"') % 2, line);
  }

  [Test] public void ExeCheckNeedsPeHeaderAndSize() {
   var exe = new byte[UpdateFileCheck.MinimumSize];
   exe[0] = (byte)'M';
   exe[1] = (byte)'Z';
   exe[0x3C] = 0x80;
   exe[0x80] = (byte)'P';
   exe[0x81] = (byte)'E';
   Assert.IsTrue(UpdateFileCheck.LooksLikeExe(exe));
   exe[0x80] = 0;
   Assert.IsFalse(UpdateFileCheck.LooksLikeExe(exe));
   Assert.IsFalse(UpdateFileCheck.LooksLikeExe(new byte[10]));
   Assert.IsFalse(UpdateFileCheck.LooksLikeExe(null));
  }
 }
}
