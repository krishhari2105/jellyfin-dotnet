
using JellyfinTizen.Core;
using JellyfinTizen.Models;
using System.Collections.Generic;
using Tizen.NUI;
using Tizen.NUI.BaseComponents;

namespace JellyfinTizen.Screens
{
    public class DetailsScreen : ScreenBase
    {
        private readonly JellyfinMovie _movie;

        public DetailsScreen(JellyfinMovie movie)
        {
            _movie = movie;
            Initialize();
        }

        private void Initialize()
        {
            // Create a root view
            var rootView = new View
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FillToParent,
                BackgroundColor = Color.Black
            };
            Add(rootView);

            // Create a title
            var title = new TextLabel
            {
                Text = _movie.Name,
                PointSize = 24,
                TextColor = Color.White,
                Position = new Position(20, 20)
            };
            rootView.Add(title);

            // Create an overview
            var overview = new TextLabel
            {
                Text = _movie.Overview,
                MultiLine = true,
                LineWrapMode = LineWrapMode.Word,
                PointSize = 16,
                TextColor = Color.White,
                Size = new Size(Window.Default.Size.Width - 40, 400),
                Position = new Position(20, 80)
            };
            rootView.Add(overview);
        }
    }
}
