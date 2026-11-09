using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace WordAss
{
    internal static class Icons
    {
        public static readonly Geometry Book = Shape("M12,6 C9,3 6,3 3,4 V18 C6,17 9,17 12,20 C15,17 18,17 21,18 V4 C18,3 15,3 12,6 Z M12,6 V20");
        public static readonly Geometry Translate = Shape("M3,5 H13 M8,3 V5 M11,5 C10,10 7,13 3,15 M5,8 C6,11 8,13 11,15 M13,20 L17.5,10 L22,20 M15,17 H20");
        public static readonly Geometry Speaker = Shape("M3,9 H7 L12,5 V19 L7,15 H3 Z M16,9 C18,10.5 18,13.5 16,15 M19,6 C22.5,9 22.5,15 19,18");
        public static readonly Geometry Bookmark = Shape("M6,4 H18 V20 L12,16 L6,20 Z M9.5,10 H14.5 M12,7.5 V12.5");
        public static readonly Geometry Check = Shape("M5,12 L10,17 L19,7");
        public static readonly Geometry Grip = Shape("M6,9 H18 M6,15 H18");
        public static readonly Geometry Pin = Shape("M7,4 H17 L15.5,6 V10 L18,13 V15 H6 V13 L8.5,10 V6 Z M12,15 V21");
        public static readonly Geometry Close = Shape("M6,6 L18,18 M18,6 L6,18");
        public static readonly Geometry Trash = Shape("M4,6 H20 M9,6 V4 H15 V6 M6,6 L7,20 H17 L18,6 M10,10 V16 M14,10 V16");
        public static readonly Geometry Chevron = Shape("M7,10 L12,15 L17,10");
        public static readonly Geometry Key = Shape("M13,11 A4.5,4.5 0 1 1 18,16 L16,18 H13 V21 H9 V18 L13,14 Z M17,10 H17.1");

        public static Viewbox Draw(Geometry geometry, Brush brush, double size = 16)
        {
            var canvas = new Canvas { Width = 24, Height = 24, IsHitTestVisible = false };
            canvas.Children.Add(new Path
            {
                Data = geometry, Stroke = brush, StrokeThickness = 1.8,
                StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round,
                StrokeLineJoin = PenLineJoin.Round
            });
            return new Viewbox
            {
                Width = size, Height = size, Child = canvas, IsHitTestVisible = false,
                VerticalAlignment = VerticalAlignment.Center
            };
        }

        private static Geometry Shape(string data)
        {
            var geometry = Geometry.Parse(data);
            geometry.Freeze();
            return geometry;
        }
    }
}
