using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace AABattle {
 // 시작 화면의 업데이트 알림: 백그라운드로 새 버전을 확인하고, 버튼을 누르면 받아서 교체한다
 partial class StartForm {
  FlatButton updateButton;
  string updateVersion = "";
  string updateAssetUrl = "";
  bool updating;

  void InitUpdate() {
   updateButton = new FlatButton { Text = "", Visible = false, Primary = true };
   updateButton.Click += (s, e) => StartUpdate();
   Controls.Add(updateButton);
   // 검사·캡처처럼 인자를 줘서 띄운 실행(Theme.Instant)은 네트워크를 쓰지 않는다
   if (Theme.Instant) return;
   ThreadPool.QueueUserWorkItem(state => {
    Updater.CleanOld();
    UpdateManifest manifest = Updater.CheckForUpdate();
    if (manifest != null) RunOnUi(() => ShowUpdate(manifest.Version, manifest.AssetUrl));
   });
  }

  void RunOnUi(Action action) {
   try {
    if (IsDisposed || !IsHandleCreated) return;
    BeginInvoke(action);
   } catch (Exception) {
   }
  }

  void ShowUpdate(string version, string assetUrl) {
   updateVersion = version;
   updateAssetUrl = assetUrl;
   updateButton.Text = "새 버전 " + version + " — 업데이트";
   updateButton.Visible = true;
   ArrangeUpdate();
  }

  // 오른쪽 아래, 안내 줄 위
  void ArrangeUpdate() {
   if (updateButton == null) return;
   int width = Math.Min(440, ClientSize.Width - 80);
   updateButton.SetBounds(ClientSize.Width - 40 - width, ClientSize.Height - 100, width, 40);
  }

  internal void SetUpdateForTest(string version) {
   ShowUpdate(version, "");
  }

  void StartUpdate() {
   if (updating) return;
   updating = true;
   updateButton.Text = "받는 중 0%";
   ThreadPool.QueueUserWorkItem(state => {
    try {
     string folder = Updater.Download(updateAssetUrl, p => RunOnUi(() => { updateButton.Text = "받는 중 " + (int)(p * 100) + "%"; }));
     RunOnUi(() => {
      try {
       Updater.LaunchSwap(folder);
       Application.Exit();
      } catch (Exception ex) {
       FailUpdate(ex.Message);
      }
     });
    } catch (Exception ex) {
     RunOnUi(() => FailUpdate(ex.Message));
    }
   });
  }

  void FailUpdate(string message) {
   updating = false;
   updateButton.Text = "업데이트 실패 — 다시 시도";
   GameDialog.Show(this, "업데이트를 받지 못했습니다.\r\n" + message, "업데이트 오류");
  }
 }
}
