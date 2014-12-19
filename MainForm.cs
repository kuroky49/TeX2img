﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Drawing;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.ComponentModel;

namespace TeX2img {
    public partial class MainForm : Form, IOutputController {
        private OutputForm myOutputForm;
        private PreambleForm myPreambleForm;

        #region コンストラクタおよび初期化処理関連のメソッド
        List<string> FirstFiles;

        public MainForm(List<string> files) {
            FirstFiles = files;

            InitializeComponent();
            
            saveFileDialog1.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            myPreambleForm = new PreambleForm(this);
            myOutputForm = new OutputForm(this);

            sourceTextBox.Highlighter = Sgry.Azuki.Highlighter.Highlighters.Latex;
            sourceTextBox.Resize += delegate { sourceTextBox.ViewWidth = sourceTextBox.ClientSize.Width; };
            loadSettings();

            if(InputFromTextboxRadioButton.Checked) ActiveControl = sourceTextBox;
            else ActiveControl = inputFileNameTextBox;

        }


        protected override void OnShown(EventArgs e) {
            if(FirstFiles.Count != 0) {
                clearOutputTextBox();
                if(Properties.Settings.Default.showOutputWindowFlag) showOutputWindow(true);
                this.Enabled = false;
                convertWorker.RunWorkerAsync(100);
            }
            base.OnShown(e);
        }

        #endregion

        #region 設定値の読み書き
        private void loadSettings() {
            this.Height = Properties.Settings.Default.Height;
            this.Width = Properties.Settings.Default.Width;
            this.Left = Properties.Settings.Default.Left;
            this.Top = Properties.Settings.Default.Top;
            myPreambleForm.Height = Properties.Settings.Default.preambleWindowHeight;
            myPreambleForm.Width = Properties.Settings.Default.preambleWindowWidth;
            myOutputForm.Height = Properties.Settings.Default.outputWindowHeight;
            myOutputForm.Width = Properties.Settings.Default.outputWindowWidth;

            if(Properties.Settings.Default.outputFile != "") {
                outputFileNameTextBox.Text = Properties.Settings.Default.outputFile;
            } else {
                outputFileNameTextBox.Text = Environment.GetFolderPath(Environment.SpecialFolder.Desktop) + "\\equation.eps";
            }
            inputFileNameTextBox.Text = Properties.Settings.Default.inputFile;
            InputFromTextboxRadioButton.Checked = Properties.Settings.Default.inputFromTextBox;
            InputFromFileRadioButton.Checked = !InputFromTextboxRadioButton.Checked;

            Sgry.Azuki.WinForms.AzukiControl preambleTextBox = myPreambleForm.PreambleTextBox;
            preambleTextBox.Text = Properties.Settings.Default.preamble;
            // プリアンブルフォームのキャレットを最後に持っていく
            preambleTextBox.SetSelection(preambleTextBox.TextLength, preambleTextBox.TextLength);
            preambleTextBox.Focus();
            preambleTextBox.ScrollToCaret();

            ChangeSetting();
            setEnabled();
        }

        private void saveSettings() {
            Properties.Settings.Default.Height = this.Height;
            Properties.Settings.Default.Width = this.Width;
            Properties.Settings.Default.Left = this.Left;
            Properties.Settings.Default.Top = this.Top;
            Properties.Settings.Default.preambleWindowHeight = myPreambleForm.Height;
            Properties.Settings.Default.preambleWindowWidth = myPreambleForm.Width;
            Properties.Settings.Default.outputWindowHeight = myOutputForm.Height;
            Properties.Settings.Default.outputWindowWidth = myOutputForm.Width;


            Properties.Settings.Default.outputFile = outputFileNameTextBox.Text;
            Properties.Settings.Default.inputFile = inputFileNameTextBox.Text;
            Properties.Settings.Default.inputFromTextBox = InputFromTextboxRadioButton.Checked;
            Properties.Settings.Default.preamble = myPreambleForm.PreambleTextBox.Text;
        }
        #endregion

        #region 参照ボタンクリックのイベントハンドラ
        private void OutputBrowseButton_Click(object sender, EventArgs e) {
            if(saveFileDialog1.ShowDialog() == DialogResult.OK) {
                outputFileNameTextBox.Text = saveFileDialog1.FileName;
            }
        }

        private void InputFileBrowseButton_Click(object sender, EventArgs e) {
            if(openFileDialog1.ShowDialog() == DialogResult.OK) {
                inputFileNameTextBox.Text = openFileDialog1.FileName;
            }
        }
        #endregion

        #region メニュークリックのイベントハンドラ関連
        private void setEnabled(object sender, EventArgs e) {
            setEnabled();
        }

        private void setEnabled() {
            sourceTextBox.BackColor = (InputFromTextboxRadioButton.Checked ? Properties.Settings.Default.editorFontColor["テキスト"].Back : System.Drawing.SystemColors.ButtonFace);
            sourceTextBox.Enabled = InputFromTextboxRadioButton.Checked;
            inputFileNameTextBox.Enabled = InputFileBrowseButton.Enabled = InputFromFileRadioButton.Checked;
        }

        private void ExitCToolStripMenuItem_Click(object sender, EventArgs e) {
            this.Close();
        }

        private void showPreambleWindowToolStripMenuItem_Click(object sender, EventArgs e) {
            showPreambleWindow(!showPreambleWindowToolStripMenuItem.Checked);
        }

        public void showPreambleWindow(bool show) {
            showPreambleWindowToolStripMenuItem.Checked = show;
            if(show) {
                myPreambleForm.setLocation(Left - myPreambleForm.Width, Top);
                myPreambleForm.Show();
            } else {
                myPreambleForm.Hide();
            }
        }

        private void showOutputWindowToolStripMenuItem_Click(object sender, EventArgs e) {
            showOutputWindow(!showOutputWindowToolStripMenuItem.Checked);
        }

        private void SettingToolStripMenuItem_Click(object sender, EventArgs e) {
            (new SettingForm()).ShowDialog();
            ChangeSetting();
        }

        private void AboutToolStripMenuItem_Click(object sender, EventArgs e) {
            (new AboutDialog()).ShowDialog();
        }
        #endregion

        #region その他のイベントハンドラ
        protected override void OnClosing(CancelEventArgs e) {
            if(Properties.Settings.Default.SaveSettings) saveSettings();
            base.OnClosing(e);
        }
        #endregion

        #region OutputController インターフェースの実装
        public void showPathError(string exeName, string necessary) {
            MessageBox.Show(exeName + " を起動することができませんでした。\n" + necessary + "がインストールされているか，\n" + exeName + " のパスの設定が正しいかどうか，\n確認してください。", "失敗", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
        }

        public void showExtensionError(string file) {
            MessageBox.Show("出力ファイルの拡張子は eps/png/jpg/pdf のいずれかにしてください。", "ファイル形式エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        delegate void appendOutputDelegate(string log);
        public void appendOutput(string log) {
            if(myOutputForm.InvokeRequired) {
                this.Invoke(new appendOutputDelegate(appendOutput), new Object[] { log });
            } else {
                myOutputForm.getOutputTextBox().AppendText(log);
            }
        }

        public void showGenerateError() {
            MessageBox.Show("画像生成に失敗しました。\nソースコードにエラーがないか確認してください。", "画像生成失敗", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        public void showImageMagickError() {
            MessageBox.Show("ImageMagick がインストールされていないため，画像変換ができませんでした。\nImageMagick を別途インストールしてください。\nインストールされている場合は，パスが通っているかどうか確認してください。", "失敗", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
        }

        delegate void scrollOutputTextBoxToEndDelegate();
        public void scrollOutputTextBoxToEnd() {
            TextBox output = myOutputForm.getOutputTextBox();
            if(output.InvokeRequired) {
                this.Invoke(new scrollOutputTextBoxToEndDelegate(scrollOutputTextBoxToEnd));
            } else {
                output.SelectionStart = output.Text.Length;
                output.ScrollToCaret();
            }
        }

        public void showUnauthorizedError(string filePath) {
            MessageBox.Show(filePath + "\nに上書き保存できませんでした。\n一度このファイルを削除してから再試行してください。", "画像生成失敗", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        public void showIOError(string filePath) {
            MessageBox.Show(filePath + "\nが他のアプリケーション開かれているため生成できませんでした。\nこのファイルを開いているアプリケーションを閉じて再試行してください。", "画像生成失敗", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        public bool askYesorNo(string msg) {
            return (MessageBox.Show(msg, "TeX2img", MessageBoxButtons.YesNo) == System.Windows.Forms.DialogResult.Yes);
        }
        #endregion

        public void showOutputWindow(bool show) {
            showOutputWindowToolStripMenuItem.Checked = show;
            if(show) {
                myOutputForm.setLocation(Left + Width, Top);
                myOutputForm.Show();
            } else {
                myOutputForm.Hide();
            }
        }

        public void clearOutputTextBox() {
            myOutputForm.getOutputTextBox().Text = "";
        }

        private void GenerateButton_Click(object sender, EventArgs arg) {
            clearOutputTextBox();
            if(Properties.Settings.Default.showOutputWindowFlag) showOutputWindow(true);
            this.Enabled = false;

            myOutputForm.Activate();
            this.Activate();
            convertWorker.RunWorkerAsync(100);
        }

        private void convertWorker_DoWork(object sender, DoWorkEventArgs e) {
            if(FirstFiles.Count != 0) {
                for(int i = 0 ; i < FirstFiles.Count / 2 ; ++i) {
                    string file = FirstFiles[2 * i];
                    string tmppath = Path.GetTempFileName();
                    string tmptexfn = Path.Combine(Path.GetDirectoryName(tmppath), Path.GetFileNameWithoutExtension(tmppath) + ".tex");
                    File.Delete(tmptexfn);
                    File.Copy(file, tmptexfn, true);
                    (new Converter(this, tmptexfn, FirstFiles[2 * i + 1])).Convert();
                }
                FirstFiles.Clear();
                return;
            }

            string outputFilePath = outputFileNameTextBox.Text;

            if(InputFromFileRadioButton.Checked) {
                string inputTeXFilePath = inputFileNameTextBox.Text;
                if(!File.Exists(inputTeXFilePath)) {
                    MessageBox.Show(inputTeXFilePath + "  が存在しません。", "エラー", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    return;
                }
            }

            string extension = Path.GetExtension(outputFilePath).ToLower();

            string tmpFilePath = Path.GetTempFileName();
            string tmpFileName = Path.GetFileName(tmpFilePath);
            string tmpFileBaseName = Path.GetFileNameWithoutExtension(tmpFileName);


            string tmpTeXFileName = tmpFileBaseName + ".tex";
            string tmpDir = Path.GetDirectoryName(tmpFilePath);

            var converter = new Converter(this, Path.Combine(tmpDir, tmpTeXFileName), outputFileNameTextBox.Text);
            if(!converter.CheckFormat()) return;

            File.Delete(Path.Combine(tmpDir, tmpTeXFileName));
            File.Move(Path.Combine(tmpDir, tmpFileName), Path.Combine(tmpDir, tmpTeXFileName));

            #region TeX ソースファイルの準備
            // 外部ファイルから入力する場合はテンポラリディレクトリにコピー
            if(InputFromFileRadioButton.Checked) {
                string inputTeXFilePath = inputFileNameTextBox.Text;
                string tmpfile = Path.Combine(tmpDir, tmpTeXFileName);
                File.Copy(inputTeXFilePath,tmpfile , true);
                // 読み取り専用の場合解除しておく（後でFile.Deleteに失敗するため）．
                (new FileInfo(tmpfile)).Attributes = FileAttributes.Normal;
            }

            // 直接入力の場合 tex ソースを出力
            if(InputFromTextboxRadioButton.Checked) {
                if(Properties.Settings.Default.encode == "") Properties.Settings.Default.encode = "_sjis";// あり得ないはずだけど
                string enc = Properties.Settings.Default.encode;
                if(enc.Substring(0, 1) == "_") enc = enc.Remove(0, 1);
                Encoding encoding;
                switch(enc) {
                case "sjis": encoding = Encoding.GetEncoding("shift_jis"); break;
                case "euc": encoding = Encoding.GetEncoding("euc-jp"); break;
                case "jis": encoding = Encoding.GetEncoding("iso-2022-jp"); break;
                // BOM付きUTF-8にすることで，文字コードの推定を確実にさせる．
                default: encoding = Encoding.UTF8; break;
                }
                using(StreamWriter sw = new StreamWriter(Path.Combine(tmpDir, tmpTeXFileName), false, encoding)) {
                    try {
                        WriteTeXSourceFile(sw, myPreambleForm.PreambleTextBox.Text, sourceTextBox.Text);
                    }
                    finally {
                        if(sw != null) {
                            sw.Dispose();
                        }
                    }
                }
            }
            #endregion

            converter.Convert();
        }

        private void convertWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e) {
            this.Enabled = true;
        }

        #region 設定変更通知関連
        public void ChangeSetting() {
            ChangeSettingofEditor(sourceTextBox);
            ChangeSettingofEditor(myPreambleForm.PreambleTextBox);
        }

        public void ChangeSettingofEditor(Sgry.Azuki.WinForms.AzukiControl textBox) {
            if(textBox == null) return;
            textBox.FontInfo = new Sgry.Azuki.FontInfo(Properties.Settings.Default.editorFont);
            var x = Properties.Settings.Default.editorFontColor;
            textBox.ColorScheme.ForeColor = Properties.Settings.Default.editorFontColor["テキスト"].Font;
            textBox.ColorScheme.SetColor(Sgry.Azuki.CharClass.Normal, Properties.Settings.Default.editorFontColor["テキスト"].Font, Properties.Settings.Default.editorFontColor["テキスト"].Back);
            textBox.ColorScheme.SetColor(Sgry.Azuki.CharClass.Heading1, Properties.Settings.Default.editorFontColor["テキスト"].Font, Properties.Settings.Default.editorFontColor["テキスト"].Back);
            textBox.ColorScheme.SetColor(Sgry.Azuki.CharClass.Heading2, Properties.Settings.Default.editorFontColor["テキスト"].Font, Properties.Settings.Default.editorFontColor["テキスト"].Back);
            textBox.ColorScheme.SetColor(Sgry.Azuki.CharClass.Heading3, Properties.Settings.Default.editorFontColor["テキスト"].Font, Properties.Settings.Default.editorFontColor["テキスト"].Back);
            textBox.ColorScheme.SelectionFore = Properties.Settings.Default.editorFontColor["選択範囲"].Font;
            textBox.ColorScheme.SelectionBack = Properties.Settings.Default.editorFontColor["選択範囲"].Back;
            textBox.ColorScheme.SetColor(Sgry.Azuki.CharClass.LatexCommand, Properties.Settings.Default.editorFontColor["コントロールシークエンス"].Font, Properties.Settings.Default.editorFontColor["コントロールシークエンス"].Back);
            textBox.ColorScheme.SetColor(Sgry.Azuki.CharClass.LatexEquation, Properties.Settings.Default.editorFontColor["$"].Font, Properties.Settings.Default.editorFontColor["$"].Back);
            textBox.ColorScheme.SetColor(Sgry.Azuki.CharClass.LatexBracket, Properties.Settings.Default.editorFontColor["中 / 大括弧"].Font, Properties.Settings.Default.editorFontColor["中 / 大括弧"].Back);
            textBox.ColorScheme.SetColor(Sgry.Azuki.CharClass.LatexCurlyBracket, Properties.Settings.Default.editorFontColor["中 / 大括弧"].Font, Properties.Settings.Default.editorFontColor["中 / 大括弧"].Back);
            textBox.ColorScheme.SetColor(Sgry.Azuki.CharClass.Comment, Properties.Settings.Default.editorFontColor["コメント"].Font, Properties.Settings.Default.editorFontColor["コメント"].Back);
            textBox.ColorScheme.EofColor = Properties.Settings.Default.editorFontColor["改行，EOF"].Font;
            textBox.ColorScheme.EolColor = Properties.Settings.Default.editorFontColor["改行，EOF"].Font;
            textBox.ColorScheme.MatchedBracketFore = Properties.Settings.Default.editorFontColor["対応する括弧"].Font;
            textBox.ColorScheme.MatchedBracketBack = Properties.Settings.Default.editorFontColor["対応する括弧"].Back;
            Color backColor = new Color();
            if(textBox.Enabled) backColor = Properties.Settings.Default.editorFontColor["テキスト"].Back;
            else backColor = System.Drawing.SystemColors.ButtonFace;
            textBox.ColorScheme.BackColor = backColor;
        }

        #endregion

        #region 右クリック
        private void Undo_Click(object sender, EventArgs e) {
            sourceTextBox.Undo();
        }

        private void Cut_Click(object sender, EventArgs e) {
            sourceTextBox.Cut();
        }

        private void Copy_Click(object sender, EventArgs e) {
            sourceTextBox.Copy();
        }

        private void Paste_Click(object sender, EventArgs e) {
            sourceTextBox.Paste();
        }

        private void Delete_Click(object sender, EventArgs e) {
            sourceTextBox.Delete();
        }

        private void SelectAll_Click(object sender, EventArgs e) {
            sourceTextBox.SelectAll();
        }

        private void Redo_Click(object sender, EventArgs e) {
            sourceTextBox.Redo();
        }
        #endregion

        private void ImportToolStripMenuItem_Click(object sender, EventArgs e) {
            if(MessageBox.Show("TeX ソースファイルをインポートします．\n現在のプリアンブル及び編集中のソースは破棄されます．\nよろしいですか？", "TeX2img", MessageBoxButtons.YesNo) == System.Windows.Forms.DialogResult.No) return;
            if(openFileDialog1.ShowDialog() == DialogResult.OK) {
                try {
                    ImportFile(openFileDialog1.FileName);
                }
                catch(FileNotFoundException) {
                    MessageBox.Show(openFileDialog1.FileName + " は存在しません．");
                }
            }
        }

        private void ExportToolStripMenuItem_Click(object sender, EventArgs e) {
            var sfd = new SaveFileDialog();
            sfd.Filter = "TeX ソースファイル (*.tex)|*.tex|全てのファイル (*.*)|*.*";
            if(sfd.ShowDialog() == System.Windows.Forms.DialogResult.OK) {
                Encoding encoding;
                switch(Properties.Settings.Default.encode) {
                case "euc": encoding = Encoding.GetEncoding("euc-jp"); break;
                case "jis": encoding = Encoding.GetEncoding("iso-2022-jp"); break;
                case "utf8":
                case "_utf8": encoding = new System.Text.UTF8Encoding(false); break;
                case "sjis":
                default: encoding = Encoding.GetEncoding("shift_jis"); break;
                }
                try {
                    using(var file = new StreamWriter(sfd.FileName, false, encoding)) {
                        WriteTeXSourceFile(file, myPreambleForm.PreambleTextBox.Text, sourceTextBox.Text);
                    }
                }
                catch(UnauthorizedAccessException){
                    MessageBox.Show("ファイルの書き込みに失敗しました．\n他のアプリケーションで開いていないかを確認してください．");
                }
            }
        }

        void ImportFile(string path) {
            byte[] buf;
            using(var fs = new FileStream(path, FileMode.Open, FileAccess.Read)) {
                buf = new byte[fs.Length];
                fs.Read(buf, 0, (int) fs.Length);
            }

            var GuessedEncoding = KanjiEncoding.CheckBOM(buf);
            bool bom = false;
            if(GuessedEncoding != null) {
                bom = true;
                buf = KanjiEncoding.DeleteBOM(buf, GuessedEncoding);
            } else {
                var guessed = KanjiEncoding.GuessKajiEncoding(buf);
                if(guessed.Length == 0) GuessedEncoding = null;
                else GuessedEncoding = guessed[0];
            }
            Encoding encoding;
            switch(Properties.Settings.Default.encode) {
            case "euc": encoding = Encoding.GetEncoding("euc-jp"); break;
            case "jis": encoding = Encoding.GetEncoding("iso-2022-jp"); break;
            case "utf8": encoding = Encoding.UTF8; break;
            case "sjis": encoding = Encoding.GetEncoding("shift_jis"); break;
            case "_utf8":
                if(GuessedEncoding != null) encoding = GuessedEncoding;
                else encoding = Encoding.UTF8;
                break;
            case "_sjis":
            default:
                if(GuessedEncoding != null) encoding = GuessedEncoding;
                else encoding = Encoding.GetEncoding("shift_jis");
                break;
            }
            if(encoding.CodePage != GuessedEncoding.CodePage) {
                if(bom || MessageBox.Show("設定されている文字コードは\n" + encoding.EncodingName + "\nですが，読み込まれるファイルは\n" + GuessedEncoding.EncodingName + "\nと推定されました．推定の\n" + GuessedEncoding.EncodingName + "\nを用いてもよろしいですか？", "TeX2img", MessageBoxButtons.YesNo) == System.Windows.Forms.DialogResult.Yes) {
                    encoding = GuessedEncoding;
                }
            }
            using(var sr = new StringReader(encoding.GetString(buf))) {
                string body, preamble;
                if(ParseTeXSourceFile(sr, out preamble, out body)) {
                    if(preamble != null) myPreambleForm.PreambleTextBox.Text = preamble;
                    if(body != null) sourceTextBox.Text = body;
                } else {
                    MessageBox.Show("TeX ソースファイルの解析に失敗しました．\n\\begin{document} や \\end{document} 等が正しく入力されているか確認してください．","TeX2img");
                }
            }
        }

        static bool ParseTeXSourceFile(TextReader file, out string preamble, out string body) {
            preamble = null; body = null;
            var reg = new Regex(@"(?<preamble>^(.*\n)*?[^%]*?(\\\\)*)\\begin\{document\}\n?(?<body>(.*\n)*[^%]*)\\end\{document\}");
            var text = file.ReadToEnd().Replace("\r\n", "\n").Replace("\r", "\n");
            var m = reg.Match(text);
            if(m.Success) {
                preamble = m.Groups["preamble"].Value;
                body = m.Groups["body"].Value;
                return true;
            } else {
                body = text;
                return true;
            }
        }

        static void WriteTeXSourceFile(TextWriter sw, string preamble, string body) {
            sw.Write(preamble);
            if(!preamble.EndsWith("\n")) sw.WriteLine("");
            sw.WriteLine("\\begin{document}");
            sw.Write(body);
            if(!body.EndsWith("\n")) sw.WriteLine("");
            sw.WriteLine("\\end{document}");
        }

        private void sourceTextBox_DragDrop(object sender, DragEventArgs e) {
            if(e.Data.GetDataPresent(DataFormats.FileDrop)) {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if(files.Length == 0)return;
                if(MessageBox.Show("ファイルをインポートします．\n現在のプリアンブル及び編集中のソースは破棄されます．\nよろしいですか？", "TeX2img", MessageBoxButtons.YesNo) == System.Windows.Forms.DialogResult.No) return;
                ImportFile(files[0]);
            }
        }

        private void TextBox_DragEnter(object sender, DragEventArgs e) {
            if(e.Data.GetDataPresent(DataFormats.FileDrop)) {
                e.Effect = DragDropEffects.Copy;
            }
        }

        private void inputFileNameTextBox_DragDrop(object sender, DragEventArgs e) {
            if(e.Data.GetDataPresent(DataFormats.FileDrop)) {
                string[] files = (string[]) e.Data.GetData(DataFormats.FileDrop);
                if(files.Length == 0) return;
                inputFileNameTextBox.Text = files[0];
            }
        }
    }
}
