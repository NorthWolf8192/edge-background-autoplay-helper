using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Automation;
using System.Windows.Forms;
using System.Xml;

[assembly: System.Reflection.AssemblyVersion("0.2.0.0")]
[assembly: System.Reflection.AssemblyFileVersion("0.2.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersion("0.2.0")]

namespace EdgeBackgroundHelper {
  internal static class Native {
    [DllImport("user32.dll")] internal static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(IntPtr window, out uint pid);
    [DllImport("user32.dll")] internal static extern bool GetCursorPos(out Point point);
    [DllImport("user32.dll")] internal static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] internal static extern bool UnregisterHotKey(IntPtr window, int id);
    [DllImport("user32.dll")] internal static extern bool SetProcessDPIAware();
  }
  internal sealed class HotkeyWindow : Form {
    internal Action CaptureRequested;
    internal bool Registered;
    internal HotkeyWindow() { ShowInTaskbar = false; FormBorderStyle = FormBorderStyle.FixedToolWindow; Opacity = 0; }
    protected override void OnHandleCreated(EventArgs e) {
      base.OnHandleCreated(e);
      Registered = Native.RegisterHotKey(Handle, 1, 0x4003, 0x77); // Ctrl + Alt + F8, MOD_NOREPEAT.
    }
    protected override void WndProc(ref Message m) {
      if (m.Msg == 0x0312 && m.WParam.ToInt32() == 1 && CaptureRequested != null) CaptureRequested();
      base.WndProc(ref m);
    }
    protected override void OnHandleDestroyed(EventArgs e) { Native.UnregisterHotKey(Handle, 1); base.OnHandleDestroyed(e); }
  }
  internal sealed class Selector {
    internal string Name, Id, Class;
    internal double RightGap, BottomGap;
  }
  internal sealed class Page {
    internal IntPtr Window;
    internal AutomationElement Root, Document, Tab;
    internal string Key;
  }
  internal sealed class HelperContext : ApplicationContext {
    readonly HotkeyWindow window = new HotkeyWindow();
    readonly NotifyIcon tray = new NotifyIcon();
    readonly string folder;
    readonly Thread worker;
    readonly HashSet<string> completed = new HashSet<string>();
    volatile bool stopping;
    volatile bool enabled;
    volatile string state = "\u8bf7\u5f55\u5165\u64ad\u653e\u6309\u94ae";
    long captureAt;
    Selector selector;
    string pendingKey = "";
    DateTime pendingSince;
    [ThreadStatic] static string pageFailure;

    internal HelperContext(string path) {
      folder = path;
      selector = LoadSelector();
      enabled = selector != null;
      window.CaptureRequested = delegate { Interlocked.Exchange(ref captureAt, DateTime.UtcNow.Ticks); };
      IntPtr handle = window.Handle;
      var menu = new ContextMenuStrip();
      menu.Items.Add("\u5f55\u5165\u64ad\u653e\u6309\u94ae\uff085 \u79d2\u540e\uff09", null, delegate {
        SetState("\u8bf7\u5728 5 \u79d2\u5185\u5207\u5230\u65b0\u6807\u7b7e\u9875\uff0c\u5c06\u9f20\u6807\u653e\u5728\u64ad\u653e\u6309\u94ae\u4e0a", true);
        Interlocked.Exchange(ref captureAt, DateTime.UtcNow.AddSeconds(5).Ticks);
      });
      var pause = new ToolStripMenuItem("\u542f\u7528\u81ea\u52a8\u64ad\u653e") { Checked = enabled, CheckOnClick = true };
      pause.Click += delegate {
        enabled = pause.Checked;
        if (enabled && selector == null) SetState("\u8bf7\u5148\u5f55\u5165\u64ad\u653e\u6309\u94ae", true);
        else SetState(enabled ? "\u81ea\u52a8\u64ad\u653e\u5df2\u542f\u7528" : "\u81ea\u52a8\u64ad\u653e\u5df2\u6682\u505c", false);
      };
      menu.Opening += delegate { pause.Checked = enabled; };
      menu.Items.Add(pause);
      menu.Items.Add("\u67e5\u770b\u72b6\u6001", null, delegate {
        MessageBox.Show(state + "\r\n\r\nCtrl + Alt + F8: \u5f55\u5165\u9f20\u6807\u4e0b\u7684\u64ad\u653e\u6309\u94ae\r\n\r\n\u65e5\u5fd7\uff1a" + Path.Combine(folder, "helper.log"),
          "Edge \u80cc\u666f\u64ad\u653e\u52a9\u624b", MessageBoxButtons.OK, MessageBoxIcon.Information);
      });
      menu.Items.Add("\u9000\u51fa", null, delegate { ExitThread(); });
      tray.Icon = SystemIcons.Application;
      tray.Text = "Edge \u80cc\u666f\u64ad\u653e\u52a9\u624b";
      tray.ContextMenuStrip = menu;
      tray.Visible = true;
      tray.DoubleClick += delegate { MessageBox.Show(state, "Edge \u80cc\u666f\u64ad\u653e\u52a9\u624b"); };
      worker = new Thread(Work) { IsBackground = true };
      worker.SetApartmentState(ApartmentState.MTA);
      worker.Start();
      SetState(enabled ? "\u5df2\u542f\u52a8\uff1b\u4ec5\u5904\u7406\u524d\u53f0\u7684 Edge \u65b0\u6807\u7b7e\u9875" :
        "\u8bf7\u6253\u5f00\u65b0\u6807\u7b7e\u9875\uff0c\u9f20\u6807\u653e\u5230\u64ad\u653e\u6309\u94ae\uff0c\u6309 Ctrl + Alt + F8", true);
      if (!window.Registered) SetState("\u5feb\u6377\u952e\u88ab\u5360\u7528\uff0c\u8bf7\u4f7f\u7528\u6258\u76d8\u83dc\u5355\u7684 5 \u79d2\u5f55\u5165", true);
    }

    void SetState(string text, bool balloon) {
      bool changed = state != text;
      state = text;
      if (changed) {
        try {
          string log = Path.Combine(folder, "helper.log");
          if (File.Exists(log) && new FileInfo(log).Length > 256000) File.WriteAllText(log, "");
          File.AppendAllText(log, DateTime.Now.ToString("s") + " " + text + Environment.NewLine);
        } catch { }
      }
      try {
        window.BeginInvoke((Action)delegate {
          if (stopping) return;
          tray.Text = text.Length > 60 ? text.Substring(0, 60) : text;
          if (balloon) tray.ShowBalloonTip(6000, "Edge \u80cc\u666f\u64ad\u653e\u52a9\u624b", text, ToolTipIcon.Info);
        });
      } catch { }
    }

    static bool IsNewTabTitle(string text) {
      return Regex.IsMatch(NormalizeTitle(text), @"^(新建标签页|新标签页|New tab|New Tab Page)(\s*[-–—|].*|\s*)$", RegexOptions.IgnoreCase);
    }
    static string NormalizeTitle(string text) {
      return Regex.Replace(text ?? "", @"\p{Cf}", "").Trim();
    }
    static bool IsNewTabWindowTitle(string text) {
      return Regex.IsMatch(NormalizeTitle(text),
        @"^(新建标签页|新标签页|New tab|New Tab Page)(\s+和另外\s*\d+\s*个(?:页面|标签页)|\s+and\s+\d+\s+other\s+tabs?)?(\s*[-–—|].*|\s*)$",
        RegexOptions.IgnoreCase);
    }
    static string RuntimeId(AutomationElement element) { return string.Join(".", element.GetRuntimeId()); }

    static bool AddressIsNewTab(AutomationElement root) {
      var edits = root.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit));
      foreach (AutomationElement edit in edits) {
        if (edit.Current.IsOffscreen) continue;
        string name = edit.Current.Name ?? "";
        string id = edit.Current.AutomationId ?? "";
        if (id != "addressEditBox" && !Regex.IsMatch(name,
          @"^(地址和搜索栏|地址栏|Address and search bar|Search or enter web address|Address bar)$", RegexOptions.IgnoreCase)) continue;
        object pattern;
        if (!edit.TryGetCurrentPattern(ValuePattern.Pattern, out pattern)) { pageFailure = "地址栏不支持读取值"; return false; }
        string value = ((ValuePattern)pattern).Current.Value.Trim();
        bool valid = value == "" || Regex.IsMatch(value, @"^edge://newtab/?$", RegexOptions.IgnoreCase);
        if (!valid) pageFailure = "地址栏不是原生新标签页地址";
        return valid;
      }
      pageFailure = "未识别到地址栏（可能名称或结构不同）";
      return false;
    }

    static Page GetPage() {
      pageFailure = "Edge 未处于前台";
      IntPtr foreground = Native.GetForegroundWindow();
      if (foreground == IntPtr.Zero) return null;
      uint pid;
      Native.GetWindowThreadProcessId(foreground, out pid);
      using (var process = Process.GetProcessById((int)pid)) {
        if (!string.Equals(process.ProcessName, "msedge", StringComparison.OrdinalIgnoreCase)) return null;
      }
      var root = AutomationElement.FromHandle(foreground);
      if (!IsNewTabWindowTitle(root.Current.Name)) {
        string title = NormalizeTitle(root.Current.Name).Replace("\r", " ").Replace("\n", " ");
        if (title.Length > 180) title = title.Substring(0, 180);
        pageFailure = "窗口标题不匹配新标签页，实际标题：[" + title + "]";
        return null;
      }
      if (!AddressIsNewTab(root)) return null;
      AutomationElement tab = null;
      var tabs = root.FindAll(TreeScope.Descendants, new AndCondition(
        new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.TabItem),
        new PropertyCondition(SelectionItemPattern.IsSelectedProperty, true)));
      foreach (AutomationElement candidate in tabs) {
        if (!candidate.Current.IsOffscreen && IsNewTabTitle(candidate.Current.Name)) { tab = candidate; break; }
      }
      if (tab == null) { pageFailure = "未识别到选中的新标签页标签"; return null; }
      AutomationElement document = null;
      double maxArea = 0;
      var documents = root.FindAll(TreeScope.Descendants,
        new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Document));
      foreach (AutomationElement candidate in documents) {
        var rect = candidate.Current.BoundingRectangle;
        if (candidate.Current.IsOffscreen || rect.IsEmpty || rect.Width < 300 || rect.Height < 200) continue;
        double area = rect.Width * rect.Height;
        if (area > maxArea) { maxArea = area; document = candidate; }
      }
      if (document == null) { pageFailure = "辅助功能接口未暴露可见网页文档"; return null; }
      return new Page { Window = foreground, Root = root, Document = document, Tab = tab,
        Key = foreground.ToInt64() + "/" + RuntimeId(tab) + "/" + RuntimeId(document) };
    }

    static bool IsInDocument(AutomationElement element, AutomationElement document) {
      for (int depth = 0; element != null && depth < 40; depth++) {
        if (Automation.Compare(element, document)) return true;
        element = TreeWalker.ControlViewWalker.GetParent(element);
      }
      return false;
    }
    static bool IsCornerButton(AutomationElement button, Page page) {
      var r = button.Current.BoundingRectangle;
      var d = page.Document.Current.BoundingRectangle;
      if (r.IsEmpty || d.IsEmpty || !button.Current.IsEnabled || button.Current.IsOffscreen ||
        r.Width <= 0 || r.Height <= 0 || r.Width > 160 || r.Height > 160) return false;
      double x = (r.Left + r.Width / 2 - d.Left) / d.Width;
      double y = (r.Top + r.Height / 2 - d.Top) / d.Height;
      return x > 0.70 && x < 1 && y > 0.65 && y < 1;
    }

    void Capture() {
      Page page = GetPage();
      if (page == null) {
        SetState("未能确认原生新标签页：" + pageFailure, true);
        return;
      }
      Point cursor;
      if (!Native.GetCursorPos(out cursor)) return;
      AutomationElement button = AutomationElement.FromPoint(new System.Windows.Point(cursor.X, cursor.Y));
      for (int i = 0; button != null && button.Current.ControlType != ControlType.Button && i < 8; i++)
        button = TreeWalker.ControlViewWalker.GetParent(button);
      if (button == null || button.Current.ControlType != ControlType.Button ||
        !IsInDocument(button, page.Document) || !IsCornerButton(button, page)) {
        SetState("\u9f20\u6807\u4e0b\u672a\u8bc6\u522b\u5230\u9875\u9762\u53f3\u4e0b\u89d2\u7684\u53ef\u7528\u6309\u94ae\uff0c\u672a\u8fdb\u884c\u64cd\u4f5c", true); return;
      }
      string name = button.Current.Name ?? "";
      object invoke;
      if (string.IsNullOrWhiteSpace(name) || !Regex.IsMatch(name, @"播放|play|开始|start|继续|resume", RegexOptions.IgnoreCase) ||
        Regex.IsMatch(name, @"暂停|pause|停止|stop", RegexOptions.IgnoreCase) ||
        !button.TryGetCurrentPattern(InvokePattern.Pattern, out invoke)) {
        SetState("\u6309\u94ae\u4e0d\u652f\u6301\u660e\u786e\u7684\u64ad\u653e\u64cd\u4f5c\uff1a" + name +
          "\u3002\u8bf7\u786e\u4fdd\u662f\u4e09\u89d2\u5f62\u64ad\u653e\u72b6\u6001\uff1b\u82e5\u4ecd\u5931\u8d25\uff0c\u6b64\u65b9\u6cd5\u4e0d\u517c\u5bb9", true); return;
      }
      var rect = button.Current.BoundingRectangle;
      var doc = page.Document.Current.BoundingRectangle;
      selector = new Selector { Name = name, Id = button.Current.AutomationId ?? "", Class = button.Current.ClassName ?? "",
        RightGap = (doc.Right - rect.Right) / doc.Width, BottomGap = (doc.Bottom - rect.Bottom) / doc.Height };
      SaveSelector(selector);
      completed.Clear();
      pendingKey = "";
      enabled = true;
      SetState("\u5df2\u5f55\u5165\u64ad\u653e\u6309\u94ae\uff1a" + name + "\u3002\u81ea\u52a8\u64ad\u653e\u5df2\u542f\u7528", true);
    }

    void Tick() {
      if (!enabled || selector == null) return;
      Page page = GetPage();
      if (page == null) { pendingKey = ""; return; }
      if (completed.Contains(page.Key)) return;
      if (pendingKey != page.Key) {
        pendingKey = page.Key;
        pendingSince = DateTime.UtcNow;
        return;
      }
      if ((DateTime.UtcNow - pendingSince).TotalMilliseconds < 800) return;
      var buttons = page.Document.FindAll(TreeScope.Descendants, new AndCondition(
        new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button),
        new PropertyCondition(AutomationElement.NameProperty, selector.Name)));
      AutomationElement match = null;
      int count = 0;
      foreach (AutomationElement button in buttons) {
        if (!IsCornerButton(button, page)) continue;
        if ((button.Current.AutomationId ?? "") != selector.Id || (button.Current.ClassName ?? "") != selector.Class) continue;
        var r = button.Current.BoundingRectangle;
        var d = page.Document.Current.BoundingRectangle;
        if (Math.Abs((d.Right - r.Right) / d.Width - selector.RightGap) > 0.08 ||
          Math.Abs((d.Bottom - r.Bottom) / d.Height - selector.BottomGap) > 0.08) continue;
        object pattern;
        if (!button.TryGetCurrentPattern(InvokePattern.Pattern, out pattern)) continue;
        count++; match = button;
      }
      if (count != 1) {
        SetState(count == 0 ? "\u7b49\u5f85\u5df2\u5f55\u5165\u7684\u64ad\u653e\u6309\u94ae\uff08\u80cc\u666f\u53ef\u80fd\u5df2\u64ad\u653e\u6216\u9875\u9762\u5c1a\u672a\u52a0\u8f7d\uff09" :
          "\u51fa\u73b0\u591a\u4e2a\u5339\u914d\u6309\u94ae\uff0c\u5df2\u8df3\u8fc7\u64cd\u4f5c", false);
        return;
      }
      // Recheck the active page after querying the accessibility tree.
      Page latest = GetPage();
      if (latest == null || latest.Key != page.Key || Native.GetForegroundWindow() != page.Window ||
        match.Current.Name != selector.Name || !IsInDocument(match, latest.Document) || !IsCornerButton(match, latest)) return;
      object invoke;
      if (!match.TryGetCurrentPattern(InvokePattern.Pattern, out invoke)) return;
      completed.Add(page.Key); // One attempt per tab/document avoids toggling back to pause.
      ((InvokePattern)invoke).Invoke();
      SetState("\u5df2\u53d1\u9001\u64ad\u653e\u64cd\u4f5c\uff1b\u8bf7\u89c2\u5bdf\u80cc\u666f\u662f\u5426\u5f00\u59cb\u52a8\u753b", false);
    }

    void Work() {
      while (!stopping) {
        try {
          long due = Interlocked.Read(ref captureAt);
          if (due != 0 && DateTime.UtcNow.Ticks >= due) {
            if (Interlocked.CompareExchange(ref captureAt, 0, due) == due) Capture();
          } else Tick();
        } catch (ElementNotAvailableException) { }
          catch (Exception e) { SetState("\u68c0\u67e5\u5931\u8d25\uff1a" + e.GetType().Name + " " + e.Message, false); }
        for (int i = 0; i < 10 && !stopping; i++) Thread.Sleep(100);
      }
    }
    Selector LoadSelector() {
      try {
        string file = Path.Combine(folder, "selector.xml");
        if (!File.Exists(file)) return null;
        var xml = new XmlDocument { XmlResolver = null };
        xml.Load(file);
        var root = xml.DocumentElement;
        return new Selector { Name = root.GetAttribute("name"), Id = root.GetAttribute("id"), Class = root.GetAttribute("class"),
          RightGap = double.Parse(root.GetAttribute("rightGap"), System.Globalization.CultureInfo.InvariantCulture),
          BottomGap = double.Parse(root.GetAttribute("bottomGap"), System.Globalization.CultureInfo.InvariantCulture) };
      } catch { return null; }
    }
    void SaveSelector(Selector s) {
      var xml = new XmlDocument();
      var root = xml.CreateElement("selector");
      xml.AppendChild(root);
      root.SetAttribute("name", s.Name); root.SetAttribute("id", s.Id); root.SetAttribute("class", s.Class);
      root.SetAttribute("rightGap", s.RightGap.ToString(System.Globalization.CultureInfo.InvariantCulture));
      root.SetAttribute("bottomGap", s.BottomGap.ToString(System.Globalization.CultureInfo.InvariantCulture));
      xml.Save(Path.Combine(folder, "selector.xml"));
    }
    protected override void ExitThreadCore() {
      stopping = true;
      tray.Visible = false;
      tray.Dispose();
      window.Dispose();
      base.ExitThreadCore();
    }
  }
  public static class Program {
    [STAThread]
    public static void Main() {
      string folder = AppDomain.CurrentDomain.BaseDirectory;
      try { Run(folder); }
      catch (Exception error) {
        try { File.WriteAllText(Path.Combine(folder, "startup-error.txt"), error.ToString()); } catch { }
        MessageBox.Show("\u542f\u52a8\u5931\u8d25\uff1a" + error.Message + "\r\n\u8bf7\u67e5\u770b startup-error.txt", "Edge \u80cc\u666f\u64ad\u653e\u52a9\u624b");
      }
    }
    public static void Run(string folder) {
      bool created;
      using (var mutex = new Mutex(true, @"Local\EdgeBackgroundAutoplayHelper_20261007", out created)) {
        if (!created) { MessageBox.Show("\u52a9\u624b\u5df2\u5728\u8fd0\u884c\uff0c\u8bf7\u67e5\u770b\u53f3\u4e0b\u89d2\u6258\u76d8", "Edge \u80cc\u666f\u64ad\u653e\u52a9\u624b"); return; }
        try {
          Native.SetProcessDPIAware();
          Application.EnableVisualStyles();
          Application.SetCompatibleTextRenderingDefault(false);
          Application.Run(new HelperContext(folder));
        } finally { mutex.ReleaseMutex(); }
      }
    }
  }
}
