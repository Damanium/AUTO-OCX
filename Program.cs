using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace AutoOcx
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }

    class MainForm : Form
    {
        static readonly Color Accent = Color.FromArgb(0, 120, 212);
        static readonly Color Bg = Color.FromArgb(243, 245, 248);

        ListView list;
        RichTextBox log;
        ProgressBar progress;
        Button btnAdd, btnFolder, btnRemove, btnClear, btnRegister, btnUnregister;
        Label lblStatus;
        bool busy;
        Button[] saveLoad;

        public MainForm()
        {
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            Text = "AUTO OCX - Registro de controles ActiveX";
            Font = new Font("Segoe UI", 9.5f);
            BackColor = Bg;
            ClientSize = new Size(900, 600);
            MinimumSize = new Size(880, 520);
            StartPosition = FormStartPosition.CenterScreen;
            AllowDrop = true;
            DragEnter += (s, e) => { if (e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy; };
            DragDrop += (s, e) => AddPaths((string[])e.Data.GetData(DataFormats.FileDrop));

            var header = new Panel { Dock = DockStyle.Top, Height = 64, BackColor = Accent };
            header.Controls.Add(new Label
            {
                Text = "AUTO OCX", ForeColor = Color.White, AutoSize = true,
                Font = new Font("Segoe UI Semibold", 16f), Location = new Point(18, 8)
            });
            header.Controls.Add(new Label
            {
                Text = "Registra o desregistra varios OCX/DLL a la vez. Arrastra los archivos a esta ventana.",
                ForeColor = Color.FromArgb(220, 235, 250), AutoSize = true, Location = new Point(20, 38)
            });

            var about = new LinkLabel
            {
                Text = "Acerca de", AutoSize = true, LinkColor = Color.White, ActiveLinkColor = Color.White,
                Anchor = AnchorStyles.Top | AnchorStyles.Right, Location = new Point(810, 22), Cursor = Cursors.Hand
            };
            about.LinkClicked += (s, e) => ShowAbout();
            header.Controls.Add(about);
            header.Resize += (s, e) => about.Left = header.ClientSize.Width - about.Width - 18;

            var bar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 48, Padding = new Padding(12, 8, 12, 0) };
            btnAdd = Btn("Añadir archivos…", false, (s, e) => PickFiles());
            btnFolder = Btn("Añadir carpeta…", false, (s, e) => PickFolder());
            btnRemove = Btn("Quitar", false, (s, e) => RemoveSelected());
            btnClear = Btn("Vaciar", false, (s, e) => list.Items.Clear());
            var btnSave = Btn("Guardar lista…", false, (s, e) => SaveList());
            var btnLoad = Btn("Cargar lista…", false, (s, e) => LoadList());
            bar.Controls.AddRange(new Control[] { btnAdd, btnFolder, btnRemove, btnClear, btnSave, btnLoad });
            saveLoad = new[] { btnSave, btnLoad };

            list = new ListView
            {
                Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, GridLines = false,
                BorderStyle = BorderStyle.FixedSingle, HideSelection = false
            };
            list.Columns.Add("Archivo", 230);
            list.Columns.Add("Ruta", 330);
            list.Columns.Add("Bits", 55);
            list.Columns.Add("Estado", 150);
            list.KeyDown += (s, e) => { if (e.KeyCode == Keys.Delete) RemoveSelected(); };
            var listHost = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12, 0, 12, 0) };
            listHost.Controls.Add(list);

            log = new RichTextBox
            {
                Dock = DockStyle.Bottom, Height = 130, ReadOnly = true, BackColor = Color.White,
                Font = new Font("Consolas", 9f), BorderStyle = BorderStyle.FixedSingle
            };
            var logHost = new Panel { Dock = DockStyle.Bottom, Height = 142, Padding = new Padding(12, 6, 12, 6) };
            logHost.Controls.Add(log);

            var bottom = new Panel { Dock = DockStyle.Bottom, Height = 84 };
            progress = new ProgressBar { Left = 12, Top = 8, Height = 10, Width = 100, Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top };
            lblStatus = new Label { Left = 12, Top = 24, AutoSize = true, ForeColor = Color.DimGray, Text = "Admin: " + (IsAdmin() ? "sí" : "no") };
            btnUnregister = Btn("Desregistrar", false, (s, e) => Run(false));
            btnRegister = Btn("Registrar todo", true, (s, e) => Run(true));
            btnRegister.Size = new Size(160, 38);
            btnUnregister.Size = new Size(130, 38);
            btnRegister.Anchor = btnUnregister.Anchor = AnchorStyles.Right | AnchorStyles.Top;
            bottom.Controls.AddRange(new Control[] { progress, lblStatus, btnUnregister, btnRegister });
            bottom.Resize += (s, e) =>
            {
                progress.Width = bottom.ClientSize.Width - 24;
                btnRegister.Location = new Point(bottom.ClientSize.Width - 12 - btnRegister.Width, 36);
                btnUnregister.Location = new Point(btnRegister.Left - 10 - btnUnregister.Width, 36);
            };

            Controls.Add(listHost);
            Controls.Add(logHost);
            Controls.Add(bottom);
            Controls.Add(bar);
            Controls.Add(header);

            Shown += (s, e) =>
            {
                bottom.PerformLayout();
                if (!IsAdmin()) Log("Aviso: sin permisos de administrador; el registro puede fallar.", Color.DarkOrange);
                Log("Listo. Añade archivos .ocx / .dll / .ax.", Color.Gray);
            };
        }

        Button Btn(string text, bool primary, EventHandler click)
        {
            var b = new Button
            {
                Text = text, AutoSize = true, MinimumSize = new Size(110, 32), FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand, Margin = new Padding(0, 0, 8, 0),
                BackColor = primary ? Accent : Color.White, ForeColor = primary ? Color.White : Color.FromArgb(40, 40, 40)
            };
            b.FlatAppearance.BorderColor = primary ? Accent : Color.FromArgb(200, 205, 212);
            b.Click += click;
            return b;
        }

        static bool IsAdmin()
        {
            using (var id = System.Security.Principal.WindowsIdentity.GetCurrent())
                return new System.Security.Principal.WindowsPrincipal(id)
                    .IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
        }

        void PickFiles()
        {
            using (var d = new OpenFileDialog { Multiselect = true, Filter = "Controles ActiveX (*.ocx;*.dll;*.ax)|*.ocx;*.dll;*.ax|Todos|*.*" })
                if (d.ShowDialog(this) == DialogResult.OK) AddPaths(d.FileNames);
        }

        void PickFolder()
        {
            using (var d = new FolderBrowserDialog { Description = "Se añadirán los .ocx de la carpeta y subcarpetas" })
                if (d.ShowDialog(this) == DialogResult.OK) AddPaths(new[] { d.SelectedPath });
        }

        void ShowAbout()
        {
            var ver = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            using (var f = new Form
            {
                Text = "Acerca de AUTO OCX", FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false,
                MinimizeBox = false, ShowInTaskbar = false, StartPosition = FormStartPosition.CenterParent,
                ClientSize = new Size(420, 250), Font = Font, BackColor = Color.White, Icon = Icon
            })
            {
                f.Controls.Add(new Label { Text = "AUTO OCX", Font = new Font("Segoe UI Semibold", 16f), ForeColor = Accent, AutoSize = true, Location = new Point(20, 16) });
                f.Controls.Add(new Label { Text = "Versión " + ver.Major + "." + ver.Minor + "." + ver.Build, ForeColor = Color.DimGray, AutoSize = true, Location = new Point(22, 52) });
                f.Controls.Add(new Label
                {
                    Text = "Registro y desregistro de OCX por lotes.\r\nSoftware gratuito y de uso libre (licencia MIT).\r\n\r\nCreado por Juan Carlos Alvarez Oncala\r\n© 2026 Juan Carlos Alvarez Oncala. Libre uso y distribución.",
                    AutoSize = true, Location = new Point(22, 82)
                });
                var mail = new LinkLabel { Text = "juankylodk@gmail.com", AutoSize = true, Location = new Point(22, 180) };
                mail.LinkClicked += (s, e) => { try { Process.Start("mailto:juankylodk@gmail.com"); } catch { } };
                f.Controls.Add(mail);
                var ok = new Button { Text = "Cerrar", DialogResult = DialogResult.Cancel, Size = new Size(90, 30), Location = new Point(310, 206) };
                f.Controls.Add(ok);
                f.CancelButton = ok;
                f.ShowDialog(this);
            }
        }

        void SaveList()
        {
            if (list.Items.Count == 0) { Log("No hay nada que guardar.", Color.DarkOrange); return; }
            using (var d = new SaveFileDialog { Filter = "Lista de OCX (*.ocxlist)|*.ocxlist", FileName = "mi-lista.ocxlist" })
            {
                if (d.ShowDialog(this) != DialogResult.OK) return;
                var lines = new List<string>();
                foreach (ListViewItem i in list.Items) lines.Add((string)i.Tag);
                File.WriteAllLines(d.FileName, lines.ToArray(), System.Text.Encoding.UTF8);
                Log("Lista guardada: " + d.FileName, Accent);
            }
        }

        void LoadList()
        {
            using (var d = new OpenFileDialog { Filter = "Lista de OCX (*.ocxlist)|*.ocxlist|Texto (*.txt)|*.txt" })
            {
                if (d.ShowDialog(this) != DialogResult.OK) return;
                var paths = new List<string>();
                int missing = 0;
                foreach (var l in File.ReadAllLines(d.FileName))
                {
                    var p = l.Trim();
                    if (p.Length == 0) continue;
                    if (File.Exists(p)) paths.Add(p); else { missing++; Log("No existe: " + p, Color.DarkOrange); }
                }
                AddPaths(paths.ToArray());
                Log(string.Format("Lista cargada ({0} archivos, {1} no encontrados).", paths.Count, missing), Accent);
            }
        }

        void RemoveSelected()
        {
            foreach (ListViewItem i in new List<ListViewItem>(list.SelectedItems.Cast())) list.Items.Remove(i);
        }

        void AddPaths(string[] paths)
        {
            var files = new List<string>();
            foreach (var p in paths)
            {
                if (Directory.Exists(p)) files.AddRange(Directory.GetFiles(p, "*.ocx", SearchOption.AllDirectories));
                else if (File.Exists(p)) files.Add(p);
            }
            foreach (var f in files)
            {
                bool dup = false;
                foreach (ListViewItem it in list.Items)
                    if (string.Equals((string)it.Tag, f, StringComparison.OrdinalIgnoreCase)) { dup = true; break; }
                if (dup) continue;
                var bits = GetBitness(f);
                var item = new ListViewItem(new[] { Path.GetFileName(f), Path.GetDirectoryName(f), bits == 0 ? "?" : bits.ToString(), "Pendiente" }) { Tag = f };
                list.Items.Add(item);
            }
        }

        // Lee la cabecera PE: devuelve 32, 64 o 0 si no es un PE valido.
        static int GetBitness(string path)
        {
            try
            {
                using (var br = new BinaryReader(File.OpenRead(path)))
                {
                    if (br.ReadUInt16() != 0x5A4D) return 0;
                    br.BaseStream.Seek(0x3C, SeekOrigin.Begin);
                    int pe = br.ReadInt32();
                    br.BaseStream.Seek(pe, SeekOrigin.Begin);
                    if (br.ReadUInt32() != 0x4550) return 0;
                    ushort machine = br.ReadUInt16();
                    if (machine == 0x014c) return 32;
                    if (machine == 0x8664 || machine == 0xAA64) return 64;
                }
            }
            catch { }
            return 0;
        }

        static string Regsvr32For(int bits)
        {
            string win = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            // Proceso de 32 bits en Windows de 64: System32 se redirige; Sysnative da el real.
            bool os64 = Environment.Is64BitOperatingSystem;
            if (!os64) return Path.Combine(win, "System32", "regsvr32.exe");
            if (bits == 32) return Path.Combine(win, "SysWOW64", "regsvr32.exe");
            return Environment.Is64BitProcess
                ? Path.Combine(win, "System32", "regsvr32.exe")
                : Path.Combine(win, "Sysnative", "regsvr32.exe");
        }

        void Run(bool register)
        {
            if (busy || list.Items.Count == 0) return;
            busy = true;
            SetEnabled(false);
            progress.Maximum = list.Items.Count;
            progress.Value = 0;
            var items = new List<ListViewItem>();
            foreach (ListViewItem i in list.Items) { items.Add(i); i.SubItems[3].Text = "Esperando…"; i.ForeColor = Color.Black; }
            Log(register ? "--- Registrando ---" : "--- Desregistrando ---", Accent);

            new Thread(() =>
            {
                int ok = 0, fail = 0;
                foreach (var it in items)
                {
                    string path = (string)it.Tag;
                    int bits = GetBitness(path);
                    var res = Exec(Regsvr32For(bits), (register ? "" : "/u ") + "/s \"" + path + "\"");
                    bool success = res == 0;
                    if (success) ok++; else fail++;
                    BeginInvoke((Action)(() =>
                    {
                        it.SubItems[3].Text = success ? (register ? "Registrado" : "Desregistrado") : "Error (código " + res + ")";
                        it.ForeColor = success ? Color.FromArgb(0, 128, 0) : Color.Firebrick;
                        Log((success ? "OK    " : "ERROR ") + Path.GetFileName(path) + (success ? "" : "  -> " + Describe(res)),
                            success ? Color.FromArgb(0, 128, 0) : Color.Firebrick);
                        progress.Value = Math.Min(progress.Maximum, progress.Value + 1);
                    }));
                }
                BeginInvoke((Action)(() =>
                {
                    Log(string.Format("Terminado: {0} correctos, {1} con error.", ok, fail), fail == 0 ? Accent : Color.Firebrick);
                    busy = false;
                    SetEnabled(true);
                }));
            }) { IsBackground = true }.Start();
        }

        static int Exec(string exe, string args)
        {
            try
            {
                var psi = new ProcessStartInfo(exe, args) { UseShellExecute = false, CreateNoWindow = true };
                using (var p = Process.Start(psi))
                {
                    if (!p.WaitForExit(60000)) { try { p.Kill(); } catch { } return -1; }
                    return p.ExitCode;
                }
            }
            catch { return -2; }
        }

        static string Describe(int code)
        {
            switch (code)
            {
                case 1: return "argumentos no válidos";
                case 2: return "fallo al cargar la DLL/OCX (dependencias o bits incorrectos)";
                case 3: return "no se encontró DllRegisterServer/DllUnregisterServer";
                case 4: return "fallo al ejecutar el registro (¿permisos?)";
                case 5: return "fallo de registro";
                case -1: return "tiempo de espera agotado";
                case -2: return "no se pudo ejecutar regsvr32";
                default: return "código " + code;
            }
        }

        void SetEnabled(bool on)
        {
            foreach (var b in new[] { btnAdd, btnFolder, btnRemove, btnClear, btnRegister, btnUnregister }) b.Enabled = on;
            foreach (var b in saveLoad) b.Enabled = on;
            AllowDrop = on;
        }

        void Log(string msg, Color c)
        {
            log.SelectionStart = log.TextLength;
            log.SelectionColor = c;
            log.AppendText(DateTime.Now.ToString("HH:mm:ss") + "  " + msg + "\n");
            log.ScrollToCaret();
        }
    }

    static class Ext
    {
        public static IEnumerable<ListViewItem> Cast(this ListView.SelectedListViewItemCollection c)
        {
            foreach (ListViewItem i in c) yield return i;
        }
    }
}
