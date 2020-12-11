using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Emgu.CV;
using Emgu.CV.Structure;
using Emgu.Util;
using System.Runtime.InteropServices;


namespace Gesture_Recognition
{
    public partial class Form1 : Form
    {

        [DllImport("user32.dll")]
        

        public static extern IntPtr SendMessageW(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr IParam);
        //public static extern void Mouse_move(int dwFlag,int dx,int dy,int dwData,int dwExtraInfo);

        Emgu.CV.UI.ImageBox ImgFrame = new Emgu.CV.UI.ImageBox();
        Emgu.CV.UI.ImageBox ImgSkin = new Emgu.CV.UI.ImageBox();

        Label lblSign = new Label();

        AdaptiveSkinDetector Detector;

        Image<Bgr, byte> Cam_Income;

        Capture Cam = null;

        Seq<Point> hull;
        Seq<Point> FilterHull;
        Seq<MCvConvexityDefect> Defects;
        MCvConvexityDefect[] DefectArray;

        Ycc Skin_Min = new Ycc(0, 131, 80);
        Ycc Skin_Max = new Ycc(255, 185, 135);

        MCvBox2D box = new MCvBox2D();

        private const int APPCOMMAND_VOLUME_MUTE = 0x80000;
        private const int APPCOMMAND_VOLUME_UP = 0xA0000;
        private const int APPCOMMAND_VOLUME_DOWN = 0x90000;
        private const int WM_APPCOMMAND = 0x319;

        PointF judge = new PointF();
        double dif = 0;
        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {   
            ImgFrame.Location = new Point(0, 0);
            ImgFrame.Size = new Size(300, 300);
            ImgFrame.SizeMode = PictureBoxSizeMode.Zoom;
            this.Controls.Add(ImgFrame);

            ImgSkin.Location = new Point(310, 0);
            ImgSkin.Size = new Size(300,300);
            ImgSkin.SizeMode = PictureBoxSizeMode.Zoom;
            this.Controls.Add(ImgSkin);

            Detector = new AdaptiveSkinDetector(1, AdaptiveSkinDetector.MorphingMethod.NONE);

            lblSign.Location = new Point(350, 350);
            lblSign.Font = new System.Drawing.Font("Arial", 48);
            lblSign.AutoSize = true;
            lblSign.Text = "Null";
            this.Controls.Add(lblSign);
                
           
            Cam = new Capture(0);
            Application.Idle += new EventHandler(Grab_While_Idle);
        }

        private void Grab_While_Idle(object sender, EventArgs e)
        {
            Image<Bgr, byte> Cam_Ori = Cam.QueryFrame();
            Rectangle RectCenter = new Rectangle(Cam_Ori.Width / 2 - 200, Cam_Ori.Height / 2 - 200, 400, 400);

            Cam_Income = Cam.QueryFrame();
            Cam_Income.ROI = RectCenter;
            Image<Bgr, byte> Cam_Copy = Cam_Income.Copy();
            Image<Gray, byte> skin = GetSkin(Cam_Copy, Skin_Min, Skin_Max);

            GetContour_Hull(skin);

            

            ImgSkin.Image = skin;
            ImgFrame.Image = Cam_Income;
        }

        private Image<Gray, byte> GetSkin(Image<Bgr, byte> Img, IColor min, IColor Max)
        {
            Image<Ycc, byte> Img_Ycbcr = Img.Convert<Ycc, byte>();
            Image<Gray, byte> skins = new Image<Gray, byte>(Img.Width, Img.Height);
            skins = Img_Ycbcr.InRange((Ycc)min, (Ycc)Max);
            StructuringElementEx rect_for_erode = new StructuringElementEx(12, 12, 6, 6, Emgu.CV.CvEnum.CV_ELEMENT_SHAPE.CV_SHAPE_RECT);
            CvInvoke.cvErode(skins, skins, rect_for_erode, 1);
            StructuringElementEx rect_for_Dilate = new StructuringElementEx(6, 6, 3, 3, Emgu.CV.CvEnum.CV_ELEMENT_SHAPE.CV_SHAPE_RECT);
            CvInvoke.cvDilate(skins, skins, rect_for_Dilate, 2);
            return skins;
        }

        private void GetContour_Hull(Image<Gray, byte> skin)
        {
            using (MemStorage Mem = new MemStorage())
            {
                Contour<Point> Contours = skin.FindContours(Emgu.CV.CvEnum.CHAIN_APPROX_METHOD.CV_CHAIN_APPROX_SIMPLE,
                    Emgu.CV.CvEnum.RETR_TYPE.CV_RETR_LIST, Mem);
                Contour<Point> Maxium = null;

                Double area_cur = 0, area_max = 0;
                while (Contours != null)    
                {
                    area_cur = Contours.Area;
                    if (area_cur > area_max)
                    {
                        area_max = area_cur;
                        Maxium = Contours;
                    }
                    Contours = Contours.HNext;
                }

                if (Maxium != null)
                {
                    
                    Contour<Point> CurrentContour = Maxium.ApproxPoly(Maxium.Perimeter * 0.0025, Mem);
                    Cam_Income.Draw(CurrentContour, new Bgr(Color.LimeGreen), 2);
                    Maxium = CurrentContour;

                    hull = Maxium.GetConvexHull(Emgu.CV.CvEnum.ORIENTATION.CV_CLOCKWISE);
                    box = Maxium.GetMinAreaRect();
                    PointF[] points = box.GetVertices();
                    //box.center.Y += 30;
                    judge.X = (float)(box.center.X - (box.size.Width - box.center.X) * 0.20);
                    judge.Y = (float)(box.center.Y + (box.size.Height - box.center.Y) * 0.40);
                    //dif = box.center.Y + (box.size.Height - box.center.Y) * 0.30;
                    Point[] P_To_int = new Point[points.Length];
                    for (int i = 0; i < points.Length; i++)
                    {
                        P_To_int[i] = new Point((int)points[i].X, (int)points[i].Y);
                    }

                    Cam_Income.DrawPolyline(hull.ToArray(), true, new Bgr(Color.Pink), 2);
                    Cam_Income.Draw(new CircleF(new PointF(box.center.X, box.center.Y), 3), new Bgr(Color.DarkOrchid), 5);
                    Cam_Income.Draw(new CircleF(new PointF(judge.X, judge.Y), 3), new Bgr(Color.Cyan), 5);

                    FilterHull = new Seq<Point>(Mem);
                    for (int i = 0; i < hull.Total - 1; i++)
                    {
                        if (Math.Sqrt(Math.Pow(hull[i].X - hull[i + 1].X, 2) + Math.Pow(hull[i].Y - hull[i + 1].Y, 2)) > box.size.Width / 10)
                        {
                            FilterHull.Push(hull[i]);
                        }
                    }
                    Defects = Maxium.GetConvexityDefacts(Mem, Emgu.CV.CvEnum.ORIENTATION.CV_CLOCKWISE);
                    DefectArray = Defects.ToArray();
                    Get_Result();
                    //MessageBox.Show(DefectArray.Count().ToString());
                }
            }
        }
        

        private void Get_Result()
        {
            int fingerNum = 0;
            for (int i = 0; i < Defects.Total; i++)
            {
                PointF st_P = new PointF((float)DefectArray[i].StartPoint.X,
                    (float)DefectArray[i].StartPoint.Y);
                PointF dep_P = new PointF((float)DefectArray[i].DepthPoint.X,
                    (float)DefectArray[i].DepthPoint.Y);

                LineSegment2D shapeLine = new LineSegment2D(DefectArray[i].StartPoint, DefectArray[i].DepthPoint);

                CircleF startC = new CircleF(st_P, 5f);
                CircleF depthC = new CircleF(dep_P, 5f);

                if ((startC.Center.Y < judge.Y || depthC.Center.Y < judge.Y) && 
                    (startC.Center.Y < depthC.Center.Y) && 
                    (Math.Sqrt(Math.Pow(startC.Center.X - depthC.Center.X, 2) + 
                    Math.Pow(startC.Center.Y - depthC.Center.Y, 2)) > box.size.Height / 6.5))
                {
                    fingerNum++;
                    Cam_Income.Draw(shapeLine, new Bgr(Color.Green), 2);
                    Cam_Income.Draw(startC, new Bgr(Color.Red), 2);
                    Cam_Income.Draw(depthC, new Bgr(Color.Yellow), 2);
                }
            }

            lblSign.Text = fingerNum.ToString();
            Run_function(fingerNum);
        }

        private void Run_function(int num)
        {
            //Point Cursor_P = Cursor.Position;
            switch (num)
            {
                case 0:
                    //SendMessageW(this.Handle, WM_APPCOMMAND, this.Handle, (IntPtr)APPCOMMAND_VOLUME_MUTE);
                    SendMessageW(this.Handle, WM_APPCOMMAND, this.Handle, (IntPtr)APPCOMMAND_VOLUME_DOWN);
                    break;
                case 1:
                    SendMessageW(this.Handle, WM_APPCOMMAND, this.Handle, (IntPtr)APPCOMMAND_VOLUME_UP);
                    break;
                case 2:
                    Cursor.Position = new Point(Cursor.Position.X, Cursor.Position.Y - 5);
                    break;
                case 3:
                    Cursor.Position = new Point(Cursor.Position.X + 5, Cursor.Position.Y);
                    break;
                case 4:
                    Cursor.Position = new Point(Cursor.Position.X, Cursor.Position.Y + 5);
                    break;
                case 5:
                    Cursor.Position = new Point(Cursor.Position.X - 5, Cursor.Position.Y);
                    break;
            }
        }
    }
}
