/*
    NAPS2 (Not Another PDF Scanner 2)
    http://sourceforge.net/projects/naps2/
    
    Copyright (C) 2009       Pavel Sorejs
    Copyright (C) 2012       Michael Adams
    Copyright (C) 2013       Peter De Leeuw
    Copyright (C) 2012-2013  Ben Olden-Cooligan

    This program is free software; you can redistribute it and/or
    modify it under the terms of the GNU General Public License
    as published by the Free Software Foundation; either version 2
    of the License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.
*/

using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Windows.Forms;
using NAPS2.Scan;
using NAPS2.Scan.Twain;
using NLog;

namespace NAPS2.WinForms
{
    internal partial class FTwainGui : Form, IMessageFilter
    {
        private readonly Logger logger;

        private readonly List<IScannedImage> bitmaps;
        private readonly ExtendedScanSettings settings;
        private bool activated;
        private bool msgfilter;
        private Twain tw;

        public FTwainGui(ExtendedScanSettings settings)
        {
            InitializeComponent();
            bitmaps = new List<IScannedImage>();
            this.settings = settings;
        }

        public List<IScannedImage> Bitmaps
        {
            get { return bitmaps; }
        }

        public Twain TwainIface
        {
            set { tw = value; }
        }

        bool IMessageFilter.PreFilterMessage(ref Message m)
        {
            TwainCommand cmd = tw.PassMessage(ref m);
            if (cmd == TwainCommand.Not)
                return false;

            switch (cmd)
            {
                case TwainCommand.CloseRequest:
                    {
                        EndingScan();
                        tw.CloseSrc();
                        Close();
                        break;
                    }
                case TwainCommand.CloseOk:
                    {
                        EndingScan();
                        tw.CloseSrc();
                        break;
                    }
                case TwainCommand.DeviceEvent:
                    {
                        break;
                    }
                case TwainCommand.TransferReady:
                    {
                        ArrayList pics = tw.TransferPictures();
                        EndingScan();
                        tw.CloseSrc();
                        foreach (IntPtr img in pics)
                        {
                            int bitcount = 0;

                            using (Bitmap bmp = DibUtils.BitmapFromDib(img, out bitcount))
                            {
                                bitmaps.Add(new ScannedImage(bmp, bitcount == 1 ? ScanBitDepth.BlackWhite : ScanBitDepth.C24Bit, settings.MaxQuality));
                            }
                        }
                        Close();
                        break;
                    }
            }

            return true;
        }

        private void EndingScan()
        {
            if (msgfilter)
            {
                Application.RemoveMessageFilter(this);
                msgfilter = false;
                Enabled = true;
                Activate();
            }
        }

        private void FTwainGui_Activated(object sender, EventArgs e)
        {
            if (activated)
                return;
            activated = true;
            if (!msgfilter)
            {
                Enabled = false;
                msgfilter = true;
                Application.AddMessageFilter(this);
            }
            try
            {
                if (!tw.Acquire())
                {
                    EndingScan();
                    Close();
                }
            }
            catch (Exception ex)
            {
                logger.ErrorException("An error occurred while interacting with TWAIN.", ex);
                EndingScan();
                Close();
            }
        }
    }
}