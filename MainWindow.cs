using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using ShapePath = System.Windows.Shapes.Path;

namespace ResolutionChanger
{
    public sealed class MainWindow : Window
    {
        private static readonly Color CText = ColorFromHex("#1D1D1F");
        private static readonly Color CSub = ColorFromHex("#86868B");
        private static readonly Color CCardBorder = ColorFromHex("#E5E5EA");
        private static readonly Color CBorder = ColorFromHex("#D0D0D6");
        private static readonly Color CPrimary = ColorFromHex("#007AFF");
        private static readonly Color CWindowBg = ColorFromHex("#F5F5F7");
        private static readonly Color CTitleBg = ColorFromHex("#ECECF0");
        private static readonly string IniPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ResolutionChanger.ini");

        private readonly List<DisplayInfo> _displays;
        private readonly List<ComboBox> _boxes = new List<ComboBox>();
        private readonly List<DEVMODE> _originals = new List<DEVMODE>();
        private readonly Dictionary<string, string> _saved = new Dictionary<string, string>();
        private TextBlock _status;

        public MainWindow()
        {
            _displays = DisplayInfo.Enumerate();
            foreach (var d in _displays) _originals.Add(d.Current);
            foreach (var kv in LoadIni()) _saved[kv.Key] = kv.Value;
            InitializeUi();
        }

        private void InitializeUi()
        {
            Width = 1000;
            Height = 700;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.CanMinimize;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            FontFamily = new FontFamily("Microsoft YaHei UI, Segoe UI");
            Title = "分辨率切换";

            var root = new Border
            {
                CornerRadius = new CornerRadius(24),
                Background = new SolidColorBrush(CWindowBg),
                BorderBrush = new SolidColorBrush(CCardBorder),
                BorderThickness = new Thickness(1),
                Effect = DropShadow(0.18, 28),
                Margin = new Thickness(16)
            };
            Content = root;

            var grid = new Grid();
            root.Child = grid;
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(52) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(104) });
            var titleBar = BuildTitleBar();
            var content = BuildContent();
            var footer = BuildFooter();
            Grid.SetRow(titleBar, 0);
            Grid.SetRow(content, 1);
            Grid.SetRow(footer, 2);
            grid.Children.Add(titleBar);
            grid.Children.Add(content);
            grid.Children.Add(footer);
            var clip = new RectangleGeometry { RadiusX = 24, RadiusY = 24 };
            grid.Clip = clip;
            grid.SizeChanged += (s, e) => clip.Rect = new Rect(0, 0, grid.ActualWidth, grid.ActualHeight);
        }

        private FrameworkElement BuildTitleBar()
        {
            var bar = new Grid { Background = new SolidColorBrush(CTitleBg) };
            var lights = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(22, 0, 0, 0)
            };
            lights.Children.Add(Light(ColorFromHex("#FF5F57"), "✕", (s, e) => Close()));
            lights.Children.Add(Light(ColorFromHex("#FEBC2E"), "−", (s, e) => WindowState = WindowState.Minimized));
            lights.Children.Add(Light(ColorFromHex("#28C840"), "⤢", (s, e) => ToggleMaximize()));
            bar.Children.Add(lights);
            var title = new TextBlock
            {
                Text = "分辨率切换",
                FontSize = 13.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(ColorFromHex("#3A3A3C")),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            bar.Children.Add(title);
            bar.MouseLeftButtonDown += (s, e) =>
            {
                if (e.ClickCount == 2) { ToggleMaximize(); return; }
                try { DragMove(); } catch { }
            };
            return bar;
        }

        private static Border Light(Color c, string glyph, MouseButtonEventHandler onClick)
        {
            var glyphTb = new TextBlock
            {
                Text = glyph,
                FontSize = 9,
                FontFamily = new FontFamily("Segoe UI Symbol"),
                Foreground = new SolidColorBrush(Color.FromArgb(150, 0, 0, 0)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Visibility = Visibility.Collapsed
            };
            var bd = new Border
            {
                Width = 12,
                Height = 12,
                CornerRadius = new CornerRadius(6),
                Background = new SolidColorBrush(c),
                Child = glyphTb,
                Cursor = Cursors.Hand,
                Margin = new Thickness(0, 0, 9, 0)
            };
            var hover = Color.FromRgb((byte)(c.R * 0.78), (byte)(c.G * 0.78), (byte)(c.B * 0.78));
            bd.MouseEnter += (s, e) => { bd.Background = new SolidColorBrush(hover); glyphTb.Visibility = Visibility.Visible; };
            bd.MouseLeave += (s, e) => { bd.Background = new SolidColorBrush(c); glyphTb.Visibility = Visibility.Collapsed; };
            bd.MouseLeftButtonDown += (s, e) => { e.Handled = true; onClick(s, e); };
            return bd;
        }

        private void ToggleMaximize()
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }

        private FrameworkElement BuildContent()
        {
            var sp = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            if (_displays.Count == 0)
            {
                sp.Children.Add(new TextBlock
                {
                    Text = "未检测到显示器",
                    FontSize = 15,
                    Foreground = new SolidColorBrush(CSub)
                });
                return sp;
            }
            for (int i = 0; i < _displays.Count; i++)
                sp.Children.Add(BuildCard(_displays[i], i));
            return sp;
        }

        private Border BuildCard(DisplayInfo d, int index)
        {
            var card = new Border
            {
                Width = 404,
                Margin = index == _displays.Count - 1 ? new Thickness(0) : new Thickness(0, 0, 28, 0),
                CornerRadius = new CornerRadius(18),
                Background = new SolidColorBrush(Colors.White),
                BorderBrush = new SolidColorBrush(CCardBorder),
                BorderThickness = new Thickness(1),
                Effect = DropShadow(0.06, 12),
                Padding = new Thickness(24, 26, 24, 22)
            };
            var sp = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            sp.Children.Add(MonitorIcon(d, index));
            sp.Children.Add(new TextBlock
            {
                Text = "显示器 " + (index + 1),
                FontSize = 17,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(CText),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 16, 0, 0)
            });
            sp.Children.Add(new TextBlock
            {
                Text = (d.MonitorName ?? d.AdapterName) + (d.Primary ? "  ·  主屏" : ""),
                FontSize = 12,
                Foreground = new SolidColorBrush(CSub),
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 5, 0, 0)
            });
            sp.Children.Add(new TextBlock
            {
                Text = "分辨率",
                FontSize = 12,
                Foreground = new SolidColorBrush(ColorFromHex("#6E6E73")),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 22, 0, 8)
            });
            sp.Children.Add(BuildCombo(d));
            sp.Children.Add(new TextBlock
            {
                Text = "当前  " + d.ModeLabel(d.Current),
                FontSize = 11.5,
                Foreground = new SolidColorBrush(ColorFromHex("#A1A1A6")),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 10, 0, 0)
            });
            card.Child = sp;
            return card;
        }

        private ComboBox BuildCombo(DisplayInfo d)
        {
            var cb = new ComboBox
            {
                Width = 292,
                Height = 38,
                FontSize = 13,
                Foreground = new SolidColorBrush(CText),
                Template = ComboTemplate(),
                ItemContainerStyle = ComboItemStyle(),
                FocusVisualStyle = null
            };
            for (int i = 0; i < d.Modes.Count; i++)
                cb.Items.Add(new ComboBoxItem { Content = d.ModeLabel(d.Modes[i]), Tag = i });
            string currentKey = d.ModeKey(d.Current);
            string sv;
            if (_saved.TryGetValue(d.DeviceName.ToUpperInvariant(), out sv))
            {
                for (int i = 0; i < d.Modes.Count; i++)
                {
                    if (d.ModeKey(d.Modes[i]) == sv) { cb.SelectedIndex = i; break; }
                }
            }
            if (cb.SelectedIndex < 0)
            {
                for (int i = 0; i < d.Modes.Count; i++)
                {
                    if (d.ModeKey(d.Modes[i]) == currentKey) { cb.SelectedIndex = i; break; }
                }
            }
            if (cb.SelectedIndex < 0 && d.Modes.Count > 0) cb.SelectedIndex = 0;
            _boxes.Add(cb);
            return cb;
        }

        private FrameworkElement MonitorIcon(DisplayInfo d, int index)
        {
            var g = new Grid { Width = 124, Height = 104 };
            g.RowDefinitions.Add(new RowDefinition { Height = new GridLength(68) });
            g.RowDefinitions.Add(new RowDefinition { Height = new GridLength(16) });
            g.RowDefinitions.Add(new RowDefinition { Height = new GridLength(20) });

            var grad = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };
            grad.GradientStops.Add(new GradientStop(d.Primary ? ColorFromHex("#A5D3FF") : ColorFromHex("#D5D9DE"), 0));
            grad.GradientStops.Add(new GradientStop(d.Primary ? ColorFromHex("#3D8BFF") : ColorFromHex("#9AA1A9"), 1));
            var screen = new Rectangle { Width = 114, Height = 66, RadiusX = 11, RadiusY = 11, Fill = grad, Effect = DropShadow(0.10, 8), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetRow(screen, 0);
            g.Children.Add(screen);
            var inner = new Rectangle
            {
                Width = 102,
                Height = 54,
                RadiusX = 7,
                RadiusY = 7,
                Fill = new SolidColorBrush(Color.FromArgb(55, 255, 255, 255)),
                Margin = new Thickness(6, 6, 6, 6),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetRow(inner, 0);
            g.Children.Add(inner);
            var stand = new Rectangle
            {
                Width = 7,
                Height = 14,
                Fill = new SolidColorBrush(ColorFromHex("#B7BDC4")),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Stretch
            };
            Grid.SetRow(stand, 1);
            g.Children.Add(stand);
            var baseE = new Ellipse
            {
                Width = 28,
                Height = 7,
                Fill = new SolidColorBrush(ColorFromHex("#B7BDC4")),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top
            };
            Grid.SetRow(baseE, 2);
            g.Children.Add(baseE);
            var badge = new Grid
            {
                Width = 24,
                Height = 24,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 0, 6, 0)
            };
            badge.Children.Add(new Ellipse { Fill = new SolidColorBrush(d.Primary ? CPrimary : ColorFromHex("#8E8E93")) });
            badge.Children.Add(new TextBlock
            {
                Text = (index + 1).ToString(),
                Foreground = Brushes.White,
                FontSize = 12.5,
                FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            });
            Grid.SetRow(badge, 0);
            g.Children.Add(badge);
            return g;
        }

        private static ControlTemplate ComboTemplate()
        {
            var tpl = new ControlTemplate(typeof(ComboBox));
            var grid = new FrameworkElementFactory(typeof(Grid));
            var bd = new FrameworkElementFactory(typeof(Border));
            bd.Name = "bd";
            bd.SetValue(Border.CornerRadiusProperty, new CornerRadius(10));
            bd.SetValue(Border.BackgroundProperty, new SolidColorBrush(Colors.White));
            bd.SetValue(Border.BorderBrushProperty, new SolidColorBrush(CBorder));
            bd.SetValue(Border.BorderThicknessProperty, new Thickness(1));
            grid.AppendChild(bd);

            var toggle = new FrameworkElementFactory(typeof(ToggleButton));
            toggle.SetValue(ToggleButton.IsCheckedProperty, new Binding
            {
                Path = new PropertyPath("IsDropDownOpen"),
                RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent),
                Mode = BindingMode.TwoWay
            });
            var ttpl = new ControlTemplate(typeof(ToggleButton));
            var tbd = new FrameworkElementFactory(typeof(Border));
            tbd.SetValue(Border.BackgroundProperty, Brushes.Transparent);
            ttpl.VisualTree = tbd;
            toggle.SetValue(Control.TemplateProperty, ttpl);
            grid.AppendChild(toggle);

            var content = new FrameworkElementFactory(typeof(ContentPresenter));
            content.SetValue(ContentPresenter.ContentProperty, new Binding
            {
                Path = new PropertyPath("SelectionBoxItem"),
                RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent)
            });
            content.SetValue(ContentPresenter.ContentTemplateProperty, new Binding
            {
                Path = new PropertyPath("SelectionBoxItemTemplate"),
                RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent)
            });
            content.SetValue(ContentPresenter.MarginProperty, new Thickness(14, 0, 34, 0));
            content.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            grid.AppendChild(content);

            var arrow = new FrameworkElementFactory(typeof(ShapePath));
            arrow.SetValue(ShapePath.DataProperty, Geometry.Parse("M 0 0 L 5 5 L 10 0"));
            arrow.SetValue(ShapePath.StrokeProperty, new SolidColorBrush(ColorFromHex("#8E8E93")));
            arrow.SetValue(ShapePath.StrokeThicknessProperty, 1.7);
            arrow.SetValue(ShapePath.HorizontalAlignmentProperty, HorizontalAlignment.Right);
            arrow.SetValue(ShapePath.MarginProperty, new Thickness(0, 0, 15, 0));
            arrow.SetValue(ShapePath.VerticalAlignmentProperty, VerticalAlignment.Center);
            grid.AppendChild(arrow);

            var popup = new FrameworkElementFactory(typeof(Popup));
            popup.Name = "PART_Popup";
            popup.SetValue(Popup.AllowsTransparencyProperty, true);
            popup.SetValue(Popup.IsOpenProperty, new Binding
            {
                Path = new PropertyPath("IsDropDownOpen"),
                RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent),
                Mode = BindingMode.TwoWay
            });
            popup.SetValue(Popup.PlacementProperty, PlacementMode.Bottom);
            popup.SetValue(Popup.PopupAnimationProperty, PopupAnimation.Fade);
            popup.SetValue(Popup.PlacementTargetProperty, new Binding
            {
                RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent)
            });

            var popBorder = new FrameworkElementFactory(typeof(Border));
            popBorder.SetValue(Border.CornerRadiusProperty, new CornerRadius(10));
            popBorder.SetValue(Border.BackgroundProperty, new SolidColorBrush(Colors.White));
            popBorder.SetValue(Border.BorderBrushProperty, new SolidColorBrush(ColorFromHex("#DDDDE3")));
            popBorder.SetValue(Border.BorderThicknessProperty, new Thickness(1));
            popBorder.SetValue(Border.MarginProperty, new Thickness(0, 5, 0, 0));
            popBorder.SetValue(Border.PaddingProperty, new Thickness(4));
            popBorder.SetValue(Border.MinWidthProperty, new Binding
            {
                Path = new PropertyPath("ActualWidth"),
                RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent)
            });
            popBorder.SetValue(Border.MaxHeightProperty, 280.0);
            popBorder.SetValue(Border.EffectProperty, DropShadow(0.12, 14));
            var scroller = new FrameworkElementFactory(typeof(ScrollViewer));
            scroller.Name = "PART_ScrollViewer";
            scroller.SetValue(ScrollViewer.VerticalScrollBarVisibilityProperty, ScrollBarVisibility.Auto);
            var items = new FrameworkElementFactory(typeof(ItemsPresenter));
            scroller.AppendChild(items);
            popBorder.AppendChild(scroller);
            popup.AppendChild(popBorder);
            grid.AppendChild(popup);

            tpl.VisualTree = grid;
            var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            hover.Setters.Add(new Setter(Border.BorderBrushProperty, new SolidColorBrush(ColorFromHex("#A2A2AC")), "bd"));
            tpl.Triggers.Add(hover);
            return tpl;
        }

        private static Style ComboItemStyle()
        {
            var st = new Style(typeof(ComboBoxItem));
            st.Setters.Add(new Setter(Control.FontSizeProperty, 13.0));
            st.Setters.Add(new Setter(FrameworkElement.MarginProperty, new Thickness(1, 1, 1, 1)));
            st.Setters.Add(new Setter(Control.FocusVisualStyleProperty, null));
            var tpl = new ControlTemplate(typeof(ComboBoxItem));
            var bd = new FrameworkElementFactory(typeof(Border));
            bd.Name = "bd";
            bd.SetValue(Border.CornerRadiusProperty, new CornerRadius(7));
            bd.SetValue(Border.PaddingProperty, new Thickness(12, 8, 12, 8));
            var cp = new FrameworkElementFactory(typeof(ContentPresenter));
            cp.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            bd.AppendChild(cp);
            tpl.VisualTree = bd;
            var sel = new Trigger { Property = ComboBoxItem.IsSelectedProperty, Value = true };
            sel.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(ColorFromHex("#EDF2FE")), "bd"));
            var hi = new Trigger { Property = ComboBoxItem.IsHighlightedProperty, Value = true };
            hi.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(ColorFromHex("#E9E9EE")), "bd"));
            tpl.Triggers.Add(sel);
            tpl.Triggers.Add(hi);
            st.Setters.Add(new Setter(Control.TemplateProperty, tpl));
            return st;
        }

        private static Button MakeButton(string text, bool primary, RoutedEventHandler click)
        {
            var b = new Button
            {
                Content = text,
                Height = 40,
                MinWidth = 112,
                Padding = new Thickness(26, 0, 26, 0),
                FontSize = 13.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(primary ? Colors.White : CText),
                Cursor = Cursors.Hand,
                FocusVisualStyle = null
            };
            var tpl = new ControlTemplate(typeof(Button));
            var bd = new FrameworkElementFactory(typeof(Border));
            bd.Name = "bd";
            bd.SetValue(Border.CornerRadiusProperty, new CornerRadius(11));
            bd.SetValue(Border.BackgroundProperty, new SolidColorBrush(primary ? CPrimary : Colors.White));
            if (!primary)
            {
                bd.SetValue(Border.BorderBrushProperty, new SolidColorBrush(CBorder));
                bd.SetValue(Border.BorderThicknessProperty, new Thickness(1));
            }
            var cp = new FrameworkElementFactory(typeof(ContentPresenter));
            cp.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            cp.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            bd.AppendChild(cp);
            tpl.VisualTree = bd;
            var hov = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            hov.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(primary ? ColorFromHex("#0071EB") : ColorFromHex("#EFEFF3")), "bd"));
            var press = new Trigger { Property = ButtonBase.IsPressedProperty, Value = true };
            press.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(primary ? ColorFromHex("#0068D6") : ColorFromHex("#E6E6EA")), "bd"));
            tpl.Triggers.Add(hov);
            tpl.Triggers.Add(press);
            b.Template = tpl;
            b.Click += click;
            return b;
        }

        private FrameworkElement BuildFooter()
        {
            var sp = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 14)
            };
            var btns = new StackPanel { Orientation = Orientation.Horizontal };
            var b1 = MakeButton("恢复", false, RestoreClick);
            var b2 = MakeButton("保存", false, SaveClick);
            b1.Margin = new Thickness(0, 0, 14, 0);
            b2.Margin = new Thickness(0, 0, 14, 0);
            btns.Children.Add(b1);
            btns.Children.Add(b2);
            btns.Children.Add(MakeButton("应用", true, ApplyClick));
            sp.Children.Add(btns);
            _status = new TextBlock
            {
                FontSize = 12,
                Foreground = new SolidColorBrush(CSub),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 12, 0, 0)
            };
            sp.Children.Add(_status);
            sp.Children.Add(new TextBlock
            {
                Text = "应用 = 临时切换 · 保存 = 应用并持久化到系统 · 恢复 = 还原启动时的设置",
                FontSize = 10.5,
                Foreground = new SolidColorBrush(ColorFromHex("#A6A6AC")),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 5, 0, 0)
            });
            return sp;
        }

        private void ApplyClick(object sender, RoutedEventArgs e)
        {
            ApplyCurrent(false);
        }

        private void SaveClick(object sender, RoutedEventArgs e)
        {
            ApplyCurrent(true);
            SaveIni();
        }

        private void ApplyCurrent(bool persist)
        {
            var parts = new List<string>();
            for (int i = 0; i < _displays.Count; i++)
            {
                var item = _boxes[i].SelectedItem as ComboBoxItem;
                if (item == null) continue;
                int mi = (int)item.Tag;
                var mode = _displays[i].Modes[mi];
                string err = _displays[i].Apply(mode, persist);
                parts.Add("显示器" + (i + 1) + "  " + _displays[i].ModeLabel(mode) + "  " + (err == null ? "成功" : "失败 · " + err));
            }
            _status.Text = string.Join("        ", parts);
        }

        private void RestoreClick(object sender, RoutedEventArgs e)
        {
            var parts = new List<string>();
            for (int i = 0; i < _displays.Count; i++)
            {
                var d = _displays[i];
                string err = d.Apply(_originals[i], false);
                string key = d.ModeKey(_originals[i]);
                for (int j = 0; j < d.Modes.Count; j++)
                {
                    if (d.ModeKey(d.Modes[j]) == key) { _boxes[i].SelectedIndex = j; break; }
                }
                parts.Add("显示器" + (i + 1) + "  " + (err == null ? "已恢复" : "恢复失败 · " + err));
            }
            _status.Text = string.Join("        ", parts);
        }

        private void SaveIni()
        {
            var sb = new StringBuilder();
            for (int i = 0; i < _displays.Count; i++)
            {
                var item = _boxes[i].SelectedItem as ComboBoxItem;
                if (item == null) continue;
                int mi = (int)item.Tag;
                sb.AppendLine(_displays[i].DeviceName.ToUpperInvariant() + "=" + _displays[i].ModeKey(_displays[i].Modes[mi]));
            }
            try { File.WriteAllText(IniPath, sb.ToString(), new UTF8Encoding(false)); } catch { }
        }

        private static Dictionary<string, string> LoadIni()
        {
            var d = new Dictionary<string, string>();
            try
            {
                if (!File.Exists(IniPath)) return d;
                foreach (var line in File.ReadAllLines(IniPath))
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    d[line.Substring(0, eq).Trim().ToUpperInvariant()] = line.Substring(eq + 1).Trim();
                }
            }
            catch { }
            return d;
        }

        private static DropShadowEffect DropShadow(double opacity, double blur)
        {
            return new DropShadowEffect { Color = Colors.Black, BlurRadius = blur, ShadowDepth = 0, Opacity = opacity };
        }

        private static Color ColorFromHex(string hex)
        {
            return (Color)ColorConverter.ConvertFromString(hex);
        }
    }
}
