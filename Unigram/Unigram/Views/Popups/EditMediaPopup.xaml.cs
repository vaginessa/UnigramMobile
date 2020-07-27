﻿using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Geometry;
using System;
using System.Collections.Generic;
using System.Numerics;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Converters;
using Unigram.Entities;
using Unigram.Services;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Views.Popups
{
    public sealed partial class EditMediaPopup : OverlayPage
    {
        public StorageFile Result { get; private set; }
        public StorageMedia ResultMedia { get; private set; }

        private StorageFile _file;
        private StorageMedia _media;

        private BitmapRotation _rotation = BitmapRotation.None;
        private BitmapFlip _flip = BitmapFlip.None;

        public EditMediaPopup(StorageFile file, BitmapProportions proportions = BitmapProportions.Custom, ImageCropperMask mask = ImageCropperMask.Rectangle)
        {
            InitializeComponent();
            //TODO!!
            Cropper.SetMask(mask);
            Cropper.SetProportions(proportions);

            if (proportions != BitmapProportions.Custom)
            {
                Proportions.IsChecked = true;
                Proportions.IsEnabled = false;
            }

            _file = file;

            Loaded += async (s, args) =>
            {
                await Cropper.SetSourceAsync(file, proportions: proportions);
            };
        }
        /* Stays here for easier merging with the original:
        public EditMediaPopup(StorageMedia media)
        {
            InitializeComponent();

            Canvas.Strokes = media.EditState.Strokes;

            Cropper.SetMask(ImageCropperMask.Rectangle);
            Cropper.SetProportions(media.EditState.Proportions);

            if (media.EditState.Proportions != BitmapProportions.Custom)
            {
                Proportions.IsChecked = true;
                Proportions.IsEnabled = true;
            }

            _file = media.File;
            _media = media;

            Loaded += async (s, args) =>
            {
                await Cropper.SetSourceAsync(media.File, media.EditState.Rotation, media.EditState.Flip, media.EditState.Proportions, media.EditState.Rectangle);
            };
        }
        */
        public EditMediaPopup(StorageMedia media, bool ttl)
        {
            InitializeComponent();

            Canvas.Strokes = media.EditState.Strokes;

            _file = media.File;
            _media = media;

            Loaded += async (s, args) =>
            {
                await Cropper.SetSourceAsync(media.File, media.EditState.Rotation, media.EditState.Flip, media.EditState.Proportions, media.EditState.Rectangle);
                Cropper.IsCropEnabled = false;
            };

            if (_media is StorageVideo video)
            {
                Mute.IsChecked = video.IsMuted;
                Mute.Visibility = Visibility.Visible;
            }
            else
            {
                Mute.Visibility = Visibility.Collapsed;
            }

            Crop.Visibility = _media is StoragePhoto ? Visibility.Visible : Visibility.Collapsed;
            Draw.Visibility = Crop.Visibility;
            Ttl.Visibility = ttl ? Visibility.Visible : Visibility.Collapsed;
            Ttl.IsChecked = _media.Ttl > 0;

            if (_media.EditState is BitmapEditState editSate)
            {
                Crop.IsChecked = editSate.Proportions != BitmapProportions.Custom ||
                                    editSate.Flip != BitmapFlip.None ||
                                    editSate.Rotation != BitmapRotation.None ||
                                    (editSate.Rectangle != null && !editSate.Rectangle.IsEmpty && 
                                    (editSate.Rectangle.X > 0 || editSate.Rectangle.Y > 0 ||
                                    editSate.Rectangle.Width != 1 || editSate.Rectangle.Height != 1));

                Draw.IsChecked = editSate.Strokes != null && editSate.Strokes.Count > 0;
            } else
            {
                Crop.IsChecked = false;
                Draw.IsChecked = false;
            }
        }

        public bool IsCropEnabled
        {
            get { return this.Cropper.IsCropEnabled; }
            set { this.Cropper.IsCropEnabled = value; }
        }

        public Rect CropRectangle
        {
            get { return this.Cropper.CropRectangle; }
        }

        private async void Accept_Click(object sender, RoutedEventArgs e)
        {
            if (CropToolbar != null && CropToolbar.Visibility == Visibility.Visible)
            {
                if (_media != null)
                {
                    var rect = Cropper.CropRectangle;
                    //var w = Cropper.PixelWidth;
                    //var h = Cropper.PixelHeight;

                    _media.EditState = new BitmapEditState
                    {
                        //Rectangle = new Rect(rect.X * w, rect.Y * h, rect.Width * w, rect.Height * h),
                        Rectangle = rect,
                        Proportions = Cropper.Proportions,
                        Strokes = Canvas.Strokes,
                        Flip = _flip,
                        Rotation = _rotation
                    };

                    ResetUiVisibility(); //Hide(ContentDialogResult.Primary);
                    Crop.IsChecked = Cropper.Proportions != BitmapProportions.Custom ||
                                        _flip != BitmapFlip.None ||
                                        _rotation != BitmapRotation.None ||
                                        (rect != null && !rect.IsEmpty && 
                                        (rect.X > 0 || rect.Y > 0 || 
                                        rect.Width != 1 || rect.Height != 1));
                    return;
                }

                var cropped = await Cropper.CropAsync();

                var drawing = Canvas.Strokes;
                if (drawing != null && drawing.Count > 0)
                {
                    cropped = await ImageHelper.DrawStrokesAsync(cropped, drawing, Cropper.CropRectangle, _rotation, _flip);
                }

                Result = cropped;
                Hide(ContentDialogResult.Primary);
            }
            else if (DrawToolbar != null && DrawToolbar.Visibility == Visibility.Visible)
            {
                Canvas.SaveState();
                _media.EditState.Strokes = Canvas.Strokes;

                Draw.IsChecked = Canvas.Strokes != null && Canvas.Strokes.Count > 0;
                ResetUiVisibility();

                SettingsService.Current.Pencil = DrawSlider.GetDefault();
            }
            else
            {
                ResetUiVisibility();
                Hide(ContentDialogResult.Primary);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            if (CropToolbar != null && CropToolbar.Visibility == Visibility.Visible)
            {
                ResetUiVisibility();
            }
            else if(DrawToolbar != null && DrawToolbar.Visibility == Visibility.Visible)
            {
                Canvas.RestoreState();

                ResetUiVisibility();
                SettingsService.Current.Pencil = DrawSlider.GetDefault();
            }
            else
            {
                ResetUiVisibility();
                Hide(ContentDialogResult.Secondary);
            }
        }

        private void ResetUiVisibility()
        {
            Cropper.IsCropEnabled = false;
            Canvas.IsEnabled = false;
            BasicToolbar.Visibility = Visibility.Visible;
            if (CropToolbar != null)
                CropToolbar.Visibility = Visibility.Collapsed;
            if (DrawToolbar != null)
                DrawToolbar.Visibility = Visibility.Collapsed;
            if (DrawSlider != null)
                DrawSlider.Visibility = Visibility.Collapsed;
        }

        private void Proportions_Click(object sender, RoutedEventArgs e)
        {
            if (Cropper.Proportions != BitmapProportions.Custom)
            {
                Cropper.SetProportions(BitmapProportions.Custom);
                Proportions.IsChecked = false;
            }
            else
            {
                var flyout = new MenuFlyout();
                var items = Cropper.GetProportions();

                var handler = new RoutedEventHandler((s, args) =>
                {
                    if (s is MenuFlyoutItem option)
                    {
                        Cropper.SetProportions((BitmapProportions)option.Tag);
                        Proportions.IsChecked = true;
                    }
                });

                foreach (var item in items)
                {
                    var option = new MenuFlyoutItem();
                    option.Click += handler;
                    option.Text = ProportionsToLabelConverter.Convert(item);
                    option.Tag = item;
                    option.MinWidth = 140;
                    option.HorizontalContentAlignment = HorizontalAlignment.Center;

                    flyout.Items.Add(option);
                }

                if (flyout.Items.Count > 0)
                {
                    flyout.ShowAt((GlyphToggleButton)sender);
                }
            }
        }

        private async void Rotate_Click(object sender, RoutedEventArgs e)
        {
            var rotation = BitmapRotation.None;

            var proportions = RotateProportions(Cropper.Proportions);
            var rectangle = RotateArea(Cropper.CropRectangle);

            switch (_rotation)
            {
                case BitmapRotation.None:
                    rotation = BitmapRotation.Clockwise90Degrees;
                    break;
                case BitmapRotation.Clockwise90Degrees:
                    rotation = BitmapRotation.Clockwise180Degrees;
                    break;
                case BitmapRotation.Clockwise180Degrees:
                    rotation = BitmapRotation.Clockwise270Degrees;
                    break;
            }

            _rotation = rotation;
            await Cropper.SetSourceAsync(_file, rotation, _flip, proportions, rectangle);

            Rotate.IsChecked = _rotation != BitmapRotation.None;
            Canvas.Invalidate();
        }

        private Rect RotateArea(Rect area)
        {
            var point = new Point(1 - area.Bottom, 1 - (1 - area.X));
            var result = new Rect(point.X, point.Y, area.Height, area.Width);

            return result;
        }

        private BitmapProportions RotateProportions(BitmapProportions proportions)
        {
            switch (proportions)
            {
                case BitmapProportions.Original:
                case BitmapProportions.Square:
                default:
                    return proportions;
                // Portrait
                case BitmapProportions.TwoOverThree:
                    return BitmapProportions.ThreeOverTwo;
                case BitmapProportions.ThreeOverFive:
                    return BitmapProportions.FiveOverThree;
                case BitmapProportions.ThreeOverFour:
                    return BitmapProportions.FourOverThree;
                case BitmapProportions.FourOverFive:
                    return BitmapProportions.FiveOverFour;
                case BitmapProportions.FiveOverSeven:
                    return BitmapProportions.SevenOverFive;
                case BitmapProportions.NineOverSixteen:
                    return BitmapProportions.SixteenOverNine;
                // Landscape
                case BitmapProportions.ThreeOverTwo:
                    return BitmapProportions.TwoOverThree;
                case BitmapProportions.FiveOverThree:
                    return BitmapProportions.ThreeOverFive;
                case BitmapProportions.FourOverThree:
                    return BitmapProportions.ThreeOverFour;
                case BitmapProportions.FiveOverFour:
                    return BitmapProportions.FourOverFive;
                case BitmapProportions.SevenOverFive:
                    return BitmapProportions.FiveOverSeven;
                case BitmapProportions.SixteenOverNine:
                    return BitmapProportions.NineOverSixteen;
            }
        }

        private async void Flip_Click(object sender, RoutedEventArgs e)
        {
            var flip = _flip;
            var rotation = _rotation;

            var proportions = Cropper.Proportions;
            var rectangle = FlipArea(Cropper.CropRectangle);

            switch (rotation)
            {
                case BitmapRotation.Clockwise90Degrees:
                case BitmapRotation.Clockwise270Degrees:
                    switch (flip)
                    {
                        case BitmapFlip.None:
                            flip = BitmapFlip.Vertical;
                            break;
                        case BitmapFlip.Vertical:
                            flip = BitmapFlip.None;
                            break;
                        case BitmapFlip.Horizontal:
                            flip = BitmapFlip.None;
                            rotation = rotation == BitmapRotation.Clockwise90Degrees
                                ? BitmapRotation.Clockwise270Degrees
                                : BitmapRotation.Clockwise90Degrees;
                            break;
                    }
                    break;
                case BitmapRotation.None:
                case BitmapRotation.Clockwise180Degrees:
                    switch (flip)
                    {
                        case BitmapFlip.None:
                            flip = BitmapFlip.Horizontal;
                            break;
                        case BitmapFlip.Horizontal:
                            flip = BitmapFlip.None;
                            break;
                        case BitmapFlip.Vertical:
                            flip = BitmapFlip.None;
                            rotation = rotation == BitmapRotation.None
                                ? BitmapRotation.Clockwise180Degrees
                                : BitmapRotation.None;
                            break;
                    }
                    break;
            }

            _flip = flip;
            _rotation = rotation;
            await Cropper.SetSourceAsync(_file, _rotation, flip, proportions, rectangle);

            //Transform.ScaleX = _flip == BitmapFlip.Horizontal ? -1 : 1;
            //Transform.ScaleY = _flip == BitmapFlip.Vertical ? -1 : 1;

            Flip.IsChecked = _flip != BitmapFlip.None;
            Canvas.Invalidate();
        }

        private Rect FlipArea(Rect area)
        {
            var point = new Point(1 - area.Right, area.Y);
            var result = new Rect(point.X, point.Y, area.Width, area.Height);

            return result;
        }

        private void Mute_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Windows.UI.Xaml.Controls.Primitives.ToggleButton;
            if (_media is StorageVideo video)
            {
                button.IsChecked = !button.IsChecked == true;
                video.IsMuted = button.IsChecked == true;
            }
        }

        private void Crop_Click(object sender, RoutedEventArgs e)
        {
            ResetUiVisibility();
            Cropper.Visibility = Visibility.Visible;
            Cropper.SetMask(ImageCropperMask.Rectangle);
            Cropper.SetProportions(_media.EditState.Proportions);
            Cropper.IsCropEnabled = true;
            BasicToolbar.Visibility = Visibility.Collapsed;

            if (CropToolbar == null)
                FindName(nameof(CropToolbar));
            CropToolbar.Visibility = Visibility.Visible;

            if (_media.EditState.Proportions != BitmapProportions.Custom)
            {
                Proportions.IsChecked = true;
                Proportions.IsEnabled = true;
            }
        }

        private void Draw_Click(object sender, RoutedEventArgs e)
        {
            ResetUiVisibility();
            Canvas.IsEnabled = true;

            BasicToolbar.Visibility = Visibility.Collapsed;

            if (DrawToolbar == null)
                FindName(nameof(DrawToolbar));
            if (DrawSlider == null)
                FindName(nameof(DrawSlider));

            DrawToolbar.Visibility = Visibility.Visible;
            DrawSlider.Visibility = Visibility.Visible;
            DrawSlider.SetDefault(SettingsService.Current.Pencil);

            Canvas.Mode = PencilCanvasMode.Stroke;
            Canvas.Stroke = DrawSlider.Stroke;
            Canvas.StrokeThickness = DrawSlider.StrokeThickness;

            Brush.IsChecked = true;
            Erase.IsChecked = false;

            if (_media.EditState.Proportions != BitmapProportions.Custom)
            {
                Proportions.IsChecked = true;
                Proportions.IsEnabled = true;
            }
        }

        private void Ttl_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Windows.UI.Xaml.Controls.Primitives.ToggleButton;
            var media = _media as StorageMedia;

            var slider = new Slider();
            slider.IsThumbToolTipEnabled = false;
            slider.Header = MessageTtlConverter.Convert(MessageTtlConverter.ConvertSeconds(media.Ttl));
            slider.Minimum = 0;
            slider.Maximum = 28;
            slider.StepFrequency = 1;
            slider.SmallChange = 1;
            slider.LargeChange = 1;
            slider.Value = MessageTtlConverter.ConvertSeconds(media.Ttl);
            slider.ValueChanged += (s, args) =>
            {
                var index = (int)args.NewValue;
                var label = MessageTtlConverter.Convert(index);

                slider.Header = label;
                media.Ttl = MessageTtlConverter.ConvertBack(index);
                Ttl.IsChecked = media.Ttl > 0;
            };

            var text = new TextBlock();
            text.Style = App.Current.Resources["InfoCaptionTextBlockStyle"] as Style;
            text.TextWrapping = TextWrapping.Wrap;
            text.Text = media is StoragePhoto
                ? Strings.Resources.MessageLifetimePhoto
                : Strings.Resources.MessageLifetimeVideo;

            var stack = new StackPanel();
            stack.Width = 260;
            stack.Children.Add(slider);
            stack.Children.Add(text);

            var flyout = new Flyout();
            flyout.Content = stack;

            if (ApiInfo.CanUseNewFlyoutPlacementMode)
            {
                flyout.ShowAt(button.Parent as UIElement, new Windows.UI.Xaml.Controls.Primitives.FlyoutShowOptions { Placement = Windows.UI.Xaml.Controls.Primitives.FlyoutPlacementMode.TopEdgeAlignedRight });
            }
            else
            {
                flyout.ShowAt(button.Parent as FrameworkElement);
            }

        }

        private void Brush_Click(object sender, RoutedEventArgs e)
        {
            if (Canvas.Mode != PencilCanvasMode.Stroke)
            {
                Canvas.Mode = PencilCanvasMode.Stroke;
                Brush.IsChecked = true;
                Erase.IsChecked = false;
            }
        }

        private void Erase_Click(object sender, RoutedEventArgs e)
        {
            if (Canvas.Mode != PencilCanvasMode.Eraser)
            {
                Canvas.Mode = PencilCanvasMode.Eraser;
                Brush.IsChecked = false;
                Erase.IsChecked = true;
            }
        }

        private void Undo_Click(object sender, RoutedEventArgs e)
        {
            Canvas.Undo();
        }

        private void Redo_Click(object sender, RoutedEventArgs e)
        {
            Canvas.Redo();
        }

        private void DrawSlider_StrokeChanged(object sender, EventArgs e)
        {
            Canvas.Stroke = DrawSlider.Stroke;
            Canvas.StrokeThickness = DrawSlider.StrokeThickness;

            Brush_Click(null, null);
        }

        private void Canvas_StrokesChanged(object sender, EventArgs e)
        {
            InvalidateToolbar();
        }

        private void InvalidateToolbar()
        {
            if (Undo != null)
            {
                Undo.IsEnabled = Canvas.CanUndo;
            }

            if (Redo != null)
            {
                Redo.IsEnabled = Canvas.CanRedo;
            }
        }
    }

    public sealed class SmoothPathBuilder
    {
        private List<Vector2> _controlPoints;
        private List<Vector2> _path;

        private Vector2 _beginPoint;

        public SmoothPathBuilder(Vector2 beginPoint)
        {
            _beginPoint = beginPoint;

            _controlPoints = new List<Vector2>();
            _path = new List<Vector2>();
        }

        public Color? Stroke { get; set; }
        public float StrokeThickness { get; set; }

        public void MoveTo(Vector2 point)
        {
            if (_controlPoints.Count < 4)
            {
                _controlPoints.Add(point);
                return;
            }

            var endPoint = new Vector2(
                (_controlPoints[2].X + point.X) / 2,
                (_controlPoints[2].Y + point.Y) / 2);

            _path.Add(_controlPoints[1]);
            _path.Add(_controlPoints[2]);
            _path.Add(endPoint);

            _controlPoints = new List<Vector2> { endPoint, point };
        }

        public void EndFigure(Vector2 point)
        {
            if (_controlPoints.Count > 1)
            {
                for (int i = 0; i < _controlPoints.Count; i++)
                {
                    MoveTo(point);
                }
            }
        }

        public CanvasGeometry ToGeometry(ICanvasResourceCreator resourceCreator, Vector2 canvasSize)
        {
            //var multiplier = NSMakePoint(imageSize.width / touch.canvasSize.width, imageSize.height / touch.canvasSize.height)
            var multiplier = canvasSize; //_imageSize / canvasSize;

            var builder = new CanvasPathBuilder(resourceCreator);
            builder.BeginFigure(_beginPoint * multiplier);

            for (int i = 0; i < _path.Count; i += 3)
            {
                builder.AddCubicBezier(
                    _path[i] * multiplier,
                    _path[i + 1] * multiplier,
                    _path[i + 2] * multiplier);
            }

            builder.EndFigure(CanvasFigureLoop.Open);

            return CanvasGeometry.CreatePath(builder);
        }
    }
}
