   M S B A M L   `   `   `  ����M  IFaceTracking3D-WPF, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null/clr-namespace:FaceTracking3DFaceTracking3D  5   	   P LCoding4Fun.Kinect.Wpf, Version=1.7.0.0, Culture=neutral, PublicKeyToken=nullnKclr-namespace:Coding4Fun.Kinect.Wpf.Controls;assembly=Coding4Fun.Kinect.WpfCoding4Fun.Kinect.Wpf.Controls 5   	       FaceTracking3D.MainWindow   5   	   R NWindowsBase, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35W SPresentationCore, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35\ XPresentationFramework, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35D 9http://schemas.microsoft.com/winfx/2006/xaml/presentation    5   	   R NSystem.Xaml, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089:x,http://schemas.microsoft.com/winfx/2006/xaml     5   	   .localclr-namespace:FaceTracking3D     5   	   `ControlsKclr-namespace:Coding4Fun.Kinect.Wpf.Controls;assembly=Coding4Fun.Kinect.Wpf     5   	     � WindowState$  	Maximized=�5   	    � Title$, &Interactive 3D cartoon face expression��5   	    � WindowStyle$
 None=�5   	   ��5      �� 5   
    �� ImageSource$4 ./FaceTracking3D-WPF;component/Images/black.png��6    � 	Resources 5         MediumGreyBrush �� Color  KinectPurpleBrush  KinectBlueBrush �� 
TargetType �� Property �� Value��%�   &	        &	 #     &	 F     '�� i     �� 5   
   $ 	#ff6e6e6e��62   �� 5   
   $ 	#ff52318f��64   �� 5   
   $ 	#ff00BCF2��62   �� 5   
   " ��6   .�5      ��  �@J�SnapsToDevicePixels . 64   5      5      .��5      � -   5        $��
layoutGrid��5      $��
10 0 10 10q�6   	 � RowDefinitions	 5   
   �� 5      $
��Auto �6   �� 5      $��* �6   .+�5   
   �� 
������ $��1��6C   $	��720��6P   $
��1280��6]   
 � DataContext
 6*   �� .+�5   
   ]� $��0��6   $��0 0 0 20q�6!   65   0  *Coding4Fun.Kinect.Wpf.Controls.HoverButton  5      -   5          Name$ kinectButton��5   :   $��0q�6#   $��0q�6.      	ImageSize$ 64Q�6P      ImageSource$ /Images/Hand_Basic.png��5   !      ActiveImageSource$ /Images/Hand_Basic.png��5    !      TimeInterval$
 2000��5!   !    �� Left$	 525��66   
 �� Top$ 32��6H    K� ZIndex$
 1000��6X   (� 5"      $��1��6    (� Stretch$ Uniform=�6#   $��Center=�65   $	 120��6R   $	 120��6d   6w   �� 5#      -   5        $��	statusBar��5#   O   $��3��6   $��23��6%   $��Stretch=�61   $��Bottom=�6`   
�������#��[  6�   .@�5$      �� $��0q�6   .��5%      �� -   5        $��statusBarText��5%      63   5&      5'      �� 5(      -   5        $��
textBlock3��5(      $	 150��6    �� TextAlignment$ Center=�6*   $ 32��6A   $��43(�6Q   
�������$	��101��6r   $,��&Interactive 3D cartoon face expression��6�   $
��1000��6�   �� 5)      -   5        $��button1��5)   C   $ 37��6   $ 70��6&   $	��150��66   $	��200��6R   $��Right=�6^   $��Bottom=�6�   ��5*      �� 5+      $< 6/FaceTracking3D-WPF;component/Images/kinect-hub001.PNG��6!   5-      �� 5.      -   5        $��button2��5.   D   $ 37��6   $	 300��6&   $	��150��67   $	��200��6S   $��Right=�6_   $��Bottom=�6�   ��5/      �� 50      $< 6/FaceTracking3D-WPF;component/Images/kinect-hub002.PNG��6!   52      �� 53      -   5        $��button3��53   D   $ 37��6   $	 530��6&   $	��150��67   $	��200��6S   $��Right=�6_   $��Bottom=�6�   ��54      �� 55      $< 6/FaceTracking3D-WPF;component/Images/kinect-hub003.PNG��6!   57      �� 59      -	   5        $��button4��59   D    �� Right$ 50��6   $ 70��6'   $	��150��67   $	��200��6S   $��Right=�6_   $��Bottom=�6�   ��5:      �� 5;      $< 6/FaceTracking3D-WPF;component/Images/kinect-hub004.PNG��6!   5=      �� 5?      -
   5        $
��Exit��5?   :    �� Bottom$ 70��6   $ 70��6(   $��50��6F   $	��120��6R   ��5@      �� 5A      $3 -/FaceTracking3D-WPF;component/Images/exit.PNG��6!   5C      �� 5D      $��1��6   $��Center=�6%   $��Bottom=�6B   $��42(�6]   $��285,0,313,12q�6k   #��[  6�   $��52��6�   $ 10��6�   $	 628��6�   Say: "�� 5E      -   5         �Name$ HulkSpan��5E      .�6)   Hulk6/   ", "�� 69   -   5        $ 	shrekSpan��5E   >   .�6O   Shrek6V   ", "�� 6`   -   5        $ backSpan��5E   e   .�6u   Back6{   ", "�� 6�   -   5        $ exitSpan��5E   �   .�6�   Exit6�   " to control.6�   � 5F      $	 202��6   $	 128��6%   	 5G      �� 5H      $	��155 �6$   �� 5I      $	��345 �6$   5K      +   %FaceTracking3D.TexturedFaceMeshViewer  5L      -   5          Name$ faceTrackingVisualizer��5L   +   $	��650��6K   $	��650��6W   $	 350��6d   $ 70��6v   5M      5N      5O      