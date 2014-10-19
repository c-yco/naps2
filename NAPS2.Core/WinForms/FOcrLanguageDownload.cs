﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using NAPS2.Lang.Resources;
using NAPS2.Ocr;

namespace NAPS2.WinForms
{
    public partial class FOcrLanguageDownload : FormBase
    {
        private static readonly string DownloadBase = @"file://C:\Users\Ben\Documents\naps2\tesseract-3.0.2\traineddata\";

        private readonly OcrDependencyManager ocrDependencyManager;
        private readonly IFormFactory formFactory;

        public FOcrLanguageDownload(OcrDependencyManager ocrDependencyManager, IFormFactory formFactory)
        {
            this.ocrDependencyManager = ocrDependencyManager;
            this.formFactory = formFactory;
            InitializeComponent();

            // Add missing languages to the list of language options
            // Special case for English: sorted first, and checked by default
            var languageOptions = this.ocrDependencyManager.GetMissingLanguages().OrderBy(x => x.Code == "eng" ? "aaa" : x.Code);
            foreach (var languageOption in languageOptions)
            {
                var item = new ListViewItem { Text = languageOption.LangName, Tag = languageOption };
                if (languageOption.Code == "eng")
                {
                    item.Checked = true;
                }
                lvLanguages.Items.Add(item);
            }

            UpdateView();
        }

        protected override void OnLoad(object sender, EventArgs eventArgs)
        {
            new LayoutManager(this)
                .Bind(lvLanguages)
                    .WidthToForm()
                    .HeightToForm()
                .Bind(labelSizeEstimate, btnCancel, btnDownload)
                    .BottomToForm()
                .Bind(btnCancel, btnDownload)
                    .RightToForm()
                .Activate();
        }

        private void UpdateView()
        {
            double downloadSize =
                lvLanguages.Items.Cast<ListViewItem>().Where(x => x.Checked).Select(x => ((OcrLanguage)x.Tag).Size).Sum();
            if (!ocrDependencyManager.IsExecutableDownloaded)
            {
                downloadSize += ocrDependencyManager.ExecutableFileSize;
            }
            labelSizeEstimate.Text = string.Format(MiscResources.EstimatedDownloadSize, downloadSize.ToString("f1"));

            btnDownload.Enabled = lvLanguages.Items.Cast<ListViewItem>().Any(x => x.Checked);
        }

        private void lvLanguages_ItemChecked(object sender, ItemCheckedEventArgs e)
        {
            UpdateView();
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void btnDownload_Click(object sender, EventArgs e)
        {
            var progressForm = formFactory.Create<FDownloadProgress>();
            if (!ocrDependencyManager.IsExecutableDownloaded)
            {
                progressForm.QueueFile(DownloadBase, ocrDependencyManager.ExecutableFileName, tempPath =>
                {
                    string exeFilePath = Path.Combine(ocrDependencyManager.GetExecutableDir().FullName,
                        ocrDependencyManager.ExecutableFileName.Replace(".gz", ""));
                    DecompressFile(tempPath, exeFilePath);
                });
            }
            foreach (
                var lang in
                    lvLanguages.Items.Cast<ListViewItem>().Where(x => x.Checked).Select(x => (OcrLanguage)x.Tag))
            {
                progressForm.QueueFile(DownloadBase, lang.Filename, tempPath =>
                {
                    string langFilePath = Path.Combine(ocrDependencyManager.GetLanguageDir().FullName,
                        lang.Filename.Replace(".gz", ""));
                    DecompressFile(tempPath, langFilePath);
                });
            }
            Close();
            progressForm.ShowDialog();
            // TODO: Show something else
        }

        private static void DecompressFile(string sourcePath, string destPath)
        {
            using (FileStream inFile = new FileInfo(sourcePath).OpenRead())
            {
                using (FileStream outFile = File.Create(destPath))
                {
                    using (GZipStream decompress = new GZipStream(inFile, CompressionMode.Decompress))
                    {
                        decompress.CopyTo(outFile);
                    }
                }
            }
        }
    }
}
