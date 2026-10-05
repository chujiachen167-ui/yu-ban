using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Automation;
namespace EnglishCompanion {
    // 使用原生输入控件模板，避免自定义 ContentHost 裁剪文本与光标。
    internal sealed class KeyEntry : Grid {
        readonly PasswordBox password = new PasswordBox();
        readonly TextBox visible = new TextBox();
        readonly TextBlock hint = new TextBlock();
        readonly ToggleButton eye = new ToggleButton();
        readonly Border frame = new Border();
        readonly System.Windows.Shapes.Path eyeSlash = new System.Windows.Shapes.Path();
        bool synchronizing;
        Skin skin=Skin.Get("glass");
        internal event Action Changed;
        internal string Value { get { return password.Password; } set { Set(value ?? ""); } }
        internal KeyEntry(string name) {
            Height=52;
            frame.CornerRadius=new CornerRadius(13); frame.Background=Skin.Brush("#FAFFFFFF"); frame.BorderBrush=Skin.Brush("#C6D4DB"); frame.BorderThickness=new Thickness(1);
            Children.Add(frame);
            var layout=new Grid(); layout.ColumnDefinitions.Add(new ColumnDefinition()); layout.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(44)}); frame.Child=layout;
            password.FontSize=16; password.FontFamily=new FontFamily("Segoe UI"); password.Foreground=Skin.Brush("#233C45"); password.CaretBrush=password.Foreground; password.Background=Brushes.Transparent; password.BorderThickness=new Thickness(0); password.Padding=new Thickness(14,0,4,0); password.VerticalContentAlignment=VerticalAlignment.Center; password.MaxLength=2048;
            visible.FontSize=16; visible.FontFamily=password.FontFamily; visible.Foreground=password.Foreground; visible.CaretBrush=password.Foreground; visible.Background=Brushes.Transparent; visible.BorderThickness=new Thickness(0); visible.Padding=password.Padding; visible.VerticalContentAlignment=VerticalAlignment.Center; visible.MaxLength=2048; visible.Visibility=Visibility.Collapsed;
            AutomationProperties.SetName(password,name); AutomationProperties.SetName(visible,name+"（已显示）");
            layout.Children.Add(password); layout.Children.Add(visible);
            hint.Text="请输入 API Key"; hint.FontSize=14; hint.Foreground=Skin.Brush("#75858E"); hint.Margin=new Thickness(14,0,0,0); hint.VerticalAlignment=VerticalAlignment.Center; hint.IsHitTestVisible=false; layout.Children.Add(hint);
            var icon=new Grid {Width=24,Height=24,IsHitTestVisible=false};
            icon.Children.Add(new System.Windows.Shapes.Path {Data=Geometry.Parse("M2,12 C4.3,7.8 7.5,5.8 12,5.8 C16.5,5.8 19.7,7.8 22,12 C19.7,16.2 16.5,18.2 12,18.2 C7.5,18.2 4.3,16.2 2,12 Z"),Stroke=Skin.Brush("#50688A"),StrokeThickness=1.5,StrokeLineJoin=PenLineJoin.Round});
            icon.Children.Add(new System.Windows.Shapes.Ellipse {Width=6.5,Height=6.5,Stroke=Skin.Brush("#50688A"),StrokeThickness=1.5,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center});
            eyeSlash.Data=Geometry.Parse("M4,3 L20,21"); eyeSlash.Stroke=Skin.Brush("#50688A"); eyeSlash.StrokeThickness=1.7; eyeSlash.StrokeStartLineCap=eyeSlash.StrokeEndLineCap=PenLineCap.Round; eyeSlash.Visibility=Visibility.Collapsed; icon.Children.Add(eyeSlash);
            eye.Content=icon; eye.Background=Brushes.Transparent; eye.BorderThickness=new Thickness(0); eye.Margin=new Thickness(0,6,6,6); eye.Cursor=Cursors.Hand; eye.ToolTip="显示 API Key"; Grid.SetColumn(eye,1); layout.Children.Add(eye); AutomationProperties.SetName(eye,"显示或隐藏"+name);
            var template=new ControlTemplate(typeof(ToggleButton)); var border=new FrameworkElementFactory(typeof(Border)); border.Name="EyeBackground"; border.SetValue(Border.CornerRadiusProperty,new CornerRadius(9)); border.SetValue(Border.BackgroundProperty,Brushes.Transparent); var presenter=new FrameworkElementFactory(typeof(ContentPresenter)); presenter.SetValue(HorizontalAlignmentProperty,HorizontalAlignment.Center); presenter.SetValue(VerticalAlignmentProperty,VerticalAlignment.Center); border.AppendChild(presenter); template.VisualTree=border;
            var hover=new Trigger {Property=IsMouseOverProperty,Value=true}; hover.Setters.Add(new Setter(Border.BackgroundProperty,new DynamicResourceExtension("Hover"),"EyeBackground")); template.Triggers.Add(hover); eye.Template=template;
            password.PasswordChanged+=delegate { if(!synchronizing) Set(password.Password); };
            visible.TextChanged+=delegate { if(!synchronizing && eye.IsChecked==true) Set(visible.Text); };
            eye.Checked+=delegate { eyeSlash.Visibility=Visibility.Visible; eye.ToolTip="隐藏 API Key"; synchronizing=true; visible.Text=password.Password; synchronizing=false; visible.Visibility=Visibility.Visible; password.Visibility=Visibility.Collapsed; visible.Focus(); visible.CaretIndex=visible.Text.Length; };
            eye.Unchecked+=delegate { eyeSlash.Visibility=Visibility.Collapsed; eye.ToolTip="显示 API Key"; visible.Visibility=Visibility.Collapsed; synchronizing=true; visible.Text=""; synchronizing=false; password.Visibility=Visibility.Visible; if(Window.GetWindow(this)!=null && Window.GetWindow(this).IsActive) password.Focus(); };
            IsKeyboardFocusWithinChanged+=delegate { UpdateFrame(); };
            Set("");
        }
        void UpdateFrame() { frame.BorderThickness=new Thickness(IsKeyboardFocusWithin?2:1); frame.BorderBrush=Skin.Brush(IsKeyboardFocusWithin?skin.Focus:skin.FieldEdge); }
        internal void ApplySkin(Skin value) {
            skin=value; frame.Background=Skin.Brush(skin.Field);
            password.Foreground=visible.Foreground=password.CaretBrush=visible.CaretBrush=Skin.Brush(skin.Ink);
            password.SelectionBrush=visible.SelectionBrush=Skin.Brush(skin.Focus);
            hint.Foreground=Skin.Brush(skin.Muted);
            foreach(var item in ((Grid)eye.Content).Children) {var shape=item as System.Windows.Shapes.Shape;if(shape!=null)shape.Stroke=Skin.Brush(skin.Muted);}
            eye.Resources["Hover"]=Skin.Brush(skin.Hover);
            UpdateFrame();
        }
        void Set(string value) {
            synchronizing=true;
            if(password.Password!=value) password.Password=value;
            if(eye.IsChecked==true && visible.Text!=value) { int caret=visible.CaretIndex; visible.Text=value; visible.CaretIndex=Math.Min(caret,value.Length); }
            hint.Visibility=value.Length==0?Visibility.Visible:Visibility.Collapsed;
            synchronizing=false; if(Changed!=null) Changed();
        }
        internal void SetEnabled(bool enabled) { IsEnabled=enabled; hint.Text=enabled?"请输入 API Key":"系统语音无需 Key"; }
        internal void FocusInput() { if(eye.IsChecked==true) visible.Focus(); else password.Focus(); }
        internal void HideSecret() { eye.IsChecked=false; }
        internal void Clear() { Value=""; }
    }
}
