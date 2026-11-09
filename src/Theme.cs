using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace WordAss
{
    internal enum Appearance { Modern, OldBook }

    internal enum ThemeKey
    {
        Ink, Muted, Accent, AccentLine, Danger, Tint, Surface, Canvas, Paper, Line, OnAccent, AccentHover, AccentPressed,
        BodyFont, ReadingFont, CardCorner, PopupCorner, ControlCorner, ToolCorner, InsetCorner, BadgeCorner, PopupShadow,
        FolioRule
    }

    internal static class Theme
    {
        private static ResourceDictionary active;
        public static Appearance Current { get; private set; }

        public static void Apply(Appearance appearance)
        {
            if (active != null && Current == appearance) return;
            bool oldBook = appearance == Appearance.OldBook;
            var bodyFont = new FontFamily("Segoe UI Variable, Microsoft YaHei UI, Segoe UI");
            var resources = new ResourceDictionary
            {
                { ThemeKey.Ink, Brush(oldBook ? "#3D2C20" : "#192237") },
                { ThemeKey.Muted, Brush(oldBook ? "#705743" : "#525E76") },
                { ThemeKey.Accent, Brush(oldBook ? "#85432F" : "#4442C9") },
                { ThemeKey.AccentLine, Brush(oldBook ? "#B98364" : "#9F9DE7") },
                { ThemeKey.Danger, Brush(oldBook ? "#8A2E24" : "#A43041") },
                { ThemeKey.Tint, Brush(oldBook ? "#EEDBB7" : "#EDEEFF") },
                { ThemeKey.Surface, Brush(oldBook ? "#F1E2C6" : "#F5F7FC") },
                { ThemeKey.Canvas, oldBook ? Page("#F0E5CD", "#E8D8B9") : Brush("#F5F7FC") },
                { ThemeKey.Paper, oldBook ? Page("#FFF6E4", "#F8EBD2") : Brush("#FFFFFF") },
                { ThemeKey.Line, Brush(oldBook ? "#CDB58F" : "#DCE1EC") },
                { ThemeKey.OnAccent, Brush(oldBook ? "#FFF5DE" : "#FFFFFF") },
                { ThemeKey.AccentHover, Brush(oldBook ? "#733A28" : "#3734B2") },
                { ThemeKey.AccentPressed, Brush(oldBook ? "#632F21" : "#2F2B9B") },
                { ThemeKey.BodyFont, bodyFont },
                { ThemeKey.ReadingFont, oldBook ? new FontFamily("Georgia, SimSun, Microsoft YaHei UI") : bodyFont },
                { ThemeKey.CardCorner, new CornerRadius(oldBook ? 5 : 14) },
                { ThemeKey.PopupCorner, new CornerRadius(oldBook ? 4 : 12) },
                { ThemeKey.ControlCorner, new CornerRadius(oldBook ? 3 : 8) },
                { ThemeKey.ToolCorner, new CornerRadius(oldBook ? 3 : 7) },
                { ThemeKey.InsetCorner, new CornerRadius(oldBook ? 3 : 9) },
                { ThemeKey.BadgeCorner, new CornerRadius(oldBook ? 2 : 6) },
                { ThemeKey.FolioRule, oldBook ? Visibility.Visible : Visibility.Collapsed },
                { ThemeKey.PopupShadow, new DropShadowEffect
                    { Color = ColorOf(oldBook ? "#58432F" : "#1C2641"), BlurRadius = 13, ShadowDepth = 2, Opacity = 0.22 } }
            };
            if (oldBook)
            {
                resources.Add(typeof(ScrollBar), XamlReader.Parse(PaperScrollBar));
                resources.Add(SystemParameters.VerticalScrollBarWidthKey, 9.0);
                resources.Add(SystemParameters.HorizontalScrollBarHeightKey, 9.0);
            }

            var dictionaries = Application.Current.Resources.MergedDictionaries;
            if (active == null) dictionaries.Add(resources);
            else dictionaries[dictionaries.IndexOf(active)] = resources;
            active = resources;
            Current = appearance;
        }

        private static Color ColorOf(string hex) { return (Color)ColorConverter.ConvertFromString(hex); }

        private static Brush Brush(string hex)
        {
            var brush = new SolidColorBrush(ColorOf(hex));
            brush.Freeze();
            return brush;
        }

        private static Brush Page(string light, string shaded)
        {
            var brush = new LinearGradientBrush(ColorOf(light), ColorOf(shaded), 70);
            brush.Freeze();
            return brush;
        }

        private const string PaperScrollBar = @"
<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
       xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='{x:Type ScrollBar}'>
  <Setter Property='Width' Value='9'/>
  <Setter Property='Background' Value='#EEDBB7'/>
  <Setter Property='Foreground' Value='#A28362'/>
  <Setter Property='Template'>
    <Setter.Value>
      <ControlTemplate TargetType='{x:Type ScrollBar}'>
        <ControlTemplate.Resources>
          <Style x:Key='PageButton' TargetType='{x:Type RepeatButton}'>
            <Setter Property='Focusable' Value='False'/>
            <Setter Property='Template'>
              <Setter.Value>
                <ControlTemplate TargetType='{x:Type RepeatButton}'><Border Background='Transparent'/></ControlTemplate>
              </Setter.Value>
            </Setter>
          </Style>
        </ControlTemplate.Resources>
        <Border Background='{TemplateBinding Background}' CornerRadius='2'>
          <Track x:Name='PART_Track' IsDirectionReversed='True' Orientation='{TemplateBinding Orientation}'>
            <Track.DecreaseRepeatButton>
              <RepeatButton x:Name='PreviousPage' Style='{StaticResource PageButton}' Command='ScrollBar.PageUpCommand'/>
            </Track.DecreaseRepeatButton>
            <Track.Thumb>
              <Thumb Foreground='{Binding Foreground, RelativeSource={RelativeSource AncestorType={x:Type ScrollBar}}}'>
                <Thumb.Template>
                  <ControlTemplate TargetType='{x:Type Thumb}'>
                    <Border Background='{TemplateBinding Foreground}' CornerRadius='2' Margin='1'/>
                  </ControlTemplate>
                </Thumb.Template>
              </Thumb>
            </Track.Thumb>
            <Track.IncreaseRepeatButton>
              <RepeatButton x:Name='NextPage' Style='{StaticResource PageButton}' Command='ScrollBar.PageDownCommand'/>
            </Track.IncreaseRepeatButton>
          </Track>
        </Border>
        <ControlTemplate.Triggers>
          <Trigger Property='Orientation' Value='Horizontal'>
            <Setter TargetName='PART_Track' Property='IsDirectionReversed' Value='False'/>
            <Setter TargetName='PreviousPage' Property='Command' Value='ScrollBar.PageLeftCommand'/>
            <Setter TargetName='NextPage' Property='Command' Value='ScrollBar.PageRightCommand'/>
          </Trigger>
        </ControlTemplate.Triggers>
      </ControlTemplate>
    </Setter.Value>
  </Setter>
  <Style.Triggers>
    <Trigger Property='Orientation' Value='Horizontal'>
      <Setter Property='Width' Value='Auto'/>
      <Setter Property='Height' Value='9'/>
    </Trigger>
  </Style.Triggers>
</Style>";
    }
}
