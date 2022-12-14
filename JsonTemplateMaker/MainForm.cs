
// (c) 2022 Kazuki KOHZUKI

using JsonTemplateMaker.Properties;
using System.ComponentModel;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace JsonTemplateMaker
{
    [DesignerCategory("Code")]
    internal sealed class MainForm : Form
    {
        private const string PLACEHOLDER = @"{
	""key"": ""value""
}
";
        private static readonly string dstPlaceholder = new JsonObject(PLACEHOLDER, "Namespace", "ClassName").ToString();

        private readonly SplitContainer container;
        private readonly AdvancedTextBox ns, source;
        private readonly TextBox destination;
        private readonly ToolStripMenuItem tabWidthSelector;

        private static readonly Regex re_identifier = new(@"^[_a-zA-Z][_a-zA-Z0-9]*");

        private int tabWidth = 4;

        [DllImport("user32.dll")]
        static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int[] lParam);
        private const int EM_SETTABSTOPS = 0x00CB;

        [DllImport("gdi32.dll", ExactSpelling = true)]
        private static extern IntPtr AddFontMemResourceEx(byte[] pbFont, int cbFont, IntPtr pdv, out uint pcFonts);

        private static readonly PrivateFontCollection pfc;
        private static int pfc_counter = 0;
        internal static readonly Font Migu1M_9;

        private static Font LoadFont(byte[] fontBuf, float size)
        {
            var fontBufPtr = IntPtr.Zero;
            try
            {
                fontBufPtr = Marshal.AllocCoTaskMem(fontBuf.Length);
                Marshal.Copy(fontBuf, 0, fontBufPtr, fontBuf.Length);
                AddFontMemResourceEx(fontBuf, fontBuf.Length, IntPtr.Zero, out var _);
                pfc.AddMemoryFont(fontBufPtr, fontBuf.Length);
                return new Font(pfc.Families[pfc_counter++], size);
            }
            finally
            {
                Marshal.FreeCoTaskMem(fontBufPtr);
            }
        } // private static Font LoadFont (byte[], float)

        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        } // private static void Main ()

        static MainForm()
        {
            pfc = new PrivateFontCollection();
            Migu1M_9 = LoadFont(Resources.Migu1M, 9);
        } // cctor ()

        private MainForm()
        {
            this.Text = nameof(JsonTemplateMaker);
            this.Size = new(1200, 675);

            this.container = new()
            {
                Orientation = Orientation.Vertical,
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.FixedSingle,
                Parent = this,
            };

            this.source = new()
            {
                Multiline = true,
                Font = Migu1M_9,
                AcceptsTab = true,
                Dock = DockStyle.Fill,
                WordWrap = false,
                TabIndex = 2,
                ScrollBars = ScrollBars.Both,
                Parent = this.container.Panel1,
            };
            
            this.ns = new()
            {
                PlaceholderText = "Namespace.ClassName",
                Font = Migu1M_9,
                Dock = DockStyle.Top,
                TabIndex = 1,
                Parent = this.container.Panel1,
            };

            this.destination = new()
            {
                Multiline = true,
                Font = Migu1M_9,
                AcceptsTab = true,
                Dock = DockStyle.Fill,
                WordWrap = false,
                TabStop = false,
                ScrollBars = ScrollBars.Both,
                Parent = this.container.Panel2,
            };

            #region menu

            var ms = new MenuStrip()
            {
                Dock = DockStyle.Top,
                Parent = this,
            };

            #region menu.file

            var file = new ToolStripMenuItem()
            {
                Text = "&File",
            };
            ms.Items.Add(file);

            var load = new ToolStripMenuItem()
            {
                Text = "&Load JSON file",
                ShortcutKeys = Keys.Control | Keys.O,
            };
            load.Click += LoadJsonFile;
            file.DropDownItems.Add(load);

            var save = new ToolStripMenuItem()
            {
                Text = "&Save C# file",
                ShortcutKeys = Keys.Control | Keys.S,
            };
            save.Click += SaveCSFile;
            file.DropDownItems.Add(save);

            file.DropDownItems.Add("-");

            var exit = new ToolStripMenuItem()
            {
                Text = "Exit (&X)",
                ShortcutKeys = Keys.Alt | Keys.F4,
            };
            exit.Click += (sender, e) => Application.Exit();
            file.DropDownItems.Add(exit);

            #endregion menu.file

            #region menu.view

            var view = new ToolStripMenuItem()
            {
                Text = "&View",
            };
            ms.Items.Add(view);

            var font = new ToolStripMenuItem()
            {
                Text = "&Font",
            };
            font.Click += SetFont;
            view.DropDownItems.Add(font);

            this.tabWidthSelector = new()
            {
                Text = "&Tab width",
            };
            view.DropDownItems.Add(this.tabWidthSelector);

            foreach (var w in new[] { 1, 2, 4, 8 })
            {
                var item = new ToolStripMenuItem()
                {
                    Text = w.ToString(),
                    Tag = w,
                };
                item.Click += SetTabWidth;
                this.tabWidthSelector.DropDownItems.Add(item);
            }

            #endregion menu.view

            #endregion menu

            SetTabWidth(this.tabWidth);

            this.ns.DelayedTextChanged += UpdateResult;
            this.source.DelayedTextChanged += UpdateResult;

            this.container.SplitterDistance = this.Width / 2;
        } // ctor ()

        private void UpdateResult(object? sender, EventArgs e)
        {
            if (this.ns.TextLength * this.source.TextLength == 0) return;

            try
            {
                var fullyQualifiedName = this.ns.Text.Split('.');
                if (fullyQualifiedName.Length < 2) return;
                if (!fullyQualifiedName.All(n => re_identifier.IsMatch(n))) return;
                var name = fullyQualifiedName.Last();
                var ns = string.Join('.', fullyQualifiedName.SkipLast(1).ToArray());

                var src = this.source.Text;
                var json = new JsonObject(src, ns, name);

                this.destination.Text = json.ToString();
            }
            catch (Exception ex)
            {
                this.destination.Text = ex.ToString();
            }
        } // private void UpdateResult (object?, EventArgs)

        private void SetTabWidth(object? sender, EventArgs e)
        {
            if (sender is not ToolStripMenuItem menuItem) return;
            if (menuItem.Tag is not int width) return;
            SetTabWidth(width);
        } // private void SetTabWidth (object?, EventArgs)

        private void SetTabWidth(int width)
        {
            this.tabWidth = width;

            width <<= 2;
            SendMessage(this.source.Handle, EM_SETTABSTOPS, 1, new[] { width });
            SendMessage(this.destination.Handle, EM_SETTABSTOPS, 1, new[] { width });

            foreach (ToolStripMenuItem item in this.tabWidthSelector.DropDownItems)
                item.Checked = item.Tag is int w && w == this.tabWidth;

            this.source.PlaceholderText = PLACEHOLDER.Replace("\t", new String(' ', this.tabWidth));
            this.destination.PlaceholderText = dstPlaceholder.Replace("\t", new String(' ', this.tabWidth));
        } // private void SetTabWidth (int)

        private void SetFont(object? sender, EventArgs e)
        {
            using var fd = new FontDialog()
            {
                Font = this.ns.Font,
                MinSize = 1,
                MaxSize = 64,
                FontMustExist = true,
                AllowVerticalFonts = false,
                ShowEffects = false,
                ShowColor = false,
                FixedPitchOnly = true,
            };

            if (fd.ShowDialog() != DialogResult.OK) return;
            this.ns.Font = this.source.Font = this.destination.Font = fd.Font;
        } // private void SetFont (object?, EventArgs)

        private void LoadJsonFile(object? sender, EventArgs e)
        {
            using var ofd = new OpenFileDialog()
            {
                Title = "Load JSON file",
                Filter = "JSON files|*.json|All files|*.*",
            };
            if (ofd.ShowDialog() != DialogResult.OK) return;

            try
            {
                this.source.Text = File.ReadAllText(ofd.FileName);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        } // private void LoadJsonFile (object?, EventArgs)

        private void SaveCSFile(object? sender, EventArgs e)
        {
            if (this.destination.TextLength == 0) return;

            using var sfd = new SaveFileDialog()
            {
                Title = "Save C# file",
                Filter = "C# files|*.cs|All files|*.*",
            };
            if (sfd.ShowDialog() != DialogResult.OK) return;

            try
            {
                File.WriteAllText(sfd.FileName, this.destination.Text);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        } // private void SaveCSFile (object?, EventArgs)
    } // internal sealed class MainForm : Form
} // namespace JsonTemplateMaker
