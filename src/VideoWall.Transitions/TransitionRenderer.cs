using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using VideoWall.Core.Enums;

namespace VideoWall.Transitions;

/// <summary>
/// Renders transition effects between two images/content using WPF
/// </summary>
public class TransitionRenderer
{
    /// <summary>
    /// Create a storyboard for the specified transition
    /// </summary>
    /// <param name="transitionType">Type of transition</param>
    /// <param name="durationMs">Duration in milliseconds</param>
    /// <param name="fromElement">Element transitioning out</param>
    /// <param name="toElement">Element transitioning in</param>
    /// <returns>Storyboard to animate the transition</returns>
    private static readonly TransitionType[] RandomPool =
    {
        TransitionType.Fade, TransitionType.FadeToBlack,
        TransitionType.WipeLeft, TransitionType.WipeRight, TransitionType.WipeUp, TransitionType.WipeDown,
        TransitionType.PushLeft, TransitionType.PushRight,
        TransitionType.SlideLeft, TransitionType.SlideRight,
        TransitionType.IrisOpen, TransitionType.ZoomIn, TransitionType.ZoomOut,
        TransitionType.Dissolve, TransitionType.Blur
    };

    private static TransitionType PickRandomTransition()
        => RandomPool[Random.Shared.Next(RandomPool.Length)];

    public Storyboard CreateTransition(
        TransitionType transitionType,
        int durationMs,
        FrameworkElement fromElement,
        FrameworkElement toElement)
    {
        // Resolve Random to a concrete transition
        if (transitionType == TransitionType.Random)
            transitionType = PickRandomTransition();

        var duration = TimeSpan.FromMilliseconds(durationMs);
        var storyboard = new Storyboard();

        switch (transitionType)
        {
            case TransitionType.Cut:
                CreateCutTransition(storyboard, fromElement, toElement);
                break;

            case TransitionType.Fade:
                CreateFadeTransition(storyboard, duration, fromElement, toElement);
                break;

            case TransitionType.FadeToBlack:
                CreateFadeToBlackTransition(storyboard, duration, fromElement, toElement);
                break;

            case TransitionType.WipeLeft:
                CreateWipeTransition(storyboard, duration, fromElement, toElement, WipeDirection.Left);
                break;

            case TransitionType.WipeRight:
                CreateWipeTransition(storyboard, duration, fromElement, toElement, WipeDirection.Right);
                break;

            case TransitionType.WipeUp:
                CreateWipeTransition(storyboard, duration, fromElement, toElement, WipeDirection.Up);
                break;

            case TransitionType.WipeDown:
                CreateWipeTransition(storyboard, duration, fromElement, toElement, WipeDirection.Down);
                break;

            case TransitionType.PushLeft:
                CreatePushTransition(storyboard, duration, fromElement, toElement, WipeDirection.Left);
                break;

            case TransitionType.PushRight:
                CreatePushTransition(storyboard, duration, fromElement, toElement, WipeDirection.Right);
                break;

            case TransitionType.PushUp:
                CreatePushTransition(storyboard, duration, fromElement, toElement, WipeDirection.Up);
                break;

            case TransitionType.PushDown:
                CreatePushTransition(storyboard, duration, fromElement, toElement, WipeDirection.Down);
                break;

            case TransitionType.SlideLeft:
            case TransitionType.SlideRight:
            case TransitionType.SlideUp:
            case TransitionType.SlideDown:
                CreateSlideTransition(storyboard, duration, fromElement, toElement,
                    GetWipeDirection(transitionType));
                break;

            case TransitionType.IrisOpen:
                CreateIrisTransition(storyboard, duration, fromElement, toElement, true);
                break;

            case TransitionType.IrisClose:
                CreateIrisTransition(storyboard, duration, fromElement, toElement, false);
                break;

            case TransitionType.ZoomIn:
                CreateZoomTransition(storyboard, duration, fromElement, toElement, true);
                break;

            case TransitionType.ZoomOut:
                CreateZoomTransition(storyboard, duration, fromElement, toElement, false);
                break;

            case TransitionType.Dissolve:
                CreateDissolveTransition(storyboard, duration, fromElement, toElement);
                break;

            case TransitionType.Blur:
                CreateBlurTransition(storyboard, duration, fromElement, toElement);
                break;

            default:
                // Default to fade
                CreateFadeTransition(storyboard, duration, fromElement, toElement);
                break;
        }

        return storyboard;
    }

    #region Transition Implementations

    private void CreateCutTransition(Storyboard storyboard, FrameworkElement from, FrameworkElement to)
    {
        // Instant cut - just hide/show
        var hideAnimation = new ObjectAnimationUsingKeyFrames();
        hideAnimation.KeyFrames.Add(new DiscreteObjectKeyFrame(Visibility.Collapsed, TimeSpan.Zero));
        Storyboard.SetTarget(hideAnimation, from);
        Storyboard.SetTargetProperty(hideAnimation, new PropertyPath(UIElement.VisibilityProperty));
        storyboard.Children.Add(hideAnimation);

        var showAnimation = new ObjectAnimationUsingKeyFrames();
        showAnimation.KeyFrames.Add(new DiscreteObjectKeyFrame(Visibility.Visible, TimeSpan.Zero));
        Storyboard.SetTarget(showAnimation, to);
        Storyboard.SetTargetProperty(showAnimation, new PropertyPath(UIElement.VisibilityProperty));
        storyboard.Children.Add(showAnimation);
    }

    private void CreateFadeTransition(Storyboard storyboard, TimeSpan duration, FrameworkElement from, FrameworkElement to)
    {
        // Fade out old
        var fadeOut = new DoubleAnimation(1, 0, duration)
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
        };
        Storyboard.SetTarget(fadeOut, from);
        Storyboard.SetTargetProperty(fadeOut, new PropertyPath(UIElement.OpacityProperty));
        storyboard.Children.Add(fadeOut);

        // Fade in new
        var fadeIn = new DoubleAnimation(0, 1, duration)
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
        };
        Storyboard.SetTarget(fadeIn, to);
        Storyboard.SetTargetProperty(fadeIn, new PropertyPath(UIElement.OpacityProperty));
        storyboard.Children.Add(fadeIn);
    }

    private void CreateFadeToBlackTransition(Storyboard storyboard, TimeSpan duration, FrameworkElement from, FrameworkElement to)
    {
        var halfDuration = TimeSpan.FromMilliseconds(duration.TotalMilliseconds / 2);

        // Fade out old to black
        var fadeOut = new DoubleAnimation(1, 0, halfDuration)
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
        };
        Storyboard.SetTarget(fadeOut, from);
        Storyboard.SetTargetProperty(fadeOut, new PropertyPath(UIElement.OpacityProperty));
        storyboard.Children.Add(fadeOut);

        // Fade in new from black
        var fadeIn = new DoubleAnimation(0, 1, halfDuration)
        {
            BeginTime = halfDuration,
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        Storyboard.SetTarget(fadeIn, to);
        Storyboard.SetTargetProperty(fadeIn, new PropertyPath(UIElement.OpacityProperty));
        storyboard.Children.Add(fadeIn);
    }

    private void CreateWipeTransition(Storyboard storyboard, TimeSpan duration, FrameworkElement from, FrameworkElement to, WipeDirection direction)
    {
        // The new element needs a clip that reveals it progressively
        to.Opacity = 1;

        double fromX = 0, fromY = 0, toX = 0, toY = 0;
        double fromWidth = 0, toWidth = 1;
        double fromHeight = 0, toHeight = 1;

        switch (direction)
        {
            case WipeDirection.Left:
                fromX = 1; toX = 0;
                fromWidth = 1; toWidth = 1;
                fromHeight = toHeight = 1;
                break;
            case WipeDirection.Right:
                fromX = 0; toX = 0;
                fromWidth = 0; toWidth = 1;
                fromHeight = toHeight = 1;
                break;
            case WipeDirection.Up:
                fromY = 1; toY = 0;
                fromWidth = toWidth = 1;
                fromHeight = 1; toHeight = 1;
                break;
            case WipeDirection.Down:
                fromY = 0; toY = 0;
                fromWidth = toWidth = 1;
                fromHeight = 0; toHeight = 1;
                break;
        }

        // Create clip rectangle animation using a RectAnimation
        var clipAnimation = CreateRectAnimation(duration, from, to, direction);
        storyboard.Children.Add(clipAnimation);

        // Fade out old element at end
        var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(1))
        {
            BeginTime = duration
        };
        Storyboard.SetTarget(fadeOut, from);
        Storyboard.SetTargetProperty(fadeOut, new PropertyPath(UIElement.OpacityProperty));
        storyboard.Children.Add(fadeOut);
    }

    private AnimationTimeline CreateRectAnimation(TimeSpan duration, FrameworkElement from, FrameworkElement to, WipeDirection direction)
    {
        // Use a DoubleAnimation on a clipping geometry
        // This is a simplified approach - in production, you'd use a proper clipping rect
        var anim = new DoubleAnimation(0, 1, duration)
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
        };

        // Store direction info for the clip update
        to.Tag = new WipeInfo { Direction = direction, Progress = 0 };

        Storyboard.SetTarget(anim, to);
        Storyboard.SetTargetProperty(anim, new PropertyPath("Tag.Progress"));

        return anim;
    }

    private void CreatePushTransition(Storyboard storyboard, TimeSpan duration, FrameworkElement from, FrameworkElement to, WipeDirection direction)
    {
        // Ensure transforms exist
        EnsureTranslateTransform(from, "FromTransform");
        EnsureTranslateTransform(to, "ToTransform");

        double fromEndX = 0, fromEndY = 0, toStartX = 0, toStartY = 0;

        switch (direction)
        {
            case WipeDirection.Left:
                fromEndX = -from.ActualWidth;
                toStartX = to.ActualWidth;
                break;
            case WipeDirection.Right:
                fromEndX = from.ActualWidth;
                toStartX = -to.ActualWidth;
                break;
            case WipeDirection.Up:
                fromEndY = -from.ActualHeight;
                toStartY = to.ActualHeight;
                break;
            case WipeDirection.Down:
                fromEndY = from.ActualHeight;
                toStartY = -to.ActualHeight;
                break;
        }

        // Animate old element out
        if (fromEndX != 0)
        {
            var slideOut = new DoubleAnimation(0, fromEndX, duration)
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
            };
            Storyboard.SetTarget(slideOut, from);
            Storyboard.SetTargetProperty(slideOut, new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.X)"));
            storyboard.Children.Add(slideOut);
        }
        if (fromEndY != 0)
        {
            var slideOut = new DoubleAnimation(0, fromEndY, duration)
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
            };
            Storyboard.SetTarget(slideOut, from);
            Storyboard.SetTargetProperty(slideOut, new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.Y)"));
            storyboard.Children.Add(slideOut);
        }

        // Animate new element in
        if (toStartX != 0)
        {
            var slideIn = new DoubleAnimation(toStartX, 0, duration)
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
            };
            Storyboard.SetTarget(slideIn, to);
            Storyboard.SetTargetProperty(slideIn, new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.X)"));
            storyboard.Children.Add(slideIn);
        }
        if (toStartY != 0)
        {
            var slideIn = new DoubleAnimation(toStartY, 0, duration)
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
            };
            Storyboard.SetTarget(slideIn, to);
            Storyboard.SetTargetProperty(slideIn, new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.Y)"));
            storyboard.Children.Add(slideIn);
        }
    }

    private void CreateSlideTransition(Storyboard storyboard, TimeSpan duration, FrameworkElement from, FrameworkElement to, WipeDirection direction)
    {
        // New element slides over old (old doesn't move)
        EnsureTranslateTransform(to, "ToTransform");

        double toStartX = 0, toStartY = 0;

        switch (direction)
        {
            case WipeDirection.Left:
                toStartX = to.ActualWidth;
                break;
            case WipeDirection.Right:
                toStartX = -to.ActualWidth;
                break;
            case WipeDirection.Up:
                toStartY = to.ActualHeight;
                break;
            case WipeDirection.Down:
                toStartY = -to.ActualHeight;
                break;
        }

        if (toStartX != 0)
        {
            var slideIn = new DoubleAnimation(toStartX, 0, duration)
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
            };
            Storyboard.SetTarget(slideIn, to);
            Storyboard.SetTargetProperty(slideIn, new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.X)"));
            storyboard.Children.Add(slideIn);
        }
        if (toStartY != 0)
        {
            var slideIn = new DoubleAnimation(toStartY, 0, duration)
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
            };
            Storyboard.SetTarget(slideIn, to);
            Storyboard.SetTargetProperty(slideIn, new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.Y)"));
            storyboard.Children.Add(slideIn);
        }

        // Hide old at end
        var hide = new ObjectAnimationUsingKeyFrames();
        hide.KeyFrames.Add(new DiscreteObjectKeyFrame(Visibility.Collapsed, duration));
        Storyboard.SetTarget(hide, from);
        Storyboard.SetTargetProperty(hide, new PropertyPath(UIElement.VisibilityProperty));
        storyboard.Children.Add(hide);
    }

    private void CreateIrisTransition(Storyboard storyboard, TimeSpan duration, FrameworkElement from, FrameworkElement to, bool opening)
    {
        // Iris uses a circular clip that expands/contracts
        // Simplified with scale transform
        EnsureScaleTransform(to, "ToScale");

        to.RenderTransformOrigin = new Point(0.5, 0.5);

        if (opening)
        {
            // Start small, grow to full
            var scaleX = new DoubleAnimation(0, 1, duration)
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            Storyboard.SetTarget(scaleX, to);
            Storyboard.SetTargetProperty(scaleX, new PropertyPath("(UIElement.RenderTransform).(ScaleTransform.ScaleX)"));
            storyboard.Children.Add(scaleX);

            var scaleY = new DoubleAnimation(0, 1, duration)
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            Storyboard.SetTarget(scaleY, to);
            Storyboard.SetTargetProperty(scaleY, new PropertyPath("(UIElement.RenderTransform).(ScaleTransform.ScaleY)"));
            storyboard.Children.Add(scaleY);
        }

        // Fade out old
        var fadeOut = new DoubleAnimation(1, 0, duration);
        Storyboard.SetTarget(fadeOut, from);
        Storyboard.SetTargetProperty(fadeOut, new PropertyPath(UIElement.OpacityProperty));
        storyboard.Children.Add(fadeOut);
    }

    private void CreateZoomTransition(Storyboard storyboard, TimeSpan duration, FrameworkElement from, FrameworkElement to, bool zoomIn)
    {
        EnsureScaleTransform(from, "FromScale");
        EnsureScaleTransform(to, "ToScale");

        from.RenderTransformOrigin = new Point(0.5, 0.5);
        to.RenderTransformOrigin = new Point(0.5, 0.5);

        if (zoomIn)
        {
            // Old zooms in and fades, new appears
            var scaleOutX = new DoubleAnimation(1, 1.5, duration);
            Storyboard.SetTarget(scaleOutX, from);
            Storyboard.SetTargetProperty(scaleOutX, new PropertyPath("(UIElement.RenderTransform).(ScaleTransform.ScaleX)"));
            storyboard.Children.Add(scaleOutX);

            var scaleOutY = new DoubleAnimation(1, 1.5, duration);
            Storyboard.SetTarget(scaleOutY, from);
            Storyboard.SetTargetProperty(scaleOutY, new PropertyPath("(UIElement.RenderTransform).(ScaleTransform.ScaleY)"));
            storyboard.Children.Add(scaleOutY);
        }
        else
        {
            // New zooms in from far
            var scaleInX = new DoubleAnimation(2, 1, duration)
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            Storyboard.SetTarget(scaleInX, to);
            Storyboard.SetTargetProperty(scaleInX, new PropertyPath("(UIElement.RenderTransform).(ScaleTransform.ScaleX)"));
            storyboard.Children.Add(scaleInX);

            var scaleInY = new DoubleAnimation(2, 1, duration)
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            Storyboard.SetTarget(scaleInY, to);
            Storyboard.SetTargetProperty(scaleInY, new PropertyPath("(UIElement.RenderTransform).(ScaleTransform.ScaleY)"));
            storyboard.Children.Add(scaleInY);
        }

        // Cross-fade
        CreateFadeTransition(storyboard, duration, from, to);
    }

    private void CreateDissolveTransition(Storyboard storyboard, TimeSpan duration, FrameworkElement from, FrameworkElement to)
    {
        // Dissolve is similar to fade but could use pixelation effect
        // For now, use simple cross-fade
        CreateFadeTransition(storyboard, duration, from, to);
    }

    private void CreateBlurTransition(Storyboard storyboard, TimeSpan duration, FrameworkElement from, FrameworkElement to)
    {
        var halfDuration = TimeSpan.FromMilliseconds(duration.TotalMilliseconds / 2);

        // Add blur effect to both elements
        var fromBlur = new BlurEffect { Radius = 0 };
        var toBlur = new BlurEffect { Radius = 20 };
        from.Effect = fromBlur;
        to.Effect = toBlur;

        // Blur out old
        var blurOut = new DoubleAnimation(0, 20, halfDuration);
        Storyboard.SetTarget(blurOut, from);
        Storyboard.SetTargetProperty(blurOut, new PropertyPath("(UIElement.Effect).(BlurEffect.Radius)"));
        storyboard.Children.Add(blurOut);

        // Fade out old
        var fadeOut = new DoubleAnimation(1, 0, halfDuration);
        Storyboard.SetTarget(fadeOut, from);
        Storyboard.SetTargetProperty(fadeOut, new PropertyPath(UIElement.OpacityProperty));
        storyboard.Children.Add(fadeOut);

        // Blur in new (unblur)
        var blurIn = new DoubleAnimation(20, 0, halfDuration)
        {
            BeginTime = halfDuration
        };
        Storyboard.SetTarget(blurIn, to);
        Storyboard.SetTargetProperty(blurIn, new PropertyPath("(UIElement.Effect).(BlurEffect.Radius)"));
        storyboard.Children.Add(blurIn);

        // Fade in new
        var fadeIn = new DoubleAnimation(0, 1, halfDuration)
        {
            BeginTime = halfDuration
        };
        Storyboard.SetTarget(fadeIn, to);
        Storyboard.SetTargetProperty(fadeIn, new PropertyPath(UIElement.OpacityProperty));
        storyboard.Children.Add(fadeIn);
    }

    #endregion

    #region Helpers

    private void EnsureTranslateTransform(FrameworkElement element, string name)
    {
        if (element.RenderTransform is not TranslateTransform)
        {
            element.RenderTransform = new TranslateTransform();
        }
    }

    private void EnsureScaleTransform(FrameworkElement element, string name)
    {
        if (element.RenderTransform is not ScaleTransform)
        {
            element.RenderTransform = new ScaleTransform(1, 1);
        }
    }

    private WipeDirection GetWipeDirection(TransitionType type)
    {
        return type switch
        {
            TransitionType.WipeLeft or TransitionType.SlideLeft or TransitionType.PushLeft => WipeDirection.Left,
            TransitionType.WipeRight or TransitionType.SlideRight or TransitionType.PushRight => WipeDirection.Right,
            TransitionType.WipeUp or TransitionType.SlideUp or TransitionType.PushUp => WipeDirection.Up,
            TransitionType.WipeDown or TransitionType.SlideDown or TransitionType.PushDown => WipeDirection.Down,
            _ => WipeDirection.Right
        };
    }

    #endregion
}

/// <summary>
/// Wipe direction
/// </summary>
public enum WipeDirection
{
    Left,
    Right,
    Up,
    Down
}

/// <summary>
/// Info for wipe clipping
/// </summary>
public class WipeInfo
{
    public WipeDirection Direction { get; set; }
    public double Progress { get; set; }
}
