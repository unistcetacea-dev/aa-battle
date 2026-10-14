using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using NUnit.Framework;

namespace AABattle
{
  [TestFixture, Category("UI"), Apartment(System.Threading.ApartmentState.STA)]
  public class AbilityFamilySplitUiTests
  {
    static void Capture(Form form, string directory, string name)
    {
      if (string.IsNullOrEmpty(directory)) return;
      Directory.CreateDirectory(directory);
      Application.DoEvents();
      using (var bitmap = new Bitmap(form.Width, form.Height))
      {
        form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
        bitmap.Save(Path.Combine(directory, name));
      }
    }

    [Test]
    public void EditorShowsFunctionalFamiliesAndReopensTheStatSelection()
    {
      string directory = Environment.GetEnvironmentVariable("O_CAPTURE_PATH");
      using (var form = new Form { Text = "효과 편집 · 기능별 모듈", Width = 1280, Height = 850 })
      using (var builder = new ModuleBuilderPanel(new[] { ModuleKind.Effect }, TestData.Db, null))
      {
        form.Controls.Add(builder);
        form.Show();
        Assert.That(builder.Families, Does.Not.Contain("특성·계속 효과"));
        Assert.That(builder.Families.Last(), Is.EqualTo("효과 없음"));
        Assert.That(builder.Select("ability.field.scalar.stats"), Is.True);
        Assert.That(builder.SetValue("능력", "방어"), Is.True);
        Assert.That(builder.Result, Is.EqualTo("필드에 있는 전원의 「방어」를 강화(1.33배)한다."));
        Capture(form, directory, "O-editor-field-stat.png");
        using (var families = new SearchToggleForm("분류 선택 · 검색", builder.Families, new[] { "수치 배율" }, 1))
        {
          families.Show(form);
          Application.DoEvents();
          Capture(families, directory, "O-editor-families-top.png");
          var list = (PixelCheckedListBox)typeof(SearchToggleForm).GetField("list", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(families);
          list.TopIndex = Math.Max(0, list.Items.Count - 8);
          Application.DoEvents();
          Assert.That(list.Items[list.Items.Count - 1], Is.EqualTo("효과 없음"));
          Capture(families, directory, "O-editor-families-last.png");
          families.Close();
        }
        form.Close();
      }
    }
  }
}
