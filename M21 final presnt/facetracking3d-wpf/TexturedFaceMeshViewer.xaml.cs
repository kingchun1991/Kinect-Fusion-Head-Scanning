// --------------------------------------------------------------------------------------------------------------------
// <copyright file="TexturedFaceMeshViewer.xaml.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace FaceTracking3D
{
    using System;
    using System.Diagnostics;
    using System.Linq;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Media;
    using System.Windows.Media.Imaging;
    using System.Windows.Media.Media3D;
    using Microsoft.Kinect;
    using Microsoft.Kinect.Toolkit.FaceTracking;

    using Point = System.Windows.Point;
    using System.Media;

    /// <summary>
    /// Interaction logic for TexturedFaceMeshViewer.xaml
    /// </summary>
    /// 
    
    public partial class TexturedFaceMeshViewer : UserControl, IDisposable
    {
        public static readonly DependencyProperty KinectProperty = DependencyProperty.Register(
            "Kinect",
            typeof(KinectSensor),
            typeof(TexturedFaceMeshViewer),
            new UIPropertyMetadata(
                null,
                (o, args) =>
                ((TexturedFaceMeshViewer)o).OnKinectChanged((KinectSensor)args.OldValue, (KinectSensor)args.NewValue)));

        private static readonly int Bgr32BytesPerPixel = (PixelFormats.Bgr32.BitsPerPixel + 7) / 8;

        private byte[] colorImage;

        private ColorImageFormat colorImageFormat = ColorImageFormat.Undefined;

        private WriteableBitmap colorImageWritableBitmap;

        private short[] depthImage;

        private DepthImageFormat depthImageFormat = DepthImageFormat.Undefined;

        private FaceTracker faceTracker;

        private Skeleton[] skeletonData;

        private int trackingId = -1;

        private FaceTriangle[] triangleIndices;

        private int change = 0;

        public int changer
        {
            // This is your getter.
            // it uses the accessibility of the property (public)
            get
            {
                return change;
            }
            // this is your setter
            // Note: you can specifiy different accessibility
            // for your getter and setter.
             set
        {
            // You can put logic into your getters and setters
            // since they actually map to functions behind the scenes
                // The input of the setter is always called "value"
                // and is of the same type as your property definition
                change = value;
                //Debug.Print("Change1: {0}", change);
        }
        }

        private static Point3DCollection v = null;

        private static PointCollection vt = null;

        private static Point3DCollection vn = null;

        private static Int32Collection f = null;

        private SoundPlayer recognizedSound = new SoundPlayer();

        public TexturedFaceMeshViewer()
        {
            recognizedSound.Stream = Properties.Resources.recognized;
            this.DataContext = this;
            this.InitializeComponent();
        }

        public KinectSensor Kinect
        {
            get
            {
                return (KinectSensor)this.GetValue(KinectProperty);
            }

            set
            {
                this.SetValue(KinectProperty, value);
            }
        }

        public static void setPosition(Point3DCollection _v)
        {
            v = _v;
        }
        public static void setTextureCoordinates(PointCollection _vt)
        {
            vt = _vt;
        }
        public static void setNormals(Point3DCollection _vn)
        {
            vn = _vn;
        }
        public static void setTriangleIndices(Int32Collection _f)
        {
            f = _f;
        }

        public static Point3DCollection getPosition()
        {
            return v;
        }
        public static PointCollection getTextureCoordinates()
        {
            return vt;
        }
        public static Point3DCollection getNormals()
        {
            return vn;
        }
        public static Int32Collection getTriangleIndices()
        {
            return f;
        }

        public void Dispose()
        {
            this.DestroyFaceTracker();
        }

        private void AllFramesReady(object sender, AllFramesReadyEventArgs allFramesReadyEventArgs)
        {
            ColorImageFrame colorImageFrame = null;
            DepthImageFrame depthImageFrame = null;
            SkeletonFrame skeletonFrame = null;

            try
            {
                colorImageFrame = allFramesReadyEventArgs.OpenColorImageFrame();
                depthImageFrame = allFramesReadyEventArgs.OpenDepthImageFrame();
                skeletonFrame = allFramesReadyEventArgs.OpenSkeletonFrame();

                if (colorImageFrame == null || depthImageFrame == null || skeletonFrame == null)
                {
                    return;
                }

                // Check for changes in any of the data this function is receiving
                // and reset things appropriately.
                if (this.depthImageFormat != depthImageFrame.Format)
                {
                    this.DestroyFaceTracker();
                    this.depthImage = null;
                    this.depthImageFormat = depthImageFrame.Format;
                }

                if (this.colorImageFormat != colorImageFrame.Format)
                {
                    this.DestroyFaceTracker();
                    this.colorImage = null;
                    this.colorImageFormat = colorImageFrame.Format;
                    this.colorImageWritableBitmap = null;
                    this.ColorImage.Source = null;
                    this.theMaterial.Brush = null;
                }

                if (this.skeletonData != null && this.skeletonData.Length != skeletonFrame.SkeletonArrayLength)
                {
                    this.skeletonData = null;
                }

                // Create any buffers to store copies of the data we work with
                if (this.depthImage == null)
                {
                    this.depthImage = new short[depthImageFrame.PixelDataLength];
                }

                if (this.colorImage == null)
                {
                    this.colorImage = new byte[colorImageFrame.PixelDataLength];
                }

                if (this.colorImageWritableBitmap == null)
                {
                    this.colorImageWritableBitmap = new WriteableBitmap(
                        colorImageFrame.Width, colorImageFrame.Height, 96, 96, PixelFormats.Bgr32, null);
                    this.ColorImage.Source = this.colorImageWritableBitmap;
                    this.theMaterial.Brush = new ImageBrush(this.colorImageWritableBitmap)
                    {
                        ViewportUnits = BrushMappingMode.Absolute
                    };
                }

                if (this.skeletonData == null)
                {
                    this.skeletonData = new Skeleton[skeletonFrame.SkeletonArrayLength];
                }

                // Copy data received in this event to our buffers.
                colorImageFrame.CopyPixelDataTo(this.colorImage);
                depthImageFrame.CopyPixelDataTo(this.depthImage);
                skeletonFrame.CopySkeletonDataTo(this.skeletonData);
                this.colorImageWritableBitmap.WritePixels(
                    new Int32Rect(0, 0, colorImageFrame.Width, colorImageFrame.Height),
                    this.colorImage,
                    colorImageFrame.Width * Bgr32BytesPerPixel,
                    0);

                // Find a skeleton to track.
                // First see if our old one is good.
                // When a skeleton is in PositionOnly tracking state, don't pick a new one
                // as it may become fully tracked again.
                Skeleton skeletonOfInterest =
                    this.skeletonData.FirstOrDefault(
                        skeleton =>
                        skeleton.TrackingId == this.trackingId
                        && skeleton.TrackingState != SkeletonTrackingState.NotTracked);

                if (skeletonOfInterest == null)
                {
                    // Old one wasn't around.  Find any skeleton that is being tracked and use it.
                    skeletonOfInterest =
                        this.skeletonData.FirstOrDefault(
                            skeleton => skeleton.TrackingState == SkeletonTrackingState.Tracked);

                    if (skeletonOfInterest != null)
                    {
                        // This may be a different person so reset the tracker which
                        // could have tuned itself to the previous person.
                        if (this.faceTracker != null)
                        {
                            this.faceTracker.ResetTracking();
                        }

                        this.trackingId = skeletonOfInterest.TrackingId;
                    }
                }

                bool displayFaceMesh = false;

                if (skeletonOfInterest != null && skeletonOfInterest.TrackingState == SkeletonTrackingState.Tracked)
                {
                    if (this.faceTracker == null)
                    {
                        try
                        {
                            this.faceTracker = new FaceTracker(this.Kinect);
                        }
                        catch (InvalidOperationException)
                        {
                            // During some shutdown scenarios the FaceTracker
                            // is unable to be instantiated.  Catch that exception
                            // and don't track a face.
                            Debug.WriteLine("AllFramesReady - creating a new FaceTracker threw an InvalidOperationException");
                            this.faceTracker = null;
                        }
                    }

                    if (this.faceTracker != null)
                    {
                        FaceTrackFrame faceTrackFrame = this.faceTracker.Track(
                            this.colorImageFormat,
                            this.colorImage,
                            this.depthImageFormat,
                            this.depthImage,
                            skeletonOfInterest);
                            
                        if (faceTrackFrame.TrackSuccessful)
                        {
                            
                            this.UpdateMesh(faceTrackFrame);
                            // Only display the face mesh if there was a successful track.
                            displayFaceMesh = true;
                        }
                    }
                }
                else
                {
                    this.trackingId = -1;
                }

                this.viewport3d.Visibility = displayFaceMesh ? Visibility.Visible : Visibility.Hidden;
            }
            finally
            {
                if (colorImageFrame != null)
                {
                    colorImageFrame.Dispose();
                }

                if (depthImageFrame != null)
                {
                    depthImageFrame.Dispose();
                }

                if (skeletonFrame != null)
                {
                    skeletonFrame.Dispose();
                }
            }
        }

        private void DestroyFaceTracker()
        {
            if (this.faceTracker != null)
            {
                this.faceTracker.Dispose();
                this.faceTracker = null;
            }
        }

        private void OnKinectChanged(KinectSensor oldSensor, KinectSensor newSensor)
        {
            if (oldSensor != null)
            {
                try
                {
                    oldSensor.AllFramesReady -= this.AllFramesReady;

                    this.DestroyFaceTracker();
                }
                catch (InvalidOperationException)
                {
                    // KinectSensor might enter an invalid state while enabling/disabling streams or stream features.
                    // E.g.: sensor might be abruptly unplugged.
                }
            }

            if (newSensor != null)
            {
                try
                {
                    this.faceTracker = new FaceTracker(this.Kinect);

                    newSensor.AllFramesReady += this.AllFramesReady;
                }
                catch (InvalidOperationException)
                {
                    // KinectSensor might enter an invalid state while enabling/disabling streams or stream features.
                    // E.g.: sensor might be abruptly unplugged.
                }
            }
        }

        

        private void UpdateMesh(FaceTrackFrame faceTrackingFrame)
        {
            
            EnumIndexableCollection<FeaturePoint, Vector3DF> shapePoints = faceTrackingFrame.Get3DShape();
            EnumIndexableCollection<FeaturePoint, PointF> projectedShapePoints = faceTrackingFrame.GetProjected3DShape();
            

            if (this.triangleIndices == null)
            {
                // Update stuff that doesn't change from frame to frame
                this.triangleIndices = faceTrackingFrame.GetTriangles();
                var indices = new Int32Collection(this.triangleIndices.Length * 3);
                foreach (FaceTriangle triangle in this.triangleIndices)
                {
                    indices.Add(triangle.Third);
                    indices.Add(triangle.Second);
                    indices.Add(triangle.First);
                }

                this.recognizedSound.Play();


                this.theGeometry.TriangleIndices = indices;
                this.theGeometry.Normals = null; // Let WPF3D calculate these.

                this.theGeometry.Positions = new Point3DCollection(shapePoints.Count);
                this.theGeometry.TextureCoordinates = new PointCollection(projectedShapePoints.Count);
                for (int pointIndex = 0; pointIndex < shapePoints.Count; pointIndex++)
                {
                    this.theGeometry.Positions.Add(new Point3D());
                    this.theGeometry.TextureCoordinates.Add(new Point());
                    if (pointIndex == 0)
                    {
                        Debug.Print("{0}: ({1}, {2}, {3})", ((FeaturePoint)pointIndex).ToString(), shapePoints[pointIndex].X, shapePoints[pointIndex].Y, shapePoints[pointIndex].Z);
                    }
                    if (pointIndex == 10)
                    {
                        Debug.Print("{0}: ({1}, {2}, {3})", ((FeaturePoint)pointIndex).ToString(), shapePoints[pointIndex].X, shapePoints[pointIndex].Y, shapePoints[pointIndex].Z);
                    }
                    if (pointIndex == 20)
                    {
                        Debug.Print("{0}: ({1}, {2}, {3})", ((FeaturePoint)pointIndex).ToString(), shapePoints[pointIndex].X, shapePoints[pointIndex].Y, shapePoints[pointIndex].Z);
                    }
                    if (pointIndex == 53)
                    {
                        Debug.Print("{0}: ({1}, {2}, {3})", ((FeaturePoint)pointIndex).ToString(), shapePoints[pointIndex].X, shapePoints[pointIndex].Y, shapePoints[pointIndex].Z);
                    }
                }
            }
            //Debug.Print("Change2: {0}",change);
            switch (change)
            {
                case 0:
                    this.theLight.Color = System.Windows.Media.Brushes.FloralWhite.Color;
                    for (int pointIndex = 0; pointIndex < shapePoints.Count; pointIndex++)
                    {
                        Vector3DF point = shapePoints[pointIndex];
                        this.theGeometry.Positions[pointIndex] = new Point3D(point.X, point.Y, -point.Z);

                        PointF projected = projectedShapePoints[pointIndex];

                        this.theGeometry.TextureCoordinates[pointIndex] =
                            new Point(
                                projected.X / (double)this.colorImageWritableBitmap.PixelWidth,
                                projected.Y / (double)this.colorImageWritableBitmap.PixelHeight);
                    }
                    break;
                #region Hulk face
                case 1://hulk
                    this.theLight.Color = System.Windows.Media.Brushes.ForestGreen.Color;
                    float hulkScaleY = 0.006689972f;
                    float hulkScaleX = 0.004966908f;
                 
                    //MessageBox.Show(hulkScaleX.ToString("R"));
                    for (int pointIndex = 0; pointIndex < shapePoints.Count; pointIndex++)
               
                    {
                        //Debug.Print("Hey");
                        Vector3DF point = /*new Vector3DF(0.2f,02f,0.2f);*/shapePoints[pointIndex];
                        // Debug.Write("{"+point.X+" "+point.Y+" "+-point.Z+"}");
                        switch (pointIndex)
                        {
                            case 0:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X, point.Y, -point.Z);
                                break;
                            case 35:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X, point.Y - (3 * hulkScaleY), -point.Z);
                                break;
                            case 36:
                                 this.theGeometry.Positions[pointIndex] = new Point3D(point.X, point.Y - (0.5 * hulkScaleY), -point.Z);
                                break;
                            case 94:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X, point.Y + (0.5 * hulkScaleY), -point.Z);
                                break;
                            case 38:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X, point.Y + (1.5 * hulkScaleY), -point.Z);
                                break;
                            case 7:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X, point.Y - (0.5 * hulkScaleY), -point.Z);
                                break;
                            case 87:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X, point.Y - (0.5 * hulkScaleY), -point.Z);
                                break;
                            case 40:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X, point.Y - (0.5 * hulkScaleY), -point.Z);
                                break;
                            case 8:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X, point.Y - (0.5 * hulkScaleY), -point.Z);
                                break;
                            case 9:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X, point.Y - (0.5 * hulkScaleY), -point.Z);
                                break;
                            case 41:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X, point.Y - (0.5 * hulkScaleY), -point.Z);
                                break;
                            case 42:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X, point.Y - hulkScaleY, -point.Z);
                                break;
                            case 10:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X, point.Y + hulkScaleY, -point.Z);
                                break;
                            case 11:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X - (0.5 * hulkScaleX), point.Y - (0.5 * hulkScaleY), -point.Z);
                                break;
                            case 12:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X, point.Y, -point.Z);
                                break;
                            case 1:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X - (2 * hulkScaleX), point.Y + hulkScaleY, -point.Z);
                                break;
                            case 34:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X - (0.5 * hulkScaleX), point.Y + (0.5 * hulkScaleY), -point.Z);
                                break;
                            case 44:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X, point.Y - (0.5 * hulkScaleY), -point.Z);
                                break;
                            case 13:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X, point.Y - (2.5 * hulkScaleY), -point.Z);
                                break;
                            case 45:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X + (0.5 * hulkScaleX), point.Y - (0.5 * hulkScaleY), -point.Z);
                                break;
                            case 46:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X, point.Y - (2.5 * hulkScaleY), -point.Z);
                                break;
                            case 14:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X - hulkScaleX, point.Y - hulkScaleY, -point.Z);
                                break;
                            case 47:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X + (0.5 * hulkScaleX), point.Y - hulkScaleY, -point.Z);
                                break;
                            case 15:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X , point.Y - (3.5 * hulkScaleY), -point.Z);
                                break;
                            case 16:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X - (1.5 * hulkScaleX), point.Y - (2 * hulkScaleY), -point.Z);
                                break;
                            case 17:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X - (2.5 * hulkScaleX), point.Y - (0.5 * hulkScaleY), -point.Z);
                                break;
                            case 18:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X - (0.5 * hulkScaleX), point.Y - (2.5 * hulkScaleY), -point.Z);
                                break;
                            case 48:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X + (0.5 * hulkScaleX), point.Y - (0.5 * hulkScaleY), -point.Z);
                                break;
                            case 49:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X , point.Y - (2 * hulkScaleY), -point.Z);
                                break;
                            case 50:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X + hulkScaleX, point.Y - (2.5 * hulkScaleY) + hulkScaleY, -point.Z);
                                break;
                            case 51:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X - hulkScaleX, point.Y - (2.5 * hulkScaleY), -point.Z);
                                break;
                            case 20:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X -(0.5* hulkScaleX), point.Y - (0.5 * hulkScaleY), -point.Z);
                                break;
                            case 19:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X + (0.5* hulkScaleX), point.Y - (1.5 * hulkScaleY), -point.Z);
                                break;
                            case 21:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X + (0.5* hulkScaleX), point.Y - (1.5 * hulkScaleY), -point.Z);
                                break;
                            case 23:
                                 this.theGeometry.Positions[pointIndex] = new Point3D(point.X , point.Y - hulkScaleY, -point.Z);
                                break;
                            case 24:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X , point.Y - (1.5 * hulkScaleY), -point.Z);
                                break;
                            case 95:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X + hulkScaleX, point.Y - (1.5 * hulkScaleY), -point.Z);
                                break;
                            case 97:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X + hulkScaleX, point.Y - (1.5 * hulkScaleY), -point.Z);
                                break;
                            case 99:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X + hulkScaleX, point.Y - hulkScaleY, -point.Z);
                                break;
                            case 101:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X + hulkScaleX, point.Y - (1.5 * hulkScaleY), -point.Z);
                                break;
                            case 67:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X + (0.5 * hulkScaleX), point.Y - (0.5 * hulkScaleY), -point.Z);
                                break;
                            case 22:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X + hulkScaleX, point.Y - (1.5 * hulkScaleY), -point.Z);
                                break;
                            case 71:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X + hulkScaleX, point.Y - (1.5 * hulkScaleY), -point.Z);
                                break;
                            case 72:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X + hulkScaleX, point.Y - hulkScaleY, -point.Z);
                                break;
                            case 103:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X + (0.5 * hulkScaleX), point.Y - hulkScaleY, -point.Z);
                                break;
                            case 105:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X + (0.5 * hulkScaleX), point.Y - hulkScaleY, -point.Z);
                                break;
                            case 107:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X + (0.5 * hulkScaleX), point.Y -(1.5* hulkScaleY), -point.Z);
                                break;
                            case 109:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X + (0.5 * hulkScaleX), point.Y -(1.5* hulkScaleY), -point.Z);
                                break;
                            case 56:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X + hulkScaleX, point.Y - hulkScaleY, -point.Z);
                                break;
                            case 52:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X + (0.5 * hulkScaleX), point.Y - hulkScaleY, -point.Z);
                                break;
                            case 54:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X + (0.5 * hulkScaleX), point.Y - hulkScaleY, -point.Z);
                                break;
                            case 53:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X - (0.5 * hulkScaleX), point.Y - hulkScaleY, -point.Z);
                                break;
                            case 55:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X - (0.5 * hulkScaleX), point.Y - hulkScaleY, -point.Z);
                                break;
                            case 57:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X - (0.5 * hulkScaleX), point.Y - hulkScaleY, -point.Z);
                                break;
                            case 104:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X , point.Y - (0.5 * hulkScaleY), -point.Z);
                                break;
                            case 106:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X , point.Y - (0.5 * hulkScaleY), -point.Z);
                                break;
                            case 73:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X -(0.5 * hulkScaleX), point.Y - hulkScaleY, -point.Z);
                                break;
                            case 69:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X + (0.5 * hulkScaleX) , point.Y - hulkScaleY, -point.Z);
                                break;
                            case 96:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X - (0.5 * hulkScaleX) , point.Y -(0.5* hulkScaleY), -point.Z);
                                break;
                            case 98:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X - (0.5 * hulkScaleX) , point.Y -(0.5* hulkScaleY), -point.Z);
                                break;
                            case 108:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X + (0.5 * hulkScaleX) , point.Y -(1.5* hulkScaleY), -point.Z);
                                break;
                            case 110:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X + (0.5 * hulkScaleX) , point.Y -(1.5* hulkScaleY), -point.Z);
                                break;
                            case 70:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X - (0.5 * hulkScaleX) , point.Y -(0.5* hulkScaleY), -point.Z);
                                break;
                            case 100:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X - hulkScaleX , point.Y -(1.5* hulkScaleY), -point.Z);
                                break;
                            case 102:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X - hulkScaleX , point.Y -(1.5* hulkScaleY), -point.Z);
                                break;
                            case 27:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X - (1.5 * hulkScaleX) , point.Y, -point.Z);
                                break;
                            case 60:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X + (2  *hulkScaleX) , point.Y, -point.Z);
                                break;
                            case 28:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X - (0.5 * hulkScaleX) , point.Y -(2.5* hulkScaleY), -point.Z);
                                break;
                            case 61:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X + (0.5 * hulkScaleX), point.Y - (2.5 * hulkScaleY), -point.Z);
                                break;
                            case 77:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X - hulkScaleX, point.Y + (0.5 * hulkScaleY), -point.Z);
                                break;
                            case 78:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X + hulkScaleX, point.Y + (0.5 * hulkScaleY), -point.Z);
                                break;
                            case 92:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X + (0.5 * hulkScaleX), point.Y, -point.Z);
                                break;
                            case 93:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X - (0.5 * hulkScaleX), point.Y, -point.Z);
                                break;
                            case 25:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X -(0.5 * hulkScaleX), point.Y + (2.5 * hulkScaleY), -point.Z);
                                break;
                            case 58:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X +(0.5 * hulkScaleX), point.Y + (2.5 * hulkScaleY), -point.Z);
                                break;
                            case 26:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X -(0.5 * hulkScaleX), point.Y + (2.5 * hulkScaleY), -point.Z);
                                break;
                            case 75:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X, point.Y + (2 * hulkScaleY), -point.Z);
                                break;
                            case 76:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X, point.Y + (2 * hulkScaleY), -point.Z);
                                break;
                            case 59:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X +(0.5 * hulkScaleX), point.Y + (2.5 * hulkScaleY), -point.Z);
                                break;
                            case 111:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X +(0.5 * hulkScaleX), point.Y + (2.5 * hulkScaleY), -point.Z);
                                break;
                            case 112:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X -(0.5 * hulkScaleX), point.Y + (2.5 * hulkScaleY), -point.Z);
                                break;
                            case 33:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X - (2 * hulkScaleX), point.Y, -point.Z);
                                break;
                            case 66:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X - (2 * hulkScaleX), point.Y, -point.Z);
                                break;
                            case 31:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X - (2.5 * hulkScaleX), point.Y, -point.Z);
                                break;
                            case 64:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X - (2.5 * hulkScaleX), point.Y, -point.Z);
                                break;
                            case 88:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X - (2 * hulkScaleX), point.Y - hulkScaleY, -point.Z);
                                break;
                            case 89:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X + (2 * hulkScaleX), point.Y - hulkScaleY, -point.Z);
                                break;
                            case 83:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X - (1.5 * hulkScaleX), point.Y - (0.5 * hulkScaleY), -point.Z);
                                break;
                            case 84:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X + (1.5 * hulkScaleX), point.Y - (0.5 * hulkScaleY), -point.Z);
                                break;
                            case 85:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X , point.Y - (0.5 * hulkScaleY), -point.Z);
                                break;
                            case 86:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X , point.Y - (0.5 * hulkScaleY), -point.Z);
                                break;
                            case 90:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X - (1.5 * hulkScaleX), point.Y , -point.Z);
                                break;
                            case 91:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X + (1.5 * hulkScaleX), point.Y , -point.Z);
                                break;
                            case 30:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X - hulkScaleX, point.Y + (0.5 * hulkScaleY), -point.Z);
                                break;
                            case 63:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X + hulkScaleX, point.Y + (0.5 * hulkScaleY), -point.Z);
                                break;
                            default:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X, point.Y, -point.Z);
                                break;
                        }
                        PointF projected = projectedShapePoints[pointIndex];
                        this.theGeometry.TextureCoordinates[pointIndex] = new Point(//0, 0);
                        projected.X / (double)this.colorImageWritableBitmap.PixelWidth, 
                        projected.Y / (double)this.colorImageWritableBitmap.PixelHeight);
                        /*new Point(rand.NextDouble(),rand.NextDouble());*/

                        //Debug.Print("X:{0} Y:{1}", rand.NextDouble(), rand.NextDouble());
                    }
                    break;
                #endregion
                #region Shrek face
                case 2:
                    this.theLight.Color = System.Windows.Media.Brushes.LightGreen.Color;
                    float shrekScaleX = 0.005439947f;
                    float shrekScaleY = 0.005547781f;
                    for (int pointIndex = 0; pointIndex < shapePoints.Count; pointIndex++)
                    {
                        //Debug.Print("Hey");
                        Vector3DF point = /*new Vector3DF(0.2f,02f,0.2f);*/shapePoints[pointIndex];
                        // Debug.Write("{"+point.X+" "+point.Y+" "+-point.Z+"}");
                        switch (pointIndex)
                        {
                            case 0:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X, point.Y - (0.5 * shrekScaleY), -point.Z);
                                break;
                            case 35:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X, point.Y + (2 * shrekScaleY), -point.Z);
                                break;
                            case 36:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X, point.Y + (5 * shrekScaleY) , -point.Z);
                                break;
                            case 94:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X, point.Y + (8 * shrekScaleY) , -point.Z);
                                break;
                            case 38:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X, point.Y + (9 * shrekScaleY) , -point.Z);
                                break;
                            case 7:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X, point.Y + (7.5 * shrekScaleY) , -point.Z);
                                break;
                            case 87:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X, point.Y + (2.5 * shrekScaleY) , -point.Z);
                                break;
                            case 40:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X, point.Y + (4 * shrekScaleY) , -point.Z);
                                break;
                            case 8:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X, point.Y + (4 * shrekScaleY) , -point.Z);
                                break;
                            case 9:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X, point.Y + (4 * shrekScaleY) , -point.Z);
                                break;
                            case 41:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X, point.Y + (4.5 * shrekScaleY) , -point.Z);
                                break;
                            case 42:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X, point.Y + (5.5 * shrekScaleY) , -point.Z);
                                break;
                            case 10:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X, point.Y , -point.Z);
                                break;
                            case 11:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X - (0.5 * shrekScaleX), point.Y - (0.5 * shrekScaleY) , -point.Z);
                                break;
                            case 44:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X + (0.5 * shrekScaleX), point.Y - (0.5 * shrekScaleY) , -point.Z);
                                break;
                            case 12:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X + (0.5 * shrekScaleX), point.Y + (0.5 * shrekScaleY) , -point.Z);
                                break;
                            case 45:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X - (0.5 * shrekScaleX), point.Y + (0.5 * shrekScaleY) , -point.Z);
                                break;
                            case 1:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X + (0.5 * shrekScaleX), point.Y + (0.5 * shrekScaleY) , -point.Z);
                                break;
                            case 34:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X - (0.5 * shrekScaleX), point.Y + (0.5 * shrekScaleY) , -point.Z);
                                break;
                            case 14:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X - shrekScaleX, point.Y + shrekScaleY , -point.Z);
                                break;
                            case 47:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X + shrekScaleX, point.Y + shrekScaleY , -point.Z);
                                break;
                            case 13:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X + (2 * shrekScaleX), point.Y + (3.5 * shrekScaleY) , -point.Z);
                                break;
                            case 46:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X - (2 * shrekScaleX), point.Y + (3.5 * shrekScaleY) , -point.Z);
                                break;
                            case 20:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X + shrekScaleX, point.Y + (5 * shrekScaleY) , -point.Z);
                                break;
                            case 53:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X - shrekScaleX, point.Y + (5 * shrekScaleY) , -point.Z);
                                break;
                            case 23:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X + (1.5 * shrekScaleX), point.Y + (5.5 * shrekScaleY) , -point.Z);
                                break;
                            case 56:   
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X + (1.5 * shrekScaleX), point.Y + (5.5 * shrekScaleY) , -point.Z);
                                break;
                            case 19:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X, point.Y + (5 * shrekScaleY) , -point.Z);
                                break;
                            case 21:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X, point.Y + (5 * shrekScaleY) , -point.Z);
                                break;
                            case 52:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X, point.Y + (5 * shrekScaleY) , -point.Z);
                                break;
                            case 54:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X, point.Y + (5 * shrekScaleY) , -point.Z);
                                break;
                            case 22:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X, point.Y + (5 * shrekScaleY) , -point.Z);
                                break;
                            case 24:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X, point.Y + (5 * shrekScaleY) , -point.Z);
                                break;
                            case 55: 
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X, point.Y + (5 * shrekScaleY) , -point.Z);
                                break;
                            case 57:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X, point.Y + (5 * shrekScaleY) , -point.Z);
                                break;
                            case 95:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X, point.Y + (5 * shrekScaleY) , -point.Z);
                                break;
                            case 97:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X, point.Y + (5 * shrekScaleY) , -point.Z);
                                break;
                            case 96:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X, point.Y + (5 * shrekScaleY) , -point.Z);
                                break;
                            case 98:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X, point.Y + (5 * shrekScaleY) , -point.Z);
                                break;
                            case 67:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X, point.Y + (5 * shrekScaleY) , -point.Z);
                                break;
                            case 69:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X, point.Y + (5 * shrekScaleY) , -point.Z);
                                break;
                            case 99:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X , point.Y + (4.5 * shrekScaleY) , -point.Z);
                                break;
                            case 101:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X , point.Y + (4.5 * shrekScaleY) , -point.Z);
                                break;
                            case 100:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X , point.Y + (4.5 * shrekScaleY) , -point.Z);
                                break;
                            case 102:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X , point.Y + (4.5 * shrekScaleY) , -point.Z);
                                break;
                            case 68:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X , point.Y + (5.5 * shrekScaleY) , -point.Z);
                                break;
                            case 70:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X , point.Y + (5.5 * shrekScaleY) , -point.Z);
                                break;
                            case 71:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X + (0.5 * shrekScaleX), point.Y + (5 * shrekScaleY) , -point.Z);
                                break;
                            case 73:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X - (0.5 * shrekScaleX), point.Y + (5 * shrekScaleY), -point.Z);
                                break;
                            case 103:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X - (0.5 * shrekScaleX), point.Y + (5 * shrekScaleY) , -point.Z);
                                break;
                            case 105:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X - (0.5 * shrekScaleX), point.Y + (5 * shrekScaleY) , -point.Z);
                                break;
                            case 104:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X + (0.5 * shrekScaleX), point.Y + (5 * shrekScaleY) , -point.Z);
                                break;
                            case 106:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X + (0.5 * shrekScaleX), point.Y + (5 * shrekScaleY) , -point.Z);
                                break;
                            case 72:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X, point.Y + (5 * shrekScaleY) , -point.Z);
                                break;
                            case 74:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X, point.Y + (5 * shrekScaleY) , -point.Z);
                                break;
                            case 107:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X - shrekScaleX, point.Y + (5 * shrekScaleY) , -point.Z);
                                break;
                            case 109:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X - shrekScaleX, point.Y + (5 * shrekScaleY) , -point.Z);
                                break;
                            case 108:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X + shrekScaleX, point.Y + (5 * shrekScaleY) , -point.Z);
                                break;
                            case 110:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X + shrekScaleX, point.Y + (5 * shrekScaleY) , -point.Z);
                                break;
                            case 15:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X + shrekScaleX, point.Y + (2 * shrekScaleY) , -point.Z);
                                break;
                            case 48:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X - shrekScaleX, point.Y + (2 * shrekScaleY) , -point.Z);
                                break;
                            case 16:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X + (2.5 *  shrekScaleX), point.Y + (3 * shrekScaleY) , -point.Z);
                                break;
                            case 49:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X - (2.5 *  shrekScaleX), point.Y + (3 * shrekScaleY) , -point.Z);
                                break;
                            case 18:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X + (3 *  shrekScaleX), point.Y + (2.5 * shrekScaleY) , -point.Z);
                                break;
                            case 51:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X - (3 *  shrekScaleX), point.Y + (2.5 * shrekScaleY) , -point.Z);
                                break;
                            case 17:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X + shrekScaleX, point.Y + (5 * shrekScaleY) , -point.Z);
                                break;
                            case 50:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X + shrekScaleX, point.Y + (5 * shrekScaleY) , -point.Z);
                                break;
                            case 77:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X - (0.5 * shrekScaleX), point.Y + (6 * shrekScaleY) , -point.Z);
                                break;
                            case 78:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X - (0.5 * shrekScaleX), point.Y + (6 * shrekScaleY) , -point.Z);
                                break;
                            case 92:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X, point.Y + (8 * shrekScaleY) , -point.Z);
                                break;
                            case 93:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X, point.Y + (8 * shrekScaleY) , -point.Z);
                                break;
                            case 27:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X - (3 * shrekScaleX), point.Y + (4.5 * shrekScaleY) , -point.Z);
                                break;
                            case 60:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X + (3 * shrekScaleX), point.Y + (4.5 * shrekScaleY) , -point.Z);
                                break;
                            case 28:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X - (4.5 * shrekScaleX), point.Y - shrekScaleY , -point.Z);
                                break;
                            case 61:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X + (4.5 * shrekScaleX), point.Y - shrekScaleY , -point.Z);
                                break;
                            case 25:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X - (2 * shrekScaleX), point.Y + (9 * shrekScaleY) , -point.Z);
                                break;
                            case 58:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X + (2 * shrekScaleX), point.Y + (9 * shrekScaleY) , -point.Z);
                                break;
                            case 26:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X - (1.5 * shrekScaleX), point.Y + (9 * shrekScaleY) , -point.Z);
                                break;
                            case 59:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X + (1.5 * shrekScaleX), point.Y + (9 * shrekScaleY) , -point.Z);
                                break;
                            case 111:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X - (0.5 * shrekScaleX), point.Y + (7.5 * shrekScaleY) , -point.Z);
                                break;
                            case 112:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X + (0.5 * shrekScaleX), point.Y + (7.5 * shrekScaleY) , -point.Z);
                                break;
                            case 31:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X - (5 * shrekScaleX), point.Y + (5 * shrekScaleY) , -point.Z);
                                break;
                            case 64:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X + (5 * shrekScaleX), point.Y + (5 * shrekScaleY) , -point.Z);
                                break;
                            case 79:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X - (4 * shrekScaleX), point.Y + (4 * shrekScaleY) , -point.Z);
                                break;
                            case 80:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X + (4 * shrekScaleX), point.Y + (4 * shrekScaleY) , -point.Z);
                                break;
                            case 33:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X - (2.5 * shrekScaleX), point.Y + (3.5 * shrekScaleY) , -point.Z);
                                break;
                            case 66:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X + (2.5 * shrekScaleX), point.Y + (3.5 * shrekScaleY) , -point.Z);
                                break;
                            case 88:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X - (3.5 * shrekScaleX), point.Y + (4 * shrekScaleY) , -point.Z);
                                break;
                            case 89:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X + (3.5 * shrekScaleX), point.Y + (4 * shrekScaleY) , -point.Z);
                                break;
                            case 83:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X - (1.5 * shrekScaleX), point.Y + (4 * shrekScaleY) , -point.Z);
                                break;
                            case 84:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X + (1.5 * shrekScaleX), point.Y + (4 * shrekScaleY) , -point.Z);
                                break;
                            case 85:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X, point.Y + (2.5 * shrekScaleY) , -point.Z);
                                break;
                            case 86:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X, point.Y + (2.5 * shrekScaleY) , -point.Z);
                                break;
                            case 90:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X - (5.5 * shrekScaleX), point.Y + (5 * shrekScaleY) , -point.Z);
                                break;
                            case 91:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X + (5.5 * shrekScaleX), point.Y + (5 * shrekScaleY) , -point.Z);
                                break;
                            case 30:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X - (4 * shrekScaleX), point.Y + (1 * shrekScaleY) , -point.Z);
                                break;
                            case 63:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X + (4 * shrekScaleX), point.Y + (1 * shrekScaleY) , -point.Z);
                                break;
                            case 32:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X - (1.5 * shrekScaleX), point.Y - shrekScaleY, -point.Z);
                                break;
                            case 65:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X + (1.5 * shrekScaleX), point.Y - shrekScaleY, -point.Z);
                                break;
                            case 29:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X - (3 * shrekScaleX), point.Y + shrekScaleY, -point.Z);
                                break;
                            case 116:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X - (3 * shrekScaleX), point.Y + shrekScaleY, -point.Z);
                                break;
                            case 62:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X + (3 * shrekScaleX), point.Y + shrekScaleY, -point.Z);
                                break;
                            default:
                                this.theGeometry.Positions[pointIndex] = new Point3D(point.X, point.Y, -point.Z);
                                break;
                        }
                        PointF projected = projectedShapePoints[pointIndex];
                        this.theGeometry.TextureCoordinates[pointIndex] = new Point(//0, 0);
                        projected.X / (double)this.colorImageWritableBitmap.PixelWidth,
                        projected.Y / (double)this.colorImageWritableBitmap.PixelHeight);
                        /*new Point(rand.NextDouble(),rand.NextDouble());*/

                        //Debug.Print("X:{0} Y:{1}", rand.NextDouble(), rand.NextDouble());
                    }
                    break;
                #endregion
            }
            Point3DCollection v = this.theGeometry.Positions;
            PointCollection vt = this.theGeometry.TextureCoordinates;
            Int32Collection f = this.theGeometry.TriangleIndices;
            setPosition(v);
            setTextureCoordinates(vt);
            setTriangleIndices(f);
           
        }
    }
}