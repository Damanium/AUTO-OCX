using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using ComTypes = System.Runtime.InteropServices.ComTypes;
using System.Threading;
using Microsoft.Win32;
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
        CheckBox chkCopy;
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
            chkCopy = new CheckBox
            {
                Left = 130, Top = 22, AutoSize = true,
                Text = "Copiar a la carpeta del sistema (System32/SysWOW64) antes de registrar"
            };
            btnUnregister = Btn("Desregistrar", false, (s, e) => Run(false));
            btnRegister = Btn("Registrar todo", true, (s, e) => Run(true));
            btnRegister.Size = new Size(160, 38);
            btnUnregister.Size = new Size(130, 38);
            btnRegister.Anchor = btnUnregister.Anchor = AnchorStyles.Right | AnchorStyles.Top;
            bottom.Controls.AddRange(new Control[] { progress, lblStatus, chkCopy, btnUnregister, btnRegister });
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

        // Carpeta del sistema que corresponde a los bits del OCX (la real, sin redireccion).
        static string SysDirFor(int bits)
        {
            string win = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            if (!Environment.Is64BitOperatingSystem) return Path.Combine(win, "System32");
            if (bits == 32) return Path.Combine(win, "SysWOW64");
            // Proceso de 32 bits en Windows de 64: System32 se redirige; Sysnative da el real.
            return Environment.Is64BitProcess ? Path.Combine(win, "System32") : Path.Combine(win, "Sysnative");
        }

        static string Regsvr32For(int bits)
        {
            return Path.Combine(SysDirFor(bits), "regsvr32.exe");
        }

        // Copia el OCX a la carpeta del sistema. Devuelve la ruta a registrar (la copia, o la original si no se pudo).
        static string CopyToSystem(string path, int bits, out string note)
        {
            note = null;
            try
            {
                string dest = Path.Combine(SysDirFor(bits), Path.GetFileName(path));
                if (string.Equals(dest, path, StringComparison.OrdinalIgnoreCase)) return path;
                if (File.Exists(dest))
                {
                    note = "ya existe en la carpeta del sistema; se registra la ruta original";
                    return path;
                }
                File.Copy(path, dest, false);
                note = "copiado a " + dest;
                return dest;
            }
            catch (Exception ex)
            {
                note = "no se pudo copiar (" + ex.Message + "); se registra la ruta original";
                return path;
            }
        }

        [DllImport("oleaut32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
        static extern void LoadTypeLibEx(string file, int regKind, out ComTypes.ITypeLib lib);

        // CLSID de las clases COM que declara la libreria de tipos del OCX.
        static List<Guid> GetClsids(string path)
        {
            var res = new List<Guid>();
            ComTypes.ITypeLib lib;
            LoadTypeLibEx(path, 2 /* REGKIND_NONE: no registra */, out lib);
            try
            {
                int n = lib.GetTypeInfoCount();
                for (int i = 0; i < n; i++)
                {
                    ComTypes.TYPEKIND kind;
                    lib.GetTypeInfoType(i, out kind);
                    if (kind != ComTypes.TYPEKIND.TKIND_COCLASS) continue;
                    ComTypes.ITypeInfo info;
                    lib.GetTypeInfo(i, out info);
                    IntPtr ptr;
                    info.GetTypeAttr(out ptr);
                    try { res.Add(((ComTypes.TYPEATTR)Marshal.PtrToStructure(ptr, typeof(ComTypes.TYPEATTR))).guid); }
                    finally { info.ReleaseTypeAttr(ptr); Marshal.ReleaseComObject(info); }
                }
            }
            finally { Marshal.ReleaseComObject(lib); }
            return res;
        }

        // Comprueba en el registro (vista de 32 o 64 bits) que los CLSID del OCX
        // estan registrados y apuntan al archivo. state: 0 = verificado, 1 = no verificable, 2 = fallo.
        static string Verify(string path, int bits, bool register, out int state)
        {
            state = 1;
            List<Guid> clsids;
            try { clsids = GetClsids(path); }
            catch { return "sin verificar (el archivo no tiene libreria de tipos)"; }
            if (clsids.Count == 0) return "sin verificar (no declara clases COM)";

            var view = (bits == 32 || !Environment.Is64BitOperatingSystem) ? RegistryView.Registry32 : RegistryView.Registry64;
            int found = 0, wrongPath = 0;
            string other = null;
            using (var root = RegistryKey.OpenBaseKey(RegistryHive.ClassesRoot, view))
            {
                foreach (var g in clsids)
                {
                    using (var k = root.OpenSubKey(@"CLSID\" + g.ToString("B") + @"\InprocServer32"))
                    {
                        if (k == null) continue;
                        found++;
                        string v = Environment.ExpandEnvironmentVariables(((k.GetValue(null) as string) ?? "").Trim().Trim('"'));
                        if (!string.Equals(v, path, StringComparison.OrdinalIgnoreCase)) { wrongPath++; other = v; }
                    }
                }
            }
            string vista = view == RegistryView.Registry32 ? "32 bits" : "64 bits";
            if (register)
            {
                if (found == 0) { state = 2; return "regsvr32 dijo OK pero NO hay CLSID en el registro de " + vista; }
                if (wrongPath > 0) { state = 2; return "registrado, pero apunta a otra ruta: " + other; }
                state = 0;
                return "verificado (" + found + " clase(s) en el registro de " + vista + ")";
            }
            if (found > 0) { state = 2; return "aun quedan " + found + " CLSID en el registro de " + vista; }
            state = 0;
            return "verificado (sin CLSID en el registro de " + vista + ")";
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
            bool copy = chkCopy.Checked && register;
            Log(register ? "--- Registrando ---" : "--- Desregistrando ---", Accent);

            new Thread(() =>
            {
                int ok = 0, fail = 0;
                foreach (var it in items)
                {
                    string path = (string)it.Tag;
                    int bits = GetBitness(path);
                    string note = null;
                    if (copy && bits != 0) path = CopyToSystem(path, bits, out note);
                    var res = Exec(Regsvr32For(bits), (register ? "" : "/u ") + "/s \"" + path + "\"");
                    bool success = res == 0;
                    string detail = null;
                    int state = 1;
                    if (success) detail = Verify(path, bits, register, out state);
                    if (success && state == 2) success = false;
                    if (success) ok++; else fail++;
                    string extra = null;
                    if (success && register && bits == 32 && Environment.Is64BitOperatingSystem)
                        extra = "OCX de 32 bits: solo lo veran programas de 32 bits";
                    string status = success
                        ? (register ? "Registrado" : "Desregistrado") + (state == 0 ? " (verificado)" : " (sin verificar)")
                        : (res == 0 ? "Sin efecto" : "Error (código " + res + ")");
                    string why = res != 0 ? Describe(res) : detail;
                    BeginInvoke((Action)(() =>
                    {
                        it.SubItems[3].Text = status;
                        it.ForeColor = success ? Color.FromArgb(0, 128, 0) : Color.Firebrick;
                        Log((success ? "OK    " : "ERROR ") + Path.GetFileName(path) + "  [" + (bits == 0 ? "?" : bits.ToString()) + " bits]"
                            + (success ? (detail != null ? "  " + detail : "") : "  -> " + why),
                            success ? Color.FromArgb(0, 128, 0) : Color.Firebrick);
                        if (note != null) Log("      " + note, Color.DarkOrange);
                        if (extra != null) Log("      " + extra, Color.DarkOrange);
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
                case 2: return "fallo al inicializar OLE";
                case 3: return "no se pudo cargar el archivo (faltan dependencias, bits incorrectos o no es un OCX/DLL válido)";
                case 4: return "no exporta DllRegisterServer/DllUnregisterServer (no es un control registrable)";
                case 5: return "DllRegisterServer devolvió error (¿permisos, licencia o claves de registro protegidas?)";
                case -1: return "tiempo de espera agotado";
                case -2: return "no se pudo ejecutar regsvr32";
                default: return "código " + code;
            }
        }

        void SetEnabled(bool on)
        {
            foreach (var b in new[] { btnAdd, btnFolder, btnRemove, btnClear, btnRegister, btnUnregister }) b.Enabled = on;
            foreach (var b in saveLoad) b.Enabled = on;
            chkCopy.Enabled = on;
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
