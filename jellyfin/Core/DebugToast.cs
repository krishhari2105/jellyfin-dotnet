using System;
using Tizen.NUI;
using Tizen.NUI.BaseComponents;

namespace JellyfinTizen.Core
{
    public static class DebugToast
    {
        private static View _view;
        private static TextLabel _label;
        private static Timer _timer;

        public static void Show(string msg)
        {
            try
            {
                // Always run on the main UI thread to prevent crashes
                Tizen.Applications.CoreApplication.Post(() =>
                {
                    if (Window.Default == null) return;

                    if (_view == null)
                    {
                        _view = new View()
                        {
                            WidthResizePolicy = ResizePolicyType.FillToParent,
                            HeightSpecification = 100,
                            BackgroundColor = new Color(0.8f, 0, 0, 0.9f), // Bright Red
                            PositionY = 0,
                            ParentOrigin = ParentOrigin.TopCenter,
                            PivotPoint = PivotPoint.TopCenter,
                            PositionUsesPivotPoint = true,
                        };
                        
                        _label = new TextLabel()
                        {
                            WidthResizePolicy = ResizePolicyType.FillToParent,
                            HeightResizePolicy = ResizePolicyType.FillToParent,
                            TextColor = Color.White,
                            PointSize = 24, // Large text
                            HorizontalAlignment = HorizontalAlignment.Center,
                            VerticalAlignment = VerticalAlignment.Center,
                            MultiLine = true
                        };
                        
                        _view.Add(_label);
                        Window.Default.Add(_view);
                    }
                    
                    _view.Show();
                    _view.RaiseToTop();
                    _label.Text = msg;
                    Console.WriteLine("[DEBUG TOAST] " + msg); // Also log to console

                    _timer?.Stop();
                    _timer = new Timer(3000); // Show for 3 seconds
                    _timer.Tick += (s, e) => 
                    {
                        _view.Hide();
                        _timer.Stop();
                        return false;
                    };
                    _timer.Start();
                });
            }
            catch (Exception e)
            {
                Console.WriteLine("TOAST ERROR: " + e.Message);
            }
        }
    }
}