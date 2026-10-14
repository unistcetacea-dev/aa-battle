using System;
using System.Linq;
using NUnit.Framework;

namespace AABattle {
 // 업데이트 확인의 순수 로직: 버전 비교·Releases JSON 해석·zip 경로·교체 스크립트 (네트워크는 쓰지 않는다)
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

  const string ReleaseJson = "{\"tag_name\":\"v2026.10.15.2\",\"body\":\"첫 줄\\r\\n둘째 줄\",\"assets\":["
   + "{\"name\":\"notes.txt\",\"browser_download_url\":\"https://example.com/notes.txt\"},"
   + "{\"name\":\"AABattle-2026.10.15.2.zip\",\"browser_download_url\":\"https://example.com/AABattle-2026.10.15.2.zip\"}]}";

  [Test] public void ReleaseJsonGivesVersionNotesAndZipAsset() {
   var manifest = UpdateManifest.Parse(ReleaseJson);
   Assert.AreEqual("2026.10.15.2", manifest.Version);
   Assert.AreEqual("첫 줄\n둘째 줄", manifest.Notes);
   Assert.AreEqual("https://example.com/AABattle-2026.10.15.2.zip", manifest.AssetUrl);
  }

  [TestCase("")]
  [TestCase("<html>404: Not Found</html>")]
  [TestCase("[]")]
  [TestCase("{\"message\":\"Not Found\"}")]
  [TestCase("{\"tag_name\":\"latest\",\"assets\":[{\"name\":\"AABattle-1.zip\",\"browser_download_url\":\"https://x/a.zip\"}]}")]
  [TestCase("{\"tag_name\":\"v1.0\",\"assets\":[]}")]
  [TestCase("{\"tag_name\":\"v1.0\",\"assets\":[{\"name\":\"other.zip\",\"browser_download_url\":\"https://x/o.zip\"}]}")]
  [TestCase("{\"tag_name\":\"v1.0\",\"assets\":[{\"name\":\"AABattle-1.0.zip\",\"browser_download_url\":\"http://x/a.zip\"}]}")]
  public void ReleaseJsonRejectsMalformedOrWithoutZip(string json) {
   Assert.IsNull(UpdateManifest.Parse(json));
  }

  [TestCase("AABattle.exe", "AABattle.exe", true)]
  [TestCase("AABattle-1.0/AABattle.exe", "AABattle.exe", true)]
  [TestCase("AABattle-1.0\\AABattle.exe", "AABattle.exe", true)]
  [TestCase("../AABattle.exe", "AABattle.exe", false)]
  [TestCase("a/b/AABattle.exe", "AABattle.exe", false)]
  [TestCase("AABattle.exe.bak", "AABattle.exe", false)]
  public void ZipEntryLookupRejectsOutsidePaths(string entry, string file, bool found) {
   Assert.AreEqual(found, UpdateZip.FindEntryName(new[] { entry }, file) != null);
  }

  // 업데이트는 zip의 samples/<이름>.aabdata.json만 샘플로 받는다
  [TestCase("samples\\finis-chaldea.aabdata.json", "finis-chaldea.aabdata.json")]
  [TestCase("samples/abaddon-sylph.aabdata.json", "abaddon-sylph.aabdata.json")]
  [TestCase("samples/notes.txt", null)]
  [TestCase("samples/../evil.aabdata.json", null)]
  [TestCase("a/samples/x.aabdata.json", null)]
  [TestCase("x.aabdata.json", null)]
  public void SampleEntriesAreOnlyTopLevelSampleFiles(string entry, string expected) {
   var found = UpdateZip.SampleEntries(new[] { entry });
   Assert.AreEqual(expected, found.Count == 0 ? null : found[0].Value);
  }

  [Test] public void ZipTargetStaysInsideFolder() {
   string folder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "aa update");
   Assert.AreEqual(System.IO.Path.Combine(folder, "AABattle.exe"), UpdateZip.TargetPath(folder, "AABattle.exe"));
   Assert.Throws<System.IO.InvalidDataException>(() => UpdateZip.TargetPath(folder, "..\\AABattle.exe"));
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
