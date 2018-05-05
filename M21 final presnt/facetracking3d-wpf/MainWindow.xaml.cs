// --------------------------------------------------------------------------------------------------------------------
// <copyright file="MainWindow.xaml.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace FaceTracking3D
{
    using System;
    using System.Windows;
    using System.Windows.Data;
    //face
    using Microsoft.Kinect;
    using Microsoft.Kinect.Toolkit;
    //voice
    using Microsoft.Speech.Recognition;
    using Microsoft.Speech.AudioFormat;
    using System.Threading;
    using System.IO;
    using System.Text;
    using System.Collections.Generic;
    using System.Windows.Documents;
    using System.Windows.Media;
    //hand
    using System.Linq;
    using System.Windows.Controls;
    using System.Windows.Controls.Primitives;
    using Coding4Fun.Kinect.Wpf;
    using Coding4Fun.Kinect.Wpf.Controls;
    using System.Diagnostics;
    using System.Windows.Media.Imaging;
    using System.Media;

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly KinectSensorChooser sensorChooser = new KinectSensorChooser();

        /// <summary>
        /// Speech recognition engine using audio data from Kinect.
        /// </summary>
        private SpeechRecognitionEngine speechEngine;

        /// <summary>
        /// List of all UI span elements used to select recognized text.
        /// </summary>
        private List<Span> recognitionSpans;
        /// <summary>
        /// Resource key for medium-gray-colored brush.
        /// </summary>
        private const string MediumGreyBrushKey = "MediumGreyBrush";
        KinectSensor oldSensor;
        KinectSensor newSensor;
        /// <summary>
        /// Gets the metadata for the speech recognizer (acoustic model) most suitable to
        /// process audio from Kinect device.
        /// </summary>
        /// <returns>
        /// RecognizerInfo if found, <code>null</code> otherwise.
        /// </returns>
        private static RecognizerInfo GetKinectRecognizer()
        {
            foreach (RecognizerInfo recognizer in SpeechRecognitionEngine.InstalledRecognizers())
            {
                string value;
                recognizer.AdditionalInfo.TryGetValue("Kinect", out value);
                if ("True".Equals(value, StringComparison.OrdinalIgnoreCase) && "en-US".Equals(recognizer.Culture.Name, StringComparison.OrdinalIgnoreCase))
                {
                    return recognizer;
                }
            }
            return null;
        }

        private List<Button> buttons;
        private Button hoveredButton;
        private SoundPlayer hulkSound = new SoundPlayer();
        private SoundPlayer shrekSound = new SoundPlayer();
        private SoundPlayer selectionSound = new SoundPlayer();
       
        private bool isWindowsClosing = false;

        public MainWindow()
        {
            this.Closed += this.WindowClosed;
            this.InitializeComponent();

            var kinectSensorBinding = new Binding("Kinect") { Source = this.sensorChooser };
            this.faceTrackingVisualizer.SetBinding(TexturedFaceMeshViewer.KinectProperty, kinectSensorBinding);

            this.sensorChooser.KinectChanged += this.SensorChooserOnKinectChanged;

            kinectButton.Click += new RoutedEventHandler(kinectButton_Clicked);

            hulkSound.Stream = Properties.Resources.hulk;
            shrekSound.Stream = Properties.Resources.shrek;
            selectionSound.Stream = Properties.Resources.selection;
            
            InitializeButtons();

            this.sensorChooser.Start();

        }

        /// <summary>
        /// Setup the sensor for any components in this app that will be using it.
        /// </summary>
        private void SensorChooserOnKinectChanged(object sender, KinectChangedEventArgs kinectChangedEventArgs)
        {
            oldSensor = kinectChangedEventArgs.OldSensor;
            newSensor = kinectChangedEventArgs.NewSensor;

            if (oldSensor != null)
            {
                oldSensor.ColorStream.Disable();
                oldSensor.DepthStream.Disable();
                oldSensor.DepthStream.Range = DepthRange.Default;
                oldSensor.SkeletonStream.Disable();
                oldSensor.SkeletonStream.EnableTrackingInNearRange = false;
                oldSensor.SkeletonStream.TrackingMode = SkeletonTrackingMode.Default;
            }

            if (newSensor != null)
            {
                try
                {
                    newSensor.ColorStream.Enable(ColorImageFormat.RgbResolution640x480Fps30);
                    newSensor.DepthStream.Enable(DepthImageFormat.Resolution320x240Fps30);
                    try
                    {
                        // This will throw on non Kinect For Windows devices.
                        newSensor.DepthStream.Range = DepthRange.Near;
                        newSensor.SkeletonStream.EnableTrackingInNearRange = true;
                    }
                    catch (InvalidOperationException)
                    {
                        newSensor.DepthStream.Range = DepthRange.Default;
                        newSensor.SkeletonStream.EnableTrackingInNearRange = false;
                    }

                    newSensor.SkeletonStream.TrackingMode = SkeletonTrackingMode.Seated;
                    newSensor.SkeletonStream.Enable();
                }
                catch (InvalidOperationException)
                {
                    // This exception can be thrown when we are trying to
                    // enable streams on a device that has gone away.  This
                    // can occur in app shutdown scenarios when the sensor
                    // goes away between the time it changed status and the
                    // time we get the sensor changed notification.
                    //
                    // Behavior here is to just eat the exception and assume
                    // another notification will come along if a sensor
                    // comes back.
                }
            }
            RecognizerInfo ri = GetKinectRecognizer();

            var tsp = new TransformSmoothParameters
            {
                Smoothing = 0.5f,
                Correction = 0.5f,
                Prediction = 0.5f,
                JitterRadius = 0.05f,
                MaxDeviationRadius = 0.04f
            };

            newSensor.SkeletonStream.Enable(tsp);

            newSensor.SkeletonFrameReady += new EventHandler<SkeletonFrameReadyEventArgs>(kinect_SkeletonFrameReady);


            if (null != ri)
            {
                recognitionSpans = new List<Span> { HulkSpan, backSpan, exitSpan, shrekSpan };
                this.speechEngine = new SpeechRecognitionEngine(ri.Id);

                // Create a grammar from grammar definition XML file.
                using (var memoryStream = new MemoryStream(Encoding.ASCII.GetBytes(Properties.Resources.SpeechGrammar)))
                {
                    var g = new Grammar(memoryStream);
                    speechEngine.LoadGrammar(g);
                }

                speechEngine.SpeechRecognized += SpeechRecognized;
                speechEngine.SpeechRecognitionRejected += SpeechRejected;

                // For long recognition sessions (a few hours or more), it may be beneficial to turn off adaptation of the acoustic model. 
                // This will prevent recognition accuracy from degrading over time.
                ////speechEngine.UpdateRecognizerSetting("AdaptationOn", 0);

                speechEngine.SetInputToAudioStream(
                    newSensor.AudioSource.Start(), new SpeechAudioFormatInfo(EncodingFormat.Pcm, 16000, 16, 1, 32000, 2, null));
                speechEngine.RecognizeAsync(RecognizeMode.Multiple);
            }
            else
            {
                this.statusBarText.Text = Properties.Resources.NoSpeechRecognizer;
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

                var hand = (rightHand.Position.Y > leftHand.Position.Y)
                                ? rightHand
                                : leftHand;

                if (hand.TrackingState != JointTrackingState.Tracked)
                    return;

                int screenWidth = (int)SystemParameters.PrimaryScreenWidth;
                int screenHeight = (int)SystemParameters.PrimaryScreenHeight;

                float posX = hand.ScaleTo(screenWidth, screenHeight, 0.2f, 0.2f).Position.X;
                float posY = hand.ScaleTo(screenWidth, screenHeight, 0.2f, 0.2f).Position.Y;

                OnButtonLocationChanged(kinectButton, buttons, (int)posX, (int)posY);
            }
        }

        private void InitializeButtons()
        {
            buttons = new List<Button>
			    {
			        button1,
					button2,
                    button3,
                    button4,
                    Exit
			    };
        }

        /// <param name="X">SkeletonHandX</param>
        /// <param name="Y">SkeletonHandY</param>
        private void OnButtonLocationChanged(HoverButton hand, List<Button> buttons, int X, int Y)
        {
            if (IsButtonOverObject(hand, buttons))
                hand.Hovering(); 
            else
                hand.Release();

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


            var handTopLeft = new Point(Canvas.GetTop(hand), Canvas.GetLeft(hand));
            double handLeft = handTopLeft.X + (hand.ActualWidth / 2);
            double handTop = handTopLeft.Y + (hand.ActualHeight / 2);
            //Debug.Print("HandLeft:{0}, HandTop:{1}", handTopLeft.X, handTopLeft.Y);
            //Debug.Print("HandLeft:{0}, HandTop:{1}", handLeft, handTop);

            //check if the hand is on the pic
            foreach (Button target in buttons)
            {
                Point targetTopLeft = target.PointToScreen(new Point());
                if (handTop > (targetTopLeft.X * 1280 / 1440)
                    && handTop < (targetTopLeft.X * 1280 / 1440) + target.ActualWidth
                    && handLeft > (targetTopLeft.Y * 720 / 900)
                    && handLeft < (targetTopLeft.Y * 720 / 900) + target.ActualHeight)
                {
                    hoveredButton = target;
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Handler for recognized speech events.
        /// </summary>
        /// <param name="sender">object sending the event.</param>
        /// <param name="e">event arguments.</param>
        private void SpeechRecognized(object sender, SpeechRecognizedEventArgs e)
        {
            // Speech utterance confidence below which we treat speech as if it hadn't been heard
            const double ConfidenceThreshold = 0.3;

            ClearRecognitionHighlights();

            //MessageBox.Show("here");

            if (e.Result.Confidence >= ConfidenceThreshold)
            {
                switch (e.Result.Semantics.Value.ToString())
                {
                    case "BACK":
                        backSpan.Foreground = Brushes.DeepSkyBlue;
                        backSpan.FontWeight = FontWeights.Bold;
                        this.faceTrackingVisualizer.changer = 0;
                        this.Background = new ImageBrush(new BitmapImage(new Uri(@"pack://application:,,,/FaceTracking3D-WPF;component/Images/black.png")));
                        this.selectionSound.Play();
                        break;

                    case "HULK":
                        HulkSpan.Foreground = Brushes.DeepSkyBlue;
                        HulkSpan.FontWeight = FontWeights.Bold;
                        this.faceTrackingVisualizer.changer = 1;
                        this.Background = new ImageBrush(new BitmapImage(new Uri(@"pack://application:,,,/FaceTracking3D-WPF;component/Images/hulkBackground.png")));
                        this.hulkSound.Play();
                        break;

                    case "SHREK":
                        shrekSpan.Foreground = Brushes.DeepSkyBlue;
                        shrekSpan.FontWeight = FontWeights.Bold;
                        this.faceTrackingVisualizer.changer = 2;
                        this.Background = new ImageBrush(new BitmapImage(new Uri(@"pack://application:,,,/FaceTracking3D-WPF;component/Images/shrekBackground.png")));
                        this.shrekSound.Play();
                        break;

                    case "EXIT":
                        //this.Close();
                        break;
                }
            }
        }
        private void ClearRecognitionHighlights()
        {
            foreach (Span span in recognitionSpans)
            {
                span.Foreground = (Brush)this.Resources[MediumGreyBrushKey];
                span.FontWeight = FontWeights.Normal;
            }
        }
        /// <summary>
        /// Handler for rejected speech events.
        /// </summary>
        /// <param name="sender">object sending the event.</param>
        /// <param name="e">event arguments.</param>
        private void SpeechRejected(object sender, SpeechRecognitionRejectedEventArgs e)
        {
            ClearRecognitionHighlights();
        }

        private void WindowClosed(object sender, EventArgs e)
        {
            this.sensorChooser.Stop();
            this.faceTrackingVisualizer.Dispose();
            isWindowsClosing = true;

       

            if (null != this.speechEngine)
            {
                this.speechEngine.SpeechRecognized -= SpeechRecognized;
                this.speechEngine.SpeechRecognitionRejected -= SpeechRejected;
                this.speechEngine.RecognizeAsyncStop();
            }
        }

        private void button1_Click(object sender, RoutedEventArgs e)
        {
            //MessageBox.Show("Button 1 Clicked");
            ClearRecognitionHighlights();
            this.faceTrackingVisualizer.changer = 0;
            backSpan.Foreground = Brushes.DeepSkyBlue;
            backSpan.FontWeight = FontWeights.Bold;
            this.Background = new ImageBrush(new BitmapImage(new Uri(@"pack://application:,,,/FaceTracking3D-WPF;component/Images/black.png")));
            this.selectionSound.Play();
        }

        private void hulkBtn_Click(object sender, RoutedEventArgs e)
        {
            //MessageBox.Show("Button 2 Clicked");
            ClearRecognitionHighlights();
            this.faceTrackingVisualizer.changer = 1;
            HulkSpan.Foreground = Brushes.DeepSkyBlue;
            HulkSpan.FontWeight = FontWeights.Bold;
            this.Background = new ImageBrush(new BitmapImage(new Uri(@"pack://application:,,,/FaceTracking3D-WPF;component/Images/hulkBackground.png")));
            this.hulkSound.Play();
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            ClearRecognitionHighlights();
            this.Close();
        }

        private void button3_Click(object sender, RoutedEventArgs e)
        {
            ClearRecognitionHighlights();
            this.faceTrackingVisualizer.changer = 2;
            shrekSpan.Foreground = Brushes.DeepSkyBlue;
            shrekSpan.FontWeight = FontWeights.Bold;
            this.Background = new ImageBrush(new BitmapImage(new Uri(@"pack://application:,,,/FaceTracking3D-WPF;component/Images/shrekBackground.png")));
            this.shrekSound.Play();
        }

        private void _3D_Click(object sender, RoutedEventArgs e)
        {
            _3DOutputStream.output(TexturedFaceMeshViewer.getPosition(), TexturedFaceMeshViewer.getTextureCoordinates(), TexturedFaceMeshViewer.getNormals(), TexturedFaceMeshViewer.getTriangleIndices());
            MessageBox.Show("File created, Please check if the file exists.");
        }

    }
}