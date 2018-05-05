using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

using Coding4Fun.Kinect.Wpf;
using Coding4Fun.Kinect.Wpf.Controls;
using Microsoft.Kinect;
using System.Diagnostics;

namespace KinectHubDemo
{
	public partial class MainWindow : Window
	{
        KinectSensor kinect;

		private List<Button> buttons;
        private Button hoveredButton;

		private bool isWindowsClosing = false;


        /// <summary>
        /// 启动Kinect设备，默认初始化选项，并注册AllFramesReady同步事件
        /// </summary>
        private void startKinect()
        {
            if (KinectSensor.KinectSensors.Count > 0)
            {
                //选择第一个Kinect设备
                kinect = KinectSensor.KinectSensors[0];
                if (kinect == null)
                    return;

                var tsp = new TransformSmoothParameters
                {
                    Smoothing = 0.5f,
                    Correction = 0.5f,
                    Prediction = 0.5f,
                    JitterRadius = 0.05f,
                    MaxDeviationRadius = 0.04f
                };
                kinect.SkeletonStream.Enable(tsp);

                //启用骨骼跟踪，并在屏幕右下方显示彩色视频信息
                kinect.SkeletonFrameReady += new EventHandler<SkeletonFrameReadyEventArgs>(kinect_SkeletonFrameReady);

                //启动Kinect设备
                kinect.Start();
            }
            else
            {
                MessageBox.Show("没有发现任何Kinect设备");
            }
        }

        void kinect_SkeletonFrameReady(object sender, SkeletonFrameReadyEventArgs e)
        {            
            using (SkeletonFrame frame = e.OpenSkeletonFrame())
            {

                if (frame == null)
                    return;

                if (frame.SkeletonArrayLength == 0)
                    return;

                Skeleton[] allSkeletons = new Skeleton[frame.SkeletonArrayLength];
                frame.CopySkeletonDataTo(allSkeletons);

                //Linq语法，查找离Kinect最近的、被跟踪的骨骼，以头部Z坐标为参考
                Skeleton closestSkeleton = (from s in allSkeletons
                                            where s.TrackingState == SkeletonTrackingState.Tracked &&
                                                  s.Joints[JointType.Head].TrackingState == JointTrackingState.Tracked
                                            select s).OrderBy(s => s.Joints[JointType.Head].Position.Z)
                                    .FirstOrDefault();

                if (closestSkeleton == null)
                    return;
                if (closestSkeleton.TrackingState != SkeletonTrackingState.Tracked)
                    return;

                var joints = closestSkeleton.Joints;

                Joint rightHand = joints[JointType.HandRight];
                Joint leftHand = joints[JointType.HandLeft];

                //通过Y轴坐标判断是左手习惯还是右手习惯：举起的那支手的Y轴坐标值更大
                var hand = (rightHand.Position.Y > leftHand.Position.Y)
                                ? rightHand
                                : leftHand;

                if (hand.TrackingState != JointTrackingState.Tracked)
                    return;

                //获得屏幕的宽度和高度
                int screenWidth = (int)SystemParameters.PrimaryScreenWidth;
                int screenHeight = (int)SystemParameters.PrimaryScreenHeight;

                //将部位“手”的骨骼坐标映射为屏幕坐标；手只需要在有限范围内移动即可覆盖整个屏幕区域
                float posX = hand.ScaleTo(screenWidth, screenHeight, 0.2f, 0.2f).Position.X;
                float posY = hand.ScaleTo(screenWidth, screenHeight, 0.2f, 0.2f).Position.Y;
                
                //判断是否悬浮在图片按钮上，有则触发Click事件
                OnButtonLocationChanged(kinectButton, buttons, (int)posX, (int)posY);
            }
        }


		public MainWindow()
		{
			InitializeComponent();

			kinectButton.Click += new RoutedEventHandler(kinectButton_Clicked);
		}


		private void InitializeButtons()
		{
			buttons = new List<Button>
			    {
			        button1,
					button2,
					button3,
					button4,
					button5,
					button6
			    };
		}

        /// <summary>
        /// 悬停选择按钮处理
        /// </summary>
        /// <param name="hand">当前移动的悬浮手型光标</param>
        /// <param name="buttons">图片按钮集合</param>
        /// <param name="X">SkeletonHandX</param>
        /// <param name="Y">SkeletonHandY</param>
		private void OnButtonLocationChanged(HoverButton hand, List<Button> buttons, int X, int Y)
		{
			if (IsButtonOverObject(hand, buttons))
                hand.Hovering(); // 触发Mouse Click事件
            else
                hand.Release();

            //移动手型光标
			Canvas.SetLeft(hand, X - (hand.ActualWidth / 2));
			Canvas.SetTop(hand, Y - (hand.ActualHeight / 2));
		}

		private void kinectButton_Clicked(object sender, RoutedEventArgs e)
		{			
			hoveredButton.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, hoveredButton));
		}

		public bool IsButtonOverObject(FrameworkElement hand, List<Button> buttons)
		{
			if (isWindowsClosing || !Window.GetWindow(hand).IsActive) 
                return false;
                        
		    
			// 找到悬浮手型控件的中心点位置
			var handTopLeft = new Point(Canvas.GetTop(hand), Canvas.GetLeft(hand));
			double handLeft = handTopLeft.X + (hand.ActualWidth / 2);
			double handTop = handTopLeft.Y + (hand.ActualHeight / 2);

            //遍历图片按钮，判断Hand图标是否悬浮在其中之一
			foreach (Button target in buttons)
			{
				Point targetTopLeft = target.PointToScreen(new Point());
				if (handTop > targetTopLeft.X
					&& handTop < targetTopLeft.X + target.ActualWidth
					&& handLeft > targetTopLeft.Y
					&& handLeft < targetTopLeft.Y + target.ActualHeight)
				{
					hoveredButton = target;
					return true;
				}
			}
			return false;
		}

        string target = @"C:\Users\KingChun\Desktop\Hub test\FaceTracking3D-WPF\bin\x86\Debug\FaceTracking3D-WPF.exe";


		private void button1_Click(object sender, RoutedEventArgs e)
		{
            MessageBox.Show("Button 1 Clicked");
		}

		private void button2_Click(object sender, RoutedEventArgs e)
		{
            //MessageBox.Show("Button 2 Clicked");
            ProcessStartInfo pInfo = new ProcessStartInfo(target);

            pInfo.Arguments = "我是參數";

            using (Process p = new Process()) {           

                p.StartInfo = pInfo;

                p.Start();

            }

 


 

		}

		private void button3_Click(object sender, RoutedEventArgs e)
		{
            MessageBox.Show("Button 3 Clicked");
		}

		private void button4_Click(object sender, RoutedEventArgs e)
		{
            MessageBox.Show("Button 4 Clicked");
		}

		private void button5_Click(object sender, RoutedEventArgs e)
		{
            MessageBox.Show("Button 5 Clicked");
		}

		private void button6_Click(object sender, RoutedEventArgs e)
		{
            MessageBox.Show("Button 6 Clicked");
		}

        private void Canvas_Loaded(object sender, RoutedEventArgs e)
        {

        }

        private void stopKinect()
        {
            if (kinect != null)
            {
                if (kinect.Status == KinectStatus.Connected)
                {
                    //关闭Kinect设备
                    kinect.Stop();
                }
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            InitializeButtons();
            startKinect();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            isWindowsClosing = true;
            stopKinect();
        }
	}
}