﻿/*
ImageGlass Project - Image viewer for Windows
Copyright (C) 2018 DUONG DIEU PHAP
Project homepage: https://imageglass.org

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program.  If not, see <http://www.gnu.org/licenses/>.
*/
using ImageGlass.Core;
using ImageGlass.Theme;
using ImageGlass.Services.Configuration;
using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ImageGlass
{
    public partial class frmMetadataView : Form
    {
        // default location offset on the parent form
        private static Point DefaultLocationOffset = new Point((int)(20 * DPIScaling.GetDPIScaleFactor()), (int)(250 * DPIScaling.GetDPIScaleFactor()));

        private Form _currentOwner = null;
        private Point _locationOffset = DefaultLocationOffset;

        public frmMetadataView()
        {
            InitializeComponent();
        }
		
        #region Borderless form moving
        public const int WM_NCLBUTTONDOWN = 0xA1;
        public const int HT_CAPTION = 0x2;

        [DllImport("user32.dll")]
        private static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, Int32 lParam);
        [DllImport("user32.dll")]
        public static extern bool ReleaseCapture();


        private void frmColorPicker_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
            }
        }
        #endregion

        #region Create shadow for borderless form

        [DllImport("Gdi32.dll", EntryPoint = "CreateRoundRectRgn")]
        private static extern IntPtr CreateRoundRectRgn(
            int nLeftRect, // x-coordinate of upper-left corner
            int nTopRect, // y-coordinate of upper-left corner
            int nRightRect, // x-coordinate of lower-right corner
            int nBottomRect, // y-coordinate of lower-right corner
            int nWidthEllipse, // height of ellipse
            int nHeightEllipse // width of ellipse
        );

        [DllImport("dwmapi.dll")]
        public static extern int DwmExtendFrameIntoClientArea(IntPtr hWnd, ref MARGINS pMarInset);

        [DllImport("dwmapi.dll")]
        public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        [DllImport("dwmapi.dll")]
        public static extern int DwmIsCompositionEnabled(ref int pfEnabled);

        private bool m_aeroEnabled = false;              // variables for box shadow
        private const int CS_DROPSHADOW = 0x00020000;
        private const int WM_NCPAINT = 0x0085;
        private const int WM_ACTIVATEAPP = 0x001C;

        public struct MARGINS                           // struct for box shadow
        {
            public int leftWidth;
            public int rightWidth;
            public int topHeight;
            public int bottomHeight;
        }

        private bool CheckAeroEnabled()
        {
            if (Environment.OSVersion.Version.Major >= 6)
            {
                int enabled = 0;
                DwmIsCompositionEnabled(ref enabled);
                return (enabled == 1) ? true : false;
            }
            return false;
        }

        protected override void WndProc(ref Message m)
        {
            switch (m.Msg)
            {
                case WM_NCPAINT: // box shadow
                    if (m_aeroEnabled)
                    {
                        var v = 2;
                        DwmSetWindowAttribute(this.Handle, 2, ref v, 4);

                        MARGINS margins = new MARGINS()
                        {
                            bottomHeight = 1,
                            leftWidth = 1,
                            rightWidth = 1,
                            topHeight = 1
                        };

                        DwmExtendFrameIntoClientArea(this.Handle, ref margins);
                    }
                    break;
                default:
                    break;
            }

            base.WndProc(ref m);
        }
        #endregion

        #region Properties to make a tool window

        protected override bool ShowWithoutActivation
        {
            get { return true; }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams baseParams = base.CreateParams;
                baseParams.ExStyle |= 0x8000000 // WS_EX_NOACTIVATE
                    | 0x00000080;   // WS_EX_TOOLWINDOW


                #region Shadow for Borderless form
                m_aeroEnabled = CheckAeroEnabled();

                if (!m_aeroEnabled)
                    baseParams.ClassStyle |= CS_DROPSHADOW;
                #endregion


                return baseParams;
            }
        }

        #endregion

        #region Events to manage the form location

        private Point parentLocation = Point.Empty;
        private Point parentOffset = Point.Empty;
        private bool formOwnerMoving = false;



        private void _AttachEventsToParent(Form frmOwner)
        {
            if (frmOwner == null)
                return;

            parentLocation = this.Owner.Location;

            frmOwner.Move += Owner_Move;
            frmOwner.SizeChanged += Owner_Move;
            frmOwner.VisibleChanged += Owner_Move;
            frmOwner.Deactivate += FrmOwner_Deactivate;
            frmOwner.LocationChanged += FrmOwner_LocationChanged;
        }

        private void FrmOwner_LocationChanged(object sender, EventArgs e)
        {
            formOwnerMoving = false;
        }

        private void _DetachEventsFromParent(Form frmOwner)
        {
            if (frmOwner == null)
                return;

            frmOwner.Move -= Owner_Move;
            frmOwner.SizeChanged -= Owner_Move;
            frmOwner.VisibleChanged -= Owner_Move;
            frmOwner.Deactivate -= FrmOwner_Deactivate;
            frmOwner.LocationChanged -= FrmOwner_LocationChanged;
        }


        private void FrmOwner_Deactivate(object sender, EventArgs e)
        {
            this.TopMost = false;
        }

        private void Owner_Move(object sender, EventArgs e)
        {
            if (this.Owner == null) return;

            formOwnerMoving = true;

            var parentOffset = new Point(this.Owner.Left - parentLocation.X, this.Owner.Top - parentLocation.Y);

            _SetLocationBasedOnParent();
            parentLocation = this.Owner.Location;
        }

        private void frmColorPicker_Move(object sender, EventArgs e)
        {
            if (!formOwnerMoving)
            {
                _locationOffset = new Point(this.Left - this.Owner.Left, this.Top - this.Owner.Top);
                parentOffset = _locationOffset;
            }
        }

        protected override void OnShown(EventArgs e)
        {
            if (Owner != _currentOwner)
            {
                _DetachEventsFromParent(_currentOwner);
                _currentOwner = Owner;
                _AttachEventsToParent(_currentOwner);
            }
            
            base.OnShown(e);
        }


        private void _SetLocationBasedOnParent()
        {
            if (Owner == null)
                return;

            if (Owner.WindowState == FormWindowState.Minimized || !Owner.Visible)
            {
                Visible = false;
                return;
            }

            // set location based on the main form
            Point ownerLocation = Owner.Location;
            ownerLocation.Offset(parentOffset);

            this.Location = ownerLocation;
        }

        #endregion

        private void frmColorPicker_KeyDown(object sender, KeyEventArgs e)
        {
            //lblPixel.Text = e.KeyCode.ToString();


            #region ESC or CTRL + SHIFT + K
            //ESC or CTRL + SHIFT + K --------------------------------------------------------
            if ((e.KeyCode == Keys.Escape && !e.Control && !e.Shift && !e.Alt) || //ESC 
                (e.KeyCode == Keys.K && e.Control && e.Shift && !e.Alt))//CTRL + SHIFT + K
            {
                LocalSetting.IsShowMetadataViewOnStartup = false;
                this.Close();
            }
            #endregion
        }


        // KBR abstract
        private void frmColorPicker_FormClosing(object sender, FormClosingEventArgs e)
        {
            LocalSetting.IsMetadataViewOpening = false;
            LocalSetting.ForceUpdateActions |= MainFormForceUpdateAction.METADATA_VIEW_MENU;
        }
		
		public void UpdateUI()
		{
			// Apply current theme
			this.BackColor = LocalSetting.Theme.BackgroundColor;
		}
	
        private void frmColorPicker_Load(object sender, EventArgs e)
        {
            UpdateUI();

            //Windows Bound (Position + Size)-------------------------------------------
            Rectangle rc = GlobalSetting.StringToRect("0,300,100,100");

            if (rc.X == 0 && rc.Y == 0)
            {
                _locationOffset = DefaultLocationOffset;
                parentOffset = _locationOffset;

                _SetLocationBasedOnParent();
            }
            else
            {
                this.Location = rc.Location;
            }
		}
		
    }
}
