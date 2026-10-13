using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;

namespace AABattle {
 // 배포 폴더(GitHub raw)에서 새 버전을 확인하고 받아 교체하는 네트워크·파일 처리
 static class Updater {
  [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
  static extern bool DeleteFileW(string path);

  // 내 버전: 빌드 때 version.txt에서 들어간 어셈블리 버전
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
    using (var response = Open(UpdateConfig.VersionUrl, 8000).GetResponse())
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

  // 파일을 임시 폴더로 받는다. progress는 0~1. 실패하면 예외
  public static string Download(Action<double> progress) {
   string folder = Path.Combine(Path.GetTempPath(), "aabattle-update-" + Guid.NewGuid().ToString("N"));
   Directory.CreateDirectory(folder);
   int index = 0;
   foreach (string file in UpdateConfig.Files) {
    string path = Path.Combine(folder, file);
    using (var response = Open(UpdateConfig.RawUrl(file), 30000).GetResponse())
    using (var input = response.GetResponseStream())
    using (var output = File.Create(path)) {
     long total = response.ContentLength;
     var buffer = new byte[64 * 1024];
     long done = 0;
     int read;
     while ((read = input.Read(buffer, 0, buffer.Length)) > 0) {
      output.Write(buffer, 0, read);
      done += read;
      if (total > 0 && progress != null) progress((index + (double)done / total) / UpdateConfig.Files.Length);
     }
    }
    if (!UpdateFileCheck.LooksLikeExe(File.ReadAllBytes(path))) throw new InvalidDataException(file + " 다운로드 파일이 올바른 실행 파일이 아닙니다.");
    // 다운로드 차단 표시(Mark of the Web)를 지운다
    DeleteFileW(path + ":Zone.Identifier");
    index++;
    if (progress != null) progress((double)index / UpdateConfig.Files.Length);
   }
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
