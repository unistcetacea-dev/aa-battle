using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;

namespace AABattle {
 // GitHub Releases에서 새 버전을 확인하고 받아 교체하는 네트워크·파일 처리
 static class Updater {
  [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
  static extern bool DeleteFileW(string path);

  // 내 버전: 빌드 때 -p:Version으로 들어간 어셈블리 버전
  public static string CurrentVersion {
   get {
    Version version = typeof(Updater).Assembly.GetName().Version;
    return version == null ? "0.0.0.0" : version.ToString();
   }
  }

  static void Prepare() {
   // .NET Framework 4.8 기본값에 TLS 1.2를 명시한다
   ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
  }

  static HttpWebRequest Open(string url, int timeout) {
   Prepare();
   var request = (HttpWebRequest)WebRequest.Create(url);
   request.Timeout = timeout;
   request.ReadWriteTimeout = timeout;
   request.UserAgent = "AABattle-Updater";
   request.CachePolicy = new System.Net.Cache.RequestCachePolicy(System.Net.Cache.RequestCacheLevel.NoCacheNoStore);
   return request;
  }

  // 새 버전 정보. 새 버전이 없거나 네트워크 실패면 null (조용히 무시)
  public static UpdateManifest CheckForUpdate() {
   try {
    HttpWebRequest request = Open(UpdateConfig.LatestReleaseUrl, 8000);
    request.Accept = "application/vnd.github+json";
    using (var response = request.GetResponse())
    using (var reader = new StreamReader(response.GetResponseStream(), System.Text.Encoding.UTF8)) {
     UpdateManifest manifest = UpdateManifest.Parse(reader.ReadToEnd());
     if (manifest != null && UpdateVersion.IsNewer(manifest.Version, CurrentVersion)) return manifest;
    }
   } catch (Exception) {
   }
   return null;
  }

  // 지난 업데이트가 남긴 *.old.exe를 지운다
  public static void CleanOld() {
   try {
    string directory = AppDomain.CurrentDomain.BaseDirectory;
    foreach (string file in UpdateConfig.Files) {
     string old = Path.Combine(directory, UpdateScript.OldName(file));
     if (File.Exists(old)) File.Delete(old);
    }
   } catch (Exception) {
   }
  }

  // Release의 zip을 임시 폴더에 받아 풀고, 풀린 EXE가 든 폴더를 돌려준다. progress는 0~1. 실패하면 예외
  public static string Download(string zipUrl, Action<double> progress) {
   string folder = Path.Combine(Path.GetTempPath(), "aabattle-update-" + Guid.NewGuid().ToString("N"));
   Directory.CreateDirectory(folder);
   string zipPath = folder + ".zip";
   using (var response = Open(zipUrl, 30000).GetResponse())
   using (var input = response.GetResponseStream())
   using (var output = File.Create(zipPath)) {
    long total = response.ContentLength;
    var buffer = new byte[64 * 1024];
    long done = 0;
    int read;
    while ((read = input.Read(buffer, 0, buffer.Length)) > 0) {
     output.Write(buffer, 0, read);
     done += read;
     if (total > 0 && progress != null) progress(0.9 * done / total);
    }
   }
   using (ZipArchive archive = ZipFile.OpenRead(zipPath)) {
    foreach (string file in UpdateConfig.Files) {
     string entryName = UpdateZip.FindEntryName(archive.Entries.Select(e => e.FullName), file);
     if (entryName == null) throw new InvalidDataException(file + "이(가) zip에 없습니다.");
     string path = UpdateZip.TargetPath(folder, file);
     archive.GetEntry(entryName).ExtractToFile(path, true);
     if (!UpdateFileCheck.LooksLikeExe(File.ReadAllBytes(path))) throw new InvalidDataException(file + " 파일이 올바른 실행 파일이 아닙니다.");
     // 다운로드 차단 표시(Mark of the Web)를 지운다
     DeleteFileW(path + ":Zone.Identifier");
    }
    // 샘플 파티는 실행 중에도 바꿀 수 있으므로 바로 프로그램 옆 samples 폴더에 덮어쓴다.
    // zip에 있는 이름만 바꾸고, 사용자가 따로 저장한 다른 파일은 건드리지 않는다.
    string samples = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "samples");
    foreach (var sample in UpdateZip.SampleEntries(archive.Entries.Select(e => e.FullName))) {
     Directory.CreateDirectory(samples);
     string target = UpdateZip.TargetPath(samples, sample.Value);
     archive.GetEntry(sample.Key).ExtractToFile(target, true);
     DeleteFileW(target + ":Zone.Identifier");
    }
   }
   File.Delete(zipPath);
   if (progress != null) progress(1.0);
   return folder;
  }

  // 교체 스크립트를 띄운다. 호출한 쪽이 곧바로 프로그램을 종료해야 한다
  public static void LaunchSwap(string downloadDirectory) {
   string appDirectory = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\');
   string restart = Path.GetFileName(Assembly.GetEntryAssembly().Location);
   string script = UpdateScript.Build(Process.GetCurrentProcess().Id, appDirectory, downloadDirectory, UpdateConfig.Files, restart);
   string scriptPath = Path.Combine(Path.GetTempPath(), "aabattle-update-" + Guid.NewGuid().ToString("N") + ".cmd");
   File.WriteAllText(scriptPath, script, new System.Text.UTF8Encoding(false));
   var info = new ProcessStartInfo("cmd.exe", "/c \"" + scriptPath + "\"") {
    UseShellExecute = false,
    CreateNoWindow = true,
    WindowStyle = ProcessWindowStyle.Hidden
   };
   Process.Start(info);
  }
 }
}
