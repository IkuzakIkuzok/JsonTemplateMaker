
// (c) 2022 Kazuki KOHZUKI

using JsonTemplateMaker.Properties;
using System.ComponentModel;
using System.Drawing.Text;
using System.Runtime.InteropServices;

namespace JsonTemplateMaker
{
    [DesignerCategory("Code")]
    internal sealed class MainForm : Form
    {
        private readonly SplitContainer container;
        private readonly TextBox ns, source, destination;

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
        }

        static MainForm()
        {
            pfc = new PrivateFontCollection();
            Migu1M_9 = LoadFont(Resources.Migu1M, 9);
        } // cctor ()

        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        } // private static void Main ()

        private MainForm()
        {
            this.Text = nameof(JsonTemplateMaker);

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
                ScrollBars = ScrollBars.Both,
                Parent = this.container.Panel1,
            };

            this.ns = new()
            {
                PlaceholderText = "Namespace.ClassName",
                Font = Migu1M_9,
                Dock = DockStyle.Top,
                Parent = this.container.Panel1,
            };

            this.destination = new()
            {
                Multiline = true,
                Font = Migu1M_9,
                AcceptsTab = true,
                Dock = DockStyle.Fill,
                ScrollBars = ScrollBars.Both,
                Parent = this.container.Panel2,
            };

            this.ns.TextChanged += UpdateResult;
            this.source.TextChanged += UpdateResult;
        } // ctor ()

        private void UpdateResult(object? sender, EventArgs e)
        {
            if (this.ns.TextLength * this.source.TextLength == 0) return;

            try
            {
                var fullyQualifiedName = this.ns.Text.Split('.');
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
    } // internal sealed class MainForm : Form
} // namespace JsonTemplateMaker
